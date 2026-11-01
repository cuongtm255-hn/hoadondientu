#if DEBUG
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class PortalAuthAndSupportTests
    {
        private const string Mst = "0101234567";

        private static CompanyContext Company(string mst = Mst)
        {
            return new CompanyContext(CompanyId.NewId(), TaxCode.Parse(mst), "Công ty mẫu", @"C:\data\" + mst, null);
        }

        private static PortalLoginResult Verify(MockPortalHandler handler, PortalApiConfig config, PortalSessionMaterial material, CompanyContext company = null)
        {
            var service = new PortalAuthenticationService(config, new FixedClock(), () => handler);
            return service.VerifyAsync(company ?? Company(), material, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static PortalSessionMaterial Token(string mst)
        {
            return new PortalSessionMaterial(null, null, MockInvoiceDataset.TokenFor(mst));
        }

        // ---- PortalAuthenticationService ----

        [TestCaseSource(typeof(Variants), "All")]
        public void Verify_MatchingTaxCode_Succeeds_AndSessionBelongsToTheCompany(string variant)
        {
            var config = Variants.Create(variant);
            var company = Company();

            var result = Verify(new MockPortalHandler(config), config, Token(Mst), company);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Session.CompanyId, Is.EqualTo(company.CompanyId));
            Assert.That(result.Session.VerifiedTaxCode.Value, Is.EqualTo(Mst));
        }

        [Test]
        public void Verify_OtherTaxCode_IsMismatch_NoSession()
        {
            var config = Variants.Create(Variants.Default);

            var result = Verify(new MockPortalHandler(config), config, Token("0109999999"));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.TaxCodeMismatch));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void Verify_ExpiredToken_IsInvalidSession()
        {
            var config = Variants.Create(Variants.Default);

            var result = Verify(new MockPortalHandler(config), config, new PortalSessionMaterial(null, null, "mock-token-expired"));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.InvalidSession));
        }

        [Test]
        public void Verify_NetworkAndServerErrors_ArePortalUnavailable()
        {
            var config = Variants.Create(Variants.Default);
            var handler = new MockPortalHandler(config);

            handler.EnqueueNetworkError();
            var network = Verify(handler, config, Token(Mst));
            handler.EnqueueFailure(502);
            var server = Verify(handler, config, Token(Mst));
            handler.EnqueueFailure(429, 1);
            var limited = Verify(handler, config, Token(Mst));

            Assert.That(network.Outcome, Is.EqualTo(PortalLoginOutcome.PortalUnavailable));
            Assert.That(server.Outcome, Is.EqualTo(PortalLoginOutcome.PortalUnavailable));
            Assert.That(limited.Outcome, Is.EqualTo(PortalLoginOutcome.PortalUnavailable));
        }

        [Test]
        public void Verify_ProfileWithoutTaxCode_IsInvalidSession()
        {
            var config = Variants.Create(Variants.Default);
            config.Profile.TaxCodePath = "data.somethingElse";
            var handler = new MockPortalHandler(config);
            // Mock vẫn ghi MST vào đường dẫn đã cấu hình; đổi cấu hình sau khi mock dựng để mô phỏng cổng đổi cấu trúc.
            var verifyConfig = Variants.Create(Variants.Default);
            verifyConfig.Profile.TaxCodePath = "data.taxCodeMovedElsewhere";

            var result = Verify(handler, verifyConfig, Token(Mst));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.InvalidSession));
        }

        [Test]
        public void Verify_ResultAndFailures_NeverContainToken()
        {
            var config = Variants.Create(Variants.Default);
            var material = new PortalSessionMaterial(null, null, "mock-token-expired-SECRETVALUE");

            var result = Verify(new MockPortalHandler(config), config, material);

            Assert.That(result.Message ?? string.Empty, Does.Not.Contain("SECRETVALUE"));
            Assert.That(result.Session, Is.Null);
        }

        // ---- PortalApiClientFactory + provider ----

        [Test]
        public void Factory_CreatesOneClientPerSessionGeneration_UsingTheSessionMaterial()
        {
            var config = Variants.Create(Variants.Default);
            var handler = new MockPortalHandler(config);
            var factory = new PortalApiClientFactory(config, () => handler);
            var company = Company();
            var session = new PortalSession(company.CompanyId, company.TaxCode, Token(Mst), DateTime.UtcNow);

            using (var client = factory.Create(company, session))
            {
                var page = client.SearchAsync(
                    new HoaDonDienTu.Application.Portal.InvoiceSearchRequest(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)),
                    CancellationToken.None).GetAwaiter().GetResult();

                Assert.That(page.Items.Count, Is.EqualTo(1));
            }
        }

        // ---- MinIntervalRateLimiter ----

        [Test]
        public void RateLimiter_SpacesCalls()
        {
            var limiter = new MinIntervalRateLimiter(80);
            var watch = Stopwatch.StartNew();

            for (var i = 0; i < 4; i++) limiter.WaitAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(220), "Ba khoảng chờ 80ms giữa bốn lần gọi");
        }

        [Test]
        public void RateLimiter_ZeroInterval_DoesNotDelay()
        {
            var limiter = new MinIntervalRateLimiter(0);
            var watch = Stopwatch.StartNew();

            for (var i = 0; i < 50; i++) limiter.WaitAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500));
        }

        [Test]
        public void RateLimiter_ConcurrentCallers_AreStillSpaced()
        {
            var limiter = new MinIntervalRateLimiter(60);
            var watch = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, 4).Select(i => Task.Run(() => limiter.WaitAsync(CancellationToken.None))).ToArray();
            Task.WaitAll(tasks);

            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(160));
        }

        [Test]
        public void RateLimiter_Cancellation_Throws()
        {
            var limiter = new MinIntervalRateLimiter(5000);
            limiter.WaitAsync(CancellationToken.None).GetAwaiter().GetResult();
            using (var cts = new CancellationTokenSource(50))
            {
                Assert.Throws<TaskCanceledException>(() => limiter.WaitAsync(cts.Token).GetAwaiter().GetResult());
            }
        }

        // ---- MockInvoiceDataset ----

        [Test]
        public void Dataset_IsDeterministic_PerTaxCode()
        {
            var a1 = new MockInvoiceDataset(Mst);
            var a2 = new MockInvoiceDataset(Mst);
            var b = new MockInvoiceDataset("0109999999");

            Assert.That(a1.All.Select(r => r.Dto.PortalInvoiceId), Is.EqualTo(a2.All.Select(r => r.Dto.PortalInvoiceId)));
            Assert.That(a1.All.Select(r => r.Dto.TotalPayment), Is.EqualTo(a2.All.Select(r => r.Dto.TotalPayment)));
            Assert.That(a1.All.Select(r => r.Dto.PortalInvoiceId).Intersect(b.All.Select(r => r.Dto.PortalInvoiceId)), Is.Empty);
        }

        [Test]
        public void Dataset_CoversTheScenariosNeededByDownstreamFeatures()
        {
            var data = new MockInvoiceDataset(Mst);

            Assert.That(data.Query(InvoiceDirection.Purchase, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)).Count, Is.EqualTo(120));
            Assert.That(data.Query(InvoiceDirection.Sold, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31)), Is.Empty);
            Assert.That(data.Query(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)).Count, Is.EqualTo(1));
            Assert.That(data.All.Select(r => r.Dto.PortalInvoiceId).Distinct().Count(), Is.EqualTo(data.All.Count), "ID cổng duy nhất");
            Assert.That(data.All.Any(r => r.Dto.SellerTaxCode.Contains("-")), Is.True, "Có MST đơn vị phụ thuộc");
            Assert.That(data.All.Any(r => r.Dto.TotalFee != null) && data.All.Any(r => r.Dto.TotalFee == null), Is.True);
            Assert.That(data.All.All(r => r.Dto.TotalPayment >= r.Dto.TotalBeforeTax - (r.Dto.TotalDiscount ?? 0m)), Is.True);
        }

        [Test]
        public void Dataset_Xml_IsWellFormed_AndMarkedAsMock()
        {
            var data = new MockInvoiceDataset(Mst);
            var record = data.All.First();

            var xml = System.Text.Encoding.UTF8.GetString(MockInvoiceDataset.BuildXml(record));

            Assert.That(xml, Does.Contain("DỮ LIỆU GIẢ"));
            Assert.That(xml[0], Is.EqualTo('<'), "Kh\u00F4ng c\u00F3 BOM \u1EDF \u0111\u1EA7u file");
            Assert.DoesNotThrow(() => System.Xml.Linq.XDocument.Parse(xml));
        }

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow
            {
                get { return new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc); }
            }
        }
    }
}

#endif
