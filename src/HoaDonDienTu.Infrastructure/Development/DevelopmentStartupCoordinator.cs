#if DEBUG
using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>
    /// Adapter phát triển: trả kết quả cố định cho tới khi có StartupCoordinator thật (T01-06).
    /// Chỉ được biên dịch ở Debug; Release không có adapter này.
    /// </summary>
    public sealed class DevelopmentStartupCoordinator : IStartupCoordinator
    {
        private readonly AppMode _mode;

        public DevelopmentStartupCoordinator(AppMode mode)
        {
            _mode = mode;
        }

        public Task<StartupResult> InitializeAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken)
        {
            if (progress != null) progress.Report(new StartupProgress(StartupStage.ResolvingMode, "Đang xác định chế độ làm việc"));

            StartupResult result;
            switch (_mode)
            {
                case AppMode.Service:
                    result = new StartupResult(AppMode.Service, StartupRoute.ServiceCompanySelection,
                        LicenseStatus.Valid, null, null, RecoveryReason.None);
                    break;
                case AppMode.ReadOnlyService:
                    result = new StartupResult(AppMode.ReadOnlyService, StartupRoute.ReadOnlyCompanySelection,
                        LicenseStatus.Expired, null, "Licence hết hạn - chỉ xem dữ liệu cũ.", RecoveryReason.None);
                    break;
                default:
                    var company = new CompanyProfile(CompanyId.NewId(), "CÔNG TY TNHH MẪU", TaxCode.Parse("0101234567"));
                    result = new StartupResult(AppMode.Standard, StartupRoute.StandardPortalLogin,
                        LicenseStatus.None, company, null, RecoveryReason.None);
                    break;
            }

            if (progress != null) progress.Report(new StartupProgress(StartupStage.Ready, "Sẵn sàng"));
            return Task.FromResult(result);
        }
    }
}
#endif
