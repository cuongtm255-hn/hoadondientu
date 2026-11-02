using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>SCR-05 Kho hóa đơn. Chỉ hiển thị và phát sự kiện; mọi cập nhật được chuyển về UI thread.</summary>
    public partial class ArchiveControl : KryptonPanel, IArchiveView
    {
        private static readonly Color Missing = Color.FromArgb(170, 40, 30);
        private static readonly Color Multi = Color.FromArgb(150, 100, 10);
        private static readonly Color Good = Color.FromArgb(30, 110, 60);

        private sealed class FileOption
        {
            public FileOption(FileIntegrityFilter filter, string label)
            {
                Filter = filter;
                Label = label;
            }

            public FileIntegrityFilter Filter { get; private set; }
            public string Label { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private bool _suppress;

        public ArchiveControl()
        {
            InitializeComponent();
            Fill(cboStatus, PortalOptions.InvoiceStatuses, "Mọi trạng thái");
            Fill(cboCheckResult, PortalOptions.CheckResults, "Mọi kết quả kiểm tra");
            cboFile.Items.Add(new FileOption(FileIntegrityFilter.All, "Tình trạng file"));
            cboFile.Items.Add(new FileOption(FileIntegrityFilter.Problems, "Lỗi/thiếu file"));
            cboFile.Items.Add(new FileOption(FileIntegrityFilter.MultiVersion, "Nhiều phiên bản"));
            cboFile.SelectedIndex = 0;
            cboYear.Items.Add("Tất cả năm");
            cboYear.SelectedIndex = 0;
        }

        public event EventHandler FilterChanged;
        public event EventHandler ClearRequested;
        public event EventHandler CheckFilesRequested;
        public event EventHandler<PageRequestedEventArgs> PageRequested;
        public event EventHandler<SortRequestedEventArgs> SortRequested;

        public ArchiveFilterInput ReadFilter()
        {
            var status = cboStatus.SelectedItem as PortalOption;
            var check = cboCheckResult.SelectedItem as PortalOption;
            var file = cboFile.SelectedItem as FileOption;
            int year;
            return new ArchiveFilterInput
            {
                Direction = btnSold.Checked ? InvoiceDirection.Sold : InvoiceDirection.Purchase,
                QuickSearch = txtQuick.Text,
                Year = int.TryParse(cboYear.Text, out year) ? year : 0,
                StatusCode = status == null ? string.Empty : status.Code,
                CheckResultCode = check == null ? string.Empty : check.Code,
                FileFilter = file == null ? FileIntegrityFilter.All : file.Filter
            };
        }

        public void ShowFilter(ArchiveFilterInput filter)
        {
            OnUi(delegate
            {
                _suppress = true;
                try
                {
                    btnPurchase.Checked = filter.Direction == InvoiceDirection.Purchase;
                    btnSold.Checked = filter.Direction == InvoiceDirection.Sold;
                    txtQuick.Text = filter.QuickSearch ?? string.Empty;
                    Select(cboYear, filter.Year > 0 ? filter.Year.ToString() : null);
                    SelectCode(cboStatus, filter.StatusCode);
                    SelectCode(cboCheckResult, filter.CheckResultCode);
                    for (var i = 0; i < cboFile.Items.Count; i++)
                    {
                        if (((FileOption)cboFile.Items[i]).Filter == filter.FileFilter) cboFile.SelectedIndex = i;
                    }
                }
                finally
                {
                    _suppress = false;
                }
            });
        }

        public void ShowYears(IList<int> years, int selected)
        {
            OnUi(delegate
            {
                _suppress = true;
                try
                {
                    cboYear.Items.Clear();
                    cboYear.Items.Add("Tất cả năm");
                    foreach (var year in years) cboYear.Items.Add(year.ToString());
                    Select(cboYear, selected > 0 ? selected.ToString() : null);
                }
                finally
                {
                    _suppress = false;
                }
            });
        }

        public void ShowResult(ArchiveViewModel m)
        {
            OnUi(delegate
            {
                colCounterpartyId.HeaderText = m.CounterpartyIdCaption;
                colCounterpartyName.HeaderText = m.CounterpartyNameCaption;
                lblStat1Value.Values.Text = m.ResultCount;
                lblStat1Sub.Values.Text = m.ResultSub;
                lblStat2Value.Values.Text = m.TotalBeforeTax;
                lblStat3Value.Values.Text = m.TotalTax;
                lblStat4Value.Values.Text = m.TotalPayment;
                lblFooter.Values.Text = m.FooterText;
                lblPage.Values.Text = m.PageText;
                btnPrev.Enabled = m.CanPrevious;
                btnNext.Enabled = m.CanNext;

                gridInvoices.Rows.Clear();
                foreach (var r in m.Rows)
                {
                    var index = gridInvoices.Rows.Add(
                        r.IssueDate, r.CounterpartyId, r.CounterpartyName, r.FormSymbol, r.InvoiceSymbol, r.InvoiceNumber,
                        r.Status, r.FileText, r.BeforeTax, r.Tax, r.Payment);
                    gridInvoices.Rows[index].Tag = r;
                }

                foreach (DataGridViewColumn column in gridInvoices.Columns) column.HeaderCell.SortGlyphDirection = SortOrder.None;
                var sorted = SortColumn(m.SortKey);
                if (sorted != null) sorted.HeaderCell.SortGlyphDirection = m.SortDescending ? SortOrder.Descending : SortOrder.Ascending;
                gridInvoices.ClearSelection();
            });
        }

        public void ShowMessage(string text, bool isError)
        {
            OnUi(delegate
            {
                lblMessage.Text = text ?? string.Empty;
                lblMessage.ForeColor = isError ? Missing : Good;
            });
        }

        // ---- Nội bộ ----

        private void OnUi(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private DataGridViewColumn SortColumn(ArchiveSortKey key)
        {
            switch (key)
            {
                case ArchiveSortKey.InvoiceNumber: return colNumber;
                case ArchiveSortKey.Counterparty: return colCounterpartyName;
                case ArchiveSortKey.TotalPayment: return colPayment;
                default: return colIssueDate;
            }
        }

        private static void Fill(KryptonComboBox combo, IList<PortalOption> options, string allLabel)
        {
            foreach (var option in options)
            {
                // "Tất cả" của cổng đổi nhãn cho hợp ngữ cảnh thanh lọc; mã rỗng vẫn nghĩa là không lọc.
                combo.Items.Add(string.IsNullOrEmpty(option.Code) ? new PortalOption(string.Empty, allLabel) : option);
            }

            combo.SelectedIndex = 0;
        }

        private static void Select(KryptonComboBox combo, string text)
        {
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if ((text == null && i == 0) || (text != null && combo.Items[i].ToString() == text))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private static void SelectCode(KryptonComboBox combo, string code)
        {
            for (var i = 0; i < combo.Items.Count; i++)
            {
                var option = combo.Items[i] as PortalOption;
                if (option != null && option.Code == (code ?? string.Empty))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridInvoices.Rows.Count || e.ColumnIndex != colFile.Index) return;
            var row = gridInvoices.Rows[e.RowIndex].Tag as ArchiveRowViewModel;
            if (row == null) return;
            e.CellStyle.ForeColor = row.FileState == ArchiveFileState.Missing ? Missing : row.FileState == ArchiveFileState.MultiVersion ? Multi : Good;
        }

        private void OnHeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            ArchiveSortKey key;
            if (e.ColumnIndex == colIssueDate.Index) key = ArchiveSortKey.IssueDate;
            else if (e.ColumnIndex == colNumber.Index) key = ArchiveSortKey.InvoiceNumber;
            else if (e.ColumnIndex == colCounterpartyName.Index) key = ArchiveSortKey.Counterparty;
            else if (e.ColumnIndex == colPayment.Index) key = ArchiveSortKey.TotalPayment;
            else return;

            var handler = SortRequested;
            if (handler != null) handler(this, new SortRequestedEventArgs(key));
        }

        private void OnDirectionClick(object sender, EventArgs e)
        {
            if (_suppress) return;
            _suppress = true;
            try
            {
                var sold = ReferenceEquals(sender, btnSold);
                btnSold.Checked = sold;
                btnPurchase.Checked = !sold;
            }
            finally
            {
                _suppress = false;
            }

            Raise(FilterChanged);
        }

        private void OnQuickTextChanged(object sender, EventArgs e)
        {
            if (!_suppress) Raise(FilterChanged);
        }

        private void OnFilterSelectionChanged(object sender, EventArgs e)
        {
            if (!_suppress) Raise(FilterChanged);
        }

        private void OnClearClick(object sender, EventArgs e)
        {
            Raise(ClearRequested);
        }

        private void OnCheckFilesClick(object sender, EventArgs e)
        {
            Raise(CheckFilesRequested);
        }

        private void OnPrevClick(object sender, EventArgs e)
        {
            var handler = PageRequested;
            if (handler != null) handler(this, new PageRequestedEventArgs(-1));
        }

        private void OnNextClick(object sender, EventArgs e)
        {
            var handler = PageRequested;
            if (handler != null) handler(this, new PageRequestedEventArgs(1));
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
