using System;
using System.Collections.Generic;
using System.Linq;
using HoaDonDienTu.Application.Portal;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    public static class PortalApiConfigValidator
    {
        public const int CurrentSchemaVersion = 2;

        /// <summary>Tên các trường của <c>PortalInvoiceDto</c> cho phép ánh xạ.</summary>
        public static readonly string[] DtoFields =
        {
            "PortalInvoiceId", "FormSymbol", "InvoiceSymbol", "InvoiceNumber", "IssueDate", "SellerTaxCode", "SellerName",
            "BuyerTaxCode", "BuyerIdNumber", "BuyerName", "BuyerAddress", "TotalBeforeTax", "TotalTax", "TotalDiscount", "TotalFee",
            "TotalPayment", "Currency", "ExchangeRate", "StatusRaw", "CheckResultRaw"
        };

        /// <summary>
        /// Các trường bắt buộc: thiếu trong phản hồi nghĩa là cổng đã đổi cấu trúc. Khóa tổng hợp của hóa đơn (người bán, ký hiệu mẫu số,
        /// ký hiệu, số) là thứ cổng dùng để xem chi tiết và xuất XML nên đều bắt buộc.
        /// </summary>
        public static readonly string[] RequiredDtoFields =
        {
            "PortalInvoiceId", "SellerTaxCode", "FormSymbol", "InvoiceSymbol", "InvoiceNumber", "IssueDate"
        };

        public static readonly string[] FilterFields =
        {
            "CounterpartyTaxCode", "BuyerIdNumber", "StatusCode", "CheckResultCode", "InvoiceNumber", "FormSymbol", "InvoiceSymbol", "ProxyInvoice"
        };

        public static readonly string[] RefFields = { "PortalInvoiceId", "SellerTaxCode", "FormSymbol", "InvoiceSymbol", "InvoiceNumber" };

        public static IList<string> Validate(PortalApiConfig c)
        {
            var errors = new List<string>();
            if (c == null)
            {
                errors.Add("Thiếu cấu hình.");
                return errors;
            }

            if (c.SchemaVersion != CurrentSchemaVersion) errors.Add("schemaVersion phải là " + CurrentSchemaVersion + ".");

            Uri baseUri;
            if (string.IsNullOrWhiteSpace(c.BaseUrl) || !Uri.TryCreate(c.BaseUrl, UriKind.Absolute, out baseUri))
            {
                errors.Add("baseUrl không hợp lệ.");
            }
            else if (baseUri.Scheme != Uri.UriSchemeHttps)
            {
                errors.Add("baseUrl phải dùng https (TLS bắt buộc).");
            }

            if (c.Auth == null || string.IsNullOrWhiteSpace(c.Auth.HeaderName)) errors.Add("auth.headerName là bắt buộc.");
            else
            {
                if (string.IsNullOrWhiteSpace(c.Auth.AuthenticatePath) || !c.Auth.AuthenticatePath.StartsWith("/", StringComparison.Ordinal))
                {
                    errors.Add("auth.authenticatePath phải bắt đầu bằng '/'.");
                }

                if (string.IsNullOrWhiteSpace(c.Auth.TokenResponsePath)) errors.Add("auth.tokenResponsePath là bắt buộc.");
            }

            if (c.Http == null || c.Http.TimeoutSeconds <= 0) errors.Add("http.timeoutSeconds phải > 0.");
            if (c.RateLimit == null || c.RateLimit.MinIntervalMs < 0) errors.Add("rateLimit.minIntervalMs phải >= 0.");

            ValidateSearch(c.Search, errors);
            ValidateKeyed(c.Detail == null ? null : c.Detail.Endpoint, c.Detail == null ? null : c.Detail.Parameters, "detail", errors);
            ValidateDownload(c.Download, errors);
            if (c.ExportExcel != null) ValidateEndpoint(c.ExportExcel.Endpoint, "exportExcel.endpoint", errors);

            if (c.Errors != null && c.Errors.StatusKinds != null)
            {
                foreach (var pair in c.Errors.StatusKinds)
                {
                    PortalErrorKind kind;
                    if (!Enum.TryParse(pair.Value, false, out kind))
                    {
                        errors.Add("errors.statusKinds[" + pair.Key + "] không phải PortalErrorKind: " + pair.Value);
                    }
                }
            }

            return errors;
        }

        private static void ValidateSearch(SearchConfig s, List<string> errors)
        {
            if (s == null) { errors.Add("Thiếu search."); return; }
            ValidateEndpoint(s.Purchase, "search.purchase", errors);
            ValidateEndpoint(s.Sold, "search.sold", errors);
            if (s.Purchase != null && s.Sold != null && s.Purchase.Path == s.Sold.Path)
            {
                errors.Add("search.purchase và search.sold không được trùng đường dẫn.");
            }

            var q = s.Rsql;
            if (q == null) errors.Add("Thiếu search.rsql.");
            else
            {
                if (string.IsNullOrWhiteSpace(q.Param)) errors.Add("search.rsql.param là bắt buộc.");
                if (string.IsNullOrEmpty(q.Separator)) errors.Add("search.rsql.separator là bắt buộc.");
                if (string.IsNullOrWhiteSpace(q.DateField)) errors.Add("search.rsql.dateField là bắt buộc.");
                if (string.IsNullOrWhiteSpace(q.DateTimeFormat)) errors.Add("search.rsql.dateTimeFormat là bắt buộc.");
            }

            var p = s.Pagination;
            if (p == null) errors.Add("Thiếu search.pagination.");
            else
            {
                if (p.PageSize <= 0) errors.Add("search.pagination.pageSize phải > 0.");
                if (string.IsNullOrWhiteSpace(p.PageSizeParam)) errors.Add("search.pagination.pageSizeParam là bắt buộc.");
                if (string.IsNullOrWhiteSpace(p.CursorParam)) errors.Add("search.pagination.cursorParam là bắt buộc.");
            }

            ValidateFilterMap(s.SoldFilterFields, "search.soldFilterFields", errors);
            ValidateFilterMap(s.PurchaseFilterFields, "search.purchaseFilterFields", errors);

            var r = s.Response;
            if (r == null) { errors.Add("Thiếu search.response."); return; }
            if (string.IsNullOrWhiteSpace(r.ItemsPath)) errors.Add("search.response.itemsPath là bắt buộc.");
            if (string.IsNullOrWhiteSpace(r.NextCursorPath)) errors.Add("search.response.nextCursorPath là bắt buộc.");
            if (r.TimeZoneOffsetHours < -12 || r.TimeZoneOffsetHours > 14) errors.Add("search.response.timeZoneOffsetHours phải trong khoảng -12..14.");
            if (r.DateFormats == null || r.DateFormats.Count == 0) errors.Add("search.response.dateFormats cần ít nhất một định dạng.");
            if (r.Fields == null) { errors.Add("Thiếu search.response.fields."); return; }

            foreach (var key in r.Fields.Keys.Where(k => !DtoFields.Contains(k)))
            {
                errors.Add("search.response.fields: trường không biết: " + key);
            }

            foreach (var required in RequiredDtoFields.Where(f => !r.Fields.ContainsKey(f)))
            {
                errors.Add("search.response.fields thiếu trường bắt buộc: " + required);
            }
        }

        private static void ValidateFilterMap(Dictionary<string, string> map, string name, List<string> errors)
        {
            if (map == null) return;
            foreach (var key in map.Keys.Where(k => !FilterFields.Contains(k)))
            {
                errors.Add(name + ": trường lọc không biết: " + key);
            }
        }

        private static void ValidateDownload(DownloadConfig d, List<string> errors)
        {
            if (d == null) { errors.Add("Thiếu download."); return; }
            ValidateKeyed(d.Endpoint, d.Parameters, "download", errors);
            if (!IsOneOf(d.ResponseMode, "Zip", "RawXml")) errors.Add("download.responseMode phải là Zip hoặc RawXml.");
            if (d.ResponseMode == "Zip" && string.IsNullOrWhiteSpace(d.ZipEntryName)) errors.Add("download.zipEntryName là bắt buộc khi responseMode = Zip.");
        }

        /// <summary>Endpoint nhận khóa tổng hợp của hóa đơn qua query string (xem chi tiết, xuất XML).</summary>
        private static void ValidateKeyed(EndpointConfig endpoint, Dictionary<string, string> parameters, string name, List<string> errors)
        {
            if (endpoint == null && parameters == null) { errors.Add("Thiếu " + name + "."); return; }
            ValidateEndpoint(endpoint, name + ".endpoint", errors);
            if (parameters == null || parameters.Count == 0) { errors.Add(name + ".parameters cần ít nhất một tham số."); return; }

            foreach (var pair in parameters.Where(p => !RefFields.Contains(p.Value)))
            {
                errors.Add(name + ".parameters[" + pair.Key + "]: trường không biết: " + pair.Value);
            }
        }

        private static void ValidateEndpoint(EndpointConfig e, string name, List<string> errors)
        {
            if (e == null) { errors.Add("Thiếu " + name + "."); return; }
            if (e.Method != "GET") errors.Add(name + ".method phải là GET.");
            if (string.IsNullOrWhiteSpace(e.Path) || !e.Path.StartsWith("/", StringComparison.Ordinal)) errors.Add(name + ".path phải bắt đầu bằng '/'.");
        }

        private static bool IsOneOf(string value, params string[] allowed)
        {
            return allowed.Contains(value);
        }
    }
}
