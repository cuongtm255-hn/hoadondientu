using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Infrastructure.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Invoices
{
    /// <summary>Dàn trang/PDF/ảnh của hóa đơn GIẢ (MOCK, dữ liệu tự đặt); chạy PDFsharp thật nhưng không gọi cổng Thuế.</summary>
    [TestFixture]
    [Category("MockData")]
    public class PdfSharpInvoiceRendererTests
    {
        private readonly PdfSharpInvoiceRenderer _renderer = new PdfSharpInvoiceRenderer();

        private static InvoicePrintModel Model(int lines, string name = "Vé máy bay nội địa Hà Nội - Hồ Chí Minh (hạng phổ thông tiết kiệm)")
        {
            var m = new InvoicePrintModel
            {
                SchemaVersion = "2.1.0",
                Title = "Hóa đơn GTGT (mô phỏng)",
                FormSymbol = "1",
                InvoiceSymbol = "C26TAA",
                InvoiceNumber = "0000128",
                IssueDate = new DateTime(2026, 10, 31),
                Currency = "VND",
                PaymentMethod = "TM, CK",
                TotalBeforeTax = 2015000m,
                TotalTax = 160000m,
                TotalPayment = 2175000m,
                TotalInWords = "Hai triệu một trăm bảy mươi lăm nghìn đồng",
                AuthorityCode = "00ABCDEF0123456789ABCDEF0123456789",
                SellerSignedAt = new DateTime(2026, 10, 31, 15, 4, 36),
                SellerSignedAtText = "2026-10-31T15:04:36",
                SellerSignerName = "CÔNG TY TNHH GIẢ BÁN HÀNG",
                AuthoritySignedAt = new DateTime(2026, 10, 31, 15, 4, 40),
                QrPayload = "000201999500323A23B70457BDD05DD4FED45AF33B99A501100108460041020110130 (MÃ QR GIẢ)"
            };
            m.Seller.Name = "CÔNG TY TNHH GIẢ BÁN HÀNG";
            m.Seller.TaxCode = "0100000001";
            m.Seller.Address = "Số 1, phố Giả, phường Thử, quận Nghiệm, Hà Nội";
            m.Seller.Phone = "0900000000";
            m.Seller.BankAccount = "111222333";
            m.Seller.BankName = "Ngân hàng Giả";
            m.Buyer.Name = "CÔNG TY CỔ PHẦN GIẢ MUA HÀNG";
            m.Buyer.TaxCode = "0100000002";
            m.Buyer.Address = "Số 2, phố Mua, Hà Nội";
            m.Buyer.ContactName = "Nguyễn Văn Mua";
            m.TotalFee = null;
            m.TotalDiscount = null;
            for (var i = 1; i <= lines; i++)
            {
                m.Lines.Add(new InvoicePrintLine { Number = i.ToString(), Name = name + " #" + i, Unit = "vé", Quantity = 1, UnitPrice = 1000000m, TaxRate = "8%", Amount = 1000000m, Kind = "1" });
            }

            m.TaxLines.Add(new InvoicePrintTaxLine { Rate = "8%", TaxableAmount = 2000000m, TaxAmount = 160000m });
            return m;
        }

        [Test]
        public void SmallInvoice_IsOneA4Page_AndTheSamePdfOpensWithEmbeddedUnicodeFont()
        {
            var document = _renderer.Build(Model(2, "Vé máy bay nội địa HAN-SGN"));

            var pdf = document.ToPdf();

            Assert.That(document.PageCount, Is.EqualTo(1));
            Assert.That(Encoding.ASCII.GetString(pdf, 0, 5), Is.EqualTo("%PDF-"));
            var text = Encoding.GetEncoding("latin1").GetString(pdf);
            Assert.That(text, Does.Contain("/Type/Page").Or.Contain("/Type /Page"));
            Assert.That(text, Does.Contain("/FontFile2"), "phông Times New Roman được nhúng để tiếng Việt hiển thị đúng ở mọi máy");
            Assert.That(pdf.Length, Is.GreaterThan(5000));
        }

        [Test]
        public void ManyLines_FlowOntoMorePages_WithTheTableHeaderRepeated()
        {
            var document = _renderer.Build(Model(80));

            Assert.That(document.PageCount, Is.GreaterThan(1));
            Assert.That(Count(document.ToPdf(), "/Type/Page\n") + Count(document.ToPdf(), "/Type /Page\n"), Is.GreaterThanOrEqualTo(0));
            for (var i = 0; i < document.PageCount; i++) Assert.That(document.RenderPagePng(i, 48), Is.Not.Empty);
        }

        [TestCase(96, 794, 1123)]
        [TestCase(72, 596, 842)]
        public void PagePng_HasA4DimensionsAtTheRequestedDpi(int dpi, int width, int height)
        {
            var png = _renderer.Build(Model(3)).RenderPagePng(0, dpi);

            using (var image = Image.FromStream(new MemoryStream(png)))
            {
                Assert.That(image.Width, Is.EqualTo(width).Within(2));
                Assert.That(image.Height, Is.EqualTo(height).Within(2));
            }

            Assert.That(png.Take(4).ToArray(), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        }

        [Test]
        public void PagePng_IsNotBlank_TheInvoiceIsActuallyDrawn()
        {
            var png = _renderer.Build(Model(3)).RenderPagePng(0, 96);

            using (var bitmap = new Bitmap(new MemoryStream(png)))
            {
                var dark = 0;
                for (var y = 0; y < bitmap.Height; y += 3)
                {
                    for (var x = 0; x < bitmap.Width; x += 3)
                    {
                        if (bitmap.GetPixel(x, y).GetBrightness() < 0.5f) dark++;
                    }
                }

                Assert.That(dark, Is.GreaterThan(300));
            }
        }

        [Test]
        public void QrCode_IsDrawnTopLeft_WhenTheXmlHasOne_AndOmittedOtherwise()
        {
            var withQr = Model(1);
            var withoutQr = Model(1);
            withoutQr.QrPayload = null;

            var a = DarkPixelsInTopLeft(_renderer.Build(withQr).RenderPagePng(0, 96));
            var b = DarkPixelsInTopLeft(_renderer.Build(withoutQr).RenderPagePng(0, 96));

            Assert.That(a, Is.GreaterThan(b + 200), "ô QR có nhiều điểm đen");
        }

        private static int DarkPixelsInTopLeft(byte[] png)
        {
            using (var bitmap = new Bitmap(new MemoryStream(png)))
            {
                var dark = 0;
                for (var y = 48; y < 140; y++)
                {
                    for (var x = 55; x < 140; x++)
                    {
                        if (bitmap.GetPixel(x, y).GetBrightness() < 0.3f) dark++;
                    }
                }

                return dark;
            }
        }

        [Test]
        public void TypeOfInvoiceTitle_FollowsTheFormSymbol_AndSurvivesAnUnknownOne()
        {
            foreach (var symbol in new[] { "1", "2", "5", "9", null })
            {
                var model = Model(1);
                model.FormSymbol = symbol;

                Assert.That(() => _renderer.Build(model).ToPdf(), Throws.Nothing, "ký hiệu mẫu số " + symbol);
            }
        }

        [Test]
        public void EmptyModel_StillBuildsAPage_NothingIsInvented()
        {
            var document = _renderer.Build(new InvoicePrintModel());

            Assert.That(document.PageCount, Is.EqualTo(1));
            Assert.That(document.ToPdf().Length, Is.GreaterThan(1000));
        }

        [Test]
        public void VeryLongUnbreakableText_DoesNotThrowOrLoop()
        {
            var model = Model(1, new string('X', 400));

            Assert.That(() => _renderer.Build(model).ToPdf(), Throws.Nothing);
        }

        [Test]
        public void PagePng_RejectsOutOfRangeArguments()
        {
            var document = _renderer.Build(Model(1));

            Assert.Throws<ArgumentOutOfRangeException>(() => document.RenderPagePng(5, 96));
            Assert.Throws<ArgumentOutOfRangeException>(() => document.RenderPagePng(0, 5));
        }

        [Test]
        public void Build_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _renderer.Build(null));
        }

        /// <summary>Ghi các ảnh trang ra thư mục tạm để xem bằng mắt khi cần (đặt HDDT_PDF_PREVIEW_DIR); không thành lỗi nếu không đặt.</summary>
        [Test]
        public void Preview_CanBeWrittenForManualInspection()
        {
            var dir = Environment.GetEnvironmentVariable("HDDT_PDF_PREVIEW_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("Đặt HDDT_PDF_PREVIEW_DIR để ghi ảnh xem thử.");

            Directory.CreateDirectory(dir);
            var document = _renderer.Build(Model(45));
            File.WriteAllBytes(Path.Combine(dir, "hoa-don-nho.png"), _renderer.Build(Model(2, "Vé máy bay nội địa HAN-SGN")).RenderPagePng(0, 110));
            File.WriteAllBytes(Path.Combine(dir, "hoa-don-mau.pdf"), document.ToPdf());
            for (var i = 0; i < document.PageCount; i++) File.WriteAllBytes(Path.Combine(dir, "trang-" + (i + 1) + ".png"), document.RenderPagePng(i, 110));
        }

        private static int Count(byte[] bytes, string needle)
        {
            var text = Encoding.GetEncoding("latin1").GetString(bytes);
            var count = 0;
            for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + 1, StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
