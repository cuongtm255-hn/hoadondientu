using System.Collections.Generic;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    /// <summary>
    /// Mô tả cách gọi API cổng Thuế <c>https://hoadondientu.gdt.gov.vn</c>. Giá trị mặc định lấy từ traffic thật đã ghi lại trong
    /// <c>docs/api-samples/sample-api-web.md</c> (đăng nhập, tra cứu hóa đơn bán ra, xem chi tiết, xuất XML, xuất Excel).
    /// Những phần chưa có mẫu thật (tra cứu mua vào, trang kế tiếp bằng <c>state</c>, kích thước trang tối đa) được đánh dấu trong
    /// <see cref="Notes"/>; sửa file cấu hình khi đã đối chiếu, không phải đổi code.
    /// </summary>
    public sealed class PortalApiConfig
    {
        public int SchemaVersion { get; set; }

        /// <summary>True khi toàn bộ cấu hình đã đối chiếu với traffic thật; False nếu còn phần suy luận (xem Notes).</summary>
        public bool Verified { get; set; }

        public string Notes { get; set; }

        /// <summary>Phải là https.</summary>
        public string BaseUrl { get; set; }

        public AuthConfig Auth { get; set; }
        public HttpSettings Http { get; set; }
        public SearchConfig Search { get; set; }
        public DetailConfig Detail { get; set; }
        public DownloadConfig Download { get; set; }
        public ExportExcelConfig ExportExcel { get; set; }
        public ErrorConfig Errors { get; set; }
        public RateLimitConfig RateLimit { get; set; }
    }

    public sealed class AuthConfig
    {
        /// <summary>Endpoint đăng nhập của cổng (POST). Ứng dụng không tự gọi: WebView gọi, ứng dụng chỉ ghi lại kết quả.</summary>
        public string AuthenticatePath { get; set; }

        /// <summary>Đường dẫn JSON tới token trong phản hồi đăng nhập (ví dụ <c>token</c>).</summary>
        public string TokenResponsePath { get; set; }

        public string HeaderName { get; set; }

        /// <summary>Ví dụ "Bearer"; để trống nếu header chứa token trần.</summary>
        public string Scheme { get; set; }

        /// <summary>Gửi các cookie thu được từ WebView (jwt, JSESSIONID, cookie F5/WAF) trong header Cookie.</summary>
        public bool SendCookies { get; set; }

        /// <summary>Tên các header thu được từ WebView cần chuyển tiếp nguyên văn.</summary>
        public List<string> ForwardHeaders { get; set; }
    }

    public sealed class HttpSettings
    {
        public int TimeoutSeconds { get; set; }

        /// <summary>User-Agent mặc định; khi WebView đã ghi lại User-Agent thật của nó thì dùng giá trị đó thay thế.</summary>
        public string UserAgent { get; set; }

        public Dictionary<string, string> DefaultHeaders { get; set; }

        /// <summary>Tên header mang mã yêu cầu duy nhất mỗi lần gọi (cổng web gửi <c>request-id</c> dạng GUID); để trống nếu không gửi.</summary>
        public string RequestIdHeader { get; set; }

        /// <summary>Tên header mô tả hành động (cổng web gửi <c>action</c>, giá trị mã hóa phần trăm).</summary>
        public string ActionHeader { get; set; }
    }

    public sealed class EndpointConfig
    {
        /// <summary>GET hoặc POST.</summary>
        public string Method { get; set; }

        /// <summary>Đường dẫn tương đối so với BaseUrl.</summary>
        public string Path { get; set; }

        /// <summary>
        /// Nội dung header <c>action</c> mà cổng web gửi kèm, chứa {direction} sẽ thay bằng "bán ra" hoặc "mua vào".
        /// Ví dụ "Tìm kiếm (hóa đơn {direction})".
        /// </summary>
        public string Action { get; set; }
    }

    public sealed class SearchConfig
    {
        /// <summary>Tra cứu hóa đơn mua vào. Đường dẫn suy luận theo mẫu bán ra, chưa có mẫu thật.</summary>
        public EndpointConfig Purchase { get; set; }

        public EndpointConfig Sold { get; set; }

        /// <summary>Tham số <c>search</c> dạng RSQL, ví dụ <c>tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;tthai==1</c>.</summary>
        public RsqlConfig Rsql { get; set; }

        /// <summary>Tham số hằng luôn được gửi (ví dụ <c>sort=tdlap:desc</c>).</summary>
        public Dictionary<string, string> FixedParameters { get; set; }

        public PaginationConfig Pagination { get; set; }

        /// <summary>Tên trường bộ lọc (<c>StatusCode</c>, ...) → tên trường RSQL, cho tra cứu bán ra. Thiếu nghĩa là cổng không hỗ trợ.</summary>
        public Dictionary<string, string> SoldFilterFields { get; set; }

        /// <summary>Như <see cref="SoldFilterFields"/> nhưng cho tra cứu mua vào (MST đối tác là người bán, không có CCCD).</summary>
        public Dictionary<string, string> PurchaseFilterFields { get; set; }

        public SearchResponseConfig Response { get; set; }
    }

    public sealed class RsqlConfig
    {
        /// <summary>Tên tham số chứa biểu thức, thường là <c>search</c>.</summary>
        public string Param { get; set; }

        /// <summary>Dấu nối các điều kiện, thường là <c>;</c> (AND).</summary>
        public string Separator { get; set; }

        /// <summary>Trường ngày lập hóa đơn, thường là <c>tdlap</c>.</summary>
        public string DateField { get; set; }

        /// <summary>Định dạng ngày giờ trong biểu thức, ví dụ <c>dd/MM/yyyy'T'HH:mm:ss</c>.</summary>
        public string DateTimeFormat { get; set; }

        /// <summary>Giá trị gửi cho trường ủy nhiệm khi người dùng bật (<c>unhiem==1</c>).</summary>
        public string ProxyTrueValue { get; set; }
    }

    public sealed class PaginationConfig
    {
        public string PageSizeParam { get; set; }
        public int PageSize { get; set; }

        /// <summary>Tên tham số mang con trỏ trang kế tiếp (cổng web dùng <c>state</c>).</summary>
        public string CursorParam { get; set; }
    }

    public sealed class SearchResponseConfig
    {
        public string ItemsPath { get; set; }

        /// <summary>Tùy chọn: tổng số bản ghi.</summary>
        public string TotalPath { get; set; }

        /// <summary>Con trỏ trang kế; rỗng/null nghĩa là hết dữ liệu.</summary>
        public string NextCursorPath { get; set; }

        /// <summary>
        /// Múi giờ dùng để đổi ngày giờ UTC của cổng (ví dụ <c>2026-10-30T17:00:00Z</c>) sang ngày lập thực tế theo giờ Việt Nam (+7 giờ).
        /// Chỉ áp dụng cho giá trị có hậu tố Z.
        /// </summary>
        public int TimeZoneOffsetHours { get; set; }

        /// <summary>Các định dạng ngày cổng trả về (thử lần lượt).</summary>
        public List<string> DateFormats { get; set; }

        /// <summary>Tên thuộc tính của <c>PortalInvoiceDto</c> → đường dẫn JSON trong từng phần tử.</summary>
        public Dictionary<string, string> Fields { get; set; }
    }

    public sealed class DetailConfig
    {
        public EndpointConfig Endpoint { get; set; }

        /// <summary>Tên tham số → trường của <c>PortalInvoiceRef</c> (dùng chung với xuất XML).</summary>
        public Dictionary<string, string> Parameters { get; set; }
    }

    public sealed class DownloadConfig
    {
        public EndpointConfig Endpoint { get; set; }

        /// <summary>
        /// Tên tham số → trường của <c>PortalInvoiceRef</c> (SellerTaxCode, FormSymbol, InvoiceSymbol, InvoiceNumber, ...).
        /// Cổng nhận khóa tổng hợp <c>nbmst, khhdon, shdon, khmshdon</c> chứ không nhận id.
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; }

        /// <summary>"Zip" (cổng trả file zip chứa XML và bản xem) hoặc "RawXml" (thân phản hồi là XML).</summary>
        public string ResponseMode { get; set; }

        /// <summary>Tên entry XML trong file zip khi ResponseMode = Zip (cổng đặt tên <c>invoice.xml</c>).</summary>
        public string ZipEntryName { get; set; }

        /// <summary>Mẫu tên file gợi ý, dùng {SellerTaxCode}, {InvoiceSymbol}, {InvoiceNumber}, {PortalInvoiceId}.</summary>
        public string FileNameTemplate { get; set; }
    }

    /// <summary>API xuất bảng kê Excel của chính cổng. Ứng dụng tự xuất theo mẫu từ dữ liệu đã lưu nên không gọi; giữ để đối chiếu.</summary>
    public sealed class ExportExcelConfig
    {
        public EndpointConfig Endpoint { get; set; }
    }

    public sealed class ErrorConfig
    {
        /// <summary>Ghi đè ánh xạ mã HTTP → <c>PortalErrorKind</c> (tên enum). Mặc định xem <c>PortalErrorMapper</c>.</summary>
        public Dictionary<int, string> StatusKinds { get; set; }

        public string RetryAfterHeader { get; set; }
    }

    public sealed class RateLimitConfig
    {
        public int MinIntervalMs { get; set; }
    }
}
