using System;
using System.Drawing;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Licensing
{
    /// <summary>SCR-12 Bản quyền. Chỉ hiển thị và phát sự kiện; logic nằm ở <see cref="LicensePresenter"/>.</summary>
    public partial class LicenseControl : KryptonPanel, ILicenseView
    {
        private bool _busy;
        private bool _hasLicense;

        public LicenseControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
        }

        public event EventHandler ActivateRequested;
        public event EventHandler ValidateRequested;
        public event EventHandler DeactivateRequested;
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
            lblMachine.Values.Text = model.MachineShort;
            lblType.Values.Text = model.LicenseType;
            lblExpires.Values.Text = model.ExpiresAt;
            lblLast.Values.Text = model.LastValidation;
            lblVersion.Values.Text = model.Version;
            lblNote.Text = model.Note;
            _hasLicense = model.HasLicense;
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
            btnValidate.Enabled = !_busy && _hasLicense;
            btnDeactivate.Enabled = !_busy && _hasLicense;
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

        private void OnValidateClick(object sender, EventArgs e)
        {
            Raise(ValidateRequested);
        }

        private void OnDeactivateClick(object sender, EventArgs e)
        {
            Raise(DeactivateRequested);
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
