using System.Linq;
using System.Windows.Forms;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.WinForms.Archive;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class IntegrityReportFormTests
    {
        [Test]
        public void CleanTempsButton_ShowsOnlyWhenThereAreTempsAndACleanerIsGiven()
        {
            var withTemps = new IntegrityReport();
            withTemps.StrayTemps.Add(@"2026\a.tmp");
            var withoutTemps = new IntegrityReport();
            withoutTemps.Orphans.Add(@"2026\x.xml");

            using (var form = new IntegrityReportForm(withTemps, paths => paths))
            {
                form.Show();
                Assert.That(form.Controls.Find("btnCleanTemps", true).Single().Visible, Is.True);
            }

            using (var form = new IntegrityReportForm(withTemps))
            {
                form.Show();
                Assert.That(form.Controls.Find("btnCleanTemps", true).Single().Visible, Is.False);
            }

            using (var form = new IntegrityReportForm(withoutTemps, paths => paths))
            {
                form.Show();
                Assert.That(form.Controls.Find("btnCleanTemps", true).Single().Visible, Is.False);
            }
        }

        [Test]
        public void TheForm_ListsEveryProblemWithItsInvoiceAndFile_AndStatesItChangesNothing()
        {
            var report = new IntegrityReport { VersionsChecked = 5 };
            report.Problems.Add(new IntegrityProblem { InvoiceNumber = "0000128", InvoiceSymbol = "C26TAA", VersionNo = 1, RelativePath = @"2026\10\128.xml", Kind = IntegrityProblemKind.HashMismatch });
            report.Orphans.Add(@"2026\10\stray.xml");

            using (var form = new IntegrityReportForm(report))
            {
                var list = form.Controls.OfType<ListView>().Single();
                var note = form.Controls.Find("lblNote", true).Single().Text;
                var summary = form.Controls.Find("lblSummary", true).Single().Text;

                Assert.That(list.Items.Count, Is.EqualTo(2));
                Assert.That(list.Items[0].SubItems[1].Text, Is.EqualTo("Số 0000128 · C26TAA"));
                Assert.That(list.Items[0].SubItems[3].Text, Is.EqualTo(@"2026\10\128.xml"));
                Assert.That(summary, Does.Contain("5 file XML").And.Contain("1 bị đổi nội dung").And.Contain("1 file lạ"));
                Assert.That(note, Does.Contain("không tự sửa hay xóa"));

                // Xem tận mắt: đặt HDDT_PDF_PREVIEW_DIR để lưu ảnh hộp thoại.
                var preview = System.Environment.GetEnvironmentVariable("HDDT_PDF_PREVIEW_DIR");
                if (!string.IsNullOrEmpty(preview))
                {
                    form.Show();
                    using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                        bitmap.Save(System.IO.Path.Combine(preview, "integrity-form.png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            }
        }
    }
}
