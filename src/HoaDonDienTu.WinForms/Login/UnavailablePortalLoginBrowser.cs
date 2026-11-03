using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Authentication;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Trình duyệt đăng nhập của bản production cho tới khi chọn xong WebView (ADR G-01, T01-16). Không đăng nhập giả:
    /// báo rõ lý do để người dùng biết chức năng chưa sẵn sàng thay vì thấy một trang trống.
    /// </summary>
    public sealed class UnavailablePortalLoginBrowser : IPortalLoginBrowser
    {
        public const string Reason = "Trình duyệt đăng nhập cổng Thuế chưa được tích hợp trong bản này.";

        private readonly KryptonPanel _view = new KryptonPanel();

        public UnavailablePortalLoginBrowser()
        {
            _view.Controls.Add(new KryptonLabel { Dock = DockStyle.Fill, Values = { Text = Reason } });
        }

        public Control View
        {
            get { return _view; }
        }

        public event EventHandler<LoginCandidateEventArgs> LoginCandidateDetected
        {
            add { }
            remove { }
        }

        public event EventHandler<BrowserNavigationEventArgs> NavigationChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<BrowserFailureEventArgs> BrowserFailed;

        public event EventHandler LoginDialogClosed
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            var handler = BrowserFailed;
            if (handler != null) handler(this, new BrowserFailureEventArgs(Reason));
            return Task.FromResult(0);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task NavigateToLoginAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(Reason);
        }

        public void Dispose()
        {
            _view.Dispose();
        }
    }
}
