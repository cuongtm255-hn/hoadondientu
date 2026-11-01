namespace HoaDonDienTu.WinForms.Settings
{
    partial class SettingsControl
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
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlLeft = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnSecAbout = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnSecLicense = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnSecDiagnostics = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnSecPortal = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnSecStorage = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnSecCompany = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlRight = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlCard = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblNote = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.tlpRows = new System.Windows.Forms.TableLayoutPanel();
            this.lblCap1 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblVal1 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCap2 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblVal2 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCap3 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblVal3 = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCardTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(900, 84);
            this.pnlTitle.TabIndex = 2;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Cài đặt";
            //
            // lblSubtitle
            //
            this.lblSubtitle.Location = new System.Drawing.Point(16, 48);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Values.Text = "Thông tin doanh nghiệp, lưu trữ, bản quyền và ứng dụng";
            //
            // pnlRight
            //
            this.pnlRight.Controls.Add(this.pnlCard);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Padding = new System.Windows.Forms.Padding(8, 0, 16, 8);
            this.pnlRight.TabIndex = 0;
            //
            // pnlCard
            //
            this.pnlCard.Controls.Add(this.lblNote);
            this.pnlCard.Controls.Add(this.tlpRows);
            this.pnlCard.Controls.Add(this.lblCardTitle);
            this.pnlCard.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCard.Name = "pnlCard";
            this.pnlCard.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
            this.pnlCard.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCard.Size = new System.Drawing.Size(560, 220);
            this.pnlCard.TabIndex = 0;
            //
            // lblCardTitle
            //
            this.lblCardTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCardTitle.Name = "lblCardTitle";
            this.lblCardTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardTitle.Values.Text = "";
            //
            // tlpRows
            //
            this.tlpRows.BackColor = System.Drawing.Color.Transparent;
            this.tlpRows.ColumnCount = 2;
            this.tlpRows.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tlpRows.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRows.Controls.Add(this.lblCap1, 0, 0);
            this.tlpRows.Controls.Add(this.lblVal1, 1, 0);
            this.tlpRows.Controls.Add(this.lblCap2, 0, 1);
            this.tlpRows.Controls.Add(this.lblVal2, 1, 1);
            this.tlpRows.Controls.Add(this.lblCap3, 0, 2);
            this.tlpRows.Controls.Add(this.lblVal3, 1, 2);
            this.tlpRows.Dock = System.Windows.Forms.DockStyle.Top;
            this.tlpRows.Name = "tlpRows";
            this.tlpRows.RowCount = 3;
            this.tlpRows.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpRows.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpRows.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpRows.Size = new System.Drawing.Size(536, 90);
            this.tlpRows.TabIndex = 1;
            //
            // lblCap1 .. lblVal3
            //
            this.lblCap1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCap1.Name = "lblCap1";
            this.lblCap1.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCap1.Values.Text = "";
            this.lblVal1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblVal1.Name = "lblVal1";
            this.lblVal1.Values.Text = "";
            this.lblCap2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCap2.Name = "lblCap2";
            this.lblCap2.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCap2.Values.Text = "";
            this.lblVal2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblVal2.Name = "lblVal2";
            this.lblVal2.Values.Text = "";
            this.lblCap3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCap3.Name = "lblCap3";
            this.lblCap3.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCap3.Values.Text = "";
            this.lblVal3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblVal3.Name = "lblVal3";
            this.lblVal3.Values.Text = "";
            //
            // lblNote
            //
            this.lblNote.AutoSize = false;
            this.lblNote.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNote.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblNote.Name = "lblNote";
            this.lblNote.Text = "";
            //
            // pnlLeft
            //
            this.pnlLeft.Controls.Add(this.btnSecAbout);
            this.pnlLeft.Controls.Add(this.btnSecLicense);
            this.pnlLeft.Controls.Add(this.btnSecDiagnostics);
            this.pnlLeft.Controls.Add(this.btnSecPortal);
            this.pnlLeft.Controls.Add(this.btnSecStorage);
            this.pnlLeft.Controls.Add(this.btnSecCompany);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(16, 0, 8, 8);
            this.pnlLeft.Size = new System.Drawing.Size(290, 460);
            this.pnlLeft.TabIndex = 1;
            //
            // section buttons
            //
            this.btnSecCompany.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnSecCompany.Name = "btnSecCompany";
            this.btnSecCompany.Size = new System.Drawing.Size(266, 38);
            this.btnSecCompany.TabIndex = 0;
            this.btnSecCompany.Values.Text = "Doanh nghiệp đang hoạt động";
            this.btnSecCompany.Click += new System.EventHandler(this.OnSectionClick);
            this.btnSecStorage.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnSecStorage.Name = "btnSecStorage";
            this.btnSecStorage.Size = new System.Drawing.Size(266, 38);
            this.btnSecStorage.TabIndex = 1;
            this.btnSecStorage.Values.Text = "Lưu trữ và sao lưu";
            this.btnSecStorage.Click += new System.EventHandler(this.OnSectionClick);
            this.btnSecPortal.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnSecPortal.Name = "btnSecPortal";
            this.btnSecPortal.Size = new System.Drawing.Size(266, 38);
            this.btnSecPortal.TabIndex = 2;
            this.btnSecPortal.Values.Text = "Kết nối cổng Thuế";
            this.btnSecPortal.Click += new System.EventHandler(this.OnSectionClick);
            this.btnSecDiagnostics.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnSecDiagnostics.Name = "btnSecDiagnostics";
            this.btnSecDiagnostics.Size = new System.Drawing.Size(266, 38);
            this.btnSecDiagnostics.TabIndex = 3;
            this.btnSecDiagnostics.Values.Text = "Nhật ký và chẩn đoán";
            this.btnSecDiagnostics.Click += new System.EventHandler(this.OnSectionClick);
            this.btnSecLicense.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnSecLicense.Name = "btnSecLicense";
            this.btnSecLicense.Size = new System.Drawing.Size(266, 38);
            this.btnSecLicense.TabIndex = 4;
            this.btnSecLicense.Values.Text = "Bản quyền ứng dụng →";
            this.btnSecLicense.Click += new System.EventHandler(this.OnSectionClick);
            this.btnSecAbout.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnSecAbout.Name = "btnSecAbout";
            this.btnSecAbout.Size = new System.Drawing.Size(266, 38);
            this.btnSecAbout.TabIndex = 5;
            this.btnSecAbout.Values.Text = "Thông tin ứng dụng";
            this.btnSecAbout.Click += new System.EventHandler(this.OnSectionClick);
            //
            // SettingsControl
            //
            this.Controls.Add(this.pnlRight);
            this.Controls.Add(this.pnlLeft);
            this.Controls.Add(this.pnlTitle);
            this.Name = "SettingsControl";
            this.Size = new System.Drawing.Size(900, 560);
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlLeft;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSecAbout;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSecLicense;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSecDiagnostics;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSecPortal;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSecStorage;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSecCompany;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlRight;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCard;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblNote;
        private System.Windows.Forms.TableLayoutPanel tlpRows;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCap1;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblVal1;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCap2;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblVal2;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCap3;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblVal3;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardTitle;
    }
}
