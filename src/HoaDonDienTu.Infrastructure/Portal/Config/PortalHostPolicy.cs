using System;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    /// <summary>
    /// Chỉ cho phép <c>baseUrl</c> của cấu hình API trỏ tới máy chủ của cổng Thuế (<c>*.gdt.gov.vn</c>, HTTPS, cổng mặc định, không kèm tài khoản).
    /// <c>portal-api.json</c> có thể nằm trong thư mục người dùng (không cần quyền quản trị); nếu không ghim máy chủ, một file bị thay có thể
    /// đưa trang đăng nhập và Bearer token sang máy chủ lạ (NFR-SEC-001, P6-03).
    /// </summary>
    public static class PortalHostPolicy
    {
        public const string TrustedDomain = "gdt.gov.vn";

        public static bool IsTrusted(string baseUrl)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!uri.IsDefaultPort) return false;
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

            var host = uri.DnsSafeHost.TrimEnd('.');
            return string.Equals(host, TrustedDomain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + TrustedDomain, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Thông báo lỗi dùng khi <see cref="IsTrusted"/> trả về false.</summary>
        public static string Describe()
        {
            return "baseUrl phải là https, cổng mặc định, không kèm tài khoản và thuộc " + TrustedDomain + " (chống đưa đăng nhập sang máy chủ lạ).";
        }
    }
}
