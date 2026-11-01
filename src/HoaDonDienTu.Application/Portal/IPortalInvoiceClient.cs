using System;
using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Portal
{
    /// <summary>Phân loại lỗi từ cổng Thuế (DD-05 mục 4).</summary>
    public enum PortalErrorKind
    {
        /// <summary>401/403: token hết hạn hoặc không hợp lệ; dừng và yêu cầu đăng nhập lại.</summary>
        AuthExpired,

        /// <summary>Timeout, lỗi mạng, 5xx: có thể thử lại.</summary>
        Transient,

        /// <summary>429: chờ theo <see cref="PortalApiException.RetryAfter"/> rồi thử lại.</summary>
        RateLimited,

        /// <summary>Phản hồi không đúng cấu trúc đã cấu hình: cổng đổi API hoặc cấu hình sai.</summary>
        SchemaChanged,

        /// <summary>Cổng từ chối bộ lọc/tham số (thường 400).</summary>
        FilterRejected,

        Fatal
    }

    /// <summary>
    /// Lỗi khi gọi cổng Thuế. Thông báo chỉ gồm loại lỗi và mã HTTP, không bao giờ chứa token, header hay nội dung phản hồi.
    /// </summary>
    public sealed class PortalApiException : Exception
    {
        public PortalApiException(PortalErrorKind kind, string message, int? httpStatus, TimeSpan? retryAfter)
            : base(message)
        {
            Kind = kind;
            HttpStatus = httpStatus;
            RetryAfter = retryAfter;
        }

        public PortalErrorKind Kind { get; private set; }
        public int? HttpStatus { get; private set; }
        public TimeSpan? RetryAfter { get; private set; }
    }

    /// <summary>Adapter cổng Thuế mà tầng Application phụ thuộc; hình dạng thật được khớp qua cấu hình sau G-02.</summary>
    public interface IPortalInvoiceClient
    {
        /// <exception cref="PortalApiException"/>
        Task<InvoicePage> SearchAsync(InvoiceSearchRequest request, CancellationToken cancellationToken);

        /// <exception cref="PortalApiException"/>
        Task<InvoiceXmlPayload> DownloadXmlAsync(PortalInvoiceRef invoice, CancellationToken cancellationToken);
    }

    /// <summary>Giữ nhịp gọi tối thiểu giữa các request (FR-JOB-012).</summary>
    public interface IRateLimiter
    {
        Task WaitAsync(CancellationToken cancellationToken);
    }
}
