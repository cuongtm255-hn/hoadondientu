using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Licensing;
using Newtonsoft.Json;

namespace HoaDonDienTu.Infrastructure.Licensing
{
    /// <summary>
    /// Nội dung <c>license-api.json</c>: địa chỉ licence server và public key (RSA XML) dùng xác minh chữ ký.
    /// Khi gate G-06 chốt, chỉ cần đặt file này; không phải build lại.
    /// </summary>
    public sealed class LicenseApiSettings
    {
        public string BaseUrl { get; set; }
        public int TimeoutSeconds { get; set; }
        public string PublicKeyXml { get; set; }

        public bool IsComplete
        {
            get
            {
                Uri uri;
                return !string.IsNullOrWhiteSpace(PublicKeyXml)
                    && Uri.TryCreate(BaseUrl, UriKind.Absolute, out uri)
                    && uri.Scheme == Uri.UriSchemeHttps;
            }
        }

        public LicenseApiConfig ToConfig()
        {
            return new LicenseApiConfig(BaseUrl, TimeSpan.FromSeconds(TimeoutSeconds > 0 ? TimeoutSeconds : 15));
        }
    }

    public static class LicenseApiSettingsLoader
    {
        /// <summary>Trả về null khi thiếu file, sai JSON, thiếu public key hoặc địa chỉ không phải HTTPS (không chấp nhận cấu hình dở dang).</summary>
        public static LicenseApiSettings TryLoad(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var settings = JsonConvert.DeserializeObject<LicenseApiSettings>(File.ReadAllText(path));
                return settings != null && settings.IsComplete ? settings : null;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Dùng khi chưa có license-api.json: mọi lời gọi báo "chưa kết nối được" để ứng dụng vẫn chạy Standard và SCR-12 hiện lý do rõ ràng.
    /// </summary>
    public sealed class UnconfiguredLicenseServer : ILicenseServer
    {
        public Task<LicenseServerResponse> ActivateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken)
        {
            throw Unavailable();
        }

        public Task<LicenseServerResponse> ValidateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken)
        {
            throw Unavailable();
        }

        public Task DeactivateAsync(string licenseKey, string machineIdHash, CancellationToken cancellationToken)
        {
            throw Unavailable();
        }

        private static LicenseException Unavailable()
        {
            return new LicenseException(LicenseFailure.ServerUnavailable, "Chưa cấu hình licence server (license-api.json)");
        }
    }
}
