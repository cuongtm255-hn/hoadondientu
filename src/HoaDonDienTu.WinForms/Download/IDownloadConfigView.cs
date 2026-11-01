using System;
using System.Collections.Generic;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>Giá trị người dùng đang nhập trên SCR-03; chưa kiểm tra hợp lệ (việc của <c>IPeriodSplitter</c>).</summary>
    public sealed class DownloadConfigInput
    {
        public InvoiceDirection Direction { get; set; }
        public PeriodKind Kind { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Quarter { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public string CounterpartyTaxCode { get; set; }
        public string BuyerIdNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public string StatusCode { get; set; }
        public string CheckResultCode { get; set; }
        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }
        public bool ProxyInvoice { get; set; }
    }

    public interface IDownloadConfigView
    {
        event EventHandler InputChanged;
        event EventHandler ClearFiltersRequested;
        event EventHandler StartRequested;

        DownloadConfigInput ReadInput();

        /// <summary>Đặt giá trị mặc định ban đầu: mua vào, kỳ Năm của năm hiện tại.</summary>
        void ApplyDefaults(DateTime today);

        /// <summary>Chỉ hiện các trường lọc mà cổng Thuế hỗ trợ (tên trường của <c>InvoiceSearchFilter</c>).</summary>
        void ShowSupportedFilters(ISet<string> supportedFields);

        void ClearFilters();

        /// <summary>Đổi nhãn theo chiều tra cứu: bán ra lọc theo MST người mua, mua vào theo MST người bán.</summary>
        void ShowDirectionLabels(InvoiceDirection direction);

        /// <summary>Lỗi giá trị bộ lọc (sai định dạng); rỗng khi hợp lệ.</summary>
        void ShowFilterMessage(string message);

        void ShowPlan(IList<DateSegment> segments);
        void ShowPeriodMessages(IList<string> errors, IList<string> warnings);
        void ShowStartState(bool canStart, string reason);
        void ShowStorageState(bool writable, string text);
    }
}
