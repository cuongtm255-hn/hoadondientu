namespace HoaDonDienTu.WinForms.Companies
{
    partial class CompanyEditControl
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
            this.btnPrimary = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnCancel = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lnkBreadcrumb = new ComponentFactory.Krypton.Toolkit.KryptonLinkLabel();
            this.pnlRight = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlSteps = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSteps = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.lblStepsHeader = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlWarning = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblWarning = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlLeft = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlCardChecks = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblCheck5 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCheck4 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCheck3 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCheck2 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCheck1 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCardChecks = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlCardFolder = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblFolderNote = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.btnBrowse = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.txtStoragePath = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblStoragePath = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCardFolder = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlCardInfo = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblNameHint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtDisplayName = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblDisplayName = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTaxHint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtTaxCode = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblTaxCode = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCardInfo = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
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
            this.pnlTitle.TabIndex = 3;
            //
            // lnkBreadcrumb
            //
            this.lnkBreadcrumb.Location = new System.Drawing.Point(14, 6);
            this.lnkBreadcrumb.Name = "lnkBreadcrumb";
            this.lnkBreadcrumb.Values.Text = "Quản lý doanh nghiệp";
            this.lnkBreadcrumb.LinkClicked += new System.EventHandler(this.OnCancelClick);
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 28);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Thêm doanh nghiệp";
            //
            // lblSubtitle
            //
            this.lblSubtitle.Location = new System.Drawing.Point(16, 66);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Values.Text = "";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnCancel);
            this.pnlTitleActions.Controls.Add(this.btnPrimary);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 34, 16, 34);
            this.pnlTitleActions.Size = new System.Drawing.Size(360, 100);
            //
            // btnCancel
            //
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.Values.Text = "Hủy";
            this.btnCancel.Click += new System.EventHandler(this.OnCancelClick);
            //
            // btnPrimary
            //
            this.btnPrimary.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnPrimary.Enabled = false;
            this.btnPrimary.Name = "btnPrimary";
            this.btnPrimary.Size = new System.Drawing.Size(240, 32);
            this.btnPrimary.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnPrimary.Values.Text = "Tạo và chuyển sang MST này";
            this.btnPrimary.Click += new System.EventHandler(this.OnPrimaryClick);
            //
            // pnlRight
            //
            this.pnlRight.Controls.Add(this.pnlWarning);
            this.pnlRight.Controls.Add(this.pnlSteps);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Padding = new System.Windows.Forms.Padding(8, 0, 16, 8);
            this.pnlRight.Size = new System.Drawing.Size(300, 460);
            this.pnlRight.TabIndex = 1;
            //
            // pnlSteps
            //
            this.pnlSteps.Controls.Add(this.lblSteps);
            this.pnlSteps.Controls.Add(this.lblStepsHeader);
            this.pnlSteps.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlSteps.Name = "pnlSteps";
            this.pnlSteps.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlSteps.Padding = new System.Windows.Forms.Padding(12, 0, 12, 8);
            this.pnlSteps.Size = new System.Drawing.Size(276, 210);
            this.pnlSteps.TabIndex = 1;
            //
            // lblStepsHeader
            //
            this.lblStepsHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStepsHeader.Name = "lblStepsHeader";
            this.lblStepsHeader.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblStepsHeader.Values.Text = "Sau khi lưu";
            //
            // lblSteps
            //
            this.lblSteps.AutoSize = false;
            this.lblSteps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSteps.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSteps.Name = "lblSteps";
            this.lblSteps.Text = "";
            //
            // pnlWarning
            //
            this.pnlWarning.Controls.Add(this.lblWarning);
            this.pnlWarning.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlWarning.Name = "pnlWarning";
            this.pnlWarning.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
            this.pnlWarning.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlWarning.Size = new System.Drawing.Size(276, 96);
            this.pnlWarning.StateCommon.Color1 = System.Drawing.Color.FromArgb(253, 236, 234);
            this.pnlWarning.TabIndex = 0;
            //
            // lblWarning
            //
            this.lblWarning.AutoSize = false;
            this.lblWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblWarning.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblWarning.ForeColor = System.Drawing.Color.FromArgb(130, 30, 20);
            this.lblWarning.Name = "lblWarning";
            this.lblWarning.Text = "Không lưu tài khoản hoặc mật khẩu. Hồ sơ chỉ tạo kho dữ liệu; bạn vẫn phải đăng nhập thủ công và tự nhập CAPTCHA.";
            //
            // pnlLeft
            //
            this.pnlLeft.AutoScroll = true;
            this.pnlLeft.Controls.Add(this.lblMessage);
            this.pnlLeft.Controls.Add(this.pnlCardChecks);
            this.pnlLeft.Controls.Add(this.pnlCardFolder);
            this.pnlLeft.Controls.Add(this.pnlCardInfo);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(16, 0, 8, 8);
            this.pnlLeft.TabIndex = 0;
            //
            // pnlCardInfo
            //
            this.pnlCardInfo.Controls.Add(this.lblNameHint);
            this.pnlCardInfo.Controls.Add(this.txtDisplayName);
            this.pnlCardInfo.Controls.Add(this.lblDisplayName);
            this.pnlCardInfo.Controls.Add(this.lblTaxHint);
            this.pnlCardInfo.Controls.Add(this.txtTaxCode);
            this.pnlCardInfo.Controls.Add(this.lblTaxCode);
            this.pnlCardInfo.Controls.Add(this.lblCardInfo);
            this.pnlCardInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardInfo.Name = "pnlCardInfo";
            this.pnlCardInfo.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardInfo.Size = new System.Drawing.Size(560, 150);
            this.pnlCardInfo.TabIndex = 0;
            //
            // lblCardInfo
            //
            this.lblCardInfo.Location = new System.Drawing.Point(12, 8);
            this.lblCardInfo.Name = "lblCardInfo";
            this.lblCardInfo.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardInfo.Values.Text = "1. Thông tin doanh nghiệp";
            //
            // lblTaxCode
            //
            this.lblTaxCode.Location = new System.Drawing.Point(12, 38);
            this.lblTaxCode.Name = "lblTaxCode";
            this.lblTaxCode.Values.Text = "Mã số thuế";
            //
            // txtTaxCode
            //
            this.txtTaxCode.Location = new System.Drawing.Point(16, 62);
            this.txtTaxCode.Name = "txtTaxCode";
            this.txtTaxCode.Size = new System.Drawing.Size(240, 24);
            this.txtTaxCode.TextChanged += new System.EventHandler(this.OnTaxCodeTextChanged);
            //
            // lblTaxHint
            //
            this.lblTaxHint.Location = new System.Drawing.Point(12, 90);
            this.lblTaxHint.Name = "lblTaxHint";
            this.lblTaxHint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblTaxHint.Values.Text = "Dùng để kiểm tra phiên đăng nhập và cách ly kho";
            //
            // lblDisplayName
            //
            this.lblDisplayName.Location = new System.Drawing.Point(280, 38);
            this.lblDisplayName.Name = "lblDisplayName";
            this.lblDisplayName.Values.Text = "Tên hiển thị";
            //
            // txtDisplayName
            //
            this.txtDisplayName.Location = new System.Drawing.Point(284, 62);
            this.txtDisplayName.Name = "txtDisplayName";
            this.txtDisplayName.Size = new System.Drawing.Size(260, 24);
            this.txtDisplayName.TextChanged += new System.EventHandler(this.OnInputTextChanged);
            //
            // lblNameHint
            //
            this.lblNameHint.Location = new System.Drawing.Point(280, 90);
            this.lblNameHint.Name = "lblNameHint";
            this.lblNameHint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblNameHint.Values.Text = "Có thể cập nhật sau khi đăng nhập";
            //
            // pnlCardFolder
            //
            this.pnlCardFolder.Controls.Add(this.lblFolderNote);
            this.pnlCardFolder.Controls.Add(this.btnBrowse);
            this.pnlCardFolder.Controls.Add(this.txtStoragePath);
            this.pnlCardFolder.Controls.Add(this.lblStoragePath);
            this.pnlCardFolder.Controls.Add(this.lblCardFolder);
            this.pnlCardFolder.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardFolder.Name = "pnlCardFolder";
            this.pnlCardFolder.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardFolder.Size = new System.Drawing.Size(560, 150);
            this.pnlCardFolder.TabIndex = 1;
            //
            // lblCardFolder
            //
            this.lblCardFolder.Location = new System.Drawing.Point(12, 8);
            this.lblCardFolder.Name = "lblCardFolder";
            this.lblCardFolder.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardFolder.Values.Text = "2. Kho dữ liệu riêng";
            //
            // lblStoragePath
            //
            this.lblStoragePath.Location = new System.Drawing.Point(12, 38);
            this.lblStoragePath.Name = "lblStoragePath";
            this.lblStoragePath.Values.Text = "Thư mục kho";
            //
            // txtStoragePath
            //
            this.txtStoragePath.Location = new System.Drawing.Point(16, 62);
            this.txtStoragePath.Name = "txtStoragePath";
            this.txtStoragePath.Size = new System.Drawing.Size(410, 24);
            this.txtStoragePath.TextChanged += new System.EventHandler(this.OnInputTextChanged);
            //
            // btnBrowse
            //
            this.btnBrowse.Location = new System.Drawing.Point(436, 60);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(108, 28);
            this.btnBrowse.Values.Text = "Chọn thư mục";
            this.btnBrowse.Click += new System.EventHandler(this.OnBrowseClick);
            //
            // lblFolderNote
            //
            this.lblFolderNote.AutoSize = false;
            this.lblFolderNote.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblFolderNote.Location = new System.Drawing.Point(16, 98);
            this.lblFolderNote.Name = "lblFolderNote";
            this.lblFolderNote.Size = new System.Drawing.Size(528, 40);
            this.lblFolderNote.Text = "Thư mục chứa dữ liệu riêng của MST này: kho hóa đơn, XML, PDF cache, file xuất, bản sao lưu và log. Chọn thư mục trống hoặc kho đã có của đúng MST.";
            //
            // pnlCardChecks
            //
            this.pnlCardChecks.Controls.Add(this.lblCheck5);
            this.pnlCardChecks.Controls.Add(this.lblCheck4);
            this.pnlCardChecks.Controls.Add(this.lblCheck3);
            this.pnlCardChecks.Controls.Add(this.lblCheck2);
            this.pnlCardChecks.Controls.Add(this.lblCheck1);
            this.pnlCardChecks.Controls.Add(this.lblCardChecks);
            this.pnlCardChecks.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardChecks.Name = "pnlCardChecks";
            this.pnlCardChecks.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardChecks.Size = new System.Drawing.Size(560, 180);
            this.pnlCardChecks.TabIndex = 2;
            //
            // lblCardChecks
            //
            this.lblCardChecks.Location = new System.Drawing.Point(12, 8);
            this.lblCardChecks.Name = "lblCardChecks";
            this.lblCardChecks.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardChecks.Values.Text = "3. Kiểm tra trước khi lưu";
            //
            // lblCheck1
            //
            this.lblCheck1.Location = new System.Drawing.Point(16, 40);
            this.lblCheck1.Name = "lblCheck1";
            this.lblCheck1.Values.Text = "";
            //
            // lblCheck2
            //
            this.lblCheck2.Location = new System.Drawing.Point(16, 68);
            this.lblCheck2.Name = "lblCheck2";
            this.lblCheck2.Values.Text = "";
            //
            // lblCheck3
            //
            this.lblCheck3.Location = new System.Drawing.Point(16, 96);
            this.lblCheck3.Name = "lblCheck3";
            this.lblCheck3.Values.Text = "";
            //
            // lblCheck4
            //
            this.lblCheck4.Location = new System.Drawing.Point(16, 124);
            this.lblCheck4.Name = "lblCheck4";
            this.lblCheck4.Values.Text = "";
            //
            // lblCheck5
            //
            this.lblCheck5.Location = new System.Drawing.Point(16, 152);
            this.lblCheck5.Name = "lblCheck5";
            this.lblCheck5.Values.Text = "";
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblMessage.ForeColor = System.Drawing.Color.FromArgb(170, 40, 30);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(560, 32);
            this.lblMessage.Text = "";
            //
            // CompanyEditControl
            //
            this.Controls.Add(this.pnlLeft);
            this.Controls.Add(this.pnlRight);
            this.Controls.Add(this.pnlTitle);
            this.Name = "CompanyEditControl";
            this.Size = new System.Drawing.Size(900, 560);
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnPrimary;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCancel;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLinkLabel lnkBreadcrumb;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlRight;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlSteps;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblSteps;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStepsHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlWarning;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblWarning;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlLeft;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardChecks;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheck5;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheck4;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheck3;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheck2;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheck1;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardChecks;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardFolder;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblFolderNote;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnBrowse;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtStoragePath;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStoragePath;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardFolder;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardInfo;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblNameHint;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtDisplayName;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblDisplayName;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTaxHint;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtTaxCode;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTaxCode;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardInfo;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
    }
}
