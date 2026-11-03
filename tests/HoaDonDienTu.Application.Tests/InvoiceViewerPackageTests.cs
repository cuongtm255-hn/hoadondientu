using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using HoaDonDienTu.Application.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>
    /// Gói xem hóa đơn dựng từ file XML đã làm sạch của cổng trong <c>docs/api-samples/invoice.zip</c> (không gọi cổng) và so với trang <c>invoice.html</c> mà cổng đóng gói sẵn.
    /// </summary>
    [TestFixture]
    public class InvoiceViewerPackageTests
    {
        private static Func<string, byte[]> OpenSample()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "api-samples", "invoice.zip"))) dir = dir.Parent;
            if (dir == null) Assert.Ignore("Không thấy docs/api-samples/invoice.zip.");
            var path = Path.Combine(dir.FullName, "docs", "api-samples", "invoice.zip");
            return name =>
            {
                using (var zip = ZipFile.OpenRead(path))
                using (var stream = zip.GetEntry(name).Open())
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            };
        }

        private static string Text(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        }

        /// <summary>Phần chữ nhìn thấy: bỏ thẻ, giải mã ký tự HTML, gộp khoảng trắng (so nội dung, không so từng byte HTML).</summary>
        private static string Visible(string html)
        {
            var body = html.Substring(html.IndexOf("<body", StringComparison.Ordinal));
            body = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", " ");
            body = System.Net.WebUtility.HtmlDecode(body).Replace(' ', ' ');
            return System.Text.RegularExpressions.Regex.Replace(body, @"\s+", " ").Trim();
        }

        [Test]
        public void TheGeneratedPackage_HasTheSameFilesAsThePortalsExport()
        {
            var sample = OpenSample();

            var files = InvoiceViewerPackage.Files(sample("invoice.xml"));

            Assert.That(files.Select(f => f.Name), Is.EquivalentTo(new[] { "invoice.xml", "invoice.html", "details.js", "sign-check.jpg", "viewinvoice-bg.jpg" }));

            // Xem tận mắt: đặt HDDT_PDF_PREVIEW_DIR rồi mở invoice.html trong trình duyệt để so với trang của cổng.
            var preview = Environment.GetEnvironmentVariable("HDDT_PDF_PREVIEW_DIR");
            if (!string.IsNullOrEmpty(preview))
            {
                var folder = Path.Combine(preview, "viewer");
                Directory.CreateDirectory(folder);
                foreach (var file in files) File.WriteAllBytes(Path.Combine(folder, file.Name), file.Content);
            }
        }

        [Test]
        public void TheStaticFiles_AreTheSamePortalFilesByteForByte()
        {
            var sample = OpenSample();
            var files = InvoiceViewerPackage.Files(sample("invoice.xml")).ToDictionary(f => f.Name, f => f.Content);

            Assert.That(files["invoice.xml"], Is.EqualTo(sample("invoice.xml")));
            Assert.That(files["details.js"], Is.EqualTo(sample("details.js")));
            Assert.That(files["sign-check.jpg"], Is.EqualTo(sample("sign-check.jpg")));
            Assert.That(files["viewinvoice-bg.jpg"], Is.EqualTo(sample("viewinvoice-bg.jpg")));
        }

        [Test]
        public void TheGeneratedHtml_ShowsTheSameTextAsThePortalsHtml()
        {
            var sample = OpenSample();

            var generated = Visible(Text(InvoiceViewerPackage.Files(sample("invoice.xml")).Single(f => f.Name == "invoice.html").Content));
            var portal = Visible(Text(sample("invoice.html")));

            // Cổng ghi cả chuỗi chứng thư (OID..., CN=..., OU=...) rồi details.js rút còn CN khi mở trang; ta ghi sẵn CN.
            portal = System.Text.RegularExpressions.Regex.Replace(portal, @"OID\.\S+ CN=(.+?), OU=.+? C=VN", "CN=$1");
            Assert.That(generated, Is.EqualTo(portal));
        }

        [Test]
        public void TheGeneratedHtml_KeepsThePortalsHeadAndClasses()
        {
            var sample = OpenSample();
            var html = Text(InvoiceViewerPackage.Files(sample("invoice.xml")).Single(f => f.Name == "invoice.html").Content);
            var portal = Text(sample("invoice.html"));

            var head = portal.Substring(0, portal.IndexOf("<body>", StringComparison.Ordinal));
            Assert.That(html, Does.StartWith(head), "Phần đầu (CSS, script details.js) giống hệt cổng");
            foreach (var cls in new[] { "main-page", "heading-content", "title-heading", "list-fill-out", "data-item", "di-label", "di-value", "res-tb", "table-horizontal-wrapper", "ft-sign", "sign-box", "fd-end" })
            {
                Assert.That(html, Does.Contain("class=\"" + cls), cls);
            }

            Assert.That(html, Does.Contain("<script type=\"text/javascript\" src=\"details.js\"></script>"));
            Assert.That(html, Does.Contain("url(viewinvoice-bg.jpg)"));
        }

        [Test]
        public void ASignatureThatVerifies_ShowsSignatureValid_ATamperedOneShowsInvalid()
        {
            var sample = OpenSample();
            var xml = sample("invoice.xml");
            var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(xml).Replace("<SHDon>40</SHDon>", "<SHDon>41</SHDon>"));

            var valid = Text(InvoiceViewerPackage.Files(xml).Single(f => f.Name == "invoice.html").Content);
            var invalid = Text(InvoiceViewerPackage.Files(tampered).Single(f => f.Name == "invoice.html").Content);

            Assert.That(valid, Does.Contain("<span>Signature Valid</span>"));
            Assert.That(invalid, Does.Contain("<span>Signature Invalid</span>"));
            Assert.That(invalid, Does.Not.Contain("Signature Valid"));
        }

        [Test]
        public void UserTextIsEscaped_SoAnXmlValueCannotInjectScript()
        {
            const string xml = "<HDon><DLHDon><TTChung><KHMSHDon>1</KHMSHDon><SHDon>1</SHDon></TTChung><NDHDon><NBan><Ten>&lt;script&gt;alert(1)&lt;/script&gt;</Ten></NBan></NDHDon></DLHDon></HDon>";

            var html = Text(InvoiceViewerPackage.Files(Encoding.UTF8.GetBytes(xml)).Single(f => f.Name == "invoice.html").Content);
            var body = html.Substring(html.IndexOf("<body>", StringComparison.Ordinal));

            Assert.That(body, Does.Not.Contain("<script>alert"));
            Assert.That(body, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt;"));
        }

        [Test]
        public void XmlThatIsNotAnInvoice_GivesOnlyTheXml()
        {
            var files = InvoiceViewerPackage.Files(Encoding.UTF8.GetBytes("<Khac><A>1</A></Khac>"));

            Assert.That(files.Select(f => f.Name), Is.EqualTo(new[] { "invoice.xml" }));
        }

        [Test]
        public void ThePortalBackgroundIsAvailableForThePdf()
        {
            Assert.That(InvoiceViewerPackage.Background(), Is.Not.Null.And.Not.Empty);
        }
    }
}
