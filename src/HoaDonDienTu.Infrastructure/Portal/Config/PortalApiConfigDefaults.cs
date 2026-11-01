using System.Collections.Generic;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    /// <summary>
    /// Cấu hình NHÁP tự định nghĩa để DD-04…DD-09 phát triển được khi chưa có tài khoản thật. Mọi endpoint, tên tham số và
    /// cấu trúc phản hồi dưới đây là giả định, chưa được đối chiếu với cổng Thuế (Verified = false).
    /// </summary>
    public static class PortalApiConfigDefaults
    {
        public static PortalApiConfig Create()
        {
            return new PortalApiConfig
            {
                SchemaVersion = 1,
                Verified = false,
                Notes = "NHÁP tự định nghĩa, chưa đối chiếu cổng Thuế thật (G-02). Sửa file này cho khớp traffic thực tế; không cần đổi code.",
                BaseUrl = "https://hoadondientu.gdt.gov.vn",
                Auth = new AuthConfig
                {
                    HeaderName = "Authorization",
                    Scheme = "Bearer",
                    SendCookies = false,
                    ForwardHeaders = new List<string>()
                },
                Http = new HttpSettings
                {
                    TimeoutSeconds = 60,
                    UserAgent = "HoaDonDienTu/1.0",
                    DefaultHeaders = new Dictionary<string, string> { { "Accept", "application/json" } }
                },
                Profile = new ProfileConfig
                {
                    Endpoint = new EndpointConfig { Method = "GET", Path = "/api/v1/profile" },
                    TaxCodePath = "data.taxCode",
                    NamePath = "data.name"
                },
                Search = new SearchConfig
                {
                    Purchase = new EndpointConfig { Method = "GET", Path = "/api/v1/invoices/purchase" },
                    Sold = new EndpointConfig { Method = "GET", Path = "/api/v1/invoices/sold" },
                    ParameterLocation = "Query",
                    DateFormat = "yyyy-MM-dd",
                    FromParam = "from",
                    ToParam = "to",
                    FixedParameters = new Dictionary<string, string> { { "sort", "issueDate:asc" } },
                    Pagination = new PaginationConfig
                    {
                        Mode = "Cursor",
                        PageSizeParam = "size",
                        PageSize = 50,
                        CursorParam = "cursor",
                        StartPage = 1
                    },
                    FilterParameters = new Dictionary<string, string>
                    {
                        { "CounterpartyTaxCode", "partnerTaxCode" },
                        { "StatusCode", "status" },
                        { "CheckResultCode", "checkResult" },
                        { "InvoiceNumber", "invoiceNumber" },
                        { "FormSymbol", "formSymbol" },
                        { "InvoiceSymbol", "invoiceSymbol" },
                        { "ProxyInvoice", "proxy" }
                    },
                    Response = new SearchResponseConfig
                    {
                        ItemsPath = "data.items",
                        TotalPath = "data.total",
                        NextCursorPath = "data.nextCursor",
                        HasMorePath = null,
                        DateFormats = new List<string> { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy" },
                        Fields = new Dictionary<string, string>
                        {
                            { "PortalInvoiceId", "id" },
                            { "FormSymbol", "formSymbol" },
                            { "InvoiceSymbol", "invoiceSymbol" },
                            { "InvoiceNumber", "number" },
                            { "IssueDate", "issueDate" },
                            { "SellerTaxCode", "seller.taxCode" },
                            { "SellerName", "seller.name" },
                            { "BuyerTaxCode", "buyer.taxCode" },
                            { "BuyerName", "buyer.name" },
                            { "BuyerAddress", "buyer.address" },
                            { "TotalBeforeTax", "amounts.beforeTax" },
                            { "TotalTax", "amounts.tax" },
                            { "TotalDiscount", "amounts.discount" },
                            { "TotalFee", "amounts.fee" },
                            { "TotalPayment", "amounts.payment" },
                            { "Currency", "currency" },
                            { "ExchangeRate", "exchangeRate" },
                            { "StatusRaw", "status" },
                            { "CheckResultRaw", "checkResult" }
                        }
                    }
                },
                Download = new DownloadConfig
                {
                    Endpoint = new EndpointConfig { Method = "GET", Path = "/api/v1/invoices/{id}/xml" },
                    ParameterLocation = "Path",
                    Parameters = new Dictionary<string, string> { { "id", "PortalInvoiceId" } },
                    ResponseMode = "RawXml",
                    ContentPath = null,
                    FileNameTemplate = "{InvoiceSymbol}-{InvoiceNumber}.xml"
                },
                Errors = new ErrorConfig
                {
                    StatusKinds = new Dictionary<int, string>(),
                    RetryAfterHeader = "Retry-After"
                },
                RateLimit = new RateLimitConfig { MinIntervalMs = 500 }
            };
        }
    }
}
