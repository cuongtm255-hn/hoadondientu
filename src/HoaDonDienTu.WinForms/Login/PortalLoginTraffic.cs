using System;
using System.IO;
using System.Text;
using HoaDonDienTu.Application.Authentication;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Quy tắc thuần (không phụ thuộc WebView) cho trình duyệt đăng nhập: trang nào được mở và làm sao nhận ra phản hồi đăng nhập
    /// thành công (G-01 mục 6-7, ADR 0007). Tách riêng để kiểm thử được mà không khởi tạo Chromium.
    /// </summary>
    public sealed class PortalLoginTraffic
    {
        /// <summary>Phản hồi đăng nhập thật chỉ có <c>{"token": "..."}</c>; chặn trên để thân lớn bất thường không làm đầy bộ nhớ.</summary>
        public const int MaxResponseBytes = 64 * 1024;

        private readonly Uri _origin;
        private readonly string _authenticatePath;

        /// <param name="baseUrl">Gốc cổng Thuế, ví dụ <c>https://hoadondientu.gdt.gov.vn</c>.</param>
        /// <param name="authenticatePath">Đường dẫn API đăng nhập, ví dụ <c>/api/security-taxpayer/authenticate</c>.</param>
        public PortalLoginTraffic(string baseUrl, string authenticatePath, string captchaPath = "/api/captcha")
        {
            Uri origin;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out origin) || origin.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("Gốc cổng Thuế phải là địa chỉ https hợp lệ.", "baseUrl");
            }

            if (string.IsNullOrEmpty(authenticatePath) || !authenticatePath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Đường dẫn đăng nhập phải bắt đầu bằng '/'.", "authenticatePath");
            }

            if (string.IsNullOrEmpty(captchaPath) || !captchaPath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Đường dẫn CAPTCHA phải bắt đầu bằng '/'.", "captchaPath");
            }

            _origin = origin;
            _authenticatePath = authenticatePath;
            CaptchaPath = captchaPath;
        }

        /// <summary>Đường dẫn API đăng nhập (<c>POST</c>), gọi từ trang cổng.</summary>
        public string AuthenticatePath
        {
            get { return _authenticatePath; }
        }

        /// <summary>Đường dẫn API lấy CAPTCHA (<c>GET</c>, api-samples mục 0).</summary>
        public string CaptchaPath { get; private set; }

        public string LoginUrl
        {
            get { return _origin.GetLeftPart(UriPartial.Authority) + "/"; }
        }

        public string Origin
        {
            get { return _origin.GetLeftPart(UriPartial.Authority); }
        }

        /// <summary>
        /// Chỉ mở trang https của đúng máy chủ cổng (hoặc máy chủ con của nó) và about:blank; mọi điều hướng khác (quảng cáo, liên kết ngoài,
        /// cửa sổ mới) bị chặn để Chromium 109 không bị dùng làm trình duyệt chung.
        /// </summary>
        public bool IsNavigationAllowed(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase)) return true;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
            var host = uri.Host;
            return string.Equals(host, _origin.Host, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + _origin.Host, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Đúng là lời gọi đăng nhập <c>POST {BaseUrl}{AuthenticatePath}</c>; so khớp đường dẫn, bỏ qua chuỗi truy vấn.</summary>
        public bool IsAuthenticateRequest(string method, string url)
        {
            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) return false;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            return string.Equals(uri.Host, _origin.Host, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.AbsolutePath, _authenticatePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Gom thân phản hồi đăng nhập (giới hạn <see cref="MaxResponseBytes"/>); không bao giờ chạm vào thân request chứa mật khẩu và CAPTCHA.</summary>
        public sealed class ResponseBuffer
        {
            private readonly MemoryStream _bytes = new MemoryStream();
            private bool _overflow;

            public void Append(byte[] buffer, int offset, int count)
            {
                if (_overflow || count <= 0) return;
                if (_bytes.Length + count > MaxResponseBytes)
                {
                    _overflow = true;
                    _bytes.SetLength(0);
                    return;
                }

                _bytes.Write(buffer, offset, count);
            }

            /// <summary>
            /// Dựng bằng chứng đăng nhập từ phản hồi đã gom. Tên đăng nhập lấy từ claim <c>sub</c> của token (không đọc request). False khi thân rỗng,
            /// quá lớn hoặc không có token (sai CAPTCHA, sai mật khẩu...). Bộ đệm được xóa sau khi đọc để token không nằm lại thêm chỗ nào.
            /// </summary>
            public bool TryBuild(System.Collections.Generic.IDictionary<string, string> cookies, string userAgent, out PortalAuthCapture capture)
            {
                capture = null;
                if (_overflow || _bytes.Length == 0) return false;
                var text = Encoding.UTF8.GetString(_bytes.ToArray());
                Clear();
                return PortalAuthCapture.TryCreate(null, text, cookies, userAgent, out capture);
            }

            public void Clear()
            {
                _bytes.SetLength(0);
                _overflow = false;
            }

            public bool HasContent
            {
                get { return !_overflow && _bytes.Length > 0; }
            }
        }
    }
}
