using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Startup
{
    /// <summary>
    /// Xác định AppMode và route khởi động theo DD-01 mục 7. Chỉ đọc, không ghi file.
    /// </summary>
    public sealed class StartupCoordinator : IStartupCoordinator
    {
        private readonly ILicenseService _licenseService;
        private readonly ICompanyCatalog _companyCatalog;
        private readonly IApplicationLogger _logger;

        public StartupCoordinator(
            ILicenseService licenseService,
            ICompanyCatalog companyCatalog,
            IApplicationLogger logger)
        {
            if (licenseService == null) throw new ArgumentNullException("licenseService");
            if (companyCatalog == null) throw new ArgumentNullException("companyCatalog");
            if (logger == null) throw new ArgumentNullException("logger");
            _licenseService = licenseService;
            _companyCatalog = companyCatalog;
            _logger = logger;
        }

        public async Task<StartupResult> InitializeAsync(
            IProgress<StartupProgress> progress,
            CancellationToken cancellationToken)
        {
            Report(progress, StartupStage.ValidatingLocalLicense, "Đang kiểm tra bản quyền");
            var local = await ValidateLocalAsync(cancellationToken).ConfigureAwait(false);

            Report(progress, StartupStage.LoadingCompanyCatalog, "Đang đọc danh sách doanh nghiệp");
            CatalogSnapshot catalog;
            try
            {
                catalog = await _companyCatalog.LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("Không đọc được danh sách doanh nghiệp", ex);
                return Recovery(local, RecoveryReason.CatalogCorrupt,
                    "Danh sách doanh nghiệp không đọc được. Dữ liệu được giữ nguyên.", progress);
            }

            if (catalog.Status == CatalogLoadStatus.Corrupt)
            {
                return Recovery(local, RecoveryReason.CatalogCorrupt,
                    "Danh sách doanh nghiệp bị hỏng. Dữ liệu được giữ nguyên.", progress);
            }

            Report(progress, StartupStage.ResolvingMode, "Đang xác định chế độ làm việc");
            var hasHistory = catalog.HasServiceHistory || catalog.Companies.Count > 1;

            // Licence hỏng nhưng đã có nhiều kho: không được rơi về Standard rồi che mất dữ liệu.
            if (local == LicenseStatus.Invalid && hasHistory)
            {
                return Recovery(local, RecoveryReason.LicenseCorrupt,
                    "Licence không hợp lệ trong khi đã có nhiều doanh nghiệp.", progress);
            }

            var status = local;
            string message = null;
            if (ResolveMode(status, hasHistory) == AppMode.Service)
            {
                Report(progress, StartupStage.ValidatingLicenseOnline, "Đang xác thực bản quyền trực tuyến");
                try
                {
                    status = await _licenseService.ValidateOnlineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Lỗi server/mạng không đồng nghĩa bị thu hồi: giữ kết luận cục bộ.
                    _logger.Warning("Không xác thực được licence trực tuyến: " + ex.Message);
                    if (local == LicenseStatus.GracePeriod)
                    {
                        message = "Không kết nối được máy chủ bản quyền; đang dùng thời gian ân hạn.";
                    }
                }
            }

            var mode = ResolveMode(status, hasHistory);
            if (mode == AppMode.ReadOnlyService)
            {
                message = "Bản quyền dịch vụ không còn hiệu lực - chỉ xem, in, xuất dữ liệu cũ.";
            }

            Report(progress, StartupStage.Ready, "Sẵn sàng");
            return BuildResult(mode, status, catalog, message);
        }

        private async Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _licenseService.ValidateLocalAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("Không kiểm tra được licence cục bộ", ex);
                return LicenseStatus.Invalid;
            }
        }

        private static AppMode ResolveMode(LicenseStatus status, bool hasServiceHistory)
        {
            switch (status)
            {
                case LicenseStatus.Valid:
                case LicenseStatus.GracePeriod:
                    return AppMode.Service;
                case LicenseStatus.Expired:
                case LicenseStatus.Revoked:
                case LicenseStatus.None:
                case LicenseStatus.Invalid:
                    return hasServiceHistory ? AppMode.ReadOnlyService : AppMode.Standard;
                default:
                    return AppMode.Standard;
            }
        }

        private static StartupResult BuildResult(AppMode mode, LicenseStatus status, CatalogSnapshot catalog, string message)
        {
            switch (mode)
            {
                case AppMode.Service:
                    return new StartupResult(mode, StartupRoute.ServiceCompanySelection, status, null, message, RecoveryReason.None);
                case AppMode.ReadOnlyService:
                    return new StartupResult(mode, StartupRoute.ReadOnlyCompanySelection, status, null, message, RecoveryReason.None);
                default:
                    if (catalog.Companies.Count == 0)
                    {
                        return new StartupResult(mode, StartupRoute.StandardFirstRun, status, null, message, RecoveryReason.None);
                    }

                    return new StartupResult(mode, StartupRoute.StandardPortalLogin, status, catalog.Companies[0], message, RecoveryReason.None);
            }
        }

        private static StartupResult Recovery(
            LicenseStatus status,
            RecoveryReason reason,
            string message,
            IProgress<StartupProgress> progress)
        {
            Report(progress, StartupStage.RecoveryRequired, message);
            return new StartupResult(AppMode.Standard, StartupRoute.Recovery, status, null, message, reason);
        }

        private static void Report(IProgress<StartupProgress> progress, StartupStage stage, string message)
        {
            if (progress != null) progress.Report(new StartupProgress(stage, message));
        }
    }
}
