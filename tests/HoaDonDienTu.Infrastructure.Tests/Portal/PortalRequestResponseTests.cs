#if DEBUG
using System;
using System.Linq;
using System.Net.Http;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class PortalRequestResponseTests
    {
        private static readonly DateTime July1 = new DateTime(2026, 7, 1);
        private static readonly DateTime July31 = new DateTime(2026, 7, 31);

        // ---- request builder ----

        [Test]
        public void Search_Query_BuildsExpectedUrl()
        {
            var config = PortalApiConfigDefaults.Create();
            var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31)
            {
                Filter = new InvoiceSearchFilter { CounterpartyTaxCode = " 0100100001 " }
            };

            var message = PortalRequestBuilder.BuildSearch(config, request);

            Assert.That(message.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(message.RequestUri.OriginalString, Is.EqualTo(
                "https://hoadondientu.gdt.gov.vn/api/v1/invoices/purchase?sort=issueDate%3Aasc&from=2026-07-01&to=2026-07-31&size=50&partnerTaxCode=0100100001"));
        }

        [Test]
        public void Search_SoldUsesSoldEndpoint_AndCursorIsAppended()
        {
            var config = PortalApiConfigDefaults.Create();
            var request = new InvoiceSearchRequest(InvoiceDirection.Sold, July1, July31) { Cursor = "b3A6NTA=", PageSize = 20 };

            var message = PortalRequestBuilder.BuildSearch(config, request);

            Assert.That(message.RequestUri.AbsolutePath, Is.EqualTo("/api/v1/invoices/sold"));
            Assert.That(message.RequestUri.Query, Does.Contain("size=20"));
            Assert.That(message.RequestUri.Query, Does.Contain("cursor=b3A6NTA%3D"));
        }

        [Test]
        public void Search_PageMode_SendsPageNumber_StartingAtConfiguredStartPage()
        {
            var config = Variants.Create(Variants.Page);

            var first = PortalRequestBuilder.BuildSearch(config, new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31));
            var third = PortalRequestBuilder.BuildSearch(config, new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31) { Cursor = "3" });

            Assert.That(first.RequestUri.Query, Does.Contain("cursor=1"));
            Assert.That(third.RequestUri.Query, Does.Contain("cursor=3"));
            Assert.Throws<ArgumentException>(() =>
                PortalRequestBuilder.BuildSearch(config, new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31) { Cursor = "abc" }));
        }

        [Test]
        public void Search_Body_BuildsJsonWithConfiguredNamesAndFormats()
        {
            var config = Variants.Create(Variants.PostBody);
            var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31) { Cursor = "xyz" };

            var message = PortalRequestBuilder.BuildSearch(config, request);
            var json = message.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            Assert.That(message.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(message.RequestUri.Query, Is.Empty);
            Assert.That(json, Does.Contain("\"dateFrom\":\"01/07/2026\""));
            Assert.That(json, Does.Contain("\"dateTo\":\"31/07/2026\""));
            Assert.That(json, Does.Contain("\"limit\":50"), "Kích thước trang gửi dạng số");
            Assert.That(json, Does.Contain("\"next\":\"xyz\""));
        }

        [Test]
        public void Search_UnmappedFilter_IsDroppedInsteadOfSentWrong()
        {
            var config = PortalApiConfigDefaults.Create();
            config.Search.FilterParameters.Remove("InvoiceNumber");
            var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31)
            {
                Filter = new InvoiceSearchFilter { InvoiceNumber = "0000123", StatusCode = "1" }
            };

            var query = PortalRequestBuilder.BuildSearch(config, request).RequestUri.Query;

            Assert.That(query, Does.Not.Contain("0000123"));
            Assert.That(query, Does.Contain("status=1"));
        }

        [Test]
        public void SearchRequest_RejectsReversedRange()
        {
            Assert.Throws<ArgumentException>(() => new InvoiceSearchRequest(InvoiceDirection.Sold, July31, July1));
        }

        [Test]
        public void Download_PathParameter_IsEscaped()
        {
            var config = PortalApiConfigDefaults.Create();
            var dto = new PortalInvoiceDto { PortalInvoiceId = "a/b c", InvoiceNumber = "1", InvoiceSymbol = "C", IssueDate = July1 };

            var message = PortalRequestBuilder.BuildDownload(config, new PortalInvoiceRef(InvoiceDirection.Sold, dto));

            Assert.That(message.RequestUri.AbsolutePath, Is.EqualTo("/api/v1/invoices/a%2Fb%20c/xml"));
        }

        // ---- response parser ----

        private const string SamplePage = @"{
            ""data"": {
                ""total"": 2,
                ""nextCursor"": ""abc"",
                ""items"": [
                    { ""id"": ""x1"", ""formSymbol"": ""1"", ""invoiceSymbol"": ""C26TAA"", ""number"": ""0000123"", ""issueDate"": ""2026-07-05"",
                      ""seller"": { ""taxCode"": ""0100100001"", ""name"": ""Alpha"" },
                      ""buyer"": { ""taxCode"": ""0101234567"", ""name"": ""Beta"", ""address"": ""Hà Nội"" },
                      ""amounts"": { ""beforeTax"": 1000000, ""tax"": 100000.5, ""discount"": null, ""payment"": ""1100000.5"" },
                      ""currency"": ""VND"", ""status"": 1, ""checkResult"": ""0"" },
                    { ""id"": ""x2"", ""number"": 7, ""issueDate"": ""2026-07-06"" }
                ]
            }
        }";

        [Test]
        public void Parse_MapsFields_KeepsLeadingZeros_AndHandlesNullsAndNumbers()
        {
            var page = PortalResponseParser.ParseSearch(PortalApiConfigDefaults.Create(), SamplePage, null);

            Assert.That(page.Items.Count, Is.EqualTo(2));
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.NextCursor, Is.EqualTo("abc"));
            Assert.That(page.HasMore, Is.True);

            var first = page.Items[0];
            Assert.That(first.InvoiceNumber, Is.EqualTo("0000123"));
            Assert.That(first.IssueDate, Is.EqualTo(new DateTime(2026, 7, 5)));
            Assert.That(first.BuyerAddress, Is.EqualTo("Hà Nội"));
            Assert.That(first.TotalBeforeTax, Is.EqualTo(1000000m));
            Assert.That(first.TotalTax, Is.EqualTo(100000.5m));
            Assert.That(first.TotalDiscount, Is.Null);
            Assert.That(first.TotalPayment, Is.EqualTo(1100000.5m), "Số dạng chuỗi cũng được đọc bằng invariant culture");
            Assert.That(first.StatusRaw, Is.EqualTo("1"));

            var second = page.Items[1];
            Assert.That(second.InvoiceNumber, Is.EqualTo("7"));
            Assert.That(second.SellerTaxCode, Is.Null);
        }

        [Test]
        public void Parse_NullOrMissingNextCursor_MeansLastPage()
        {
            var config = PortalApiConfigDefaults.Create();

            var nullCursor = PortalResponseParser.ParseSearch(config, SamplePage.Replace("\"nextCursor\": \"abc\"", "\"nextCursor\": null"), null);
            var noCursor = PortalResponseParser.ParseSearch(config, SamplePage.Replace("\"nextCursor\": \"abc\",", string.Empty), null);

            Assert.That(nullCursor.HasMore, Is.False);
            Assert.That(noCursor.HasMore, Is.False);
        }

        [Test]
        public void Parse_PageMode_ContinuesUntilEmptyPage_AndHonoursHasMoreFlag()
        {
            var config = Variants.Create(Variants.Page);
            var withItems = "{\"data\":{\"items\":[{\"id\":\"a\",\"number\":\"1\",\"issueDate\":\"2026-07-01\"}]}}";
            var empty = "{\"data\":{\"items\":[]}}";

            Assert.That(PortalResponseParser.ParseSearch(config, withItems, "2").NextCursor, Is.EqualTo("3"));
            Assert.That(PortalResponseParser.ParseSearch(config, withItems, null).NextCursor, Is.EqualTo("2"));
            Assert.That(PortalResponseParser.ParseSearch(config, empty, "4").HasMore, Is.False);

            config.Search.Response.HasMorePath = "data.more";
            var flagFalse = "{\"data\":{\"more\":false,\"items\":[{\"id\":\"a\",\"number\":\"1\",\"issueDate\":\"2026-07-01\"}]}}";
            Assert.That(PortalResponseParser.ParseSearch(config, flagFalse, "1").HasMore, Is.False);
        }

        [TestCase("not json at all")]
        [TestCase("{\"data\":{}}")]
        [TestCase("{\"data\":{\"items\":\"oops\"}}")]
        [TestCase("{\"data\":{\"items\":[{\"number\":\"1\",\"issueDate\":\"2026-07-01\"}]}}")]
        [TestCase("{\"data\":{\"items\":[{\"id\":\"a\",\"issueDate\":\"2026-07-01\"}]}}")]
        [TestCase("{\"data\":{\"items\":[{\"id\":\"a\",\"number\":\"1\"}]}}")]
        [TestCase("{\"data\":{\"items\":[{\"id\":\"a\",\"number\":\"1\",\"issueDate\":\"05-07-2026\"}]}}")]
        [TestCase("{\"data\":{\"items\":[{\"id\":\"a\",\"number\":\"1\",\"issueDate\":\"2026-07-01\",\"amounts\":{\"tax\":\"abc\"}}]}}")]
        public void Parse_BrokenStructure_IsSchemaChanged(string body)
        {
            var ex = Assert.Throws<PortalApiException>(() => PortalResponseParser.ParseSearch(PortalApiConfigDefaults.Create(), body, null));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [Test]
        public void ParseProfile_ReadsTaxCode_OrSchemaChanged()
        {
            var config = PortalApiConfigDefaults.Create();

            Assert.That(PortalResponseParser.ParseProfileTaxCode(config, "{\"data\":{\"taxCode\":\" 0101234567 \"}}"), Is.EqualTo("0101234567"));
            Assert.Throws<PortalApiException>(() => PortalResponseParser.ParseProfileTaxCode(config, "{\"data\":{}}"));
        }

        [Test]
        public void ParseExceptions_NeverContainResponseBody()
        {
            var secret = "SECRET-BODY-VALUE";

            var ex = Assert.Throws<PortalApiException>(() =>
                PortalResponseParser.ParseSearch(PortalApiConfigDefaults.Create(), "{\"data\":{\"items\":[{\"id\":\"" + secret + "\"}]}}", null));

            Assert.That(ex.Message, Does.Not.Contain(secret));
        }

        // ---- error mapper ----

        [TestCase(401, PortalErrorKind.AuthExpired)]
        [TestCase(403, PortalErrorKind.AuthExpired)]
        [TestCase(429, PortalErrorKind.RateLimited)]
        [TestCase(408, PortalErrorKind.Transient)]
        [TestCase(500, PortalErrorKind.Transient)]
        [TestCase(503, PortalErrorKind.Transient)]
        [TestCase(400, PortalErrorKind.FilterRejected)]
        [TestCase(422, PortalErrorKind.FilterRejected)]
        [TestCase(404, PortalErrorKind.Fatal)]
        public void ErrorMapper_DefaultStatusMapping(int status, PortalErrorKind expected)
        {
            Assert.That(PortalErrorMapper.KindOf(PortalApiConfigDefaults.Create(), status), Is.EqualTo(expected));
        }

        [Test]
        public void ErrorMapper_ConfigOverridesDefaults()
        {
            var config = PortalApiConfigDefaults.Create();
            config.Errors.StatusKinds[404] = "SchemaChanged";
            config.Errors.StatusKinds[401] = "Fatal";

            Assert.That(PortalErrorMapper.KindOf(config, 404), Is.EqualTo(PortalErrorKind.SchemaChanged));
            Assert.That(PortalErrorMapper.KindOf(config, 401), Is.EqualTo(PortalErrorKind.Fatal));
            Assert.That(PortalErrorMapper.KindOf(config, 403), Is.EqualTo(PortalErrorKind.AuthExpired));
        }
    }
}

#endif
