using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Authentication;

namespace HoaDonDienTu.WinForms.Login
{
    public sealed class LoginCandidateEventArgs : EventArgs
    {
    }

    public sealed class BrowserNavigationEventArgs : EventArgs
    {
        public BrowserNavigationEventArgs(string url)
        {
            Url = url;
        }

        public string Url { get; private set; }
    }

    public sealed class BrowserFailureEventArgs : EventArgs
    {
        public BrowserFailureEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }

    /// <summary>CAPTCHA do cổng cấp (api-samples mục 0): <see cref="Key"/> gửi lại làm <c>ckey</c>, <see cref="Svg"/> là ảnh SVG để hiển thị.</summary>
    public sealed class LoginCaptcha
    {
        public LoginCaptcha(string key, string svg)
        {
            Key = key;
            Svg = svg;
        }

        public string Key { get; private set; }

        public string Svg { get; private set; }
    }

    /// <summary>Kết quả gửi tài khoản/mật khẩu/CAPTCHA tới cổng (chưa phải xác minh MST).</summary>
    public enum LoginSubmitOutcome
    {
        /// <summary>Cổng trả 200 kèm token; token do <see cref="IPortalLoginBrowser.CaptureSessionAsync"/> lấy.</summary>
        Accepted,

        /// <summary>Cổng từ chối (sai mật khẩu hoặc CAPTCHA, tài khoản bị khóa...): phải lấy CAPTCHA mới.</summary>
        Rejected,

        /// <summary>Cổng lỗi hoặc không phản hồi (5xx, mất mạng, quá thời gian).</summary>
        Unavailable
    }

    /// <summary>
    /// Trừu tượng hóa WebView (ADR G-01 chưa chốt). Không giả định token nằm trong cookie, header hay storage.
    /// </summary>
    public interface IPortalLoginBrowser : IDisposable
    {
        Control View { get; }

        event EventHandler<LoginCandidateEventArgs> LoginCandidateDetected;
        event EventHandler<BrowserNavigationEventArgs> NavigationChanged;
        event EventHandler<BrowserFailureEventArgs> BrowserFailed;

        /// <summary>Người dùng đóng hộp thoại đăng nhập của trang (nút X) khi chưa đăng nhập.</summary>
        event EventHandler LoginDialogClosed;

        /// <summary>Chuỗi cảnh báo "dữ liệu mô phỏng" của trình duyệt giả (Debug); null ở bản chạy thật.</summary>
        string SimulationNotice { get; }

        Task InitializeAsync(CancellationToken cancellationToken);
        Task ClearSessionAsync(CancellationToken cancellationToken);
        Task NavigateToLoginAsync(CancellationToken cancellationToken);
        Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken);

        /// <summary>Lấy CAPTCHA mới (<c>GET /api/captcha</c>) trong cùng phiên trình duyệt sẽ dùng để đăng nhập. Mỗi lần gọi trả một mã mới.</summary>
        Task<LoginCaptcha> GetCaptchaAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gửi <c>POST /api/security-taxpayer/authenticate</c> từ trong trang cổng (cùng cookie/phiên với CAPTCHA). <paramref name="username"/> là MST
        /// của doanh nghiệp đang chọn, do ứng dụng quyết định chứ người dùng không gõ. Mật khẩu chỉ đi qua đây, không được lưu hay ghi nhật ký.
        /// Trả kết quả HTTP; token không được trả về ở đây.
        /// </summary>
        Task<LoginSubmitOutcome> SubmitCredentialsAsync(string username, string password, LoginCaptcha captcha, string captchaText, CancellationToken cancellationToken);

        /// <summary>Phương án dự phòng: cho người dùng tự thao tác trên trang đăng nhập thật của cổng (như trước khi có form riêng).</summary>
        void EnableManualLogin();
    }
}
