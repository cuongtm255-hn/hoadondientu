using System;
using System.Drawing;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Licensing
{
    /// <summary>SCR-12 Bản quyền. Chỉ hiển thị và phát sự kiện; logic nằm ở <see cref="LicensePresenter"/>.</summary>
    public partial class LicenseControl : KryptonPanel, ILicenseView
    {
        private bool _busy;
        private string _machineCode = string.Empty;

        public LicenseControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
        }

        public event EventHandler ActivateRequested;
        public event EventHandler BackRequested;

        public string LicenseKeyInput
        {
            get { return txtLicenseKey.Text; }
        }

        public void ShowState(LicenseViewModel model)
        {
            lblModeLetter.Values.Text = model.ModeLetter;
            lblModeTitle.Values.Text = model.ModeTitle;
            lblModeSubtitle.Values.Text = model.ModeSubtitle;
            lblDevice.Values.Text = model.Device;
            _machineCode = model.MachineCode ?? string.Empty;
            lblMachine.Values.Text = _machineCode;
            lblType.Values.Text = model.LicenseType;
            lblExpires.Values.Text = model.ExpiresAt;
            lblLast.Values.Text = model.LastValidation;
            lblVersion.Values.Text = model.Version;
            lblNote.Text = model.Note;
            UpdateButtons();
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            UseWaitCursor = busy;
            UpdateButtons();
        }

        public void ShowMessage(string text, bool isError)
        {
            lblMessage.Text = text ?? string.Empty;
            lblMessage.ForeColor = isError ? Color.FromArgb(170, 40, 30) : Color.FromArgb(30, 110, 60);
        }

        public void ClearKeyInput()
        {
            txtLicenseKey.Text = string.Empty;
        }

        private void UpdateButtons()
        {
            btnActivate.Enabled = !_busy && txtLicenseKey.Text.Trim().Length > 0;
            btnCopyMachine.Enabled = _machineCode.Length > 0;
            btnBack.Enabled = !_busy;
        }

        private void OnKeyTextChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void OnActivateClick(object sender, EventArgs e)
        {
            Raise(ActivateRequested);
        }

        private void OnCopyMachineClick(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Forms.Clipboard.SetText(_machineCode);
                ShowMessage("Đã sao chép mã máy. Dán vào email hoặc tin nhắn gửi cho đơn vị phát hành.", false);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard đang bị ứng dụng khác giữ: nhắc người dùng chọn và chép tay.
                ShowMessage("Không sao chép được (bảng nhớ tạm đang bị chiếm). Hãy thử lại sau giây lát.", true);
            }
        }

        private void OnBackClick(object sender, EventArgs e)
        {
            Raise(BackRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
