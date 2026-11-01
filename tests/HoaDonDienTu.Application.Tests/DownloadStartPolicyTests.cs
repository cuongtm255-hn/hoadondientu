using System;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class DownloadStartPolicyTests
    {
        private static readonly CompanyId Id = CompanyId.NewId();
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");
        private static readonly PeriodValidationResult ValidPeriod = new PeriodValidationResult(null, null);
        private static readonly StorageProbeResult Writable = StorageProbeResult.Ok();

        private Context _context;
        private Session _session;
        private Entitlement _entitlement;
        private DownloadStartPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _context = new Context { Current = new CompanyContext(Id, Mst, "A", @"C:\data\A", null) };
            _session = new Session { Snapshot = new PortalSessionSnapshot(Id, Mst, DateTime.UtcNow, 1) };
            _entitlement = new Entitlement { Mode = AppMode.Service };
            _policy = new DownloadStartPolicy(_context, _session, _entitlement);
        }

        [Test]
        public void AllConditionsMet_Allowed()
        {
            var decision = _policy.Evaluate(ValidPeriod, Writable);

            Assert.That(decision.CanStart, Is.True);
            Assert.That(decision.Reason, Is.Null);
        }

        [Test]
        public void NoCompany_Denied()
        {
            _context.Current = null;

            Assert.That(_policy.Evaluate(ValidPeriod, Writable).Reason, Does.Contain("doanh nghiệp"));
        }

        [Test]
        public void ReadOnlyMode_Denied()
        {
            _entitlement.Mode = AppMode.ReadOnlyService;

            var decision = _policy.Evaluate(ValidPeriod, Writable);

            Assert.That(decision.CanStart, Is.False);
            Assert.That(decision.Reason, Does.Contain("không cho phép"));
        }

        [Test]
        public void NotLoggedIn_Denied_NamingTheTaxCode()
        {
            _session.Snapshot = null;

            var decision = _policy.Evaluate(ValidPeriod, Writable);

            Assert.That(decision.CanStart, Is.False);
            Assert.That(decision.Reason, Does.Contain("0101234567"));
        }

        [Test]
        public void SessionOfAnotherTaxCode_Denied()
        {
            _session.Snapshot = new PortalSessionSnapshot(Id, TaxCode.Parse("0109999999"), DateTime.UtcNow, 1);

            Assert.That(_policy.Evaluate(ValidPeriod, Writable).CanStart, Is.False);
        }

        [Test]
        public void SessionOfAnotherCompany_Denied()
        {
            _session.Snapshot = new PortalSessionSnapshot(CompanyId.NewId(), Mst, DateTime.UtcNow, 1);

            Assert.That(_policy.Evaluate(ValidPeriod, Writable).CanStart, Is.False);
        }

        [Test]
        public void InvalidPeriod_Denied_WithFirstError()
        {
            var invalid = new PeriodSplitter().Validate(PeriodSpec.Custom(new DateTime(2026, 3, 10), new DateTime(2026, 3, 9)));

            var decision = _policy.Evaluate(invalid, Writable);

            Assert.That(decision.CanStart, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(invalid.Errors[0]));
        }

        [Test]
        public void NullPeriod_Denied()
        {
            Assert.That(_policy.Evaluate(null, Writable).CanStart, Is.False);
        }

        [Test]
        public void StorageNotWritable_Denied_WithReason()
        {
            var decision = _policy.Evaluate(ValidPeriod, StorageProbeResult.Failed("không có quyền ghi"));

            Assert.That(decision.CanStart, Is.False);
            Assert.That(decision.Reason, Does.Contain("không có quyền ghi"));
            Assert.That(_policy.Evaluate(ValidPeriod, null).CanStart, Is.False);
        }

        [Test]
        public void Priority_CompanyThenEntitlementThenLoginThenPeriodThenStorage()
        {
            var invalid = new PeriodSplitter().Validate(PeriodSpec.ForMonth(2026, 13));
            _session.Snapshot = null;

            // Chưa đăng nhập được báo trước kỳ sai và thư mục lỗi.
            Assert.That(_policy.Evaluate(invalid, StorageProbeResult.Failed("x")).Reason, Does.Contain("đăng nhập"));

            _session.Snapshot = new PortalSessionSnapshot(Id, Mst, DateTime.UtcNow, 1);
            Assert.That(_policy.Evaluate(invalid, StorageProbeResult.Failed("x")).Reason, Is.EqualTo(invalid.Errors[0]));
        }

        // ---- DownloadPlan ----

        [Test]
        public void Plan_Create_UsesSplitter_AndCopiesFilter()
        {
            var filter = new InvoiceSearchFilter { CounterpartyTaxCode = "0100100001" };

            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForYear(2026), filter, new PeriodSplitter());
            filter.CounterpartyTaxCode = "changed-later";

            Assert.That(plan.Segments.Count, Is.EqualTo(12));
            Assert.That(plan.Direction, Is.EqualTo(InvoiceDirection.Purchase));
            Assert.That(plan.Filter.CounterpartyTaxCode, Is.EqualTo("0100100001"));
        }

        [Test]
        public void Plan_Create_RejectsInvalidPeriod_AndNullSplitter()
        {
            Assert.Throws<ArgumentException>(() =>
                DownloadPlan.Create(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 0), null, new PeriodSplitter()));
            Assert.Throws<ArgumentNullException>(() =>
                DownloadPlan.Create(InvoiceDirection.Sold, PeriodSpec.ForYear(2026), null, null));
        }

        [Test]
        public void Plan_Segments_AreReadOnly()
        {
            var plan = DownloadPlan.Create(InvoiceDirection.Sold, PeriodSpec.ForYear(2026), null, new PeriodSplitter());

            Assert.Throws<NotSupportedException>(() => plan.Segments.Clear());
        }

        private sealed class Context : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }

        private sealed class Session : IPortalSessionStore
        {
            public PortalSessionSnapshot Snapshot { get; set; }
            public event EventHandler SessionChanged { add { } remove { } }
            public bool HasActiveSession { get { return Snapshot != null; } }
            public PortalSessionSnapshot GetSnapshot() { return Snapshot; }
            public PortalSession GetSessionForApi() { return null; }
            public void Set(PortalSession session) { }
            public void Clear() { }
        }

        private sealed class Entitlement : IEntitlementService
        {
            public AppMode Mode { get; set; }
            public AppMode CurrentMode { get { return Mode; } }
            public bool IsAllowed(AppFeature feature) { return Mode == AppMode.Service || Mode == AppMode.Standard; }
            public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }
    }
}
