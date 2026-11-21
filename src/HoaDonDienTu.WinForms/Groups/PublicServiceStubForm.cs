using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.PublicService;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>Dịch vụ đăng nhập DVC dùng chung cho cửa sổ nhóm Dịch vụ công. Release chưa có trình duyệt thật nên Login = null.</summary>
    public sealed class DvcServices : IDisposable
    {
        public DvcLoginService Login { get; private set; }

        public DvcSession Session { get; private set; }

        /// <summary>Dịch vụ tra cứu danh mục TTHC; null nếu bản này chưa có nguồn dữ liệu (Release chờ G-DVC-03/06).</summary>
        public TthcService Tthc { get; private set; }

        public string TthcNotice { get; private set; }

        /// <summary>Dịch vụ tra cứu hồ sơ đã nộp (cần đăng nhập); null nếu bản này chưa có nguồn (Release chờ đăng nhập thật và mẫu HTML).</summary>
        public HoSoService HoSo { get; private set; }

        public string HoSoNotice { get; private set; }

        private IDisposable _disposable;

        public void Dispose()
        {
            if (_disposable != null) _disposable.Dispose();
            _disposable = null;
        }

        /// <summary>Dòng chú thích hiển thị trên form đăng nhập (bản mô phỏng ghi rõ cách thử).</summary>
        public string Notice { get; private set; }

        public static DvcServices Create(IApplicationLogger logger, string userNameFile)
        {
            var session = new DvcSession();
#if DEBUG
            var names = new HoaDonDienTu.Infrastructure.Settings.FileDvcUserNameStore(userNameFile);
            return new DvcServices
            {
                Session = session,
                Login = new DvcLoginService(new MockDvcLoginBrowser(), session, logger, names),
                Notice = MockDvcLoginBrowser.Hint,
                Tthc = new TthcService(new MockTthcSource(), session, logger),
                TthcNotice = MockTthcSource.Hint,
                HoSo = new HoSoService(new MockHoSoSource(), session, logger),
                HoSoNotice = MockHoSoSource.Hint
            };
#else
            // Release: tra cứu danh mục TTHC công khai chạy thật qua WebView ngầm (không cần đăng nhập); đăng nhập DVC chưa có
            // CefDvcLoginBrowser (chờ G-DVC-02/06) nên nút Đăng nhập bị khóa.
            var runner = new CefDvcPageRunner(m => logger.Info(m));
            return new DvcServices
            {
                Session = session,
                Tthc = new TthcService(new HoaDonDienTu.Infrastructure.PublicService.PortalTthcSource(runner), session, logger),
                _disposable = runner
            };
#endif
        }
    }

    /// <summary>
    /// Cửa sổ nhóm Dịch vụ công. Hiện mới có đăng nhập (mô phỏng ở Debug); các tra cứu làm ở DD-DVC-02 trở đi.
    /// Không gọi cổng thật.
    /// </summary>
    public sealed class PublicServiceStubForm : KryptonForm
    {
        private readonly DvcServices _services;
        private readonly Func<string> _taxCode;
        private readonly Label _state = new Label();
        private readonly KryptonButton _loginButton = new KryptonButton();
        private readonly KryptonButton _tthcButton = new KryptonButton();
        private readonly KryptonButton _hoSoButton = new KryptonButton();

        public PublicServiceStubForm(DvcServices services, Func<string> taxCode)
        {
            _services = services;
            _taxCode = taxCode ?? (() => string.Empty);

            Text = "Hóa đơn điện tử — Tra cứu Dịch vụ công thuế"
#if DEBUG
                + " — [TEST] DỮ LIỆU GIẢ"
#endif
                ;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 240);

            var switchButton = new KryptonButton { Text = "Đổi nhóm chức năng", Dock = DockStyle.Bottom, Height = 40 };
            switchButton.Click += (s, e) =>
            {
                var handler = SwitchRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };

            _state.SetBounds(20, 20, 520, 70);
            _state.TextAlign = ContentAlignment.MiddleLeft;
            _loginButton.SetBounds(20, 100, 160, 36);
            _loginButton.Values.Text = "Đăng nhập cổng DVC";
            _loginButton.Click += (s, e) => ShowLogin();

            _tthcButton.SetBounds(190, 100, 200, 36);
            _tthcButton.Values.Text = "Tra cứu thủ tục hành chính";
            _tthcButton.Enabled = _services.Tthc != null;
            _tthcButton.Click += (s, e) => ShowTthc();
            Controls.Add(_tthcButton);

            _hoSoButton.SetBounds(400, 100, 150, 36);
            _hoSoButton.Values.Text = "Hồ sơ đã nộp";
            _hoSoButton.Enabled = false;
            _hoSoButton.Click += (s, e) => ShowHoSo();
            Controls.Add(_hoSoButton);

            var note = new Label
            {
                Text = "Các chức năng tra cứu của nhóm này đang được phát triển.",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(20, 160)
            };
            Controls.Add(note);
            Controls.Add(_state);
            Controls.Add(_loginButton);
            Controls.Add(switchButton);

            if (_services.Login == null)
            {
                _loginButton.Enabled = false;
                _state.Text = "Bản này chưa hỗ trợ đăng nhập cổng Dịch vụ công.";
            }
            else
            {
                _services.Session.StateChanged += OnSessionChanged;
                RefreshState();
            }
        }

        /// <summary>Người dùng bấm "Đổi nhóm chức năng".</summary>
        public event EventHandler SwitchRequested;

        private void ShowLogin()
        {
            using (var form = new DvcLoginForm(_services.Login, _taxCode(), _services.Notice))
            {
                form.ShowDialog(this);
            }
            RefreshState();
        }

        private void ShowTthc()
        {
            using (var form = new TthcForm(new TthcSearchSession(_services.Tthc), _services.TthcNotice))
            {
                form.ShowDialog(this);
            }
        }

        private void ShowHoSo()
        {
            var form = new HoSoForm(new HoSoSearchSession(_services.HoSo), _services.HoSoNotice);
            form.FormClosed += (s, e) => form.Dispose();
            form.Show(this);
        }

        private void OnSessionChanged(object sender, EventArgs e)
        {
            // Sự kiện có thể phát từ luồng nền (dịch vụ dùng ConfigureAwait(false)).
            if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)RefreshState);
        }

        private void RefreshState()
        {
            var session = _services.Session;
            // Hồ sơ đã nộp là dữ liệu riêng tư: chỉ mở được khi đã đăng nhập và bản này có nguồn dữ liệu.
            _hoSoButton.Enabled = session.IsLoggedIn && _services.HoSo != null;
            switch (session.State)
            {
                case DvcSessionState.LoggedIn:
                    _state.Text = "Đã đăng nhập: " + session.UserName + Environment.NewLine + session.OrganizationName;
                    _loginButton.Values.Text = "Đăng nhập lại";
                    break;
                case DvcSessionState.Expired:
                    _state.Text = "Phiên đã hết hạn. Hãy đăng nhập lại.";
                    break;
                case DvcSessionState.TakenByOtherTab:
                    _state.Text = "Tài khoản đang được dùng ở nơi khác. Hãy đóng nơi đó rồi đăng nhập lại.";
                    break;
                default:
                    _state.Text = "Chưa đăng nhập cổng Dịch vụ công.";
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _services.Login != null) _services.Session.StateChanged -= OnSessionChanged;
            base.Dispose(disposing);
        }
    }
}
