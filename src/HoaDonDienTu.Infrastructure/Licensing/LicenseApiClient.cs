using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Licensing
{
    /// <summary>Địa chỉ licence server. Hợp đồng API còn là bản nháp tới khi chốt gate G-06.</summary>
    public sealed class LicenseApiConfig
    {
        public LicenseApiConfig(string baseUrl, TimeSpan timeout)
        {
            BaseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            Timeout = timeout;
        }

        public string BaseUrl { get; private set; }
        public TimeSpan Timeout { get; private set; }
    }

    /// <summary>
    /// Client HTTP của licence server (DD-03 mục 7), tách hẳn handler của cổng Thuế. Hợp đồng nháp (chưa xác minh với server thật):
    /// <c>POST {base}/activate|validate</c> body <c>{licenseKey, machineIdHash, appVersion}</c> trả
    /// <c>{licenseType, machineIdHash, issuedAtUtc, expiresAtUtc|null, features[], signature}</c>;
    /// <c>POST {base}/deactivate</c> body <c>{licenseKey, machineIdHash}</c>. Từ chối nghiệp vụ là 4xx với
    /// <c>{"error": "key_invalid|bound_to_other_machine|expired|revoked"}</c>. Mất mạng, timeout và 5xx là ServerUnavailable.
    /// </summary>
    public sealed class LicenseApiClient : ILicenseServer, IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public LicenseApiClient(LicenseApiConfig config, HttpMessageHandler handler)
        {
            if (config == null) throw new ArgumentNullException("config");
            _baseUrl = config.BaseUrl;
            _http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = config.Timeout };
        }

        public async Task<LicenseServerResponse> ActivateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken)
        {
            var body = await PostAsync("activate", Payload(licenseKey, machineIdHash, appVersion), cancellationToken).ConfigureAwait(false);
            return ParseGrant(body);
        }

        public async Task<LicenseServerResponse> ValidateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken)
        {
            var body = await PostAsync("validate", Payload(licenseKey, machineIdHash, appVersion), cancellationToken).ConfigureAwait(false);
            return ParseGrant(body);
        }

        public async Task DeactivateAsync(string licenseKey, string machineIdHash, CancellationToken cancellationToken)
        {
            // Chỉ key và mã máy: giải phóng máy không cần phiên bản.
            await PostAsync("deactivate", new JObject { { "licenseKey", licenseKey }, { "machineIdHash", machineIdHash } }, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        /// <summary>Đúng ba trường cho phép gửi (BR-025); không thêm MST, đường dẫn kho hay dữ liệu khác.</summary>
        private static JObject Payload(string key, string machine, string version)
        {
            return new JObject { { "licenseKey", key }, { "machineIdHash", machine }, { "appVersion", version } };
        }

        private async Task<string> PostAsync(string path, JObject payload, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                var content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                response = await _http.PostAsync(_baseUrl + "/" + path, content, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Hủy do người dùng thì giữ nguyên; HttpClient hết hạn cũng ném cùng loại nên phân biệt bằng token.
                if (cancellationToken.IsCancellationRequested) throw;
                throw Unavailable("Hết thời gian chờ licence server");
            }
            catch (HttpRequestException)
            {
                throw Unavailable("Không kết nối được licence server");
            }
            catch (WebException)
            {
                throw Unavailable("Không kết nối được licence server");
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return text;
                if ((int)response.StatusCode >= 500 || response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    throw Unavailable("Licence server tạm thời lỗi (" + (int)response.StatusCode + ")");
                }

                throw MapRejection(text);
            }
        }

        private static LicenseException Unavailable(string message)
        {
            return new LicenseException(LicenseFailure.ServerUnavailable, message);
        }

        private static LicenseException MapRejection(string body)
        {
            string code = null;
            try
            {
                var json = JObject.Parse(body);
                code = (string)json["error"];
            }
            catch (Exception)
            {
                // Thân phản hồi không đọc được: coi như key không hợp lệ chứ không đoán trạng thái khác.
            }

            switch (code)
            {
                case "bound_to_other_machine":
                    return new LicenseException(LicenseFailure.KeyBoundToOtherMachine, "Licence gắn với máy khác");
                case "expired":
                    return new LicenseException(LicenseFailure.Expired, "Licence đã hết hạn");
                case "revoked":
                    return new LicenseException(LicenseFailure.Revoked, "Licence đã bị thu hồi");
                default:
                    return new LicenseException(LicenseFailure.KeyInvalid, "Licence key không hợp lệ");
            }
        }

        private static LicenseServerResponse ParseGrant(string body)
        {
            try
            {
                var json = JObject.Parse(body);
                var features = new List<AppFeature>();
                var array = json["features"] as JArray;
                if (array != null)
                {
                    foreach (var item in array)
                    {
                        AppFeature feature;
                        if (Enum.TryParse((string)item, out feature)) features.Add(feature);
                    }
                }

                var expires = json["expiresAtUtc"];
                var grant = new LicenseGrant(
                    (string)json["licenseType"],
                    (string)json["machineIdHash"],
                    ParseUtc((string)json["issuedAtUtc"]),
                    expires == null || expires.Type == JTokenType.Null ? (DateTime?)null : ParseUtc((string)expires),
                    features);
                return new LicenseServerResponse(grant, (string)json["signature"]);
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is ArgumentException || ex is InvalidCastException)
            {
                throw new LicenseException(LicenseFailure.ResponseRejected, "Phản hồi licence không đúng định dạng");
            }
        }

        private static DateTime ParseUtc(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }
    }
}
