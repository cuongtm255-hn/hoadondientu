using System;
using System.Text;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Dựng các đoạn JavaScript mà trình duyệt ẩn chạy trong trang cổng Thuế để lấy CAPTCHA và gửi đăng nhập (ADR 0011). Đoạn chạy bằng
    /// <c>fetch</c> cùng gốc với trang nên dùng đúng cookie/phiên WAF đã gắn với CAPTCHA. Kết quả đặt vào <c>window.__hddtJob</c> để C# hỏi lại;
    /// không bao giờ đặt token hay mật khẩu vào đó (chỉ mã HTTP, <c>key</c> và ảnh CAPTCHA). Tách riêng khỏi Chromium để kiểm thử được.
    /// </summary>
    public static class PortalLoginScripts
    {
        public const string JobName = "window.__hddtJob";

        /// <summary>Biểu thức hỏi "việc đã xong chưa".</summary>
        public const string IsDone = "!!window.__hddtJob && window.__hddtJob.done === true";

        public const string Status = "window.__hddtJob ? window.__hddtJob.status : 0";
        public const string Failed = "!window.__hddtJob || window.__hddtJob.failed === true";
        public const string CaptchaKey = "window.__hddtJob ? window.__hddtJob.key : null";
        public const string CaptchaSvg = "window.__hddtJob ? window.__hddtJob.svg : null";
        public const string Clear = "window.__hddtJob = null;";

        /// <summary><c>GET {captchaPath}</c>: lấy <c>key</c> và <c>content</c> (SVG).</summary>
        public static string CaptchaRequest(string captchaPath, string requestId)
        {
            return JobName + " = { done: false, status: 0 };"
                + "fetch(" + Quote(captchaPath) + ", { method: 'GET', credentials: 'same-origin', headers: " + Headers(requestId, false) + " })"
                + ".then(function (r) { " + JobName + ".status = r.status; return r.json(); })"
                + ".then(function (j) { " + JobName + ".key = j && j.key; " + JobName + ".svg = j && j.content; " + JobName + ".done = true; })"
                + ".catch(function () { " + JobName + ".failed = true; " + JobName + ".done = true; });";
        }

        /// <summary>
        /// <c>POST {authenticatePath}</c> với <c>{username, password, cvalue, ckey}</c>. Thân phản hồi (chứa token) được đọc hết rồi bỏ để bộ lọc của
        /// ứng dụng nhận đủ byte; token không được giữ trong biến nào của trang.
        /// </summary>
        public static string AuthenticateRequest(string authenticatePath, string requestId, string username, string password, string captchaText, string captchaKey)
        {
            var body = "{ username: " + Quote(username) + ", password: " + Quote(password) + ", cvalue: " + Quote(captchaText) + ", ckey: " + Quote(captchaKey) + " }";
            return JobName + " = { done: false, status: 0 };"
                + "fetch(" + Quote(authenticatePath) + ", { method: 'POST', credentials: 'same-origin', headers: " + Headers(requestId, true) + ", body: JSON.stringify(" + body + ") })"
                + ".then(function (r) { " + JobName + ".status = r.status; return r.text(); })"
                + ".then(function () { " + JobName + ".done = true; })"
                + ".catch(function () { " + JobName + ".failed = true; " + JobName + ".done = true; });";
        }

        private static string Headers(string requestId, bool json)
        {
            return "{ 'accept': 'application/json, text/plain, */*', 'action': '', 'end-point': '/', 'request-id': " + Quote(requestId)
                + (json ? ", 'content-type': 'application/json'" : string.Empty) + " }";
        }

        /// <summary>Chuỗi JavaScript trong ngoặc kép: thoát dấu nháy, gạch chéo ngược, ký tự điều khiển và U+2028/U+2029 (kết thúc dòng trong JS).</summary>
        public static string Quote(string value)
        {
            if (value == null) return "null";
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '<': builder.Append("\\u003C"); break;
                    default:
                        if (c < 0x20 || c == 0x2028 || c == 0x2029 || c == 0x7F)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        /// <summary>GUID mới cho header <c>request-id</c> như trang cổng.</summary>
        public static string NewRequestId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
