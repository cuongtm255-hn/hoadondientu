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
        }

        public event EventHandler ViewShown;
        public event EventHandler RetryRequested;
        public event EventHandler ClearSessionRequested;
        public event EventHandler CancelRequested;

        public void ShowRequest(string companyName, string expectedTaxCode)
        {
            lblCompanyName.Values.Text = companyName;
            lblExpectedTaxCode.Values.Text = "MST cần đăng nhập: " + expectedTaxCode;
            lblFooter.Values.Text = "MST cấu hình: " + expectedTaxCode + " · Kết nối TLS 1.2 · Token chỉ lưu trong bộ nhớ";
        }

        public void ShowState(PortalLoginViewState state)
        {
            lblStateMessage.Text = state.Message ?? string.Empty;
            lblStateMessage.ForeColor = state.IsError ? Color.FromArgb(170, 40, 30) : SystemColors.ControlText;
            progressLogin.Visible = state.IsBusy;
            btnRetry.Visible = state.CanRetry;
            btnClearSession.Enabled = !state.IsBusy;
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

        private void OnClearSessionClick(object sender, EventArgs e)
        {
            Raise(ClearSessionRequested);
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
