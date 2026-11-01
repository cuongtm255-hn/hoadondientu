using System.Collections.Generic;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    /// <summary>
    /// Mô tả cách gọi API cổng Thuế: endpoint, cách đặt tham số, phân trang, cách đọc phản hồi, ánh xạ lỗi.
    /// Giá trị mặc định là bản NHÁP tự định nghĩa (<see cref="Verified"/> = false), chưa đối chiếu với cổng thật (G-02).
    /// Khi có tài khoản thật chỉ cần sửa file cấu hình cho khớp với traffic thực tế; code client không phải đổi.
    /// </summary>
    public sealed class PortalApiConfig
    {
        public int SchemaVersion { get; set; }

        /// <summary>False cho tới khi toàn bộ cấu hình đã được đối chiếu với traffic thật.</summary>
        public bool Verified { get; set; }

        public string Notes { get; set; }

        /// <summary>Phải là https.</summary>
        public string BaseUrl { get; set; }

        public AuthConfig Auth { get; set; }
        public HttpSettings Http { get; set; }
        public ProfileConfig Profile { get; set; }
        public SearchConfig Search { get; set; }
        public DownloadConfig Download { get; set; }
        public ErrorConfig Errors { get; set; }
        public RateLimitConfig RateLimit { get; set; }
    }

    public sealed class AuthConfig
    {
        public string HeaderName { get; set; }

        /// <summary>Ví dụ "Bearer"; để trống nếu header chứa token trần.</summary>
        public string Scheme { get; set; }

        /// <summary>Gửi các cookie thu được từ WebView trong header Cookie.</summary>
        public bool SendCookies { get; set; }

        /// <summary>Tên các header thu được từ WebView cần chuyển tiếp nguyên văn (ví dụ header CSRF).</summary>
        public List<string> ForwardHeaders { get; set; }
    }

    public sealed class HttpSettings
    {
        public int TimeoutSeconds { get; set; }
        public string UserAgent { get; set; }
        public Dictionary<string, string> DefaultHeaders { get; set; }
    }

    public sealed class EndpointConfig
    {
        /// <summary>GET hoặc POST.</summary>
        public string Method { get; set; }

        /// <summary>Đường dẫn tương đối so với BaseUrl; có thể chứa placeholder dạng {ten}.</summary>
        public string Path { get; set; }
    }

    public sealed class ProfileConfig
    {
        public EndpointConfig Endpoint { get; set; }

        /// <summary>Đường dẫn JSON tới MST của tài khoản đang đăng nhập (dùng đối chiếu MST).</summary>
        public string TaxCodePath { get; set; }

        public string NamePath { get; set; }
    }

    public sealed class SearchConfig
    {
        public EndpointConfig Purchase { get; set; }
        public EndpointConfig Sold { get; set; }

        /// <summary>"Query" (chuỗi truy vấn) hoặc "Body" (JSON body, dùng với POST).</summary>
        public string ParameterLocation { get; set; }

        public string DateFormat { get; set; }
        public string FromParam { get; set; }
        public string ToParam { get; set; }

        /// <summary>Tham số hằng luôn được gửi (ví dụ sắp xếp).</summary>
        public Dictionary<string, string> FixedParameters { get; set; }

        public PaginationConfig Pagination { get; set; }

        /// <summary>Tên trường bộ lọc (<c>CounterpartyTaxCode</c>, <c>StatusCode</c>, ...) → tên tham số của cổng.</summary>
        public Dictionary<string, string> FilterParameters { get; set; }

        public SearchResponseConfig Response { get; set; }
    }

    public sealed class PaginationConfig
    {
        /// <summary>"Cursor" (token trang kế tiếp) hoặc "Page" (số trang).</summary>
        public string Mode { get; set; }

        public string PageSizeParam { get; set; }
        public int PageSize { get; set; }
        public string CursorParam { get; set; }

        /// <summary>Số trang đầu tiên khi Mode = Page (0 hoặc 1).</summary>
        public int StartPage { get; set; }
    }

    public sealed class SearchResponseConfig
    {
        public string ItemsPath { get; set; }

        /// <summary>Tùy chọn: tổng số bản ghi.</summary>
        public string TotalPath { get; set; }

        /// <summary>Tùy chọn (Mode = Cursor): con trỏ trang kế; rỗng/null nghĩa là hết.</summary>
        public string NextCursorPath { get; set; }

        /// <summary>Tùy chọn: cờ boolean còn trang.</summary>
        public string HasMorePath { get; set; }

        /// <summary>Các định dạng ngày cổng trả về (thử lần lượt).</summary>
        public List<string> DateFormats { get; set; }

        /// <summary>Tên thuộc tính của <c>PortalInvoiceDto</c> → đường dẫn JSON trong từng phần tử.</summary>
        public Dictionary<string, string> Fields { get; set; }
    }

    public sealed class DownloadConfig
    {
        public EndpointConfig Endpoint { get; set; }

        /// <summary>"Query", "Path" (thay placeholder trong Path) hoặc "Body".</summary>
        public string ParameterLocation { get; set; }

        /// <summary>
        /// Tên tham số → trường của <c>PortalInvoiceRef</c> (PortalInvoiceId, SellerTaxCode, FormSymbol, InvoiceSymbol,
        /// InvoiceNumber, IssueDate, Direction).
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; }

        /// <summary>"RawXml" (thân phản hồi là XML) hoặc "JsonBase64" (JSON chứa XML mã hóa base64).</summary>
        public string ResponseMode { get; set; }

        /// <summary>Đường dẫn JSON tới chuỗi base64 khi ResponseMode = JsonBase64.</summary>
        public string ContentPath { get; set; }

        /// <summary>Mẫu tên file gợi ý, dùng {InvoiceSymbol}, {InvoiceNumber}, {PortalInvoiceId}.</summary>
        public string FileNameTemplate { get; set; }
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
