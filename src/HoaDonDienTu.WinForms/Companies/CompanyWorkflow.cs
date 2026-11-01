using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>
    /// "Làm việc với MST này" (DD-02 mục 4.2): chuyển kho (xử lý tác vụ đang chạy, xóa phiên, đóng kho cũ, mở kho mới), vào Dashboard rồi
    /// mở đăng nhập cổng Thuế. Chạy ngoài các screen nên không bị hủy khi screen danh sách/thêm bị thay thế.
    /// ReadOnlyService mở kho nhưng không mở đăng nhập.
    /// </summary>
    public sealed class CompanyWorkflow
    {
        private readonly ICompanySwitchService _switch;
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalSessionStore _session;
        private readonly IEntitlementService _entitlement;
        private readonly IConfirmationService _confirmation;
        private readonly IUserErrorPresenter _errors;
        private readonly Func<INavigationService> _navigation;
        private readonly Func<IShellActions> _shell;

        public CompanyWorkflow(
            ICompanySwitchService switchService,
            ICompanyContextAccessor context,
            IPortalSessionStore session,
            IEntitlementService entitlement,
            IConfirmationService confirmation,
            IUserErrorPresenter errors,
            Func<INavigationService> navigation,
            Func<IShellActions> shell)
        {
            if (switchService == null) throw new ArgumentNullException("switchService");
            if (context == null) throw new ArgumentNullException("context");
            if (session == null) throw new ArgumentNullException("session");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            if (errors == null) throw new ArgumentNullException("errors");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (shell == null) throw new ArgumentNullException("shell");
            _switch = switchService;
            _context = context;
            _session = session;
            _entitlement = entitlement;
            _confirmation = confirmation;
            _errors = errors;
            _navigation = navigation;
            _shell = shell;
        }

        /// <returns>True nếu đã sẵn sàng làm việc với doanh nghiệp (kể cả khi người dùng hủy đăng nhập).</returns>
        public async Task<bool> StartWorkingAsync(CompanyId id, CancellationToken cancellationToken)
        {
            var alreadyCurrent = _context.HasCurrent && _context.Current.CompanyId.Equals(id);
            if (!alreadyCurrent && !await SwitchToAsync(id, cancellationToken).ConfigureAwait(true)) return false;

            await _navigation().NavigateAsync(ScreenId.Dashboard, null, cancellationToken).ConfigureAwait(true);

            if (_entitlement.IsAllowed(AppFeature.PortalLogin) && !_session.HasActiveSession)
            {
                await _shell().LoginAsync(LoginReason.CompanySelected).ConfigureAwait(true);
            }

            return true;
        }

        private async Task<bool> SwitchToAsync(CompanyId id, CancellationToken cancellationToken)
        {
            var assessment = await _switch.AssessAsync(id, cancellationToken).ConfigureAwait(true);
            var resolution = ActiveJobResolution.NotRequired;

            if (assessment.RequiresJobResolution)
            {
                var from = _context.HasCurrent ? _context.Current : null;
                var message = "Đang có tác vụ tải" + (from == null ? string.Empty : " của " + from.DisplayName + " (MST " + from.TaxCode.Value + ")")
                    + ". Tạm dừng (giữ tiến độ) và chuyển sang doanh nghiệp được chọn?";
                if (!_confirmation.Confirm("Chuyển doanh nghiệp", message)) return false;
                resolution = ActiveJobResolution.PauseAndSwitch;
            }
            else if (!assessment.CanSwitch)
            {
                Show(BlockerMessage(assessment.Blocker));
                return false;
            }

            var result = await _switch.SwitchAsync(id, resolution, cancellationToken).ConfigureAwait(true);
            switch (result.Outcome)
            {
                case CompanySwitchOutcome.Switched:
                case CompanySwitchOutcome.Unchanged:
                    return true;

                case CompanySwitchOutcome.RecoveryRequired:
                    Show(result.Message ?? "Không mở được kho dữ liệu nào. Cần khôi phục.");
                    await _navigation().NavigateAsync(ScreenId.Recovery, null, cancellationToken).ConfigureAwait(true);
                    return false;

                case CompanySwitchOutcome.RestoredPrevious:
                    Show((result.Message ?? "Không mở được kho của doanh nghiệp được chọn.") + " Đã quay lại doanh nghiệp trước đó.");
                    return false;

                default:
                    Show(result.Message ?? BlockerMessage(result.Blocker));
                    return false;
            }
        }

        private void Show(string message)
        {
            _errors.Show(new UserFacingError(
                "COMPANY_SWITCH", ErrorCategory.Storage, "Chuyển doanh nghiệp", message,
                "Kiểm tra thư mục dữ liệu hoặc liên kết lại thư mục tại màn hình Doanh nghiệp.", CorrelationId.New(), true));
        }

        private static string BlockerMessage(CompanySwitchBlocker blocker)
        {
            switch (blocker)
            {
                case CompanySwitchBlocker.LicenceReadOnly: return "Chế độ hiện tại không cho phép chuyển doanh nghiệp.";
                case CompanySwitchBlocker.StorageUnavailable: return "Không tìm thấy doanh nghiệp hoặc thư mục kho dữ liệu.";
                case CompanySwitchBlocker.DatabaseInvalid: return "Kho dữ liệu của doanh nghiệp không hợp lệ.";
                case CompanySwitchBlocker.DatabaseLocked: return "Kho dữ liệu đang bị khóa.";
                case CompanySwitchBlocker.SwitchInProgress: return "Đang chuyển doanh nghiệp khác.";
                default: return "Không thể chuyển doanh nghiệp.";
            }
        }
    }
}
