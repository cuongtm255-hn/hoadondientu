using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Startup
{
    public enum RecoveryReason
    {
        None,
        CatalogCorrupt,
        LicenseCorrupt,
        StorageMissing
    }

    public sealed class StartupResult
    {
        public StartupResult(
            AppMode mode,
            StartupRoute route,
            LicenseStatus licenseStatus,
            CompanyProfile standardCompany,
            string userMessage,
            RecoveryReason recoveryReason)
        {
            Mode = mode;
            Route = route;
            LicenseStatus = licenseStatus;
            StandardCompany = standardCompany;
            UserMessage = userMessage;
            RecoveryReason = recoveryReason;
        }

        public AppMode Mode { get; private set; }
        public StartupRoute Route { get; private set; }
        public LicenseStatus LicenseStatus { get; private set; }
        public CompanyProfile StandardCompany { get; private set; }
        public string UserMessage { get; private set; }
        public RecoveryReason RecoveryReason { get; private set; }
    }
}
