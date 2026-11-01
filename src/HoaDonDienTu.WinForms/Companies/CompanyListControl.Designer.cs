namespace HoaDonDienTu.WinForms.Companies
{
    partial class CompanyListControl
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
            this.ctxRow = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnuOpenFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuRename = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuUnlink = new System.Windows.Forms.ToolStripMenuItem();
            this.gridCompanies = new ComponentFactory.Krypton.Toolkit.KryptonDataGridView();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTaxCode = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLastOpened = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colInvoices = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAction = new System.Windows.Forms.DataGridViewButtonColumn();
            this.pnlFooter = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSummary = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlSearch = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnSearch = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.cboStatusFilter = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.txtSearch = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblSearchCaption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.tblStats = new System.Windows.Forms.TableLayoutPanel();
            this.pnlStat1 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat1Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat1Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat1Hint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat2 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat2Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat2Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat2Hint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat3 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat3Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat3Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat3Hint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat4 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat4Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat4Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat4Hint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlBanner = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblBanner = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlTitle = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlTitleActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnAddCompany = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            ((System.ComponentModel.ISupportInitialize)(this.gridCompanies)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatusFilter)).BeginInit();
            this.ctxRow.SuspendLayout();
            this.tblStats.SuspendLayout();
            this.SuspendLayout();
            //
            // ctxRow
            //
            this.ctxRow.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.mnuOpenFolder, this.mnuRename, this.mnuUnlink });
            this.ctxRow.Name = "ctxRow";
            //
            // mnuOpenFolder
            //
            this.mnuOpenFolder.Name = "mnuOpenFolder";
            this.mnuOpenFolder.Text = "Mở thư mục kho";
            this.mnuOpenFolder.Click += new System.EventHandler(this.OnMenuOpenFolder);
            //
            // mnuRename
            //
            this.mnuRename.Name = "mnuRename";
            this.mnuRename.Text = "Thông tin / đổi tên / liên kết lại thư mục";
            this.mnuRename.Click += new System.EventHandler(this.OnMenuRename);
            //
            // mnuUnlink
            //
            this.mnuUnlink.Name = "mnuUnlink";
            this.mnuUnlink.Text = "Ngừng quản lý";
            this.mnuUnlink.Click += new System.EventHandler(this.OnMenuUnlink);
            //
            // gridCompanies
            //
            this.gridCompanies.AllowUserToAddRows = false;
            this.gridCompanies.AllowUserToDeleteRows = false;
            this.gridCompanies.AllowUserToResizeRows = false;
            this.gridCompanies.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridCompanies.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colName, this.colTaxCode, this.colLastOpened, this.colInvoices, this.colSize, this.colStatus, this.colAction });
            this.gridCompanies.ContextMenuStrip = this.ctxRow;
            this.gridCompanies.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCompanies.MultiSelect = false;
            this.gridCompanies.Name = "gridCompanies";
            this.gridCompanies.ReadOnly = true;
            this.gridCompanies.RowHeadersVisible = false;
            this.gridCompanies.RowTemplate.Height = 48;
            this.gridCompanies.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCompanies.TabIndex = 0;
            this.gridCompanies.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnGridCellContentClick);
            this.gridCompanies.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.OnGridCellMouseDown);
            //
            // colName
            //
            this.colName.DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { WrapMode = System.Windows.Forms.DataGridViewTriState.True };
            this.colName.FillWeight = 34F;
            this.colName.HeaderText = "Doanh nghiệp";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            //
            // colTaxCode
            //
            this.colTaxCode.FillWeight = 14F;
            this.colTaxCode.HeaderText = "MST";
            this.colTaxCode.Name = "colTaxCode";
            this.colTaxCode.ReadOnly = true;
            //
            // colLastOpened
            //
            this.colLastOpened.FillWeight = 15F;
            this.colLastOpened.HeaderText = "Mở gần nhất";
            this.colLastOpened.Name = "colLastOpened";
            this.colLastOpened.ReadOnly = true;
            //
            // colInvoices
            //
            this.colInvoices.DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight };
            this.colInvoices.FillWeight = 9F;
            this.colInvoices.HeaderText = "Hóa đơn";
            this.colInvoices.Name = "colInvoices";
            this.colInvoices.ReadOnly = true;
            //
            // colSize
            //
            this.colSize.DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight };
            this.colSize.FillWeight = 9F;
            this.colSize.HeaderText = "Dung lượng";
            this.colSize.Name = "colSize";
            this.colSize.ReadOnly = true;
            //
            // colStatus
            //
            this.colStatus.FillWeight = 13F;
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            //
            // colAction
            //
            this.colAction.FillWeight = 12F;
            this.colAction.HeaderText = "Thao tác";
            this.colAction.Name = "colAction";
            this.colAction.ReadOnly = true;
            //
            // pnlFooter
            //
            this.pnlFooter.Controls.Add(this.lblSummary);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Name = "pnlFooter";
            this.pnlFooter.Size = new System.Drawing.Size(900, 34);
            this.pnlFooter.TabIndex = 1;
            //
            // lblSummary
            //
            this.lblSummary.Location = new System.Drawing.Point(12, 6);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Values.Text = "";
            //
            // pnlSearch
            //
            this.pnlSearch.Controls.Add(this.lblSearchCaption);
            this.pnlSearch.Controls.Add(this.btnSearch);
            this.pnlSearch.Controls.Add(this.cboStatusFilter);
            this.pnlSearch.Controls.Add(this.txtSearch);
            this.pnlSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlSearch.Name = "pnlSearch";
            this.pnlSearch.Size = new System.Drawing.Size(900, 52);
            this.pnlSearch.TabIndex = 2;
            //
            // lblSearchCaption
            //
            this.lblSearchCaption.Location = new System.Drawing.Point(12, 16);
            this.lblSearchCaption.Name = "lblSearchCaption";
            this.lblSearchCaption.Values.Text = "Tìm theo tên hoặc MST:";
            //
            // txtSearch
            //
            this.txtSearch.Location = new System.Drawing.Point(196, 14);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(280, 24);
            this.txtSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnSearchKeyDown);
            //
            // cboStatusFilter
            //
            this.cboStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStatusFilter.Location = new System.Drawing.Point(486, 14);
            this.cboStatusFilter.Name = "cboStatusFilter";
            this.cboStatusFilter.Size = new System.Drawing.Size(150, 24);
            //
            // btnSearch
            //
            this.btnSearch.Location = new System.Drawing.Point(646, 12);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(90, 28);
            this.btnSearch.Values.Text = "Tìm kiếm";
            this.btnSearch.Click += new System.EventHandler(this.OnSearchClick);
            //
            // tblStats
            //
            this.tblStats.ColumnCount = 4;
            this.tblStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tblStats.Controls.Add(this.pnlStat1, 0, 0);
            this.tblStats.Controls.Add(this.pnlStat2, 1, 0);
            this.tblStats.Controls.Add(this.pnlStat3, 2, 0);
            this.tblStats.Controls.Add(this.pnlStat4, 3, 0);
            this.tblStats.Dock = System.Windows.Forms.DockStyle.Top;
            this.tblStats.Name = "tblStats";
            this.tblStats.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.tblStats.RowCount = 1;
            this.tblStats.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblStats.Size = new System.Drawing.Size(900, 100);
            this.tblStats.TabIndex = 3;
            //
            // pnlStat1
            //
            this.pnlStat1.Controls.Add(this.lblStat1Hint);
            this.pnlStat1.Controls.Add(this.lblStat1Value);
            this.pnlStat1.Controls.Add(this.lblStat1Caption);
            this.pnlStat1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat1.Margin = new System.Windows.Forms.Padding(4);
            this.pnlStat1.Name = "pnlStat1";
            this.pnlStat1.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            //
            // lblStat1Caption
            //
            this.lblStat1Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Caption.Name = "lblStat1Caption";
            this.lblStat1Caption.Values.Text = "Doanh nghiệp đang quản lý";
            //
            // lblStat1Value
            //
            this.lblStat1Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Value.Name = "lblStat1Value";
            this.lblStat1Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat1Value.Values.Text = "0";
            //
            // lblStat1Hint
            //
            this.lblStat1Hint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Hint.Name = "lblStat1Hint";
            this.lblStat1Hint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblStat1Hint.Values.Text = "";
            //
            // pnlStat2
            //
            this.pnlStat2.Controls.Add(this.lblStat2Hint);
            this.pnlStat2.Controls.Add(this.lblStat2Value);
            this.pnlStat2.Controls.Add(this.lblStat2Caption);
            this.pnlStat2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat2.Margin = new System.Windows.Forms.Padding(4);
            this.pnlStat2.Name = "pnlStat2";
            this.pnlStat2.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            //
            // lblStat2Caption
            //
            this.lblStat2Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Caption.Name = "lblStat2Caption";
            this.lblStat2Caption.Values.Text = "Cần kiểm tra";
            //
            // lblStat2Value
            //
            this.lblStat2Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Value.Name = "lblStat2Value";
            this.lblStat2Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat2Value.Values.Text = "0";
            //
            // lblStat2Hint
            //
            this.lblStat2Hint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Hint.Name = "lblStat2Hint";
            this.lblStat2Hint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblStat2Hint.Values.Text = "Mất thư mục kho hoặc kho không khớp";
            //
            // pnlStat3
            //
            this.pnlStat3.Controls.Add(this.lblStat3Hint);
            this.pnlStat3.Controls.Add(this.lblStat3Value);
            this.pnlStat3.Controls.Add(this.lblStat3Caption);
            this.pnlStat3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat3.Margin = new System.Windows.Forms.Padding(4);
            this.pnlStat3.Name = "pnlStat3";
            this.pnlStat3.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            //
            // lblStat3Caption
            //
            this.lblStat3Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Caption.Name = "lblStat3Caption";
            this.lblStat3Caption.Values.Text = "Tổng dung lượng";
            //
            // lblStat3Value
            //
            this.lblStat3Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Value.Name = "lblStat3Value";
            this.lblStat3Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat3Value.Values.Text = "—";
            //
            // lblStat3Hint
            //
            this.lblStat3Hint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Hint.Name = "lblStat3Hint";
            this.lblStat3Hint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblStat3Hint.Values.Text = "Các kho được cách ly";
            //
            // pnlStat4
            //
            this.pnlStat4.Controls.Add(this.lblStat4Hint);
            this.pnlStat4.Controls.Add(this.lblStat4Value);
            this.pnlStat4.Controls.Add(this.lblStat4Caption);
            this.pnlStat4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat4.Margin = new System.Windows.Forms.Padding(4);
            this.pnlStat4.Name = "pnlStat4";
            this.pnlStat4.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            //
            // lblStat4Caption
            //
            this.lblStat4Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Caption.Name = "lblStat4Caption";
            this.lblStat4Caption.Values.Text = "MST đang hoạt động";
            //
            // lblStat4Value
            //
            this.lblStat4Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Value.Name = "lblStat4Value";
            this.lblStat4Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
            this.lblStat4Value.Values.Text = "Chưa chọn";
            //
            // lblStat4Hint
            //
            this.lblStat4Hint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Hint.Name = "lblStat4Hint";
            this.lblStat4Hint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblStat4Hint.Values.Text = "";
            //
            // pnlBanner
            //
            this.pnlBanner.Controls.Add(this.lblBanner);
            this.pnlBanner.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlBanner.Name = "pnlBanner";
            this.pnlBanner.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlBanner.Size = new System.Drawing.Size(900, 34);
            this.pnlBanner.StateCommon.Color1 = System.Drawing.Color.FromArgb(232, 245, 236);
            this.pnlBanner.TabIndex = 4;
            //
            // lblBanner
            //
            this.lblBanner.Location = new System.Drawing.Point(12, 6);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.StateCommon.ShortText.Color1 = System.Drawing.Color.FromArgb(30, 100, 60);
            this.lblBanner.Values.Text = "";
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(900, 76);
            this.pnlTitle.TabIndex = 5;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(16, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Quản lý doanh nghiệp";
            //
            // lblSubtitle
            //
            this.lblSubtitle.Location = new System.Drawing.Point(18, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Values.Text = "Chọn một MST để làm việc; mỗi doanh nghiệp sử dụng kho dữ liệu riêng";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnAddCompany);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 20, 16, 20);
            this.pnlTitleActions.Size = new System.Drawing.Size(200, 76);
            //
            // btnAddCompany
            //
            this.btnAddCompany.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAddCompany.Name = "btnAddCompany";
            this.btnAddCompany.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnAddCompany.Values.Text = "Thêm doanh nghiệp";
            this.btnAddCompany.Click += new System.EventHandler(this.OnAddClick);
            //
            // CompanyListControl
            //
            this.Controls.Add(this.gridCompanies);
            this.Controls.Add(this.pnlFooter);
            this.Controls.Add(this.pnlSearch);
            this.Controls.Add(this.tblStats);
            this.Controls.Add(this.pnlBanner);
            this.Controls.Add(this.pnlTitle);
            this.Name = "CompanyListControl";
            this.Size = new System.Drawing.Size(900, 560);
            ((System.ComponentModel.ISupportInitialize)(this.gridCompanies)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatusFilter)).EndInit();
            this.ctxRow.ResumeLayout(false);
            this.tblStats.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip ctxRow;
        private System.Windows.Forms.ToolStripMenuItem mnuOpenFolder;
        private System.Windows.Forms.ToolStripMenuItem mnuRename;
        private System.Windows.Forms.ToolStripMenuItem mnuUnlink;
        private ComponentFactory.Krypton.Toolkit.KryptonDataGridView gridCompanies;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTaxCode;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLastOpened;
        private System.Windows.Forms.DataGridViewTextBoxColumn colInvoices;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewButtonColumn colAction;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlFooter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSummary;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlSearch;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSearch;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboStatusFilter;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtSearch;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSearchCaption;
        private System.Windows.Forms.TableLayoutPanel tblStats;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat1;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Hint;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat2;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Hint;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat3;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Hint;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat4;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Hint;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlBanner;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblBanner;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnAddCompany;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
    }
}
