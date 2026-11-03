using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using HoaDonDienTu.Application.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>
    /// Kiểm chữ ký số trên file XML hóa đơn thật đã làm sạch trong <c>docs/api-samples/invoice.zip</c> (gói xuất của cổng; không gọi cổng) và các biến thể bị sửa.
    /// </summary>
    [TestFixture]
    public class InvoiceSignatureVerifierTests
    {
        private static byte[] SampleXml()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "api-samples", "invoice.zip"))) dir = dir.Parent;
            if (dir == null) Assert.Ignore("Không thấy docs/api-samples/invoice.zip.");

            using (var zip = ZipFile.OpenRead(Path.Combine(dir.FullName, "docs", "api-samples", "invoice.zip")))
            using (var stream = zip.GetEntry("invoice.xml").Open())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        [Test]
        public void TheSignedSampleFromThePortal_IsValid()
        {
            Assert.That(InvoiceSignatureVerifier.VerifySeller(SampleXml()), Is.EqualTo(InvoiceSignatureStatus.Valid));
        }

        [Test]
        public void ChangingTheInvoiceAfterSigning_MakesTheSignatureInvalid()
        {
            var text = Encoding.UTF8.GetString(SampleXml());
            Assert.That(text, Does.Contain("<SHDon>40</SHDon>"), "mẫu đổi thì sửa test");
            var tampered = Encoding.UTF8.GetBytes(text.Replace("<SHDon>40</SHDon>", "<SHDon>41</SHDon>"));

            Assert.That(InvoiceSignatureVerifier.VerifySeller(tampered), Is.EqualTo(InvoiceSignatureStatus.Invalid));
        }

        [Test]
        public void ChangingTheAmountAfterSigning_MakesTheSignatureInvalid()
        {
            var text = Encoding.UTF8.GetString(SampleXml());
            var tampered = Encoding.UTF8.GetBytes(text.Replace("<TgTTTBSo>3103181</TgTTTBSo>", "<TgTTTBSo>1</TgTTTBSo>"));

            Assert.That(text, Does.Contain("<TgTTTBSo>3103181</TgTTTBSo>"));
            Assert.That(InvoiceSignatureVerifier.VerifySeller(tampered), Is.EqualTo(InvoiceSignatureStatus.Invalid));
        }

        [Test]
        public void ParserSetsTheStatusFromTheSameBytes()
        {
            var model = InvoicePrintModelParser.Parse(SampleXml());

            Assert.That(model.SellerSignatureStatus, Is.EqualTo(InvoiceSignatureStatus.Valid));
        }

        [Test]
        public void XmlWithoutASellerSignature_IsNone()
        {
            var xml = Encoding.UTF8.GetBytes("<HDon><DLHDon><TTChung><SHDon>1</SHDon></TTChung></DLHDon></HDon>");

            Assert.That(InvoiceSignatureVerifier.VerifySeller(xml), Is.EqualTo(InvoiceSignatureStatus.None));
        }

        [TestCase("")]
        [TestCase("<HDon><broken>")]
        [TestCase("not xml")]
        public void UnreadableXml_IsNotChecked_NeverThrows(string text)
        {
            Assert.That(InvoiceSignatureVerifier.VerifySeller(Encoding.UTF8.GetBytes(text)), Is.EqualTo(InvoiceSignatureStatus.NotChecked));
        }

        [Test]
        public void NullBytes_AreNotChecked()
        {
            Assert.That(InvoiceSignatureVerifier.VerifySeller(null), Is.EqualTo(InvoiceSignatureStatus.NotChecked));
        }

        [Test]
        public void ASignatureThatPointsOutsideTheDocument_IsNotExecuted()
        {
            const string xml =
                "<HDon><DSCKS><NBan><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><SignedInfo>" +
                "<CanonicalizationMethod Algorithm=\"http://www.w3.org/TR/2001/REC-xml-c14n-20010315\"/>" +
                "<SignatureMethod Algorithm=\"http://www.w3.org/2001/04/xmldsig-more#rsa-sha256\"/>" +
                "<Reference URI=\"file:///C:/Windows/win.ini\"><DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"/><DigestValue>AAAA</DigestValue></Reference>" +
                "</SignedInfo><SignatureValue>AAAA</SignatureValue></Signature></NBan></DSCKS></HDon>";

            Assert.That(InvoiceSignatureVerifier.VerifySeller(Encoding.UTF8.GetBytes(xml)), Is.EqualTo(InvoiceSignatureStatus.NotChecked));
        }

        [Test]
        public void DtdIsRefused_SoItIsNotChecked()
        {
            const string xml = "<!DOCTYPE HDon [<!ENTITY x SYSTEM \"file:///C:/Windows/win.ini\">]><HDon>&x;</HDon>";

            Assert.That(InvoiceSignatureVerifier.VerifySeller(Encoding.UTF8.GetBytes(xml)), Is.EqualTo(InvoiceSignatureStatus.NotChecked));
        }
    }
}
