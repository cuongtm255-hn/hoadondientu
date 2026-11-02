using System;
using System.Collections.Generic;

namespace HoaDonDienTu.WinForms.Dashboard
{
    public enum DashboardTone
    {
        Neutral,
        Good,
        Warning,
        Bad
    }

    public sealed class DashboardAttentionRowViewModel
    {
        public string Badge { get; set; }
        public DashboardTone Tone { get; set; }
        public string Text { get; set; }
        public string ActionText { get; set; }
    }

    public sealed class DashboardRecentRowViewModel
    {
        public string IssueDate { get; set; }
        public string Direction { get; set; }
        public string Partner { get; set; }
        public string Number { get; set; }
        public string Status { get; set; }
        public DashboardTone Tone { get; set; }
        public string Payment { get; set; }
    }

    public sealed class DashboardViewModel
    {
        public string TotalInvoices { get; set; }
        public string TotalSub { get; set; }
        public string YearCaption { get; set; }
        public string YearPayment { get; set; }
        public string YearSub { get; set; }
        public string SyncValue { get; set; }
        public string SyncSub { get; set; }
        public string AttentionValue { get; set; }
        public string AttentionSub { get; set; }

        public bool HasActiveSession { get; set; }
        public string SessionBadge { get; set; }
        public string SessionTitle { get; set; }
        public string SessionDetail { get; set; }
        public int ProgressPercent { get; set; }
        public string ProgressText { get; set; }
        public string SessionStats { get; set; }

        public IList<DashboardAttentionRowViewModel> Attention { get; set; }
        public IList<DashboardRecentRowViewModel> Recent { get; set; }
    }

    public sealed class AttentionRequestedEventArgs : EventArgs
    {
        public AttentionRequestedEventArgs(int index)
        {
            Index = index;
        }

        public int Index { get; private set; }
    }

    public interface IDashboardView
    {
        event EventHandler NewDownloadRequested;
        event EventHandler ActiveSessionRequested;
        event EventHandler OpenArchiveRequested;
        event EventHandler<AttentionRequestedEventArgs> AttentionRequested;

        void ShowResult(DashboardViewModel model);

        /// <summary>Khóa/mở nút tải theo quyền sử dụng; <paramref name="note"/> hiện bên cạnh khi bị khóa (ví dụ chế độ chỉ đọc).</summary>
        void ShowMode(bool canDownload, string note);

        void ShowMessage(string text, bool isError);
    }
}
