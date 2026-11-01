#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>
    /// Client thật + cổng giả cùng đọc một cấu hình. Mọi test chính chạy lại cho cả ba "hình dạng API"
    /// (con trỏ/query, số trang/query, POST/body + base64) để chứng minh chỉ cần đổi cấu hình khi khớp cổng thật.
    /// </summary>
    [TestFixture]
    public class PortalClientMockTests
    {
        private static readonly DateTime July1 = new DateTime(2026, 7, 1);
        private static readonly DateTime July31 = new DateTime(2026, 7, 31);

        private static IEnumerable<string> VariantNames
        {
            get { return Variants.All; }
        }

        private static PortalApiException ExpectError(TestDelegate action)
        {
            return Assert.Throws<PortalApiException>(action);
        }

        [TestCaseSource("VariantNames")]
        public void Search_MultiPageMonth_ReturnsEveryInvoiceExactlyOnce(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var expected = portal.Dataset.Query(InvoiceDirection.Purchase, July1, July31);

                var all = portal.SearchAll(InvoiceDirection.Purchase, July1, July31);

                Assert.That(expected.Count, Is.EqualTo(120));
                Assert.That(all.Count, Is.EqualTo(120));
                Assert.That(all.Select(i => i.PortalInvoiceId).Distinct().Count(), Is.EqualTo(120));
                Assert.That(all.Select(i => i.PortalInvoiceId), Is.EqualTo(expected.Select(r => r.Dto.PortalInvoiceId)));
                Assert.That(portal.Pages, Is.GreaterThanOrEqualTo(3), "Cổng giới hạn 50 bản ghi/trang nên cần ít nhất 3 trang");
            }
        }

        [TestCaseSource("VariantNames")]
        public void Search_ServerCapsPageSizeBelowRequested_StillGetsEverything(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                portal.Handler.MaxPageSize = 17;

                var all = portal.SearchAll(InvoiceDirection.Purchase, July1, July31, pageSize: 100);

                Assert.That(all.Count, Is.EqualTo(120), "Client không được giả định cổng trả đủ kích thước trang đã xin");
                Assert.That(portal.Pages, Is.GreaterThanOrEqualTo(8));
            }
        }

        [TestCaseSource("VariantNames")]
        public void Search_EmptyMonth_ReturnsNoItems_AndStops(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var all = portal.SearchAll(InvoiceDirection.Sold, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

                Assert.That(all, Is.Empty);
                Assert.That(portal.Pages, Is.LessThanOrEqualTo(1));
            }
        }

        [TestCaseSource("VariantNames")]
        public void Search_DirectionsAreSeparate_AndOwnTaxCodeIsOnTheRightSide(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var purchase = portal.SearchAll(InvoiceDirection.Purchase, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
                var sold = portal.SearchAll(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

                Assert.That(sold.Count, Is.EqualTo(1));
                Assert.That(sold[0].SellerTaxCode, Is.EqualTo(TestPortal.TaxCode));
                Assert.That(purchase.Count, Is.GreaterThan(1));
                Assert.That(purchase.All(i => i.BuyerTaxCode == TestPortal.TaxCode), Is.True);
            }
        }

        [TestCaseSource("VariantNames")]
        public void Search_DateWindow_IsInclusiveOnBothEnds(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var from = new DateTime(2026, 7, 10);
                var to = new DateTime(2026, 7, 20);
                var expected = portal.Dataset.Query(InvoiceDirection.Purchase, from, to);

                var all = portal.SearchAll(InvoiceDirection.Purchase, from, to);

                Assert.That(all.Count, Is.EqualTo(expected.Count));
                Assert.That(all.Count, Is.GreaterThan(0));
                Assert.That(all.All(i => i.IssueDate >= from && i.IssueDate <= to), Is.True);
                Assert.That(all.Any(i => i.IssueDate == from) || all.Any(i => i.IssueDate == to), Is.True, "Mẫu phải có hóa đơn ở biên");
            }
        }

        [TestCaseSource("VariantNames")]
        public void Search_PreservesRawFields_LeadingZeroNumbers_ForeignCurrency_AndNulls(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var all = portal.SearchAll(InvoiceDirection.Purchase, July1, July31);

                Assert.That(all.All(i => i.InvoiceNumber.Length == 7 && i.InvoiceNumber.StartsWith("0")), Is.True);
                Assert.That(all.Any(i => i.Currency == "USD" && i.ExchangeRate == 25000m), Is.True);
                Assert.That(all.Any(i => i.TotalDiscount == null), Is.True);
                Assert.That(all.Any(i => i.TotalDiscount != null), Is.True);
                Assert.That(all.Select(i => i.StatusRaw).Distinct().Count(), Is.GreaterThan(1));
                Assert.That(all.All(i => i.TotalPayment.HasValue), Is.True);
            }
        }

        [TestCaseSource("VariantNames")]
        public void Search_Filters_Work_WhenSupported(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var records = portal.Dataset.Query(InvoiceDirection.Purchase, July1, July31);
                var counterparty = records[0].Dto.SellerTaxCode;
                var number = records[5].Dto.InvoiceNumber;

                var byPartner = portal.SearchAll(InvoiceDirection.Purchase, July1, July31, new InvoiceSearchFilter { CounterpartyTaxCode = counterparty });
                var byNumber = portal.SearchAll(InvoiceDirection.Purchase, July1, July31, new InvoiceSearchFilter { InvoiceNumber = number });
                var byStatus = portal.SearchAll(InvoiceDirection.Purchase, July1, July31, new InvoiceSearchFilter { StatusCode = "6" });

                Assert.That(byPartner.Count, Is.EqualTo(records.Count(r => r.Dto.SellerTaxCode == counterparty)));
                Assert.That(byPartner.All(i => i.SellerTaxCode == counterparty), Is.True);
                Assert.That(byNumber.Count, Is.EqualTo(1));
                Assert.That(byNumber[0].InvoiceNumber, Is.EqualTo(number));
                Assert.That(byStatus.All(i => i.StatusRaw == "6"), Is.True);
                Assert.That(byStatus.Count, Is.EqualTo(records.Count(r => r.Dto.StatusRaw == "6")));
            }
        }

        [Test]
        public void Search_UnsupportedFilter_IsFilterRejected()
        {
            using (var portal = new TestPortal())
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31)
                {
                    Filter = new InvoiceSearchFilter { ProxyInvoice = true }
                };

                var ex = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.FilterRejected));
                Assert.That(ex.HttpStatus, Is.EqualTo(400));
            }
        }

        [Test]
        public void Search_RangeLongerThanOneMonth_IsRejectedByPortal_SoSegmentingIsRequired()
        {
            using (var portal = new TestPortal())
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30));

                var ex = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.FilterRejected));
            }
        }

        [Test]
        public void Search_BogusCursor_IsRejected()
        {
            using (var portal = new TestPortal())
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31) { Cursor = "not-a-cursor" };

                var ex = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(ex.HttpStatus, Is.EqualTo(400));
            }
        }

        [TestCaseSource("VariantNames")]
        public void Errors_ExpiredOrMissingToken_AreAuthExpired(string variant)
        {
            using (var portal = new TestPortal(variant, token: "mock-token-expired"))
            using (var anonymous = portal.NewClient(null))
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31);

                var expired = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());
                var missing = ExpectError(() => anonymous.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(expired.Kind, Is.EqualTo(PortalErrorKind.AuthExpired));
                Assert.That(missing.Kind, Is.EqualTo(PortalErrorKind.AuthExpired));
            }
        }

        [Test]
        public void Errors_RateLimited_CarriesRetryAfter_AndNextCallSucceeds()
        {
            using (var portal = new TestPortal())
            {
                portal.Handler.EnqueueFailure(429, 7);
                var request = new InvoiceSearchRequest(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

                var ex = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());
                var page = portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult();

                Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.RateLimited));
                Assert.That(ex.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(7)));
                Assert.That(page.Items.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void Errors_ServerErrorAndNetworkFailure_AreTransient()
        {
            using (var portal = new TestPortal())
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
                portal.Handler.EnqueueFailure(503);
                portal.Handler.EnqueueNetworkError();

                var server = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());
                var network = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(server.Kind, Is.EqualTo(PortalErrorKind.Transient));
                Assert.That(server.HttpStatus, Is.EqualTo(503));
                Assert.That(network.Kind, Is.EqualTo(PortalErrorKind.Transient));
            }
        }

        [Test]
        public void Errors_Timeout_IsTransient_NotCancellation()
        {
            using (var portal = new TestPortal())
            {
                portal.Config.Http.TimeoutSeconds = 1;
                using (var slow = portal.NewClient(MockInvoiceDataset.TokenFor(TestPortal.TaxCode)))
                {
                    portal.Handler.Latency = TimeSpan.FromSeconds(3);
                    var request = new InvoiceSearchRequest(InvoiceDirection.Sold, July1, July31);

                    var ex = ExpectError(() => slow.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                    Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.Transient));
                }
            }
        }

        [Test]
        public void Errors_CallerCancellation_PropagatesAsCancellation()
        {
            using (var portal = new TestPortal())
            using (var cts = new CancellationTokenSource())
            {
                portal.Handler.Latency = TimeSpan.FromSeconds(5);
                cts.CancelAfter(100);
                var request = new InvoiceSearchRequest(InvoiceDirection.Sold, July1, July31);

                Assert.Throws<System.Threading.Tasks.TaskCanceledException>(() => portal.Client.SearchAsync(request, cts.Token).GetAwaiter().GetResult());
            }
        }

        [Test]
        public void Errors_MalformedBody_AndMissingField_AreSchemaChanged()
        {
            using (var portal = new TestPortal())
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Purchase, July1, July31);

                portal.Handler.MalformedSearchResponse = true;
                var malformed = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());
                portal.Handler.MalformedSearchResponse = false;

                portal.Handler.OmitDtoField = "InvoiceNumber";
                var omitted = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(malformed.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
                Assert.That(omitted.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
            }
        }

        [Test]
        public void Errors_NeverLeakTokenOrBodyIntoMessages()
        {
            const string secretToken = "mock-token-expired-SECRETVALUE";
            using (var portal = new TestPortal(token: secretToken))
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Sold, July1, July31);

                var ex = ExpectError(() => portal.Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(ex.Message, Does.Not.Contain("SECRETVALUE"));
                Assert.That(ex.ToString(), Does.Not.Contain("SECRETVALUE"));
                Assert.That(string.Join("\n", portal.Handler.RequestLog), Does.Not.Contain("SECRETVALUE"));
            }
        }

        [TestCaseSource("VariantNames")]
        public void Download_ReturnsWellFormedXml_ForEveryInvoiceOfASegment(string variant)
        {
            using (var portal = new TestPortal(variant))
            {
                var items = portal.SearchAll(InvoiceDirection.Purchase, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

                foreach (var item in items)
                {
                    var payload = portal.Client.DownloadXmlAsync(new PortalInvoiceRef(InvoiceDirection.Purchase, item), CancellationToken.None)
                        .GetAwaiter().GetResult();

                    var document = new XmlDocument { XmlResolver = null };
                    using (var stream = new System.IO.MemoryStream(payload.Content))
                    using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                    {
                        document.Load(reader);
                    }

                    Assert.That(document.SelectSingleNode("/HDon/DLHDon/TTChung/SHDon").InnerText, Is.EqualTo(item.InvoiceNumber));
                    Assert.That(document.SelectSingleNode("/HDon/DLHDon/NDHDon/NMua/MST").InnerText, Is.EqualTo(item.BuyerTaxCode));
                    Assert.That(payload.SuggestedFileName, Is.EqualTo(item.InvoiceSymbol + "-" + item.InvoiceNumber + ".xml"));
                }
            }
        }

        [Test]
        public void Download_UnknownInvoice_IsFatal404()
        {
            using (var portal = new TestPortal())
            {
                var dto = new PortalInvoiceDto { PortalInvoiceId = "does-not-exist", InvoiceNumber = "1", IssueDate = July1 };

                var ex = ExpectError(() => portal.Client.DownloadXmlAsync(new PortalInvoiceRef(InvoiceDirection.Sold, dto), CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.Fatal));
                Assert.That(ex.HttpStatus, Is.EqualTo(404));
            }
        }

        [Test]
        public void Download_FileName_CannotEscapeTargetFolder()
        {
            using (var portal = new TestPortal())
            {
                var record = portal.Dataset.Query(InvoiceDirection.Purchase, July1, July31)[0];
                var dto = record.Dto;
                var hostile = new PortalInvoiceDto
                {
                    PortalInvoiceId = dto.PortalInvoiceId,
                    InvoiceSymbol = "..\\..",
                    InvoiceNumber = "a/b:c*d",
                    IssueDate = dto.IssueDate
                };

                var payload = portal.Client.DownloadXmlAsync(new PortalInvoiceRef(InvoiceDirection.Purchase, hostile), CancellationToken.None)
                    .GetAwaiter().GetResult();

                Assert.That(payload.SuggestedFileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()), Is.EqualTo(-1));
            }
        }

        [Test]
        public void ClientDispose_DoesNotDisposeSharedMockHandler()
        {
            using (var portal = new TestPortal())
            {
                portal.Client.Dispose();
                using (var second = portal.NewClient(MockInvoiceDataset.TokenFor(TestPortal.TaxCode)))
                {
                    var page = second.SearchAsync(new InvoiceSearchRequest(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)), CancellationToken.None)
                        .GetAwaiter().GetResult();

                    Assert.That(page.Items.Count, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void Cookies_AndForwardedHeaders_AreSentOnlyWhenConfigured()
        {
            var config = Variants.Create(Variants.Default);
            config.Auth.SendCookies = true;
            config.Auth.ForwardHeaders = new List<string> { "X-Csrf" };
            var handler = new MockPortalHandler(config);
            var seen = new List<string>();
            var spy = new SpyHandler(handler, seen);
            var material = new PortalSessionMaterial(
                new Dictionary<string, string> { { "sid", "abc" }, { "lang", "vi" } },
                new Dictionary<string, string> { { "X-Csrf", "tok" }, { "X-Other", "no" } },
                MockInvoiceDataset.TokenFor(TestPortal.TaxCode));

            using (var client = new PortalHttpApiClient(config, material, spy))
            {
                client.SearchAsync(new InvoiceSearchRequest(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            Assert.That(seen.Single(), Does.Contain("Cookie=sid=abc; lang=vi"));
            Assert.That(seen.Single(), Does.Contain("X-Csrf=tok"));
            Assert.That(seen.Single(), Does.Not.Contain("X-Other"));
        }

        private sealed class SpyHandler : System.Net.Http.DelegatingHandler
        {
            private readonly List<string> _seen;

            public SpyHandler(System.Net.Http.HttpMessageHandler inner, List<string> seen)
                : base(inner)
            {
                _seen = seen;
            }

            protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
                System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _seen.Add(string.Join(";", request.Headers.Where(h => h.Key == "Cookie" || h.Key == "X-Csrf" || h.Key == "X-Other")
                    .Select(h => h.Key + "=" + string.Join(",", h.Value))));
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}

#endif
