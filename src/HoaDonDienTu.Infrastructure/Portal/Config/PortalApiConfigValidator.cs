using System;
using System.Collections.Generic;
using System.Linq;
using HoaDonDienTu.Application.Portal;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    public static class PortalApiConfigValidator
    {
        /// <summary>Tên các trường của <c>PortalInvoiceDto</c> cho phép ánh xạ.</summary>
        public static readonly string[] DtoFields =
        {
            "PortalInvoiceId", "FormSymbol", "InvoiceSymbol", "InvoiceNumber", "IssueDate", "SellerTaxCode", "SellerName",
            "BuyerTaxCode", "BuyerName", "BuyerAddress", "TotalBeforeTax", "TotalTax", "TotalDiscount", "TotalFee",
            "TotalPayment", "Currency", "ExchangeRate", "StatusRaw", "CheckResultRaw"
        };

        /// <summary>Các trường bắt buộc: thiếu trong phản hồi nghĩa là cổng đã đổi cấu trúc.</summary>
        public static readonly string[] RequiredDtoFields = { "PortalInvoiceId", "InvoiceNumber", "IssueDate" };

        public static readonly string[] FilterFields =
        {
            "CounterpartyTaxCode", "StatusCode", "CheckResultCode", "InvoiceNumber", "FormSymbol", "InvoiceSymbol", "ProxyInvoice"
        };

        public static readonly string[] RefFields =
        {
            "PortalInvoiceId", "SellerTaxCode", "FormSymbol", "InvoiceSymbol", "InvoiceNumber", "IssueDate", "Direction"
        };

        public static IList<string> Validate(PortalApiConfig c)
        {
            var errors = new List<string>();
            if (c == null)
            {
                errors.Add("Thiếu cấu hình.");
                return errors;
            }

            if (c.SchemaVersion != 1) errors.Add("schemaVersion phải là 1.");

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
            if (c.Http == null || c.Http.TimeoutSeconds <= 0) errors.Add("http.timeoutSeconds phải > 0.");
            if (c.RateLimit == null || c.RateLimit.MinIntervalMs < 0) errors.Add("rateLimit.minIntervalMs phải >= 0.");

            ValidateProfile(c.Profile, errors);
            ValidateSearch(c.Search, errors);
            ValidateDownload(c.Download, errors);

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

        private static void ValidateProfile(ProfileConfig p, List<string> errors)
        {
            if (p == null) { errors.Add("Thiếu profile."); return; }
            ValidateEndpoint(p.Endpoint, "profile.endpoint", errors);
            if (string.IsNullOrWhiteSpace(p.TaxCodePath)) errors.Add("profile.taxCodePath là bắt buộc.");
        }

        private static void ValidateSearch(SearchConfig s, List<string> errors)
        {
            if (s == null) { errors.Add("Thiếu search."); return; }
            ValidateEndpoint(s.Purchase, "search.purchase", errors);
            ValidateEndpoint(s.Sold, "search.sold", errors);

            if (!IsOneOf(s.ParameterLocation, "Query", "Body")) errors.Add("search.parameterLocation phải là Query hoặc Body.");
            if (string.Equals(s.ParameterLocation, "Body", StringComparison.Ordinal)
                && (s.Purchase == null || s.Purchase.Method != "POST" || s.Sold == null || s.Sold.Method != "POST"))
            {
                errors.Add("search.parameterLocation = Body yêu cầu method POST.");
            }

            if (string.IsNullOrWhiteSpace(s.DateFormat)) errors.Add("search.dateFormat là bắt buộc.");
            if (string.IsNullOrWhiteSpace(s.FromParam) || string.IsNullOrWhiteSpace(s.ToParam)) errors.Add("search.fromParam/toParam là bắt buộc.");

            var p = s.Pagination;
            if (p == null) errors.Add("Thiếu search.pagination.");
            else
            {
                if (!IsOneOf(p.Mode, "Cursor", "Page")) errors.Add("search.pagination.mode phải là Cursor hoặc Page.");
                if (p.PageSize <= 0) errors.Add("search.pagination.pageSize phải > 0.");
                if (string.IsNullOrWhiteSpace(p.PageSizeParam)) errors.Add("search.pagination.pageSizeParam là bắt buộc.");
                if (string.IsNullOrWhiteSpace(p.CursorParam)) errors.Add("search.pagination.cursorParam là bắt buộc.");
                if (p.StartPage < 0) errors.Add("search.pagination.startPage phải >= 0.");
            }

            if (s.FilterParameters != null)
            {
                foreach (var key in s.FilterParameters.Keys.Where(k => !FilterFields.Contains(k)))
                {
                    errors.Add("search.filterParameters: trường lọc không biết: " + key);
                }
            }

            var r = s.Response;
            if (r == null) { errors.Add("Thiếu search.response."); return; }
            if (string.IsNullOrWhiteSpace(r.ItemsPath)) errors.Add("search.response.itemsPath là bắt buộc.");
            if (p != null && p.Mode == "Cursor" && string.IsNullOrWhiteSpace(r.NextCursorPath)) errors.Add("search.response.nextCursorPath là bắt buộc khi pagination.mode = Cursor.");
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

        private static void ValidateDownload(DownloadConfig d, List<string> errors)
        {
            if (d == null) { errors.Add("Thiếu download."); return; }
            ValidateEndpoint(d.Endpoint, "download.endpoint", errors);
            if (!IsOneOf(d.ParameterLocation, "Query", "Path", "Body")) errors.Add("download.parameterLocation phải là Query, Path hoặc Body.");
            if (!IsOneOf(d.ResponseMode, "RawXml", "JsonBase64")) errors.Add("download.responseMode phải là RawXml hoặc JsonBase64.");
            if (d.ResponseMode == "JsonBase64" && string.IsNullOrWhiteSpace(d.ContentPath)) errors.Add("download.contentPath là bắt buộc khi responseMode = JsonBase64.");
            if (d.Parameters == null || d.Parameters.Count == 0) errors.Add("download.parameters cần ít nhất một tham số.");
            else
            {
                foreach (var pair in d.Parameters.Where(p => !RefFields.Contains(p.Value)))
                {
                    errors.Add("download.parameters[" + pair.Key + "]: trường không biết: " + pair.Value);
                }
            }

            if (d.ParameterLocation == "Path" && d.Endpoint != null && d.Parameters != null)
            {
                foreach (var name in d.Parameters.Keys.Where(k => d.Endpoint.Path == null || !d.Endpoint.Path.Contains("{" + k + "}")))
                {
                    errors.Add("download.endpoint.path thiếu placeholder {" + name + "}.");
                }
            }
        }

        private static void ValidateEndpoint(EndpointConfig e, string name, List<string> errors)
        {
            if (e == null) { errors.Add("Thiếu " + name + "."); return; }
            if (!IsOneOf(e.Method, "GET", "POST")) errors.Add(name + ".method phải là GET hoặc POST.");
            if (string.IsNullOrWhiteSpace(e.Path) || !e.Path.StartsWith("/", StringComparison.Ordinal)) errors.Add(name + ".path phải bắt đầu bằng '/'.");
        }

        private static bool IsOneOf(string value, params string[] allowed)
        {
            return allowed.Contains(value);
        }
    }
}
