using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>Yêu cầu mở SCR-05 với bộ lọc cho sẵn (ví dụ từ thẻ "Cần xử lý" của Tổng quan); bỏ qua bộ lọc đã nhớ và mọi năm đều được xem.</summary>
    public sealed class ArchiveNavigationRequest
    {
        public ArchiveNavigationRequest(InvoiceDirection direction, FileIntegrityFilter fileFilter)
        {
            Direction = direction;
            FileFilter = fileFilter;
        }

        public InvoiceDirection Direction { get; private set; }
        public FileIntegrityFilter FileFilter { get; private set; }
    }
}
