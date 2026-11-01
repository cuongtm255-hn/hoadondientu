#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Portal;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>Client thật chạy trên cổng giả có hình dạng API thật (RSQL, datas/total/state, JWT, xuất XML dạng zip).</summary>
    [TestFixture]
    public class PortalClientMockTests
    {
        private TestPortal _portal;

        [SetUp]
        public void SetUp()
        {
            _portal = new TestPortal();
        }

        [TearDown]
        public void TearDown()
        {
            _portal.Dispose();
        }

        private static DateTime D(int y, int m, int d)
        {
            return new DateTime(y, m, d);
        }

        // ---- Tra cứu ----

        [Test]
        public void Search_Sold_ReturnsRecordsNewestFirst_LikeSortTdlapDesc()
        {
            var items = _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

            Assert.That(items, Is.Not.Empty);
            Assert.That(items.Select(i => i.IssueDate), Is.Ordered.Descending);
            Assert.That(items.Select(i => i.InvoiceNumber).Distinct().Count(), Is.EqualTo(items.Count));
            Assert.That(items.All(i => i.SellerTaxCode == TestPortal.TaxCode), Is.True, "Bán ra: người bán là MST đang đăng nhập");
        }

        [Test]
        public void Search_Purchase_ReturnsBuyerIsOwnTaxCode()
        {
            var items = _portal.SearchAll(InvoiceDirection.Purchase, D(2026, 1, 1), D(2026, 1, 31));

            Assert.That(items, Is.Not.Empty);
            Assert.That(items.All(i => i.BuyerTaxCode == TestPortal.TaxCode), Is.True);
        }

        [Test]
        public void Search_RangeIsInclusiveOnBothEnds_IncludingTheLastSecondOfTheDay()
        {
            var all = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));
            var lastDay = all.Max(r => r.Dto.IssueDate);

            var items = _portal.SearchAll(InvoiceDirection.Sold, lastDay, lastDay);

            Assert.That(items, Is.Not.Empty);
            Assert.That(items.All(i => i.IssueDate == lastDay), Is.True);
        }

        [Test]
        public void Search_MultiPageMonth_FollowsStateToTheEnd_WithoutLossOrDuplicates()
        {
            var expected = _portal.Dataset.Query(InvoiceDirection.Purchase, D(2026, 7, 1), D(2026, 7, 31));
            Assert.That(expected.Count, Is.EqualTo(120));

            var items = _portal.SearchAll(InvoiceDirection.Purchase, D(2026, 7, 1), D(2026, 7, 31), null, 50);

            Assert.That(items.Count, Is.EqualTo(120));
            Assert.That(_portal.Pages, Is.EqualTo(3));
            Assert.That(items.Select(i => i.PortalInvoiceId).Distinct().Count(), Is.EqualTo(120));
        }

        [Test]
        public void Search_PortalCapsPageSize_ClientStillGetsEverythingByFollowingState()
        {
            _portal.Handler.MaxPageSize = 10;

            var items = _portal.SearchAll(InvoiceDirection.Purchase, D(2026, 7, 1), D(2026, 7, 31), null, 1000);

            Assert.That(items.Count, Is.EqualTo(120));
            Assert.That(_portal.Pages, Is.EqualTo(12));
        }

        [Test]
        public void Search_EmptyMonth_ReturnsEmptyPage()
        {
            var items = _portal.SearchAll(InvoiceDirection.Sold, D(2026, 3, 1), D(2026, 3, 31));

            Assert.That(items, Is.Empty);
        }

        [Test]
        public void Search_StatusAndCheckResultFilters_UseThePortalCodes()
        {
            var cancelled = _portal.SearchAll(InvoiceDirection.Purchase, D(2026, 7, 1), D(2026, 7, 31), new InvoiceSearchFilter { StatusCode = "6" });
            var granted = _portal.SearchAll(InvoiceDirection.Purchase, D(2026, 7, 1), D(2026, 7, 31), new InvoiceSearchFilter { CheckResultCode = "5" });

            Assert.That(cancelled, Is.Not.Empty);
            Assert.That(cancelled.All(i => i.StatusRaw == "6"), Is.True);
            Assert.That(granted.All(i => i.CheckResultRaw == "5"), Is.True);
        }

        [Test]
        public void Search_BuyerIdNumber_FindsIndividualBuyers_OnlyInSold()
        {
            var sold = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 12, 31)).First(r => r.Dto.BuyerIdNumber != null);
            var date = sold.Dto.IssueDate;

            var items = _portal.SearchAll(InvoiceDirection.Sold, date, date, new InvoiceSearchFilter { BuyerIdNumber = sold.Dto.BuyerIdNumber });

            Assert.That(items.Select(i => i.PortalInvoiceId), Does.Contain(sold.Dto.PortalInvoiceId));
            Assert.That(items.All(i => i.BuyerIdNumber == sold.Dto.BuyerIdNumber && i.BuyerTaxCode == null), Is.True);
        }

        [Test]
        public void Search_InvoiceNumberFormAndSymbol_Filters()
        {
            var target = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)).First().Dto;

            var items = _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31), new InvoiceSearchFilter
            {
                InvoiceNumber = target.InvoiceNumber,
                FormSymbol = target.FormSymbol,
                InvoiceSymbol = target.InvoiceSymbol
            });

            Assert.That(items.Single().PortalInvoiceId, Is.EqualTo(target.PortalInvoiceId));
        }

        [Test]
        public void Search_ProxyCheckbox_OnlyReturnsProxyInvoices()
        {
            var items = _portal.SearchAll(InvoiceDirection.Purchase, D(2026, 7, 1), D(2026, 7, 31), new InvoiceSearchFilter { ProxyInvoice = true });
            var proxyIds = _portal.Dataset.All.Where(r => r.Proxy).Select(r => r.Dto.PortalInvoiceId).ToList();

            Assert.That(items, Is.Not.Empty);
            Assert.That(items.All(i => proxyIds.Contains(i.PortalInvoiceId)), Is.True);
        }

        [Test]
        public void Search_RejectedFilter_IsFilterRejected()
        {
            _portal.Handler.RejectedRsqlFields.Add("unhiem");

            var ex = Assert.Throws<PortalApiException>(() =>
                _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31), new InvoiceSearchFilter { ProxyInvoice = true }));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.FilterRejected));
        }

        [Test]
        public void Search_RangeLongerThanAMonth_IsRejectedByPortal()
        {
            var ex = Assert.Throws<PortalApiException>(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 3, 31)));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.FilterRejected));
        }

        [Test]
        public void Search_PortalAcceptsThirtyDaysAcrossTwoMonths_LikeTheCapturedSample()
        {
            // Mẫu thật: tdlap từ 02/10/2026 đến 01/11/2026 (30 ngày, hai tháng dương lịch) được cổng chấp nhận.
            Assert.DoesNotThrow(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 10, 2), D(2026, 11, 1)));
        }

        [Test]
        public void Search_BogusState_IsRejected()
        {
            var request = new InvoiceSearchRequest(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)) { Cursor = "###" };

            var ex = Assert.Throws<AggregateException>(() => _portal.Client.SearchAsync(request, CancellationToken.None).Wait());

            Assert.That(((PortalApiException)ex.InnerException).Kind, Is.EqualTo(PortalErrorKind.FilterRejected));
        }

        // ---- Header gửi đi ----

        [Test]
        public void Requests_CarryActionRequestIdAndDefaultHeaders_ButAuthIsNotLogged()
        {
            _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

            var headers = _portal.Handler.HeaderLog.Single();
            Assert.That(headers["action"], Does.StartWith("T%C3%ACm"));
            Assert.That(Guid.TryParse(headers["request-id"], out var _), Is.True);
            Assert.That(headers["end-point"], Is.EqualTo("/tra-cuu/tra-cuu-hoa-don"));
            Assert.That(headers["Accept-Language"], Is.EqualTo("vi"));
            Assert.That(headers.ContainsKey("Authorization"), Is.False);
        }

        [Test]
        public void RequestId_IsDifferentForEachRequest()
        {
            _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));
            _portal.SearchAll(InvoiceDirection.Sold, D(2026, 2, 1), D(2026, 2, 28));

            var ids = _portal.Handler.HeaderLog.Select(h => h["request-id"]).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public void WebViewUserAgent_IsUsedInsteadOfTheDefault()
        {
            using (var portal = new TestPortal(userAgent: "Mozilla/5.0 WebView-Test"))
            {
                portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

                Assert.That(portal.Handler.HeaderLog.Single()["User-Agent"], Is.EqualTo("Mozilla/5.0 WebView-Test"));
            }
        }

        [Test]
        public void DefaultUserAgent_IsUsedWhenWebViewDidNotProvideOne()
        {
            _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

            Assert.That(_portal.Handler.HeaderLog.Single()["User-Agent"], Does.Contain("Chrome"));
        }

        [Test]
        public void Cookies_AreSentWhenConfigured_AndNeverLoggedByTheMock()
        {
            // SendCookies = true theo cấu hình mặc định (jwt, JSESSIONID, cookie WAF của F5).
            using (var portal = new TestPortal(cookies: new Dictionary<string, string> { { "JSESSIONID", "S1" }, { "jwt", "J1" } }))
            {
                portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

                Assert.That(portal.Handler.HeaderLog.Single().ContainsKey("Cookie"), Is.False, "Cookie bị loại khỏi nhật ký của mock");
            }
        }

        // ---- Chi tiết ----

        [Test]
        public async Task Detail_ReturnsRawJsonWithLineItems()
        {
            var record = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)).First();

            var json = await _portal.Client.GetInvoiceDetailAsync(new PortalInvoiceRef(InvoiceDirection.Sold, record.Dto), CancellationToken.None);

            var obj = JObject.Parse(json);
            Assert.That((string)obj["khhdon"], Is.EqualTo(record.Dto.InvoiceSymbol));
            Assert.That(obj["hdhhdvu"], Is.InstanceOf<JArray>());
            Assert.That(((JArray)obj["hdhhdvu"]).Count, Is.GreaterThan(0));
            Assert.That(_portal.Handler.RequestLog.Single(), Does.Contain("/api/query/invoices/detail?nbmst=" + record.Dto.SellerTaxCode));
        }

        [Test]
        public void Detail_UnknownInvoice_IsFatal()
        {
            var dto = new PortalInvoiceDto { SellerTaxCode = "0100000000", InvoiceSymbol = "X", InvoiceNumber = "1", FormSymbol = "1", PortalInvoiceId = "x" };

            var ex = Assert.ThrowsAsync<PortalApiException>(() => _portal.Client.GetInvoiceDetailAsync(new PortalInvoiceRef(InvoiceDirection.Sold, dto), CancellationToken.None));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.Fatal));
        }

        // ---- Xuất XML (zip) ----

        [Test]
        public async Task ExportXml_ReturnsOnlyTheInvoiceXml_ExtractedFromTheZip()
        {
            var record = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)).First();

            var payload = await _portal.Client.DownloadXmlAsync(new PortalInvoiceRef(InvoiceDirection.Sold, record.Dto), CancellationToken.None);

            var xml = Encoding.UTF8.GetString(payload.Content);
            Assert.That(xml, Does.StartWith("<?xml").Or.StartWith("<HDon"));
            Assert.That(xml, Does.Contain("<KHHDon>" + record.Dto.InvoiceSymbol + "</KHHDon>"));
            Assert.That(xml, Does.Not.Contain("MOCK VIEWER"), "Không được lẫn trang xem HTML của gói zip");
            Assert.That(payload.SuggestedFileName, Is.EqualTo(record.Dto.SellerTaxCode + "_" + record.Dto.InvoiceSymbol + "_" + record.Dto.InvoiceNumber + ".xml"));
        }

        [Test]
        public void ExportXml_MalformedZip_IsSchemaChanged()
        {
            _portal.Handler.MalformedExportResponse = true;
            var record = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)).First();

            var ex = Assert.ThrowsAsync<PortalApiException>(() => _portal.Client.DownloadXmlAsync(new PortalInvoiceRef(InvoiceDirection.Sold, record.Dto), CancellationToken.None));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [Test]
        public void ExportXml_UnknownInvoice_IsFatal404()
        {
            var dto = new PortalInvoiceDto { SellerTaxCode = "0100000000", InvoiceSymbol = "X", InvoiceNumber = "1", FormSymbol = "1", PortalInvoiceId = "x" };

            var ex = Assert.ThrowsAsync<PortalApiException>(() => _portal.Client.DownloadXmlAsync(new PortalInvoiceRef(InvoiceDirection.Sold, dto), CancellationToken.None));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.Fatal));
            Assert.That(ex.HttpStatus, Is.EqualTo(404));
        }

        [Test]
        public async Task ExportXml_FileName_CannotEscapeTargetFolder()
        {
            // Tên file đến từ dữ liệu cổng: dù mẫu tên dùng đến id có ký tự đường dẫn, kết quả vẫn không thoát khỏi thư mục đích.
            _portal.Config.Download.FileNameTemplate = "{PortalInvoiceId}.xml";
            var record = _portal.Dataset.Query(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)).First();
            var invoice = new PortalInvoiceRef(InvoiceDirection.Sold, new PortalInvoiceDto
            {
                SellerTaxCode = record.Dto.SellerTaxCode, InvoiceSymbol = record.Dto.InvoiceSymbol, InvoiceNumber = record.Dto.InvoiceNumber,
                FormSymbol = record.Dto.FormSymbol, PortalInvoiceId = "..\\..\\evil"
            });

            var payload = await _portal.Client.DownloadXmlAsync(invoice, CancellationToken.None);

            Assert.That(payload.SuggestedFileName.IndexOfAny(Path.GetInvalidFileNameChars()), Is.EqualTo(-1));
            Assert.That(payload.SuggestedFileName, Does.EndWith(".xml"));
        }

        // ---- Lỗi và xác thực ----

        [Test]
        public void ExpiredToken_IsAuthExpired()
        {
            using (var portal = new TestPortal(token: MockInvoiceDataset.ExpiredTokenFor(TestPortal.TaxCode)))
            {
                var ex = Assert.Throws<PortalApiException>(() => portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));

                Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.AuthExpired));
                Assert.That(ex.HttpStatus, Is.EqualTo(401));
            }
        }

        [Test]
        public void MissingOrGarbageToken_IsAuthExpired()
        {
            foreach (var token in new[] { "garbage", "a.b.c", string.Empty })
            {
                using (var portal = new TestPortal(token: token))
                {
                    var ex = Assert.Throws<PortalApiException>(() => portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)), token);
                    Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.AuthExpired), token);
                }
            }
        }

        [Test]
        public void TokenOfAnotherTaxCode_SeesOnlyItsOwnData()
        {
            using (var portal = new TestPortal(token: MockInvoiceDataset.TokenFor("0109999999")))
            {
                var items = portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

                Assert.That(items.All(i => i.SellerTaxCode == "0109999999"), Is.True);
            }
        }

        [Test]
        public void Errors_RateLimited_CarriesRetryAfter_AndNextCallSucceeds()
        {
            _portal.Handler.EnqueueFailure(429, 7);

            var ex = Assert.Throws<PortalApiException>(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));

            Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.RateLimited));
            Assert.That(ex.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.DoesNotThrow(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));
        }

        [Test]
        public void Errors_ServerErrorAndNetworkFailure_AreTransient()
        {
            _portal.Handler.EnqueueFailure(503);
            _portal.Handler.EnqueueNetworkError();

            var server = Assert.Throws<PortalApiException>(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));
            var network = Assert.Throws<PortalApiException>(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));

            Assert.That(server.Kind, Is.EqualTo(PortalErrorKind.Transient));
            Assert.That(network.Kind, Is.EqualTo(PortalErrorKind.Transient));
        }

        [Test]
        public void Errors_Timeout_IsTransient_NotCancellation()
        {
            _portal.Config.Http.TimeoutSeconds = 1;
            _portal.Handler.Latency = TimeSpan.FromSeconds(3);
            using (var slow = _portal.NewClient(MockInvoiceDataset.TokenFor(TestPortal.TaxCode)))
            {
                var request = new InvoiceSearchRequest(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

                var ex = Assert.ThrowsAsync<PortalApiException>(() => slow.SearchAsync(request, CancellationToken.None));

                Assert.That(ex.Kind, Is.EqualTo(PortalErrorKind.Transient));
            }
        }

        [Test]
        public void Errors_CallerCancellation_PropagatesAsCancellation()
        {
            _portal.Handler.Latency = TimeSpan.FromSeconds(5);
            var cts = new CancellationTokenSource(100);
            var request = new InvoiceSearchRequest(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31));

            Assert.CatchAsync<OperationCanceledException>(() => _portal.Client.SearchAsync(request, cts.Token));
        }

        [Test]
        public void Errors_MalformedBody_AndMissingField_AreSchemaChanged()
        {
            _portal.Handler.MalformedSearchResponse = true;
            var malformed = Assert.Throws<PortalApiException>(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));
            _portal.Handler.MalformedSearchResponse = false;
            _portal.Handler.OmitJsonField = "shdon";
            var missing = Assert.Throws<PortalApiException>(() => _portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));

            Assert.That(malformed.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
            Assert.That(missing.Kind, Is.EqualTo(PortalErrorKind.SchemaChanged));
        }

        [Test]
        public void Errors_NeverLeakTokenOrBodyIntoMessages()
        {
            const string secret = "SECRETVALUE";
            var token = HoaDonDienTu.Application.Authentication.PortalToken.BuildUnsigned(TestPortal.TaxCode, DateTime.UtcNow.AddHours(-25), DateTime.UtcNow.AddHours(-1))
                + secret;
            using (var portal = new TestPortal(token: token))
            {
                var ex = Assert.Throws<PortalApiException>(() => portal.SearchAll(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)));

                Assert.That(ex.Message, Does.Not.Contain(secret));
                Assert.That(ex.Message, Does.Not.Contain("mock failure"));
                Assert.That(portal.Handler.RequestLog.Any(l => l.Contains(secret)), Is.False);
            }
        }

        [Test]
        public void ClientDispose_DoesNotDisposeSharedMockHandler()
        {
            using (var second = _portal.NewClient(MockInvoiceDataset.TokenFor(TestPortal.TaxCode)))
            {
                _portal.Client.Dispose();

                Assert.DoesNotThrow(() => second.SearchAsync(
                    new InvoiceSearchRequest(InvoiceDirection.Sold, D(2026, 1, 1), D(2026, 1, 31)), CancellationToken.None).Wait());
            }
        }

        // ---- Dữ liệu giả hợp lệ với mẫu thật ----

        [Test]
        public void MockXml_HasTheSameStructureAsTheRealInvoiceXml()
        {
            var record = _portal.Dataset.All.First();

            var xml = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(MockInvoiceDataset.BuildXml(record)));

            Assert.That(xml.Root.Name.LocalName, Is.EqualTo("HDon"));
            Assert.That(xml.Root.Element("DLHDon").Element("TTChung").Element("KHMSHDon"), Is.Not.Null);
            Assert.That(xml.Root.Element("DLHDon").Element("NDHDon").Element("NBan").Element("MST").Value, Is.EqualTo(record.Dto.SellerTaxCode));
            Assert.That(xml.Root.Element("MCCQT"), Is.Not.Null);
            Assert.That(xml.Root.Element("DSCKS"), Is.Not.Null);
        }

        [Test]
        public void MockSearchJson_UsesRealFieldNamesAndTypes()
        {
            var json = MockInvoiceDataset.ToSearchJson(_portal.Dataset.All.First(r => r.Dto.BuyerIdNumber == null));

            Assert.That(json["shdon"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(json["khmshdon"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(json["tthai"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(((string)json["tdlap"]), Does.EndWith("T17:00:00Z").Or.Contain("T"));
            Assert.That(json["nmcmnd"].Type, Is.EqualTo(JTokenType.Null));
        }
    }
}
#endif
