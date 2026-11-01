using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ComponentFactory.Krypton.Ribbon;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Navigation;

namespace HoaDonDienTu.WinForms.Shell
{
    public partial class MainShellForm : KryptonForm, IMainShellView
    {
        private readonly ScreenHost _screenHost;
        private readonly List<KryptonCheckButton> _navButtons;

        public MainShellForm()
        {
            InitializeComponent();
            _screenHost = new ScreenHost(pnlContent);
            _navButtons = new List<KryptonCheckButton>
            {
                navDashboard, navDownload, navInvoiceArchive, navDownloadHistory, navCompanies, navSettings
            };
        }

        public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;
        public event EventHandler CompanySelectorRequested;
        public event EventHandler PortalLoginRequested;
        public event EventHandler ShellClosingRequested;

        public void ApplyMode(ShellModeViewState state)
        {
            btnCompanyContext.Enabled = state.ShowCompanySelector;
            ribbonGroupCompany.Visible = state.ShowCompanyMenu;
            navCompanies.Visible = state.ShowCompanyMenu;
            ribbonBtnPortalLogin.Enabled = state.CanPortalLogin;
            btnPortalLogin.Enabled = state.CanPortalLogin;
            navDownload.Enabled = state.CanDownload;
            ribbonBtnDownload.Enabled = state.CanDownload;
            lblLicenseStatus.Values.Text = state.LicenseLabel ?? string.Empty;
        }

        public void ShowCompany(CompanyHeaderViewState state)
        {
            btnCompanyContext.Values.Text = state.CompanyName;
            lblTaxCode.Values.Text = string.IsNullOrEmpty(state.TaxCode) ? string.Empty : "MST " + state.TaxCode;
        }

        public void ShowPortalSession(PortalSessionViewState state)
        {
            lblPortalSession.Values.Text = state.Text;
            btnPortalLogin.Visible = !state.IsLoggedIn;
        }

        public void ShowOperation(OperationViewState state)
        {
            lblOperationStatus.Text = state.Text;
            progressOperation.Visible = state.IsBusy;
            progressOperation.Value = Math.Max(0, Math.Min(100, state.Percent));
        }

        public void ShowScreen(Control screen)
        {
            _screenHost.Show(screen);
        }

        public void ShowError(string title, string message)
        {
            KryptonMessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void SelectNavigation(ScreenId screen)
        {
            foreach (var button in _navButtons)
            {
                button.Checked = button.Tag is ScreenId && (ScreenId)button.Tag == screen;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            var handler = ShellClosingRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnNavigationClick(object sender, EventArgs e)
        {
            object tag = null;
            var control = sender as Control;
            var ribbonButton = sender as KryptonRibbonGroupButton;
            if (control != null) tag = control.Tag;
            else if (ribbonButton != null) tag = ribbonButton.Tag;
            if (!(tag is ScreenId)) return;

            var target = (ScreenId)tag;
            SelectNavigation(target);
            var handler = NavigationRequested;
            if (handler != null) handler(this, new NavigationRequestedEventArgs(target));
        }

        private void OnCompanySelectorClick(object sender, EventArgs e)
        {
            var handler = CompanySelectorRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnPortalLoginClick(object sender, EventArgs e)
        {
            var handler = PortalLoginRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
