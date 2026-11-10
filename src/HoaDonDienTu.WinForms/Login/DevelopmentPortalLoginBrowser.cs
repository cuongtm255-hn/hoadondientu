#if DEBUG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Infrastructure.Development.Mock;

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
        private int _captchaCount;

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
        public event EventHandler LoginDialogClosed;

        /// <summary>Mật khẩu giả đưa tới kết quả "bị từ chối".</summary>
        public const string RejectedPassword = "sai";

        /// <summary>Mật khẩu giả đăng nhập thành công nhưng bằng tài khoản của MST khác (thử chặn sai MST).</summary>
        public const string OtherTaxCodePassword = "khac-mst";

        public string SimulationNotice
        {
            get
            {
                return "MÔ PHỎNG (Debug): không gọi cổng Thuế. Mật khẩu \"" + RejectedPassword + "\" bị từ chối, \"" + OtherTaxCodePassword
                    + "\" đăng nhập bằng MST khác, giá trị khác thành công. CAPTCHA là ảnh giả, gõ gì cũng được.";
            }
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken)
        {
            _simulatedTaxCode = null;
            _status.Values.Text = "Phiên đăng nhập đã được làm mới.";
            return Task.FromResult(true);
        }

        public Task NavigateToLoginAsync(CancellationToken cancellationToken)
        {
            _status.Values.Text = "Trang đăng nhập mô phỏng (Debug). Chọn một nút bên dưới.";
            var handler = NavigationChanged;
            if (handler != null) handler(this, new BrowserNavigationEventArgs("https://hoadondientu.gdt.gov.vn/ (mô phỏng)"));
            return Task.FromResult(true);
        }

        /// <summary>
        /// Mô phỏng đúng đường đi của bản thật: trang đăng nhập gọi <c>POST /api/security-taxpayer/authenticate</c>, WebView ghi lại request/response
        /// cùng cookie rồi <see cref="PortalAuthCapture"/> rút ra token, tên đăng nhập, cookie và User-Agent. Không có mật khẩu hay CAPTCHA nào được tạo ra.
        /// </summary>
        public Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken)
        {
            var token = _simulatedTaxCode == null
                ? MockInvoiceDataset.ExpiredTokenFor(_expectedTaxCode())
                : MockInvoiceDataset.TokenFor(_simulatedTaxCode);
            var username = _simulatedTaxCode ?? _expectedTaxCode();

            PortalAuthCapture capture;
            if (!PortalAuthCapture.TryCreate(
                    "{\"username\":\"" + username + "\"}",
                    "{\"token\":\"" + token + "\"}",
                    new System.Collections.Generic.Dictionary<string, string> { { "jwt", token }, { "JSESSIONID", "MOCKSESSION" } },
                    "Mozilla/5.0 (Debug WebView)",
                    out capture))
            {
                return Task.FromResult(new PortalSessionMaterial(null, null, null));
            }

            return Task.FromResult(capture.ToMaterial());
        }

        public Task<LoginCaptcha> GetCaptchaAsync(CancellationToken cancellationToken)
        {
            _captchaCount++;
            // Ảnh SVG giả cùng dạng với ảnh thật (path tô màu + đường nhiễu), vẽ hình khối thay vì chữ.
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"40\" viewBox=\"0,0,200,40\">"
                + "<path d=\"M10 30 C60 10,120 35,190 12\" stroke=\"#222\" fill=\"none\"/>"
                + "<path fill=\"#222\" d=\"M20 8L40 8L40 32L20 32ZM26 14L26 26L34 26L34 14Z\"/>"
                + "<path fill=\"#333\" d=\"M60 8L80 8Q90 20 80 32L60 32Z\"/>"
                + "<path fill=\"#222\" d=\"M100 8L120 8L120 32L100 32Z\"/>"
                + "<path fill=\"#333\" d=\"M140 8L170 8L155 32Z\"/>"
                + "</svg>";
            return Task.FromResult(new LoginCaptcha("mock-captcha-" + _captchaCount, svg));
        }

        public Task<LoginSubmitOutcome> SubmitCredentialsAsync(string username, string password, LoginCaptcha captcha, string captchaText, CancellationToken cancellationToken)
        {
            if (password == RejectedPassword) return Task.FromResult(LoginSubmitOutcome.Rejected);
            Simulate(password == OtherTaxCodePassword ? "0109999999" : username);
            return Task.FromResult(LoginSubmitOutcome.Accepted);
        }

        public void EnableManualLogin()
        {
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

        /// <summary>Mô phỏng người dùng bấm nút đóng hộp thoại đăng nhập của trang.</summary>
        public void RaiseDialogClosed()
        {
            var handler = LoginDialogClosed;
            if (handler != null) handler(this, EventArgs.Empty);
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
