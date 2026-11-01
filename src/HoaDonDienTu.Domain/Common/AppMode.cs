namespace HoaDonDienTu.Domain.Common
{
    public enum AppMode
    {
        Standard,
        Service,
        ReadOnlyService
    }

    public enum StartupRoute
    {
        StandardFirstRun,
        StandardPortalLogin,
        ServiceCompanySelection,
        ReadOnlyCompanySelection,
        Recovery
    }
}
