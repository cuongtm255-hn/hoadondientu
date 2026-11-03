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

        Task InitializeAsync(CancellationToken cancellationToken);
        Task ClearSessionAsync(CancellationToken cancellationToken);
        Task NavigateToLoginAsync(CancellationToken cancellationToken);
        Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken);
    }
}
