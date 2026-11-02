using System;
using System.Linq;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>Request dựng ra phải trùng từng ký tự với các URL mẫu thật trong sample-api-web.md.</summary>
    [TestFixture]
    public class PortalRequestTests
    {
        private PortalApiConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = PortalApiConfigDefaults.Create();
        }

        private static InvoiceSearchRequest Request(InvoiceDirection direction, InvoiceSearchFilter filter = null)
        {
            return new InvoiceSearchRequest(direction, new DateTime(2026, 10, 2), new DateTime(2026, 11, 1)) { Filter = filter ?? new InvoiceSearchFilter() };
        }

        [Test]
        public void Rsql_OnlyDateRange_MatchesSampleUrl()
        {
            Assert.That(PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold)),
                Is.EqualTo("tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59"));
        }

        [Test]
        public void Rsql_StatusOnly_MatchesSampleUrl()
        {
            var filter = new InvoiceSearchFilter { StatusCode = "1" };

            Assert.That(PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, filter)),
                Is.EqualTo("tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;tthai==1"));
        }

        [Test]
        public void Rsql_AllFilters_MatchesSampleUrlExactly()
        {
            var filter = new InvoiceSearchFilter
            {
                CounterpartyTaxCode = "1112232234",
                StatusCode = "6",
                CheckResultCode = "0",
                FormSymbol = "1",
                InvoiceNumber = "323",
                InvoiceSymbol = "121",
                ProxyInvoice = true,
                BuyerIdNumber = "036402421225"
            };

            Assert.That(PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, filter)), Is.EqualTo(
                "tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nmmst==1112232234;tthai==6;ttxly==0;khmshdon==1;shdon==323;khhdon==121;unhiem==1;nmcmnd==036402421225"));
        }

        [Test]
        public void Rsql_AllStatusAndCheckResult_AreOmitted()
        {
            var filter = new InvoiceSearchFilter { StatusCode = string.Empty, CheckResultCode = null, FormSymbol = "  " };

            Assert.That(PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, filter)), Is.EqualTo("tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59"));
        }

        [Test]
        public void Rsql_ProxyUnchecked_SendsNothing_NotZero()
        {
            var unchecked1 = new InvoiceSearchFilter { ProxyInvoice = false };
            var unchecked2 = new InvoiceSearchFilter { ProxyInvoice = null };

            Assert.That(PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, unchecked1)), Does.Not.Contain("unhiem"));
            Assert.That(PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, unchecked2)), Does.Not.Contain("unhiem"));
        }

        [Test]
        public void Rsql_Purchase_UsesSellerTaxCodeField_AndSendsBuyerIdNumber()
        {
            var filter = new InvoiceSearchFilter { CounterpartyTaxCode = "0100100001", BuyerIdNumber = "036402421225" };

            var rsql = PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Purchase, filter));

            Assert.That(rsql, Does.Contain("nbmst==0100100001"));
            Assert.That(rsql, Does.Not.Contain("nmmst"));
            Assert.That(rsql, Does.EndWith("nmcmnd==036402421225"), "CCCD người mua có ở cả mua vào");
        }

        [Test]
        public void Rsql_Purchase_MatchesTheRealSampleOfSection4()
        {
            var filter = new InvoiceSearchFilter
            {
                CounterpartyTaxCode = "1234567890", StatusCode = "1", CheckResultCode = "6", FormSymbol = "8", InvoiceNumber = "123",
                InvoiceSymbol = "321", ProxyInvoice = true, BuyerIdNumber = "999999999999"
            };

            var rsql = PortalRequestBuilder.BuildRsql(_config, new InvoiceSearchRequest(InvoiceDirection.Purchase, new DateTime(2026, 10, 2), new DateTime(2026, 11, 1)) { Filter = filter });

            Assert.That(rsql, Is.EqualTo(
                "tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nbmst==1234567890;tthai==1;ttxly==6;khmshdon==8;shdon==123;khhdon==321;unhiem==1;nmcmnd==999999999999"));
        }

        [Test]
        public void Rsql_ValueWithSpecialCharacters_IsQuotedSoItCannotInjectConditions()
        {
            var filter = new InvoiceSearchFilter { InvoiceSymbol = "A;tthai==6" };

            var rsql = PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, filter));

            Assert.That(rsql, Does.EndWith("khhdon==\"A;tthai==6\""));
        }

        [Test]
        public void Rsql_FieldWithoutConfiguredName_IsNotSent()
        {
            _config.Search.SoldFilterFields.Remove("ProxyInvoice");

            var rsql = PortalRequestBuilder.BuildRsql(_config, Request(InvoiceDirection.Sold, new InvoiceSearchFilter { ProxyInvoice = true }));

            Assert.That(rsql, Does.Not.Contain("unhiem"));
        }

        [Test]
        public void Search_Url_MatchesPortalShape()
        {
            var message = PortalRequestBuilder.BuildSearch(_config, Request(InvoiceDirection.Sold));

            Assert.That(message.Method.Method, Is.EqualTo("GET"));
            Assert.That(message.RequestUri.GetLeftPart(UriPartial.Path), Is.EqualTo("https://hoadondientu.gdt.gov.vn/api/query/invoices/sold"));
            Assert.That(message.RequestUri.Query, Is.EqualTo(
                "?sort=tdlap:desc&size=50&search=tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59"));
        }

        [Test]
        public void Search_NextPage_AddsStateCursor()
        {
            var request = Request(InvoiceDirection.Sold);
            request.Cursor = "abc==";
            request.PageSize = 15;

            var query = PortalRequestBuilder.BuildSearch(_config, request).RequestUri.Query;

            Assert.That(query, Does.Contain("size=15"));
            Assert.That(query, Does.Contain("state=abc=="));
        }

        [Test]
        public void Search_Purchase_UsesPurchasePath()
        {
            var message = PortalRequestBuilder.BuildSearch(_config, Request(InvoiceDirection.Purchase));

            Assert.That(message.RequestUri.AbsolutePath, Is.EqualTo("/api/query/invoices/purchase"));
        }

        [Test]
        public void Search_ActionHeader_MatchesCapturedValueExactly()
        {
            var message = PortalRequestBuilder.BuildSearch(_config, Request(InvoiceDirection.Sold));

            Assert.That(Header(message, "action"), Is.EqualTo("T%C3%ACm%20ki%E1%BA%BFm%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)"));
        }

        [Test]
        public void Detail_Url_AndAction_MatchSample()
        {
            var invoice = Ref();

            var message = PortalRequestBuilder.BuildDetail(_config, invoice);

            Assert.That(message.RequestUri.PathAndQuery, Is.EqualTo("/api/query/invoices/detail?nbmst=0109213133&khhdon=C26TBN&shdon=40&khmshdon=1"));
            Assert.That(Header(message, "action"), Is.EqualTo("Xem%20h%C3%B3a%20%C4%91%C6%A1n%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)"));
        }

        [Test]
        public void ExportXml_Url_AndAction_MatchSample()
        {
            var message = PortalRequestBuilder.BuildDownload(_config, Ref());

            Assert.That(message.RequestUri.PathAndQuery, Is.EqualTo("/api/query/invoices/export-xml?nbmst=0109213133&khhdon=C26TBN&shdon=40&khmshdon=1"));
            Assert.That(Header(message, "action"), Is.EqualTo("Xu%E1%BA%A5t%20xml%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)"));
        }

        [Test]
        public void Purchase_ActionHeader_UsesPurchaseWording()
        {
            var invoice = new PortalInvoiceRef(InvoiceDirection.Purchase, Dto());

            var action = Uri.UnescapeDataString(Header(PortalRequestBuilder.BuildDownload(_config, invoice), "action"));

            Assert.That(action, Is.EqualTo("Xuất xml (hóa đơn mua vào)"));
        }

        [Test]
        public void EncodeHeaderValue_BehavesLikeEncodeUriComponent()
        {
            Assert.That(PortalRequestBuilder.EncodeHeaderValue("a b(c)!*'"), Is.EqualTo("a%20b(c)!*'"));
        }

        [Test]
        public void KeyParameters_UseTheCompositeKey_NotTheInternalId()
        {
            var query = PortalRequestBuilder.BuildDownload(_config, Ref()).RequestUri.Query;

            Assert.That(query, Does.Not.Contain("d2240f37"));
            Assert.That(query, Does.Contain("nbmst=0109213133").And.Contain("khhdon=C26TBN").And.Contain("shdon=40").And.Contain("khmshdon=1"));
        }

        private static PortalInvoiceDto Dto()
        {
            return new PortalInvoiceDto
            {
                PortalInvoiceId = "d2240f37-51af-48d9-98f1-f01d4a0810ff",
                SellerTaxCode = "0109213133",
                InvoiceSymbol = "C26TBN",
                InvoiceNumber = "40",
                FormSymbol = "1",
                IssueDate = new DateTime(2026, 10, 31)
            };
        }

        private static PortalInvoiceRef Ref()
        {
            return new PortalInvoiceRef(InvoiceDirection.Sold, Dto());
        }

        private static string Header(System.Net.Http.HttpRequestMessage message, string name)
        {
            return message.Headers.GetValues(name).Single();
        }
    }
}
