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

        private const int TypingDelayMs = 250;

        private bool _suppress;
        private bool _suppressSelection;
        private string _footerBase = string.Empty;
        private int _selectedCount;
        private readonly ContextMenuStrip _columnMenu = new ContextMenuStrip();
        private readonly Timer _typingTimer;

        public ArchiveControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
            _typingTimer = new Timer(components) { Interval = TypingDelayMs };
            _typingTimer.Tick += delegate
            {
                _typingTimer.Stop();
                Raise(FilterChanged);
            };
            Fill(cboStatus, PortalOptions.InvoiceStatuses, "Mọi trạng thái");
            Fill(cboCheckResult, PortalOptions.CheckResults, "Mọi kết quả kiểm tra");
            cboFile.Items.Add(new FileOption(FileIntegrityFilter.All, "Tình trạng file"));
            cboFile.Items.Add(new FileOption(FileIntegrityFilter.Problems, "Lỗi/thiếu file"));
            cboFile.Items.Add(new FileOption(FileIntegrityFilter.MultiVersion, "Nhiều phiên bản"));
            cboFile.SelectedIndex = 0;
            cboYear.Items.Add("Tất cả năm");
            cboYear.SelectedIndex = 0;
            BuildColumnMenu();
        }

        public event EventHandler FilterChanged;
        public event EventHandler ClearRequested;
        public event EventHandler CheckFilesRequested;
        public event EventHandler<PageRequestedEventArgs> PageRequested;
        public event EventHandler<SortRequestedEventArgs> SortRequested;
        public event EventHandler<InvoiceRequestedEventArgs> InvoiceRequested;
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
        public event EventHandler ColumnsChanged;
        public event EventHandler ExportExcelRequested;
        public event EventHandler ExportZipRequested;

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
                _footerBase = m.FooterText;
                lblFooter.Values.Text = FooterWithSelection();
                lblPage.Values.Text = m.PageText;
                btnPrev.Enabled = m.CanPrevious;
                btnNext.Enabled = m.CanNext;

                // Dựng lại cả lưới thì lưới tự phát SelectionChanged nhiều lần; chỉ báo đúng một lần ở cuối.
                _suppressSelection = true;
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
                _suppressSelection = true;
                try
                {
                    gridInvoices.ClearSelection();
                }
                finally
                {
                    _suppressSelection = false;
                }

                // Kết quả mới thì không còn dòng nào được chọn; báo một lần để presenter bỏ lựa chọn cũ.
                RaiseSelection();
            });
        }

        public IList<string> HiddenColumns
        {
            get
            {
                var hidden = new List<string>();
                foreach (DataGridViewColumn c in gridInvoices.Columns) if (!c.Visible) hidden.Add(c.Name);
                return hidden;
            }
        }

        public void ShowHiddenColumns(IList<string> hidden)
        {
            OnUi(delegate
            {
                var set = new HashSet<string>(hidden ?? new List<string>());
                foreach (DataGridViewColumn c in gridInvoices.Columns) c.Visible = !set.Contains(c.Name);

                // Không bao giờ ẩn hết cột.
                if (gridInvoices.Columns.GetColumnCount(DataGridViewElementStates.Visible) == 0) colIssueDate.Visible = true;
                SyncColumnMenu();
            });
        }

        public void ShowSelectionCount(int count)
        {
            OnUi(delegate
            {
                _selectedCount = count;
                btnExportExcel.Values.Text = count > 0 ? "Xuất Excel (" + count + ")" : "Xuất Excel";
                btnExportZip.Values.Text = count > 0 ? "Xuất ZIP XML (" + count + ")" : "Xuất ZIP XML";
                lblFooter.Values.Text = FooterWithSelection();
            });
        }

        public void ShowExportAllowed(bool allowed)
        {
            OnUi(delegate
            {
                btnExportExcel.Enabled = allowed;
                btnExportZip.Enabled = allowed;
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
            e.CellStyle.ForeColor = row.FileState == ArchiveFileState.Missing || row.FileState == ArchiveFileState.Damaged ? Missing : row.FileState == ArchiveFileState.MultiVersion ? Multi : Good;
        }

        // ---- Chọn nhiều, phím tắt, cột ----

        private string FooterWithSelection()
        {
            return _selectedCount > 0 ? _footerBase + " · Đã chọn " + _selectedCount : _footerBase;
        }

        private void RaiseSelection()
        {
            var handler = SelectionChanged;
            if (handler == null) return;
            var ids = new List<Guid>();
            foreach (DataGridViewRow row in gridInvoices.Rows)
            {
                var vm = row.Tag as ArchiveRowViewModel;
                if (row.Selected && vm != null) ids.Add(vm.InvoiceId);
            }

            handler(this, new SelectionChangedEventArgs(ids));
        }

        private void OnGridSelectionChanged(object sender, EventArgs e)
        {
            if (!_suppressSelection) RaiseSelection();
        }

        /// <summary>Enter mở chi tiết, Ctrl+E xuất Excel, Ctrl+Shift+E xuất ZIP, Esc bỏ chọn; Ctrl+A và Ctrl+C là mặc định của lưới (chọn hết, sao chép kèm tiêu đề).</summary>
        private void OnGridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt)
            {
                var row = gridInvoices.CurrentRow == null ? null : gridInvoices.CurrentRow.Tag as ArchiveRowViewModel;
                var handler = InvoiceRequested;
                if (row != null && handler != null) handler(this, new InvoiceRequestedEventArgs(row.InvoiceId));
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.E && e.Control && !e.Alt)
            {
                if (e.Shift)
                {
                    if (btnExportZip.Enabled) Raise(ExportZipRequested);
                }
                else if (btnExportExcel.Enabled)
                {
                    Raise(ExportExcelRequested);
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                gridInvoices.ClearSelection();
                e.Handled = true;
            }
        }

        private static readonly string[][] ColumnCaptions =
        {
            new[] { "colIssueDate", "Ngày lập" }, new[] { "colCounterpartyId", "MST đối tác" }, new[] { "colCounterpartyName", "Tên đối tác" },
            new[] { "colForm", "Mẫu số" }, new[] { "colSymbol", "Ký hiệu" }, new[] { "colNumber", "Số hóa đơn" }, new[] { "colStatus", "Trạng thái" },
            new[] { "colFile", "File" }, new[] { "colBeforeTax", "Chưa thuế" }, new[] { "colTax", "Thuế" }, new[] { "colPayment", "Thanh toán" }
        };

        private void BuildColumnMenu()
        {
            foreach (var pair in ColumnCaptions)
            {
                var name = pair[0];
                var item = new ToolStripMenuItem(pair[1]) { Name = "mnuColumn_" + name, Checked = true, CheckOnClick = true, Tag = name };
                item.Click += OnColumnMenuClick;
                _columnMenu.Items.Add(item);
            }

            _columnMenu.Items.Add(new ToolStripSeparator());
            var all = new ToolStripMenuItem("Hiện tất cả cột") { Name = "mnuColumn_all" };
            all.Click += delegate
            {
                foreach (DataGridViewColumn c in gridInvoices.Columns) c.Visible = true;
                SyncColumnMenu();
                Raise(ColumnsChanged);
            };
            _columnMenu.Items.Add(all);
            gridInvoices.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            foreach (DataGridViewColumn c in gridInvoices.Columns) c.HeaderCell.ContextMenuStrip = _columnMenu;
        }

        private void OnColumnMenuClick(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            var column = gridInvoices.Columns[(string)item.Tag];
            if (!item.Checked && gridInvoices.Columns.GetColumnCount(DataGridViewElementStates.Visible) <= 1)
            {
                // Phải còn ít nhất một cột.
                item.Checked = true;
                return;
            }

            column.Visible = item.Checked;
            Raise(ColumnsChanged);
        }

        private void SyncColumnMenu()
        {
            foreach (ToolStripItem i in _columnMenu.Items)
            {
                var item = i as ToolStripMenuItem;
                if (item == null || !(item.Tag is string)) continue;
                item.Checked = gridInvoices.Columns[(string)item.Tag].Visible;
            }
        }

        private void OnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridInvoices.Rows.Count) return;
            var row = gridInvoices.Rows[e.RowIndex].Tag as ArchiveRowViewModel;
            var handler = InvoiceRequested;
            if (row != null && handler != null) handler(this, new InvoiceRequestedEventArgs(row.InvoiceId));
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
            if (_suppress) return;
            _typingTimer.Stop();
            _typingTimer.Start();
        }

        private void OnFilterSelectionChanged(object sender, EventArgs e)
        {
            if (_suppress) return;
            _typingTimer.Stop();
            Raise(FilterChanged);
        }

        private void OnClearClick(object sender, EventArgs e)
        {
            Raise(ClearRequested);
        }

        private void OnExportExcelClick(object sender, EventArgs e)
        {
            Raise(ExportExcelRequested);
        }

        private void OnExportZipClick(object sender, EventArgs e)
        {
            Raise(ExportZipRequested);
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
