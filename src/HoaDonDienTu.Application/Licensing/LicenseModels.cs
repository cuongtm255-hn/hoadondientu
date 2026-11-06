using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>Lý do chi tiết khi trạng thái khác Valid, để SCR-12 hiển thị đúng hướng dẫn.</summary>
    public enum LicenseIssue
    {
        None,
        NotActivated,
        Corrupt,
        SignatureInvalid,
        MachineMismatch,
        Expired,
        Revoked,
        OfflineGraceExceeded,
        ClockTampered
    }

    public static class LicenseKeys
    {
        public static string Hint(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return "••••" + (key.Length <= 4 ? key : key.Substring(key.Length - 4));
        }
    }

    /// <summary>Kết quả đánh giá cục bộ, dùng cho cả khởi động lẫn SCR-12.</summary>
    public sealed class LicenseInfo
    {
        public static readonly LicenseInfo NotActivated = new LicenseInfo(LicenseStatus.None, LicenseIssue.NotActivated, null, string.Empty, null, null, null);

        public LicenseInfo(
            LicenseStatus status,
            LicenseIssue issue,
            string licenseType,
            string keyHint,
            DateTime? expiresAtUtc,
            DateTime? lastOnlineValidationUtc,
            DateTime? mustReconnectBeforeUtc)
        {
            Status = status;
            Issue = issue;
            LicenseType = licenseType;
            KeyHint = keyHint ?? string.Empty;
            ExpiresAtUtc = expiresAtUtc;
            LastOnlineValidationUtc = lastOnlineValidationUtc;
            MustReconnectBeforeUtc = mustReconnectBeforeUtc;
        }

        public LicenseStatus Status { get; private set; }
        public LicenseIssue Issue { get; private set; }
        public string LicenseType { get; private set; }
        public string KeyHint { get; private set; }
        public DateTime? ExpiresAtUtc { get; private set; }
        public DateTime? LastOnlineValidationUtc { get; private set; }

        /// <summary>Hạn chót phải kết nối lại (lần xác thực cuối + thời gian ngoại tuyến tối đa).</summary>
        public DateTime? MustReconnectBeforeUtc { get; private set; }

        /// <summary>Đồng hồ bất thường: phải xác thực trực tuyến trước thao tác cần ServiceMode.</summary>
        public bool RequiresOnlineValidation
        {
            get { return Issue == LicenseIssue.ClockTampered; }
        }
    }
}
