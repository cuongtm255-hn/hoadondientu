using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HoaDonDienTu.Application.Authentication
{
    /// <summary>
    /// Kết quả ghi lại cuộc gọi đăng nhập <c>POST /api/security-taxpayer/authenticate</c> do chính trang đăng nhập của cổng thực hiện trong WebView
    /// (sample-api-web.md mục 1). Ứng dụng không tự đăng nhập và không giữ mật khẩu: request chỉ được đọc để lấy <c>username</c> (MST),
    /// còn <c>password</c>, <c>cvalue</c>, <c>ckey</c> (CAPTCHA) không bao giờ được đọc, lưu hay ghi log. Phản hồi <c>{"token": "..."}</c>
    /// cùng cookie của trang (jwt, JSESSIONID, cookie WAF) và User-Agent là toàn bộ thứ cần để gọi các API chức năng sau đó.
    /// </summary>
    public sealed class PortalAuthCapture
    {
        private static readonly Regex TokenField = new Regex("\"token\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex UsernameField = new Regex("\"username\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);

        private PortalAuthCapture(string token, string username, IDictionary<string, string> cookies, string userAgent)
        {
            Token = token;
            Username = username;
            Cookies = cookies;
            UserAgent = userAgent;
        }

        public string Token { get; private set; }

        /// <summary>Tên đăng nhập (MST) lấy từ request nếu có, nếu không thì từ claim <c>sub</c> của token.</summary>
        public string Username { get; private set; }

        public IDictionary<string, string> Cookies { get; private set; }
        public string UserAgent { get; private set; }

        /// <summary>
        /// Tạo từ nội dung WebView ghi được. Trả về false nếu phản hồi không có token (đăng nhập thất bại, sai CAPTCHA...).
        /// </summary>
        /// <param name="requestBody">Thân request đăng nhập; chỉ đọc trường username, có thể null.</param>
        /// <param name="responseBody">Thân phản hồi đăng nhập.</param>
        /// <param name="cookies">Cookie hiện có của host cổng Thuế trong WebView sau đăng nhập.</param>
        /// <param name="userAgent">User-Agent của WebView.</param>
        /// <param name="capture">Kết quả khi thành công.</param>
        public static bool TryCreate(
            string requestBody,
            string responseBody,
            IDictionary<string, string> cookies,
            string userAgent,
            out PortalAuthCapture capture)
        {
            capture = null;
            var tokenMatch = string.IsNullOrEmpty(responseBody) ? null : TokenField.Match(responseBody);
            if (tokenMatch == null || !tokenMatch.Success) return false;

            var token = tokenMatch.Groups[1].Value;
            string username = null;
            var userMatch = string.IsNullOrEmpty(requestBody) ? null : UsernameField.Match(requestBody);
            if (userMatch != null && userMatch.Success) username = userMatch.Groups[1].Value;
            if (string.IsNullOrEmpty(username))
            {
                PortalToken parsed;
                if (PortalToken.TryParse(token, out parsed)) username = parsed.Subject;
            }

            capture = new PortalAuthCapture(
                token, username, new Dictionary<string, string>(cookies ?? new Dictionary<string, string>()), userAgent);
            return true;
        }

        /// <summary>Phiên dùng cho các lần gọi API sau đó. Chỉ tồn tại trong bộ nhớ.</summary>
        public PortalSessionMaterial ToMaterial()
        {
            return new PortalSessionMaterial(Cookies, null, Token, UserAgent);
        }

        /// <summary>Mô tả an toàn để log: không có token, cookie hay thông tin đăng nhập.</summary>
        public override string ToString()
        {
            return "PortalAuthCapture(" + (Username ?? "?") + ", token=***, cookies=" + Cookies.Count + ")";
        }
    }
}
