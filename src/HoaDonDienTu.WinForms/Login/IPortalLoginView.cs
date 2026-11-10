using System;
using System.Windows.Forms;

namespace HoaDonDienTu.WinForms.Login
{
    public enum PortalLoginState
    {
        Created,
        InitializingBrowser,
        ClearingPreviousSession,
        LoadingPortalLogin,
        LoadingCaptcha,
        WaitingForUser,
        Submitting,
        VerifyingPortalSession,
        Succeeded,
        BrowserUnavailable,
        PortalUnavailable,
        InvalidSession,
        TaxCodeMismatch,
        Cancelled
    }

    /// <summary>View state không bao giờ chứa token/cookie.</summary>
    public sealed class PortalLoginViewState
    {
        public PortalLoginState State { get; set; }
        public string StepText { get; set; }
        public string Message { get; set; }
        public bool IsBusy { get; set; }
        public bool IsError { get; set; }
        public bool CanRetry { get; set; }

        /// <summary>Cho nhập mật khẩu/CAPTCHA và bấm Đăng nhập.</summary>
        public bool CanSubmit { get; set; }
    }

    /// <summary>Dữ liệu người dùng vừa nhập; chỉ sống trong lời gọi sự kiện, không lưu ở đâu.</summary>
    public sealed class LoginSubmitEventArgs : EventArgs
    {
        public LoginSubmitEventArgs(string password, string captchaText)
        {
            Password = password;
            CaptchaText = captchaText;
        }

        public string Password { get; private set; }

        public string CaptchaText { get; private set; }
    }

    public interface IPortalLoginView
    {
        event EventHandler ViewShown;
        event EventHandler RetryRequested;
        event EventHandler CancelRequested;
        event EventHandler<LoginSubmitEventArgs> SubmitRequested;
        event EventHandler RefreshCaptchaRequested;

        /// <summary>Người dùng chọn đăng nhập trực tiếp trên trang cổng Thuế (phương án dự phòng).</summary>
        event EventHandler ManualLoginRequested;

        void ShowRequest(string companyName, string expectedTaxCode);
        void ShowState(PortalLoginViewState state);
        void HostBrowser(Control browserView);

        /// <summary>Hiện ảnh CAPTCHA (SVG của cổng); null xóa ảnh. Ảnh không dựng được thì view tự báo cho người dùng bấm đổi mã.</summary>
        void ShowCaptcha(string svg);

        /// <summary>Xóa ô mật khẩu và ô CAPTCHA ngay sau khi đã gửi.</summary>
        void ClearSecrets();

        /// <summary>Hiện dòng cảnh báo dữ liệu mô phỏng (Debug).</summary>
        void ShowSimulationNotice(string text);

        /// <summary>Đổi sang bố cục WebView lớn để người dùng thao tác trên trang cổng thật.</summary>
        void ShowManualMode();

        void CloseView();

        /// <summary>Hiển thị dạng modal và chỉ trả về khi view đã đóng.</summary>
        void ShowModal(IWin32Window owner);
    }
}
