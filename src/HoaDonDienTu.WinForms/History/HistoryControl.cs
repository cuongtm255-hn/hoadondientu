using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;

namespace HoaDonDienTu.WinForms.History
{
    /// <summary>SCR-08 Lịch sử tải. Chỉ hiển thị và phát sự kiện; mọi cập nhật được chuyển về UI thread.</summary>
    public partial class HistoryControl : KryptonPanel, IHistoryView
    {
        private static readonly Color Good = Color.FromArgb(30, 110, 60);
        private static readonly Color Warning = Color.FromArgb(150, 100, 10);
        private static readonly Color Bad = Color.FromArgb(170, 40, 30);

        private sealed class Option<T>
        {
            public Option(T value, string label)
            {
                Value = value;
                Label = label;
            }

            public T Value { get; private set; }
            public string Label { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private bool _suppress;

        public HistoryControl()
        {
            InitializeComponent();
            cboDirection.Items.Add(new Option<InvoiceDirection?>(null, "Tất cả loại"));
            cboDirection.Items.Add(new Option<InvoiceDirection?>(InvoiceDirection.Purchase, "Mua vào"));
            cboDirection.Items.Add(new Option<InvoiceDirection?>(InvoiceDirection.Sold, "Bán ra"));
            cboDirection.SelectedIndex = 0;
            cboStatus.Items.Add(new Option<HistoryStatusFilter>(HistoryStatusFilter.All, "Tất cả trạng thái"));
            cboStatus.Items.Add(new Option<HistoryStatusFilter>(HistoryStatusFilter.Completed, "Hoàn tất"));
            cboStatus.Items.Add(new Option<HistoryStatusFilter>(HistoryStatusFilter.CompletedWithErrors, "Hoàn tất có lỗi"));
            cboStatus.Items.Add(new Option<HistoryStatusFilter>(HistoryStatusFilter.InProgress, "Đang chạy/tạm dừng"));
            cboStatus.Items.Add(new Option<HistoryStatusFilter>(HistoryStatusFilter.Stopped, "Đã hủy/không tiếp tục được"));
            cboStatus.SelectedIndex = 0;
        }

        public event EventHandler FilterChanged;
        public event EventHandler ClearRequested;
        public event EventHandler NewSessionRequested;
        public event EventHandler<PageRequestedEventArgs> PageRequested;
        public event EventHandler<SessionRequestedEventArgs> SessionRequested;

        public HistoryFilterInput ReadFilter()
        {
            var direction = cboDirection.SelectedItem as Option<InvoiceDirection?>;
            var status = cboStatus.SelectedItem as Option<HistoryStatusFilter>;
            return new HistoryFilterInput
            {
                Search = txtSearch.Text,
                Direction = direction == null ? null : direction.Value,
                Status = status == null ? HistoryStatusFilter.All : status.Value,
                From = dtpFrom.Checked ? (DateTime?)dtpFrom.Value.Date : null,
                To = dtpTo.Checked ? (DateTime?)dtpTo.Value.Date : null
            };
        }

        public void ShowFilter(HistoryFilterInput filter)
        {
            OnUi(delegate
            {
                _suppress = true;
                try
                {
                    txtSearch.Text = filter.Search ?? string.Empty;
                    for (var i = 0; i < cboDirection.Items.Count; i++)
                    {
                        if (((Option<InvoiceDirection?>)cboDirection.Items[i]).Value == filter.Direction) cboDirection.SelectedIndex = i;
                    }

                    for (var i = 0; i < cboStatus.Items.Count; i++)
                    {
                        if (((Option<HistoryStatusFilter>)cboStatus.Items[i]).Value == filter.Status) cboStatus.SelectedIndex = i;
                    }

                    SetDate(dtpFrom, filter.From);
                    SetDate(dtpTo, filter.To);
                }
                finally
                {
                    _suppress = false;
                }
            });
        }

        public void ShowResult(HistoryViewModel m)
        {
            OnUi(delegate
            {
                lblStat1Value.Values.Text = m.TotalSessions;
                lblStat2Value.Values.Text = m.CompletedSessions;
                lblStat3Value.Values.Text = m.ActiveSessions;
                lblStat4Value.Values.Text = m.AttentionSessions;
                lblFooter.Values.Text = m.FooterText;
                lblPage.Values.Text = m.PageText;
                btnPrev.Enabled = m.CanPrevious;
                btnNext.Enabled = m.CanNext;

                gridSessions.Rows.Clear();
                foreach (var r in m.Rows)
                {
                    var index = gridSessions.Rows.Add(r.Code, r.Started, r.Direction, r.Period, r.Status, r.NewCount, r.ExistingCount, r.ErrorCount, r.User, r.ActionText);
                    gridSessions.Rows[index].Tag = r;
                }

                gridSessions.ClearSelection();
            });
        }

        public void ShowMessage(string text, bool isError)
        {
            OnUi(delegate
            {
                lblMessage.Text = text ?? string.Empty;
                lblMessage.ForeColor = isError ? Bad : Good;
            });
        }

        // ---- Nội bộ ----

        private static void SetDate(KryptonDateTimePicker picker, DateTime? value)
        {
            if (value.HasValue)
            {
                picker.Value = value.Value;
                picker.Checked = true;
            }
            else
            {
                picker.Checked = false;
            }
        }

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

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridSessions.Rows.Count || e.ColumnIndex != colStatus.Index) return;
            var row = gridSessions.Rows[e.RowIndex].Tag as HistoryRowViewModel;
            if (row == null) return;
            e.CellStyle.ForeColor = row.Tone == HistoryStatusTone.Good ? Good : row.Tone == HistoryStatusTone.Warning ? Warning : row.Tone == HistoryStatusTone.Bad ? Bad : e.CellStyle.ForeColor;
        }

        private void OnCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == colAction.Index) OpenRow(e.RowIndex);
        }

        private void OnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != colAction.Index) OpenRow(e.RowIndex);
        }

        private void OpenRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= gridSessions.Rows.Count) return;
            var row = gridSessions.Rows[rowIndex].Tag as HistoryRowViewModel;
            var handler = SessionRequested;
            if (row != null && handler != null) handler(this, new SessionRequestedEventArgs(row.SessionId));
        }

        private void OnSearchTextChanged(object sender, EventArgs e)
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

        private void OnNewSessionClick(object sender, EventArgs e)
        {
            Raise(NewSessionRequested);
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
