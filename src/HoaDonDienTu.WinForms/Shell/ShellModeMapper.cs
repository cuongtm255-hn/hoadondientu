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

            return ToModeState(result.Mode);
        }

        /// <summary>
        /// Nhãn bản quyền cho chế độ Service: "Dùng thử còn N ngày" khi đang dùng thử, "Đã kích hoạt" khi đã nhập mã. Chế độ khác giữ nguyên.
        /// </summary>
        public static ShellModeViewState WithLicense(ShellModeViewState state, AppMode mode, HoaDonDienTu.Application.Licensing.LicenseInfo info, System.DateTime utcNow)
        {
            if (mode != AppMode.Service || info == null) return state;
            if (info.Status == HoaDonDienTu.Domain.Licensing.LicenseStatus.Trial && info.ExpiresAtUtc.HasValue)
            {
                var days = System.Math.Max(0, (int)(info.ExpiresAtUtc.Value.Date - utcNow.Date).TotalDays);
                state.LicenseLabel = "Dùng thử còn " + days.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ngày";
            }
            else if (info.Status == HoaDonDienTu.Domain.Licensing.LicenseStatus.Valid && info.LicenseType == "PerMachine")
            {
                state.LicenseLabel = "Đã kích hoạt";
            }

            return state;
        }

        /// <summary>Dùng khi chế độ đổi giữa chừng (kích hoạt, hết hạn, hủy licence).</summary>
        public static ShellModeViewState ToModeState(AppMode mode)
        {
            switch (mode)
            {
                case AppMode.Service:
                    return new ShellModeViewState
                    {
                        ShowCompanySelector = true,
                        ShowCompanyMenu = true,
                        CanAddCompany = true,
                        // Khả năng theo chế độ; MainShellPresenter chỉ bật khi đã mở một doanh nghiệp.
                        CanPortalLogin = true,
                        CanDownload = true,
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
