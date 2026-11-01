using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal.Config;

namespace HoaDonDienTu.Infrastructure.Portal
{
    /// <summary>
    /// Dựng HTTP request theo <see cref="PortalApiConfig"/>. Tra cứu dùng một tham số <c>search</c> dạng RSQL, ví dụ
    /// <c>tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;tthai==1;nmmst==0101654633</c> (sample-api-web.md mục 2).
    /// </summary>
    public static class PortalRequestBuilder
    {
        public static HttpRequestMessage BuildSearch(PortalApiConfig config, InvoiceSearchRequest request)
        {
            var search = config.Search;
            var endpoint = request.Direction == InvoiceDirection.Purchase ? search.Purchase : search.Sold;
            var parameters = new List<KeyValuePair<string, string>>();

            if (search.FixedParameters != null)
            {
                foreach (var pair in search.FixedParameters) parameters.Add(Pair(pair.Key, pair.Value));
            }

            parameters.Add(Pair(search.Pagination.PageSizeParam, (request.PageSize ?? search.Pagination.PageSize).ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrEmpty(request.Cursor)) parameters.Add(Pair(search.Pagination.CursorParam, request.Cursor));
            parameters.Add(Pair(search.Rsql.Param, BuildRsql(config, request)));

            var message = new HttpRequestMessage(HttpMethod.Get, Url(config, endpoint.Path, parameters));
            SetAction(config, message, endpoint, request.Direction);
            return message;
        }

        /// <summary>Biểu thức <c>search</c>: khoảng ngày lập (bắt buộc) rồi các bộ lọc có giá trị; "Tất cả" không được gửi.</summary>
        public static string BuildRsql(PortalApiConfig config, InvoiceSearchRequest request)
        {
            var rsql = config.Search.Rsql;
            var terms = new List<string>
            {
                rsql.DateField + "=ge=" + request.From.Date.ToString(rsql.DateTimeFormat, CultureInfo.InvariantCulture),
                rsql.DateField + "=le=" + request.To.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToString(rsql.DateTimeFormat, CultureInfo.InvariantCulture)
            };

            var map = request.Direction == InvoiceDirection.Purchase ? config.Search.PurchaseFilterFields : config.Search.SoldFilterFields;
            var filter = request.Filter;
            if (filter != null && map != null)
            {
                // Thứ tự giống form của cổng (sample-api-web.md mục 2): MST, trạng thái, kết quả kiểm tra, mẫu số, số, ký hiệu, ủy nhiệm, CCCD.
                AddTerm(terms, map, "CounterpartyTaxCode", filter.CounterpartyTaxCode);
                AddTerm(terms, map, "StatusCode", filter.StatusCode);
                AddTerm(terms, map, "CheckResultCode", filter.CheckResultCode);
                AddTerm(terms, map, "FormSymbol", filter.FormSymbol);
                AddTerm(terms, map, "InvoiceNumber", filter.InvoiceNumber);
                AddTerm(terms, map, "InvoiceSymbol", filter.InvoiceSymbol);

                // Hóa đơn ủy nhiệm là checkbox: chỉ có "tích" mới gửi, bỏ tích thì không gửi gì (không phải =0).
                if (filter.ProxyInvoice == true) AddTerm(terms, map, "ProxyInvoice", rsql.ProxyTrueValue);
                AddTerm(terms, map, "BuyerIdNumber", filter.BuyerIdNumber);
            }

            return string.Join(rsql.Separator, terms);
        }

        public static HttpRequestMessage BuildDetail(PortalApiConfig config, PortalInvoiceRef invoice)
        {
            var detail = config.Detail;
            var message = new HttpRequestMessage(HttpMethod.Get, Url(config, detail.Endpoint.Path, KeyParameters(detail.Parameters, invoice)));
            SetAction(config, message, detail.Endpoint, invoice.Direction);
            return message;
        }

        public static HttpRequestMessage BuildDownload(PortalApiConfig config, PortalInvoiceRef invoice)
        {
            var download = config.Download;
            var message = new HttpRequestMessage(HttpMethod.Get, Url(config, download.Endpoint.Path, KeyParameters(download.Parameters, invoice)));
            SetAction(config, message, download.Endpoint, invoice.Direction);
            return message;
        }

        /// <summary>Tên hiển thị của chiều hóa đơn trong header action của cổng web.</summary>
        public static string DirectionText(InvoiceDirection direction)
        {
            return direction == InvoiceDirection.Purchase ? "mua vào" : "bán ra";
        }

        /// <summary>Mã hóa phần trăm giống <c>encodeURIComponent</c> của trình duyệt (giữ nguyên ! ' ( ) *).</summary>
        public static string EncodeHeaderValue(string value)
        {
            return Uri.EscapeDataString(value)
                .Replace("%21", "!").Replace("%27", "'").Replace("%28", "(").Replace("%29", ")").Replace("%2A", "*");
        }

        private static void SetAction(PortalApiConfig config, HttpRequestMessage message, EndpointConfig endpoint, InvoiceDirection direction)
        {
            if (string.IsNullOrEmpty(endpoint.Action) || string.IsNullOrEmpty(config.Http.ActionHeader)) return;
            message.Headers.TryAddWithoutValidation(
                config.Http.ActionHeader, EncodeHeaderValue(endpoint.Action.Replace("{direction}", DirectionText(direction))));
        }

        private static List<KeyValuePair<string, string>> KeyParameters(Dictionary<string, string> map, PortalInvoiceRef invoice)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var pair in map) list.Add(Pair(pair.Key, RefValue(invoice, pair.Value)));
            return list;
        }

        private static void AddTerm(List<string> terms, Dictionary<string, string> map, string field, string value)
        {
            string name;
            // Trường lọc chưa có tên RSQL nghĩa là cổng không hỗ trợ ở chiều này: bỏ qua thay vì gửi sai.
            if (string.IsNullOrWhiteSpace(value) || !map.TryGetValue(field, out name) || string.IsNullOrWhiteSpace(name)) return;
            terms.Add(name + "==" + RsqlValue(value.Trim()));
        }

        /// <summary>Giá trị chứa ký tự đặc biệt của RSQL thì đặt trong dấu nháy kép để không phá cấu trúc biểu thức.</summary>
        private static string RsqlValue(string value)
        {
            return value.IndexOfAny(new[] { ';', ',', '(', ')', '=', '!', '<', '>', '"', '\'', ' ' }) >= 0
                ? "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
                : value;
        }

        private static string RefValue(PortalInvoiceRef invoice, string field)
        {
            switch (field)
            {
                case "PortalInvoiceId": return invoice.PortalInvoiceId ?? string.Empty;
                case "SellerTaxCode": return invoice.SellerTaxCode ?? string.Empty;
                case "FormSymbol": return invoice.FormSymbol ?? string.Empty;
                case "InvoiceSymbol": return invoice.InvoiceSymbol ?? string.Empty;
                case "InvoiceNumber": return invoice.InvoiceNumber ?? string.Empty;
                default: throw new ArgumentException("Trường tham chiếu không biết: " + field);
            }
        }

        /// <summary>
        /// Ghép URL. Dấu <c>=</c>, <c>;</c>, <c>:</c> và <c>/</c> trong tham số được giữ nguyên như cổng web gửi
        /// (<c>search=tdlap=ge=...;tdlap=le=...</c>, <c>sort=tdlap:desc</c>); các ký tự còn lại mã hóa phần trăm.
        /// </summary>
        private static Uri Url(PortalApiConfig config, string path, List<KeyValuePair<string, string>> parameters)
        {
            var sb = new StringBuilder(config.BaseUrl.TrimEnd('/')).Append(path);
            for (var i = 0; i < parameters.Count; i++)
            {
                sb.Append(i == 0 ? '?' : '&').Append(Uri.EscapeDataString(parameters[i].Key)).Append('=').Append(EscapeValue(parameters[i].Value));
            }

            return new Uri(sb.ToString());
        }

        private static string EscapeValue(string value)
        {
            return Uri.EscapeDataString(value).Replace("%3D", "=").Replace("%3B", ";").Replace("%3A", ":").Replace("%2F", "/");
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
