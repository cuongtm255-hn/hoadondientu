#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal.Config;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Development.Mock
{
    /// <summary>
    /// Cổng Thuế giả chạy trong tiến trình (không mở socket), đọc cùng <see cref="PortalApiConfig"/> với client thật:
    /// đổi cấu hình (tên tham số, kiểu phân trang, vị trí tham số, đường dẫn JSON...) thì mock đổi theo, nên có thể thử
    /// nhiều "hình dạng API" khác nhau. Token hợp lệ có dạng <c>mock-token-{MST}</c>; token chứa "expired" bị từ chối.
    /// Giới hạn: đường dẫn JSON trong cấu hình chỉ hỗ trợ dạng chấm (a.b.c) khi mock dựng phản hồi.
    /// Đây là hành vi TỰ ĐỊNH NGHĨA, không phản ánh cổng Thuế thật.
    /// </summary>
    public sealed class MockPortalHandler : HttpMessageHandler
    {
        private readonly PortalApiConfig _config;
        private readonly Dictionary<string, MockInvoiceDataset> _datasets = new Dictionary<string, MockInvoiceDataset>();
        private readonly Queue<Func<HttpResponseMessage>> _failures = new Queue<Func<HttpResponseMessage>>();
        private readonly object _gate = new object();

        public MockPortalHandler(PortalApiConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config;
            RequestLog = new List<string>();
            UnsupportedFilterFields = new HashSet<string> { "ProxyInvoice" };
            MaxPageSize = 50;
        }

        /// <summary>Số bản ghi tối đa mỗi trang mà "cổng" chấp nhận, bất kể client xin bao nhiêu.</summary>
        public int MaxPageSize { get; set; }

        public TimeSpan Latency { get; set; }

        /// <summary>Trường lọc mà cổng giả từ chối bằng HTTP 400 (mô phỏng bộ lọc không được hỗ trợ).</summary>
        public HashSet<string> UnsupportedFilterFields { get; private set; }

        /// <summary>Bỏ trường DTO này khỏi từng hóa đơn trong phản hồi (mô phỏng cổng đổi cấu trúc).</summary>
        public string OmitDtoField { get; set; }

        /// <summary>Trả về thân phản hồi tra cứu không phải JSON.</summary>
        public bool MalformedSearchResponse { get; set; }

        /// <summary>Mỗi request ghi một dòng "METHOD /path?query" (không có header/token).</summary>
        public List<string> RequestLog { get; private set; }

        public void EnqueueFailure(int status, int? retryAfterSeconds = null)
        {
            lock (_gate)
            {
                _failures.Enqueue(() =>
                {
                    var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("{\"error\":\"mock failure\"}", Encoding.UTF8, "application/json") };
                    if (retryAfterSeconds.HasValue) response.Headers.TryAddWithoutValidation("Retry-After", retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture));
                    return response;
                });
            }
        }

        public void EnqueueNetworkError()
        {
            lock (_gate)
            {
                _failures.Enqueue(() => { throw new HttpRequestException("mock network error"); });
            }
        }

        public MockInvoiceDataset DatasetFor(string taxCode)
        {
            lock (_gate)
            {
                MockInvoiceDataset dataset;
                if (!_datasets.TryGetValue(taxCode, out dataset))
                {
                    dataset = new MockInvoiceDataset(taxCode);
                    _datasets[taxCode] = dataset;
                }

                return dataset;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Func<HttpResponseMessage> failure = null;
            lock (_gate)
            {
                RequestLog.Add(request.Method.Method + " " + request.RequestUri.PathAndQuery);
                if (_failures.Count > 0) failure = _failures.Dequeue();
            }

            if (Latency > TimeSpan.Zero) await Task.Delay(Latency, cancellationToken).ConfigureAwait(false);
            if (failure != null) return failure();

            var taxCode = AuthenticatedTaxCode(request);
            if (taxCode == null) return Error(HttpStatusCode.Unauthorized, "unauthorized");

            var path = request.RequestUri.AbsolutePath;
            if (path == _config.Profile.Endpoint.Path) return Profile(taxCode);
            if (path == _config.Search.Purchase.Path) return await Search(request, taxCode, InvoiceDirection.Purchase).ConfigureAwait(false);
            if (path == _config.Search.Sold.Path) return await Search(request, taxCode, InvoiceDirection.Sold).ConfigureAwait(false);

            var downloadId = await DownloadId(request, path).ConfigureAwait(false);
            if (downloadId != null) return Download(taxCode, downloadId);

            return Error(HttpStatusCode.NotFound, "not found");
        }

        private string AuthenticatedTaxCode(HttpRequestMessage request)
        {
            IEnumerable<string> values;
            if (!request.Headers.TryGetValues(_config.Auth.HeaderName, out values)) return null;
            var value = values.FirstOrDefault();
            if (string.IsNullOrEmpty(value)) return null;

            var scheme = string.IsNullOrEmpty(_config.Auth.Scheme) ? string.Empty : _config.Auth.Scheme + " ";
            if (!value.StartsWith(scheme + MockInvoiceDataset.TokenPrefix, StringComparison.Ordinal)) return null;
            var taxCode = value.Substring(scheme.Length + MockInvoiceDataset.TokenPrefix.Length);
            return taxCode.Length == 0 || taxCode.Contains("expired") ? null : taxCode;
        }

        private HttpResponseMessage Profile(string taxCode)
        {
            var body = new JObject();
            SetPath(body, _config.Profile.TaxCodePath, new JValue(taxCode));
            if (!string.IsNullOrEmpty(_config.Profile.NamePath)) SetPath(body, _config.Profile.NamePath, new JValue("Công ty mẫu " + taxCode));
            return Json(body);
        }

        private async Task<HttpResponseMessage> Search(HttpRequestMessage request, string taxCode, InvoiceDirection direction)
        {
            var search = _config.Search;
            var parameters = await ReadParameters(request, search.ParameterLocation == "Body").ConfigureAwait(false);

            DateTime from, to;
            string fromText, toText;
            if (!parameters.TryGetValue(search.FromParam, out fromText) || !parameters.TryGetValue(search.ToParam, out toText)
                || !DateTime.TryParseExact(fromText, search.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out from)
                || !DateTime.TryParseExact(toText, search.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
            {
                return Error(HttpStatusCode.BadRequest, "invalid date range");
            }

            // Cổng thật giới hạn một tháng mỗi lần tra cứu; mock mô phỏng bằng cách từ chối khoảng dài hơn 31 ngày.
            if (to < from || (to - from).TotalDays > 31) return Error(HttpStatusCode.BadRequest, "range too large");

            var pageSize = MaxPageSize;
            string sizeText;
            if (parameters.TryGetValue(search.Pagination.PageSizeParam, out sizeText))
            {
                int requested;
                if (!int.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out requested) || requested <= 0) return Error(HttpStatusCode.BadRequest, "invalid size");
                pageSize = Math.Min(requested, MaxPageSize);
            }

            int offset;
            if (!TryReadOffset(parameters, search.Pagination, pageSize, out offset)) return Error(HttpStatusCode.BadRequest, "invalid cursor");

            var records = DatasetFor(taxCode).Query(direction, from, to).AsEnumerable();
            foreach (var pair in search.FilterParameters ?? new Dictionary<string, string>())
            {
                string value;
                if (string.IsNullOrEmpty(pair.Value) || !parameters.TryGetValue(pair.Value, out value)) continue;
                if (UnsupportedFilterFields.Contains(pair.Key)) return Error(HttpStatusCode.BadRequest, "unsupported filter");
                records = ApplyFilter(records, pair.Key, value, direction);
            }

            var matches = records.ToList();
            var pageItems = matches.Skip(offset).Take(pageSize).ToList();
            var hasMore = offset + pageItems.Count < matches.Count;

            if (MalformedSearchResponse)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>not json</html>", Encoding.UTF8, "text/html") };
            }

            var response = search.Response;
            var body = new JObject();
            var items = new JArray(pageItems.Select(r => MapItem(response, r)));
            SetPath(body, response.ItemsPath, items);
            if (!string.IsNullOrEmpty(response.TotalPath)) SetPath(body, response.TotalPath, new JValue(matches.Count));
            if (!string.IsNullOrEmpty(response.HasMorePath)) SetPath(body, response.HasMorePath, new JValue(hasMore));
            if (search.Pagination.Mode == "Cursor" && !string.IsNullOrEmpty(response.NextCursorPath))
            {
                SetPath(body, response.NextCursorPath, hasMore ? new JValue(EncodeCursor(offset + pageItems.Count)) : JValue.CreateNull());
            }

            return Json(body);
        }

        private HttpResponseMessage Download(string taxCode, string id)
        {
            var record = DatasetFor(taxCode).Find(id);
            if (record == null) return Error(HttpStatusCode.NotFound, "invoice not found");

            var xml = MockInvoiceDataset.BuildXml(record);
            if (_config.Download.ResponseMode == "JsonBase64")
            {
                var body = new JObject();
                SetPath(body, _config.Download.ContentPath, new JValue(Convert.ToBase64String(xml)));
                return Json(body);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(xml) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            return response;
        }

        private async Task<string> DownloadId(HttpRequestMessage request, string path)
        {
            var download = _config.Download;
            var idParam = download.Parameters.First(p => p.Value == "PortalInvoiceId").Key;

            if (download.ParameterLocation == "Path")
            {
                var pattern = "^" + Regex.Escape(download.Endpoint.Path).Replace(Regex.Escape("{" + idParam + "}"), "([^/]+)") + "$";
                var match = Regex.Match(path, pattern);
                return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
            }

            if (path != download.Endpoint.Path) return null;
            var parameters = await ReadParameters(request, download.ParameterLocation == "Body").ConfigureAwait(false);
            string id;
            return parameters.TryGetValue(idParam, out id) ? id : null;
        }

        private bool TryReadOffset(Dictionary<string, string> parameters, PaginationConfig paging, int pageSize, out int offset)
        {
            offset = 0;
            string cursor;
            if (!parameters.TryGetValue(paging.CursorParam, out cursor) || string.IsNullOrEmpty(cursor))
            {
                return paging.Mode != "Page" || paging.StartPage >= 0;
            }

            if (paging.Mode == "Page")
            {
                int page;
                if (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < paging.StartPage) return false;
                offset = (page - paging.StartPage) * pageSize;
                return true;
            }

            try
            {
                var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                return text.StartsWith("o:", StringComparison.Ordinal)
                    && int.TryParse(text.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out offset);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string EncodeCursor(int offset)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes("o:" + offset.ToString(CultureInfo.InvariantCulture)));
        }

        private static IEnumerable<MockInvoiceRecord> ApplyFilter(IEnumerable<MockInvoiceRecord> records, string field, string value, InvoiceDirection direction)
        {
            value = value.Trim();
            switch (field)
            {
                case "CounterpartyTaxCode":
                    return records.Where(r => (direction == InvoiceDirection.Purchase ? r.Dto.SellerTaxCode : r.Dto.BuyerTaxCode) == value);
                case "StatusCode": return records.Where(r => r.Dto.StatusRaw == value);
                case "CheckResultCode": return records.Where(r => r.Dto.CheckResultRaw == value);
                case "InvoiceNumber": return records.Where(r => r.Dto.InvoiceNumber == value);
                case "FormSymbol": return records.Where(r => r.Dto.FormSymbol == value);
                case "InvoiceSymbol": return records.Where(r => r.Dto.InvoiceSymbol == value);
                default: return records;
            }
        }

        private JObject MapItem(SearchResponseConfig response, MockInvoiceRecord record)
        {
            var item = new JObject();
            foreach (var pair in response.Fields)
            {
                if (pair.Key == OmitDtoField) continue;
                var property = typeof(PortalInvoiceDto).GetProperty(pair.Key);
                var value = property == null ? null : property.GetValue(record.Dto, null);
                if (value == null) continue;

                if (value is DateTime) SetPath(item, pair.Value, new JValue(((DateTime)value).ToString(response.DateFormats[0], CultureInfo.InvariantCulture)));
                else if (value is decimal) SetPath(item, pair.Value, new JValue((decimal)value));
                else SetPath(item, pair.Value, new JValue(Convert.ToString(value, CultureInfo.InvariantCulture)));
            }

            return item;
        }

        private static async Task<Dictionary<string, string>> ReadParameters(HttpRequestMessage request, bool fromBody)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (fromBody)
            {
                if (request.Content == null) return result;
                var text = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(text);
                foreach (var property in json.Properties()) result[property.Name] = Convert.ToString(property.Value, CultureInfo.InvariantCulture);
                return result;
            }

            var query = request.RequestUri.Query.TrimStart('?');
            foreach (var part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var index = part.IndexOf('=');
                var key = index < 0 ? part : part.Substring(0, index);
                var value = index < 0 ? string.Empty : part.Substring(index + 1);
                result[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return result;
        }

        private static void SetPath(JObject root, string path, JToken value)
        {
            var parts = path.Split('.');
            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var next = current[parts[i]] as JObject;
                if (next == null)
                {
                    next = new JObject();
                    current[parts[i]] = next;
                }

                current = next;
            }

            current[parts[parts.Length - 1]] = value;
        }

        private static HttpResponseMessage Json(JToken body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage Error(HttpStatusCode status, string code)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"error\":{\"code\":\"" + code + "\"}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
#endif
