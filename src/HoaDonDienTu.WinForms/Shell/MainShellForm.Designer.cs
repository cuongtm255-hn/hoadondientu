namespace HoaDonDienTu.WinForms.Shell
{
    partial class MainShellForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.kryptonManager = new ComponentFactory.Krypton.Toolkit.KryptonManager(this.components);
            this.ribbon = new ComponentFactory.Krypton.Ribbon.KryptonRibbon();
            this.ribbonTabHome = new ComponentFactory.Krypton.Ribbon.KryptonRibbonTab();
            this.ribbonGroupInvoices = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup();
            this.ribbonTripleInvoices = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupTriple();
            this.ribbonBtnDownload = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton();
            this.ribbonBtnArchive = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton();
            this.ribbonBtnHistory = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton();
            this.ribbonGroupCompany = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup();
            this.ribbonTripleCompany = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupTriple();
            this.ribbonBtnSwitchCompany = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton();
            this.ribbonBtnCompanies = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton();
            this.ribbonGroupPortal = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup();
            this.ribbonTriplePortal = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupTriple();
            this.ribbonBtnPortalLogin = new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton();
            this.pnlContent = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlHeader = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnCompanyContext = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblTaxCode = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblLicenseStatus = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblPortalSession = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnPortalLogin = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlSidebar = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.navSettings = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.navCompanies = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.navDownloadHistory = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.navInvoiceArchive = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.navDownload = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.navDashboard = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.lblApplicationTagline = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblApplicationName = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.statusStrip = new ComponentFactory.Krypton.Toolkit.KryptonStatusStrip();
            this.lblOperationStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.progressOperation = new System.Windows.Forms.ToolStripProgressBar();
            this.lblApplicationVersion = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.ribbon)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlContent)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).BeginInit();
            this.pnlHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlSidebar)).BeginInit();
            this.pnlSidebar.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // kryptonManager
            //
            this.kryptonManager.GlobalPaletteMode = ComponentFactory.Krypton.Toolkit.PaletteModeManager.Office2010Silver;
            //
            // ribbon
            //
            this.ribbon.Name = "ribbon";
            this.ribbon.RibbonAppButton.AppButtonText = "Hóa đơn";
            this.ribbon.RibbonAppButton.AppButtonShowRecentDocs = false;
            this.ribbon.RibbonTabs.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonTab[] {
            this.ribbonTabHome});
            this.ribbon.SelectedContext = null;
            this.ribbon.SelectedTab = this.ribbonTabHome;
            this.ribbon.TabIndex = 4;
            //
            // ribbonTabHome
            //
            this.ribbonTabHome.Groups.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup[] {
            this.ribbonGroupInvoices,
            this.ribbonGroupCompany,
            this.ribbonGroupPortal});
            this.ribbonTabHome.Text = "Trang chủ";
            //
            // ribbonGroupInvoices
            //
            this.ribbonGroupInvoices.DialogBoxLauncher = false;
            this.ribbonGroupInvoices.Items.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupContainer[] {
            this.ribbonTripleInvoices});
            this.ribbonGroupInvoices.TextLine1 = "Hóa đơn";
            //
            // ribbonTripleInvoices
            //
            this.ribbonTripleInvoices.Items.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupItem[] {
            this.ribbonBtnDownload,
            this.ribbonBtnArchive,
            this.ribbonBtnHistory});
            //
            // ribbonBtnDownload
            //
            this.ribbonBtnDownload.TextLine1 = "Tải hóa đơn";
            this.ribbonBtnDownload.Tag = HoaDonDienTu.Application.Navigation.ScreenId.Download;
            this.ribbonBtnDownload.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // ribbonBtnArchive
            //
            this.ribbonBtnArchive.TextLine1 = "Kho hóa đơn";
            this.ribbonBtnArchive.Tag = HoaDonDienTu.Application.Navigation.ScreenId.InvoiceArchive;
            this.ribbonBtnArchive.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // ribbonBtnHistory
            //
            this.ribbonBtnHistory.TextLine1 = "Lịch sử tải";
            this.ribbonBtnHistory.Tag = HoaDonDienTu.Application.Navigation.ScreenId.DownloadHistory;
            this.ribbonBtnHistory.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // ribbonGroupCompany
            //
            this.ribbonGroupCompany.DialogBoxLauncher = false;
            this.ribbonGroupCompany.Items.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupContainer[] {
            this.ribbonTripleCompany});
            this.ribbonGroupCompany.TextLine1 = "Doanh nghiệp";
            //
            // ribbonTripleCompany
            //
            this.ribbonTripleCompany.Items.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupItem[] {
            this.ribbonBtnSwitchCompany,
            this.ribbonBtnCompanies});
            //
            // ribbonBtnSwitchCompany
            //
            this.ribbonBtnSwitchCompany.TextLine1 = "Đổi doanh nghiệp";
            this.ribbonBtnSwitchCompany.Click += new System.EventHandler(this.OnCompanySelectorClick);
            //
            // ribbonBtnCompanies
            //
            this.ribbonBtnCompanies.TextLine1 = "Danh sách MST";
            this.ribbonBtnCompanies.Tag = HoaDonDienTu.Application.Navigation.ScreenId.CompanyList;
            this.ribbonBtnCompanies.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // ribbonGroupPortal
            //
            this.ribbonGroupPortal.DialogBoxLauncher = false;
            this.ribbonGroupPortal.Items.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupContainer[] {
            this.ribbonTriplePortal});
            this.ribbonGroupPortal.TextLine1 = "Cổng Thuế";
            //
            // ribbonTriplePortal
            //
            this.ribbonTriplePortal.Items.AddRange(new ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupItem[] {
            this.ribbonBtnPortalLogin});
            //
            // ribbonBtnPortalLogin
            //
            this.ribbonBtnPortalLogin.TextLine1 = "Đăng nhập";
            this.ribbonBtnPortalLogin.Click += new System.EventHandler(this.OnPortalLoginClick);
            //
            // pnlContent
            //
            this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContent.Name = "pnlContent";
            this.pnlContent.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelClient;
            this.pnlContent.TabIndex = 0;
            //
            // pnlHeader
            //
            this.pnlHeader.Controls.Add(this.btnPortalLogin);
            this.pnlHeader.Controls.Add(this.lblPortalSession);
            this.pnlHeader.Controls.Add(this.lblLicenseStatus);
            this.pnlHeader.Controls.Add(this.lblTaxCode);
            this.pnlHeader.Controls.Add(this.btnCompanyContext);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(900, 64);
            this.pnlHeader.TabIndex = 1;
            //
            // btnCompanyContext
            //
            this.btnCompanyContext.Location = new System.Drawing.Point(12, 6);
            this.btnCompanyContext.Name = "btnCompanyContext";
            this.btnCompanyContext.Size = new System.Drawing.Size(280, 30);
            this.btnCompanyContext.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnCompanyContext.TabIndex = 0;
            this.btnCompanyContext.Values.Text = "-";
            this.btnCompanyContext.Click += new System.EventHandler(this.OnCompanySelectorClick);
            //
            // lblTaxCode
            //
            this.lblTaxCode.Location = new System.Drawing.Point(10, 38);
            this.lblTaxCode.Name = "lblTaxCode";
            this.lblTaxCode.Size = new System.Drawing.Size(100, 20);
            this.lblTaxCode.TabIndex = 1;
            this.lblTaxCode.Values.Text = "MST";
            //
            // lblLicenseStatus
            //
            this.lblLicenseStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLicenseStatus.Location = new System.Drawing.Point(540, 20);
            this.lblLicenseStatus.Name = "lblLicenseStatus";
            this.lblLicenseStatus.Size = new System.Drawing.Size(130, 20);
            this.lblLicenseStatus.TabIndex = 2;
            this.lblLicenseStatus.Values.Text = "";
            //
            // lblPortalSession
            //
            this.lblPortalSession.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblPortalSession.Location = new System.Drawing.Point(680, 20);
            this.lblPortalSession.Name = "lblPortalSession";
            this.lblPortalSession.Size = new System.Drawing.Size(110, 20);
            this.lblPortalSession.TabIndex = 3;
            this.lblPortalSession.Values.Text = "Chưa đăng nhập";
            //
            // btnPortalLogin
            //
            this.btnPortalLogin.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPortalLogin.Location = new System.Drawing.Point(800, 15);
            this.btnPortalLogin.Name = "btnPortalLogin";
            this.btnPortalLogin.Size = new System.Drawing.Size(90, 30);
            this.btnPortalLogin.TabIndex = 4;
            this.btnPortalLogin.Values.Text = "Đăng nhập";
            this.btnPortalLogin.Click += new System.EventHandler(this.OnPortalLoginClick);
            //
            // pnlSidebar
            //
            this.pnlSidebar.Controls.Add(this.navSettings);
            this.pnlSidebar.Controls.Add(this.navCompanies);
            this.pnlSidebar.Controls.Add(this.navDownloadHistory);
            this.pnlSidebar.Controls.Add(this.navInvoiceArchive);
            this.pnlSidebar.Controls.Add(this.navDownload);
            this.pnlSidebar.Controls.Add(this.navDashboard);
            this.pnlSidebar.Controls.Add(this.lblApplicationTagline);
            this.pnlSidebar.Controls.Add(this.lblApplicationName);
            this.pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlSidebar.Name = "pnlSidebar";
            this.pnlSidebar.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlSidebar.Size = new System.Drawing.Size(200, 500);
            this.pnlSidebar.StateCommon.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.pnlSidebar.TabIndex = 2;
            //
            // lblApplicationName
            //
            this.lblApplicationName.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblApplicationName.Name = "lblApplicationName";
            this.lblApplicationName.Size = new System.Drawing.Size(200, 34);
            this.lblApplicationName.StateCommon.ShortText.Color1 = System.Drawing.Color.White;
            this.lblApplicationName.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
            this.lblApplicationName.TabIndex = 0;
            this.lblApplicationName.Values.Text = "HÓA ĐƠN ĐIỆN TỬ";
            //
            // lblApplicationTagline
            //
            this.lblApplicationTagline.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblApplicationTagline.Name = "lblApplicationTagline";
            this.lblApplicationTagline.Size = new System.Drawing.Size(200, 28);
            this.lblApplicationTagline.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(190, 200, 210);
            this.lblApplicationTagline.TabIndex = 1;
            this.lblApplicationTagline.Values.Text = "Kho chứng từ doanh nghiệp";
            //
            // navDashboard
            //
            this.navDashboard.Dock = System.Windows.Forms.DockStyle.Top;
            this.navDashboard.Name = "navDashboard";
            this.navDashboard.Size = new System.Drawing.Size(200, 40);
            this.navDashboard.ButtonStyle = ComponentFactory.Krypton.Toolkit.ButtonStyle.Standalone;
            this.navDashboard.StateCommon.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navDashboard.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navDashboard.StateCommon.Border.DrawBorders = ComponentFactory.Krypton.Toolkit.PaletteDrawBorders.None;
            this.navDashboard.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navDashboard.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.navDashboard.StateCommon.Content.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Near;
            this.navDashboard.StateCheckedNormal.Back.Color1 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navDashboard.StateCheckedNormal.Back.Color2 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navDashboard.StateCheckedNormal.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navDashboard.TabIndex = 2;
            this.navDashboard.Tag = HoaDonDienTu.Application.Navigation.ScreenId.Dashboard;
            this.navDashboard.Values.Text = "Tổng quan";
            this.navDashboard.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // navDownload
            //
            this.navDownload.Dock = System.Windows.Forms.DockStyle.Top;
            this.navDownload.Name = "navDownload";
            this.navDownload.Size = new System.Drawing.Size(200, 40);
            this.navDownload.ButtonStyle = ComponentFactory.Krypton.Toolkit.ButtonStyle.Standalone;
            this.navDownload.StateCommon.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navDownload.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navDownload.StateCommon.Border.DrawBorders = ComponentFactory.Krypton.Toolkit.PaletteDrawBorders.None;
            this.navDownload.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navDownload.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.navDownload.StateCommon.Content.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Near;
            this.navDownload.StateCheckedNormal.Back.Color1 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navDownload.StateCheckedNormal.Back.Color2 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navDownload.StateCheckedNormal.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navDownload.TabIndex = 3;
            this.navDownload.Tag = HoaDonDienTu.Application.Navigation.ScreenId.Download;
            this.navDownload.Values.Text = "Tải hóa đơn";
            this.navDownload.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // navInvoiceArchive
            //
            this.navInvoiceArchive.Dock = System.Windows.Forms.DockStyle.Top;
            this.navInvoiceArchive.Name = "navInvoiceArchive";
            this.navInvoiceArchive.Size = new System.Drawing.Size(200, 40);
            this.navInvoiceArchive.ButtonStyle = ComponentFactory.Krypton.Toolkit.ButtonStyle.Standalone;
            this.navInvoiceArchive.StateCommon.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navInvoiceArchive.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navInvoiceArchive.StateCommon.Border.DrawBorders = ComponentFactory.Krypton.Toolkit.PaletteDrawBorders.None;
            this.navInvoiceArchive.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navInvoiceArchive.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.navInvoiceArchive.StateCommon.Content.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Near;
            this.navInvoiceArchive.StateCheckedNormal.Back.Color1 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navInvoiceArchive.StateCheckedNormal.Back.Color2 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navInvoiceArchive.StateCheckedNormal.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navInvoiceArchive.TabIndex = 4;
            this.navInvoiceArchive.Tag = HoaDonDienTu.Application.Navigation.ScreenId.InvoiceArchive;
            this.navInvoiceArchive.Values.Text = "Kho hóa đơn";
            this.navInvoiceArchive.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // navDownloadHistory
            //
            this.navDownloadHistory.Dock = System.Windows.Forms.DockStyle.Top;
            this.navDownloadHistory.Name = "navDownloadHistory";
            this.navDownloadHistory.Size = new System.Drawing.Size(200, 40);
            this.navDownloadHistory.ButtonStyle = ComponentFactory.Krypton.Toolkit.ButtonStyle.Standalone;
            this.navDownloadHistory.StateCommon.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navDownloadHistory.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navDownloadHistory.StateCommon.Border.DrawBorders = ComponentFactory.Krypton.Toolkit.PaletteDrawBorders.None;
            this.navDownloadHistory.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navDownloadHistory.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.navDownloadHistory.StateCommon.Content.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Near;
            this.navDownloadHistory.StateCheckedNormal.Back.Color1 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navDownloadHistory.StateCheckedNormal.Back.Color2 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navDownloadHistory.StateCheckedNormal.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navDownloadHistory.TabIndex = 5;
            this.navDownloadHistory.Tag = HoaDonDienTu.Application.Navigation.ScreenId.DownloadHistory;
            this.navDownloadHistory.Values.Text = "Lịch sử tải";
            this.navDownloadHistory.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // navCompanies
            //
            this.navCompanies.Dock = System.Windows.Forms.DockStyle.Top;
            this.navCompanies.Name = "navCompanies";
            this.navCompanies.Size = new System.Drawing.Size(200, 40);
            this.navCompanies.ButtonStyle = ComponentFactory.Krypton.Toolkit.ButtonStyle.Standalone;
            this.navCompanies.StateCommon.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navCompanies.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navCompanies.StateCommon.Border.DrawBorders = ComponentFactory.Krypton.Toolkit.PaletteDrawBorders.None;
            this.navCompanies.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navCompanies.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.navCompanies.StateCommon.Content.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Near;
            this.navCompanies.StateCheckedNormal.Back.Color1 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navCompanies.StateCheckedNormal.Back.Color2 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navCompanies.StateCheckedNormal.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navCompanies.TabIndex = 6;
            this.navCompanies.Tag = HoaDonDienTu.Application.Navigation.ScreenId.CompanyList;
            this.navCompanies.Values.Text = "Doanh nghiệp";
            this.navCompanies.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // navSettings
            //
            this.navSettings.Dock = System.Windows.Forms.DockStyle.Top;
            this.navSettings.Name = "navSettings";
            this.navSettings.Size = new System.Drawing.Size(200, 40);
            this.navSettings.ButtonStyle = ComponentFactory.Krypton.Toolkit.ButtonStyle.Standalone;
            this.navSettings.StateCommon.Back.Color1 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navSettings.StateCommon.Back.Color2 = System.Drawing.Color.FromArgb(47, 62, 78);
            this.navSettings.StateCommon.Border.DrawBorders = ComponentFactory.Krypton.Toolkit.PaletteDrawBorders.None;
            this.navSettings.StateCommon.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navSettings.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.navSettings.StateCommon.Content.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Near;
            this.navSettings.StateCheckedNormal.Back.Color1 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navSettings.StateCheckedNormal.Back.Color2 = System.Drawing.Color.FromArgb(74, 92, 112);
            this.navSettings.StateCheckedNormal.Content.ShortText.Color1 = System.Drawing.Color.White;
            this.navSettings.TabIndex = 7;
            this.navSettings.Tag = HoaDonDienTu.Application.Navigation.ScreenId.Settings;
            this.navSettings.Values.Text = "Cài đặt";
            this.navSettings.Click += new System.EventHandler(this.OnNavigationClick);
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblOperationStatus,
            this.progressOperation,
            this.lblApplicationVersion});
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.TabIndex = 3;
            //
            // lblOperationStatus
            //
            this.lblOperationStatus.Name = "lblOperationStatus";
            this.lblOperationStatus.Spring = true;
            this.lblOperationStatus.Text = "Sẵn sàng";
            this.lblOperationStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // progressOperation
            //
            this.progressOperation.Name = "progressOperation";
            this.progressOperation.Size = new System.Drawing.Size(160, 16);
            this.progressOperation.Visible = false;
            //
            // lblApplicationVersion
            //
            this.lblApplicationVersion.Name = "lblApplicationVersion";
            this.lblApplicationVersion.Text = "Phiên bản 1.0";
            //
            // MainShellForm
            //
            this.ClientSize = new System.Drawing.Size(1100, 720);
            this.Controls.Add(this.pnlContent);
            this.Controls.Add(this.pnlHeader);
            this.Controls.Add(this.pnlSidebar);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.ribbon);
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.Name = "MainShellForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hóa đơn điện tử";
            ((System.ComponentModel.ISupportInitialize)(this.ribbon)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlContent)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).EndInit();
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlSidebar)).EndInit();
            this.pnlSidebar.ResumeLayout(false);
            this.pnlSidebar.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonManager kryptonManager;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbon ribbon;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonTab ribbonTabHome;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup ribbonGroupInvoices;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupTriple ribbonTripleInvoices;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton ribbonBtnDownload;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton ribbonBtnArchive;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton ribbonBtnHistory;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup ribbonGroupCompany;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupTriple ribbonTripleCompany;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton ribbonBtnSwitchCompany;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton ribbonBtnCompanies;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroup ribbonGroupPortal;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupTriple ribbonTriplePortal;
        private ComponentFactory.Krypton.Ribbon.KryptonRibbonGroupButton ribbonBtnPortalLogin;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlContent;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCompanyContext;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTaxCode;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblLicenseStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblPortalSession;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnPortalLogin;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlSidebar;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblApplicationName;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblApplicationTagline;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton navDashboard;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton navDownload;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton navInvoiceArchive;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton navDownloadHistory;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton navCompanies;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton navSettings;
        private ComponentFactory.Krypton.Toolkit.KryptonStatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblOperationStatus;
        private System.Windows.Forms.ToolStripProgressBar progressOperation;
        private System.Windows.Forms.ToolStripStatusLabel lblApplicationVersion;
    }
}
