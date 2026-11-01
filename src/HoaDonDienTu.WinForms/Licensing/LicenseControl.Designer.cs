namespace HoaDonDienTu.WinForms.Licensing
{
    partial class LicenseControl
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

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pnlTitle = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlTitleActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnBack = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lnkBreadcrumb = new ComponentFactory.Krypton.Toolkit.KryptonLinkLabel();
            this.pnlLeft = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlNote = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblNote = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlCardStatus = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblCardStatus = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblModeLetter = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblModeTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblModeSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCapDevice = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblDevice = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCapMachine = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblMachine = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCapType = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblType = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCapExpires = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblExpires = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCapLast = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblLast = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCapVersion = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblVersion = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlRight = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlCardAfter = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblAfter = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.lblCardAfter = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlCardSent = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSent = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.lblCardSent = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlCardActivate = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.tlpButtons = new System.Windows.Forms.TableLayoutPanel();
            this.pnlKey = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnDeactivate = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnValidate = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnActivate = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblKeyHint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtLicenseKey = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblKey = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCardActivate = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Controls.Add(this.lnkBreadcrumb);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(900, 100);
            this.pnlTitle.TabIndex = 2;
            //
            // lnkBreadcrumb
            //
            this.lnkBreadcrumb.Location = new System.Drawing.Point(14, 6);
            this.lnkBreadcrumb.Name = "lnkBreadcrumb";
            this.lnkBreadcrumb.Values.Text = "Cài đặt";
            this.lnkBreadcrumb.LinkClicked += new System.EventHandler(this.OnBackClick);
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 28);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Bản quyền ứng dụng";
            //
            // lblSubtitle
            //
            this.lblSubtitle.Location = new System.Drawing.Point(16, 66);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Values.Text = "Kích hoạt licence được nhà cung cấp cấp cho máy này";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnBack);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 34, 16, 34);
            this.pnlTitleActions.Size = new System.Drawing.Size(200, 100);
            //
            // btnBack
            //
            this.btnBack.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(170, 32);
            this.btnBack.Values.Text = "Quay lại cài đặt";
            this.btnBack.Click += new System.EventHandler(this.OnBackClick);
            //
            // pnlRight
            //
            this.pnlRight.Controls.Add(this.pnlCardAfter);
            this.pnlRight.Controls.Add(this.pnlCardSent);
            this.pnlRight.Controls.Add(this.pnlCardActivate);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Padding = new System.Windows.Forms.Padding(8, 0, 16, 8);
            this.pnlRight.TabIndex = 0;
            //
            // pnlCardActivate
            //
            this.pnlCardActivate.Controls.Add(this.lblMessage);
            this.pnlCardActivate.Controls.Add(this.tlpButtons);
            this.pnlCardActivate.Controls.Add(this.lblKeyHint);
            this.pnlCardActivate.Controls.Add(this.pnlKey);
            this.pnlCardActivate.Controls.Add(this.lblKey);
            this.pnlCardActivate.Controls.Add(this.lblCardActivate);
            this.pnlCardActivate.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardActivate.Name = "pnlCardActivate";
            this.pnlCardActivate.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
            this.pnlCardActivate.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardActivate.Size = new System.Drawing.Size(560, 214);
            this.pnlCardActivate.TabIndex = 0;
            //
            // lblCardActivate
            //
            this.lblCardActivate.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardActivate.Name = "lblCardActivate";
            this.lblCardActivate.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardActivate.Values.Text = "Kích hoạt licence";
            //
            // lblKey
            //
            this.lblKey.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblKey.Name = "lblKey";
            this.lblKey.Values.Text = "Licence key";
            //
            // pnlKey
            //
            this.pnlKey.Controls.Add(this.txtLicenseKey);
            this.pnlKey.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlKey.Name = "pnlKey";
            this.pnlKey.Padding = new System.Windows.Forms.Padding(4, 2, 4, 2);
            this.pnlKey.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlKey.Size = new System.Drawing.Size(536, 36);
            this.pnlKey.TabIndex = 1;
            //
            // txtLicenseKey
            //
            this.txtLicenseKey.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLicenseKey.MaxLength = 128;
            this.txtLicenseKey.Name = "txtLicenseKey";
            this.txtLicenseKey.TabIndex = 0;
            this.txtLicenseKey.TextChanged += new System.EventHandler(this.OnKeyTextChanged);
            //
            // lblKeyHint
            //
            this.lblKeyHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblKeyHint.Name = "lblKeyHint";
            this.lblKeyHint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblKeyHint.Values.Text = "Key được kiểm tra trực tuyến và gắn với máy này. Không gửi MST hoặc dữ liệu hóa đơn.";
            //
            // tlpButtons
            //
            this.tlpButtons.BackColor = System.Drawing.Color.Transparent;
            this.tlpButtons.ColumnCount = 4;
            this.tlpButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.tlpButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.tlpButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.tlpButtons.Controls.Add(this.btnDeactivate, 1, 0);
            this.tlpButtons.Controls.Add(this.btnValidate, 2, 0);
            this.tlpButtons.Controls.Add(this.btnActivate, 3, 0);
            this.tlpButtons.Dock = System.Windows.Forms.DockStyle.Top;
            this.tlpButtons.Name = "tlpButtons";
            this.tlpButtons.RowCount = 1;
            this.tlpButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpButtons.Size = new System.Drawing.Size(536, 44);
            this.tlpButtons.TabIndex = 3;
            //
            // btnActivate
            //
            this.btnActivate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnActivate.Enabled = false;
            this.btnActivate.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnActivate.Name = "btnActivate";
            this.btnActivate.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnActivate.TabIndex = 2;
            this.btnActivate.Values.Text = "Kích hoạt";
            this.btnActivate.Click += new System.EventHandler(this.OnActivateClick);
            //
            // btnValidate
            //
            this.btnValidate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnValidate.Enabled = false;
            this.btnValidate.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnValidate.Name = "btnValidate";
            this.btnValidate.TabIndex = 1;
            this.btnValidate.Values.Text = "Xác thực ngay";
            this.btnValidate.Click += new System.EventHandler(this.OnValidateClick);
            //
            // btnDeactivate
            //
            this.btnDeactivate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnDeactivate.Enabled = false;
            this.btnDeactivate.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnDeactivate.Name = "btnDeactivate";
            this.btnDeactivate.TabIndex = 0;
            this.btnDeactivate.Values.Text = "Hủy kích hoạt";
            this.btnDeactivate.Click += new System.EventHandler(this.OnDeactivateClick);
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Text = "";
            //
            // pnlCardSent
            //
            this.pnlCardSent.Controls.Add(this.lblSent);
            this.pnlCardSent.Controls.Add(this.lblCardSent);
            this.pnlCardSent.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardSent.Name = "pnlCardSent";
            this.pnlCardSent.Padding = new System.Windows.Forms.Padding(14, 38, 14, 8);
            this.pnlCardSent.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardSent.Size = new System.Drawing.Size(560, 130);
            this.pnlCardSent.TabIndex = 1;
            //
            // lblCardSent
            //
            this.lblCardSent.Location = new System.Drawing.Point(12, 8);
            this.lblCardSent.Name = "lblCardSent";
            this.lblCardSent.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardSent.Values.Text = "Dữ liệu gửi khi kích hoạt";
            //
            // lblSent
            //
            this.lblSent.AutoSize = false;
            this.lblSent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSent.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSent.Name = "lblSent";
            this.lblSent.Text = "Licence key: xác định quyền đã mua.\r\nMã máy đã băm: ràng buộc licence với thiết bị.\r\nPhiên bản ứng dụng: kiểm tra tương thích entitlement.\r\nKhông gửi: MST, danh sách khách hàng, hóa đơn, token Thuế, đường dẫn kho.";
            //
            // pnlCardAfter
            //
            this.pnlCardAfter.Controls.Add(this.lblAfter);
            this.pnlCardAfter.Controls.Add(this.lblCardAfter);
            this.pnlCardAfter.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardAfter.Name = "pnlCardAfter";
            this.pnlCardAfter.Padding = new System.Windows.Forms.Padding(14, 38, 14, 8);
            this.pnlCardAfter.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardAfter.Size = new System.Drawing.Size(560, 110);
            this.pnlCardAfter.TabIndex = 2;
            //
            // lblCardAfter
            //
            this.lblCardAfter.Location = new System.Drawing.Point(12, 8);
            this.lblCardAfter.Name = "lblCardAfter";
            this.lblCardAfter.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardAfter.Values.Text = "Sau khi kích hoạt";
            //
            // lblAfter
            //
            this.lblAfter.AutoSize = false;
            this.lblAfter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAfter.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblAfter.Name = "lblAfter";
            this.lblAfter.Text = "Ứng dụng kiểm tra chữ ký, lưu entitlement được bảo vệ theo máy và tự mở các chức năng được cấp phép mà không cần cài đặt lại. Khi máy chủ tạm thời không truy cập được, licence đã xác thực có thể tiếp tục hoạt động tối đa 30 ngày.";
            //
            // pnlLeft
            //
            this.pnlLeft.Controls.Add(this.pnlNote);
            this.pnlLeft.Controls.Add(this.pnlCardStatus);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(16, 0, 8, 8);
            this.pnlLeft.Size = new System.Drawing.Size(400, 460);
            this.pnlLeft.TabIndex = 1;
            //
            // pnlCardStatus
            //
            this.pnlCardStatus.Controls.Add(this.lblVersion);
            this.pnlCardStatus.Controls.Add(this.lblCapVersion);
            this.pnlCardStatus.Controls.Add(this.lblLast);
            this.pnlCardStatus.Controls.Add(this.lblCapLast);
            this.pnlCardStatus.Controls.Add(this.lblExpires);
            this.pnlCardStatus.Controls.Add(this.lblCapExpires);
            this.pnlCardStatus.Controls.Add(this.lblType);
            this.pnlCardStatus.Controls.Add(this.lblCapType);
            this.pnlCardStatus.Controls.Add(this.lblMachine);
            this.pnlCardStatus.Controls.Add(this.lblCapMachine);
            this.pnlCardStatus.Controls.Add(this.lblDevice);
            this.pnlCardStatus.Controls.Add(this.lblCapDevice);
            this.pnlCardStatus.Controls.Add(this.lblModeSubtitle);
            this.pnlCardStatus.Controls.Add(this.lblModeTitle);
            this.pnlCardStatus.Controls.Add(this.lblModeLetter);
            this.pnlCardStatus.Controls.Add(this.lblCardStatus);
            this.pnlCardStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardStatus.Name = "pnlCardStatus";
            this.pnlCardStatus.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardStatus.Size = new System.Drawing.Size(376, 300);
            this.pnlCardStatus.TabIndex = 0;
            //
            // lblCardStatus
            //
            this.lblCardStatus.Location = new System.Drawing.Point(12, 8);
            this.lblCardStatus.Name = "lblCardStatus";
            this.lblCardStatus.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardStatus.Values.Text = "Trạng thái hiện tại";
            //
            // lblModeLetter
            //
            this.lblModeLetter.Location = new System.Drawing.Point(14, 44);
            this.lblModeLetter.Name = "lblModeLetter";
            this.lblModeLetter.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblModeLetter.Values.Text = "S";
            //
            // lblModeTitle
            //
            this.lblModeTitle.Location = new System.Drawing.Point(50, 38);
            this.lblModeTitle.Name = "lblModeTitle";
            this.lblModeTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblModeTitle.Values.Text = "Chế độ chuẩn";
            //
            // lblModeSubtitle
            //
            this.lblModeSubtitle.Location = new System.Drawing.Point(50, 64);
            this.lblModeSubtitle.Name = "lblModeSubtitle";
            this.lblModeSubtitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblModeSubtitle.Values.Text = "";
            //
            // lblCapDevice / lblDevice
            //
            this.lblCapDevice.Location = new System.Drawing.Point(12, 100);
            this.lblCapDevice.Name = "lblCapDevice";
            this.lblCapDevice.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCapDevice.Values.Text = "Thiết bị";
            this.lblDevice.Location = new System.Drawing.Point(130, 100);
            this.lblDevice.Name = "lblDevice";
            this.lblDevice.Values.Text = "";
            //
            // lblCapMachine / lblMachine
            //
            this.lblCapMachine.Location = new System.Drawing.Point(12, 130);
            this.lblCapMachine.Name = "lblCapMachine";
            this.lblCapMachine.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCapMachine.Values.Text = "Mã máy";
            this.lblMachine.Location = new System.Drawing.Point(130, 130);
            this.lblMachine.Name = "lblMachine";
            this.lblMachine.Values.Text = "";
            //
            // lblCapType / lblType
            //
            this.lblCapType.Location = new System.Drawing.Point(12, 160);
            this.lblCapType.Name = "lblCapType";
            this.lblCapType.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCapType.Values.Text = "Loại licence";
            this.lblType.Location = new System.Drawing.Point(130, 160);
            this.lblType.Name = "lblType";
            this.lblType.Values.Text = "";
            //
            // lblCapExpires / lblExpires
            //
            this.lblCapExpires.Location = new System.Drawing.Point(12, 190);
            this.lblCapExpires.Name = "lblCapExpires";
            this.lblCapExpires.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCapExpires.Values.Text = "Hạn dùng";
            this.lblExpires.Location = new System.Drawing.Point(130, 190);
            this.lblExpires.Name = "lblExpires";
            this.lblExpires.Values.Text = "";
            //
            // lblCapLast / lblLast
            //
            this.lblCapLast.Location = new System.Drawing.Point(12, 220);
            this.lblCapLast.Name = "lblCapLast";
            this.lblCapLast.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCapLast.Values.Text = "Xác thực gần nhất";
            this.lblLast.Location = new System.Drawing.Point(130, 220);
            this.lblLast.Name = "lblLast";
            this.lblLast.Values.Text = "";
            //
            // lblCapVersion / lblVersion
            //
            this.lblCapVersion.Location = new System.Drawing.Point(12, 250);
            this.lblCapVersion.Name = "lblCapVersion";
            this.lblCapVersion.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCapVersion.Values.Text = "Phiên bản";
            this.lblVersion.Location = new System.Drawing.Point(130, 250);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Values.Text = "";
            //
            // pnlNote
            //
            this.pnlNote.Controls.Add(this.lblNote);
            this.pnlNote.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlNote.Name = "pnlNote";
            this.pnlNote.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
            this.pnlNote.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlNote.Size = new System.Drawing.Size(376, 84);
            this.pnlNote.StateCommon.Color1 = System.Drawing.Color.FromArgb(255, 246, 225);
            this.pnlNote.TabIndex = 1;
            //
            // lblNote
            //
            this.lblNote.AutoSize = false;
            this.lblNote.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNote.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblNote.ForeColor = System.Drawing.Color.FromArgb(120, 80, 10);
            this.lblNote.Name = "lblNote";
            this.lblNote.Text = "";
            //
            // LicenseControl
            //
            this.Controls.Add(this.pnlRight);
            this.Controls.Add(this.pnlLeft);
            this.Controls.Add(this.pnlTitle);
            this.Name = "LicenseControl";
            this.Size = new System.Drawing.Size(900, 560);
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnBack;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLinkLabel lnkBreadcrumb;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlLeft;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlNote;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblNote;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblModeLetter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblModeTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblModeSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCapDevice;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblDevice;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCapMachine;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblMachine;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCapType;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblType;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCapExpires;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblExpires;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCapLast;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblLast;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCapVersion;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblVersion;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlRight;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardAfter;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblAfter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardAfter;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardSent;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblSent;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardSent;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardActivate;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private System.Windows.Forms.TableLayoutPanel tlpButtons;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlKey;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnDeactivate;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnValidate;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnActivate;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblKeyHint;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtLicenseKey;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblKey;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardActivate;
    }
}
