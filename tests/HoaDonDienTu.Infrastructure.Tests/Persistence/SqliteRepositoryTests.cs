#if DEBUG
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
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Invoices;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Tests.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>Repository SQLite trên file tạm; luồng tải dùng cổng giả (MOCK), không gọi cổng Thuế.</summary>
    [TestFixture]
    public class SqliteRepositoryTests
    {
        private const string Mst = "0109213133";

        private string _root;
        private FixedContext _context;
        private CompanyDatabaseProvider _provider;
        private SqliteInvoiceRepository _invoices;
        private SqliteDownloadRepository _downloads;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-repo-" + Guid.NewGuid().ToString("N"), "Kho Công ty", Mst);
            _context = new FixedContext(_root, CompanyId.NewId());
            Reopen();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(_root)), true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Mô phỏng tắt rồi mở lại ứng dụng: repository mới, không giữ gì trong bộ nhớ.</summary>
        private void Reopen()
        {
            _provider = new CompanyDatabaseProvider(_context);
            _invoices = new SqliteInvoiceRepository(_provider);
            _downloads = new SqliteDownloadRepository(_provider);
        }

        // ---- Provider ----

        [Test]
        public void Provider_WithoutCompany_IsRejected()
        {
            var provider = new CompanyDatabaseProvider(new NoCompany());

            Assert.Throws<StorageGuardException>(() => provider.Current());
        }

        [Test]
        public async Task Provider_CreatesCompanyDbInsideTheCompanyDataFolder()
        {
            await _invoices.ListAsync(CancellationToken.None);

            Assert.That(File.Exists(Path.Combine(_root, "Data", "company.db")), Is.True);
            Assert.That(_provider.Current(), Is.SameAs(_provider.Current()), "một đối tượng cho mỗi file để ghi tuần tự");
        }

        [Test]
        public void Enums_StoredAsNumbers_MustKeepTheirValues()
        {
            Assert.That(new[] { (int)DownloadSessionState.Draft, (int)DownloadSessionState.Running, (int)DownloadSessionState.Completed, (int)DownloadSessionState.CompletedWithErrors,
                (int)DownloadSessionState.PausedByUser, (int)DownloadSessionState.WaitingForLogin, (int)DownloadSessionState.Cancelled, (int)DownloadSessionState.Failed },
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), "SQL lọc phiên chưa kết thúc theo (0, 1, 4, 5)");
            Assert.That(new[] { (int)DownloadSegmentState.Pending, (int)DownloadSegmentState.Running, (int)DownloadSegmentState.Completed, (int)DownloadSegmentState.Failed,
                (int)DownloadSegmentState.Paused, (int)DownloadSegmentState.Skipped }, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(new[] { (int)InvoiceDirection.Purchase, (int)InvoiceDirection.Sold }, Is.EqualTo(new[] { 0, 1 }));
        }

        // ---- Hóa đơn ----

        [Test]
        public async Task IntegrityResult_IsPersisted_SurvivesReopening_AndIsClearedByAFreshVersion()
        {
            var bad = NewInvoice("0000041", 1);
            var good = NewInvoice("0000042", 1);
            await _invoices.AddAsync(bad, CancellationToken.None);
            await _invoices.AddAsync(good, CancellationToken.None);
            var criteria = ArchiveCriteria.From(new ArchiveQuery { Direction = InvoiceDirection.Sold });

            await _invoices.SaveIntegrityAsync(
                new Dictionary<Guid, IntegrityProblemKind?> { { bad.InvoiceId, IntegrityProblemKind.HashMismatch }, { good.InvoiceId, null } }, DateTime.UtcNow, CancellationToken.None);
            Reopen();
            var refs = await _invoices.ListFileRefsAsync(criteria, CancellationToken.None);

            Assert.That(refs.Single(r => r.InvoiceId == bad.InvoiceId).StoredProblem, Is.EqualTo(IntegrityProblemKind.HashMismatch));
            Assert.That(refs.Single(r => r.InvoiceId == good.InvoiceId).StoredProblem, Is.Null);
            Assert.That(refs.Single(r => r.InvoiceId == good.InvoiceId).CheckedAtUtc, Is.Not.Null);

            var next = new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 2, Sha256 = "def456", RelativePath = @"2026\10\BanRa\XML\a_v2.xml", SizeBytes = 10, CreatedAtUtc = DateTime.UtcNow };
            await _invoices.AddVersionAsync(bad.InvoiceId, next, bad.Invoice, DateTime.UtcNow, CancellationToken.None);
            refs = await _invoices.ListFileRefsAsync(criteria, CancellationToken.None);

            Assert.That(refs.Single(r => r.InvoiceId == bad.InvoiceId).StoredProblem, Is.Null);
        }

        [Test]
        public async Task Invoice_AddThenFind_RoundTripsEveryField_AndSurvivesReopening()
        {
            var stored = NewInvoice("0000040", 1);
            await _invoices.AddAsync(stored, CancellationToken.None);
            Reopen();

            var byKey = await _invoices.FindAsync(stored.BusinessKey, CancellationToken.None);
            var byId = await _invoices.FindByIdAsync(stored.InvoiceId, CancellationToken.None);

            Assert.That(byId.InvoiceId, Is.EqualTo(byKey.InvoiceId));
            var i = byKey.Invoice;
            Assert.That(i.Direction, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(i.SellerName, Is.EqualTo("CÔNG TY TNHH ĐÔNG Á"));
            Assert.That(i.BuyerName, Is.EqualTo("Nguyễn Văn Mai"));
            Assert.That(i.BuyerTaxCode, Is.Null);
            Assert.That(i.BuyerIdNumber, Is.EqualTo("001092008840"));
            Assert.That(i.InvoiceNumber, Is.EqualTo("0000040"), "số hóa đơn giữ dạng text, kể cả số 0 đầu");
            Assert.That(i.IssueDate, Is.EqualTo(new DateTime(2026, 10, 31)));
            Assert.That(i.TotalBeforeTax, Is.EqualTo(12345678.9012m));
            Assert.That(i.TotalTax, Is.EqualTo(0.5m));
            Assert.That(i.TotalDiscount, Is.Null);
            Assert.That(i.ExchangeRate, Is.EqualTo(24987.123456m));
            Assert.That(i.Status, Is.EqualTo(InvoiceStatus.New));
            Assert.That(i.CheckResultRaw, Is.EqualTo("5"));
            Assert.That(byKey.FirstSeenAtUtc, Is.EqualTo(stored.FirstSeenAtUtc));
            Assert.That(byKey.Versions.Single().Sha256, Is.EqualTo("abc123"));
            Assert.That(byKey.Current.RelativePath, Is.EqualTo(@"2026\10\BanRa\XML\a.xml"));
            Assert.That(byKey.Current.IsCurrent, Is.True);
        }

        [Test]
        public async Task Invoice_UnknownKeyOrId_ReturnsNull()
        {
            Assert.That(await _invoices.FindAsync("khong-co", CancellationToken.None), Is.Null);
            Assert.That(await _invoices.FindByIdAsync(Guid.NewGuid(), CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task Invoice_DuplicateBusinessKey_IsRejectedLikeTheInMemoryRepository()
        {
            await _invoices.AddAsync(NewInvoice("1", 1), CancellationToken.None);

            Assert.ThrowsAsync<InvalidOperationException>(() => _invoices.AddAsync(NewInvoice("1", 1), CancellationToken.None));
            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Invoice_OtherConstraintViolation_IsNotReportedAsDuplicate()
        {
            var broken = NewInvoice("2", 1);
            broken.BusinessKey = null;

            var ex = Assert.CatchAsync(() => _invoices.AddAsync(broken, CancellationToken.None));

            Assert.That(ex, Is.Not.InstanceOf<InvalidOperationException>());
            Assert.That(ex, Is.InstanceOf<System.Data.SQLite.SQLiteException>());
        }

        [Test]
        public void Time_UnspecifiedKind_IsTakenAsUtc_NotAsLocalTime()
        {
            var unspecified = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Unspecified);

            Assert.That(SqliteValues.Time(unspecified), Is.EqualTo("2026-11-03T10:00:00Z"));
        }

        [Test]
        public async Task Invoice_AddVersion_KeepsOldVersions_AndMakesTheNewOneCurrent()
        {
            var stored = NewInvoice("7", 1);
            await _invoices.AddAsync(stored, CancellationToken.None);
            var changed = NewInvoice("7", 1).Invoice;
            changed.Status = InvoiceStatus.Cancelled;
            changed.StatusRaw = "6";
            var now = new DateTime(2026, 11, 3, 1, 2, 3, DateTimeKind.Utc);

            await _invoices.AddVersionAsync(
                stored.InvoiceId, new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 2, Sha256 = "def456", RelativePath = "v2.xml", SizeBytes = 99, CreatedAtUtc = now, SourceSessionId = Guid.NewGuid() },
                changed, now, CancellationToken.None);
            Reopen();

            var found = await _invoices.FindByIdAsync(stored.InvoiceId, CancellationToken.None);
            Assert.That(found.Versions.Select(v => v.VersionNo), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(found.Versions.Count(v => v.IsCurrent), Is.EqualTo(1));
            Assert.That(found.Current.VersionNo, Is.EqualTo(2));
            Assert.That(found.Invoice.Status, Is.EqualTo(InvoiceStatus.Cancelled));
            Assert.That(found.LastSeenAtUtc, Is.EqualTo(now));
            Assert.That(found.FirstSeenAtUtc, Is.EqualTo(stored.FirstSeenAtUtc), "thấy lần đầu không đổi");
        }

        [Test]
        public async Task Invoice_Touch_RefreshesMetadataAndRestoresTheCurrentFilePath()
        {
            var stored = NewInvoice("9", 1);
            await _invoices.AddAsync(stored, CancellationToken.None);
            var latest = NewInvoice("9", 1).Invoice;
            latest.CheckResultRaw = "6";
            latest.CheckResult = InvoiceCheckResult.ReceivedWithoutCode;
            var now = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

            await _invoices.TouchAsync(stored.InvoiceId, now, latest, @"2026\10\BanRa\XML\a_2.xml", CancellationToken.None);

            var found = await _invoices.FindByIdAsync(stored.InvoiceId, CancellationToken.None);
            Assert.That(found.Invoice.CheckResultRaw, Is.EqualTo("6"));
            Assert.That(found.LastSeenAtUtc, Is.EqualTo(now));
            Assert.That(found.Current.RelativePath, Is.EqualTo(@"2026\10\BanRa\XML\a_2.xml"));
            Assert.That(found.Versions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Invoice_TouchWithoutMetadata_OnlyMovesLastSeen()
        {
            var stored = NewInvoice("9", 1);
            await _invoices.AddAsync(stored, CancellationToken.None);
            var now = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

            await _invoices.TouchAsync(stored.InvoiceId, now, null, null, CancellationToken.None);

            var found = await _invoices.FindByIdAsync(stored.InvoiceId, CancellationToken.None);
            Assert.That(found.LastSeenAtUtc, Is.EqualTo(now));
            Assert.That(found.Invoice.SellerName, Is.EqualTo("CÔNG TY TNHH ĐÔNG Á"));
            Assert.That(found.Current.RelativePath, Is.EqualTo(@"2026\10\BanRa\XML\a.xml"));
        }

        [Test]
        public async Task Invoice_WritesOnUnknownInvoice_AreRejected_AndNothingPartialRemains()
        {
            var ghost = Guid.NewGuid();

            Assert.ThrowsAsync<InvalidOperationException>(() => _invoices.TouchAsync(ghost, DateTime.UtcNow, null, null, CancellationToken.None));
            Assert.ThrowsAsync<InvalidOperationException>(() => _invoices.AddVersionAsync(
                ghost, new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 2, Sha256 = "x", RelativePath = "x", CreatedAtUtc = DateTime.UtcNow, SourceSessionId = Guid.NewGuid() },
                NewInvoice("1", 1).Invoice, DateTime.UtcNow, CancellationToken.None));
            Assert.That(await _invoices.ListAsync(CancellationToken.None), Is.Empty);
        }

        [Test]
        public async Task Invoice_List_ReturnsEveryInvoiceWithItsVersions()
        {
            for (var n = 1; n <= 30; n++) await _invoices.AddAsync(NewInvoice(n.ToString(), 1 + n % 2), CancellationToken.None);

            var all = await _invoices.ListAsync(CancellationToken.None);

            Assert.That(all.Count, Is.EqualTo(30));
            Assert.That(all.All(s => s.Versions.Count == 1 && s.Current != null), Is.True);
        }

        [Test]
        public async Task Invoice_ConcurrentWrites_AreSerialisedWithoutLockErrors()
        {
            var tasks = Enumerable.Range(1, 40).Select(n => _invoices.AddAsync(NewInvoice(n.ToString(), 1), CancellationToken.None)).ToArray();

            await Task.WhenAll(tasks);

            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(40));
        }

        // ---- Phiên tải ----

        [Test]
        public async Task Session_RoundTripsPeriodFilterAndSegments_AfterReopening()
        {
            var session = NewSession(PeriodSpec.Custom(new DateTime(2026, 3, 5), new DateTime(2026, 4, 20)), new InvoiceSearchFilter { CounterpartyTaxCode = "0100100001", ProxyInvoice = true, CheckResultCode = "8" });
            var segments = new List<DownloadSegment>
            {
                new DownloadSegment(Guid.NewGuid(), session.Id, 0, new DateSegment(new DateTime(2026, 3, 5), new DateTime(2026, 3, 31))) { CheckResultCode = "5" },
                new DownloadSegment(Guid.NewGuid(), session.Id, 1, new DateSegment(new DateTime(2026, 4, 1), new DateTime(2026, 4, 20))) { CheckResultCode = "6" }
            };
            await _downloads.CreateAsync(session, segments, CancellationToken.None);
            Reopen();

            var loaded = await _downloads.GetSessionAsync(session.Id, CancellationToken.None);
            var loadedSegments = await _downloads.GetSegmentsAsync(session.Id, CancellationToken.None);

            Assert.That(loaded.TaxCode.Value, Is.EqualTo(Mst));
            Assert.That(loaded.CompanyId, Is.EqualTo(_context.Current.CompanyId));
            Assert.That(loaded.Direction, Is.EqualTo(InvoiceDirection.Purchase));
            Assert.That(loaded.Period.Kind, Is.EqualTo(PeriodKind.Custom));
            Assert.That(loaded.Period.From, Is.EqualTo(new DateTime(2026, 3, 5)));
            Assert.That(loaded.Period.To, Is.EqualTo(new DateTime(2026, 4, 20)));
            Assert.That(loaded.Filter.CounterpartyTaxCode, Is.EqualTo("0100100001"));
            Assert.That(loaded.Filter.ProxyInvoice, Is.True);
            Assert.That(loaded.Filter.CheckResultCode, Is.EqualTo("8"));
            Assert.That(loaded.State, Is.EqualTo(DownloadSessionState.Draft));
            Assert.That(loaded.StorageRoot, Is.EqualTo(_root));
            Assert.That(loadedSegments.Select(s => s.Index), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(loadedSegments.Select(s => s.CheckResultCode), Is.EqualTo(new[] { "5", "6" }));
            Assert.That(loadedSegments[1].Range, Is.EqualTo(new DateSegment(new DateTime(2026, 4, 1), new DateTime(2026, 4, 20))));
        }

        [TestCase(PeriodKind.Month)]
        [TestCase(PeriodKind.Quarter)]
        [TestCase(PeriodKind.Year)]
        public async Task Session_EveryPeriodKind_RoundTrips(PeriodKind kind)
        {
            var period = kind == PeriodKind.Month ? PeriodSpec.ForMonth(2026, 10) : kind == PeriodKind.Quarter ? PeriodSpec.ForQuarter(2026, 4) : PeriodSpec.ForYear(2026);
            var session = NewSession(period, null);
            await _downloads.CreateAsync(session, new List<DownloadSegment>(), CancellationToken.None);

            var loaded = await _downloads.GetSessionAsync(session.Id, CancellationToken.None);

            Assert.That(loaded.Period.Kind, Is.EqualTo(kind));
            Assert.That(loaded.Period.Year, Is.EqualTo(2026));
            Assert.That(loaded.Period.Month, Is.EqualTo(period.Month));
            Assert.That(loaded.Period.Quarter, Is.EqualTo(period.Quarter));
        }

        [Test]
        public async Task Session_Checkpoint_SurvivesReopening_WithoutLosingSegmentsOrEvents()
        {
            var session = NewSession(PeriodSpec.ForMonth(2026, 10), null);
            var segment = new DownloadSegment(Guid.NewGuid(), session.Id, 0, new DateSegment(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));
            await _downloads.CreateAsync(session, new[] { segment }, CancellationToken.None);
            await _downloads.AddEventAsync(new DownloadEvent(session.Id, segment.Id, DateTime.UtcNow, DownloadEventLevel.Warning, "X", "thông điệp tiếng Việt"), CancellationToken.None);

            var t = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);
            session.TransitionTo(DownloadSessionState.Running, t);
            await _downloads.SaveSessionAsync(session, CancellationToken.None);
            segment.TransitionTo(DownloadSegmentState.Running);
            segment.Cursor = "MTAw";
            segment.ItemOffset = 7;
            segment.PageIndex = 2;
            segment.ReceivedCount = 107;
            segment.XmlSavedCount = 100;
            segment.ErrorCount = 1;
            segment.RetryCount = 2;
            segment.LastError = "lỗi tạm";
            await _downloads.SaveSegmentAsync(segment, CancellationToken.None);
            session.TransitionTo(DownloadSessionState.PausedByUser, t.AddMinutes(1));
            await _downloads.SaveSessionAsync(session, CancellationToken.None);
            Reopen();

            var s = await _downloads.GetSessionAsync(session.Id, CancellationToken.None);
            var g = (await _downloads.GetSegmentsAsync(session.Id, CancellationToken.None)).Single();
            var events = await _downloads.GetEventsAsync(session.Id, CancellationToken.None);

            Assert.That(s.State, Is.EqualTo(DownloadSessionState.PausedByUser));
            Assert.That(s.StartedAtUtc, Is.EqualTo(t));
            Assert.That(s.FinishedAtUtc, Is.Null);
            Assert.That(g.State, Is.EqualTo(DownloadSegmentState.Running));
            Assert.That(new[] { g.Cursor, g.LastError }, Is.EqualTo(new[] { "MTAw", "lỗi tạm" }));
            Assert.That(new[] { g.ItemOffset, g.PageIndex, g.ReceivedCount, g.XmlSavedCount, g.ErrorCount, g.RetryCount }, Is.EqualTo(new[] { 7, 2, 107, 100, 1, 2 }));
            Assert.That(events.Single().Message, Is.EqualTo("thông điệp tiếng Việt"));
            Assert.That(events.Single().SegmentId, Is.EqualTo(segment.Id));
        }

        [Test]
        public async Task Segment_SaveOfAnUnknownSegment_IsRejected()
        {
            var ghost = new DownloadSegment(Guid.NewGuid(), Guid.NewGuid(), 0, new DateSegment(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));

            Assert.ThrowsAsync<InvalidOperationException>(() => _downloads.SaveSegmentAsync(ghost, CancellationToken.None));
            await Task.FromResult(0);
        }

        [Test]
        public async Task Unfinished_ListsDraftRunningPausedAndWaiting_OldestFirst_AndNothingElse()
        {
            var created = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            var sessions = new Dictionary<DownloadSessionState, DownloadSession>();
            var order = new[]
            {
                DownloadSessionState.Completed, DownloadSessionState.Draft, DownloadSessionState.Running, DownloadSessionState.CompletedWithErrors,
                DownloadSessionState.PausedByUser, DownloadSessionState.WaitingForLogin, DownloadSessionState.Cancelled, DownloadSessionState.Failed
            };
            var minute = 0;
            foreach (var state in order)
            {
                var s = DownloadSession.Restore(Guid.NewGuid(), _context.Current.CompanyId, TaxCode.Parse(Mst), _root, InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 10),
                    new InvoiceSearchFilter(), state, created.AddMinutes(minute++), null, null, "t");
                sessions[state] = s;
                await _downloads.CreateAsync(s, new List<DownloadSegment>(), CancellationToken.None);
            }

            var unfinished = await _downloads.GetUnfinishedAsync(_context.Current.CompanyId, CancellationToken.None);
            var all = await _downloads.GetSessionsAsync(_context.Current.CompanyId, CancellationToken.None);

            Assert.That(unfinished.Select(s => s.State), Is.EqualTo(new[] { DownloadSessionState.Draft, DownloadSessionState.Running, DownloadSessionState.PausedByUser, DownloadSessionState.WaitingForLogin }));
            Assert.That(all.Select(s => s.State), Is.EqualTo(order.Reverse()), "lịch sử: mới nhất trước");
            Assert.That(await _downloads.GetUnfinishedAsync(CompanyId.NewId(), CancellationToken.None), Is.Empty, "không lẫn doanh nghiệp khác");
        }

        [Test]
        public async Task SegmentsByCompany_GroupsPerSession_InIndexOrder_AndIncludesEmptySessions()
        {
            var first = NewSession(PeriodSpec.ForQuarter(2026, 4), null);
            var second = NewSession(PeriodSpec.ForMonth(2026, 10), null);
            var empty = NewSession(PeriodSpec.ForMonth(2026, 9), null);
            await _downloads.CreateAsync(first, Months(first.Id, 10, 11, 12), CancellationToken.None);
            await _downloads.CreateAsync(second, Months(second.Id, 10), CancellationToken.None);
            await _downloads.CreateAsync(empty, new List<DownloadSegment>(), CancellationToken.None);

            var grouped = await _downloads.GetSegmentsByCompanyAsync(_context.Current.CompanyId, CancellationToken.None);

            Assert.That(grouped.Keys, Is.EquivalentTo(new[] { first.Id, second.Id, empty.Id }));
            Assert.That(grouped[first.Id].Select(g => g.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(grouped[first.Id].Select(g => g.Range.Month), Is.EqualTo(new[] { 10, 11, 12 }));
            Assert.That(grouped[second.Id], Has.Count.EqualTo(1));
            Assert.That(grouped[empty.Id], Is.Empty);
        }

        [Test]
        public async Task SavingTheSession_DoesNotDeleteItsSegmentsOrEvents()
        {
            var session = NewSession(PeriodSpec.ForMonth(2026, 10), null);
            await _downloads.CreateAsync(session, Months(session.Id, 10), CancellationToken.None);
            await _downloads.AddEventAsync(new DownloadEvent(session.Id, null, DateTime.UtcNow, DownloadEventLevel.Info, "A", "b"), CancellationToken.None);

            session.TransitionTo(DownloadSessionState.Running, DateTime.UtcNow);
            await _downloads.SaveSessionAsync(session, CancellationToken.None);

            Assert.That(await _downloads.GetSegmentsAsync(session.Id, CancellationToken.None), Has.Count.EqualTo(1));
            Assert.That(await _downloads.GetEventsAsync(session.Id, CancellationToken.None), Has.Count.EqualTo(1));
        }

        // ---- Luồng tải thật trên cổng giả ----

        [Test]
        public async Task Download_OnMockPortal_PersistsInvoicesSessionsAndSegments_ThatSurviveReopening()
        {
            using (var portal = new TestPortal(Mst))
            {
                var sessionStore = new InMemoryPortalSessionStore();
                sessionStore.Set(new PortalSession(_context.Current.CompanyId, TaxCode.Parse(Mst), new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
                var storage = new InvoiceStorageService(_invoices, new FileSystemInvoiceFileStore(), new InvoiceNormalizer(), _context, new SystemClock(), new Quiet());
                var orchestrator = new DownloadOrchestrator(
                    _downloads, _context, sessionStore, () => portal.Client, storage, new NoLimit(), DefaultRetryPolicy.Standard,
                    new EntitlementService(AppMode.Standard, false), new SystemClock(), new Quiet(), (span, token) => Task.FromResult(0));
                var plan = DownloadPlan.Create(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1), new InvoiceSearchFilter(), new PeriodSplitter());
                var id = await orchestrator.CreateSessionAsync(plan, CancellationToken.None);
                var expected = portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)).Count;

                await orchestrator.RunAsync(id, null, CancellationToken.None);

                Reopen();
                var session = await _downloads.GetSessionAsync(id, CancellationToken.None);
                var segment = (await _downloads.GetSegmentsAsync(id, CancellationToken.None)).Single();
                var stored = await _invoices.ListAsync(CancellationToken.None);
                Assert.That(expected, Is.GreaterThan(0));
                Assert.That(session.State, Is.EqualTo(DownloadSessionState.Completed));
                Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Completed));
                Assert.That(segment.XmlSavedCount, Is.EqualTo(expected));
                Assert.That(stored.Count, Is.EqualTo(expected));
                Assert.That(stored.All(s => s.Current != null && File.Exists(Path.Combine(_root, s.Current.RelativePath))), Is.True, "metadata trỏ đúng file XML đã ghi");

                // Tải lại cùng kỳ sau khi mở lại: không tạo trùng hóa đơn hay phiên bản mới.
                var again = await orchestrator.CreateSessionAsync(plan, CancellationToken.None);
                await orchestrator.RunAsync(again, null, CancellationToken.None);
                var afterAgain = await _invoices.ListAsync(CancellationToken.None);
                Assert.That(afterAgain.Count, Is.EqualTo(expected));
                Assert.That(afterAgain.All(s => s.Versions.Count == 1), Is.True);
                Assert.That((await _downloads.GetSessionsAsync(_context.Current.CompanyId, CancellationToken.None)).Count, Is.EqualTo(2));
            }
        }

        // ---- Hỗ trợ ----

        private DownloadSession NewSession(PeriodSpec period, InvoiceSearchFilter filter)
        {
            return new DownloadSession(Guid.NewGuid(), _context.Current.CompanyId, TaxCode.Parse(Mst), _root, InvoiceDirection.Purchase, period, filter,
                new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc), "tester");
        }

        private static List<DownloadSegment> Months(Guid sessionId, params int[] months)
        {
            return months.Select((m, i) => new DownloadSegment(Guid.NewGuid(), sessionId, i, new DateSegment(new DateTime(2026, m, 1), new DateTime(2026, m, DateTime.DaysInMonth(2026, m))))).ToList();
        }

        private static StoredInvoice NewInvoice(string number, int direction)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = (InvoiceDirection)(direction % 2),
                PortalInvoiceId = Guid.NewGuid().ToString(),
                FormSymbol = "1",
                InvoiceSymbol = "C26TBN",
                InvoiceNumber = number,
                IssueDate = new DateTime(2026, 10, 31),
                SellerTaxCode = Mst,
                SellerName = "CÔNG TY TNHH ĐÔNG Á",
                BuyerTaxCode = null,
                BuyerIdNumber = "001092008840",
                BuyerName = "Nguyễn Văn Mai",
                BuyerAddress = "Thái Phù, Sóc Sơn, Hà Nội",
                TotalBeforeTax = 12345678.9012m,
                TotalTax = 0.5m,
                TotalPayment = 12345679.4012m,
                Currency = "VND",
                ExchangeRate = 24987.123456m,
                Status = InvoiceStatus.New,
                StatusRaw = "1",
                CheckResult = InvoiceCheckResult.CodeIssued,
                CheckResultRaw = "5"
            };
            var versionId = Guid.NewGuid();
            return new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(),
                BusinessKey = BusinessKey.Build(invoice),
                Invoice = invoice,
                FirstSeenAtUtc = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc),
                LastSeenAtUtc = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc),
                Versions = new List<InvoiceVersionRecord>
                {
                    new InvoiceVersionRecord
                    {
                        VersionId = versionId, VersionNo = 1, Sha256 = "abc123", RelativePath = @"2026\10\BanRa\XML\a.xml", SizeBytes = 1234,
                        CreatedAtUtc = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc), IsCurrent = true, SourceSessionId = Guid.NewGuid()
                    }
                }
            };
        }

        private sealed class FixedContext : ICompanyContextAccessor
        {
            private readonly CompanyContext _context;

            public FixedContext(string root, CompanyId id)
            {
                _context = new CompanyContext(id, TaxCode.Parse(Mst), "Công ty mẫu", root, null);
            }

            public bool HasCurrent { get { return true; } }
            public CompanyContext Current { get { return _context; } }
        }

        private sealed class NoCompany : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return false; } }
            public CompanyContext Current { get { return null; } }
        }

        private sealed class NoLimit : IRateLimiter
        {
            public Task WaitAsync(CancellationToken cancellationToken) { return Task.FromResult(0); }
        }

        private sealed class Quiet : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
#endif
