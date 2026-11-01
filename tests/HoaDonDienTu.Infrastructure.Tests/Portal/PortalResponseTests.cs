using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class PortalResponseTests
    {
        private PortalApiConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = PortalApiConfigDefaults.Create();
        }

        [Test]
        public void RealSoldResponse_MapsEveryField()
        {
            var page = PortalResponseParser.ParseSearch(_config, RealSamples.SoldSearchResponse);

            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Items, Has.Count.EqualTo(2));
            var first = page.Items[0];
            Assert.That(first.PortalInvoiceId, Is.EqualTo("d2240f37-51af-48d9-98f1-f01d4a0810ff"));
            Assert.That(first.FormSymbol, Is.EqualTo("1"));
            Assert.That(first.InvoiceSymbol, Is.EqualTo("C26TBN"));
            Assert.That(first.InvoiceNumber, Is.EqualTo("40"));
            Assert.That(first.SellerTaxCode, Is.EqualTo("0109213133"));
            Assert.That(first.SellerName, Is.EqualTo("CÔNG TY TNHH BAY NGAY"));
            Assert.That(first.BuyerTaxCode, Is.EqualTo("0101654633"));
            Assert.That(first.BuyerName, Is.EqualTo("CÔNG TY CỔ PHẦN CÔNG NGHỆ SINH HỌC THÚ Y"));
            Assert.That(first.BuyerAddress, Does.StartWith("Số 19, ngõ 37"));
            Assert.That(first.TotalBeforeTax, Is.EqualTo(2873316m));
            Assert.That(first.TotalTax, Is.EqualTo(229865m));
            Assert.That(first.TotalPayment, Is.EqualTo(3103181m));
            Assert.That(first.TotalDiscount, Is.Null);
            Assert.That(first.TotalFee, Is.Null);
            Assert.That(first.Currency, Is.EqualTo("VND"));
            Assert.That(first.ExchangeRate, Is.EqualTo(1m));
            Assert.That(first.StatusRaw, Is.EqualTo("1"));
            Assert.That(first.CheckResultRaw, Is.EqualTo("5"));
        }

        [Test]
        public void IssueDate_IsConvertedFromUtcToVietnamTime_SoItMatchesTheExportedSheet()
        {
            // tdlap = 2026-10-30T17:00:00Z là 31/10/2026 (cột "Ngày lập" của bảng kê mẫu), không phải 30/10.
            var page = PortalResponseParser.ParseSearch(_config, RealSamples.SoldSearchResponse);

            Assert.That(page.Items[0].IssueDate, Is.EqualTo(new DateTime(2026, 10, 31)));
            Assert.That(page.Items[1].IssueDate, Is.EqualTo(new DateTime(2026, 10, 30)));
        }

        [Test]
        public void IndividualBuyer_HasIdNumber_AndNoTaxCode()
        {
            var second = PortalResponseParser.ParseSearch(_config, RealSamples.SoldSearchResponse).Items[1];

            Assert.That(second.BuyerTaxCode, Is.Null);
            Assert.That(second.BuyerIdNumber, Is.EqualTo("001092008840"));
        }

        [Test]
        public void InvoiceNumber_IsKeptAsText_FromNumericJson()
        {
            var page = PortalResponseParser.ParseSearch(_config, RealSamples.SoldSearchResponse);

            Assert.That(page.Items.Select(i => i.InvoiceNumber), Is.EqualTo(new[] { "40", "39" }));
        }

        [Test]
        public void NullState_MeansNoMorePages()
        {
            var page = PortalResponseParser.ParseSearch(_config, RealSamples.SoldSearchResponse);

            Assert.That(page.NextCursor, Is.Null);
            Assert.That(page.HasMore, Is.False);
        }

        [Test]
        public void State_IsTheNextCursor()
        {
            var body = RealSamples.SoldSearchResponse.Replace("\"state\":null", "\"state\":\"c3RhdGUtMg==\"");

            var page = PortalResponseParser.ParseSearch(_config, body);

            Assert.That(page.NextCursor, Is.EqualTo("c3RhdGUtMg=="));
            Assert.That(page.HasMore, Is.True);
        }

        [Test]
        public void EmptyResult_IsAValidEmptyPage()
        {
            var page = PortalResponseParser.ParseSearch(_config, "{\"datas\":[],\"total\":0,\"state\":null,\"time\":5}");

            Assert.That(page.Items, Is.Empty);
            Assert.That(page.TotalCount, Is.EqualTo(0));
            Assert.That(page.HasMore, Is.False);
        }

        [Test]
        public void MissingItemsArray_IsSchemaChanged()
        {
            var ex = Assert.Throws<PortalApiException>(() => PortalResponseParser.ParseSearch(_config, "{\"total\":0}"));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [TestCase("nbmst")]
        [TestCase("khhdon")]
        [TestCase("khmshdon")]
        [TestCase("shdon")]
        [TestCase("tdlap")]
        [TestCase("id")]
        public void MissingKeyField_IsSchemaChanged_AndMessageHasNoData(string field)
        {
            var body = RealSamples.SoldSearchResponse.Replace("\"" + field + "\":", "\"x" + field + "\":");

            var ex = Assert.Throws<PortalApiException>(() => PortalResponseParser.ParseSearch(_config, body));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
            Assert.That(ex.Message, Does.Not.Contain("0109213133"));
        }

        [Test]
        public void NotJson_IsSchemaChanged()
        {
            var ex = Assert.Throws<PortalApiException>(() => PortalResponseParser.ParseSearch(_config, "<html>oops</html>"));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [Test]
        public void UtcOffsetIsConfigurable()
        {
            _config.Search.Response.TimeZoneOffsetHours = 0;

            var page = PortalResponseParser.ParseSearch(_config, RealSamples.SoldSearchResponse);

            Assert.That(page.Items[0].IssueDate, Is.EqualTo(new DateTime(2026, 10, 30)));
        }

        // ---- ZIP xuất XML ----

        [Test]
        public void ExtractXml_ReturnsOnlyTheXmlEntry_IgnoringViewerFiles()
        {
            var zip = Zip("invoice.html", "<html/>", "details.js", "//js", "invoice.xml", "<HDon>x</HDon>");

            var xml = PortalResponseParser.ExtractXml(_config.Download, zip);

            Assert.That(Encoding.UTF8.GetString(xml), Is.EqualTo("<HDon>x</HDon>"));
        }

        [Test]
        public void ExtractXml_EntryNameIsCaseInsensitive()
        {
            var xml = PortalResponseParser.ExtractXml(_config.Download, Zip("Invoice.XML", "<HDon/>"));

            Assert.That(Encoding.UTF8.GetString(xml), Is.EqualTo("<HDon/>"));
        }

        [Test]
        public void ExtractXml_MissingEntry_IsSchemaChanged()
        {
            var ex = Assert.Throws<PortalApiException>(() => PortalResponseParser.ExtractXml(_config.Download, Zip("other.xml", "<a/>")));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [Test]
        public void ExtractXml_NotAZip_IsSchemaChanged()
        {
            var ex = Assert.Throws<PortalApiException>(() => PortalResponseParser.ExtractXml(_config.Download, Encoding.UTF8.GetBytes("not a zip at all")));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [Test]
        public void ExtractXml_EntryPathCannotEscape_BecauseNothingIsWrittenToDisk()
        {
            var zip = Zip("../../evil/invoice.xml", "<HDon>evil</HDon>");

            var xml = PortalResponseParser.ExtractXml(_config.Download, zip);

            Assert.That(Encoding.UTF8.GetString(xml), Is.EqualTo("<HDon>evil</HDon>"), "Chỉ trả về byte trong bộ nhớ, không giải nén ra đĩa");
        }

        [Test]
        public void RealExportZipFromThePortal_YieldsTheSignedInvoiceXml()
        {
            var path = FindSample("invoice.zip");
            if (path == null) Assert.Ignore("Không thấy docs/api-samples/invoice.zip");

            var xml = Encoding.UTF8.GetString(PortalResponseParser.ExtractXml(_config.Download, File.ReadAllBytes(path)));

            Assert.That(xml, Does.StartWith("<HDon>"));
            Assert.That(xml, Does.Contain("<KHHDon>C26TBN</KHHDon>"));
            Assert.That(xml, Does.Contain("<SHDon>40</SHDon>"));
            Assert.That(xml, Does.Contain("<DSCKS>"));
        }

        /// <summary>Tạo zip từ các cặp (tên entry, nội dung) liền nhau.</summary>
        private static byte[] Zip(params string[] nameAndContent)
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    for (var i = 0; i < nameAndContent.Length; i += 2)
                    {
                        var e = zip.CreateEntry(nameAndContent[i]);
                        using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(nameAndContent[i + 1]);
                    }
                }

                return stream.ToArray();
            }
        }

        private static string FindSample(string name)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "api-samples", name);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }
    }
}
