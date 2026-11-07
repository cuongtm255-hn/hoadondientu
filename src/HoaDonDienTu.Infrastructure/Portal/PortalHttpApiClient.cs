using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;

namespace HoaDonDienTu.Infrastructure.Portal
{
    /// <summary>
    /// Client HTTP cho một phiên cổng Thuế (một generation). Mọi endpoint/tham số/cấu trúc phản hồi lấy từ
    /// <see cref="PortalApiConfig"/>. Một <see cref="HttpClient"/> được dùng lại cho mọi request của phiên và dispose cùng client.
    /// Không log; lỗi chỉ mang loại lỗi + mã HTTP.
    /// </summary>
    public sealed class PortalHttpApiClient : IPortalApiClient
    {
        private readonly PortalApiConfig _config;
        private readonly PortalSessionMaterial _material;
        private readonly HttpClient _http;

        public PortalHttpApiClient(PortalApiConfig config, PortalSessionMaterial material, HttpMessageHandler handler)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (material == null) throw new ArgumentNullException("material");
            _config = config;
            _material = material;

            // Cookie do WebView thu về được gửi qua header; không dùng CookieContainer chung để phiên không rò sang nơi khác.
            // Handler được tiêm vào (mock/test) thuộc về người tạo nên không bị dispose cùng client.
            _http = handler == null
                ? new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
                : new HttpClient(handler, false);
            _http.Timeout = TimeSpan.FromSeconds(config.Http.TimeoutSeconds);
            if (!string.IsNullOrEmpty(config.Http.UserAgent)) _http.DefaultRequestHeaders.UserAgent.ParseAdd(config.Http.UserAgent);
            if (config.Http.DefaultHeaders != null)
            {
                foreach (var pair in config.Http.DefaultHeaders) _http.DefaultRequestHeaders.TryAddWithoutValidation(pair.Key, pair.Value);
            }
        }

        public async Task<InvoicePage> SearchAsync(InvoiceSearchRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");
            var body = await SendForStringAsync(PortalRequestBuilder.BuildSearch(_config, request), cancellationToken).ConfigureAwait(false);
            return PortalResponseParser.ParseSearch(_config, body);
        }

        public async Task<InvoiceXmlPayload> DownloadXmlAsync(PortalInvoiceRef invoice, CancellationToken cancellationToken)
        {
            if (invoice == null) throw new ArgumentNullException("invoice");
            var message = PortalRequestBuilder.BuildDownload(_config, invoice);

            var content = await SendForBytesAsync(message, cancellationToken).ConfigureAwait(false);
            byte[] package = null;
            if (_config.Download.ResponseMode == "Zip" && content.Length > 0)
            {
                package = content;
                content = PortalResponseParser.ExtractXml(_config.Download, content);
            }

            if (content.Length == 0)
            {
                throw new PortalApiException(PortalErrorKind.SchemaChanged, "Cổng Thuế trả về nội dung XML rỗng.", null, null);
            }

            return new InvoiceXmlPayload(content, FileName(invoice), package);
        }

        /// <summary>
        /// Chi tiết hóa đơn dạng JSON nguyên văn (có danh sách hàng hóa <c>hdhhdvu</c>), dùng cho xem và in hóa đơn.
        /// Giữ nguyên chuỗi để lớp chuẩn hóa (DD-06) tự đọc; ở đây không giả định cấu trúc.
        /// </summary>
        public async Task<string> GetInvoiceDetailAsync(PortalInvoiceRef invoice, CancellationToken cancellationToken)
        {
            if (invoice == null) throw new ArgumentNullException("invoice");
            var body = await SendForStringAsync(PortalRequestBuilder.BuildDetail(_config, invoice), cancellationToken).ConfigureAwait(false);
            PortalResponseParser.ParseJson(body);
            return body;
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private string FileName(PortalInvoiceRef invoice)
        {
            var name = (_config.Download.FileNameTemplate ?? "{PortalInvoiceId}.xml")
                .Replace("{SellerTaxCode}", invoice.SellerTaxCode ?? string.Empty)
                .Replace("{InvoiceSymbol}", invoice.InvoiceSymbol ?? string.Empty)
                .Replace("{InvoiceNumber}", invoice.InvoiceNumber ?? string.Empty)
                .Replace("{PortalInvoiceId}", invoice.PortalInvoiceId ?? string.Empty);

            // Tên file đến từ dữ liệu cổng: loại ký tự không hợp lệ để không thể thoát khỏi thư mục đích.
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private async Task<string> SendForStringAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            using (var response = await SendAsync(message, cancellationToken).ConfigureAwait(false))
            {
                return await ReadAsync(() => response.Content.ReadAsStringAsync()).ConfigureAwait(false);
            }
        }

        private async Task<byte[]> SendForBytesAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            using (var response = await SendAsync(message, cancellationToken).ConfigureAwait(false))
            {
                return await ReadAsync(() => response.Content.ReadAsByteArrayAsync()).ConfigureAwait(false);
            }
        }

        private static async Task<T> ReadAsync<T>(Func<Task<T>> read)
        {
            try
            {
                return await read().ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                throw new PortalApiException(PortalErrorKind.Transient, "Lỗi mạng khi đọc phản hồi của cổng Thuế.", null, null);
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
        {
            ApplyAuthentication(message);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                throw new PortalApiException(PortalErrorKind.Transient, "Hết thời gian chờ phản hồi của cổng Thuế.", null, null);
            }
            catch (HttpRequestException)
            {
                throw new PortalApiException(PortalErrorKind.Transient, "Không kết nối được tới cổng Thuế.", null, null);
            }
            catch (WebException)
            {
                throw new PortalApiException(PortalErrorKind.Transient, "Không kết nối được tới cổng Thuế.", null, null);
            }

            if (response.IsSuccessStatusCode) return response;

            using (response)
            {
                throw PortalErrorMapper.FromResponse(_config, response);
            }
        }

        private void ApplyAuthentication(HttpRequestMessage message)
        {
            var auth = _config.Auth;

            // User-Agent của WebView đã đăng nhập được ưu tiên: lớp bảo vệ của cổng có thể gắn phiên với trình duyệt.
            if (!string.IsNullOrEmpty(_material.UserAgent))
            {
                message.Headers.UserAgent.Clear();
                message.Headers.UserAgent.ParseAdd(_material.UserAgent);
            }

            if (!string.IsNullOrEmpty(_config.Http.RequestIdHeader))
            {
                message.Headers.TryAddWithoutValidation(_config.Http.RequestIdHeader, Guid.NewGuid().ToString());
            }

            if (!string.IsNullOrEmpty(_material.BearerToken))
            {
                var value = string.IsNullOrEmpty(auth.Scheme) ? _material.BearerToken : auth.Scheme + " " + _material.BearerToken;
                message.Headers.TryAddWithoutValidation(auth.HeaderName, value);
            }

            if (auth.SendCookies && _material.Cookies.Count > 0)
            {
                var cookie = string.Join("; ", _material.Cookies.Select(c => c.Key + "=" + c.Value));
                message.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            if (auth.ForwardHeaders != null)
            {
                foreach (var name in auth.ForwardHeaders)
                {
                    string value;
                    if (_material.Headers.TryGetValue(name, out value)) message.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }
    }
}
