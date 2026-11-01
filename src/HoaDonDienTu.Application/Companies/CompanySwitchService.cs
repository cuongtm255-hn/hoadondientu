using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Companies
{
    /// <summary>Chuyển MST theo thứ tự bắt buộc của DD-01 mục 9.3.</summary>
    public sealed class CompanySwitchService : ICompanySwitchService
    {
        private readonly ICompanyCatalog _catalog;
        private readonly ICompanyContextManager _contextManager;
        private readonly IActiveJobService _activeJobs;
        private readonly IPortalSessionStore _portalSession;
        private readonly IEntitlementService _entitlement;
        private readonly IApplicationLogger _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public CompanySwitchService(
            ICompanyCatalog catalog,
            ICompanyContextManager contextManager,
            IActiveJobService activeJobs,
            IPortalSessionStore portalSession,
            IEntitlementService entitlement,
            IApplicationLogger logger)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (contextManager == null) throw new ArgumentNullException("contextManager");
            if (activeJobs == null) throw new ArgumentNullException("activeJobs");
            if (portalSession == null) throw new ArgumentNullException("portalSession");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (logger == null) throw new ArgumentNullException("logger");
            _catalog = catalog;
            _contextManager = contextManager;
            _activeJobs = activeJobs;
            _portalSession = portalSession;
            _entitlement = entitlement;
            _logger = logger;
        }

        public async Task<CompanySwitchAssessment> AssessAsync(CompanyId targetCompanyId, CancellationToken cancellationToken)
        {
            var target = await FindAsync(targetCompanyId, cancellationToken).ConfigureAwait(false);
            return new CompanySwitchAssessment(await AssessCoreAsync(target, cancellationToken).ConfigureAwait(false));
        }

        public async Task<CompanySwitchResult> SwitchAsync(
            CompanyId targetCompanyId,
            ActiveJobResolution jobResolution,
            CancellationToken cancellationToken)
        {
            // 1. Chỉ một switch chạy tại một thời điểm.
            if (!await _gate.WaitAsync(0).ConfigureAwait(false))
            {
                return Blocked(CompanySwitchBlocker.SwitchInProgress, "Đang chuyển doanh nghiệp khác.");
            }

            try
            {
                var target = await FindAsync(targetCompanyId, cancellationToken).ConfigureAwait(false);
                var current = _contextManager.Current;

                if (target != null && current != null && current.CompanyId.Equals(target.Id))
                {
                    return new CompanySwitchResult(CompanySwitchOutcome.Unchanged, CompanySwitchBlocker.None, current, null);
                }

                // 2. Đánh giá active job.
                var blocker = await AssessCoreAsync(target, cancellationToken).ConfigureAwait(false);
                if (blocker == CompanySwitchBlocker.DownloadRunning)
                {
                    if (jobResolution == ActiveJobResolution.NotRequired)
                    {
                        return Blocked(blocker, "Đang có tác vụ tải; chọn chờ, tạm dừng hoặc hủy trước khi chuyển.");
                    }

                    // 3. Chờ / tạm dừng + checkpoint / hủy.
                    await ResolveJobAsync(current.CompanyId, jobResolution, cancellationToken).ConfigureAwait(false);
                }
                else if (blocker != CompanySwitchBlocker.None)
                {
                    return Blocked(blocker, DescribeBlocker(blocker));
                }

                // 4. Xóa phiên cổng Thuế trước khi rời MST hiện tại.
                _portalSession.Clear();

                // 5. Đóng context hiện tại (phải đóng trước khi mở kho mới).
                var previous = current == null ? null : FindProfile(current);
                _contextManager.CloseCurrent();

                // 6-7. Xác minh và mở kho đích; context chỉ được cập nhật khi thành công (8-9 do manager đảm nhiệm).
                try
                {
                    var opened = await _contextManager.OpenAsync(target, cancellationToken).ConfigureAwait(false);
                    return new CompanySwitchResult(CompanySwitchOutcome.Switched, CompanySwitchBlocker.None, opened, null);
                }
                catch (OperationCanceledException)
                {
                    return await RestoreAsync(previous, CompanySwitchBlocker.StorageUnavailable, "Đã hủy việc mở doanh nghiệp.").ConfigureAwait(false);
                }
                catch (CompanyStorageException ex)
                {
                    _logger.Error("Không mở được kho của doanh nghiệp đích", ex);
                    return await RestoreAsync(previous, ex.Blocker, ex.Message).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<CompanySwitchResult> RestoreAsync(CompanyProfile previous, CompanySwitchBlocker blocker, string message)
        {
            if (previous == null)
            {
                return new CompanySwitchResult(CompanySwitchOutcome.Blocked, blocker, null, message);
            }

            try
            {
                var restored = await _contextManager.OpenAsync(previous, CancellationToken.None).ConfigureAwait(false);
                return new CompanySwitchResult(CompanySwitchOutcome.RestoredPrevious, blocker, restored, message);
            }
            catch (Exception ex)
            {
                _logger.Error("Không phục hồi được doanh nghiệp trước đó", ex);
                return new CompanySwitchResult(CompanySwitchOutcome.RecoveryRequired, blocker, null, message);
            }
        }

        private async Task ResolveJobAsync(CompanyId companyId, ActiveJobResolution resolution, CancellationToken cancellationToken)
        {
            switch (resolution)
            {
                case ActiveJobResolution.WaitForCompletion:
                    await _activeJobs.WaitForCompletionAsync(companyId, cancellationToken).ConfigureAwait(false);
                    break;
                case ActiveJobResolution.PauseAndSwitch:
                    await _activeJobs.PauseAsync(companyId, cancellationToken).ConfigureAwait(false);
                    break;
                case ActiveJobResolution.CancelAndSwitch:
                    await _activeJobs.CancelAsync(companyId, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        private async Task<CompanySwitchBlocker> AssessCoreAsync(CompanyProfile target, CancellationToken cancellationToken)
        {
            if (target == null) return CompanySwitchBlocker.StorageUnavailable;

            var current = _contextManager.Current;
            if (current == null) return CompanySwitchBlocker.None;
            if (current.CompanyId.Equals(target.Id)) return CompanySwitchBlocker.None;

            // Standard chỉ có một MST; ReadOnlyService được chuyển giữa các MST đã có.
            var canSwitch = _entitlement.CurrentMode == AppMode.ReadOnlyService
                || _entitlement.IsAllowed(AppFeature.MultiCompany);
            if (!canSwitch) return CompanySwitchBlocker.LicenceReadOnly;

            if (await _activeJobs.HasRunningDownloadAsync(current.CompanyId, cancellationToken).ConfigureAwait(false))
            {
                return CompanySwitchBlocker.DownloadRunning;
            }

            return CompanySwitchBlocker.None;
        }

        private async Task<CompanyProfile> FindAsync(CompanyId id, CancellationToken cancellationToken)
        {
            var snapshot = await _catalog.LoadAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.Companies.FirstOrDefault(c => c.Id.Equals(id));
        }

        private static CompanyProfile FindProfile(CompanyContext context)
        {
            return new CompanyProfile(context.CompanyId, context.DisplayName, context.TaxCode);
        }

        private static CompanySwitchResult Blocked(CompanySwitchBlocker blocker, string message)
        {
            return new CompanySwitchResult(CompanySwitchOutcome.Blocked, blocker, null, message);
        }

        private static string DescribeBlocker(CompanySwitchBlocker blocker)
        {
            switch (blocker)
            {
                case CompanySwitchBlocker.LicenceReadOnly:
                    return "Chế độ hiện tại không cho phép chuyển doanh nghiệp.";
                case CompanySwitchBlocker.StorageUnavailable:
                    return "Không tìm thấy doanh nghiệp hoặc kho dữ liệu của doanh nghiệp.";
                default:
                    return "Không thể chuyển doanh nghiệp.";
            }
        }
    }
}
