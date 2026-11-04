namespace HoaDonDienTu.WinForms.Detail
{
    partial class InvoiceDetailControl
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
            this.btnView = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnExportZip = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnBack = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblBadge = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lnkBreadcrumb = new ComponentFactory.Krypton.Toolkit.KryptonLinkLabel();
            this.pnlMessage = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.tlpBody = new System.Windows.Forms.TableLayoutPanel();
            this.flowLeft = new System.Windows.Forms.FlowLayoutPanel();
            this.flowRight = new System.Windows.Forms.FlowLayoutPanel();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblBadge);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Controls.Add(this.lnkBreadcrumb);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(1000, 104);
            this.pnlTitle.TabIndex = 3;
            //
            // lnkBreadcrumb
            //
            this.lnkBreadcrumb.Location = new System.Drawing.Point(14, 4);
            this.lnkBreadcrumb.Name = "lnkBreadcrumb";
            this.lnkBreadcrumb.Values.Text = "‹ Kho hóa đơn";
            this.lnkBreadcrumb.LinkClicked += new System.EventHandler(this.OnBackClick);
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 26);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Chi tiết hóa đơn";
            //
            // lblBadge
            //
            this.lblBadge.AutoSize = false;
            this.lblBadge.Location = new System.Drawing.Point(330, 36);
            this.lblBadge.Name = "lblBadge";
            this.lblBadge.Size = new System.Drawing.Size(240, 26);
            this.lblBadge.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(30, 110, 60);
            this.lblBadge.Values.Text = " ";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Location = new System.Drawing.Point(16, 68);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(620, 24);
            this.lblSubtitle.Values.Text = " ";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnView);
            this.pnlTitleActions.Controls.Add(this.btnExportZip);
            this.pnlTitleActions.Controls.Add(this.btnBack);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 30, 16, 30);
            this.pnlTitleActions.Size = new System.Drawing.Size(460, 104);
            //
            // btnBack
            //
            this.btnBack.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(100, 44);
            this.btnBack.Values.Text = "Quay lại";
            this.btnBack.Click += new System.EventHandler(this.OnBackClick);
            //
            // btnExportZip
            //
            this.btnExportZip.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnExportZip.Enabled = false;
            this.btnExportZip.Name = "btnExportZip";
            this.btnExportZip.Size = new System.Drawing.Size(130, 44);
            this.btnExportZip.Values.Text = "Xuất ZIP XML";
            this.btnExportZip.Click += new System.EventHandler(this.OnExportZipClick);
            //
            // btnView
            //
            this.btnView.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnView.Enabled = false;
            this.btnView.Name = "btnView";
            this.btnView.Size = new System.Drawing.Size(180, 44);
            this.btnView.Values.Text = "Xem chứng từ";
            this.btnView.Click += new System.EventHandler(this.OnViewClick);
            //
            // pnlMessage
            //
            this.pnlMessage.Controls.Add(this.lblMessage);
            this.pnlMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMessage.Name = "pnlMessage";
            this.pnlMessage.Padding = new System.Windows.Forms.Padding(16, 4, 16, 4);
            this.pnlMessage.Size = new System.Drawing.Size(1000, 28);
            this.pnlMessage.TabIndex = 2;
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Text = "";
            //
            // tlpBody
            //
            this.tlpBody.BackColor = System.Drawing.Color.Transparent;
            this.tlpBody.ColumnCount = 2;
            this.tlpBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 66F));
            this.tlpBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 34F));
            this.tlpBody.Controls.Add(this.flowLeft, 0, 0);
            this.tlpBody.Controls.Add(this.flowRight, 1, 0);
            this.tlpBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpBody.Name = "tlpBody";
            this.tlpBody.Padding = new System.Windows.Forms.Padding(16, 6, 16, 12);
            this.tlpBody.RowCount = 1;
            this.tlpBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpBody.TabIndex = 0;
            //
            // flowLeft / flowRight
            //
            this.flowLeft.AutoScroll = true;
            this.flowLeft.BackColor = System.Drawing.Color.Transparent;
            this.flowLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLeft.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLeft.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.flowLeft.Name = "flowLeft";
            this.flowLeft.WrapContents = false;
            this.flowLeft.Resize += new System.EventHandler(this.OnFlowResize);
            this.flowRight.AutoScroll = true;
            this.flowRight.BackColor = System.Drawing.Color.Transparent;
            this.flowRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowRight.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowRight.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.flowRight.Name = "flowRight";
            this.flowRight.WrapContents = false;
            this.flowRight.Resize += new System.EventHandler(this.OnFlowResize);
            //
            // InvoiceDetailControl
            //
            this.Controls.Add(this.tlpBody);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.pnlTitle);
            this.Name = "InvoiceDetailControl";
            this.Size = new System.Drawing.Size(1000, 640);
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnView;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnExportZip;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnBack;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblBadge;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLinkLabel lnkBreadcrumb;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private System.Windows.Forms.TableLayoutPanel tlpBody;
        private System.Windows.Forms.FlowLayoutPanel flowLeft;
        private System.Windows.Forms.FlowLayoutPanel flowRight;
    }
}
