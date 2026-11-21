using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Bộ điều khiển tra cứu hồ sơ đã nộp, không phụ thuộc giao diện (DD-DVC-03 mục 4). Vòng đời CAPTCHA: mỗi lần BẤM TÌM cần một mã mới do người dùng gõ;
    /// sau mỗi lần gửi mã trong ô bị bỏ và ảnh mới được tải; KHÔNG BAO GIỜ tự gửi lại. ĐỔI TRANG trong cùng một kết quả dùng lại đúng mã của lần tìm đó,
    /// giống hành vi của chính trang cổng (quyết định của chủ sản phẩm 21/11/2026): mã chỉ giữ trong bộ nhớ và bị xóa khi tìm mới, đổi tab, lỗi, hết phiên.
    /// Một yêu cầu tại một thời điểm; bộ lọc đã áp dụng được giữ để đổi trang.
    /// </summary>
    public sealed class HoSoSearchSession
    {
        private readonly HoSoService _service;
        private HoSoQuery _applied;
        private string _pagingCaptcha;
        private bool _busy;

        public HoSoSearchSession(HoSoService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
        }

        public HoSoSource Source { get; private set; }

        public HoSoPage Page { get; private set; }

        /// <summary>Ảnh CAPTCHA hiện hành, null nếu chưa có hoặc tải lỗi.</summary>
        public byte[] CaptchaImage { get; private set; }

        public HoSoFailure LastFailure { get; private set; }

        public bool IsBusy
        {
            get { return _busy; }
        }

        /// <summary>Đổi tab nguồn: bỏ kết quả và bộ lọc đã áp dụng của tab cũ, tải CAPTCHA cho tab mới.</summary>
        public async Task<bool> SelectSourceAsync(HoSoSource source, CancellationToken cancellationToken)
        {
            if (_busy) return false;
            Source = source;
            Page = null;
            _applied = null;
            _pagingCaptcha = null;
            return await RefreshCaptchaAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Tải CAPTCHA mới (nút Đổi mã). Trả false nếu bận hoặc lỗi.</summary>
        public async Task<bool> RefreshCaptchaAsync(CancellationToken cancellationToken)
        {
            if (_busy) return false;
            _busy = true;
            try
            {
                return await LoadCaptchaCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Bấm Tìm: áp dụng bộ lọc mới, về trang 1. <paramref name="filters"/>.CaptchaText là mã người dùng vừa gõ.</summary>
        public Task<bool> SearchAsync(HoSoQuery filters, CancellationToken cancellationToken)
        {
            if (filters == null) throw new ArgumentNullException("filters");
            HoSoQuery query = filters.Clone();
            query.Source = Source;
            query.PageNumber = 1;
            return RunAsync(query, true, cancellationToken);
        }

        /// <summary>
        /// Đổi trang theo bộ lọc đã áp dụng. Mặc định dùng lại mã của lần tìm đã ra kết quả này (như trang cổng); nếu người dùng đã gõ mã mới
        /// (<paramref name="captchaText"/>, ví dụ sau khi cổng từ chối mã cũ) thì dùng mã mới. Không có mã nào thì từ chối (không gọi cổng).
        /// </summary>
        public Task<bool> GoToPageAsync(int pageNumber, string captchaText, CancellationToken cancellationToken)
        {
            if (_applied == null || Page == null || pageNumber < 1 || pageNumber > Math.Max(1, Page.PageCount))
            {
                return Task.FromResult(false);
            }
            HoSoQuery query = _applied.Clone();
            query.PageNumber = pageNumber;
            query.CaptchaText = string.IsNullOrWhiteSpace(captchaText) ? _pagingCaptcha : captchaText;
            return RunAsync(query, false, cancellationToken);
        }

        /// <summary>Đổi trang bằng mã của lần tìm hiện tại, không cần gõ lại.</summary>
        public Task<bool> GoToPageAsync(int pageNumber, CancellationToken cancellationToken)
        {
            return GoToPageAsync(pageNumber, null, cancellationToken);
        }

        /// <summary>Có thể đổi trang mà không cần gõ mã mới (còn mã của lần tìm hiện tại).</summary>
        public bool CanPageWithoutNewCaptcha
        {
            get { return !string.IsNullOrEmpty(_pagingCaptcha); }
        }

        /// <summary>Quên mã dùng cho đổi trang (đóng màn hình, hết phiên...).</summary>
        public void ClearPagingCaptcha()
        {
            _pagingCaptcha = null;
        }

        private async Task<bool> RunAsync(HoSoQuery query, bool replaceApplied, CancellationToken cancellationToken)
        {
            if (_busy) return false;
            _busy = true;
            bool ok = false;
            string used = query.CaptchaText;
            if (replaceApplied) _pagingCaptcha = null; // tìm mới: bỏ mã cũ ngay, chỉ giữ lại nếu lần tìm này thành công
            try
            {
                Page = await _service.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                _applied = replaceApplied || _applied == null ? query.WithoutCaptcha() : _applied;
                if (!replaceApplied) _applied.PageNumber = query.PageNumber;
                _pagingCaptcha = used; // mã của kết quả này, dùng lại cho đổi trang (chỉ trong bộ nhớ)
                LastFailure = HoSoFailure.None;
                ok = true;
            }
            catch (HoSoServiceException ex)
            {
                LastFailure = ex.Failure;
                _pagingCaptcha = null; // lỗi (kể cả cổng từ chối mã cũ): không giữ mã, người dùng gõ mã mới
            }
            finally
            {
                // Mã đã gõ không còn được giữ ở bản sao yêu cầu; ảnh mới được tải cho lần tìm sau. Không gửi lại tự động.
                query.CaptchaText = null;
            }

            try
            {
                if (LastFailure != HoSoFailure.NotLoggedIn && LastFailure != HoSoFailure.SessionExpired)
                {
                    HoSoFailure keep = LastFailure;
                    await LoadCaptchaCoreAsync(cancellationToken).ConfigureAwait(false);
                    if (keep != HoSoFailure.None) LastFailure = keep;
                }
                else
                {
                    CaptchaImage = null;
                }
            }
            finally
            {
                _busy = false;
            }
            return ok;
        }

        private async Task<bool> LoadCaptchaCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                CaptchaImage = await _service.GetCaptchaAsync(Source, cancellationToken).ConfigureAwait(false);
                return CaptchaImage != null && CaptchaImage.Length > 0;
            }
            catch (HoSoServiceException ex)
            {
                CaptchaImage = null;
                LastFailure = ex.Failure;
                return false;
            }
        }
    }
}
