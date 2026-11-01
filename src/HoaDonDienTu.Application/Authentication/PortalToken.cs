using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HoaDonDienTu.Application.Authentication
{
    /// <summary>
    /// Các claim đọc được từ JWT mà API đăng nhập của cổng Thuế trả về (<c>sub</c> là MST/tên đăng nhập, <c>exp</c>/<c>iat</c> là Unix time,
    /// <c>type</c> là loại tài khoản). Chỉ giải mã phần payload để đọc thông tin, không xác minh chữ ký: chữ ký thuộc về cổng, và token
    /// vẫn phải được cổng chấp nhận ở mỗi lần gọi API.
    /// </summary>
    public sealed class PortalToken
    {
        private static readonly Regex Sub = new Regex("\"sub\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex Exp = new Regex("\"exp\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
        private static readonly Regex Iat = new Regex("\"iat\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        private PortalToken(string subject, DateTime? issuedAtUtc, DateTime? expiresAtUtc)
        {
            Subject = subject;
            IssuedAtUtc = issuedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
        }

        /// <summary>Tên đăng nhập của cổng, chính là MST của doanh nghiệp đăng nhập.</summary>
        public string Subject { get; private set; }

        public DateTime? IssuedAtUtc { get; private set; }
        public DateTime? ExpiresAtUtc { get; private set; }

        public bool IsExpired(DateTime nowUtc)
        {
            return ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= nowUtc;
        }

        public static bool TryParse(string token, out PortalToken parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var parts = token.Trim().Split('.');
            if (parts.Length < 2) return false;

            string json;
            try
            {
                json = Encoding.UTF8.GetString(FromBase64Url(parts[1]));
            }
            catch (FormatException)
            {
                return false;
            }

            var sub = Sub.Match(json);
            if (!sub.Success || sub.Groups[1].Value.Length == 0) return false;

            parsed = new PortalToken(sub.Groups[1].Value, Time(Iat, json), Time(Exp, json));
            return true;
        }

        /// <summary>Dựng JWT không chữ ký, chỉ dùng cho cổng giả và test.</summary>
        public static string BuildUnsigned(string subject, DateTime issuedAtUtc, DateTime expiresAtUtc)
        {
            var header = ToBase64Url(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}"));
            var payload = ToBase64Url(Encoding.UTF8.GetBytes(
                "{\"sub\":\"" + subject + "\",\"type\":2,\"exp\":" + ToUnix(expiresAtUtc) + ",\"iat\":" + ToUnix(issuedAtUtc) + "}"));
            return header + "." + payload + ".unsigned";
        }

        private static DateTime? Time(Regex pattern, string json)
        {
            var match = pattern.Match(json);
            long seconds;
            if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out seconds)) return null;
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
        }

        private static long ToUnix(DateTime utc)
        {
            return (long)(utc.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static byte[] FromBase64Url(string value)
        {
            var text = value.Replace('-', '+').Replace('_', '/');
            switch (text.Length % 4)
            {
                case 2: text += "=="; break;
                case 3: text += "="; break;
            }

            return Convert.FromBase64String(text);
        }

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
