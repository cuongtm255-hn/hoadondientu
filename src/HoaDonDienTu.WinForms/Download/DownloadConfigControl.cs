using System;
using System.Collections.Generic;
using System.Drawing;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>SCR-03 Tải hóa đơn: chọn chiều hóa đơn, kỳ, bộ lọc và xem kế hoạch chia theo tháng. Chỉ hiển thị và phát sự kiện.</summary>
    public partial class DownloadConfigControl : KryptonPanel, IDownloadConfigView
    {
        private bool _suppress;

        public DownloadConfigControl()
        {
            InitializeComponent();

            for (var m = 1; m <= 12; m++) cboMonth.Items.Add("Tháng " + m);
            for (var q = 1; q <= 4; q++) cboQuarter.Items.Add("Quý " + q);
            // Giá trị dropdown đúng như form "Tra cứu hóa đơn" của cổng; mục "Tất cả" có mã rỗng và không được gửi.
            Fill(cboStatus, PortalOptions.InvoiceStatuses);
            Fill(cboCheckResult, PortalOptions.CheckResults);
            Fill(cboFormSymbol, PortalOptions.FormSymbols);

            cboMonth.SelectedIndex = 0;
            cboQuarter.SelectedIndex = 0;
            UpdateKindLayout();

            // Khi nội dung cao hơn khung, cuộn có thể bị đẩy xuống do focus; luôn bắt đầu ở đầu trang.
            VisibleChanged += delegate { pnlLeft.AutoScrollPosition = new Point(0, 0); };
        }

        public event EventHandler InputChanged;
        public event EventHandler ClearFiltersRequested;
        public event EventHandler StartRequested;

        public DownloadConfigInput ReadInput()
        {
            return new DownloadConfigInput
            {
                Direction = rdoSold.Checked ? InvoiceDirection.Sold : InvoiceDirection.Purchase,
                Kind = SelectedKind(),
                Year = (int)numYear.Value,
                Month = cboMonth.SelectedIndex + 1,
                Quarter = cboQuarter.SelectedIndex + 1,
                From = dtpFrom.Value.Date,
                To = dtpTo.Value.Date,
                CounterpartyTaxCode = txtCounterparty.Text,
                BuyerIdNumber = txtBuyerId.Text,
                InvoiceNumber = txtInvoiceNumber.Text,
                StatusCode = Combo(cboStatus),
                CheckResultCode = Combo(cboCheckResult),
                FormSymbol = Combo(cboFormSymbol),
                InvoiceSymbol = txtInvoiceSymbol.Text,
                ProxyInvoice = chkProxy.Checked
            };
        }

        public void ApplyDefaults(DateTime today)
        {
            _suppress = true;
            try
            {
                SetExclusive(rdoPurchase, rdoPurchase, rdoSold);
                SetExclusive(btnKindYear, btnKindMonth, btnKindQuarter, btnKindYear, btnKindCustom);
                numYear.Value = Math.Max(numYear.Minimum, Math.Min(numYear.Maximum, today.Year));
                cboMonth.SelectedIndex = today.Month - 1;
                cboQuarter.SelectedIndex = (today.Month - 1) / 3;
                dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
                dtpTo.Value = today.Date;
                UpdateKindLayout();
            }
            finally
            {
                _suppress = false;
            }
        }

        public void ShowSupportedFilters(ISet<string> supportedFields)
        {
            SetVisible(supportedFields, "CounterpartyTaxCode", lblCounterparty, txtCounterparty);
            SetVisible(supportedFields, "BuyerIdNumber", lblBuyerId, txtBuyerId);
            SetVisible(supportedFields, "InvoiceNumber", lblInvoiceNumber, txtInvoiceNumber);
            SetVisible(supportedFields, "StatusCode", lblStatus, cboStatus);
            SetVisible(supportedFields, "CheckResultCode", lblCheckResult, cboCheckResult);
            SetVisible(supportedFields, "FormSymbol", lblFormSymbol, cboFormSymbol);
            SetVisible(supportedFields, "InvoiceSymbol", lblInvoiceSymbol, txtInvoiceSymbol);
            SetVisible(supportedFields, "ProxyInvoice", chkProxy);

            // Không trường nào được hỗ trợ thì ẩn cả khung bộ lọc thay vì để khung trống.
            pnlCardFilter.Visible = supportedFields.Count > 0;
        }

        public void ClearFilters()
        {
            _suppress = true;
            try
            {
                txtCounterparty.Text = string.Empty;
                txtBuyerId.Text = string.Empty;
                txtInvoiceNumber.Text = string.Empty;
                txtInvoiceSymbol.Text = string.Empty;
                cboStatus.SelectedIndex = 0;
                cboCheckResult.SelectedIndex = 0;
                cboFormSymbol.SelectedIndex = 0;
                chkProxy.Checked = false;
            }
            finally
            {
                _suppress = false;
            }
        }

        public void ShowPlan(IList<DateSegment> segments)
        {
            gridPlan.Rows.Clear();
            for (var i = 0; i < segments.Count; i++)
            {
                gridPlan.Rows.Add(i + 1, segments[i].ToString(), "Chờ");
            }

            lblPlanCount.Values.Text = segments.Count == 0 ? string.Empty : segments.Count + " đoạn";
        }

        public void ShowPeriodMessages(IList<string> errors, IList<string> warnings)
        {
            if (errors.Count > 0)
            {
                lblPeriodMessage.ForeColor = Color.FromArgb(170, 40, 30);
                lblPeriodMessage.Text = string.Join(" ", errors);
            }
            else if (warnings.Count > 0)
            {
                lblPeriodMessage.ForeColor = Color.FromArgb(150, 100, 10);
                lblPeriodMessage.Text = string.Join(" ", warnings);
            }
            else
            {
                lblPeriodMessage.Text = string.Empty;
            }
        }

        public void ShowStartState(bool canStart, string reason)
        {
            btnStartDownload.Enabled = canStart;
            lblStartReason.Text = reason ?? string.Empty;
        }

        public void ShowStorageState(bool writable, string text)
        {
            lblStorageCheck.StateCommon.ShortText.Color1 = writable ? Color.FromArgb(30, 110, 60) : Color.FromArgb(170, 40, 30);
            lblStorageCheck.Values.Text = text;
        }

        private PeriodKind SelectedKind()
        {
            if (btnKindMonth.Checked) return PeriodKind.Month;
            if (btnKindQuarter.Checked) return PeriodKind.Quarter;
            if (btnKindCustom.Checked) return PeriodKind.Custom;
            return PeriodKind.Year;
        }

        private void UpdateKindLayout()
        {
            var kind = SelectedKind();
            var custom = kind == PeriodKind.Custom;
            var showPrimary = kind == PeriodKind.Month || kind == PeriodKind.Quarter;

            lblPrimary.Visible = showPrimary;
            cboMonth.Visible = kind == PeriodKind.Month;
            cboQuarter.Visible = kind == PeriodKind.Quarter;
            lblPrimary.Values.Text = kind == PeriodKind.Quarter ? "Quý" : "Tháng";

            lblYear.Visible = !custom;
            numYear.Visible = !custom;
            var yearX = showPrimary ? 180 : 0;
            lblYear.Location = new Point(yearX, lblYear.Location.Y);
            numYear.Location = new Point(yearX, numYear.Location.Y);

            lblFrom.Visible = custom;
            dtpFrom.Visible = custom;
            lblTo.Visible = custom;
            dtpTo.Visible = custom;
        }

        private static void Fill(KryptonComboBox combo, IList<PortalOption> options)
        {
            foreach (var option in options) combo.Items.Add(option);
            combo.SelectedIndex = 0;
        }

        /// <summary>Mã của mục đang chọn (rỗng với "Tất cả").</summary>
        private static string Combo(KryptonComboBox combo)
        {
            var option = combo.SelectedItem as PortalOption;
            return option == null ? string.Empty : option.Code;
        }

        public void ShowDirectionLabels(InvoiceDirection direction)
        {
            lblCounterparty.Values.Text = direction == InvoiceDirection.Purchase ? "MST người bán" : "MST người mua";
        }

        public void ShowFilterMessage(string message)
        {
            lblFilterMessage.Text = message ?? string.Empty;
        }

        private static void SetVisible(ISet<string> supported, string field, params System.Windows.Forms.Control[] controls)
        {
            var visible = supported.Contains(field);
            foreach (var control in controls) control.Visible = visible;
        }

        private static void SetExclusive(KryptonCheckButton selected, params KryptonCheckButton[] group)
        {
            foreach (var button in group) button.Checked = ReferenceEquals(button, selected);
        }

        private void OnDirectionClick(object sender, EventArgs e)
        {
            if (_suppress) return;
            SetExclusive((KryptonCheckButton)sender, rdoPurchase, rdoSold);
            OnInputChanged(sender, e);
        }

        private void OnKindClick(object sender, EventArgs e)
        {
            if (_suppress) return;
            SetExclusive((KryptonCheckButton)sender, btnKindMonth, btnKindQuarter, btnKindYear, btnKindCustom);
            UpdateKindLayout();
            OnInputChanged(sender, e);
        }

        private void OnInputChanged(object sender, EventArgs e)
        {
            if (_suppress) return;
            var handler = InputChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnClearFiltersClick(object sender, EventArgs e)
        {
            var handler = ClearFiltersRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnStartClick(object sender, EventArgs e)
        {
            var handler = StartRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
