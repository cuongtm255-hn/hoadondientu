using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>SCR-04 Tiến độ tải. Chỉ hiển thị và phát sự kiện; engine báo tiến độ từ luồng nền nên mọi cập nhật được chuyển về UI thread.</summary>
    public partial class DownloadProgressControl : KryptonPanel, IDownloadProgressView
    {
        private static readonly Color Failed = Color.FromArgb(170, 40, 30);
        private static readonly Color Running = Color.FromArgb(255, 246, 225);

        public DownloadProgressControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
            gridSegments.CellFormatting += OnCellFormatting;
        }

        public event EventHandler PauseRequested;
        public event EventHandler ResumeRequested;
        public event EventHandler CancelRequested;
        public event EventHandler RetryFailedRequested;
        public event EventHandler OpenArchiveRequested;
        public event EventHandler NewDownloadRequested;
        public event EventHandler<SegmentRetryEventArgs> RetrySegmentRequested;

        public void Show(DownloadProgressViewModel model)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                // Chưa có handle (đang dựng) thì cập nhật ngay trên luồng gọi; đã hủy thì bỏ qua.
                if (IsDisposed) return;
                Apply(model);
                return;
            }

            try
            {
                if (InvokeRequired) BeginInvoke(new Action(() => Apply(model)));
                else Apply(model);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void ShowMessage(string text, bool isError)
        {
            Action apply = delegate
            {
                lblMessage.Text = text ?? string.Empty;
                lblMessage.ForeColor = isError ? Failed : Color.FromArgb(30, 110, 60);
            };

            if (IsDisposed) return;
            if (IsHandleCreated && InvokeRequired) BeginInvoke(apply);
            else apply();
        }

        private void Apply(DownloadProgressViewModel m)
        {
            if (IsDisposed) return;
            lblBreadcrumb.Values.Text = "Tải hóa đơn / Phiên #" + m.SessionCode;
            lblSubtitle.Values.Text = m.Subtitle;
            lblStat1Value.Values.Text = m.Percent + "%";
            lblStat1Sub.Values.Text = m.ProgressText + " · " + m.StateText;
            lblStat2Value.Values.Text = m.ReceivedText;
            lblStat2Sub.Values.Text = m.ReceivedSub;
            lblStat3Value.Values.Text = m.SavedText;
            lblStat3Sub.Values.Text = m.SavedSub;
            lblStat4Value.Values.Text = m.ElapsedText;
            lblCheckpoint.Values.Text = string.IsNullOrEmpty(m.CheckpointText) ? "—" : m.CheckpointText;
            progressTotal.Value = Math.Max(0, Math.Min(100, m.Percent));

            btnPause.Enabled = m.CanPause;
            btnResume.Enabled = m.CanResume;
            btnCancel.Enabled = m.CanCancel;
            btnRetryFailed.Enabled = m.CanRetryFailed;
            btnOpenArchive.Enabled = m.CanOpenArchive;
            btnNewDownload.Enabled = m.CanNewDownload;

            // Giữ nguyên dòng và vị trí cuộn: chỉ ghi lại giá trị khi số dòng không đổi, tránh nhấp nháy mỗi lần báo tiến độ.
            if (gridSegments.Rows.Count != m.Rows.Count)
            {
                gridSegments.Rows.Clear();
                foreach (var row in m.Rows) gridSegments.Rows.Add();
            }

            for (var i = 0; i < m.Rows.Count; i++)
            {
                var r = m.Rows[i];
                var cells = gridSegments.Rows[i].Cells;
                cells["colMonth"].Value = r.Month;
                cells["colCheck"].Value = r.CheckResult;
                cells["colRange"].Value = r.Range;
                cells["colState"].Value = r.StateText;
                cells["colPages"].Value = r.Pages;
                cells["colReceived"].Value = r.Received;
                cells["colSaved"].Value = r.Saved;
                cells["colNote"].Value = r.Note;
                cells["colRetry"].Value = r.CanRetry ? "Thử lại" : string.Empty;
                gridSegments.Rows[i].Tag = r;
            }

            gridSegments.ClearSelection();
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridSegments.Rows.Count) return;
            var row = gridSegments.Rows[e.RowIndex].Tag as SegmentRowViewModel;
            if (row == null) return;
            if (row.IsRunning) e.CellStyle.BackColor = Running;
            if (row.HasError && e.ColumnIndex == gridSegments.Columns["colState"].Index) e.CellStyle.ForeColor = Failed;
        }

        /// <summary>Dòng không có gì để thử lại thì không vẽ nút rỗng.</summary>
        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != gridSegments.Columns["colRetry"].Index) return;
            var row = gridSegments.Rows[e.RowIndex].Tag as SegmentRowViewModel;
            if (row != null && row.CanRetry) return;
            e.PaintBackground(e.CellBounds, false);
            e.Handled = true;
        }

        private void OnGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != gridSegments.Columns["colRetry"].Index) return;
            var row = gridSegments.Rows[e.RowIndex].Tag as SegmentRowViewModel;
            if (row == null || !row.CanRetry) return;
            var handler = RetrySegmentRequested;
            if (handler != null) handler(this, new SegmentRetryEventArgs(row.Id));
        }

        private void OnPauseClick(object sender, EventArgs e)
        {
            Raise(PauseRequested);
        }

        private void OnResumeClick(object sender, EventArgs e)
        {
            Raise(ResumeRequested);
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            Raise(CancelRequested);
        }

        private void OnRetryFailedClick(object sender, EventArgs e)
        {
            Raise(RetryFailedRequested);
        }

        private void OnOpenArchiveClick(object sender, EventArgs e)
        {
            Raise(OpenArchiveRequested);
        }

        private void OnNewDownloadClick(object sender, EventArgs e)
        {
            Raise(NewDownloadRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
