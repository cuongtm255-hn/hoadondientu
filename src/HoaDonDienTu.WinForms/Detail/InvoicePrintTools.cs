using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>In một bản thể hiện hóa đơn qua hộp thoại in của Windows (FR-DETAIL-006).</summary>
    public interface IInvoicePrinter
    {
        /// <summary>false khi người dùng hủy hộp thoại in.</summary>
        bool Print(IInvoiceDocument document, string jobName);
    }

    /// <summary>
    /// Gom các thứ SCR-07 cần để dựng, in và lưu PDF (ADR 0009); không truyền thì tab PDF báo chưa khả dụng.
    /// </summary>
    public sealed class InvoicePrintTools
    {
        public InvoicePrintTools(IInvoiceDocumentRenderer renderer, IExportSavePicker savePicker, IInvoicePrinter printer)
        {
            if (renderer == null) throw new ArgumentNullException("renderer");
            if (savePicker == null) throw new ArgumentNullException("savePicker");
            if (printer == null) throw new ArgumentNullException("printer");
            Renderer = renderer;
            SavePicker = savePicker;
            Printer = printer;
        }

        public IInvoiceDocumentRenderer Renderer { get; private set; }
        public IExportSavePicker SavePicker { get; private set; }
        public IInvoicePrinter Printer { get; private set; }
    }

    /// <summary>
    /// In bằng <see cref="PrintDocument"/>: mỗi trang là ảnh 300 dpi của đúng bố cục đã dùng cho PDF và xem trước, kéo vừa trang giấy.
    /// </summary>
    public sealed class WindowsInvoicePrinter : IInvoicePrinter
    {
        private const int PrintDpi = 300;

        public bool Print(IInvoiceDocument document, string jobName)
        {
            if (document == null) throw new ArgumentNullException("document");
            var page = 0;
            using (var printDocument = new PrintDocument { DocumentName = jobName })
            {
                printDocument.PrintPage += (sender, e) =>
                {
                    using (var image = Image.FromStream(new MemoryStream(document.RenderPagePng(page, PrintDpi))))
                    {
                        e.Graphics.DrawImage(image, e.PageBounds);
                    }

                    page++;
                    e.HasMorePages = page < document.PageCount;
                };

                using (var dialog = new PrintDialog { Document = printDocument, AllowSomePages = false, UseEXDialog = true })
                {
                    if (dialog.ShowDialog(Form.ActiveForm) != DialogResult.OK) return false;
                    printDocument.Print();
                    return true;
                }
            }
        }
    }
}
