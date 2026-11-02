using System;
using System.Collections.Generic;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;

namespace HoaDonDienTu.WinForms.History
{
    /// <summary>Giá trị đang chọn trên thanh lọc của SCR-08. Direction/From/To rỗng là không lọc.</summary>
    public sealed class HistoryFilterInput
    {
        public string Search { get; set; }
        public InvoiceDirection? Direction { get; set; }
        public HistoryStatusFilter Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public enum HistoryStatusTone
    {
        Neutral,
        Good,
        Warning,
        Bad
    }

    public sealed class HistoryRowViewModel
    {
        public Guid SessionId { get; set; }
        public string Code { get; set; }
        public string Started { get; set; }
        public string Direction { get; set; }
        public string Period { get; set; }
        public string Status { get; set; }
        public HistoryStatusTone Tone { get; set; }
        public string NewCount { get; set; }
        public string ExistingCount { get; set; }
        public string ErrorCount { get; set; }
        public string User { get; set; }

        /// <summary>"Xem tiến độ", "Xem kết quả" hoặc "Xử lý lỗi" tùy trạng thái phiên.</summary>
        public string ActionText { get; set; }
    }

    public sealed class HistoryViewModel
    {
        public string TotalSessions { get; set; }
        public string CompletedSessions { get; set; }
        public string ActiveSessions { get; set; }
        public string AttentionSessions { get; set; }
        public string FooterText { get; set; }
        public string PageText { get; set; }
        public bool CanPrevious { get; set; }
        public bool CanNext { get; set; }
        public IList<HistoryRowViewModel> Rows { get; set; }
    }

    public sealed class SessionRequestedEventArgs : EventArgs
    {
        public SessionRequestedEventArgs(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get; private set; }
    }

    public interface IHistoryView
    {
        event EventHandler FilterChanged;
        event EventHandler ClearRequested;
        event EventHandler NewSessionRequested;
        event EventHandler<PageRequestedEventArgs> PageRequested;
        event EventHandler<SessionRequestedEventArgs> SessionRequested;

        HistoryFilterInput ReadFilter();

        /// <summary>Đặt lại bộ lọc (khôi phục hoặc xóa lọc) mà không phát FilterChanged.</summary>
        void ShowFilter(HistoryFilterInput filter);

        void ShowResult(HistoryViewModel model);
        void ShowMessage(string text, bool isError);
    }
}
