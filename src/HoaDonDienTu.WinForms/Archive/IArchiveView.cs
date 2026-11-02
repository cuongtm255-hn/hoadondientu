using System;
using System.Collections.Generic;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>Giá trị đang chọn trên thanh lọc của SCR-05.</summary>
    public sealed class ArchiveFilterInput
    {
        public InvoiceDirection Direction { get; set; }
        public string QuickSearch { get; set; }

        /// <summary>0 là mọi năm.</summary>
        public int Year { get; set; }

        public string StatusCode { get; set; }
        public string CheckResultCode { get; set; }
        public FileIntegrityFilter FileFilter { get; set; }
    }

    public sealed class SelectionChangedEventArgs : EventArgs
    {
        public SelectionChangedEventArgs(IList<Guid> invoiceIds)
        {
            InvoiceIds = invoiceIds;
        }

        /// <summary>Hóa đơn của các dòng đang chọn trên trang hiện tại, theo thứ tự hiển thị.</summary>
        public IList<Guid> InvoiceIds { get; private set; }
    }

    public sealed class InvoiceRequestedEventArgs : EventArgs
    {
        public InvoiceRequestedEventArgs(Guid invoiceId)
        {
            InvoiceId = invoiceId;
        }

        public Guid InvoiceId { get; private set; }
    }

    public sealed class ArchiveRowViewModel
    {
        public Guid InvoiceId { get; set; }
        public string Number { get; set; }
        public string IssueDate { get; set; }
        public string CounterpartyId { get; set; }
        public string CounterpartyName { get; set; }
        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }
        public string InvoiceNumber { get; set; }
        public string Status { get; set; }
        public string FileText { get; set; }
        public ArchiveFileState FileState { get; set; }
        public string BeforeTax { get; set; }
        public string Tax { get; set; }
        public string Payment { get; set; }
    }

    public sealed class ArchiveViewModel
    {
        /// <summary>"MST người bán" cho mua vào, "MST người mua" cho bán ra.</summary>
        public string CounterpartyIdCaption { get; set; }

        public string CounterpartyNameCaption { get; set; }
        public string ResultCount { get; set; }
        public string ResultSub { get; set; }
        public string TotalBeforeTax { get; set; }
        public string TotalTax { get; set; }
        public string TotalPayment { get; set; }
        public string FooterText { get; set; }
        public string PageText { get; set; }
        public bool CanPrevious { get; set; }
        public bool CanNext { get; set; }
        public ArchiveSortKey SortKey { get; set; }
        public bool SortDescending { get; set; }
        public IList<ArchiveRowViewModel> Rows { get; set; }
    }

    public sealed class PageRequestedEventArgs : EventArgs
    {
        public PageRequestedEventArgs(int delta)
        {
            Delta = delta;
        }

        /// <summary>-1 trang trước, +1 trang sau.</summary>
        public int Delta { get; private set; }
    }

    public sealed class SortRequestedEventArgs : EventArgs
    {
        public SortRequestedEventArgs(ArchiveSortKey key)
        {
            Key = key;
        }

        public ArchiveSortKey Key { get; private set; }
    }

    public interface IArchiveView
    {
        event EventHandler FilterChanged;
        event EventHandler ClearRequested;
        event EventHandler CheckFilesRequested;
        event EventHandler<PageRequestedEventArgs> PageRequested;
        event EventHandler<SortRequestedEventArgs> SortRequested;

        /// <summary>Người dùng nhấp đôi một dòng để xem chi tiết hóa đơn.</summary>
        event EventHandler<InvoiceRequestedEventArgs> InvoiceRequested;

        /// <summary>Phát khi tập dòng chọn đổi, và luôn một lần (rỗng) sau mỗi lần hiển thị kết quả mới.</summary>
        event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        /// <summary>Người dùng ẩn/hiện một cột.</summary>
        event EventHandler ColumnsChanged;

        /// <summary>Mã các cột đang ẩn.</summary>
        IList<string> HiddenColumns { get; }

        /// <summary>Khôi phục cột ẩn (khi quay lại màn hình) mà không phát ColumnsChanged.</summary>
        void ShowHiddenColumns(IList<string> hidden);

        /// <summary>Số dòng đang chọn, để nút xuất và chân trang nói rõ phạm vi.</summary>
        void ShowSelectionCount(int count);

        event EventHandler ExportExcelRequested;
        event EventHandler ExportZipRequested;

        /// <summary>Khóa/mở hai nút xuất theo quyền (ReadExport).</summary>
        void ShowExportAllowed(bool allowed);

        ArchiveFilterInput ReadFilter();

        /// <summary>Đặt lại bộ lọc (khôi phục trạng thái khi quay lại màn hình) mà không phát FilterChanged.</summary>
        void ShowFilter(ArchiveFilterInput filter);

        /// <summary>Danh sách năm có dữ liệu của chiều hiện tại (mục "Tất cả năm" luôn có thêm).</summary>
        void ShowYears(IList<int> years, int selected);

        void ShowResult(ArchiveViewModel model);
        void ShowMessage(string text, bool isError);
    }
}
