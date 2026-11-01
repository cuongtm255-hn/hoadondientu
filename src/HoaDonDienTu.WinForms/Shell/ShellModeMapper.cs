using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;

namespace HoaDonDienTu.WinForms.Shell
{
    /// <summary>Quy đổi kết quả khởi động thành trạng thái hiển thị (DD-01 mục 8.3).</summary>
    public static class ShellModeMapper
    {
        public static ShellModeViewState ToModeState(StartupResult result)
        {
            if (result.Route == StartupRoute.Recovery)
            {
                return new ShellModeViewState { LicenseLabel = "Cần khôi phục" };
            }

            switch (result.Mode)
            {
                case AppMode.Service:
                    return new ShellModeViewState
                    {
                        ShowCompanySelector = true,
                        ShowCompanyMenu = true,
                        CanAddCompany = true,
                        LicenseLabel = "Bản dịch vụ"
                    };
                case AppMode.ReadOnlyService:
                    return new ShellModeViewState
                    {
                        ShowCompanySelector = true,
                        ShowCompanyMenu = true,
                        LicenseLabel = "Chỉ đọc - cần gia hạn"
                    };
                default:
                    return new ShellModeViewState
                    {
                        CanPortalLogin = true,
                        CanDownload = true,
                        LicenseLabel = string.Empty
                    };
            }
        }

        public static CompanyHeaderViewState ToCompanyState(StartupResult result)
        {
            if (result.StandardCompany != null)
            {
                return new CompanyHeaderViewState
                {
                    CompanyName = result.StandardCompany.DisplayName,
                    TaxCode = result.StandardCompany.TaxCode.Value
                };
            }

            return new CompanyHeaderViewState { CompanyName = "Chưa chọn doanh nghiệp", TaxCode = string.Empty };
        }
    }
}
