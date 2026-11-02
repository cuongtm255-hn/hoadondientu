using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Dashboard
{
    /// <summary>SCR-02 Tổng quan. Chỉ hiển thị và phát sự kiện; mọi cập nhật được chuyển về UI thread.</summary>
    public partial class DashboardControl : KryptonPanel, IDashboardView
    {
        private static readonly Color Good = Color.FromArgb(30, 110, 60);
        private static readonly Color Warning = Color.FromArgb(150, 100, 10);
        private static readonly Color Bad = Color.FromArgb(170, 40, 30);

        private const int AttentionRowHeight = 40;

        public DashboardControl()
        {
            InitializeComponent();
            ShowSession(null);
        }

        public event EventHandler NewDownloadRequested;
        public event EventHandler ActiveSessionRequested;
        public event EventHandler OpenArchiveRequested;
        public event EventHandler<AttentionRequestedEventArgs> AttentionRequested;

        public void ShowResult(DashboardViewModel m)
        {
            OnUi(delegate
            {
                lblStat1Value.Values.Text = m.TotalInvoices;
                lblStat1Sub.Values.Text = m.TotalSub;
                lblStat2Caption.Values.Text = m.YearCaption;
                lblStat2Value.Values.Text = m.YearPayment;
                lblStat2Sub.Values.Text = m.YearSub;
                lblStat3Value.Values.Text = m.SyncValue;
                lblStat3Sub.Values.Text = m.SyncSub;
                lblStat4Value.Values.Text = m.AttentionValue;
                lblStat4Sub.Values.Text = m.AttentionSub;

                ShowSession(m);
                ShowAttention(m.Attention);

                gridRecent.Rows.Clear();
                foreach (var r in m.Recent)
                {
                    var index = gridRecent.Rows.Add(r.IssueDate, r.Direction, r.Partner, r.Number, r.Status, r.Payment);
                    gridRecent.Rows[index].Tag = r;
                }

                gridRecent.ClearSelection();
            });
        }

        public void ShowMode(bool canDownload, string note)
        {
            OnUi(delegate
            {
                btnNewDownload.Enabled = canDownload;
                lblModeNote.Values.Text = string.IsNullOrEmpty(note) ? " " : note;
                lblModeNote.StateCommon.ShortText.Color1 = Warning;
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

        private void ShowSession(DashboardViewModel m)
        {
            var has = m != null && m.HasActiveSession;
            lblNoSession.Visible = !has;
            lblSessionBadge.Visible = has;
            lblSessionTitle.Visible = has;
            lblSessionDetail.Visible = has;
            prgSession.Visible = has;
            lblProgressText.Visible = has;
            lblSessionStats.Visible = has;
            btnSession.Visible = has;
            if (!has) return;

            lblSessionBadge.Values.Text = m.SessionBadge;
            lblSessionTitle.Values.Text = m.SessionTitle;
            lblSessionDetail.Values.Text = m.SessionDetail;
            prgSession.Value = Math.Max(0, Math.Min(100, m.ProgressPercent));
            lblProgressText.Values.Text = m.ProgressText;
            lblSessionStats.Values.Text = m.SessionStats;
        }

        private void ShowAttention(IList<DashboardAttentionRowViewModel> rows)
        {
            flowAttention.SuspendLayout();
            try
            {
                var old = new List<Control>();
                foreach (Control c in flowAttention.Controls) old.Add(c);
                flowAttention.Controls.Clear();
                foreach (var c in old) c.Dispose();

                if (rows.Count == 0)
                {
                    flowAttention.Controls.Add(new KryptonLabel { Values = { Text = "Không có việc cần xử lý." }, AutoSize = true });
                    return;
                }

                for (var i = 0; i < rows.Count; i++) flowAttention.Controls.Add(BuildAttentionRow(rows[i], i));
                ResizeAttentionRows();
            }
            finally
            {
                flowAttention.ResumeLayout(true);
            }
        }

        private Control BuildAttentionRow(DashboardAttentionRowViewModel row, int index)
        {
            var panel = new Panel { Height = AttentionRowHeight, Margin = new Padding(0, 0, 0, 2), Width = Math.Max(200, flowAttention.ClientSize.Width - 14) };
            var button = new KryptonButton { Dock = DockStyle.Right, Width = 90, Tag = index };
            button.Values.Text = row.ActionText;
            button.Click += OnAttentionClick;
            var badge = new KryptonLabel { Dock = DockStyle.Left, AutoSize = false, Width = 86 };
            badge.Values.Text = row.Badge;
            badge.StateCommon.ShortText.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            badge.StateCommon.ShortText.Color1 = row.Tone == DashboardTone.Bad ? Bad : row.Tone == DashboardTone.Warning ? Warning : Good;
            var text = new KryptonLabel { Dock = DockStyle.Fill, AutoSize = false };
            text.Values.Text = row.Text;
            panel.Controls.Add(text);
            panel.Controls.Add(badge);
            panel.Controls.Add(button);
            return panel;
        }

        private void ResizeAttentionRows()
        {
            foreach (Control c in flowAttention.Controls)
            {
                if (c is Panel) c.Width = Math.Max(200, flowAttention.ClientSize.Width - 14);
            }
        }

        private void OnAttentionResize(object sender, EventArgs e)
        {
            ResizeAttentionRows();
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
            if (e.RowIndex < 0 || e.RowIndex >= gridRecent.Rows.Count || e.ColumnIndex != colStatus.Index) return;
            var row = gridRecent.Rows[e.RowIndex].Tag as DashboardRecentRowViewModel;
            if (row == null) return;
            e.CellStyle.ForeColor = row.Tone == DashboardTone.Bad ? Bad : row.Tone == DashboardTone.Warning ? Warning : Good;
        }

        private void OnAttentionClick(object sender, EventArgs e)
        {
            var button = sender as KryptonButton;
            var handler = AttentionRequested;
            if (button != null && button.Tag is int && handler != null) handler(this, new AttentionRequestedEventArgs((int)button.Tag));
        }

        private void OnNewDownloadClick(object sender, EventArgs e)
        {
            Raise(NewDownloadRequested);
        }

        private void OnSessionClick(object sender, EventArgs e)
        {
            Raise(ActiveSessionRequested);
        }

        private void OnOpenArchiveClick(object sender, EventArgs e)
        {
            Raise(OpenArchiveRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
