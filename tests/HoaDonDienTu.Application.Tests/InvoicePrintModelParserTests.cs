using System;
using System.Linq;
using System.Text;
using HoaDonDienTu.Application.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>Đọc XML hóa đơn GIẢ (MOCK, dữ liệu tự đặt, cấu trúc theo TT78) thành mô hình in.</summary>
    [TestFixture]
    [Category("MockData")]
    public class InvoicePrintModelParserTests
    {
        private const string Sample =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><HDon><DLHDon Id=\"data\">" +
            "<TTChung><PBan>2.1.0</PBan><THDon>Hóa đơn GTGT (mô phỏng)</THDon><KHMSHDon>1</KHMSHDon><KHHDon>C26TAA</KHHDon><SHDon>0000128</SHDon>" +
            "<NLap>2026-10-31</NLap><DVTTe>VND</DVTTe><TGia>1</TGia><HTTToan>TM, CK</HTTToan></TTChung>" +
            "<NDHDon>" +
            "<NBan><Ten>CÔNG TY GIẢ BÁN</Ten><MST>0100000001</MST><DChi>1 Phố Giả, Hà Nội</DChi><SDThoai>0900000000</SDThoai><DCTDTu>ban@example.invalid</DCTDTu><STKNHang>111222333</STKNHang><TNHang>Ngân hàng Giả</TNHang></NBan>" +
            "<NMua><Ten>Nguyễn Văn Mua</Ten><DChi>2 Phố Mua, Hà Nội</DChi><HVTNMHang>Nguyễn Văn Mua</HVTNMHang><CCCDan>001000000000</CCCDan></NMua>" +
            "<DSHHDVu>" +
            "<HHDVu><TChat>1</TChat><STT>1</STT><THHDVu>Vé máy bay nội địa</THHDVu><DVTinh>vé</DVTinh><SLuong>2</SLuong><DGia>1000000</DGia><STCKhau>0</STCKhau><TSuat>8%</TSuat><ThTien>2000000</ThTien></HHDVu>" +
            "<HHDVu><TChat>1</TChat><STT>2</STT><THHDVu>Phí dịch vụ</THHDVu><DVTinh>lần</DVTinh><SLuong>1.5</SLuong><DGia>10000.25</DGia><TSuat>KCT</TSuat><ThTien>15000.38</ThTien></HHDVu>" +
            "</DSHHDVu>" +
            "<TToan><THTTLTSuat><LTSuat><TSuat>8%</TSuat><TThue>160000</TThue><ThTien>2000000</ThTien></LTSuat><LTSuat><TSuat>KCT</TSuat><TThue>0</TThue><ThTien>15000.38</ThTien></LTSuat></THTTLTSuat>" +
            "<TgTCThue>2015000</TgTCThue><TgTThue>160000</TgTThue><TTCKTMai>5000</TTCKTMai><TgTTTBSo>2170000</TgTTTBSo><TgTTTBChu>Hai triệu một trăm bảy mươi nghìn đồng</TgTTTBChu></TToan>" +
            "</NDHDon></DLHDon><MCCQT>00ABCDEF0123456789ABCDEF0123456789</MCCQT>" +
            "<DSCKS><NBan><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><Object><SignatureProperties><SignatureProperty><SigningTime>2026-10-31T15:04:36</SigningTime></SignatureProperty></SignatureProperties></Object></Signature></NBan>" +
            "<CQT><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><Object><SignatureProperties><SignatureProperty><SigningTime>2026-10-31T15:04:40</SigningTime></SignatureProperty></SignatureProperties></Object></Signature></CQT></DSCKS></HDon>";

        private static InvoicePrintModel Parse(string xml)
        {
            return InvoicePrintModelParser.Parse(Encoding.UTF8.GetBytes(xml));
        }

        [Test]
        public void ReadsHeaderPartiesLinesAndTotals()
        {
            var m = Parse(Sample);

            Assert.That(m.Title, Is.EqualTo("Hóa đơn GTGT (mô phỏng)"));
            Assert.That(m.FormSymbol, Is.EqualTo("1"));
            Assert.That(m.InvoiceSymbol, Is.EqualTo("C26TAA"));
            Assert.That(m.InvoiceNumber, Is.EqualTo("0000128"), "giữ nguyên số 0 đầu");
            Assert.That(m.IssueDate, Is.EqualTo(new DateTime(2026, 10, 31)));
            Assert.That(m.Currency, Is.EqualTo("VND"));
            Assert.That(m.PaymentMethod, Is.EqualTo("TM, CK"));
            Assert.That(m.Seller.Name, Is.EqualTo("CÔNG TY GIẢ BÁN"));
            Assert.That(m.Seller.TaxCode, Is.EqualTo("0100000001"));
            Assert.That(m.Seller.BankAccount, Is.EqualTo("111222333"));
            Assert.That(m.Buyer.Name, Is.EqualTo("Nguyễn Văn Mua"));
            Assert.That(m.Buyer.TaxCode, Is.Null);
            Assert.That(m.Buyer.IdNumber, Is.EqualTo("001000000000"));
            Assert.That(m.Lines, Has.Count.EqualTo(2));
            Assert.That(m.Lines[0].Name, Is.EqualTo("Vé máy bay nội địa"));
            Assert.That(m.Lines[0].Quantity, Is.EqualTo(2m));
            Assert.That(m.Lines[0].UnitPrice, Is.EqualTo(1000000m));
            Assert.That(m.Lines[0].TaxRate, Is.EqualTo("8%"));
            Assert.That(m.Lines[1].Quantity, Is.EqualTo(1.5m));
            Assert.That(m.Lines[1].UnitPrice, Is.EqualTo(10000.25m), "phần thập phân đọc theo InvariantCulture");
            Assert.That(m.Lines[1].TaxRate, Is.EqualTo("KCT"));
            Assert.That(m.TaxLines.Select(t => t.Rate), Is.EqualTo(new[] { "8%", "KCT" }));
            Assert.That(m.TotalBeforeTax, Is.EqualTo(2015000m));
            Assert.That(m.TotalTax, Is.EqualTo(160000m));
            Assert.That(m.TotalDiscount, Is.EqualTo(5000m));
            Assert.That(m.TotalPayment, Is.EqualTo(2170000m));
            Assert.That(m.TotalInWords, Is.EqualTo("Hai triệu một trăm bảy mươi nghìn đồng"));
        }

        [Test]
        public void ReadsAuthorityCodeAndSigningTimes_EvenInsideASignatureNamespace()
        {
            var m = Parse(Sample);

            Assert.That(m.AuthorityCode, Is.EqualTo("00ABCDEF0123456789ABCDEF0123456789"));
            Assert.That(m.SellerSignedAt, Is.EqualTo(new DateTime(2026, 10, 31, 15, 4, 36)));
            Assert.That(m.AuthoritySignedAt, Is.EqualTo(new DateTime(2026, 10, 31, 15, 4, 40)));
        }

        [Test]
        public void ReadsThePortalViewFields_StoreBudgetUnitPassportListQrFeeAndSignerName()
        {
            const string xml =
                "<HDon><DLHDon><TTChung><SHDon>13</SHDon><SBKe>BK-01</SBKe><NBKe>2026-04-07</NBKe></TTChung><NDHDon>" +
                "<NBan><Ten>Bên bán giả</Ten><MCHang>CH01</MCHang><TCHang>Cửa hàng giả</TCHang></NBan>" +
                "<NMua><Ten>Bên mua giả</Ten><MDVQHNSach>1234567</MDVQHNSach><SHChieu>B1234567</SHChieu></NMua>" +
                "<TToan><DSLPhi><LPhi><TLPhi>Phí A</TLPhi><TPhi>1000</TPhi></LPhi><LPhi><TLPhi>Phí B</TLPhi><TPhi>250.5</TPhi></LPhi></DSLPhi></TToan>" +
                "</NDHDon></DLHDon><DLQRCode>0002019995003</DLQRCode>" +
                "<DSCKS><NBan><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><KeyInfo><X509Data><X509SubjectName>OID.0.9.2342.19200300.100.1.1=MST:0100000001, CN=CÔNG TY KÝ GIẢ, O=Giả</X509SubjectName></X509Data></KeyInfo>" +
                "<Object><SignatureProperties><SignatureProperty><SigningTime>2026-04-08T20:56:58</SigningTime></SignatureProperty></SignatureProperties></Object></Signature></NBan></DSCKS></HDon>";

            var m = Parse(xml);

            Assert.That(m.ListNumber, Is.EqualTo("BK-01"));
            Assert.That(m.ListDate, Is.EqualTo("2026-04-07"));
            Assert.That(m.Seller.StoreCode, Is.EqualTo("CH01"));
            Assert.That(m.Seller.StoreName, Is.EqualTo("Cửa hàng giả"));
            Assert.That(m.Buyer.BudgetUnitCode, Is.EqualTo("1234567"));
            Assert.That(m.Buyer.PassportNumber, Is.EqualTo("B1234567"));
            Assert.That(m.QrPayload, Is.EqualTo("0002019995003"));
            Assert.That(m.TotalFee, Is.EqualTo(1250.5m), "cộng các khoản phí khi không có TgTPhi");
            Assert.That(m.SellerSignerName, Is.EqualTo("CÔNG TY KÝ GIẢ"));
            Assert.That(m.SellerSignedAtText, Is.EqualTo("2026-04-08T20:56:58"));
        }

        [Test]
        public void TotalFee_PrefersTheExplicitTotal()
        {
            var m = Parse("<HDon><DLHDon><NDHDon><TToan><TgTPhi>500</TgTPhi><DSLPhi><LPhi><TPhi>1</TPhi></LPhi></DSLPhi></TToan></NDHDon></DLHDon></HDon>");

            Assert.That(m.TotalFee, Is.EqualTo(500m));
        }

        [Test]
        public void MissingParts_AreLeftEmpty_NotGuessed()
        {
            var m = Parse("<HDon><DLHDon><TTChung><SHDon>5</SHDon></TTChung><NDHDon></NDHDon></DLHDon></HDon>");

            Assert.That(m.InvoiceNumber, Is.EqualTo("5"));
            Assert.That(m.IssueDate, Is.Null);
            Assert.That(m.Seller.Name, Is.Null);
            Assert.That(m.Lines, Is.Empty);
            Assert.That(m.TotalPayment, Is.Null);
            Assert.That(m.AuthorityCode, Is.Null);
            Assert.That(m.SellerSignedAt, Is.Null);
        }

        [Test]
        public void DefaultNamespace_IsIgnored()
        {
            var m = Parse("<HDon xmlns=\"http://example.invalid/ns\"><DLHDon><TTChung><SHDon>7</SHDon></TTChung></DLHDon></HDon>");

            Assert.That(m.InvoiceNumber, Is.EqualTo("7"));
        }

        [TestCase("<HDon><broken>")]
        [TestCase("<HDon><Khac/></HDon>")]
        [TestCase("not xml at all")]
        public void NotAnInvoice_IsRejectedWithAMessage(string xml)
        {
            var ex = Assert.Throws<InvoicePrintException>(() => Parse(xml));

            Assert.That(ex.Message, Is.Not.Empty);
        }

        [Test]
        public void NullOrEmptyBytes_AreRejected()
        {
            Assert.Throws<InvoicePrintException>(() => InvoicePrintModelParser.Parse(null));
            Assert.Throws<InvoicePrintException>(() => InvoicePrintModelParser.Parse(new byte[0]));
        }

        [Test]
        public void Dtd_IsRefused_SoExternalEntitiesCannotBeResolved()
        {
            const string malicious = "<!DOCTYPE HDon [<!ENTITY x SYSTEM \"file:///C:/Windows/win.ini\">]><HDon><DLHDon><TTChung><THDon>&x;</THDon></TTChung></DLHDon></HDon>";

            Assert.Throws<InvoicePrintException>(() => Parse(malicious));
        }
    }
}
