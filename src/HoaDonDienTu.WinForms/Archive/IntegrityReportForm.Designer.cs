namespace HoaDonDienTu.WinForms.Archive
{
    partial class IntegrityReportForm
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
            this.lblSummary = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.list = new System.Windows.Forms.ListView();
            this.colKind = new System.Windows.Forms.ColumnHeader();
            this.colInvoice = new System.Windows.Forms.ColumnHeader();
            this.colVersion = new System.Windows.Forms.ColumnHeader();
            this.colPath = new System.Windows.Forms.ColumnHeader();
            this.lblNote = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.btnClose = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnCleanTemps = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.SuspendLayout();
            //
            // lblSummary
            //
            this.lblSummary.AutoSize = false;
            this.lblSummary.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblSummary.Location = new System.Drawing.Point(16, 12);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(748, 40);
            this.lblSummary.Text = "";
            //
            // list
            //
            this.list.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.colKind, this.colInvoice, this.colVersion, this.colPath });
            this.list.FullRowSelect = true;
            this.list.GridLines = true;
            this.list.Location = new System.Drawing.Point(16, 58);
            this.list.MultiSelect = true;
            this.list.Name = "list";
            this.list.Size = new System.Drawing.Size(748, 330);
            this.list.TabIndex = 0;
            this.list.UseCompatibleStateImageBehavior = false;
            this.list.View = System.Windows.Forms.View.Details;
            //
            // colKind
            //
            this.colKind.Text = "Vấn đề";
            this.colKind.Width = 210;
            //
            // colInvoice
            //
            this.colInvoice.Text = "Hóa đơn";
            this.colInvoice.Width = 140;
            //
            // colVersion
            //
            this.colVersion.Text = "Phiên bản";
            this.colVersion.Width = 70;
            //
            // colPath
            //
            this.colPath.Text = "File (trong thư mục kho)";
            this.colPath.Width = 310;
            //
            // lblNote
            //
            this.lblNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblNote.AutoSize = false;
            this.lblNote.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblNote.Location = new System.Drawing.Point(16, 396);
            this.lblNote.Name = "lblNote";
            this.lblNote.Size = new System.Drawing.Size(520, 50);
            this.lblNote.Text = "";
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(664, 400);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 34);
            this.btnClose.TabIndex = 1;
            this.btnClose.Values.Text = "Đóng";
            this.btnClose.Click += new System.EventHandler(this.OnCloseClick);
            //
            // btnCleanTemps
            //
            this.btnCleanTemps.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCleanTemps.Location = new System.Drawing.Point(544, 400);
            this.btnCleanTemps.Name = "btnCleanTemps";
            this.btnCleanTemps.Size = new System.Drawing.Size(112, 34);
            this.btnCleanTemps.TabIndex = 2;
            this.btnCleanTemps.Values.Text = "Xóa file tạm";
            this.btnCleanTemps.Visible = false;
            this.btnCleanTemps.Click += new System.EventHandler(this.OnCleanTempsClick);
            //
            // IntegrityReportForm
            //
            this.ClientSize = new System.Drawing.Size(780, 456);
            this.Controls.Add(this.lblSummary);
            this.Controls.Add(this.list);
            this.Controls.Add(this.lblNote);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnCleanTemps);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(640, 360);
            this.Name = "IntegrityReportForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Kết quả kiểm tra file của kho";
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblSummary;
        private System.Windows.Forms.ListView list;
        private System.Windows.Forms.ColumnHeader colKind;
        private System.Windows.Forms.ColumnHeader colInvoice;
        private System.Windows.Forms.ColumnHeader colVersion;
        private System.Windows.Forms.ColumnHeader colPath;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblNote;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnClose;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCleanTemps;
    }
}
