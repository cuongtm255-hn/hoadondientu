using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>Yêu cầu mở SCR-05 với bộ lọc cho sẵn (ví dụ từ thẻ "Cần xử lý" của Tổng quan); bỏ qua bộ lọc đã nhớ và mọi năm đều được xem.</summary>
    public sealed class ArchiveNavigationRequest
    {
        public ArchiveNavigationRequest(InvoiceDirection direction, FileIntegrityFilter fileFilter, int year = 0)
        {
            Direction = direction;
            FileFilter = fileFilter;
            Year = year;
        }

        /// <summary>Năm lập hóa đơn cần xem; 0 là mọi năm. Chỉ dùng khi kho có hóa đơn năm đó.</summary>
        public int Year { get; private set; }

        public InvoiceDirection Direction { get; private set; }
        public FileIntegrityFilter FileFilter { get; private set; }
    }
}
