#if DEBUG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Infrastructure.Development;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Trình duyệt giả cho phát triển: trang mô phỏng đăng nhập cổng Thuế với hai nút chọn MST đúng/sai.
    /// Chỉ có ở Debug; production adapter chờ ADR G-01/G-02 (T01-16).
    /// </summary>
    public sealed class DevelopmentPortalLoginBrowser : IPortalLoginBrowser
    {
        private readonly Func<string> _expectedTaxCode;
        private readonly KryptonPanel _view = new KryptonPanel();
        private readonly KryptonLabel _status = new KryptonLabel();
        private string _simulatedTaxCode;

        public DevelopmentPortalLoginBrowser(Func<string> expectedTaxCode)
        {
            _expectedTaxCode = expectedTaxCode;
            BuildView();
        }

        public Control View
        {
            get { return _view; }
        }

        public event EventHandler<LoginCandidateEventArgs> LoginCandidateDetected;
        public event EventHandler<BrowserNavigationEventArgs> NavigationChanged;
        public event EventHandler<BrowserFailureEventArgs> BrowserFailed;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken)
        {
            _simulatedTaxCode = null;
            _status.Values.Text = "Phiên trình duyệt đã được xóa.";
            return Task.FromResult(true);
        }

        public Task NavigateToLoginAsync(CancellationToken cancellationToken)
        {
            _status.Values.Text = "Trang đăng nhập mô phỏng (Debug). Chọn một nút bên dưới.";
            var handler = NavigationChanged;
            if (handler != null) handler(this, new BrowserNavigationEventArgs("https://hoadondientu.gdt.gov.vn/ (mô phỏng)"));
            return Task.FromResult(true);
        }

        public Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>();
            if (_simulatedTaxCode != null) headers[FakePortalAuthenticationService.DevTaxCodeHeader] = _simulatedTaxCode;
            return Task.FromResult(new PortalSessionMaterial(null, headers, "dev-token"));
        }

        public void Dispose()
        {
            _view.Dispose();
        }

        private void BuildView()
        {
            var title = new KryptonLabel { Text = "HỆ THỐNG HÓA ĐƠN ĐIỆN TỬ (mô phỏng)", Location = new Point(24, 20), AutoSize = true };
            title.StateCommon.ShortText.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.StateCommon.ShortText.Color1 = Color.FromArgb(160, 40, 30);

            var okButton = new KryptonButton { Text = "Đăng nhập đúng MST", Location = new Point(28, 80), Size = new Size(220, 36) };
            okButton.Click += delegate { Simulate(_expectedTaxCode()); };

            var wrongButton = new KryptonButton { Text = "Đăng nhập sai MST", Location = new Point(28, 126), Size = new Size(220, 36) };
            wrongButton.Click += delegate { Simulate("0109999999"); };

            _status.Location = new Point(28, 180);
            _status.AutoSize = true;

            _view.Controls.Add(title);
            _view.Controls.Add(okButton);
            _view.Controls.Add(wrongButton);
            _view.Controls.Add(_status);
        }

        private void Simulate(string taxCode)
        {
            _simulatedTaxCode = taxCode;
            var handler = LoginCandidateDetected;
            if (handler != null) handler(this, new LoginCandidateEventArgs());
        }

        /// <summary>Dùng để thử đường lỗi trình duyệt từ test/dev.</summary>
        public void RaiseFailure(string message)
        {
            var handler = BrowserFailed;
            if (handler != null) handler(this, new BrowserFailureEventArgs(message));
        }
    }
}
#endif
