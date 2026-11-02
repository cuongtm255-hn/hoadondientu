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
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal.Config;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Development.Mock
{
    /// <summary>
    /// Cổng Thuế giả chạy trong tiến trình (không mở socket) mô phỏng đúng hình dạng API thật đã ghi lại trong
    /// <c>docs/api-samples/sample-api-web.md</c>: xác thực <c>Authorization: Bearer &lt;JWT&gt;</c> (claim <c>sub</c> là MST, <c>exp</c> hết hạn → 401),
    /// tra cứu <c>GET {sold|purchase}?sort=&amp;size=&amp;state=&amp;search=tdlap=ge=…;tdlap=le=…;tthai==…</c> trả <c>{datas, total, state, time}</c>,
    /// chi tiết và xuất XML theo khóa <c>nbmst, khhdon, shdon, khmshdon</c> (xuất XML trả file zip). Đường dẫn lấy từ cùng
    /// <see cref="PortalApiConfig"/> với client thật.
    /// Phần chưa có mẫu thật (tra cứu mua vào, trang kế tiếp bằng <c>state</c>, giới hạn <c>size</c>) là giả định và được đánh dấu như vậy.
    /// </summary>
    public sealed class MockPortalHandler : HttpMessageHandler
    {
        private static readonly Regex Term = new Regex("^(?<field>[A-Za-z0-9_]+)(?<op>=ge=|=le=|==)(?<value>.*)$", RegexOptions.Compiled);

        private readonly PortalApiConfig _config;
        private readonly Dictionary<string, MockInvoiceDataset> _datasets = new Dictionary<string, MockInvoiceDataset>();
        private readonly Queue<Func<HttpResponseMessage>> _failures = new Queue<Func<HttpResponseMessage>>();
        private readonly object _gate = new object();

        public MockPortalHandler(PortalApiConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config;
            RequestLog = new List<string>();
            HeaderLog = new List<Dictionary<string, string>>();
            RejectedRsqlFields = new HashSet<string>();
            MaxPageSize = 50;
        }

        /// <summary>Số bản ghi tối đa mỗi trang mà "cổng" chấp nhận, bất kể client xin bao nhiêu (giả định, chưa có mẫu thật).</summary>
        public int MaxPageSize { get; set; }

        public TimeSpan Latency { get; set; }

        /// <summary>Tên trường RSQL (ví dụ <c>unhiem</c>) mà cổng giả từ chối bằng HTTP 400 (mô phỏng bộ lọc không được hỗ trợ).</summary>
        public HashSet<string> RejectedRsqlFields { get; private set; }

        /// <summary>Bỏ trường JSON này khỏi từng hóa đơn trong phản hồi tra cứu (mô phỏng cổng đổi cấu trúc).</summary>
        public string OmitJsonField { get; set; }

        /// <summary>Trả về thân phản hồi tra cứu không phải JSON.</summary>
        public bool MalformedSearchResponse { get; set; }

        /// <summary>Hóa đơn có số này được xuất ra zip mà <c>invoice.xml</c> bên trong bị hỏng (XML không đọc được).</summary>
        public string CorruptXmlForInvoiceNumber { get; set; }

        /// <summary>Trả về thân phản hồi xuất XML không phải file zip.</summary>
        public bool MalformedExportResponse { get; set; }

        /// <summary>Mỗi request ghi một dòng "METHOD /path?query" (không có header/token).</summary>
        public List<string> RequestLog { get; private set; }

        /// <summary>Các header (trừ Authorization và Cookie) của từng request, để kiểm tra action/request-id/User-Agent.</summary>
        public List<Dictionary<string, string>> HeaderLog { get; private set; }

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
                HeaderLog.Add(request.Headers.Where(h => !string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(h.Key, "Cookie", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(h => h.Key, h => string.Join(string.Equals(h.Key, "User-Agent", StringComparison.OrdinalIgnoreCase) ? " " : ",", h.Value), StringComparer.OrdinalIgnoreCase));
                if (_failures.Count > 0) failure = _failures.Dequeue();
            }

            if (Latency > TimeSpan.Zero) await Task.Delay(Latency, cancellationToken).ConfigureAwait(false);
            if (failure != null) return failure();

            var taxCode = AuthenticatedTaxCode(request);
            if (taxCode == null) return Error(HttpStatusCode.Unauthorized, "unauthorized");

            var path = request.RequestUri.AbsolutePath;
            var query = ReadQuery(request);
            if (path == _config.Search.Sold.Path) return Search(query, taxCode, InvoiceDirection.Sold);
            if (path == _config.Search.Purchase.Path) return Search(query, taxCode, InvoiceDirection.Purchase);
            if (path == _config.Detail.Endpoint.Path) return Detail(query, taxCode);
            if (path == _config.Download.Endpoint.Path) return ExportXml(query, taxCode);

            return Error(HttpStatusCode.NotFound, "not found");
        }

        /// <summary>MST trong claim <c>sub</c> của JWT; null khi thiếu/sai token hoặc token hết hạn.</summary>
        private string AuthenticatedTaxCode(HttpRequestMessage request)
        {
            IEnumerable<string> values;
            if (!request.Headers.TryGetValues(_config.Auth.HeaderName, out values)) return null;
            var value = values.FirstOrDefault();
            var scheme = string.IsNullOrEmpty(_config.Auth.Scheme) ? string.Empty : _config.Auth.Scheme + " ";
            if (string.IsNullOrEmpty(value) || !value.StartsWith(scheme, StringComparison.Ordinal)) return null;

            PortalToken token;
            if (!PortalToken.TryParse(value.Substring(scheme.Length), out token) || token.IsExpired(DateTime.UtcNow)) return null;
            return token.Subject;
        }

        private HttpResponseMessage Search(Dictionary<string, string> query, string taxCode, InvoiceDirection direction)
        {
            var search = _config.Search;
            string rsql;
            if (!query.TryGetValue(search.Rsql.Param, out rsql)) return Error(HttpStatusCode.BadRequest, "missing search");

            DateTime from = DateTime.MinValue, to = DateTime.MinValue;
            var hasFrom = false;
            var hasTo = false;
            var filters = new List<KeyValuePair<string, string>>();
            foreach (var raw in rsql.Split(new[] { search.Rsql.Separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Term.Match(raw);
                if (!match.Success) return Error(HttpStatusCode.BadRequest, "invalid search");
                var field = match.Groups["field"].Value;
                var op = match.Groups["op"].Value;
                var value = Unquote(match.Groups["value"].Value);

                if (field == search.Rsql.DateField)
                {
                    DateTime parsed;
                    if (!DateTime.TryParseExact(value, search.Rsql.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return Error(HttpStatusCode.BadRequest, "invalid date");
                    if (op == "=ge=") { from = parsed; hasFrom = true; }
                    else if (op == "=le=") { to = parsed; hasTo = true; }
                    else return Error(HttpStatusCode.BadRequest, "invalid date operator");
                }
                else
                {
                    if (op != "==" || RejectedRsqlFields.Contains(field)) return Error(HttpStatusCode.BadRequest, "unsupported filter");
                    filters.Add(new KeyValuePair<string, string>(field, value));
                }
            }

            // Ngày lập là điều kiện bắt buộc của form tra cứu; cổng giới hạn khoảng tra cứu tối đa 31 ngày.
            if (!hasFrom || !hasTo || to < from || (to.Date - from.Date).TotalDays > 31) return Error(HttpStatusCode.BadRequest, "invalid range");

            var pageSize = MaxPageSize;
            string sizeText;
            if (query.TryGetValue(search.Pagination.PageSizeParam, out sizeText))
            {
                int requested;
                if (!int.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out requested) || requested <= 0) return Error(HttpStatusCode.BadRequest, "invalid size");
                pageSize = Math.Min(requested, MaxPageSize);
            }

            var offset = 0;
            string state;
            if (query.TryGetValue(search.Pagination.CursorParam, out state) && !string.IsNullOrEmpty(state) && !TryDecodeCursor(state, out offset))
            {
                return Error(HttpStatusCode.BadRequest, "invalid state");
            }

            var records = DatasetFor(taxCode).Query(direction, from, to).AsEnumerable();
            foreach (var filter in filters)
            {
                bool known;
                records = ApplyFilter(records, filter.Key, filter.Value, direction, out known);
                if (!known) return Error(HttpStatusCode.BadRequest, "unsupported filter");
            }

            var matches = records.ToList();
            var pageItems = matches.Skip(offset).Take(pageSize).ToList();
            var hasMore = offset + pageItems.Count < matches.Count;

            if (MalformedSearchResponse)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>not json</html>", Encoding.UTF8, "text/html") };
            }

            var items = new JArray(pageItems.Select(r =>
            {
                var json = MockInvoiceDataset.ToSearchJson(r);
                if (!string.IsNullOrEmpty(OmitJsonField)) json.Remove(OmitJsonField);
                return json;
            }));

            return Json(new JObject
            {
                { "datas", items },
                { "total", matches.Count },
                { "state", hasMore ? new JValue(EncodeCursor(offset + pageItems.Count)) : JValue.CreateNull() },
                { "time", 12 }
            });
        }

        private HttpResponseMessage Detail(Dictionary<string, string> query, string taxCode)
        {
            var record = FindByKey(query, _config.Detail.Parameters, taxCode);
            return record == null ? Error(HttpStatusCode.NotFound, "invoice not found") : Json(MockInvoiceDataset.ToDetailJson(record));
        }

        private HttpResponseMessage ExportXml(Dictionary<string, string> query, string taxCode)
        {
            var record = FindByKey(query, _config.Download.Parameters, taxCode);
            if (record == null) return Error(HttpStatusCode.NotFound, "invoice not found");

            var bytes = MalformedExportResponse ? Encoding.UTF8.GetBytes("not a zip")
                : record.Dto.InvoiceNumber == CorruptXmlForInvoiceNumber ? MockInvoiceDataset.BuildExportZip(record, Encoding.UTF8.GetBytes("<HDon><broken>"))
                : MockInvoiceDataset.BuildExportZip(record);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            return response;
        }

        private MockInvoiceRecord FindByKey(Dictionary<string, string> query, Dictionary<string, string> map, string taxCode)
        {
            string Get(string refField)
            {
                var name = map.First(p => p.Value == refField).Key;
                string value;
                return query.TryGetValue(name, out value) ? value : null;
            }

            var seller = Get("SellerTaxCode");
            var symbol = Get("InvoiceSymbol");
            var number = Get("InvoiceNumber");
            var form = Get("FormSymbol");
            if (seller == null || symbol == null || number == null || form == null) return null;
            return DatasetFor(taxCode).Find(seller, symbol, number, form);
        }

        private static IEnumerable<MockInvoiceRecord> ApplyFilter(
            IEnumerable<MockInvoiceRecord> records, string field, string value, InvoiceDirection direction, out bool known)
        {
            known = true;
            switch (field)
            {
                case "nmmst":
                    if (direction == InvoiceDirection.Purchase) break;
                    return records.Where(r => r.Dto.BuyerTaxCode == value);
                case "nbmst":
                    if (direction == InvoiceDirection.Sold) break;
                    return records.Where(r => r.Dto.SellerTaxCode == value);
                case "nmcmnd":
                    if (direction == InvoiceDirection.Purchase) break;
                    return records.Where(r => r.Dto.BuyerIdNumber == value);
                case "tthai": return records.Where(r => r.Dto.StatusRaw == value);
                case "ttxly": return records.Where(r => r.Dto.CheckResultRaw == value);
                case "khmshdon": return records.Where(r => r.Dto.FormSymbol == value);
                case "khhdon": return records.Where(r => r.Dto.InvoiceSymbol == value);
                case "shdon": return records.Where(r => r.Dto.InvoiceNumber == value);
                case "unhiem": return value == "1" ? records.Where(r => r.Proxy) : records.Where(r => !r.Proxy);
            }

            known = false;
            return records;
        }

        /// <summary>Giá trị RSQL đặt trong dấu nháy kép (khi chứa ký tự đặc biệt) được bỏ nháy và bỏ ký tự thoát.</summary>
        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            }

            return value;
        }

        private static string EncodeCursor(int offset)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes("o:" + offset.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool TryDecodeCursor(string cursor, out int offset)
        {
            offset = 0;
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

        private static Dictionary<string, string> ReadQuery(HttpRequestMessage request)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var query = request.RequestUri.Query.TrimStart('?');
            foreach (var part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // Chỉ tách ở dấu '=' đầu tiên: giá trị search chứa nhiều dấu '=' (tdlap=ge=...).
                var index = part.IndexOf('=');
                var key = index < 0 ? part : part.Substring(0, index);
                var value = index < 0 ? string.Empty : part.Substring(index + 1);
                result[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return result;
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
