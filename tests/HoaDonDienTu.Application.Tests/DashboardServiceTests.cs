using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Dashboard;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class DashboardServiceTests
    {
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private InMemoryInvoiceRepository _invoices;
        private InMemoryDownloadRepository _downloads;
        private FakeFiles _files;
        private FakeContext _context;
        private CompanyId _companyId;
        private DashboardService _service;

        [SetUp]
        public void SetUp()
        {
            _invoices = new InMemoryInvoiceRepository();
            _downloads = new InMemoryDownloadRepository();
            _files = new FakeFiles();
            _companyId = CompanyId.NewId();
            _context = new FakeContext { Current = new CompanyContext(_companyId, Mst, "Công ty mẫu", @"C:\Kho", null) };
            var archive = new InvoiceArchiveService(_invoices, _files, _context);
            var history = new DownloadHistoryService(_downloads, _context);
            _service = new DashboardService(_invoices, _files, archive, history, _context, new FixedClock(new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc)));
        }

        private async Task<StoredInvoice> AddInvoiceAsync(
            InvoiceDirection direction, string number, DateTime issue, decimal payment = 110m, bool fileExists = true, int versions = 1, DateTime? firstSeen = null)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = direction, InvoiceNumber = number, IssueDate = issue, FormSymbol = "1", InvoiceSymbol = "C26TAA",
                SellerTaxCode = direction == InvoiceDirection.Purchase ? "0100100001" : "0101234567",
                SellerName = direction == InvoiceDirection.Purchase ? "Công ty ABC" : "Công ty mẫu",
                BuyerTaxCode = direction == InvoiceDirection.Sold ? "0100100002" : "0101234567",
                BuyerName = direction == InvoiceDirection.Sold ? "Khách DEF" : "Công ty mẫu",
                StatusRaw = "1", CheckResultRaw = "5", TotalBeforeTax = payment - 10m, TotalTax = 10m, TotalPayment = payment
            };
            var list = new List<InvoiceVersionRecord>();
            for (var v = 1; v <= versions; v++)
            {
                var path = @"X\" + direction + number + "_" + v + ".xml";
                list.Add(new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = v, RelativePath = path, IsCurrent = v == versions });
                if (fileExists) _files.Existing.Add(path);
            }

            var stored = new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = list,
                FirstSeenAtUtc = firstSeen ?? DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
            };
            await _invoices.AddAsync(stored, CancellationToken.None);
            return stored;
        }

        private async Task<DownloadSession> AddSessionAsync(
            DownloadSessionState state, DateTime createdUtc, DateTime? finishedUtc = null, int received = 10, int saved = 10, int errors = 0,
            InvoiceDirection direction = InvoiceDirection.Purchase, int segments = 4, int completed = 2)
        {
            var session = new DownloadSession(
                Guid.NewGuid(), _companyId, Mst, @"C:\Kho", direction, PeriodSpec.ForYear(2026), new InvoiceSearchFilter(), createdUtc, "CUONGTM");
            if (state != DownloadSessionState.Draft)
            {
                session.TransitionTo(DownloadSessionState.Running, createdUtc);
                if (state != DownloadSessionState.Running) session.TransitionTo(state, finishedUtc ?? createdUtc);
            }

            var list = new List<DownloadSegment>();
            for (var i = 0; i < segments; i++)
            {
                var segment = new DownloadSegment(Guid.NewGuid(), session.Id, i, new DateSegment(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)))
                {
                    ReceivedCount = received / segments, XmlSavedCount = saved / segments, ErrorCount = errors / segments
                };
                if (i < completed)
                {
                    segment.TransitionTo(DownloadSegmentState.Running);
                    segment.TransitionTo(DownloadSegmentState.Completed);
                }

                list.Add(segment);
            }

            await _downloads.CreateAsync(session, list, CancellationToken.None);
            return session;
        }

        [Test]
        public async Task EmptyCompany_GivesZeros_NotAnError()
        {
            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.TotalInvoices, Is.EqualTo(0));
            Assert.That(s.YearPaymentSold, Is.EqualTo(0m));
            Assert.That(s.YearPaymentPurchase, Is.EqualTo(0m));
            Assert.That(s.LastSyncAtUtc, Is.Null);
            Assert.That(s.Active, Is.Empty);
            Assert.That(s.Attention, Is.Empty);
            Assert.That(s.Recent, Is.Empty);
            Assert.That(s.AttentionCount, Is.EqualTo(0));
        }

        [Test]
        public void NoCompany_IsRejected()
        {
            _context.Current = null;

            Assert.ThrowsAsync<StorageGuardException>(() => _service.GetAsync(CancellationToken.None));
        }

        [Test]
        public async Task Totals_SplitByDirection_AndYearPaymentCountsOnlyTheCurrentYear()
        {
            await AddInvoiceAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 3, 1), 100m);
            await AddInvoiceAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 4, 1), 200m);
            await AddInvoiceAsync(InvoiceDirection.Sold, "3", new DateTime(2026, 5, 1), 400m);
            await AddInvoiceAsync(InvoiceDirection.Sold, "4", new DateTime(2025, 12, 31), 9000m);

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.TotalInvoices, Is.EqualTo(4));
            Assert.That(s.PurchaseCount, Is.EqualTo(2));
            Assert.That(s.SoldCount, Is.EqualTo(2));
            Assert.That(s.Year, Is.EqualTo(2026));
            Assert.That(s.YearPaymentPurchase, Is.EqualTo(300m), "mua vào tách riêng, chỉ năm hiện tại");
            Assert.That(s.YearPaymentSold, Is.EqualTo(400m), "bán ra tách riêng, chỉ năm hiện tại");
        }

        [Test]
        public async Task YearPayment_LeavesOutForeignCurrencyInvoices()
        {
            await AddInvoiceAsync(InvoiceDirection.Sold, "1", new DateTime(2026, 5, 1), 400m);
            var usd = await AddInvoiceAsync(InvoiceDirection.Sold, "2", new DateTime(2026, 5, 2), 999m);
            usd.Invoice.Currency = "USD";
            await _invoices.TouchAsync(usd.InvoiceId, DateTime.UtcNow, usd.Invoice, null, CancellationToken.None);

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.YearPaymentSold, Is.EqualTo(400m));
        }

        [Test]
        public async Task Recent_AreNewestDownloadedFirst_LimitedToFive_WithFileState()
        {
            var t0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 1; i <= 7; i++) await AddInvoiceAsync(InvoiceDirection.Purchase, i.ToString(), new DateTime(2026, 1, i), firstSeen: t0.AddHours(i));
            await AddInvoiceAsync(InvoiceDirection.Sold, "8", new DateTime(2026, 1, 8), fileExists: false, firstSeen: t0.AddHours(8));
            await AddInvoiceAsync(InvoiceDirection.Sold, "9", new DateTime(2026, 1, 9), versions: 2, firstSeen: t0.AddHours(9));

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.Recent.Select(r => r.InvoiceNumber), Is.EqualTo(new[] { "9", "8", "7", "6", "5" }));
            Assert.That(s.Recent[0].FileState, Is.EqualTo(ArchiveFileState.MultiVersion));
            Assert.That(s.Recent[1].FileState, Is.EqualTo(ArchiveFileState.Missing));
            Assert.That(s.Recent[1].CounterpartyName, Is.EqualTo("Khách DEF"));
            Assert.That(s.Recent[2].FileState, Is.EqualTo(ArchiveFileState.Ok));
        }

        [Test]
        public async Task Recent_ShowsDamagedWhenTheLastIntegrityCheckFoundBadContent_ButMissingStillWinsOverTheStoredResult()
        {
            var t0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var damaged = await AddInvoiceAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), firstSeen: t0.AddHours(1));
            var gone = await AddInvoiceAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), fileExists: false, firstSeen: t0.AddHours(2));
            await _invoices.SaveIntegrityAsync(
                new Dictionary<Guid, IntegrityProblemKind?> { { damaged.InvoiceId, IntegrityProblemKind.HashMismatch }, { gone.InvoiceId, IntegrityProblemKind.HashMismatch } },
                t0.AddDays(1), CancellationToken.None);

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.Recent[0].InvoiceNumber, Is.EqualTo("2"));
            Assert.That(s.Recent[0].FileState, Is.EqualTo(ArchiveFileState.Missing));
            Assert.That(s.Recent[1].FileState, Is.EqualTo(ArchiveFileState.Damaged));
        }

        [Test]
        public async Task MissingFiles_AreCountedAcrossDirections_AndOpenTheDirectionThatHasThem()
        {
            await AddInvoiceAsync(InvoiceDirection.Sold, "1", new DateTime(2026, 1, 1), fileExists: false);
            await AddInvoiceAsync(InvoiceDirection.Sold, "2", new DateTime(2026, 1, 2), fileExists: false);
            await AddInvoiceAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 3));

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.MissingFileCount, Is.EqualTo(2));
            var item = s.Attention.Single();
            Assert.That(item.Kind, Is.EqualTo(AttentionKind.MissingFiles));
            Assert.That(item.Count, Is.EqualTo(2));
            Assert.That(item.Direction, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(s.AttentionCount, Is.EqualTo(2));
        }

        [Test]
        public async Task MissingFiles_WhenBothDirectionsHaveThem_GetOneItemEachWithTheirOwnCount()
        {
            await AddInvoiceAsync(InvoiceDirection.Sold, "1", new DateTime(2026, 1, 1), fileExists: false);
            await AddInvoiceAsync(InvoiceDirection.Sold, "3", new DateTime(2026, 1, 3), fileExists: false);
            await AddInvoiceAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), fileExists: false);

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.MissingFileCount, Is.EqualTo(3));
            Assert.That(s.AttentionCount, Is.EqualTo(3));
            Assert.That(s.Attention.Select(a => a.Direction), Is.EqualTo(new[] { InvoiceDirection.Purchase, InvoiceDirection.Sold }));
            Assert.That(s.Attention.Select(a => a.Count), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public async Task ActiveSessions_IncludeRunningPausedAndWaiting_ButNotDraftOrFinished()
        {
            var t = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
            await AddSessionAsync(DownloadSessionState.Draft, t);
            var running = await AddSessionAsync(DownloadSessionState.Running, t.AddHours(1), received: 40, saved: 36, errors: 4, segments: 4, completed: 3);
            await AddSessionAsync(DownloadSessionState.PausedByUser, t.AddHours(2));
            await AddSessionAsync(DownloadSessionState.WaitingForLogin, t.AddHours(3));
            await AddSessionAsync(DownloadSessionState.Completed, t.AddHours(4), t.AddHours(5));

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.Active.Count, Is.EqualTo(3));
            var a = s.Active.Single(x => x.SessionId == running.Id);
            Assert.That(a.SegmentCount, Is.EqualTo(4));
            Assert.That(a.CompletedSegments, Is.EqualTo(3));
            Assert.That(a.InvoiceCount, Is.EqualTo(36), "Mới + đã có");
            Assert.That(a.ErrorCount, Is.EqualTo(4));
        }

        [Test]
        public async Task ErrorSessions_AreListedUpToThree_ButAllAreCounted()
        {
            var t = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 5; i++) await AddSessionAsync(DownloadSessionState.CompletedWithErrors, t.AddHours(i), t.AddHours(i).AddMinutes(5), errors: 4);
            await AddSessionAsync(DownloadSessionState.Failed, t.AddHours(9), t.AddHours(9).AddMinutes(1));
            await AddInvoiceAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), fileExists: false);

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.ErrorSessionCount, Is.EqualTo(6));
            Assert.That(s.Attention.Count(a => a.Kind == AttentionKind.SessionNeedsAttention), Is.EqualTo(3));
            Assert.That(s.Attention[0].Kind, Is.EqualTo(AttentionKind.MissingFiles), "File thiếu đứng trước");
            Assert.That(s.AttentionCount, Is.EqualTo(7), "1 file thiếu + 6 phiên lỗi");
        }

        [Test]
        public async Task LastSync_IsTheLatestFinishedCompletedSession_IgnoringCancelledAndRunning()
        {
            var t = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            await AddSessionAsync(DownloadSessionState.Completed, t, t.AddHours(1), received: 20, saved: 18);
            var latest = await AddSessionAsync(DownloadSessionState.CompletedWithErrors, t.AddDays(10), t.AddDays(10).AddHours(1), received: 128, saved: 124, errors: 4);
            await AddSessionAsync(DownloadSessionState.Cancelled, t.AddDays(20), t.AddDays(20).AddMinutes(1));
            await AddSessionAsync(DownloadSessionState.Running, t.AddDays(25));

            var s = await _service.GetAsync(CancellationToken.None);

            Assert.That(s.LastSyncAtUtc, Is.EqualTo(t.AddDays(10).AddHours(1)));
            Assert.That(s.LastSyncState, Is.EqualTo(DownloadSessionState.CompletedWithErrors));
            Assert.That(s.LastSyncInvoices, Is.EqualTo(124));
            Assert.That(latest, Is.Not.Null);
        }

        private sealed class FixedClock : IClock
        {
            private readonly DateTime _now;

            public FixedClock(DateTime now)
            {
                _now = now;
            }

            public DateTime UtcNow { get { return _now; } }
        }

        private sealed class FakeFiles : IInvoiceFileStore
        {
            public HashSet<string> Existing = new HashSet<string>();

            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName) { throw new NotSupportedException(); }
            public void DiscardTemp(string tempPath) { }
            public bool Exists(string storageRoot, string relativePath) { return Existing.Contains(relativePath); }
            public void Delete(string storageRoot, string relativePath) { }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }
    }
}
