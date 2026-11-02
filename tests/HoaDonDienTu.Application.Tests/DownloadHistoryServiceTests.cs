using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class DownloadHistoryServiceTests
    {
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private InMemoryDownloadRepository _repo;
        private FakeContext _context;
        private CompanyId _companyId;
        private DownloadHistoryService _service;

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryDownloadRepository();
            _companyId = CompanyId.NewId();
            _context = new FakeContext { Current = new CompanyContext(_companyId, Mst, "Công ty mẫu", @"C:\Kho", null) };
            _service = new DownloadHistoryService(_repo, _context);
        }

        private async Task<DownloadSession> AddAsync(
            DateTime createdLocal, DownloadSessionState state = DownloadSessionState.Completed, InvoiceDirection direction = InvoiceDirection.Purchase,
            PeriodSpec period = null, int received = 10, int saved = 10, int errors = 0, string user = "CUONGTM", CompanyId? company = null)
        {
            var session = new DownloadSession(
                Guid.NewGuid(), company ?? _companyId, Mst, @"C:\Kho", direction, period ?? PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(),
                createdLocal.ToUniversalTime(), user);
            Move(session, state);
            var segment = new DownloadSegment(Guid.NewGuid(), session.Id, 0, new DateSegment(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)))
            {
                ReceivedCount = received, XmlSavedCount = saved, ErrorCount = errors
            };
            if (state == DownloadSessionState.Completed || state == DownloadSessionState.CompletedWithErrors)
            {
                segment.TransitionTo(DownloadSegmentState.Running);
                segment.TransitionTo(DownloadSegmentState.Completed);
            }

            await _repo.CreateAsync(session, new[] { segment }, CancellationToken.None);
            return session;
        }

        private static void Move(DownloadSession session, DownloadSessionState state)
        {
            var now = DateTime.UtcNow;
            if (state == DownloadSessionState.Draft) return;
            session.TransitionTo(DownloadSessionState.Running, now);
            if (state != DownloadSessionState.Running) session.TransitionTo(state, now);
        }

        private Task<HistoryPage> QueryAsync(Action<HistoryQuery> set = null)
        {
            var query = new HistoryQuery();
            if (set != null) set(query);
            return _service.QueryAsync(query, CancellationToken.None);
        }

        [Test]
        public async Task Rows_AreNewestFirst_AndOnlyOfTheCurrentCompany()
        {
            await AddAsync(new DateTime(2026, 10, 1, 9, 0, 0));
            await AddAsync(new DateTime(2026, 11, 1, 9, 15, 0));
            await AddAsync(new DateTime(2026, 11, 1, 10, 0, 0), company: CompanyId.NewId());

            var page = await QueryAsync();

            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Rows.Select(r => r.CreatedAtUtc.ToLocalTime().Day), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(page.Rows[0].CreatedAtUtc, Is.GreaterThan(page.Rows[1].CreatedAtUtc));
        }

        [Test]
        public async Task NoCompany_GivesAnEmptyPage()
        {
            await AddAsync(new DateTime(2026, 10, 1));
            _context.Current = null;

            var page = await QueryAsync();

            Assert.That(page.TotalCount, Is.EqualTo(0));
            Assert.That(page.Rows, Is.Empty);
            Assert.That(page.PageCount, Is.EqualTo(1));
            Assert.That(page.Summary.Total, Is.EqualTo(0));
        }

        [Test]
        public async Task Code_IsDayBasedWithASequenceThatDoesNotDependOnTheFilter()
        {
            var first = await AddAsync(new DateTime(2026, 11, 1, 8, 0, 0));
            var second = await AddAsync(new DateTime(2026, 11, 1, 9, 0, 0), direction: InvoiceDirection.Sold);
            var other = await AddAsync(new DateTime(2026, 10, 31, 16, 4, 0));

            var all = await QueryAsync();
            var soldOnly = await QueryAsync(q => q.Direction = InvoiceDirection.Sold);

            Assert.That(all.Rows.Single(r => r.SessionId == first.Id).Code, Is.EqualTo("DL-2026-1101-001"));
            Assert.That(all.Rows.Single(r => r.SessionId == second.Id).Code, Is.EqualTo("DL-2026-1101-002"));
            Assert.That(all.Rows.Single(r => r.SessionId == other.Id).Code, Is.EqualTo("DL-2026-1031-001"));
            Assert.That(soldOnly.Rows.Single().Code, Is.EqualTo("DL-2026-1101-002"));
        }

        [Test]
        public async Task Counts_SplitIntoNewExistingAndErrors()
        {
            await AddAsync(new DateTime(2026, 11, 1), received: 100, saved: 70, errors: 5);

            var row = (await QueryAsync()).Rows.Single();

            Assert.That(row.NewCount, Is.EqualTo(70));
            Assert.That(row.ExistingCount, Is.EqualTo(25));
            Assert.That(row.ErrorCount, Is.EqualTo(5));
            Assert.That(row.SegmentCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Summary_CountsWholeHistory_NotJustTheFilteredRows()
        {
            await AddAsync(new DateTime(2026, 11, 1));
            await AddAsync(new DateTime(2026, 11, 2), DownloadSessionState.Completed);
            await AddAsync(new DateTime(2026, 11, 3), DownloadSessionState.Running);
            await AddAsync(new DateTime(2026, 11, 4), DownloadSessionState.PausedByUser);
            await AddAsync(new DateTime(2026, 11, 5), DownloadSessionState.CompletedWithErrors, errors: 2);
            await AddAsync(new DateTime(2026, 11, 6), DownloadSessionState.Cancelled);

            var page = await QueryAsync(q => q.Status = HistoryStatusFilter.Completed);

            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Summary.Total, Is.EqualTo(6));
            Assert.That(page.Summary.Completed, Is.EqualTo(2));
            Assert.That(page.Summary.Active, Is.EqualTo(2));
            Assert.That(page.Summary.NeedsAttention, Is.EqualTo(1));
        }

        [TestCase(HistoryStatusFilter.InProgress, 3)]
        [TestCase(HistoryStatusFilter.CompletedWithErrors, 1)]
        [TestCase(HistoryStatusFilter.Stopped, 1)]
        [TestCase(HistoryStatusFilter.Completed, 1)]
        [TestCase(HistoryStatusFilter.All, 6)]
        public async Task StatusFilter_GroupsStates(HistoryStatusFilter filter, int expected)
        {
            await AddAsync(new DateTime(2026, 11, 1), DownloadSessionState.Completed);
            await AddAsync(new DateTime(2026, 11, 2), DownloadSessionState.CompletedWithErrors);
            await AddAsync(new DateTime(2026, 11, 3), DownloadSessionState.Draft);
            await AddAsync(new DateTime(2026, 11, 4), DownloadSessionState.Running);
            await AddAsync(new DateTime(2026, 11, 5), DownloadSessionState.WaitingForLogin);
            await AddAsync(new DateTime(2026, 11, 6), DownloadSessionState.Cancelled);

            var page = await QueryAsync(q => q.Status = filter);

            Assert.That(page.TotalCount, Is.EqualTo(expected));
        }

        [Test]
        public async Task DateRange_IsInclusiveOnBothEnds_ByLocalDay()
        {
            await AddAsync(new DateTime(2026, 10, 31, 23, 59, 0));
            await AddAsync(new DateTime(2026, 11, 1, 0, 1, 0));
            await AddAsync(new DateTime(2026, 11, 15, 12, 0, 0));
            await AddAsync(new DateTime(2026, 11, 30, 23, 0, 0));
            await AddAsync(new DateTime(2026, 12, 1, 0, 5, 0));

            var page = await QueryAsync(q =>
            {
                q.From = new DateTime(2026, 11, 1);
                q.To = new DateTime(2026, 11, 30);
            });

            Assert.That(page.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Search_MatchesCodePeriodAndUser_IgnoringCase()
        {
            await AddAsync(new DateTime(2026, 11, 1), period: PeriodSpec.ForQuarter(2026, 3), user: "Minh");
            await AddAsync(new DateTime(2026, 11, 2), period: PeriodSpec.ForYear(2025), user: "CUONGTM");

            Assert.That((await QueryAsync(q => q.Search = "q3/2026")).TotalCount, Is.EqualTo(1));
            Assert.That((await QueryAsync(q => q.Search = "MINH")).TotalCount, Is.EqualTo(1));
            Assert.That((await QueryAsync(q => q.Search = "dl-2026-1102")).TotalCount, Is.EqualTo(1));
            Assert.That((await QueryAsync(q => q.Search = "năm 2025")).TotalCount, Is.EqualTo(1));
            Assert.That((await QueryAsync(q => q.Search = "không có")).TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Paging_ClampsToTheLastPage()
        {
            for (var i = 0; i < 45; i++) await AddAsync(new DateTime(2026, 9, 1).AddDays(i % 28).AddHours(i));

            var page = await QueryAsync(q =>
            {
                q.PageIndex = 99;
                q.PageSize = 20;
            });

            Assert.That(page.PageCount, Is.EqualTo(3));
            Assert.That(page.PageIndex, Is.EqualTo(2));
            Assert.That(page.Rows.Count, Is.EqualTo(5));
            Assert.That(page.TotalCount, Is.EqualTo(45));
        }

        [Test]
        public void PeriodText_ReadsLikeThePortalWireframe()
        {
            Assert.That(DownloadHistoryService.PeriodText(PeriodSpec.ForMonth(2026, 10)), Is.EqualTo("Tháng 10/2026"));
            Assert.That(DownloadHistoryService.PeriodText(PeriodSpec.ForQuarter(2026, 3)), Is.EqualTo("Q3/2026"));
            Assert.That(DownloadHistoryService.PeriodText(PeriodSpec.ForYear(2026)), Is.EqualTo("Năm 2026"));
            Assert.That(DownloadHistoryService.PeriodText(PeriodSpec.Custom(new DateTime(2026, 1, 15), new DateTime(2026, 3, 2))), Is.EqualTo("15/01/2026 – 02/03/2026"));
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }
    }
}
