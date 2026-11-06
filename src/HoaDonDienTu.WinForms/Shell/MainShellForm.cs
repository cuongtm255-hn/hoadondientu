using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ComponentFactory.Krypton.Ribbon;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Shell
{
    public partial class MainShellForm : KryptonForm, IMainShellView
    {
        private readonly ScreenHost _screenHost;
        private readonly List<KryptonCheckButton> _navButtons;
        private bool _closeApproved;

        public MainShellForm()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
            _screenHost = new ScreenHost(pnlContent);
            _navButtons = new List<KryptonCheckButton>
            {
                navDashboard, navDownload, navInvoiceArchive, navDownloadHistory, navCompanies, navSettings
            };
            StyleNavigationButtons();
            DecorateRibbon();
#if DEBUG
            ApplyTestMarker();
#endif
        }

        /// <summary>Icon lớn (32) và nhỏ (16) cho các nút ribbon thay cho biểu tượng mặc định.</summary>
        private void DecorateRibbon()
        {
            SetRibbonIcon(ribbonBtnDownload, UiIcon.Download);
            SetRibbonIcon(ribbonBtnArchive, UiIcon.Archive);
            SetRibbonIcon(ribbonBtnHistory, UiIcon.History);
            SetRibbonIcon(ribbonBtnSwitchCompany, UiIcon.Switch);
            SetRibbonIcon(ribbonBtnCompanies, UiIcon.Building);
            SetRibbonIcon(ribbonBtnPortalLogin, UiIcon.Login);
        }

        private static void SetRibbonIcon(ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton button, UiIcon icon)
        {
            button.ImageLarge = UiIcons.Create(icon, 32, UiIcons.Accent);
            button.ImageSmall = UiIcons.Create(icon, 16, UiIcons.Accent);
        }

        private static readonly System.Drawing.Color NavActiveText = System.Drawing.Color.White;
        private static readonly System.Drawing.Color NavDisabledText = System.Drawing.Color.FromArgb(120, 134, 150);
        private static readonly System.Drawing.Color NavHoverBack = System.Drawing.Color.FromArgb(60, 78, 98);
        private static readonly System.Drawing.Color NavCheckedBack = System.Drawing.Color.FromArgb(31, 111, 178);

        /// <summary>Icon theo màn hình, chữ đậm khi đang chọn, hover rõ, mục bị khóa xám hẳn và có icon xám.</summary>
        private void StyleNavigationButtons()
        {
            foreach (var button in _navButtons)
            {
                var screen = (ScreenId)button.Tag;
                var normalIcon = NavigationIcons.Create(screen, NavActiveText);
                var disabledIcon = NavigationIcons.Create(screen, NavDisabledText);

                button.StateCommon.Content.Padding = new Padding(14, 0, 0, 0);
                button.StateCommon.Content.AdjacentGap = 12;
                button.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI Semibold", 10.5F);
                button.StateCommon.Content.ShortText.Color1 = NavActiveText;
                button.StateCommon.Content.Image.ImageH = PaletteRelativeAlign.Near;

                button.StateTracking.Back.Color1 = NavHoverBack;
                button.StateTracking.Back.Color2 = NavHoverBack;
                button.StatePressed.Back.Color1 = NavHoverBack;
                button.StatePressed.Back.Color2 = NavHoverBack;
                button.StateCheckedNormal.Back.Color1 = NavCheckedBack;
                button.StateCheckedNormal.Back.Color2 = NavCheckedBack;
                button.StateCheckedTracking.Back.Color1 = NavCheckedBack;
                button.StateCheckedTracking.Back.Color2 = NavCheckedBack;
                button.StateCheckedPressed.Back.Color1 = NavCheckedBack;
                button.StateCheckedPressed.Back.Color2 = NavCheckedBack;
                button.StateDisabled.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
                button.StateDisabled.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
                button.StateDisabled.Content.ShortText.Color1 = NavDisabledText;
                button.StateDisabled.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Italic);

                var captured = button;
                EventHandler refresh = delegate
                {
                    captured.Values.Image = captured.Enabled ? normalIcon : disabledIcon;
                };
                button.EnabledChanged += refresh;
                refresh(button, EventArgs.Empty);
                button.Disposed += delegate
                {
                    normalIcon.Dispose();
                    disabledIcon.Dispose();
                };
            }
        }

#if DEBUG
        /// <summary>
        /// Chính sách test (CLAUDE.md): bản Debug chạy hoàn toàn trên dữ liệu giả nên phải luôn hiện nhãn TEST/MOCK ở tiêu đề và banner
        /// trên cùng. Không có trong bản Release.
        /// </summary>
        public const string TestMarker = "[TEST] DỮ LIỆU GIẢ";

        private void ApplyTestMarker()
        {
            Text += " — " + TestMarker;

            var banner = new KryptonPanel
            {
                Name = "pnlTestBanner",
                Dock = DockStyle.Top,
                Height = 26,
                PanelBackStyle = PaletteBackStyle.PanelCustom1
            };
            banner.StateCommon.Color1 = System.Drawing.Color.FromArgb(255, 193, 7);
            var label = new KryptonLabel
            {
                Name = "lblTestBanner",
                Dock = DockStyle.Fill,
                Values = { Text = "CHẾ ĐỘ TEST — dữ liệu giả, không gọi cổng Thuế thật" }
            };
            label.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(60, 40, 0);
            label.StateCommon.ShortText.TextH = PaletteRelativeAlign.Center;
            banner.Controls.Add(label);
            Controls.Add(banner);
            banner.SendToBack();
        }
#endif

        public IWin32Window OwnerWindow
        {
            get { return this; }
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

            // Đã đăng nhập thì nút trên ribbon chuyển thành "Đăng nhập lại" (đổi phiên/MST), không còn gợi ý rằng chưa đăng nhập.
            ribbonBtnPortalLogin.TextLine1 = "Đăng nhập";
            ribbonBtnPortalLogin.TextLine2 = state.IsLoggedIn ? "lại" : string.Empty;
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

        public void CloseShell()
        {
            _closeApproved = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Người dùng đóng cửa sổ: hoãn lại để presenter xử lý tác vụ đang chạy rồi mới gọi CloseShell.
            // WM_CLOSE gửi từ ngoài (taskbar, công cụ khác) được WinForms báo là TaskManagerClosing nên cũng phải xử lý.
            // Các lý do khác (tắt Windows, Application.Exit sau lỗi nghiêm trọng) không được chặn.
            if (!_closeApproved
                && (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.TaskManagerClosing))
            {
                e.Cancel = true;
                var handler = ShellClosingRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }

            base.OnFormClosing(e);
        }

        private void OnNavigationClick(object sender, EventArgs e)
        {
            object tag = null;
            var control = sender as Control;
            var ribbonButton = sender as KryptonRibbonGroupButton;
            if (control != null) tag = control.Tag;
            else if (ribbonButton != null) tag = ribbonButton.Tag;
            if (!(tag is ScreenId)) return;

            // Việc đánh dấu mục menu do NavigationService quyết định sau khi điều hướng thành công hay bị chặn.
            var handler = NavigationRequested;
            if (handler != null) handler(this, new NavigationRequestedEventArgs((ScreenId)tag));
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
