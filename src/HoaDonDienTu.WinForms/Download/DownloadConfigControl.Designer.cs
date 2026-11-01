namespace HoaDonDienTu.WinForms.Download
{
    partial class DownloadConfigControl
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
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStorageCheck = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlLeft = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlCardFilter = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblCardFilter = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblCounterparty = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtCounterparty = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblBuyerId = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtBuyerId = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblFilterMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.lblInvoiceNumber = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtInvoiceNumber = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.lblStatus = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.cboStatus = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.lblCheckResult = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.cboCheckResult = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.lblFormSymbol = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.cboFormSymbol = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.lblInvoiceSymbol = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.txtInvoiceSymbol = new ComponentFactory.Krypton.Toolkit.KryptonTextBox();
            this.chkProxy = new ComponentFactory.Krypton.Toolkit.KryptonCheckBox();
            this.btnClearFilters = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlCardPeriod = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblCardPeriod = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.rdoPurchase = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.rdoSold = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.lblKind = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnKindMonth = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.btnKindQuarter = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.btnKindYear = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.btnKindCustom = new ComponentFactory.Krypton.Toolkit.KryptonCheckButton();
            this.pnlPeriodInputs = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblPrimary = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.cboMonth = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.cboQuarter = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.lblYear = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.numYear = new ComponentFactory.Krypton.Toolkit.KryptonNumericUpDown();
            this.lblFrom = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.dtpFrom = new ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker();
            this.lblTo = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.dtpTo = new ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker();
            this.lblPeriodMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlRight = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.gridPlan = new ComponentFactory.Krypton.Toolkit.KryptonDataGridView();
            this.colIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRange = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pnlPlanBottom = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStartReason = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.btnStartDownload = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.pnlNotice = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblNotice = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlPlanHeader = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblCardPlan = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblPlanCount = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboCheckResult)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboFormSymbol)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboMonth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboQuarter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridPlan)).BeginInit();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(900, 76);
            this.pnlTitle.TabIndex = 0;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(16, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Tải hóa đơn";
            //
            // lblSubtitle
            //
            this.lblSubtitle.Location = new System.Drawing.Point(18, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Values.Text = "Chọn kỳ và điều kiện tra cứu; ứng dụng tự chia theo từng tháng";
            //
            // lblStorageCheck
            //
            this.lblStorageCheck.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblStorageCheck.Name = "lblStorageCheck";
            this.lblStorageCheck.Size = new System.Drawing.Size(900, 26);
            this.lblStorageCheck.TabIndex = 3;
            this.lblStorageCheck.Values.Text = "";
            //
            // pnlLeft
            //
            this.pnlLeft.AutoScroll = true;
            this.pnlLeft.Controls.Add(this.pnlCardFilter);
            this.pnlLeft.Controls.Add(this.pnlCardPeriod);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(12, 0, 12, 8);
            this.pnlLeft.Size = new System.Drawing.Size(450, 400);
            this.pnlLeft.TabIndex = 1;
            //
            // pnlCardPeriod
            //
            this.pnlCardPeriod.Controls.Add(this.lblPeriodMessage);
            this.pnlCardPeriod.Controls.Add(this.pnlPeriodInputs);
            this.pnlCardPeriod.Controls.Add(this.btnKindCustom);
            this.pnlCardPeriod.Controls.Add(this.btnKindYear);
            this.pnlCardPeriod.Controls.Add(this.btnKindQuarter);
            this.pnlCardPeriod.Controls.Add(this.btnKindMonth);
            this.pnlCardPeriod.Controls.Add(this.lblKind);
            this.pnlCardPeriod.Controls.Add(this.rdoSold);
            this.pnlCardPeriod.Controls.Add(this.rdoPurchase);
            this.pnlCardPeriod.Controls.Add(this.lblCardPeriod);
            this.pnlCardPeriod.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardPeriod.Name = "pnlCardPeriod";
            this.pnlCardPeriod.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardPeriod.Size = new System.Drawing.Size(426, 270);
            this.pnlCardPeriod.TabIndex = 1;
            //
            // lblCardPeriod
            //
            this.lblCardPeriod.Location = new System.Drawing.Point(12, 8);
            this.lblCardPeriod.Name = "lblCardPeriod";
            this.lblCardPeriod.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardPeriod.Values.Text = "1. Loại hóa đơn và kỳ tải";
            //
            // rdoPurchase
            //
            this.rdoPurchase.Checked = true;
            this.rdoPurchase.Location = new System.Drawing.Point(16, 40);
            this.rdoPurchase.Name = "rdoPurchase";
            this.rdoPurchase.Size = new System.Drawing.Size(190, 34);
            this.rdoPurchase.Values.Text = "Hóa đơn mua vào";
            this.rdoPurchase.Click += new System.EventHandler(this.OnDirectionClick);
            //
            // rdoSold
            //
            this.rdoSold.Location = new System.Drawing.Point(216, 40);
            this.rdoSold.Name = "rdoSold";
            this.rdoSold.Size = new System.Drawing.Size(190, 34);
            this.rdoSold.Values.Text = "Hóa đơn bán ra";
            this.rdoSold.Click += new System.EventHandler(this.OnDirectionClick);
            //
            // lblKind
            //
            this.lblKind.Location = new System.Drawing.Point(12, 82);
            this.lblKind.Name = "lblKind";
            this.lblKind.Values.Text = "Kiểu kỳ";
            //
            // btnKindMonth
            //
            this.btnKindMonth.Location = new System.Drawing.Point(16, 106);
            this.btnKindMonth.Name = "btnKindMonth";
            this.btnKindMonth.Size = new System.Drawing.Size(94, 32);
            this.btnKindMonth.Values.Text = "Tháng";
            this.btnKindMonth.Click += new System.EventHandler(this.OnKindClick);
            //
            // btnKindQuarter
            //
            this.btnKindQuarter.Location = new System.Drawing.Point(114, 106);
            this.btnKindQuarter.Name = "btnKindQuarter";
            this.btnKindQuarter.Size = new System.Drawing.Size(94, 32);
            this.btnKindQuarter.Values.Text = "Quý";
            this.btnKindQuarter.Click += new System.EventHandler(this.OnKindClick);
            //
            // btnKindYear
            //
            this.btnKindYear.Checked = true;
            this.btnKindYear.Location = new System.Drawing.Point(212, 106);
            this.btnKindYear.Name = "btnKindYear";
            this.btnKindYear.Size = new System.Drawing.Size(94, 32);
            this.btnKindYear.Values.Text = "Năm";
            this.btnKindYear.Click += new System.EventHandler(this.OnKindClick);
            //
            // btnKindCustom
            //
            this.btnKindCustom.Location = new System.Drawing.Point(310, 106);
            this.btnKindCustom.Name = "btnKindCustom";
            this.btnKindCustom.Size = new System.Drawing.Size(96, 32);
            this.btnKindCustom.Values.Text = "Tùy chọn";
            this.btnKindCustom.Click += new System.EventHandler(this.OnKindClick);
            //
            // pnlPeriodInputs
            //
            this.pnlPeriodInputs.Controls.Add(this.dtpTo);
            this.pnlPeriodInputs.Controls.Add(this.lblTo);
            this.pnlPeriodInputs.Controls.Add(this.dtpFrom);
            this.pnlPeriodInputs.Controls.Add(this.lblFrom);
            this.pnlPeriodInputs.Controls.Add(this.numYear);
            this.pnlPeriodInputs.Controls.Add(this.lblYear);
            this.pnlPeriodInputs.Controls.Add(this.cboQuarter);
            this.pnlPeriodInputs.Controls.Add(this.cboMonth);
            this.pnlPeriodInputs.Controls.Add(this.lblPrimary);
            this.pnlPeriodInputs.Location = new System.Drawing.Point(16, 146);
            this.pnlPeriodInputs.Name = "pnlPeriodInputs";
            this.pnlPeriodInputs.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlPeriodInputs.Size = new System.Drawing.Size(390, 58);
            //
            // lblPrimary
            //
            this.lblPrimary.Location = new System.Drawing.Point(0, 0);
            this.lblPrimary.Name = "lblPrimary";
            this.lblPrimary.Values.Text = "Tháng";
            //
            // cboMonth
            //
            this.cboMonth.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMonth.Location = new System.Drawing.Point(0, 24);
            this.cboMonth.Name = "cboMonth";
            this.cboMonth.Size = new System.Drawing.Size(160, 24);
            this.cboMonth.SelectedIndexChanged += new System.EventHandler(this.OnInputChanged);
            //
            // cboQuarter
            //
            this.cboQuarter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboQuarter.Location = new System.Drawing.Point(0, 24);
            this.cboQuarter.Name = "cboQuarter";
            this.cboQuarter.Size = new System.Drawing.Size(160, 24);
            this.cboQuarter.SelectedIndexChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblYear
            //
            this.lblYear.Location = new System.Drawing.Point(180, 0);
            this.lblYear.Name = "lblYear";
            this.lblYear.Values.Text = "Năm";
            //
            // numYear
            //
            this.numYear.Location = new System.Drawing.Point(180, 24);
            this.numYear.Maximum = new decimal(new int[] { 2100, 0, 0, 0 });
            this.numYear.Minimum = new decimal(new int[] { 1990, 0, 0, 0 });
            this.numYear.Name = "numYear";
            this.numYear.Size = new System.Drawing.Size(120, 24);
            this.numYear.Value = new decimal(new int[] { 2026, 0, 0, 0 });
            this.numYear.ValueChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblFrom
            //
            this.lblFrom.Location = new System.Drawing.Point(0, 0);
            this.lblFrom.Name = "lblFrom";
            this.lblFrom.Values.Text = "Từ ngày";
            //
            // dtpFrom
            //
            this.dtpFrom.CustomFormat = "dd/MM/yyyy";
            this.dtpFrom.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpFrom.Location = new System.Drawing.Point(0, 24);
            this.dtpFrom.Name = "dtpFrom";
            this.dtpFrom.Size = new System.Drawing.Size(160, 24);
            this.dtpFrom.ValueChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblTo
            //
            this.lblTo.Location = new System.Drawing.Point(180, 0);
            this.lblTo.Name = "lblTo";
            this.lblTo.Values.Text = "Đến ngày";
            //
            // dtpTo
            //
            this.dtpTo.CustomFormat = "dd/MM/yyyy";
            this.dtpTo.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpTo.Location = new System.Drawing.Point(180, 24);
            this.dtpTo.Name = "dtpTo";
            this.dtpTo.Size = new System.Drawing.Size(160, 24);
            this.dtpTo.ValueChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblPeriodMessage
            //
            this.lblPeriodMessage.AutoSize = false;
            this.lblPeriodMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblPeriodMessage.Location = new System.Drawing.Point(16, 212);
            this.lblPeriodMessage.Name = "lblPeriodMessage";
            this.lblPeriodMessage.Size = new System.Drawing.Size(390, 48);
            this.lblPeriodMessage.Text = "";
            //
            // pnlCardFilter
            //
            this.pnlCardFilter.Controls.Add(this.lblFilterMessage);
            this.pnlCardFilter.Controls.Add(this.btnClearFilters);
            this.pnlCardFilter.Controls.Add(this.chkProxy);
            this.pnlCardFilter.Controls.Add(this.txtInvoiceSymbol);
            this.pnlCardFilter.Controls.Add(this.lblInvoiceSymbol);
            this.pnlCardFilter.Controls.Add(this.cboFormSymbol);
            this.pnlCardFilter.Controls.Add(this.lblFormSymbol);
            this.pnlCardFilter.Controls.Add(this.cboCheckResult);
            this.pnlCardFilter.Controls.Add(this.lblCheckResult);
            this.pnlCardFilter.Controls.Add(this.cboStatus);
            this.pnlCardFilter.Controls.Add(this.lblStatus);
            this.pnlCardFilter.Controls.Add(this.txtInvoiceNumber);
            this.pnlCardFilter.Controls.Add(this.lblInvoiceNumber);
            this.pnlCardFilter.Controls.Add(this.txtBuyerId);
            this.pnlCardFilter.Controls.Add(this.lblBuyerId);
            this.pnlCardFilter.Controls.Add(this.txtCounterparty);
            this.pnlCardFilter.Controls.Add(this.lblCounterparty);
            this.pnlCardFilter.Controls.Add(this.lblCardFilter);
            this.pnlCardFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardFilter.Name = "pnlCardFilter";
            this.pnlCardFilter.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlCardFilter.Size = new System.Drawing.Size(426, 336);
            this.pnlCardFilter.TabIndex = 0;
            //
            // lblCardFilter
            //
            this.lblCardFilter.Location = new System.Drawing.Point(12, 8);
            this.lblCardFilter.Name = "lblCardFilter";
            this.lblCardFilter.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardFilter.Values.Text = "2. Bộ lọc nâng cao (có thể bỏ trống)";
            //
            // lblCounterparty
            //
            this.lblCounterparty.Location = new System.Drawing.Point(12, 38);
            this.lblCounterparty.Name = "lblCounterparty";
            this.lblCounterparty.Values.Text = "MST người mua";
            //
            // txtCounterparty
            //
            this.txtCounterparty.Location = new System.Drawing.Point(16, 62);
            this.txtCounterparty.Name = "txtCounterparty";
            this.txtCounterparty.Size = new System.Drawing.Size(190, 24);
            this.txtCounterparty.TextChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblBuyerId
            //
            this.lblBuyerId.Location = new System.Drawing.Point(212, 38);
            this.lblBuyerId.Name = "lblBuyerId";
            this.lblBuyerId.Values.Text = "CCCD người mua";
            //
            // txtBuyerId
            //
            this.txtBuyerId.Location = new System.Drawing.Point(216, 62);
            this.txtBuyerId.MaxLength = 12;
            this.txtBuyerId.Name = "txtBuyerId";
            this.txtBuyerId.Size = new System.Drawing.Size(190, 24);
            this.txtBuyerId.TextChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblInvoiceNumber
            //
            this.lblInvoiceNumber.Location = new System.Drawing.Point(12, 218);
            this.lblInvoiceNumber.Name = "lblInvoiceNumber";
            this.lblInvoiceNumber.Values.Text = "Số hóa đơn";
            //
            // txtInvoiceNumber
            //
            this.txtInvoiceNumber.Location = new System.Drawing.Point(16, 242);
            this.txtInvoiceNumber.MaxLength = 16;
            this.txtInvoiceNumber.Name = "txtInvoiceNumber";
            this.txtInvoiceNumber.Size = new System.Drawing.Size(190, 24);
            this.txtInvoiceNumber.TextChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblStatus
            //
            this.lblStatus.Location = new System.Drawing.Point(12, 98);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Values.Text = "Trạng thái hóa đơn";
            //
            // cboStatus
            //
            this.cboStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStatus.DropDownWidth = 360;
            this.cboStatus.Location = new System.Drawing.Point(16, 122);
            this.cboStatus.Name = "cboStatus";
            this.cboStatus.Size = new System.Drawing.Size(190, 24);
            this.cboStatus.SelectedIndexChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblCheckResult
            //
            this.lblCheckResult.Location = new System.Drawing.Point(212, 98);
            this.lblCheckResult.Name = "lblCheckResult";
            this.lblCheckResult.Values.Text = "Kết quả kiểm tra";
            //
            // cboCheckResult
            //
            this.cboCheckResult.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCheckResult.DropDownWidth = 360;
            this.cboCheckResult.Location = new System.Drawing.Point(216, 122);
            this.cboCheckResult.Name = "cboCheckResult";
            this.cboCheckResult.Size = new System.Drawing.Size(190, 24);
            this.cboCheckResult.SelectedIndexChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblFormSymbol
            //
            this.lblFormSymbol.Location = new System.Drawing.Point(12, 158);
            this.lblFormSymbol.Name = "lblFormSymbol";
            this.lblFormSymbol.Values.Text = "Ký hiệu mẫu số";
            //
            // cboFormSymbol
            //
            this.cboFormSymbol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFormSymbol.DropDownWidth = 190;
            this.cboFormSymbol.Location = new System.Drawing.Point(16, 182);
            this.cboFormSymbol.Name = "cboFormSymbol";
            this.cboFormSymbol.Size = new System.Drawing.Size(190, 24);
            this.cboFormSymbol.SelectedIndexChanged += new System.EventHandler(this.OnInputChanged);
            //
            // lblInvoiceSymbol
            //
            this.lblInvoiceSymbol.Location = new System.Drawing.Point(212, 158);
            this.lblInvoiceSymbol.Name = "lblInvoiceSymbol";
            this.lblInvoiceSymbol.Values.Text = "Ký hiệu hóa đơn";
            //
            // txtInvoiceSymbol
            //
            this.txtInvoiceSymbol.Location = new System.Drawing.Point(216, 182);
            this.txtInvoiceSymbol.Name = "txtInvoiceSymbol";
            this.txtInvoiceSymbol.Size = new System.Drawing.Size(190, 24);
            this.txtInvoiceSymbol.TextChanged += new System.EventHandler(this.OnInputChanged);
            //
            // chkProxy
            //
            this.chkProxy.Location = new System.Drawing.Point(216, 246);
            this.chkProxy.Name = "chkProxy";
            this.chkProxy.Size = new System.Drawing.Size(190, 20);
            this.chkProxy.Values.Text = "Hóa đơn ủy nhiệm";
            this.chkProxy.CheckedChanged += new System.EventHandler(this.OnInputChanged);
            //
            // btnClearFilters
            //
            this.btnClearFilters.Location = new System.Drawing.Point(296, 284);
            this.btnClearFilters.Name = "btnClearFilters";
            this.btnClearFilters.Size = new System.Drawing.Size(110, 32);
            this.btnClearFilters.Values.Text = "Xóa bộ lọc";
            this.btnClearFilters.Click += new System.EventHandler(this.OnClearFiltersClick);
            //
            // lblFilterMessage
            //
            this.lblFilterMessage.AutoSize = false;
            this.lblFilterMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblFilterMessage.ForeColor = System.Drawing.Color.FromArgb(170, 40, 30);
            this.lblFilterMessage.Location = new System.Drawing.Point(16, 280);
            this.lblFilterMessage.Name = "lblFilterMessage";
            this.lblFilterMessage.Size = new System.Drawing.Size(270, 44);
            this.lblFilterMessage.Text = "";
            //
            // pnlRight
            //
            this.pnlRight.Controls.Add(this.gridPlan);
            this.pnlRight.Controls.Add(this.pnlPlanBottom);
            this.pnlRight.Controls.Add(this.pnlNotice);
            this.pnlRight.Controls.Add(this.pnlPlanHeader);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Padding = new System.Windows.Forms.Padding(0, 0, 16, 8);
            this.pnlRight.TabIndex = 2;
            //
            // pnlPlanHeader
            //
            this.pnlPlanHeader.Controls.Add(this.lblCardPlan);
            this.pnlPlanHeader.Controls.Add(this.lblPlanCount);
            this.pnlPlanHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlPlanHeader.Name = "pnlPlanHeader";
            this.pnlPlanHeader.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlPlanHeader.Size = new System.Drawing.Size(400, 34);
            //
            // lblCardPlan
            //
            this.lblCardPlan.Location = new System.Drawing.Point(12, 6);
            this.lblCardPlan.Name = "lblCardPlan";
            this.lblCardPlan.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblCardPlan.Values.Text = "3. Kế hoạch tải";
            //
            // lblPlanCount
            //
            this.lblPlanCount.AutoSize = false;
            this.lblPlanCount.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblPlanCount.Size = new System.Drawing.Size(100, 34);
            this.lblPlanCount.Name = "lblPlanCount";
            this.lblPlanCount.Values.Text = "";
            //
            // pnlNotice
            //
            this.pnlNotice.Controls.Add(this.lblNotice);
            this.pnlNotice.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlNotice.Name = "pnlNotice";
            this.pnlNotice.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this.pnlNotice.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelCustom1;
            this.pnlNotice.Size = new System.Drawing.Size(400, 56);
            this.pnlNotice.StateCommon.Color1 = System.Drawing.Color.FromArgb(255, 247, 230);
            //
            // lblNotice
            //
            this.lblNotice.AutoSize = false;
            this.lblNotice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNotice.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblNotice.ForeColor = System.Drawing.Color.FromArgb(110, 70, 10);
            this.lblNotice.Name = "lblNotice";
            this.lblNotice.Text = "Cổng Thuế giới hạn mỗi lần tra cứu trong một tháng. Ứng dụng sẽ chạy tuần tự từng đoạn và tự phân trang.";
            //
            // pnlPlanBottom
            //
            this.pnlPlanBottom.Controls.Add(this.lblStartReason);
            this.pnlPlanBottom.Controls.Add(this.btnStartDownload);
            this.pnlPlanBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlPlanBottom.Name = "pnlPlanBottom";
            this.pnlPlanBottom.Padding = new System.Windows.Forms.Padding(0, 8, 0, 8);
            this.pnlPlanBottom.Size = new System.Drawing.Size(400, 56);
            //
            // lblStartReason
            //
            this.lblStartReason.AutoSize = false;
            this.lblStartReason.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStartReason.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStartReason.ForeColor = System.Drawing.Color.FromArgb(150, 50, 30);
            this.lblStartReason.Name = "lblStartReason";
            this.lblStartReason.Text = "";
            //
            // btnStartDownload
            //
            this.btnStartDownload.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnStartDownload.Enabled = false;
            this.btnStartDownload.Name = "btnStartDownload";
            this.btnStartDownload.Size = new System.Drawing.Size(150, 40);
            this.btnStartDownload.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnStartDownload.Values.Text = "Bắt đầu tải";
            this.btnStartDownload.Click += new System.EventHandler(this.OnStartClick);
            //
            // gridPlan
            //
            this.gridPlan.AllowUserToAddRows = false;
            this.gridPlan.AllowUserToDeleteRows = false;
            this.gridPlan.AllowUserToResizeRows = false;
            this.gridPlan.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridPlan.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.colIndex, this.colRange, this.colStatus });
            this.gridPlan.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPlan.MultiSelect = false;
            this.gridPlan.Name = "gridPlan";
            this.gridPlan.ReadOnly = true;
            this.gridPlan.RowHeadersVisible = false;
            this.gridPlan.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridPlan.TabIndex = 0;
            //
            // colIndex
            //
            this.colIndex.FillWeight = 12F;
            this.colIndex.HeaderText = "#";
            this.colIndex.Name = "colIndex";
            this.colIndex.ReadOnly = true;
            //
            // colRange
            //
            this.colRange.FillWeight = 60F;
            this.colRange.HeaderText = "Khoảng ngày";
            this.colRange.Name = "colRange";
            this.colRange.ReadOnly = true;
            //
            // colStatus
            //
            this.colStatus.FillWeight = 28F;
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            //
            // DownloadConfigControl
            //
            this.Controls.Add(this.pnlRight);
            this.Controls.Add(this.pnlLeft);
            this.Controls.Add(this.lblStorageCheck);
            this.Controls.Add(this.pnlTitle);
            this.Name = "DownloadConfigControl";
            this.Size = new System.Drawing.Size(900, 560);
            ((System.ComponentModel.ISupportInitialize)(this.cboStatus)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboCheckResult)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboFormSymbol)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboMonth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboQuarter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridPlan)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStorageCheck;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlLeft;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardFilter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardFilter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCounterparty;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtCounterparty;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblBuyerId;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtBuyerId;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblFilterMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblInvoiceNumber;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtInvoiceNumber;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheckResult;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboCheckResult;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblFormSymbol;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboFormSymbol;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblInvoiceSymbol;
        private ComponentFactory.Krypton.Toolkit.KryptonTextBox txtInvoiceSymbol;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckBox chkProxy;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnClearFilters;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlCardPeriod;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardPeriod;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton rdoPurchase;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton rdoSold;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblKind;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton btnKindMonth;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton btnKindQuarter;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton btnKindYear;
        private ComponentFactory.Krypton.Toolkit.KryptonCheckButton btnKindCustom;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlPeriodInputs;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblPrimary;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboMonth;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboQuarter;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblYear;
        private ComponentFactory.Krypton.Toolkit.KryptonNumericUpDown numYear;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblFrom;
        private ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker dtpFrom;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTo;
        private ComponentFactory.Krypton.Toolkit.KryptonDateTimePicker dtpTo;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblPeriodMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlRight;
        private ComponentFactory.Krypton.Toolkit.KryptonDataGridView gridPlan;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIndex;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRange;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlPlanBottom;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblStartReason;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnStartDownload;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlNotice;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblNotice;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlPlanHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCardPlan;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblPlanCount;
    }
}
