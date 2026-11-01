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

    /// <summary>Phần được server ký. Chỉ chứa dữ liệu licence, không có MST hay đường dẫn kho (BR-025).</summary>
    public sealed class LicenseGrant
    {
        public LicenseGrant(string licenseType, string machineIdHash, DateTime issuedAtUtc, DateTime? expiresAtUtc, IEnumerable<AppFeature> features)
        {
            LicenseType = licenseType ?? string.Empty;
            MachineIdHash = machineIdHash ?? string.Empty;
            IssuedAtUtc = issuedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            Features = (features ?? Enumerable.Empty<AppFeature>()).Distinct().OrderBy(f => (int)f).ToList().AsReadOnly();
        }

        public string LicenseType { get; private set; }
        public string MachineIdHash { get; private set; }
        public DateTime IssuedAtUtc { get; private set; }
        public DateTime? ExpiresAtUtc { get; private set; }
        public IList<AppFeature> Features { get; private set; }

        /// <summary>Chuỗi chuẩn hóa dùng để ký và xác minh; không phụ thuộc định dạng JSON.</summary>
        public string ToSigningPayload()
        {
            return string.Join(
                "|",
                "v1",
                LicenseType,
                MachineIdHash,
                IssuedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                ExpiresAtUtc.HasValue ? ExpiresAtUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "-",
                string.Join(",", Features.Select(f => f.ToString())));
        }
    }

    /// <summary>Nội dung <c>license.dat</c> sau khi giải mã.</summary>
    public sealed class StoredLicense
    {
        public StoredLicense(
            string licenseKey,
            LicenseGrant grant,
            string signature,
            DateTime lastOnlineValidationUtc,
            DateTime lastSeenUtc,
            bool revoked)
        {
            LicenseKey = licenseKey ?? string.Empty;
            Grant = grant;
            Signature = signature ?? string.Empty;
            LastOnlineValidationUtc = lastOnlineValidationUtc;
            LastSeenUtc = lastSeenUtc;
            Revoked = revoked;
        }

        public string LicenseKey { get; private set; }
        public LicenseGrant Grant { get; private set; }
        public string Signature { get; private set; }
        public DateTime LastOnlineValidationUtc { get; private set; }

        /// <summary>Mốc thời gian lớn nhất từng ghi nhận; dùng phát hiện đồng hồ bị lùi (FR-LIC-011).</summary>
        public DateTime LastSeenUtc { get; private set; }

        public bool Revoked { get; private set; }

        public StoredLicense WithValidation(LicenseGrant grant, string signature, DateTime nowUtc)
        {
            // Xác thực trực tuyến thành công là cách duy nhất xóa nghi ngờ lệch đồng hồ (FR-LIC-011).
            return new StoredLicense(LicenseKey, grant, signature, nowUtc, nowUtc, false);
        }

        public StoredLicense WithSeen(DateTime nowUtc)
        {
            return new StoredLicense(LicenseKey, Grant, Signature, LastOnlineValidationUtc, Max(nowUtc, LastSeenUtc), Revoked);
        }

        public StoredLicense AsRevoked(DateTime nowUtc)
        {
            return new StoredLicense(LicenseKey, Grant, Signature, LastOnlineValidationUtc, Max(nowUtc, LastSeenUtc), true);
        }

        /// <summary>Chỉ 4 ký tự cuối được hiển thị hoặc ghi log (DD-03 mục 6).</summary>
        public string KeyHint
        {
            get { return LicenseKeys.Hint(LicenseKey); }
        }

        private static DateTime Max(DateTime a, DateTime b)
        {
            return a > b ? a : b;
        }
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
