using System.Text.RegularExpressions;

namespace HoaDonDienTu.Application.Common
{
    /// <summary>
    /// Che dữ liệu nhạy cảm trước khi ghi log (NFR-SEC-002): Authorization, cookie, token, mật khẩu, CAPTCHA,
    /// khóa licence, CCCD và nội dung XML. Che theo mẫu nên là lớp bảo vệ cuối, không thay cho việc không log bí mật.
    /// </summary>
    public static class LogSanitizer
    {
        public const string Mask = "***";
        public const string XmlMask = "[XML đã che]";

        private const string SecretKeys =
            "password|passwd|pwd|matkhau|token|access_token|refresh_token|id_token|captcha|ckey|secret|apikey|api_key|licensekey|license_key";

        // Chỉ coi là XML khi có khai báo <?xml hoặc một thẻ mở có thẻ đóng tương ứng; kiểu "List<int>" không bị che.
        private static readonly Regex Xml = new Regex(
            @"<\?xml[\s\S]*|<([A-Za-z][\w:.\-]*)(?:\s[^<>]*)?>[\s\S]*?</\1\s*>[\s\S]*",
            RegexOptions.Compiled);

        private static readonly Regex AuthorizationHeader = new Regex(
            @"\b(proxy-authorization|authorization)\b\s*[:=]\s*(?:(?:bearer|basic)\s+)?[^\s,;]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CookieHeader = new Regex(
            @"\b(set-cookie|cookie)\b\s*[:=]\s*[^\r\n]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BearerToken = new Regex(
            @"\bbearer\s+[A-Za-z0-9\-._~+/]+=*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex JsonSecret = new Regex(
            "(\"(?:" + SecretKeys + "|authorization|cookie)\"\\s*:\\s*\")[^\"]*(\")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex KeyValueSecret = new Regex(
            @"\b(" + SecretKeys + @")\b\s*[=:]\s*[^\s&,;""']+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Jwt = new Regex(
            @"eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]*",
            RegexOptions.Compiled);

        private static readonly Regex NationalId = new Regex(@"(?<!\d)\d{12}(?!\d)", RegexOptions.Compiled);

        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = Xml.Replace(text, XmlMask);
            result = AuthorizationHeader.Replace(result, m => m.Groups[1].Value + ": " + Mask);
            result = CookieHeader.Replace(result, m => m.Groups[1].Value + ": " + Mask);
            result = BearerToken.Replace(result, "Bearer " + Mask);
            result = JsonSecret.Replace(result, "$1" + Mask + "$2");
            result = KeyValueSecret.Replace(result, m => m.Groups[1].Value + "=" + Mask);
            result = Jwt.Replace(result, Mask);
            result = NationalId.Replace(result, Mask);
            return result;
        }

        /// <summary>MST được phép xuất hiện trong log ở dạng đã che: giữ 3 ký tự đầu và 3 ký tự cuối.</summary>
        public static string MaskTaxCode(string taxCode)
        {
            if (string.IsNullOrEmpty(taxCode)) return taxCode;
            if (taxCode.Length <= 6) return new string('*', taxCode.Length);
            return taxCode.Substring(0, 3) + new string('*', taxCode.Length - 6) + taxCode.Substring(taxCode.Length - 3);
        }
    }
}
