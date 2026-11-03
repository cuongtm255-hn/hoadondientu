using System.Collections.Generic;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    /// <summary>
    /// Cấu hình mặc định theo traffic thật của https://hoadondientu.gdt.gov.vn ghi lại trong <c>docs/api-samples/sample-api-web.md</c>.
    /// </summary>
    public static class PortalApiConfigDefaults
    {
        public static PortalApiConfig Create()
        {
            return new PortalApiConfig
            {
                SchemaVersion = 2,
                Verified = false,
                Notes = "Đối chiếu thật: đăng nhập, tra cứu bán ra (tdlap/tthai/ttxly/khmshdon/shdon/khhdon/unhiem/nmmst/nmcmnd) và mua vào (nbmst/nmcmnd, ttxly chỉ 5/6/8 và bắt buộc), xem chi tiết, "
                    + "xuất XML (zip), xuất Excel. CHƯA có mẫu thật: tra cứu mua vào khi bỏ ttxly, "
                    + "trang kế tiếp qua tham số state khi tổng > size, kích thước trang tối đa.",
                BaseUrl = "https://hoadondientu.gdt.gov.vn",
                Auth = new AuthConfig
                {
                    AuthenticatePath = "/api/security-taxpayer/authenticate",
                    TokenResponsePath = "token",
                    HeaderName = "Authorization",
                    Scheme = "Bearer",
                    SendCookies = true,
                    ForwardHeaders = new List<string>()
                },
                Http = new HttpSettings
                {
                    TimeoutSeconds = 60,
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36",
                    RequestIdHeader = "request-id",
                    ActionHeader = "action",
                    DefaultHeaders = new Dictionary<string, string>
                    {
                        { "Accept", "application/json, text/plain, */*" },
                        { "Accept-Language", "vi" },
                        { "end-point", "/tra-cuu/tra-cuu-hoa-don" },
                        { "Referer", "https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don" }
                    }
                },
                Search = new SearchConfig
                {
                    Sold = new EndpointConfig { Method = "GET", Path = "/api/query/invoices/sold", Action = "Tìm kiếm (hóa đơn {direction})" },
                    Purchase = new EndpointConfig { Method = "GET", Path = "/api/query/invoices/purchase", Action = "Tìm kiếm (hóa đơn {direction})" },
                    Rsql = new RsqlConfig
                    {
                        Param = "search",
                        Separator = ";",
                        DateField = "tdlap",
                        DateTimeFormat = "dd/MM/yyyy'T'HH:mm:ss",
                        ProxyTrueValue = "1"
                    },
                    FixedParameters = new Dictionary<string, string> { { "sort", "tdlap:desc" } },
                    Pagination = new PaginationConfig { PageSizeParam = "size", PageSize = 50, CursorParam = "state" },
                    SoldFilterFields = new Dictionary<string, string>
                    {
                        { "CounterpartyTaxCode", "nmmst" },
                        { "BuyerIdNumber", "nmcmnd" },
                        { "StatusCode", "tthai" },
                        { "CheckResultCode", "ttxly" },
                        { "InvoiceNumber", "shdon" },
                        { "FormSymbol", "khmshdon" },
                        { "InvoiceSymbol", "khhdon" },
                        { "ProxyInvoice", "unhiem" }
                    },
                    PurchaseFilterFields = new Dictionary<string, string>
                    {
                        { "CounterpartyTaxCode", "nbmst" },
                        { "BuyerIdNumber", "nmcmnd" },
                        { "StatusCode", "tthai" },
                        { "CheckResultCode", "ttxly" },
                        { "InvoiceNumber", "shdon" },
                        { "FormSymbol", "khmshdon" },
                        { "InvoiceSymbol", "khhdon" },
                        { "ProxyInvoice", "unhiem" }
                    },
                    Response = new SearchResponseConfig
                    {
                        ItemsPath = "datas",
                        TotalPath = "total",
                        NextCursorPath = "state",
                        TimeZoneOffsetHours = 7,
                        DateFormats = new List<string> { "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd", "dd/MM/yyyy" },
                        Fields = new Dictionary<string, string>
                        {
                            { "PortalInvoiceId", "id" },
                            { "FormSymbol", "khmshdon" },
                            { "InvoiceSymbol", "khhdon" },
                            { "InvoiceNumber", "shdon" },
                            { "IssueDate", "tdlap" },
                            { "SellerTaxCode", "nbmst" },
                            { "SellerName", "nbten" },
                            { "BuyerTaxCode", "nmmst" },
                            { "BuyerIdNumber", "nmcmnd" },
                            { "BuyerName", "nmten" },
                            { "BuyerAddress", "nmdchi" },
                            { "TotalBeforeTax", "tgtcthue" },
                            { "TotalTax", "tgtthue" },
                            { "TotalDiscount", "ttcktmai" },
                            { "TotalFee", "tgtphi" },
                            { "TotalPayment", "tgtttbso" },
                            { "Currency", "dvtte" },
                            { "ExchangeRate", "tgia" },
                            { "StatusRaw", "tthai" },
                            { "CheckResultRaw", "ttxly" }
                        }
                    }
                },
                Detail = new DetailConfig
                {
                    Endpoint = new EndpointConfig { Method = "GET", Path = "/api/query/invoices/detail", Action = "Xem hóa đơn (hóa đơn {direction})" },
                    Parameters = KeyParameters()
                },
                Download = new DownloadConfig
                {
                    Endpoint = new EndpointConfig { Method = "GET", Path = "/api/query/invoices/export-xml", Action = "Xuất xml (hóa đơn {direction})" },
                    Parameters = KeyParameters(),
                    ResponseMode = "Zip",
                    ZipEntryName = "invoice.xml",
                    FileNameTemplate = "{SellerTaxCode}_{InvoiceSymbol}_{InvoiceNumber}.xml"
                },
                ExportExcel = new ExportExcelConfig
                {
                    Endpoint = new EndpointConfig { Method = "GET", Path = "/api/query/invoices/export-excel", Action = "Xuất hóa đơn (hóa đơn {direction})" }
                },
                Errors = new ErrorConfig
                {
                    StatusKinds = new Dictionary<int, string>(),
                    RetryAfterHeader = "Retry-After"
                },
                RateLimit = new RateLimitConfig { MinIntervalMs = 3000 }
            };
        }

        /// <summary>Khóa tổng hợp của một hóa đơn, dùng cho xem chi tiết và xuất XML: nbmst, khhdon, shdon, khmshdon.</summary>
        private static Dictionary<string, string> KeyParameters()
        {
            return new Dictionary<string, string>
            {
                { "nbmst", "SellerTaxCode" },
                { "khhdon", "InvoiceSymbol" },
                { "shdon", "InvoiceNumber" },
                { "khmshdon", "FormSymbol" }
            };
        }
    }
}
