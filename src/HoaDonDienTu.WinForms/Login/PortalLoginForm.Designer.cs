namespace HoaDonDienTu.WinForms.Login
{
    partial class PortalLoginForm
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
            this.pnlCredentials = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSimulation = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlNotice = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSecurityNotice = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.btnManual = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnRetry = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnLogin = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.progressLogin = new System.Windows.Forms.ProgressBar();
            this.lblStateMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.txtCaptcha = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.btnRefreshCaptcha = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.picCaptcha = new System.Windows.Forms.PictureBox();
            this.lblCaptchaCaption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtPassword = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblPasswordCaption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTaxCodeValue = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTaxCodeCaption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlBrowserHost = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblFooter = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnCancel = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlHeader = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblExpectedTaxCode = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCompanyName = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            ((System.ComponentModel.ISupportInitialize)(this.pnlCredentials)).BeginInit();
            this.pnlCredentials.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlNotice)).BeginInit();
            this.pnlNotice.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picCaptcha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlBrowserHost)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlActions)).BeginInit();
            this.pnlActions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).BeginInit();
            this.pnlHeader.SuspendLayout();
            this.SuspendLayout();
            //
            // pnlCredentials
            //
            this.pnlCredentials.Controls.Add(this.lblSimulation);
            this.pnlCredentials.Controls.Add(this.pnlNotice);
            this.pnlCredentials.Controls.Add(this.btnManual);
            this.pnlCredentials.Controls.Add(this.btnRetry);
            this.pnlCredentials.Controls.Add(this.btnLogin);
            this.pnlCredentials.Controls.Add(this.progressLogin);
            this.pnlCredentials.Controls.Add(this.lblStateMessage);
            this.pnlCredentials.Controls.Add(this.txtCaptcha);
            this.pnlCredentials.Controls.Add(this.btnRefreshCaptcha);
            this.pnlCredentials.Controls.Add(this.picCaptcha);
            this.pnlCredentials.Controls.Add(this.lblCaptchaCaption);
            this.pnlCredentials.Controls.Add(this.txtPassword);
            this.pnlCredentials.Controls.Add(this.lblPasswordCaption);
            this.pnlCredentials.Controls.Add(this.lblTaxCodeValue);
            this.pnlCredentials.Controls.Add(this.lblTaxCodeCaption);
            this.pnlCredentials.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlCredentials.Name = "pnlCredentials";
            this.pnlCredentials.TabIndex = 0;
            //
            // lblTaxCodeCaption
            //
            this.lblTaxCodeCaption.Location = new System.Drawing.Point(28, 16);
            this.lblTaxCodeCaption.Name = "lblTaxCodeCaption";
            this.lblTaxCodeCaption.Size = new System.Drawing.Size(320, 22);
            this.lblTaxCodeCaption.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(100, 110, 120);
            this.lblTaxCodeCaption.TabIndex = 0;
            this.lblTaxCodeCaption.Values.Text = "Mã số thuế doanh nghiệp đang chọn";
            //
            // lblTaxCodeValue
            //
            this.lblTaxCodeValue.Location = new System.Drawing.Point(28, 38);
            this.lblTaxCodeValue.Name = "lblTaxCodeValue";
            this.lblTaxCodeValue.Size = new System.Drawing.Size(504, 40);
            this.lblTaxCodeValue.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Bold);
            this.lblTaxCodeValue.TabIndex = 1;
            this.lblTaxCodeValue.Values.Text = "";
            //
            // lblPasswordCaption
            //
            this.lblPasswordCaption.Location = new System.Drawing.Point(28, 92);
            this.lblPasswordCaption.Name = "lblPasswordCaption";
            this.lblPasswordCaption.Size = new System.Drawing.Size(200, 22);
            this.lblPasswordCaption.TabIndex = 2;
            this.lblPasswordCaption.Values.Text = "Mật khẩu";
            //
            // txtPassword
            //
            this.txtPassword.Enabled = false;
            this.txtPassword.Location = new System.Drawing.Point(28, 116);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(504, 27);
            this.txtPassword.TabIndex = 3;
            this.txtPassword.UseSystemPasswordChar = true;
            //
            // lblCaptchaCaption
            //
            this.lblCaptchaCaption.Location = new System.Drawing.Point(28, 156);
            this.lblCaptchaCaption.Name = "lblCaptchaCaption";
            this.lblCaptchaCaption.Size = new System.Drawing.Size(300, 22);
            this.lblCaptchaCaption.TabIndex = 4;
            this.lblCaptchaCaption.Values.Text = "Mã CAPTCHA (đọc từ ảnh bên dưới)";
            //
            // picCaptcha
            //
            this.picCaptcha.BackColor = System.Drawing.Color.White;
            this.picCaptcha.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picCaptcha.Location = new System.Drawing.Point(28, 180);
            this.picCaptcha.Name = "picCaptcha";
            this.picCaptcha.Size = new System.Drawing.Size(302, 62);
            this.picCaptcha.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.picCaptcha.TabIndex = 5;
            this.picCaptcha.TabStop = false;
            //
            // btnRefreshCaptcha
            //
            this.btnRefreshCaptcha.Enabled = false;
            this.btnRefreshCaptcha.Location = new System.Drawing.Point(342, 180);
            this.btnRefreshCaptcha.Name = "btnRefreshCaptcha";
            this.btnRefreshCaptcha.Size = new System.Drawing.Size(120, 34);
            this.btnRefreshCaptcha.TabIndex = 6;
            this.btnRefreshCaptcha.Values.Text = "Đổi mã khác";
            this.btnRefreshCaptcha.Click += new System.EventHandler(this.OnRefreshCaptchaClick);
            //
            // txtCaptcha
            //
            this.txtCaptcha.Enabled = false;
            this.txtCaptcha.Location = new System.Drawing.Point(28, 252);
            this.txtCaptcha.Name = "txtCaptcha";
            this.txtCaptcha.Size = new System.Drawing.Size(302, 27);
            this.txtCaptcha.TabIndex = 7;
            //
            // lblStateMessage
            //
            this.lblStateMessage.AutoSize = false;
            this.lblStateMessage.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblStateMessage.Location = new System.Drawing.Point(28, 292);
            this.lblStateMessage.Name = "lblStateMessage";
            this.lblStateMessage.Size = new System.Drawing.Size(504, 56);
            this.lblStateMessage.Text = "";
            //
            // progressLogin
            //
            this.progressLogin.Location = new System.Drawing.Point(28, 352);
            this.progressLogin.MarqueeAnimationSpeed = 30;
            this.progressLogin.Name = "progressLogin";
            this.progressLogin.Size = new System.Drawing.Size(504, 6);
            this.progressLogin.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressLogin.Visible = false;
            //
            // btnLogin
            //
            this.btnLogin.Enabled = false;
            this.btnLogin.Location = new System.Drawing.Point(28, 368);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(504, 40);
            this.btnLogin.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnLogin.TabIndex = 8;
            this.btnLogin.Values.Text = "Đăng nhập";
            this.btnLogin.Click += new System.EventHandler(this.OnLoginClick);
            //
            // btnRetry
            //
            this.btnRetry.Location = new System.Drawing.Point(28, 418);
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.Size = new System.Drawing.Size(240, 34);
            this.btnRetry.TabIndex = 9;
            this.btnRetry.Values.Text = "Thử lại";
            this.btnRetry.Visible = false;
            this.btnRetry.Click += new System.EventHandler(this.OnRetryClick);
            //
            // btnManual
            //
            this.btnManual.Location = new System.Drawing.Point(280, 418);
            this.btnManual.Name = "btnManual";
            this.btnManual.Size = new System.Drawing.Size(252, 34);
            this.btnManual.TabIndex = 10;
            this.btnManual.Values.Text = "Đăng nhập trên trang cổng Thuế...";
            this.btnManual.Click += new System.EventHandler(this.OnManualClick);
            //
            // lblSimulation
            //
            this.lblSimulation.AutoSize = false;
            this.lblSimulation.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblSimulation.ForeColor = System.Drawing.Color.FromArgb(170, 40, 30);
            this.lblSimulation.Location = new System.Drawing.Point(28, 462);
            this.lblSimulation.Name = "lblSimulation";
            this.lblSimulation.Size = new System.Drawing.Size(504, 36);
            this.lblSimulation.Text = "";
            this.lblSimulation.Visible = false;
            //
            // pnlNotice
            //
            this.pnlNotice.Controls.Add(this.lblSecurityNotice);
            this.pnlNotice.Location = new System.Drawing.Point(28, 506);
            this.pnlNotice.Name = "pnlNotice";
            this.pnlNotice.Padding = new System.Windows.Forms.Padding(10);
            this.pnlNotice.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlNotice.Size = new System.Drawing.Size(504, 58);
            this.pnlNotice.StateCommon.Color1 = System.Drawing.Color.FromArgb(255, 247, 230);
            this.pnlNotice.TabIndex = 11;
            //
            // lblSecurityNotice
            //
            this.lblSecurityNotice.AutoSize = false;
            this.lblSecurityNotice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSecurityNotice.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSecurityNotice.ForeColor = System.Drawing.Color.FromArgb(110, 70, 10);
            this.lblSecurityNotice.Name = "lblSecurityNotice";
            this.lblSecurityNotice.Text = "Mật khẩu chỉ được gửi tới cổng Thuế để đăng nhập. Ứng dụng không lưu mật khẩu và không ghi vào nhật ký. Mã CAPTCHA do bạn tự nhập.";
            //
            // pnlBrowserHost
            //
            this.pnlBrowserHost.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBrowserHost.Name = "pnlBrowserHost";
            this.pnlBrowserHost.Size = new System.Drawing.Size(560, 2);
            this.pnlBrowserHost.TabIndex = 1;
            //
            // pnlActions
            //
            this.pnlActions.Controls.Add(this.lblFooter);
            this.pnlActions.Controls.Add(this.btnCancel);
            this.pnlActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlActions.Name = "pnlActions";
            this.pnlActions.Size = new System.Drawing.Size(560, 48);
            this.pnlActions.TabIndex = 2;
            //
            // lblFooter
            //
            this.lblFooter.Location = new System.Drawing.Point(12, 12);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(400, 22);
            this.lblFooter.TabIndex = 0;
            this.lblFooter.Values.Text = "";
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(450, 8);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 32);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Values.Text = "Hủy";
            this.btnCancel.Click += new System.EventHandler(this.OnCancelClick);
            //
            // pnlHeader
            //
            this.pnlHeader.Controls.Add(this.lblExpectedTaxCode);
            this.pnlHeader.Controls.Add(this.lblCompanyName);
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlHeader.Size = new System.Drawing.Size(560, 64);
            this.pnlHeader.StateCommon.Color1 = System.Drawing.Color.FromArgb(40, 54, 70);
            this.pnlHeader.TabIndex = 3;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(16, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(300, 40);
            this.lblTitle.StateCommon.ShortText.Color1 = System.Drawing.Color.White;
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Values.Text = "ĐĂNG NHẬP CỔNG THUẾ";
            //
            // lblCompanyName
            //
            this.lblCompanyName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCompanyName.Location = new System.Drawing.Point(330, 10);
            this.lblCompanyName.Name = "lblCompanyName";
            this.lblCompanyName.Size = new System.Drawing.Size(220, 24);
            this.lblCompanyName.StateCommon.ShortText.Color1 = System.Drawing.Color.White;
            this.lblCompanyName.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblCompanyName.StateCommon.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Far;
            this.lblCompanyName.TabIndex = 1;
            this.lblCompanyName.Values.Text = "";
            //
            // lblExpectedTaxCode
            //
            this.lblExpectedTaxCode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblExpectedTaxCode.Location = new System.Drawing.Point(330, 36);
            this.lblExpectedTaxCode.Name = "lblExpectedTaxCode";
            this.lblExpectedTaxCode.Size = new System.Drawing.Size(220, 22);
            this.lblExpectedTaxCode.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(200, 210, 220);
            this.lblExpectedTaxCode.StateCommon.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Far;
            this.lblExpectedTaxCode.TabIndex = 2;
            this.lblExpectedTaxCode.Values.Text = "";
            //
            // PortalLoginForm
            //
            this.AcceptButton = this.btnLogin;
            this.ClientSize = new System.Drawing.Size(560, 690);
            this.Controls.Add(this.pnlCredentials);
            this.Controls.Add(this.pnlBrowserHost);
            this.Controls.Add(this.pnlActions);
            this.Controls.Add(this.pnlHeader);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(576, 560);
            this.Name = "PortalLoginForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Đăng nhập cổng Thuế";
            ((System.ComponentModel.ISupportInitialize)(this.pnlCredentials)).EndInit();
            this.pnlCredentials.ResumeLayout(false);
            this.pnlCredentials.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlNotice)).EndInit();
            this.pnlNotice.ResumeLayout(false);
            this.pnlNotice.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picCaptcha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlBrowserHost)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlActions)).EndInit();
            this.pnlActions.ResumeLayout(false);
            this.pnlActions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).EndInit();
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCredentials;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTaxCodeCaption;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTaxCodeValue;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblPasswordCaption;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtPassword;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCaptchaCaption;
        private System.Windows.Forms.PictureBox picCaptcha;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnRefreshCaptcha;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtCaptcha;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblStateMessage;
        private System.Windows.Forms.ProgressBar progressLogin;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnLogin;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnRetry;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnManual;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblSimulation;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlNotice;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblSecurityNotice;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlBrowserHost;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlActions;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblFooter;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCancel;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblExpectedTaxCode;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCompanyName;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
    }
}
