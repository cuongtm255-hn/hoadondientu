using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Infrastructure.Invoices;
using HoaDonDienTu.WinForms.Detail;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Tab PDF của SCR-07 với ảnh trang thật từ bộ dàn trang thật, hóa đơn GIẢ (MOCK).</summary>
    [TestFixture]
    [Category("MockData")]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class InvoiceViewerPdfControlTests
    {
        private static IList<byte[]> RealPages()
        {
            var model = new InvoicePrintModel { Title = "Hóa đơn GTGT (mô phỏng)", InvoiceNumber = "0000128" };
            model.Seller.Name = "CÔNG TY GIẢ";
            model.Lines.Add(new InvoicePrintLine { Number = "1", Name = "Hàng giả", Quantity = 1, UnitPrice = 10m, Amount = 10m, TaxRate = "8%", Kind = "1" });
            var document = new PdfSharpInvoiceRenderer().Build(model);
            return Enumerable.Range(0, document.PageCount).Select(i => document.RenderPagePng(i, 96)).ToList();
        }

        [Test]
        public void ShowPdf_WithPages_ShowsThePreviewAndEnablesPrintAndSave()
        {
            using (var control = new InvoiceViewerControl())
            {
                control.ShowPdf(new InvoicePdfViewModel { Pages = RealPages(), CanPrintOrSave = true });

                var flow = control.Controls.Find("flowPdfPages", true).Single();
                Assert.That(flow.Controls.OfType<PictureBox>().Count(), Is.EqualTo(1));
                Assert.That(control.Controls.Find("btnPrint", true).Single().Enabled, Is.True);
                Assert.That(control.Controls.Find("btnSavePdf", true).Single().Enabled, Is.True);
            }
        }

        [Test]
        public void ShowPdf_WithANotice_HidesThePagesAndDisablesTheButtons()
        {
            using (var control = new InvoiceViewerControl())
            {
                control.ShowPdf(new InvoicePdfViewModel { Pages = RealPages(), CanPrintOrSave = true });

                control.ShowPdf(new InvoicePdfViewModel { Pages = new List<byte[]>(), Notice = "Không dựng được.", IsError = true });

                Assert.That(control.Controls.Find("flowPdfPages", true).Single().Controls.Count, Is.EqualTo(0));
                var label = control.Controls.Find("lblPdf", true).Single();
                Assert.That(label.Text, Is.EqualTo("Không dựng được."));
                Assert.That(control.Controls.Find("btnPrint", true).Single().Enabled, Is.False);
                Assert.That(control.Controls.Find("btnSavePdf", true).Single().Enabled, Is.False);
            }
        }

        [Test]
        public void ShowPdf_CalledTwice_ReplacesThePagesInsteadOfStackingThem()
        {
            using (var control = new InvoiceViewerControl())
            {
                control.ShowPdf(new InvoicePdfViewModel { Pages = RealPages(), CanPrintOrSave = true });
                control.ShowPdf(new InvoicePdfViewModel { Pages = RealPages(), CanPrintOrSave = true });

                Assert.That(control.Controls.Find("flowPdfPages", true).Single().Controls.Count, Is.EqualTo(1));
            }
        }
    }
}
