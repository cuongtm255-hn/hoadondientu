namespace HoaDonDienTu.WinForms.Archive
{
    partial class ArchiveControl
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
            System.Windows.Forms.DataGridViewCellStyle rightStyle = new System.Windows.Forms.DataGridViewCellStyle();
            this.pnlTitle = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlTitleActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnCheckFiles = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnExportExcel = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnExportZip = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlFilter = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.tlpFilter = new System.Windows.Forms.TableLayoutPanel();
            this.btnPurchase = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.btnSold = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.txtQuick = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.cboYear = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.cboStatus = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.cboCheckResult = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.cboFile = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.btnClear = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.tlpStats = new System.Windows.Forms.TableLayoutPanel();
            this.pnlStat1 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat1Sub = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat1Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat1Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat2 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat2Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat2Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat3 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat3Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat3Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat4 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat4Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat4Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlMessage = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlFooter = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.tlpFooter = new System.Windows.Forms.TableLayoutPanel();
            this.lblFooter = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnPrev = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblPage = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnNext = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlGrid = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.gridInvoices = new ComponentFactory.Krypton.Toolkit.KryptonDataGridView();
            this.colIssueDate = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCounterpartyId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCounterpartyName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colForm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSymbol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBeforeTax = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTax = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPayment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.cboYear)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboCheckResult)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboFile)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridInvoices)).BeginInit();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
            this.pnlTitle.Controls.Add(this.btnPurchase);
            this.pnlTitle.Controls.Add(this.btnSold);
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(1000, 84);
            this.pnlTitle.TabIndex = 5;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Kho hóa đơn";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Location = new System.Drawing.Point(16, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(560, 24);
            this.lblSubtitle.Values.Text = "Tra cứu chứng từ đã tải của doanh nghiệp đang mở";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnExportExcel);
            this.pnlTitleActions.Controls.Add(this.btnExportZip);
            this.pnlTitleActions.Controls.Add(this.btnCheckFiles);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 22, 16, 22);
            this.pnlTitleActions.Size = new System.Drawing.Size(500, 84);
            //
            // btnCheckFiles / btnExportZip / btnExportExcel (thứ tự thêm quyết định thứ tự xếp từ phải sang trái)
            //
            this.btnCheckFiles.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCheckFiles.Name = "btnCheckFiles";
            this.btnCheckFiles.Size = new System.Drawing.Size(140, 40);
            this.btnCheckFiles.Values.Text = "Kiểm tra file";
            this.btnCheckFiles.Click += new System.EventHandler(this.OnCheckFilesClick);
            this.btnExportZip.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnExportZip.Name = "btnExportZip";
            this.btnExportZip.Size = new System.Drawing.Size(158, 40);
            this.btnExportZip.Values.Text = "Xuất ZIP XML";
            this.btnExportZip.Click += new System.EventHandler(this.OnExportZipClick);
            this.btnExportExcel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnExportExcel.Name = "btnExportExcel";
            this.btnExportExcel.Size = new System.Drawing.Size(130, 40);
            this.btnExportExcel.Values.Text = "Xuất Excel";
            this.btnExportExcel.Click += new System.EventHandler(this.OnExportExcelClick);
            //
            // pnlFilter
            //
            this.pnlFilter.Controls.Add(this.tlpFilter);
            this.pnlFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlFilter.Name = "pnlFilter";
            this.pnlFilter.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlFilter.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            this.pnlFilter.Size = new System.Drawing.Size(1000, 56);
            this.pnlFilter.TabIndex = 4;
            //
            // tlpFilter
            //
            this.tlpFilter.BackColor = System.Drawing.Color.Transparent;
            this.tlpFilter.ColumnCount = 6;
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 190F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tlpFilter.Controls.Add(this.txtQuick, 0, 0);
            this.tlpFilter.Controls.Add(this.cboYear, 1, 0);
            this.tlpFilter.Controls.Add(this.cboStatus, 2, 0);
            this.tlpFilter.Controls.Add(this.cboCheckResult, 3, 0);
            this.tlpFilter.Controls.Add(this.cboFile, 4, 0);
            this.tlpFilter.Controls.Add(this.btnClear, 5, 0);
            this.tlpFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpFilter.Name = "tlpFilter";
            this.tlpFilter.RowCount = 1;
            this.tlpFilter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // btnPurchase
            //
            this.btnPurchase.Checked = true;
            this.btnPurchase.Location = new System.Drawing.Point(210, 10);
            this.btnPurchase.Size = new System.Drawing.Size(108, 34);
            this.btnPurchase.Name = "btnPurchase";
            this.btnPurchase.Values.Text = "Mua vào";
            this.btnPurchase.Click += new System.EventHandler(this.OnDirectionClick);
            //
            // btnSold
            //
            this.btnSold.Location = new System.Drawing.Point(324, 10);
            this.btnSold.Size = new System.Drawing.Size(96, 34);
            this.btnSold.Name = "btnSold";
            this.btnSold.Values.Text = "Bán ra";
            this.btnSold.Click += new System.EventHandler(this.OnDirectionClick);
            //
            // txtQuick
            //
            this.txtQuick.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtQuick.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.txtQuick.Name = "txtQuick";
            this.txtQuick.TextChanged += new System.EventHandler(this.OnQuickTextChanged);
            //
            // cboYear
            //
            this.cboYear.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboYear.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboYear.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.cboYear.Name = "cboYear";
            this.cboYear.SelectedIndexChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // cboStatus
            //
            this.cboStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStatus.DropDownWidth = 260;
            this.cboStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboStatus.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.cboStatus.Name = "cboStatus";
            this.cboStatus.SelectedIndexChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // cboCheckResult
            //
            this.cboCheckResult.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCheckResult.DropDownWidth = 380;
            this.cboCheckResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboCheckResult.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.cboCheckResult.Name = "cboCheckResult";
            this.cboCheckResult.SelectedIndexChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // cboFile
            //
            this.cboFile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboFile.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.cboFile.Name = "cboFile";
            this.cboFile.SelectedIndexChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // btnClear
            //
            this.btnClear.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnClear.Margin = new System.Windows.Forms.Padding(0);
            this.btnClear.Name = "btnClear";
            this.btnClear.Values.Text = "Xóa lọc";
            this.btnClear.Click += new System.EventHandler(this.OnClearClick);
            //
            // tlpStats
            //
            this.tlpStats.BackColor = System.Drawing.Color.Transparent;
            this.tlpStats.ColumnCount = 4;
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.Controls.Add(this.pnlStat1, 0, 0);
            this.tlpStats.Controls.Add(this.pnlStat2, 1, 0);
            this.tlpStats.Controls.Add(this.pnlStat3, 2, 0);
            this.tlpStats.Controls.Add(this.pnlStat4, 3, 0);
            this.tlpStats.Dock = System.Windows.Forms.DockStyle.Top;
            this.tlpStats.Name = "tlpStats";
            this.tlpStats.Padding = new System.Windows.Forms.Padding(16, 6, 16, 0);
            this.tlpStats.RowCount = 1;
            this.tlpStats.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpStats.Size = new System.Drawing.Size(1000, 92);
            this.tlpStats.TabIndex = 3;
            //
            // stat cards
            //
            this.pnlStat1.Controls.Add(this.lblStat1Sub);
            this.pnlStat1.Controls.Add(this.lblStat1Value);
            this.pnlStat1.Controls.Add(this.lblStat1Caption);
            this.pnlStat1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat1.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat1.Name = "pnlStat1";
            this.pnlStat1.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat1Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Caption.Name = "lblStat1Caption";
            this.lblStat1Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Caption.Values.Text = "KẾT QUẢ";
            this.lblStat1Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Value.Name = "lblStat1Value";
            this.lblStat1Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat1Value.Values.Text = "0";
            this.lblStat1Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Sub.Name = "lblStat1Sub";
            this.lblStat1Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Sub.Values.Text = "—";
            this.pnlStat2.Controls.Add(this.lblStat2Value);
            this.pnlStat2.Controls.Add(this.lblStat2Caption);
            this.pnlStat2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat2.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat2.Name = "pnlStat2";
            this.pnlStat2.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat2Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Caption.Name = "lblStat2Caption";
            this.lblStat2Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat2Caption.Values.Text = "TỔNG CHƯA THUẾ";
            this.lblStat2Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Value.Name = "lblStat2Value";
            this.lblStat2Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat2Value.Values.Text = "0";
            this.pnlStat3.Controls.Add(this.lblStat3Value);
            this.pnlStat3.Controls.Add(this.lblStat3Caption);
            this.pnlStat3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat3.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat3.Name = "pnlStat3";
            this.pnlStat3.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat3Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Caption.Name = "lblStat3Caption";
            this.lblStat3Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat3Caption.Values.Text = "TỔNG THUẾ";
            this.lblStat3Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Value.Name = "lblStat3Value";
            this.lblStat3Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat3Value.Values.Text = "0";
            this.pnlStat4.Controls.Add(this.lblStat4Value);
            this.pnlStat4.Controls.Add(this.lblStat4Caption);
            this.pnlStat4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat4.Margin = new System.Windows.Forms.Padding(0);
            this.pnlStat4.Name = "pnlStat4";
            this.pnlStat4.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat4Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Caption.Name = "lblStat4Caption";
            this.lblStat4Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat4Caption.Values.Text = "TỔNG THANH TOÁN";
            this.lblStat4Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Value.Name = "lblStat4Value";
            this.lblStat4Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat4Value.Values.Text = "0";
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
            // pnlFooter
            //
            this.pnlFooter.Controls.Add(this.tlpFooter);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Name = "pnlFooter";
            this.pnlFooter.Padding = new System.Windows.Forms.Padding(16, 2, 16, 6);
            this.pnlFooter.Size = new System.Drawing.Size(1000, 40);
            this.pnlFooter.TabIndex = 1;
            //
            // tlpFooter
            //
            this.tlpFooter.BackColor = System.Drawing.Color.Transparent;
            this.tlpFooter.ColumnCount = 4;
            this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tlpFooter.Controls.Add(this.lblFooter, 0, 0);
            this.tlpFooter.Controls.Add(this.btnPrev, 1, 0);
            this.tlpFooter.Controls.Add(this.lblPage, 2, 0);
            this.tlpFooter.Controls.Add(this.btnNext, 3, 0);
            this.tlpFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpFooter.Name = "tlpFooter";
            this.tlpFooter.RowCount = 1;
            this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // lblFooter
            //
            this.lblFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Values.Text = "";
            //
            // btnPrev
            //
            this.btnPrev.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnPrev.Enabled = false;
            this.btnPrev.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.btnPrev.Name = "btnPrev";
            this.btnPrev.Values.Text = "‹ Trước";
            this.btnPrev.Click += new System.EventHandler(this.OnPrevClick);
            //
            // lblPage
            //
            this.lblPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPage.Name = "lblPage";
            this.lblPage.StateCommon.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Center;
            this.lblPage.Values.Text = "Trang 1/1";
            //
            // btnNext
            //
            this.btnNext.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNext.Enabled = false;
            this.btnNext.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.btnNext.Name = "btnNext";
            this.btnNext.Values.Text = "Sau ›";
            this.btnNext.Click += new System.EventHandler(this.OnNextClick);
            //
            // pnlGrid
            //
            this.pnlGrid.Controls.Add(this.gridInvoices);
            this.pnlGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlGrid.Name = "pnlGrid";
            this.pnlGrid.Padding = new System.Windows.Forms.Padding(16, 0, 16, 0);
            this.pnlGrid.TabIndex = 0;
            //
            // gridInvoices
            //
            this.gridInvoices.AllowUserToAddRows = false;
            this.gridInvoices.AllowUserToDeleteRows = false;
            this.gridInvoices.AllowUserToResizeRows = false;
            this.gridInvoices.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridInvoices.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colIssueDate, this.colCounterpartyId, this.colCounterpartyName, this.colForm, this.colSymbol, this.colNumber,
            this.colStatus, this.colFile, this.colBeforeTax, this.colTax, this.colPayment});
            this.gridInvoices.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridInvoices.Name = "gridInvoices";
            this.gridInvoices.ReadOnly = true;
            this.gridInvoices.RowHeadersVisible = false;
            this.gridInvoices.MultiSelect = true;
            this.gridInvoices.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridInvoices.SelectionChanged += new System.EventHandler(this.OnGridSelectionChanged);
            this.gridInvoices.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnGridKeyDown);
            this.gridInvoices.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.OnHeaderClick);
            this.gridInvoices.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellDoubleClick);
            this.gridInvoices.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.OnCellFormatting);
            //
            // columns
            //
            rightStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.colIssueDate.FillWeight = 75F;
            this.colIssueDate.HeaderText = "Ngày lập";
            this.colIssueDate.Name = "colIssueDate";
            this.colCounterpartyId.FillWeight = 95F;
            this.colCounterpartyId.HeaderText = "MST người bán";
            this.colCounterpartyId.Name = "colCounterpartyId";
            this.colCounterpartyName.FillWeight = 160F;
            this.colCounterpartyName.HeaderText = "Tên người bán";
            this.colCounterpartyName.Name = "colCounterpartyName";
            this.colForm.FillWeight = 45F;
            this.colForm.HeaderText = "Mẫu số";
            this.colForm.Name = "colForm";
            this.colSymbol.FillWeight = 70F;
            this.colSymbol.HeaderText = "Ký hiệu";
            this.colSymbol.Name = "colSymbol";
            this.colNumber.FillWeight = 70F;
            this.colNumber.HeaderText = "Số HĐ";
            this.colNumber.Name = "colNumber";
            this.colStatus.FillWeight = 105F;
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colFile.FillWeight = 85F;
            this.colFile.HeaderText = "File";
            this.colFile.Name = "colFile";
            this.colBeforeTax.DefaultCellStyle = rightStyle;
            this.colBeforeTax.FillWeight = 90F;
            this.colBeforeTax.HeaderText = "Chưa thuế";
            this.colBeforeTax.Name = "colBeforeTax";
            this.colTax.DefaultCellStyle = rightStyle;
            this.colTax.FillWeight = 75F;
            this.colTax.HeaderText = "Thuế";
            this.colTax.Name = "colTax";
            this.colPayment.DefaultCellStyle = rightStyle;
            this.colPayment.FillWeight = 90F;
            this.colPayment.HeaderText = "Thanh toán";
            this.colPayment.Name = "colPayment";
            //
            // ArchiveControl
            //
            this.Controls.Add(this.pnlGrid);
            this.Controls.Add(this.pnlFooter);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.tlpStats);
            this.Controls.Add(this.pnlFilter);
            this.Controls.Add(this.pnlTitle);
            this.Name = "ArchiveControl";
            this.Size = new System.Drawing.Size(1000, 640);
            ((System.ComponentModel.ISupportInitialize)(this.cboYear)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboCheckResult)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboFile)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridInvoices)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCheckFiles;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnExportExcel;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnExportZip;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlFilter;
        private System.Windows.Forms.TableLayoutPanel tlpFilter;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton btnPurchase;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton btnSold;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtQuick;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboYear;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboCheckResult;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboFile;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnClear;
        private System.Windows.Forms.TableLayoutPanel tlpStats;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat1;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Sub;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat2;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat3;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat4;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlFooter;
        private System.Windows.Forms.TableLayoutPanel tlpFooter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblFooter;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnPrev;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblPage;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnNext;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlGrid;
        private ComponentFactory.Krypton.Toolkit.KryptonDataGridView gridInvoices;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIssueDate;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCounterpartyId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCounterpartyName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colForm;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSymbol;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBeforeTax;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTax;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPayment;
    }
}
