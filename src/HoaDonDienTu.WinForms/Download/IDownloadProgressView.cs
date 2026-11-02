using System;
using System.Collections.Generic;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>Một dòng của bảng "Chi tiết theo tháng" (SCR-04), đã định dạng sẵn.</summary>
    public sealed class SegmentRowViewModel
    {
        public Guid Id { get; set; }
        public string Month { get; set; }
        public string Range { get; set; }
        public string StateText { get; set; }
        public string Pages { get; set; }
        public string Received { get; set; }
        public string Saved { get; set; }
        public string Note { get; set; }
        public bool CanRetry { get; set; }
        public bool IsRunning { get; set; }
        public bool HasError { get; set; }
    }

    public sealed class DownloadProgressViewModel
    {
        public string SessionCode { get; set; }
        public string Subtitle { get; set; }
        public string StateText { get; set; }

        /// <summary>0..100.</summary>
        public int Percent { get; set; }

        public string ProgressText { get; set; }
        public string ReceivedText { get; set; }
        public string ReceivedSub { get; set; }
        public string SavedText { get; set; }
        public string SavedSub { get; set; }
        public string ElapsedText { get; set; }
        public string CheckpointText { get; set; }

        public bool CanPause { get; set; }
        public bool CanResume { get; set; }
        public bool CanCancel { get; set; }
        public bool CanRetryFailed { get; set; }
        public IList<SegmentRowViewModel> Rows { get; set; }
    }

    public sealed class SegmentRetryEventArgs : EventArgs
    {
        public SegmentRetryEventArgs(Guid segmentId)
        {
            SegmentId = segmentId;
        }

        public Guid SegmentId { get; private set; }
    }

    public interface IDownloadProgressView
    {
        event EventHandler PauseRequested;
        event EventHandler ResumeRequested;
        event EventHandler CancelRequested;
        event EventHandler RetryFailedRequested;
        event EventHandler<SegmentRetryEventArgs> RetrySegmentRequested;

        /// <summary>Có thể được gọi từ luồng nền (engine báo tiến độ); view tự chuyển về UI thread.</summary>
        void Show(DownloadProgressViewModel model);

        void ShowMessage(string text, bool isError);
    }
}
