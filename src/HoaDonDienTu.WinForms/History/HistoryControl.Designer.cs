namespace HoaDonDienTu.WinForms.History
{
    partial class HistoryControl
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
            this.btnNewSession = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlFilter = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.tlpFilter = new System.Windows.Forms.TableLayoutPanel();
            this.txtSearch = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.cboDirection = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.cboStatus = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.dtpFrom = new ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker();
            this.dtpTo = new ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker();
            this.btnClear = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.tlpStats = new System.Windows.Forms.TableLayoutPanel();
            this.pnlStat1 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
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
            this.gridSessions = new ComponentFactory.Krypton.Toolkit.KryptonDataGridView();
            this.colCode = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStarted = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDirection = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeriod = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNew = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colExisting = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colErrors = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUser = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAction = new System.Windows.Forms.DataGridViewLinkColumn();
            ((System.ComponentModel.ISupportInitialize)(this.cboDirection)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridSessions)).BeginInit();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
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
            this.lblTitle.Values.Text = "Lịch sử tải";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Location = new System.Drawing.Point(16, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(560, 24);
            this.lblSubtitle.Values.Text = "Theo dõi các phiên tải, lỗi và kết quả đã lưu";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnNewSession);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 22, 16, 22);
            this.pnlTitleActions.Size = new System.Drawing.Size(190, 84);
            //
            // btnNewSession
            //
            this.btnNewSession.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnNewSession.Name = "btnNewSession";
            this.btnNewSession.Values.Text = "Tạo phiên tải mới";
            this.btnNewSession.Click += new System.EventHandler(this.OnNewSessionClick);
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
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 130F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 170F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tlpFilter.Controls.Add(this.txtSearch, 0, 0);
            this.tlpFilter.Controls.Add(this.cboDirection, 1, 0);
            this.tlpFilter.Controls.Add(this.cboStatus, 2, 0);
            this.tlpFilter.Controls.Add(this.dtpFrom, 3, 0);
            this.tlpFilter.Controls.Add(this.dtpTo, 4, 0);
            this.tlpFilter.Controls.Add(this.btnClear, 5, 0);
            this.tlpFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpFilter.Name = "tlpFilter";
            this.tlpFilter.RowCount = 1;
            this.tlpFilter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            //
            // txtSearch
            //
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.TextChanged += new System.EventHandler(this.OnSearchTextChanged);
            //
            // cboDirection
            //
            this.cboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDirection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboDirection.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.cboDirection.Name = "cboDirection";
            this.cboDirection.SelectedIndexChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // cboStatus
            //
            this.cboStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cboStatus.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.cboStatus.Name = "cboStatus";
            this.cboStatus.SelectedIndexChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // dtpFrom
            //
            this.dtpFrom.CustomFormat = "dd/MM/yyyy";
            this.dtpFrom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dtpFrom.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpFrom.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.dtpFrom.Name = "dtpFrom";
            this.dtpFrom.ShowCheckBox = true;
            this.dtpFrom.Checked = false;
            this.dtpFrom.ValueChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            this.dtpFrom.CheckedChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            //
            // dtpTo
            //
            this.dtpTo.CustomFormat = "dd/MM/yyyy";
            this.dtpTo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dtpTo.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpTo.Margin = new System.Windows.Forms.Padding(0, 4, 6, 0);
            this.dtpTo.Name = "dtpTo";
            this.dtpTo.ShowCheckBox = true;
            this.dtpTo.Checked = false;
            this.dtpTo.ValueChanged += new System.EventHandler(this.OnFilterSelectionChanged);
            this.dtpTo.CheckedChanged += new System.EventHandler(this.OnFilterSelectionChanged);
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
            this.tlpStats.Size = new System.Drawing.Size(1000, 78);
            this.tlpStats.TabIndex = 3;
            //
            // stat cards
            //
            this.pnlStat1.Controls.Add(this.lblStat1Value);
            this.pnlStat1.Controls.Add(this.lblStat1Caption);
            this.pnlStat1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat1.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat1.Name = "pnlStat1";
            this.pnlStat1.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat1Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Caption.Name = "lblStat1Caption";
            this.lblStat1Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Caption.Values.Text = "TỔNG PHIÊN";
            this.lblStat1Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Value.Name = "lblStat1Value";
            this.lblStat1Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat1Value.Values.Text = "0";
            this.pnlStat2.Controls.Add(this.lblStat2Value);
            this.pnlStat2.Controls.Add(this.lblStat2Caption);
            this.pnlStat2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat2.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat2.Name = "pnlStat2";
            this.pnlStat2.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat2Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Caption.Name = "lblStat2Caption";
            this.lblStat2Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat2Caption.Values.Text = "HOÀN TẤT";
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
            this.lblStat3Caption.Values.Text = "ĐANG CHẠY/TẠM DỪNG";
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
            this.lblStat4Caption.Values.Text = "CẦN XỬ LÝ";
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
            this.lblFooter.Values.Text = " ";
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
            this.pnlGrid.Controls.Add(this.gridSessions);
            this.pnlGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlGrid.Name = "pnlGrid";
            this.pnlGrid.Padding = new System.Windows.Forms.Padding(16, 0, 16, 0);
            this.pnlGrid.TabIndex = 0;
            //
            // gridSessions
            //
            this.gridSessions.AllowUserToAddRows = false;
            this.gridSessions.AllowUserToDeleteRows = false;
            this.gridSessions.AllowUserToResizeRows = false;
            this.gridSessions.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridSessions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colCode, this.colStarted, this.colDirection, this.colPeriod, this.colStatus,
            this.colNew, this.colExisting, this.colErrors, this.colUser, this.colAction});
            this.gridSessions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridSessions.Name = "gridSessions";
            this.gridSessions.ReadOnly = true;
            this.gridSessions.RowHeadersVisible = false;
            this.gridSessions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridSessions.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellContentClick);
            this.gridSessions.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnCellDoubleClick);
            this.gridSessions.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.OnCellFormatting);
            //
            // columns
            //
            rightStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.colCode.FillWeight = 105F;
            this.colCode.HeaderText = "Mã phiên";
            this.colCode.Name = "colCode";
            this.colStarted.FillWeight = 100F;
            this.colStarted.HeaderText = "Bắt đầu";
            this.colStarted.Name = "colStarted";
            this.colDirection.FillWeight = 65F;
            this.colDirection.HeaderText = "Loại";
            this.colDirection.Name = "colDirection";
            this.colPeriod.FillWeight = 90F;
            this.colPeriod.HeaderText = "Phạm vi";
            this.colPeriod.Name = "colPeriod";
            this.colStatus.FillWeight = 105F;
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colNew.DefaultCellStyle = rightStyle;
            this.colNew.FillWeight = 55F;
            this.colNew.HeaderText = "Mới";
            this.colNew.Name = "colNew";
            this.colExisting.DefaultCellStyle = rightStyle;
            this.colExisting.FillWeight = 55F;
            this.colExisting.HeaderText = "Đã có";
            this.colExisting.Name = "colExisting";
            this.colErrors.DefaultCellStyle = rightStyle;
            this.colErrors.FillWeight = 45F;
            this.colErrors.HeaderText = "Lỗi";
            this.colErrors.Name = "colErrors";
            this.colUser.FillWeight = 80F;
            this.colUser.HeaderText = "Người dùng";
            this.colUser.Name = "colUser";
            this.colAction.FillWeight = 85F;
            this.colAction.HeaderText = "Thao tác";
            this.colAction.Name = "colAction";
            this.colAction.TrackVisitedState = false;
            //
            // HistoryControl
            //
            this.Controls.Add(this.pnlGrid);
            this.Controls.Add(this.pnlFooter);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.tlpStats);
            this.Controls.Add(this.pnlFilter);
            this.Controls.Add(this.pnlTitle);
            this.Name = "HistoryControl";
            this.Size = new System.Drawing.Size(1000, 640);
            ((System.ComponentModel.ISupportInitialize)(this.cboDirection)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridSessions)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnNewSession;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlFilter;
        private System.Windows.Forms.TableLayoutPanel tlpFilter;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtSearch;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboDirection;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker dtpFrom;
        private ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker dtpTo;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnClear;
        private System.Windows.Forms.TableLayoutPanel tlpStats;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat1;
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
        private ComponentFactory.Krypton.Toolkit.KryptonDataGridView gridSessions;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCode;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStarted;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDirection;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeriod;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNew;
        private System.Windows.Forms.DataGridViewTextBoxColumn colExisting;
        private System.Windows.Forms.DataGridViewTextBoxColumn colErrors;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUser;
        private System.Windows.Forms.DataGridViewLinkColumn colAction;
    }
}
