using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class DownloadStateMachineTests
    {
        [TestCase(DownloadSessionState.Draft, DownloadSessionState.Running, true)]
        [TestCase(DownloadSessionState.Draft, DownloadSessionState.Completed, false)]
        [TestCase(DownloadSessionState.Running, DownloadSessionState.WaitingForLogin, true)]
        [TestCase(DownloadSessionState.Running, DownloadSessionState.Failed, true)]
        [TestCase(DownloadSessionState.PausedByUser, DownloadSessionState.Running, true)]
        [TestCase(DownloadSessionState.WaitingForLogin, DownloadSessionState.Running, true)]
        [TestCase(DownloadSessionState.CompletedWithErrors, DownloadSessionState.Running, true)]
        [TestCase(DownloadSessionState.Completed, DownloadSessionState.Running, false)]
        [TestCase(DownloadSessionState.Cancelled, DownloadSessionState.Running, false)]
        [TestCase(DownloadSessionState.Failed, DownloadSessionState.Running, false)]
        public void Session_Transitions(DownloadSessionState from, DownloadSessionState to, bool allowed)
        {
            Assert.That(DownloadStateMachine.CanTransition(from, to), Is.EqualTo(allowed));
        }

        [TestCase(DownloadSegmentState.Pending, DownloadSegmentState.Running, true)]
        [TestCase(DownloadSegmentState.Pending, DownloadSegmentState.Completed, false)]
        [TestCase(DownloadSegmentState.Running, DownloadSegmentState.Failed, true)]
        [TestCase(DownloadSegmentState.Running, DownloadSegmentState.Paused, true)]
        [TestCase(DownloadSegmentState.Failed, DownloadSegmentState.Pending, true)]
        [TestCase(DownloadSegmentState.Completed, DownloadSegmentState.Pending, false)]
        [TestCase(DownloadSegmentState.Skipped, DownloadSegmentState.Running, false)]
        public void Segment_Transitions(DownloadSegmentState from, DownloadSegmentState to, bool allowed)
        {
            Assert.That(DownloadStateMachine.CanTransition(from, to), Is.EqualTo(allowed));
        }

        [Test]
        public void Session_InvalidTransition_Throws_AndKeepsState()
        {
            var session = NewSession();

            Assert.Throws<InvalidDownloadStateException>(() => session.TransitionTo(DownloadSessionState.Completed, DateTime.UtcNow));
            Assert.That(session.State, Is.EqualTo(DownloadSessionState.Draft));
        }

        [Test]
        public void Session_TracksStartAndFinishTimes()
        {
            var session = NewSession();
            var t0 = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

            session.TransitionTo(DownloadSessionState.Running, t0);
            session.TransitionTo(DownloadSessionState.Completed, t0.AddMinutes(5));

            Assert.That(session.StartedAtUtc, Is.EqualTo(t0));
            Assert.That(session.FinishedAtUtc, Is.EqualTo(t0.AddMinutes(5)));
        }

        private static DownloadSession NewSession()
        {
            return new DownloadSession(
                Guid.NewGuid(), CompanyId.NewId(), TaxCode.Parse("0101234567"), @"C:\data", InvoiceDirection.Purchase,
                PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(), DateTime.UtcNow, "tester");
        }
    }

    [TestFixture]
    public class DownloadOrchestratorTests
    {
        private static readonly CompanyId Id = CompanyId.NewId();
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private InMemoryDownloadRepository _repo;
        private InMemoryInvoiceStore _store;
        private FlakyStore _flaky;
        private bool _factoryThrows;
        private ScriptedClient _client;
        private FakeContext _context;
        private FakePortalSession _portal;
        private EntitlementService _entitlement;
        private TestClock _clock;
        private List<TimeSpan> _delays;
        private DownloadOrchestrator _orchestrator;
        private ProgressLog _progress;
        private NoLimit _limiter;

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryDownloadRepository();
            _store = new InMemoryInvoiceStore();
            _flaky = new FlakyStore(_store);
            _factoryThrows = false;
            _client = new ScriptedClient();
            _context = new FakeContext { Current = new CompanyContext(Id, Mst, "Công ty", @"C:\data\A", null) };
            _portal = new FakePortalSession { Snapshot = new PortalSessionSnapshot(Id, Mst, DateTime.UtcNow, 1) };
            _entitlement = new EntitlementService(AppMode.Service, false);
            _clock = new TestClock();
            _delays = new List<TimeSpan>();
            _progress = new ProgressLog();
            _limiter = new NoLimit();
            Build(DefaultRetryPolicy.Standard);
        }

        private void Build(IRetryPolicy retry)
        {
            _orchestrator = new DownloadOrchestrator(
                _repo, _context, _portal, () => { if (_factoryThrows) throw new InvalidOperationException("session removed"); return _client; }, _flaky, _limiter, retry, _entitlement, _clock, new NullLogger(),
                (span, token) =>
                {
                    _delays.Add(span);
                    return Task.FromResult(0);
                });
        }

        private async Task<Guid> CreateAsync(PeriodSpec period)
        {
            // Mua vào bắt buộc một kết quả kiểm tra; chọn sẵn "5" để mỗi tháng là một đoạn (nhiều lượt có test riêng).
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, period, new InvoiceSearchFilter { CheckResultCode = "5" }, new PeriodSplitter());
            return await _orchestrator.CreateSessionAsync(plan, CancellationToken.None);
        }

        private static string Day(int month) { return "2026-" + month.ToString("00"); }

        private async Task<DownloadSession> SessionAsync(Guid id)
        {
            return await _repo.GetSessionAsync(id, CancellationToken.None);
        }

        private async Task<IList<DownloadSegment>> SegmentsAsync(Guid id)
        {
            return await _repo.GetSegmentsAsync(id, CancellationToken.None);
        }

        // ---- Tạo phiên ----

        [Test]
        public async Task Create_BindsCompanyAndStorage_AndOneSegmentPerMonth()
        {
            var id = await CreateAsync(PeriodSpec.ForQuarter(2026, 4));

            var session = await SessionAsync(id);
            var segments = await SegmentsAsync(id);

            Assert.That(session.State, Is.EqualTo(DownloadSessionState.Draft));
            Assert.That(session.StorageRoot, Is.EqualTo(@"C:\data\A"));
            Assert.That(session.TaxCode, Is.EqualTo(Mst));
            Assert.That(segments.Select(s => s.Range.Month), Is.EqualTo(new[] { 10, 11, 12 }));
        }

        [Test]
        public void Create_ReadOnlyMode_IsDenied()
        {
            _entitlement.ApplyLicense(HoaDonDienTu.Domain.Licensing.LicenseStatus.Expired, true);
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 10), null, new PeriodSplitter());

            Assert.ThrowsAsync<EntitlementDeniedException>(() => _orchestrator.CreateSessionAsync(plan, CancellationToken.None));
        }

        [Test]
        public void Create_WithoutCompany_IsRejected()
        {
            _context.Current = null;
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 10), null, new PeriodSplitter());

            Assert.ThrowsAsync<StorageGuardException>(() => _orchestrator.CreateSessionAsync(plan, CancellationToken.None));
        }

        [Test]
        public void Create_PortalSessionOfAnotherTaxCode_IsRejected()
        {
            _portal.Snapshot = new PortalSessionSnapshot(Id, TaxCode.Parse("0109999999"), DateTime.UtcNow, 1);
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 10), null, new PeriodSplitter());

            Assert.ThrowsAsync<StorageGuardException>(() => _orchestrator.CreateSessionAsync(plan, CancellationToken.None));
        }

        // ---- Chạy thường ----

        [Test]
        public async Task Run_MultiplePages_FollowsCursorToTheEnd_AndCompletes()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), "c2");
            _client.AddPage("2026-10-01", "c2", Invoices("B", 2), "c3");
            _client.AddPage("2026-10-01", "c3", Invoices("C", 1), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, _progress, CancellationToken.None);

            var session = await SessionAsync(id);
            var segment = (await SegmentsAsync(id)).Single();
            Assert.That(session.State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Completed));
            Assert.That(segment.ReceivedCount, Is.EqualTo(5));
            Assert.That(segment.XmlSavedCount, Is.EqualTo(5));
            Assert.That(segment.PageIndex, Is.EqualTo(3));
            Assert.That(_client.SearchCursors, Is.EqualTo(new string[] { null, "c2", "c3" }));
            Assert.That(_store.Count, Is.EqualTo(5));
            Assert.That(session.FinishedAtUtc, Is.Not.Null);
        }

        [Test]
        public async Task Run_EmptyPeriod_CompletesWithZeroInvoices()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 0), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That((await SegmentsAsync(id)).Single().ReceivedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Run_Quarter_RunsMonthsSequentiallyInOrder()
        {
            foreach (var m in new[] { 10, 11, 12 })
            {
                _client.AddPage("2026-" + m + "-01", null, Invoices("M" + m + "-", 1), null);
            }

            var id = await CreateAsync(PeriodSpec.ForQuarter(2026, 4));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_client.SearchRanges, Is.EqualTo(new[] { "10-01..10-31", "11-01..11-30", "12-01..12-31" }));
            Assert.That(_client.MaxConcurrentCalls, Is.EqualTo(1));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
        }

        [Test]
        public async Task Create_PurchaseWithoutCheckResult_GetsThreePassesPerMonth_InOrder()
        {
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForQuarter(2026, 4), new InvoiceSearchFilter(), new PeriodSplitter());

            var id = await _orchestrator.CreateSessionAsync(plan, CancellationToken.None);

            var segments = await SegmentsAsync(id);
            Assert.That(segments.Count, Is.EqualTo(9));
            Assert.That(segments.Select(s => s.Index), Is.EqualTo(Enumerable.Range(0, 9)));
            Assert.That(segments.Select(s => s.CheckResultCode), Is.EqualTo(new[] { "5", "6", "8", "5", "6", "8", "5", "6", "8" }));
            Assert.That(segments.Take(3).Select(s => s.Range.Month).Distinct().Single(), Is.EqualTo(10));
            Assert.That((await SessionAsync(id)).Filter.CheckResultCode, Is.Null.Or.Empty, "bộ lọc của phiên giữ nguyên ý người dùng");
        }

        [Test]
        public async Task Create_PurchaseWithChosenCheckResult_StaysOneSegmentPerMonth()
        {
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForQuarter(2026, 4), new InvoiceSearchFilter { CheckResultCode = "8" }, new PeriodSplitter());

            var segments = await SegmentsAsync(await _orchestrator.CreateSessionAsync(plan, CancellationToken.None));

            Assert.That(segments.Count, Is.EqualTo(3));
            Assert.That(segments.All(s => s.CheckResultCode == null), Is.True);
        }

        [Test]
        public async Task Create_SoldWithoutCheckResult_StaysOneSegmentPerMonth_BecausePortalHasAll()
        {
            var plan = DownloadPlan.Create(InvoiceDirection.Sold, PeriodSpec.ForQuarter(2026, 4), new InvoiceSearchFilter(), new PeriodSplitter());

            var segments = await SegmentsAsync(await _orchestrator.CreateSessionAsync(plan, CancellationToken.None));

            Assert.That(segments.Count, Is.EqualTo(3));
            Assert.That(segments.All(s => s.CheckResultCode == null), Is.True);
        }

        [Test]
        public async Task Run_PurchaseThreePasses_SearchesEachCheckResultAndSavesEveryInvoiceOnce()
        {
            _client.AddPage("2026-10-01", null, Invoices("A-", 2), null);
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(), new PeriodSplitter());
            var id = await _orchestrator.CreateSessionAsync(plan, CancellationToken.None);

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_client.SearchCheckResults, Is.EqualTo(new[] { "5", "6", "8" }));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            var segments = await SegmentsAsync(id);
            Assert.That(segments.All(s => s.State == DownloadSegmentState.Completed), Is.True);
        }

        [Test]
        public async Task Run_Purchase_LabelsTheProgressWithTheCheckResult()
        {
            var plan = DownloadPlan.Create(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(), new PeriodSplitter());
            var id = await _orchestrator.CreateSessionAsync(plan, CancellationToken.None);

            await _orchestrator.RunAsync(id, _progress, CancellationToken.None);

            var labels = _progress.Items.Select(p => p.CurrentSegment).ToList();

            Assert.That(labels.Any(l => l.Contains("Đã cấp mã hóa đơn")), Is.True);
            Assert.That(labels.Any(l => l.Contains("máy tính tiền")), Is.True);
        }

        [Test]
        public async Task Run_ReportsProgress_WithTotalsAndFinalState()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, _progress, CancellationToken.None);

            var last = _progress.Items.Last();
            Assert.That(last.State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That(last.ReceivedCount, Is.EqualTo(3));
            Assert.That(_progress.Items.Any(p => p.State == DownloadSessionState.Running && p.SegmentIndex == 0 && p.ReceivedCount == 2), Is.True);
        }

        [Test]
        public async Task Run_RecordsEvents_WithoutSensitiveData()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var events = await _repo.GetEventsAsync(id, CancellationToken.None);
            Assert.That(events.Select(e => e.Code), Does.Contain("SESSION_CREATED").And.Contain("SEGMENT_COMPLETED").And.Contain("COMPLETED"));
        }

        // ---- Guard khi chạy ----

        [Test]
        public async Task Run_CompanyChangedSinceCreation_IsRejected()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "Khác", @"C:\data\B", null);

            Assert.ThrowsAsync<StorageGuardException>(() => _orchestrator.RunAsync(id, null, CancellationToken.None));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Draft));
        }

        [Test]
        public async Task Run_NotLoggedIn_WaitsForLogin_WithoutCallingThePortal()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _portal.Snapshot = null;

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.WaitingForLogin));
            Assert.That(_client.SearchCursors, Is.Empty);
        }

        [Test]
        public async Task Run_ReadOnlyMode_IsDeniedBeforeAnyWork()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _entitlement.ApplyLicense(HoaDonDienTu.Domain.Licensing.LicenseStatus.Expired, true);

            Assert.ThrowsAsync<EntitlementDeniedException>(() => _orchestrator.RunAsync(id, null, CancellationToken.None));
        }

        [Test]
        public async Task Run_SameSessionTwiceAtOnce_IsRejected()
        {
            var gate = new TaskCompletionSource<bool>();
            _client.SearchGate = gate.Task;
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            var first = _orchestrator.RunAsync(id, null, CancellationToken.None);
            Assert.ThrowsAsync<InvalidDownloadStateException>(() => _orchestrator.RunAsync(id, null, CancellationToken.None));

            gate.SetResult(true);
            await first;
        }

        // ---- Thử lại / lỗi ----

        [Test]
        public async Task Transient_RetriesWithGrowingBackoff_ThenSucceeds()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            _client.FailSearch(PortalErrorKind.Transient, 2);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That((await SegmentsAsync(id)).Single().RetryCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Transient_ExhaustedAfterThreeRetries_FailsSegmentButContinuesWithTheNext()
        {
            _client.AddPage("2026-11-01", null, Invoices("N", 1), null);
            _client.FailSearchFor("2026-10-01", PortalErrorKind.Transient, int.MaxValue);
            var id = await CreateAsync(PeriodSpec.Custom(new DateTime(2026, 10, 1), new DateTime(2026, 11, 30)));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var segments = await SegmentsAsync(id);
            Assert.That(_delays, Has.Count.EqualTo(3));
            Assert.That(segments[0].State, Is.EqualTo(DownloadSegmentState.Failed));
            Assert.That(segments[1].State, Is.EqualTo(DownloadSegmentState.Completed));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.CompletedWithErrors));
        }

        [Test]
        public async Task RateLimited_WaitsForRetryAfter()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            _client.FailSearch(PortalErrorKind.RateLimited, 1, TimeSpan.FromSeconds(30));
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(30) }));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
        }

        [Test]
        public async Task SchemaChanged_StopsThatSegment_WithoutRetry_AndRecordsNoInvoices()
        {
            _client.FailSearchFor("2026-10-01", PortalErrorKind.SchemaChanged, int.MaxValue);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_delays, Is.Empty);
            Assert.That(_store.Count, Is.EqualTo(0));
            Assert.That((await SegmentsAsync(id)).Single().State, Is.EqualTo(DownloadSegmentState.Failed));
            var events = await _repo.GetEventsAsync(id, CancellationToken.None);
            Assert.That(events.Select(e => e.Code), Does.Contain("SCHEMA_CHANGED"));
        }

        [Test]
        public async Task FilterRejected_FailsTheSession_AndSkipsTheRest()
        {
            _client.FailSearchFor("2026-10-01", PortalErrorKind.FilterRejected, int.MaxValue);
            var id = await CreateAsync(PeriodSpec.Custom(new DateTime(2026, 10, 1), new DateTime(2026, 11, 30)));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var segments = await SegmentsAsync(id);
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Failed));
            Assert.That(segments[0].State, Is.EqualTo(DownloadSegmentState.Failed));
            Assert.That(segments[1].State, Is.EqualTo(DownloadSegmentState.Skipped));
        }

        [Test]
        public async Task OneInvoiceXmlFails_OthersStillSaved_SegmentFailedForRetry()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            _client.FailXml("A2", PortalErrorKind.SchemaChanged);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var segment = (await SegmentsAsync(id)).Single();
            Assert.That(segment.ReceivedCount, Is.EqualTo(3));
            Assert.That(segment.XmlSavedCount, Is.EqualTo(2));
            Assert.That(segment.ErrorCount, Is.EqualTo(1));
            Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Failed));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.CompletedWithErrors));
        }

        [Test]
        public async Task RetrySegment_ReloadsFromStart_WithoutDuplicates_AndCompletes()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            _client.FailXml("A2", PortalErrorKind.SchemaChanged);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await _orchestrator.RunAsync(id, null, CancellationToken.None);
            var segmentId = (await SegmentsAsync(id)).Single().Id;
            _client.ClearXmlFailures();

            await _orchestrator.RetrySegmentAsync(id, segmentId, null, CancellationToken.None);

            var segment = (await SegmentsAsync(id)).Single();
            Assert.That(_store.Count, Is.EqualTo(3));
            Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Completed));
            Assert.That(segment.ReceivedCount, Is.EqualTo(3));
            Assert.That(segment.XmlSavedCount, Is.EqualTo(1), "Chỉ hóa đơn thiếu được lưu mới");
            Assert.That(segment.RetryCount, Is.EqualTo(1));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
        }

        [Test]
        public async Task RetrySegment_OnlyFailedSegmentsCanBeRetried()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await _orchestrator.RunAsync(id, null, CancellationToken.None);
            var segmentId = (await SegmentsAsync(id)).Single().Id;

            Assert.ThrowsAsync<InvalidDownloadStateException>(() => _orchestrator.RetrySegmentAsync(id, segmentId, null, CancellationToken.None));
        }

        // ---- Hết token ----

        [Test]
        public async Task AuthExpired_MidSegment_WaitsForLogin_KeepsCheckpoint_ThenResumesWithoutDuplicates()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), "c2");
            _client.AddPage("2026-10-01", "c2", Invoices("B", 2), null);
            _client.FailXml("B1", PortalErrorKind.AuthExpired);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var afterExpiry = (await SegmentsAsync(id)).Single();
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.WaitingForLogin));
            Assert.That(afterExpiry.State, Is.EqualTo(DownloadSegmentState.Paused));
            Assert.That(afterExpiry.Cursor, Is.EqualTo("c2"));
            Assert.That(afterExpiry.ItemOffset, Is.EqualTo(0), "Hóa đơn B1 chưa xử lý xong nên chưa được tính");
            Assert.That(afterExpiry.ReceivedCount, Is.EqualTo(2));
            Assert.That(_delays, Is.Empty, "Hết token không được thử lại");

            _client.ClearXmlFailures();
            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var done = (await SegmentsAsync(id)).Single();
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That(done.ReceivedCount, Is.EqualTo(4));
            Assert.That(_store.Count, Is.EqualTo(4));
            Assert.That(_client.SearchCursors, Is.EqualTo(new string[] { null, "c2", "c2" }), "Không tải lại trang đầu");
        }

        [Test]
        public async Task AuthExpired_OnSearch_WaitsForLogin()
        {
            _client.FailSearchFor("2026-10-01", PortalErrorKind.AuthExpired, int.MaxValue);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.WaitingForLogin));
        }

        [Test]
        public async Task PortalSessionRemovedMidRun_BehavesLikeExpiredToken()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _client.OnXmlDownloaded = count => { if (count == 1) _factoryThrows = true; };

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.WaitingForLogin));
            Assert.That((await SegmentsAsync(id)).Single().ItemOffset, Is.EqualTo(1));
        }

        // ---- Tạm dừng / hủy / tiếp tục ----

        [Test]
        public async Task Pause_StopsAfterCurrentInvoice_AndResumeContinuesFromThere()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 4), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _client.OnXmlDownloaded = count =>
            {
                if (count == 2) _orchestrator.PauseAsync(id, CancellationToken.None).Wait();
            };

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var paused = (await SegmentsAsync(id)).Single();
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
            Assert.That(paused.State, Is.EqualTo(DownloadSegmentState.Paused));
            Assert.That(paused.ItemOffset, Is.EqualTo(2));
            Assert.That(paused.ReceivedCount, Is.EqualTo(2));

            _client.OnXmlDownloaded = null;
            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var done = (await SegmentsAsync(id)).Single();
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That(done.ReceivedCount, Is.EqualTo(4));
            Assert.That(_store.Count, Is.EqualTo(4));
            Assert.That(_client.XmlRequests, Is.EqualTo(new[] { "A1", "A2", "A3", "A4" }), "Không tải lại hóa đơn đã xong");
        }

        [Test]
        public async Task Cancel_DuringRun_KeepsSavedData_AndSkipsRemainingSegments()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            var id = await CreateAsync(PeriodSpec.Custom(new DateTime(2026, 10, 1), new DateTime(2026, 11, 30)));
            _client.OnXmlDownloaded = count =>
            {
                if (count == 1) _orchestrator.CancelAsync(id, CancellationToken.None).Wait();
            };

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var segments = await SegmentsAsync(id);
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Cancelled));
            Assert.That(segments.All(s => s.State == DownloadSegmentState.Skipped), Is.True);
            Assert.That(_store.Count, Is.EqualTo(1), "Hóa đơn đã lưu được giữ nguyên (BR-011)");
        }

        [Test]
        public async Task Cancel_WhenNotRunning_CancelsDirectly()
        {
            var id = await CreateAsync(PeriodSpec.ForQuarter(2026, 4));

            await _orchestrator.CancelAsync(id, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Cancelled));
            Assert.That((await SegmentsAsync(id)).All(s => s.State == DownloadSegmentState.Skipped), Is.True);
        }

        [Test]
        public async Task Cancel_AFinishedSession_IsANoOp()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            await _orchestrator.CancelAsync(id, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
        }

        [Test]
        public async Task ProcessInterrupted_RunningSessionCanBePausedThenResumed()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            // Mô phỏng tiến trình thoát giữa chừng: phiên Running, đoạn Running, không có lượt chạy nào đang hoạt động.
            var session = await SessionAsync(id);
            session.TransitionTo(DownloadSessionState.Running, DateTime.UtcNow);
            await _repo.SaveSessionAsync(session, CancellationToken.None);
            var segment = (await SegmentsAsync(id)).Single();
            segment.TransitionTo(DownloadSegmentState.Running);
            segment.ReceivedCount = 1;
            segment.XmlSavedCount = 1;
            segment.ItemOffset = 1;
            await _repo.SaveSegmentAsync(segment, CancellationToken.None);

            var unfinished = await _orchestrator.GetUnfinishedAsync(CancellationToken.None);
            Assert.That(unfinished.Single().Session.Id, Is.EqualTo(id));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That(_client.XmlRequests, Is.EqualTo(new[] { "A2" }), "Tiếp tục từ checkpoint, bỏ qua hóa đơn đầu");
        }

        [Test]
        public async Task ProcessInterrupted_PauseMarksSessionPaused()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            var session = await SessionAsync(id);
            session.TransitionTo(DownloadSessionState.Running, DateTime.UtcNow);
            await _repo.SaveSessionAsync(session, CancellationToken.None);

            await _orchestrator.PauseAsync(id, CancellationToken.None);

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
        }

        [Test]
        public async Task ExternalCancellation_LeavesPausedCheckpoint_AndRethrows()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            var cts = new CancellationTokenSource();
            _client.OnXmlDownloaded = count =>
            {
                if (count == 1) cts.Cancel();
            };

            Assert.CatchAsync<OperationCanceledException>(() => _orchestrator.RunAsync(id, null, cts.Token));

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
            Assert.That((await SegmentsAsync(id)).Single().State, Is.EqualTo(DownloadSegmentState.Paused));
        }

        // ---- Kho ----

        [Test]
        public async Task StorageFull_PausesWithCheckpoint_NotFailed()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _flaky.FailOnCall = 2;

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            var segment = (await SegmentsAsync(id)).Single();
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
            Assert.That(segment.ReceivedCount, Is.EqualTo(1));
            Assert.That(segment.ItemOffset, Is.EqualTo(1));

            _flaky.FailOnCall = 0;
            await _orchestrator.RunAsync(id, null, CancellationToken.None);
            Assert.That(_store.Count, Is.EqualTo(3));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
        }

        // ---- Chạy tuần tự theo doanh nghiệp / tích hợp chuyển MST ----

        [Test]
        public async Task SecondSessionOfSameCompany_CannotRunWhileFirstIsRunning()
        {
            var gate = new TaskCompletionSource<bool>();
            _client.SearchGate = gate.Task;
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            _client.AddPage("2026-11-01", null, Invoices("B", 1), null);
            var first = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            var second = await CreateAsync(PeriodSpec.ForMonth(2026, 11));

            var running = _orchestrator.RunAsync(first, null, CancellationToken.None);
            Assert.ThrowsAsync<InvalidDownloadStateException>(() => _orchestrator.RunAsync(second, null, CancellationToken.None));

            gate.SetResult(true);
            await running;
        }

        [Test]
        public async Task JobService_ReportsRunning_AndPausesBeforeSwitch()
        {
            var gate = new TaskCompletionSource<bool>();
            _client.SearchGate = gate.Task;
            _client.AddPage("2026-10-01", null, Invoices("A", 3), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            var jobs = new DownloadJobService(_orchestrator);

            var running = _orchestrator.RunAsync(id, null, CancellationToken.None);
            Assert.That(await jobs.HasRunningDownloadAsync(Id, CancellationToken.None), Is.True);

            var pausing = jobs.PauseAsync(Id, CancellationToken.None);
            gate.SetResult(true);
            await pausing;
            await running;

            Assert.That(await jobs.HasRunningDownloadAsync(Id, CancellationToken.None), Is.False);
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
        }

        [Test]
        public async Task GetUnfinished_ListsOnlyUnfinishedSessionsOfTheCurrentCompany_WithCounts()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), null);
            var finished = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await _orchestrator.RunAsync(finished, null, CancellationToken.None);
            var open = await CreateAsync(PeriodSpec.ForQuarter(2026, 4));

            var list = await _orchestrator.GetUnfinishedAsync(CancellationToken.None);

            Assert.That(list.Select(s => s.Session.Id), Is.EqualTo(new[] { open }));
            Assert.That(list[0].SegmentCount, Is.EqualTo(3));
        }

        [Test]
        public async Task GetUnfinished_WithoutCompany_IsEmpty()
        {
            await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            _context.Current = null;

            Assert.That(await _orchestrator.GetUnfinishedAsync(CancellationToken.None), Is.Empty);
        }

        // ---- Khôi phục phiên bị ngắt lúc khởi động (T05-09) ----

        [Test]
        public async Task RecoverInterrupted_PausesOrphanRunningSession_AndItsRunningSegment_KeepingCheckpoint()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await MakeInterruptedAsync(id);

            var recovered = await _orchestrator.RecoverInterruptedAsync(CancellationToken.None);

            Assert.That(recovered, Is.EqualTo(1));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
            var segment = (await SegmentsAsync(id)).Single();
            Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Paused));
            Assert.That(segment.ItemOffset, Is.EqualTo(1), "Checkpoint được giữ");
            var events = await _repo.GetEventsAsync(id, CancellationToken.None);
            Assert.That(events.Any(e => e.Code == "INTERRUPTED"), Is.True);

            await _orchestrator.RunAsync(id, null, CancellationToken.None);
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
            Assert.That(_client.XmlRequests, Is.EqualTo(new[] { "A2" }), "Tiếp tục từ checkpoint, không tải lại hóa đơn đầu");
        }

        [Test]
        public async Task RecoverInterrupted_LeavesOtherStatesAlone()
        {
            var draft = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            var waiting = await CreateAsync(PeriodSpec.ForMonth(2026, 9));
            var session = await SessionAsync(waiting);
            session.TransitionTo(DownloadSessionState.Running, DateTime.UtcNow);
            session.TransitionTo(DownloadSessionState.WaitingForLogin, DateTime.UtcNow);
            await _repo.SaveSessionAsync(session, CancellationToken.None);

            var recovered = await _orchestrator.RecoverInterruptedAsync(CancellationToken.None);

            Assert.That(recovered, Is.EqualTo(0));
            Assert.That((await SessionAsync(draft)).State, Is.EqualTo(DownloadSessionState.Draft));
            Assert.That((await SessionAsync(waiting)).State, Is.EqualTo(DownloadSessionState.WaitingForLogin));
        }

        [Test]
        public async Task RecoverInterrupted_DoesNotTouchSessionThatIsReallyRunning()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), null);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            var gate = new TaskCompletionSource<bool>();
            _client.SearchGate = gate.Task;
            var run = _orchestrator.RunAsync(id, null, CancellationToken.None);
            Assert.That(_orchestrator.HasActiveRun(Id), Is.True);

            var recovered = await _orchestrator.RecoverInterruptedAsync(CancellationToken.None);
            gate.SetResult(true);
            await run;

            Assert.That(recovered, Is.EqualTo(0));
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.Completed));
        }

        [Test]
        public async Task RecoverInterrupted_ResumePressedWhileRecovering_IsRejected_NotOverwritten()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await MakeInterruptedAsync(id);
            var gate = new TaskCompletionSource<bool>();
            var orchestrator = new DownloadOrchestrator(
                new GatedSegmentRepository(_repo, gate.Task), _context, _portal, () => _client, _flaky, new NoLimit(), DefaultRetryPolicy.Standard, _entitlement, _clock,
                new NullLogger(), (span, token) => Task.FromResult(0));

            var recovering = orchestrator.RecoverInterruptedAsync(CancellationToken.None);
            Assert.That(orchestrator.HasActiveRun(Id), Is.True, "Phiên được giữ chỗ trong lúc khôi phục");
            Assert.ThrowsAsync<InvalidDownloadStateException>(() => orchestrator.RunAsync(id, null, CancellationToken.None));

            gate.SetResult(true);
            Assert.That(await recovering, Is.EqualTo(1));
            Assert.That(orchestrator.HasActiveRun(Id), Is.False);
            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
        }

        [Test]
        public async Task RecoverInterrupted_WithoutCompany_DoesNothing()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await MakeInterruptedAsync(id);
            _context.Current = null;

            Assert.That(await _orchestrator.RecoverInterruptedAsync(CancellationToken.None), Is.EqualTo(0));
        }

        [Test]
        public async Task InterruptedSessionRecovery_RunsWhenACompanyIsOpened_AndSkipsWhenClosed()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));
            await MakeInterruptedAsync(id);
            var manager = new FakeContextManager(_context);
            var recovery = new InterruptedSessionRecovery(manager, _orchestrator, new NullLogger());
            recovery.Attach();

            var current = _context.Current;
            _context.Current = null;
            manager.Raise();
            Assert.That(recovery.LastRun, Is.Null, "Đóng doanh nghiệp thì không khôi phục");

            _context.Current = current;
            manager.Raise();
            await recovery.LastRun;

            Assert.That((await SessionAsync(id)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
        }

        [Test]
        public async Task InterruptedSessionRecovery_SwallowsFailures()
        {
            var manager = new FakeContextManager(_context);
            var recovery = new InterruptedSessionRecovery(manager, new ThrowingOrchestrator(), new NullLogger());
            recovery.Attach();

            manager.Raise();

            Assert.That(async () => await recovery.LastRun, Throws.Nothing);
            await Task.Yield();
        }

        /// <summary>Mô phỏng tiến trình thoát giữa chừng: phiên Running, đoạn Running (đã nhận 1 hóa đơn), không có lượt chạy nào.</summary>
        private async Task MakeInterruptedAsync(Guid id)
        {
            var session = await SessionAsync(id);
            session.TransitionTo(DownloadSessionState.Running, DateTime.UtcNow);
            await _repo.SaveSessionAsync(session, CancellationToken.None);
            var segment = (await SegmentsAsync(id)).First();
            segment.TransitionTo(DownloadSegmentState.Running);
            segment.ReceivedCount = 1;
            segment.XmlSavedCount = 1;
            segment.ItemOffset = 1;
            await _repo.SaveSegmentAsync(segment, CancellationToken.None);
        }

        // ---- Giãn nhịp mọi lời gọi sang cổng Thuế ----

        [Test]
        public async Task EveryPortalCall_GoesThroughTheRateLimiter_SearchesAndEachInvoiceXmlAlike()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 2), null);
            _client.AddPage("2026-11-01", null, Invoices("B", 3), null);
            var id = await CreateAsync(PeriodSpec.Custom(new DateTime(2026, 10, 1), new DateTime(2026, 11, 30)));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_client.SearchCursors, Has.Count.EqualTo(2), "mỗi tháng một lần tra cứu khi chỉ có một trang");
            Assert.That(_client.XmlRequests, Has.Count.EqualTo(5), "mỗi hóa đơn một lần tải XML");
            Assert.That(_limiter.Waits, Is.EqualTo(_client.SearchCursors.Count + _client.XmlRequests.Count), "mọi lời gọi đều phải chờ giãn nhịp");
        }

        [TestCase(0, 3000)]
        [TestCase(500, 3000)]
        [TestCase(2999, 3000)]
        [TestCase(3000, 3000)]
        [TestCase(5000, 5000)]
        public void ReleaseInterval_IsAtLeastThreeSeconds_EvenIfConfiguredLower(int configured, int expected)
        {
            Assert.That(PortalOptions.EffectiveIntervalMs(configured), Is.EqualTo(expected));
            Assert.That(PortalOptions.MinimumSafeIntervalMs, Is.EqualTo(3000));
        }

        [Test]
        public async Task Retries_AreAlsoRateLimited()
        {
            _client.AddPage("2026-10-01", null, Invoices("A", 1), null);
            _client.FailSearch(PortalErrorKind.Transient, 2);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 10));

            await _orchestrator.RunAsync(id, null, CancellationToken.None);

            Assert.That(_client.SearchCursors, Has.Count.EqualTo(3), "hai lần lỗi tạm thời rồi một lần thành công");
            Assert.That(_limiter.Waits, Is.EqualTo(_client.SearchCursors.Count + _client.XmlRequests.Count));
        }

        // ---- Chính sách thử lại ----

        [TestCase(PortalErrorKind.AuthExpired)]
        [TestCase(PortalErrorKind.SchemaChanged)]
        [TestCase(PortalErrorKind.FilterRejected)]
        [TestCase(PortalErrorKind.Fatal)]
        public void RetryPolicy_NeverRetriesNonTransientErrors(PortalErrorKind kind)
        {
            Assert.That(DefaultRetryPolicy.Standard.NextDelay(1, new PortalApiException(kind, "x", null, null)), Is.Null);
        }

        [Test]
        public void RetryPolicy_BacksOffExponentially_AndStopsAfterMax()
        {
            var policy = new DefaultRetryPolicy(3, TimeSpan.FromSeconds(1));
            var error = new PortalApiException(PortalErrorKind.Transient, "x", 503, null);

            Assert.That(policy.NextDelay(1, error), Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(policy.NextDelay(2, error), Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(policy.NextDelay(3, error), Is.EqualTo(TimeSpan.FromSeconds(4)));
            Assert.That(policy.NextDelay(4, error), Is.Null);
        }

        // ---- Helpers ----

        private static IList<PortalInvoiceDto> Invoices(string prefix, int count)
        {
            var list = new List<PortalInvoiceDto>();
            for (var i = 1; i <= count; i++)
            {
                list.Add(new PortalInvoiceDto { PortalInvoiceId = prefix + i, InvoiceNumber = i.ToString("00000000"), IssueDate = new DateTime(2026, 10, 5) });
            }

            return list;
        }

        private sealed class ProgressLog : IProgress<DownloadProgress>
        {
            public List<DownloadProgress> Items = new List<DownloadProgress>();

            public void Report(DownloadProgress value)
            {
                Items.Add(value);
            }
        }

        private sealed class ScriptedClient : IPortalInvoiceClient
        {
            private readonly Dictionary<string, InvoicePage> _pages = new Dictionary<string, InvoicePage>();
            private readonly Dictionary<string, Queue<PortalApiException>> _searchFailures = new Dictionary<string, Queue<PortalApiException>>();
            private readonly Dictionary<string, PortalErrorKind> _xmlFailures = new Dictionary<string, PortalErrorKind>();
            private int _concurrent;
            private int _xmlCount;

            public List<string> SearchCursors = new List<string>();
            public List<string> SearchRanges = new List<string>();
            public List<string> SearchCheckResults = new List<string>();
            public List<string> XmlRequests = new List<string>();
            public int MaxConcurrentCalls;
            public Task SearchGate;
            public Action<int> OnXmlDownloaded;

            public void AddPage(string fromIso, string cursor, IList<PortalInvoiceDto> items, string next)
            {
                _pages[fromIso + "|" + cursor] = new InvoicePage(items, null, next);
            }

            public void FailSearch(PortalErrorKind kind, int times, TimeSpan? retryAfter = null)
            {
                FailSearchFor("*", kind, times, retryAfter);
            }

            public void FailSearchFor(string fromIso, PortalErrorKind kind, int times, TimeSpan? retryAfter = null)
            {
                var queue = new Queue<PortalApiException>();
                for (var i = 0; i < Math.Min(times, 50); i++) queue.Enqueue(new PortalApiException(kind, "scripted", 500, retryAfter));
                _searchFailures[fromIso] = queue;
            }

            public void FailXml(string invoiceId, PortalErrorKind kind)
            {
                _xmlFailures[invoiceId] = kind;
            }

            public void ClearXmlFailures()
            {
                _xmlFailures.Clear();
            }

            public async Task<InvoicePage> SearchAsync(InvoiceSearchRequest request, CancellationToken cancellationToken)
            {
                Enter();
                try
                {
                    if (SearchGate != null) await SearchGate;
                    SearchCursors.Add(request.Cursor);
                    SearchCheckResults.Add(request.Filter == null ? null : request.Filter.CheckResultCode);
                    SearchRanges.Add(request.From.ToString("MM-dd") + ".." + request.To.ToString("MM-dd"));
                    var key = request.From.ToString("yyyy-MM-dd");
                    Queue<PortalApiException> failures;
                    if ((_searchFailures.TryGetValue(key, out failures) || _searchFailures.TryGetValue("*", out failures)) && failures.Count > 0)
                    {
                        throw failures.Dequeue();
                    }

                    InvoicePage page;
                    if (!_pages.TryGetValue(key + "|" + request.Cursor, out page)) page = new InvoicePage(null, null, null);
                    return page;
                }
                finally
                {
                    Leave();
                }
            }

            public Task<InvoiceXmlPayload> DownloadXmlAsync(PortalInvoiceRef invoice, CancellationToken cancellationToken)
            {
                Enter();
                try
                {
                    PortalErrorKind kind;
                    if (_xmlFailures.TryGetValue(invoice.PortalInvoiceId, out kind))
                    {
                        throw new PortalApiException(kind, "scripted", 500, null);
                    }

                    XmlRequests.Add(invoice.PortalInvoiceId);
                    _xmlCount++;
                    var payload = new InvoiceXmlPayload(new byte[] { 1, 2, 3 }, invoice.PortalInvoiceId + ".xml");
                    if (OnXmlDownloaded != null) OnXmlDownloaded(_xmlCount);
                    return Task.FromResult(payload);
                }
                finally
                {
                    Leave();
                }
            }

            private void Enter()
            {
                var now = Interlocked.Increment(ref _concurrent);
                if (now > MaxConcurrentCalls) MaxConcurrentCalls = now;
            }

            private void Leave()
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }

        /// <summary>Bọc kho trong bộ nhớ và ném IOException ở lần gọi thứ N (mô phỏng hết dung lượng).</summary>
        private sealed class FlakyStore : IInvoiceStore
        {
            private readonly InMemoryInvoiceStore _inner;
            private int _calls;

            public FlakyStore(InMemoryInvoiceStore inner)
            {
                _inner = inner;
            }

            public int FailOnCall { get; set; }

            public Task<InvoiceSaveResult> UpsertAsync(DownloadSession session, PortalInvoiceDto invoice, InvoiceXmlPayload xml, CancellationToken cancellationToken)
            {
                _calls++;
                if (FailOnCall > 0 && _calls == FailOnCall) throw new IOException("disk full");
                return _inner.UpsertAsync(session, invoice, xml, cancellationToken);
            }
        }

        private sealed class NoLimit : IRateLimiter
        {
            public int Waits;

            public Task WaitAsync(CancellationToken cancellationToken)
            {
                Waits++;
                return Task.FromResult(0);
            }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }

        /// <summary>Giữ lần lưu đoạn đầu tiên cho tới khi cổng mở, để dựng đúng khoảnh khắc đang khôi phục dở.</summary>
        private sealed class GatedSegmentRepository : IDownloadRepository
        {
            private readonly IDownloadRepository _inner;
            private readonly Task _gate;

            public GatedSegmentRepository(IDownloadRepository inner, Task gate)
            {
                _inner = inner;
                _gate = gate;
            }

            public Task CreateAsync(DownloadSession session, IList<DownloadSegment> segments, CancellationToken ct) { return _inner.CreateAsync(session, segments, ct); }
            public Task<DownloadSession> GetSessionAsync(Guid sessionId, CancellationToken ct) { return _inner.GetSessionAsync(sessionId, ct); }
            public Task<IList<DownloadSegment>> GetSegmentsAsync(Guid sessionId, CancellationToken ct) { return _inner.GetSegmentsAsync(sessionId, ct); }
            public Task SaveSessionAsync(DownloadSession session, CancellationToken ct) { return _inner.SaveSessionAsync(session, ct); }

            public async Task SaveSegmentAsync(DownloadSegment segment, CancellationToken ct)
            {
                await _gate;
                await _inner.SaveSegmentAsync(segment, ct);
            }

            public Task AddEventAsync(DownloadEvent downloadEvent, CancellationToken ct) { return _inner.AddEventAsync(downloadEvent, ct); }
            public Task<IList<DownloadEvent>> GetEventsAsync(Guid sessionId, CancellationToken ct) { return _inner.GetEventsAsync(sessionId, ct); }
            public Task<IList<DownloadSession>> GetUnfinishedAsync(CompanyId companyId, CancellationToken ct) { return _inner.GetUnfinishedAsync(companyId, ct); }
            public Task<IList<DownloadSession>> GetSessionsAsync(CompanyId companyId, CancellationToken ct) { return _inner.GetSessionsAsync(companyId, ct); }
            public Task<IDictionary<Guid, IList<DownloadSegment>>> GetSegmentsByCompanyAsync(CompanyId companyId, CancellationToken ct) { return _inner.GetSegmentsByCompanyAsync(companyId, ct); }
        }

        private sealed class FakeContextManager : ICompanyContextManager
        {
            private readonly FakeContext _inner;

            public FakeContextManager(FakeContext inner)
            {
                _inner = inner;
            }

            public event EventHandler ContextChanged;
            public bool HasCurrent { get { return _inner.HasCurrent; } }
            public CompanyContext Current { get { return _inner.Current; } }
            public Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public void CloseCurrent() { }

            public void Raise()
            {
                var handler = ContextChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private sealed class ThrowingOrchestrator : IDownloadOrchestrator
        {
            public Task<Guid> CreateSessionAsync(DownloadPlan plan, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task RunAsync(Guid sessionId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task PauseAsync(Guid sessionId, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task CancelAsync(Guid sessionId, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task RetrySegmentAsync(Guid sessionId, Guid segmentId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task<IList<DownloadSessionSummary>> GetUnfinishedAsync(CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken) { throw new InvalidOperationException("Kho hỏng (giả)"); }
        }

        private sealed class FakePortalSession : IPortalSessionStore
        {
            public PortalSessionSnapshot Snapshot { get; set; }
            public event EventHandler SessionChanged { add { } remove { } }
            public bool HasActiveSession { get { return Snapshot != null; } }
            public PortalSessionSnapshot GetSnapshot() { return Snapshot; }
            public PortalSession GetSessionForApi() { return null; }
            public void Set(PortalSession session) { }
            public void Clear() { }
        }

        private sealed class TestClock : IClock
        {
            private DateTime _now = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

            public DateTime UtcNow
            {
                get
                {
                    _now = _now.AddSeconds(1);
                    return _now;
                }
            }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
