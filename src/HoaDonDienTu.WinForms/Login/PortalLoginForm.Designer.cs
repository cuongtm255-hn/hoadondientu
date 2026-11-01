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
            this.pnlBrowserHost = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlInstruction = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnRetry = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.progressLogin = new System.Windows.Forms.ProgressBar();
            this.lblStateMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlNotice = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSecurityNotice = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.lblInstructions = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.lblStep = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblFooter = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnClearSession = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnCancel = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlHeader = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblExpectedTaxCode = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCompanyName = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            ((System.ComponentModel.ISupportInitialize)(this.pnlBrowserHost)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlInstruction)).BeginInit();
            this.pnlInstruction.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlNotice)).BeginInit();
            this.pnlNotice.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlActions)).BeginInit();
            this.pnlActions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).BeginInit();
            this.pnlHeader.SuspendLayout();
            this.SuspendLayout();
            //
            // pnlBrowserHost
            //
            this.pnlBrowserHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBrowserHost.Name = "pnlBrowserHost";
            this.pnlBrowserHost.Padding = new System.Windows.Forms.Padding(8);
            this.pnlBrowserHost.TabIndex = 0;
            //
            // pnlInstruction
            //
            this.pnlInstruction.Controls.Add(this.btnRetry);
            this.pnlInstruction.Controls.Add(this.progressLogin);
            this.pnlInstruction.Controls.Add(this.lblStateMessage);
            this.pnlInstruction.Controls.Add(this.pnlNotice);
            this.pnlInstruction.Controls.Add(this.lblInstructions);
            this.pnlInstruction.Controls.Add(this.lblStep);
            this.pnlInstruction.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlInstruction.Name = "pnlInstruction";
            this.pnlInstruction.Padding = new System.Windows.Forms.Padding(16);
            this.pnlInstruction.Size = new System.Drawing.Size(340, 500);
            this.pnlInstruction.TabIndex = 1;
            //
            // lblStep
            //
            this.lblStep.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStep.Name = "lblStep";
            this.lblStep.Size = new System.Drawing.Size(308, 36);
            this.lblStep.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStep.TabIndex = 0;
            this.lblStep.Values.Text = "Đăng nhập";
            //
            // lblInstructions
            //
            this.lblInstructions.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblInstructions.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(308, 130);
            this.lblInstructions.Text = "1. Nhập tài khoản và mật khẩu trên trang cổng Thuế.\r\n2. Tự nhập mã CAPTCHA.\r\n3. Chọn Đăng nhập.\r\n4. Ứng dụng sẽ tự nhận phiên đăng nhập thành công.";
            //
            // pnlNotice
            //
            this.pnlNotice.Controls.Add(this.lblSecurityNotice);
            this.pnlNotice.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlNotice.Name = "pnlNotice";
            this.pnlNotice.Padding = new System.Windows.Forms.Padding(10);
            this.pnlNotice.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlNotice.Size = new System.Drawing.Size(308, 64);
            this.pnlNotice.StateCommon.Color1 = System.Drawing.Color.FromArgb(255, 247, 230);
            this.pnlNotice.TabIndex = 2;
            //
            // lblSecurityNotice
            //
            this.lblSecurityNotice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSecurityNotice.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSecurityNotice.ForeColor = System.Drawing.Color.FromArgb(110, 70, 10);
            this.lblSecurityNotice.Name = "lblSecurityNotice";
            this.lblSecurityNotice.Text = "Ứng dụng không lưu mật khẩu hoặc tự giải CAPTCHA.";
            //
            // lblStateMessage
            //
            this.lblStateMessage.AutoSize = false;
            this.lblStateMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStateMessage.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblStateMessage.Name = "lblStateMessage";
            this.lblStateMessage.Size = new System.Drawing.Size(308, 84);
            this.lblStateMessage.Text = "";
            //
            // progressLogin
            //
            this.progressLogin.Dock = System.Windows.Forms.DockStyle.Top;
            this.progressLogin.MarqueeAnimationSpeed = 30;
            this.progressLogin.Name = "progressLogin";
            this.progressLogin.Size = new System.Drawing.Size(308, 8);
            this.progressLogin.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressLogin.Visible = false;
            //
            // btnRetry
            //
            this.btnRetry.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.Size = new System.Drawing.Size(308, 34);
            this.btnRetry.TabIndex = 5;
            this.btnRetry.Values.Text = "Thử lại";
            this.btnRetry.Visible = false;
            this.btnRetry.Click += new System.EventHandler(this.OnRetryClick);
            //
            // pnlActions
            //
            this.pnlActions.Controls.Add(this.lblFooter);
            this.pnlActions.Controls.Add(this.btnClearSession);
            this.pnlActions.Controls.Add(this.btnCancel);
            this.pnlActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlActions.Name = "pnlActions";
            this.pnlActions.Size = new System.Drawing.Size(1000, 48);
            this.pnlActions.TabIndex = 2;
            //
            // lblFooter
            //
            this.lblFooter.Location = new System.Drawing.Point(12, 12);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(400, 22);
            this.lblFooter.TabIndex = 0;
            this.lblFooter.Values.Text = "Kết nối TLS 1.2 · Token chỉ lưu trong bộ nhớ";
            //
            // btnClearSession
            //
            this.btnClearSession.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearSession.Location = new System.Drawing.Point(740, 8);
            this.btnClearSession.Name = "btnClearSession";
            this.btnClearSession.Size = new System.Drawing.Size(140, 32);
            this.btnClearSession.TabIndex = 1;
            this.btnClearSession.Values.Text = "Xóa phiên trình duyệt";
            this.btnClearSession.Click += new System.EventHandler(this.OnClearSessionClick);
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(890, 8);
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
            this.pnlHeader.Size = new System.Drawing.Size(1000, 64);
            this.pnlHeader.StateCommon.Color1 = System.Drawing.Color.FromArgb(40, 54, 70);
            this.pnlHeader.TabIndex = 3;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(16, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(360, 40);
            this.lblTitle.StateCommon.ShortText.Color1 = System.Drawing.Color.White;
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Values.Text = "ĐĂNG NHẬP CỔNG THUẾ";
            //
            // lblCompanyName
            //
            this.lblCompanyName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCompanyName.Location = new System.Drawing.Point(620, 10);
            this.lblCompanyName.Name = "lblCompanyName";
            this.lblCompanyName.Size = new System.Drawing.Size(370, 24);
            this.lblCompanyName.StateCommon.ShortText.Color1 = System.Drawing.Color.White;
            this.lblCompanyName.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblCompanyName.StateCommon.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Far;
            this.lblCompanyName.TabIndex = 1;
            this.lblCompanyName.Values.Text = "";
            //
            // lblExpectedTaxCode
            //
            this.lblExpectedTaxCode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblExpectedTaxCode.Location = new System.Drawing.Point(620, 36);
            this.lblExpectedTaxCode.Name = "lblExpectedTaxCode";
            this.lblExpectedTaxCode.Size = new System.Drawing.Size(370, 22);
            this.lblExpectedTaxCode.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(200, 210, 220);
            this.lblExpectedTaxCode.StateCommon.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Far;
            this.lblExpectedTaxCode.TabIndex = 2;
            this.lblExpectedTaxCode.Values.Text = "";
            //
            // PortalLoginForm
            //
            this.ClientSize = new System.Drawing.Size(1000, 640);
            this.Controls.Add(this.pnlBrowserHost);
            this.Controls.Add(this.pnlInstruction);
            this.Controls.Add(this.pnlActions);
            this.Controls.Add(this.pnlHeader);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(820, 520);
            this.Name = "PortalLoginForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Đăng nhập cổng Thuế";
            ((System.ComponentModel.ISupportInitialize)(this.pnlBrowserHost)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pnlInstruction)).EndInit();
            this.pnlInstruction.ResumeLayout(false);
            this.pnlInstruction.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlNotice)).EndInit();
            this.pnlNotice.ResumeLayout(false);
            this.pnlNotice.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlActions)).EndInit();
            this.pnlActions.ResumeLayout(false);
            this.pnlActions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pnlHeader)).EndInit();
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlBrowserHost;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlInstruction;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnRetry;
        private System.Windows.Forms.ProgressBar progressLogin;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblStateMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlNotice;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblSecurityNotice;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblInstructions;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStep;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlActions;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblFooter;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnClearSession;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCancel;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblExpectedTaxCode;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCompanyName;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
    }
}
