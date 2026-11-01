using System;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>
    /// Quy tắc đánh giá licence cục bộ (DD-03 mục 4.2). Thuần logic, không IO, để test được mọi nhánh.
    /// Chữ ký được xác minh ở <see cref="LicenseService"/> trước khi gọi vào đây.
    /// </summary>
    public static class LicenseEvaluator
    {
        public static readonly TimeSpan OfflineGrace = TimeSpan.FromDays(30);

        /// <summary>Trong ngưỡng này kể từ lần xác thực trực tuyến cuối thì coi là Valid, quá ngưỡng là GracePeriod.</summary>
        public static readonly TimeSpan FreshValidation = TimeSpan.FromHours(24);

        /// <summary>Sai số cho phép khi đồng hồ chỉnh lại nhẹ (đồng bộ NTP, đổi múi giờ).</summary>
        public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(10);

        public static LicenseInfo Evaluate(StoredLicense license, string machineIdHash, DateTime nowUtc)
        {
            var grant = license.Grant;
            var deadline = license.LastOnlineValidationUtc + OfflineGrace;

            if (!string.Equals(grant.MachineIdHash, machineIdHash, StringComparison.Ordinal))
            {
                return Info(license, LicenseStatus.Invalid, LicenseIssue.MachineMismatch, deadline);
            }

            if (license.Revoked)
            {
                return Info(license, LicenseStatus.Revoked, LicenseIssue.Revoked, deadline);
            }

            if (grant.ExpiresAtUtc.HasValue && nowUtc >= grant.ExpiresAtUtc.Value)
            {
                return Info(license, LicenseStatus.Expired, LicenseIssue.Expired, deadline);
            }

            if (nowUtc + ClockTolerance < license.LastSeenUtc)
            {
                // Không kết luận hết hạn: chỉ buộc xác thực trực tuyến trước khi dùng ServiceMode (FR-LIC-011).
                return Info(license, LicenseStatus.Invalid, LicenseIssue.ClockTampered, deadline);
            }

            var age = nowUtc - license.LastOnlineValidationUtc;
            if (age > OfflineGrace)
            {
                return Info(license, LicenseStatus.Expired, LicenseIssue.OfflineGraceExceeded, deadline);
            }

            return Info(license, age > FreshValidation ? LicenseStatus.GracePeriod : LicenseStatus.Valid, LicenseIssue.None, deadline);
        }

        private static LicenseInfo Info(StoredLicense license, LicenseStatus status, LicenseIssue issue, DateTime deadline)
        {
            return new LicenseInfo(
                status,
                issue,
                license.Grant.LicenseType,
                license.KeyHint,
                license.Grant.ExpiresAtUtc,
                license.LastOnlineValidationUtc,
                deadline);
        }
    }
}
