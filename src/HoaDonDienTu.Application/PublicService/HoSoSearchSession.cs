using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Bộ điều khiển tra cứu hồ sơ đã nộp, không phụ thuộc giao diện (DD-DVC-03 mục 4). Vòng đời CAPTCHA: mỗi lần gửi (kể cả thất bại) mã đã gõ
    /// bị bỏ và ảnh mới được tải cho lần sau; KHÔNG BAO GIỜ tự gửi lại. Một yêu cầu tại một thời điểm; bộ lọc đã áp dụng được giữ để đổi trang.
    /// </summary>
    public sealed class HoSoSearchSession
    {
        private readonly HoSoService _service;
        private HoSoQuery _applied;
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

        /// <summary>Đổi trang theo bộ lọc đã áp dụng, với một mã CAPTCHA mới do người dùng gõ.</summary>
        public Task<bool> GoToPageAsync(int pageNumber, string captchaText, CancellationToken cancellationToken)
        {
            if (_applied == null || Page == null || pageNumber < 1 || pageNumber > Math.Max(1, Page.PageCount))
            {
                return Task.FromResult(false);
            }
            HoSoQuery query = _applied.Clone();
            query.PageNumber = pageNumber;
            query.CaptchaText = captchaText;
            return RunAsync(query, false, cancellationToken);
        }

        private async Task<bool> RunAsync(HoSoQuery query, bool replaceApplied, CancellationToken cancellationToken)
        {
            if (_busy) return false;
            _busy = true;
            bool ok = false;
            try
            {
                Page = await _service.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                _applied = replaceApplied || _applied == null ? query.WithoutCaptcha() : _applied;
                if (!replaceApplied) _applied.PageNumber = query.PageNumber;
                LastFailure = HoSoFailure.None;
                ok = true;
            }
            catch (HoSoServiceException ex)
            {
                LastFailure = ex.Failure;
            }
            finally
            {
                // Mã đã gõ không còn dùng được và không được giữ lại: bỏ khỏi bộ nhớ, rồi tải ảnh mới. Không gửi lại tự động.
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
