using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal.Config;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Portal
{
    /// <summary>Dựng HTTP request hoàn toàn theo <see cref="PortalApiConfig"/>; không chứa kiến thức riêng về cổng thật.</summary>
    public static class PortalRequestBuilder
    {
        public static HttpRequestMessage BuildProfile(PortalApiConfig config)
        {
            return new HttpRequestMessage(Method(config.Profile.Endpoint), Url(config, config.Profile.Endpoint.Path));
        }

        public static HttpRequestMessage BuildSearch(PortalApiConfig config, InvoiceSearchRequest request)
        {
            var search = config.Search;
            var endpoint = request.Direction == InvoiceDirection.Purchase ? search.Purchase : search.Sold;
            var parameters = new List<KeyValuePair<string, object>>();

            if (search.FixedParameters != null)
            {
                foreach (var pair in search.FixedParameters) parameters.Add(Pair(pair.Key, pair.Value));
            }

            parameters.Add(Pair(search.FromParam, request.From.ToString(search.DateFormat, CultureInfo.InvariantCulture)));
            parameters.Add(Pair(search.ToParam, request.To.ToString(search.DateFormat, CultureInfo.InvariantCulture)));
            parameters.Add(Pair(search.Pagination.PageSizeParam, request.PageSize ?? search.Pagination.PageSize));
            AddPaging(parameters, search.Pagination, request.Cursor);
            AddFilters(parameters, search.FilterParameters, request.Filter);

            var message = new HttpRequestMessage(Method(endpoint), Url(config, endpoint.Path));
            if (search.ParameterLocation == "Body")
            {
                message.Content = JsonBody(parameters);
            }
            else
            {
                message.RequestUri = new Uri(message.RequestUri + "?" + Query(parameters));
            }

            return message;
        }

        public static HttpRequestMessage BuildDownload(PortalApiConfig config, PortalInvoiceRef invoice)
        {
            var download = config.Download;
            var path = download.Endpoint.Path;
            var parameters = new List<KeyValuePair<string, object>>();

            foreach (var pair in download.Parameters)
            {
                var value = RefValue(config, invoice, pair.Value);
                if (download.ParameterLocation == "Path")
                {
                    path = path.Replace("{" + pair.Key + "}", Uri.EscapeDataString(value));
                }
                else
                {
                    parameters.Add(Pair(pair.Key, value));
                }
            }

            var message = new HttpRequestMessage(Method(download.Endpoint), Url(config, path));
            if (parameters.Count > 0)
            {
                if (download.ParameterLocation == "Body") message.Content = JsonBody(parameters);
                else message.RequestUri = new Uri(message.RequestUri + "?" + Query(parameters));
            }

            return message;
        }

        private static void AddPaging(List<KeyValuePair<string, object>> parameters, PaginationConfig paging, string cursor)
        {
            if (paging.Mode == "Page")
            {
                var page = paging.StartPage;
                if (!string.IsNullOrEmpty(cursor) && !int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out page))
                {
                    throw new ArgumentException("Cursor của chế độ Page phải là số trang.");
                }

                parameters.Add(Pair(paging.CursorParam, page));
            }
            else if (!string.IsNullOrEmpty(cursor))
            {
                parameters.Add(Pair(paging.CursorParam, cursor));
            }
        }

        private static void AddFilters(List<KeyValuePair<string, object>> parameters, Dictionary<string, string> map, InvoiceSearchFilter filter)
        {
            if (filter == null || map == null) return;
            AddFilter(parameters, map, "CounterpartyTaxCode", filter.CounterpartyTaxCode);
            AddFilter(parameters, map, "StatusCode", filter.StatusCode);
            AddFilter(parameters, map, "CheckResultCode", filter.CheckResultCode);
            AddFilter(parameters, map, "InvoiceNumber", filter.InvoiceNumber);
            AddFilter(parameters, map, "FormSymbol", filter.FormSymbol);
            AddFilter(parameters, map, "InvoiceSymbol", filter.InvoiceSymbol);
            if (filter.ProxyInvoice.HasValue)
            {
                AddFilter(parameters, map, "ProxyInvoice", filter.ProxyInvoice.Value ? "true" : "false");
            }
        }

        private static void AddFilter(List<KeyValuePair<string, object>> parameters, Dictionary<string, string> map, string field, string value)
        {
            string name;
            // Trường lọc chưa cấu hình tên tham số nghĩa là cổng không hỗ trợ: bỏ qua thay vì gửi sai.
            if (string.IsNullOrWhiteSpace(value) || !map.TryGetValue(field, out name) || string.IsNullOrWhiteSpace(name)) return;
            parameters.Add(Pair(name, value.Trim()));
        }

        private static string RefValue(PortalApiConfig config, PortalInvoiceRef invoice, string field)
        {
            switch (field)
            {
                case "PortalInvoiceId": return invoice.PortalInvoiceId ?? string.Empty;
                case "SellerTaxCode": return invoice.SellerTaxCode ?? string.Empty;
                case "FormSymbol": return invoice.FormSymbol ?? string.Empty;
                case "InvoiceSymbol": return invoice.InvoiceSymbol ?? string.Empty;
                case "InvoiceNumber": return invoice.InvoiceNumber ?? string.Empty;
                case "IssueDate": return invoice.IssueDate.ToString(config.Search.DateFormat, CultureInfo.InvariantCulture);
                case "Direction": return invoice.Direction == InvoiceDirection.Purchase ? "purchase" : "sold";
                default: throw new ArgumentException("Trường tham chiếu không biết: " + field);
            }
        }

        private static string Query(List<KeyValuePair<string, object>> parameters)
        {
            var sb = new StringBuilder();
            foreach (var pair in parameters)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(pair.Key)).Append('=')
                  .Append(Uri.EscapeDataString(Convert.ToString(pair.Value, CultureInfo.InvariantCulture)));
            }

            return sb.ToString();
        }

        private static StringContent JsonBody(List<KeyValuePair<string, object>> parameters)
        {
            var body = new JObject();
            foreach (var pair in parameters) body[pair.Key] = new JValue(pair.Value);
            return new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        }

        private static string Url(PortalApiConfig config, string path)
        {
            return config.BaseUrl.TrimEnd('/') + path;
        }

        private static HttpMethod Method(EndpointConfig endpoint)
        {
            return endpoint.Method == "POST" ? HttpMethod.Post : HttpMethod.Get;
        }

        private static KeyValuePair<string, object> Pair(string key, object value)
        {
            return new KeyValuePair<string, object>(key, value);
        }
    }
}
