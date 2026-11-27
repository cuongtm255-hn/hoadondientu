using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>Nguồn dữ liệu hồ sơ đã nộp: cổng thật (Release) hoặc giả (Debug). Chỉ đọc; mọi lần tìm cần một CAPTCHA mới.</summary>
    public interface IHoSoSource
    {
        /// <summary>Ảnh CAPTCHA mới cho một lần tìm. Mã cũ coi như hết hiệu lực.</summary>
        Task<byte[]> GetCaptchaAsync(HoSoSource source, CancellationToken cancellationToken);

        /// <summary>Gửi một yêu cầu tìm. Sai CAPTCHA thì ném <see cref="HoSoServiceException"/> với <see cref="HoSoFailure.WrongCaptcha"/>.</summary>
        Task<HoSoPage> SearchAsync(HoSoQuery query, CancellationToken cancellationToken);
    }

    public enum HoSoFailure
    {
        None = 0,
        InvalidInput,
        NotLoggedIn,
        SessionExpired,
        WrongCaptcha,
        Network,
        PortalChanged,

        /// <summary>Nguồn (tab) này chưa dùng được ở bản đang chạy (ví dụ tab thuế điện tử ở bản Release chưa khảo sát).</summary>
        NotSupported
    }

    public sealed class HoSoServiceException : Exception
    {
        public HoSoServiceException(HoSoFailure failure) : base(failure.ToString())
        {
            Failure = failure;
        }

        public HoSoFailure Failure { get; private set; }
    }

    /// <summary>
    /// Kiểm tra điều kiện trước khi gọi cổng (phải đăng nhập, CAPTCHA không rỗng, khoảng ngày hợp lệ, số trang), chuẩn hóa lỗi, ghi nhật ký
    /// KHÔNG chứa CAPTCHA, MST ủy quyền, mã hồ sơ hay nội dung dòng (DD-DVC-03 mục 6).
    /// </summary>
    public sealed class HoSoService
    {
        private const int MaxPage = 100000;

        private readonly IHoSoSource _source;
        private readonly DvcSession _session;
        private readonly IApplicationLogger _logger;

        public HoSoService(IHoSoSource source, DvcSession session, IApplicationLogger logger)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (session == null) throw new ArgumentNullException("session");
            if (logger == null) throw new ArgumentNullException("logger");
            _source = source;
            _session = session;
            _logger = logger;
        }

        public Task<byte[]> GetCaptchaAsync(HoSoSource source, CancellationToken cancellationToken)
        {
            RequireLogin();
            // Hồ sơ lưu tạm không có CAPTCHA.
            if (source == HoSoSource.LuuTam) return Task.FromResult(new byte[0]);
            return Guard("Lấy CAPTCHA hồ sơ", () => _source.GetCaptchaAsync(source, cancellationToken));
        }

        public async Task<HoSoPage> SearchAsync(HoSoQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException("query");
            RequireLogin();
            Validate(query);
            HoSoPage page = await Guard("Tìm hồ sơ " + query.Source, () => _source.SearchAsync(query.Clone(), cancellationToken)).ConfigureAwait(false);
            _logger.Info("Tìm hồ sơ " + query.Source + ": " + page.TotalRecords + " bản ghi");
            return page;
        }

        private void RequireLogin()
        {
            if (!_session.IsLoggedIn) throw new HoSoServiceException(HoSoFailure.NotLoggedIn);
        }

        private static void Validate(HoSoQuery query)
        {
            bool draft = query.Source == HoSoSource.LuuTam;
            if (!draft && string.IsNullOrWhiteSpace(query.CaptchaText)) throw new HoSoServiceException(HoSoFailure.InvalidInput);
            if (!query.IgnoreDates && query.FromDate.Date > query.ToDate.Date) throw new HoSoServiceException(HoSoFailure.InvalidInput);
            if (query.PageNumber < 1 || query.PageNumber > MaxPage) throw new HoSoServiceException(HoSoFailure.InvalidInput);
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
            catch (HoSoServiceException ex)
            {
                if (ex.Failure != HoSoFailure.None) _logger.Info(operation + ": " + ex.Failure);
                throw;
            }
            catch (NotSupportedException)
            {
                throw new HoSoServiceException(HoSoFailure.NotSupported);
            }
            catch (DvcSessionExpiredException)
            {
                _session.MarkExpired();
                _logger.Info(operation + ": hết phiên");
                throw new HoSoServiceException(HoSoFailure.SessionExpired);
            }
            catch (HoSoParseException ex)
            {
                // Chỉ ghi Diagnostic (do bộ phân tích tạo: lý do, số lượng, hình dạng cấu trúc), không bao giờ ghi Message của ngoại lệ.
                _logger.Warning(operation + ": cổng đổi giao diện" + (string.IsNullOrEmpty(ex.Diagnostic) ? string.Empty : ": " + ex.Diagnostic));
                throw new HoSoServiceException(HoSoFailure.PortalChanged);
            }
            catch (Exception ex)
            {
                // Chỉ ghi loại lỗi: thông điệp ngoại lệ có thể chứa dữ liệu nhập hoặc nội dung hồ sơ.
                _logger.Warning(operation + " lỗi: " + ex.GetType().Name);
                throw new HoSoServiceException(HoSoFailure.Network);
            }
        }

        public static string MessageFor(HoSoFailure failure)
        {
            switch (failure)
            {
                case HoSoFailure.InvalidInput: return "Hãy nhập mã xác nhận và chọn khoảng ngày hợp lệ (từ ngày không sau đến ngày).";
                case HoSoFailure.NotLoggedIn: return "Cần đăng nhập cổng Dịch vụ công trước.";
                case HoSoFailure.SessionExpired: return "Phiên đã hết hạn. Hãy đăng nhập lại.";
                case HoSoFailure.WrongCaptcha: return "Mã xác nhận không đúng. Hãy nhập mã mới.";
                case HoSoFailure.Network: return "Không kết nối được cổng Dịch vụ công. Kiểm tra mạng rồi thử lại.";
                case HoSoFailure.PortalChanged: return "Cổng đã đổi giao diện, chức năng này tạm chưa dùng được.";
                case HoSoFailure.NotSupported: return "Nguồn này chưa dùng được ở bản này. Hãy chọn \"Hồ sơ đã nộp trên Dịch vụ công\".";
                default: return string.Empty;
            }
        }
    }
}
