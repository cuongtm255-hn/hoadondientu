using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>Nguồn dữ liệu danh mục TTHC: cổng thật (Release) hoặc giả (Debug). Chỉ đọc.</summary>
    public interface ITthcSource
    {
        Task<TthcFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken);

        Task<TthcPage> SearchAsync(TthcQuery query, CancellationToken cancellationToken);

        Task<TthcDetail> GetDetailAsync(string code, CancellationToken cancellationToken);
    }

    /// <summary>Lỗi tra cứu TTHC đã chuẩn hóa để hiển thị.</summary>
    public enum TthcFailure
    {
        None = 0,
        InvalidInput,
        Network,
        PortalChanged,
        NotFound,
        SessionExpired
    }

    /// <summary>Báo hết phiên (trang trả về trang đăng nhập); nguồn dữ liệu ném ra để service đánh dấu phiên.</summary>
    public sealed class DvcSessionExpiredException : Exception
    {
        public DvcSessionExpiredException() : base("Phiên Dịch vụ công đã hết hạn.")
        {
        }
    }

    public sealed class TthcServiceException : Exception
    {
        public TthcServiceException(TthcFailure failure) : base(failure.ToString())
        {
            Failure = failure;
        }

        public TthcFailure Failure { get; private set; }
    }

    /// <summary>
    /// Kiểm tra đầu vào trước khi gọi cổng (mã TTHC theo danh sách trắng, số trang trong khoảng), chuẩn hóa lỗi, ghi nhật ký
    /// không chứa từ khóa hay nội dung (DD-DVC-02 mục 4, 6).
    /// </summary>
    public sealed class TthcService
    {
        // Mã TTHC của cổng: chữ số và dấu chấm, ví dụ 1.010339. Không cho chuỗi tùy ý đi vào địa chỉ gọi.
        private static readonly Regex CodePattern = new Regex(@"^\d+(\.\d+)*$", RegexOptions.Compiled);
        private const int MaxPage = 100000;

        private readonly ITthcSource _source;
        private readonly DvcSession _session;
        private readonly IApplicationLogger _logger;

        public TthcService(ITthcSource source, DvcSession session, IApplicationLogger logger)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (logger == null) throw new ArgumentNullException("logger");
            _source = source;
            _session = session;
            _logger = logger;
        }

        public static bool IsValidCode(string code)
        {
            return !string.IsNullOrEmpty(code) && code.Length <= 30 && CodePattern.IsMatch(code);
        }

        public Task<TthcFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
        {
            return Guard("Tải ô lọc TTHC", () => _source.GetFilterOptionsAsync(cancellationToken));
        }

        public Task<TthcPage> SearchAsync(TthcQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException("query");
            if (query.PageNumber < 1 || query.PageNumber > MaxPage) throw new TthcServiceException(TthcFailure.InvalidInput);
            return Guard("Tìm TTHC trang " + query.PageNumber, () => _source.SearchAsync(query.Clone(), cancellationToken));
        }

        public Task<TthcDetail> GetDetailAsync(string code, CancellationToken cancellationToken)
        {
            if (!IsValidCode(code)) throw new TthcServiceException(TthcFailure.InvalidInput);
            return Guard("Xem chi tiết TTHC", () => _source.GetDetailAsync(code, cancellationToken));
        }

        private async Task<T> Guard<T>(string operation, Func<Task<T>> call)
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TthcServiceException)
            {
                throw;
            }
            catch (DvcSessionExpiredException)
            {
                if (_session != null) _session.MarkExpired();
                _logger.Info(operation + ": hết phiên");
                throw new TthcServiceException(TthcFailure.SessionExpired);
            }
            catch (TthcParseException)
            {
                _logger.Warning(operation + ": cổng đổi giao diện");
                throw new TthcServiceException(TthcFailure.PortalChanged);
            }
            catch (Exception ex)
            {
                // Chỉ ghi loại lỗi: thông điệp có thể chứa từ khóa hoặc nội dung.
                _logger.Warning(operation + " lỗi: " + ex.GetType().Name);
                throw new TthcServiceException(TthcFailure.Network);
            }
        }

        public static string MessageFor(TthcFailure failure)
        {
            switch (failure)
            {
                case TthcFailure.InvalidInput: return "Điều kiện tìm không hợp lệ.";
                case TthcFailure.Network: return "Không kết nối được cổng Dịch vụ công. Kiểm tra mạng rồi thử lại.";
                case TthcFailure.PortalChanged: return "Cổng đã đổi giao diện, chức năng này tạm chưa dùng được.";
                case TthcFailure.NotFound: return "Không tìm thấy thủ tục.";
                case TthcFailure.SessionExpired: return "Phiên đã hết hạn. Hãy đăng nhập lại.";
                default: return string.Empty;
            }
        }
    }
}
