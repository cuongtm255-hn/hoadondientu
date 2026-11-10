using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Login
{
    public partial class PortalLoginForm : KryptonForm, IPortalLoginView
    {
        private bool _closingByPresenter;

        public PortalLoginForm()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
        }

        public event EventHandler ViewShown;
        public event EventHandler RetryRequested;
        public event EventHandler CancelRequested;
        public event EventHandler<LoginSubmitEventArgs> SubmitRequested;
        public event EventHandler RefreshCaptchaRequested;
        public event EventHandler ManualLoginRequested;

        public void ShowRequest(string companyName, string expectedTaxCode)
        {
            lblTaxCodeValue.Values.Text = expectedTaxCode;
            lblCompanyName.Values.Text = companyName;
            lblExpectedTaxCode.Values.Text = "MST cần đăng nhập: " + expectedTaxCode;
            lblFooter.Values.Text = "MST cần đăng nhập: " + expectedTaxCode;
        }

        public void ShowState(PortalLoginViewState state)
        {
            lblStateMessage.Text = state.Message ?? string.Empty;
            lblStateMessage.ForeColor = state.IsError ? Color.FromArgb(170, 40, 30) : SystemColors.ControlText;
            progressLogin.Visible = state.IsBusy;
            btnRetry.Visible = state.CanRetry;

            var wasEnabled = btnLogin.Enabled;
            txtPassword.Enabled = state.CanSubmit;
            txtCaptcha.Enabled = state.CanSubmit;
            btnLogin.Enabled = state.CanSubmit;
            btnRefreshCaptcha.Enabled = state.CanSubmit;
            if (state.CanSubmit && !wasEnabled)
            {
                // Vừa sẵn sàng (hoặc vừa phải nhập lại): đặt con trỏ vào ô còn thiếu.
                if (txtPassword.Text.Length == 0) txtPassword.Focus();
                else txtCaptcha.Focus();
            }
        }

        public void ShowCaptcha(string svg)
        {
            var old = picCaptcha.Image;
            picCaptcha.Image = null;
            if (old != null) old.Dispose();
            txtCaptcha.Clear();
            if (string.IsNullOrEmpty(svg)) return;

            try
            {
                picCaptcha.Image = CaptchaSvgRenderer.Render(svg, 300, 60);
            }
            catch (FormatException)
            {
                // Ảnh lạ (cổng đổi định dạng) không được làm form sập: báo trên chính khung ảnh để người dùng bấm "Đổi mã khác".
                var fallback = new Bitmap(300, 60);
                using (var graphics = Graphics.FromImage(fallback))
                {
                    graphics.Clear(Color.White);
                    graphics.DrawString("Không hiển thị được ảnh. Bấm \"Đổi mã khác\".", SystemFonts.DefaultFont, Brushes.DarkRed, new PointF(6, 20));
                }

                picCaptcha.Image = fallback;
            }
        }

        public void ClearSecrets()
        {
            txtPassword.Clear();
            txtCaptcha.Clear();
        }

        public void ShowSimulationNotice(string text)
        {
            lblSimulation.Text = text ?? string.Empty;
            lblSimulation.Visible = !string.IsNullOrEmpty(text);
        }

        public void ShowManualMode()
        {
            ClearSecrets();
            pnlCredentials.Visible = false;
            pnlBrowserHost.Dock = DockStyle.Fill;
            MinimumSize = new Size(820, 520);
            ClientSize = new Size(1000, 640);
            CenterToParent();
        }

        public void HostBrowser(Control browserView)
        {
            browserView.Dock = DockStyle.Fill;
            pnlBrowserHost.Controls.Clear();
            pnlBrowserHost.Controls.Add(browserView);
        }

        public void ShowModal(IWin32Window owner)
        {
            ShowDialog(owner);
        }

        public void CloseView()
        {
            _closingByPresenter = true;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Raise(ViewShown);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Đóng bằng nút X được coi là Hủy để presenter xóa phiên trước khi đóng.
            // Tắt Windows hoặc đóng từ bên ngoài không được bị hộp thoại này chặn.
            if (!_closingByPresenter && (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None))
            {
                e.Cancel = true;
                Raise(CancelRequested);
                return;
            }

            base.OnFormClosing(e);
        }

        private void OnRetryClick(object sender, EventArgs e)
        {
            Raise(RetryRequested);
        }

        private void OnLoginClick(object sender, EventArgs e)
        {
            var handler = SubmitRequested;
            if (handler != null) handler(this, new LoginSubmitEventArgs(txtPassword.Text, txtCaptcha.Text));
        }

        private void OnRefreshCaptchaClick(object sender, EventArgs e)
        {
            Raise(RefreshCaptchaRequested);
        }

        private void OnManualClick(object sender, EventArgs e)
        {
            Raise(ManualLoginRequested);
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            Raise(CancelRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
