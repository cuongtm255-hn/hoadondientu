using System;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>Bản thể hiện đã dàn trang của một hóa đơn: cùng một bố cục cho PDF, xem trước và in (G-03, ADR 0009).</summary>
    public interface IInvoiceDocument
    {
        int PageCount { get; }

        /// <summary>Tệp PDF (A4) của toàn bộ hóa đơn.</summary>
        byte[] ToPdf();

        /// <summary>Ảnh PNG của một trang (từ 0) ở độ phân giải <paramref name="dpi"/>: ~96 để xem trước, ~300 để in.</summary>
        byte[] RenderPagePng(int pageIndex, int dpi);
    }

    /// <summary>Dựng <see cref="IInvoiceDocument"/> từ <see cref="InvoicePrintModel"/>; implementation ở Infrastructure (thư viện PDF).</summary>
    public interface IInvoiceDocumentRenderer
    {
        IInvoiceDocument Build(InvoicePrintModel model);
    }

    /// <summary>Kết quả chuẩn bị bản thể hiện từ XML đã lưu: hoặc có mô hình để dựng, hoặc có lý do tiếng Việt vì sao không dựng được.</summary>
    public sealed class InvoicePrintResult
    {
        public InvoicePrintModel Model { get; set; }

        /// <summary>null khi dựng được; ngược lại là cảnh báo cho người dùng (file mất, quá lớn, bị sửa, không phải hóa đơn).</summary>
        public string Problem { get; set; }

        public bool IsReady
        {
            get { return Model != null; }
        }
    }
}
