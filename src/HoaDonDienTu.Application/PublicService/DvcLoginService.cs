using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>Cổng vào trình duyệt đăng nhập DVC (WebView ngầm ở Release, giả ở Debug). Mọi lệnh chạy trong trang cổng (ADR DVC-0001).</summary>
    public interface IDvcLoginBrowser
    {
        /// <summary>Ảnh CAPTCHA mới do cổng cấp.</summary>
        Task<byte[]> GetCaptchaAsync(CancellationToken cancellationToken);

        Task<DvcLoginResult> SubmitAsync(DvcCredentials credentials, string captcha, CancellationToken cancellationToken);

        Task LogoutAsync(CancellationToken cancellationToken);
    }

    /// <summary>Nhớ tên đăng nhập DVC theo MST. Không bao giờ có mật khẩu.</summary>
    public interface IDvcUserNameStore
    {
        string Get(string taxCode);

        void Set(string taxCode, string userName);
    }

    /// <summary>
    /// Điều phối đăng nhập DVC (DD-DVC-01): kiểm tra đầu vào, gọi trình duyệt, ánh xạ lỗi, cập nhật phiên.
    /// Không giữ mật khẩu/CAPTCHA, không ghi chúng vào nhật ký (BR-DVC-03/04).
    /// </summary>
    public sealed class DvcLoginService
    {
        private readonly IDvcLoginBrowser _browser;
        private readonly DvcSession _session;
        private readonly IApplicationLogger _logger;
        private readonly IDvcUserNameStore _userNames;

        public DvcLoginService(IDvcLoginBrowser browser, DvcSession session, IApplicationLogger logger, IDvcUserNameStore userNames)
        {
            if (browser == null) throw new ArgumentNullException("browser");
            if (session == null) throw new ArgumentNullException("session");
            if (logger == null) throw new ArgumentNullException("logger");
            _browser = browser;
            _session = session;
            _logger = logger;
            _userNames = userNames;
        }

        /// <summary>Tên đăng nhập điền sẵn: tên đã nhớ cho MST, chưa có thì chính MST (cho sửa).</summary>
        public string SuggestUserName(string taxCode)
        {
            string remembered = _userNames == null ? null : _userNames.Get(taxCode);
            return string.IsNullOrWhiteSpace(remembered) ? taxCode : remembered;
        }

        public Task<byte[]> RefreshCaptchaAsync(CancellationToken cancellationToken)
        {
            return _browser.GetCaptchaAsync(cancellationToken);
        }

        public async Task<DvcLoginResult> LoginAsync(string taxCode, string userName, string password, string captcha, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(captcha))
            {
                return DvcLoginResult.Fail(DvcLoginFailure.MissingInput);
            }

            DvcLoginResult result;
            try
            {
                result = await _browser.SubmitAsync(new DvcCredentials(userName.Trim(), password), captcha.Trim(), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Chỉ ghi loại lỗi: thông điệp ngoại lệ có thể chứa dữ liệu nhập.
                _logger.Warning("Đăng nhập DVC lỗi: " + ex.GetType().Name);
                return DvcLoginResult.Fail(DvcLoginFailure.Network);
            }

            if (result == null) result = DvcLoginResult.Fail(DvcLoginFailure.Unknown);

            if (result.Success)
            {
                _session.SignedIn(userName.Trim(), result.OrganizationName);
                if (_userNames != null && !string.IsNullOrWhiteSpace(taxCode)) _userNames.Set(taxCode, userName.Trim());
                _logger.Info("Đăng nhập DVC: thành công");
            }
            else
            {
                if (result.Failure == DvcLoginFailure.AnotherTab) _session.MarkTakenByOtherTab();
                _logger.Info("Đăng nhập DVC: thất bại (" + result.Failure + ")");
            }

            return result;
        }

        public async Task LogoutAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _browser.LogoutAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warning("Đăng xuất DVC lỗi: " + ex.GetType().Name);
            }
            finally
            {
                _session.Reset();
            }
        }
    }
}
