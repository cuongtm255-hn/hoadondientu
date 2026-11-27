using System;
using System.Collections.Generic;
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

        private readonly int _pageDelayMs;

        /// <param name="pageDelayMs">Nghỉ giữa các trang khi xuất tất cả các trang (mặc định 300 ms) để không dồn dập yêu cầu lên cổng.</param>
        public HoSoSearchSession(HoSoService service, int pageDelayMs = 300)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
            _pageDelayMs = Math.Max(0, pageDelayMs);
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

        /// <summary>Bộ lọc đã áp dụng lúc bấm Tìm (bản sao, không có CAPTCHA và số trang); null nếu chưa tìm.</summary>
        public HoSoQuery AppliedFilters
        {
            get
            {
                if (_applied == null) return null;
                HoSoQuery copy = _applied.WithoutCaptcha();
                copy.PageNumber = 0;
                return copy;
            }
        }

        /// <summary>
        /// Tải TẤT CẢ các trang của bộ lọc đã áp dụng để xuất Excel, tuần tự và có nghỉ giữa các trang (BR-DVC-05), không đổi <see cref="Page"/> đang hiển thị.
        /// Kết quả chỉ có một trang thì trả luôn các dòng đang có, không gọi cổng. Nhiều trang thì dùng mã của lần tìm (đổi trang không cần mã mới, quyết định
        /// 21/11/2026), hoặc <paramref name="captchaText"/> nếu người dùng vừa gõ mã mới. Trả null nếu bận, chưa tìm, hoặc BẤT KỲ trang nào lỗi (không trả dữ liệu
        /// dở dang; xem <see cref="LastFailure"/>; cổng từ chối mã thì quên mã và người dùng gõ mã mới rồi xuất lại).
        /// </summary>
        public async Task<IList<HoSoRow>> FetchAllPagesAsync(string captchaText, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (_busy || _applied == null || Page == null) return null;
            if (Page.PageCount <= 1)
            {
                LastFailure = HoSoFailure.None;
                return new List<HoSoRow>(Page.Rows);
            }

            string captcha = string.IsNullOrWhiteSpace(captchaText) ? _pagingCaptcha : captchaText;
            if (string.IsNullOrWhiteSpace(captcha) && HoSoSources.RequiresCaptcha(Source))
            {
                LastFailure = HoSoFailure.InvalidInput;
                return null;
            }

            _busy = true;
            try
            {
                int pageCount = Page.PageCount;
                var all = new List<HoSoRow>();
                for (int number = 1; number <= pageCount; number++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (number > 1 && _pageDelayMs > 0) await Task.Delay(_pageDelayMs, cancellationToken).ConfigureAwait(false);
                    HoSoQuery query = _applied.Clone();
                    query.PageNumber = number;
                    query.CaptchaText = captcha;
                    HoSoPage page = await _service.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                    all.AddRange(page.Rows);
                    if (progress != null) progress.Report(number);
                }
                _pagingCaptcha = captcha;
                LastFailure = HoSoFailure.None;
                return all;
            }
            catch (HoSoServiceException ex)
            {
                LastFailure = ex.Failure;
                _pagingCaptcha = null; // lỗi (kể cả cổng từ chối mã): không giữ mã, người dùng gõ mã mới
                return null;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Có thể đổi trang mà không cần gõ mã mới (còn mã của lần tìm hiện tại).</summary>
        public bool CanPageWithoutNewCaptcha
        {
            get { return !HoSoSources.RequiresCaptcha(Source) || !string.IsNullOrEmpty(_pagingCaptcha); }
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
            // Hồ sơ lưu tạm không có CAPTCHA: không tải ảnh, luôn coi là sẵn sàng.
            if (!HoSoSources.RequiresCaptcha(Source))
            {
                CaptchaImage = null;
                return true;
            }
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
