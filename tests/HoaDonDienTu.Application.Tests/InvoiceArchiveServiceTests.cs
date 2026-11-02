using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class InvoiceArchiveServiceTests
    {
        private const string Root = @"C:\Kho\0101234567";
        private static readonly CompanyId Id = CompanyId.NewId();

        private InMemoryInvoiceRepository _repo;
        private FakeFiles _files;
        private FakeContext _context;
        private InvoiceArchiveService _service;

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryInvoiceRepository();
            _files = new FakeFiles();
            _context = new FakeContext { Current = new CompanyContext(Id, TaxCode.Parse("0101234567"), "Công ty", Root, null) };
            _service = new InvoiceArchiveService(_repo, _files, _context);
        }

        private async Task AddAsync(InvoiceDirection direction, string number, DateTime date, string counterpartyName = "Công ty ABC",
            string counterpartyMst = "0100100001", string status = "1", string check = "5", decimal before = 100m, decimal tax = 10m,
            int versions = 1, bool fileExists = true, string symbol = "C26TAA", string form = "1")
        {
            var invoice = new NormalizedInvoice
            {
                Direction = direction, InvoiceNumber = number, IssueDate = date, FormSymbol = form, InvoiceSymbol = symbol,
                SellerTaxCode = direction == InvoiceDirection.Purchase ? counterpartyMst : "0101234567",
                SellerName = direction == InvoiceDirection.Purchase ? counterpartyName : "Công ty mẫu",
                BuyerTaxCode = direction == InvoiceDirection.Sold ? counterpartyMst : "0101234567",
                BuyerName = direction == InvoiceDirection.Sold ? counterpartyName : "Công ty mẫu",
                StatusRaw = status, CheckResultRaw = check, TotalBeforeTax = before, TotalTax = tax, TotalPayment = before + tax
            };

            var list = new List<InvoiceVersionRecord>();
            for (var v = 1; v <= versions; v++)
            {
                var path = @"2026\X\" + number + "_v" + v + ".xml";
                list.Add(new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = v, RelativePath = path, Sha256 = "h" + v, IsCurrent = v == versions });
                if (fileExists) _files.Existing.Add(path);
            }

            await _repo.AddAsync(new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = list,
                FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
            }, CancellationToken.None);
        }

        private static ArchiveQuery Q(InvoiceDirection direction = InvoiceDirection.Purchase)
        {
            return new ArchiveQuery { Direction = direction };
        }

        // ---- Chiều, ngày, mã ----

        [Test]
        public async Task OnlyTheChosenDirection_IsReturned()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 5));
            await AddAsync(InvoiceDirection.Sold, "2", new DateTime(2026, 1, 5));

            var page = await _service.QueryAsync(Q(InvoiceDirection.Sold), CancellationToken.None);

            Assert.That(page.Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("2"));
        }

        [Test]
        public async Task DateRange_IsInclusiveOnBothEnds()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 31));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 2, 1));
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 2, 28));
            await AddAsync(InvoiceDirection.Purchase, "4", new DateTime(2026, 3, 1));
            var q = Q();
            q.IssueFrom = new DateTime(2026, 2, 1);
            q.IssueTo = new DateTime(2026, 2, 28);

            var page = await _service.QueryAsync(q, CancellationToken.None);

            Assert.That(page.Rows.Select(r => r.Invoice.InvoiceNumber), Is.EquivalentTo(new[] { "2", "3" }));
        }

        [Test]
        public async Task StatusCheckResultFormSymbolAndNumber_FilterByThePortalValues()
        {
            await AddAsync(InvoiceDirection.Purchase, "40", new DateTime(2026, 1, 1), status: "1", check: "5");
            await AddAsync(InvoiceDirection.Purchase, "41", new DateTime(2026, 1, 2), status: "6", check: "5");
            await AddAsync(InvoiceDirection.Purchase, "42", new DateTime(2026, 1, 3), status: "1", check: "0", symbol: "C26TBN", form: "2");

            Assert.That((await _service.QueryAsync(With(q => q.StatusCode = "6"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("41"));
            Assert.That((await _service.QueryAsync(With(q => q.CheckResultCode = "0"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("42"));
            Assert.That((await _service.QueryAsync(With(q => q.FormSymbol = "2"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("42"));
            Assert.That((await _service.QueryAsync(With(q => q.InvoiceSymbol = "c26tbn"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("42"));
            Assert.That((await _service.QueryAsync(With(q => q.InvoiceNumber = "0000040"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("40"));
        }

        [Test]
        public async Task CounterpartyTaxCode_MatchesTheSellerForPurchasesAndTheBuyerForSales()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), counterpartyMst: "0100100001");
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), counterpartyMst: "0100100002");
            await AddAsync(InvoiceDirection.Sold, "3", new DateTime(2026, 1, 3), counterpartyMst: "0100100002");

            var purchase = await _service.QueryAsync(With(q => q.CounterpartyTaxCode = "0100100002"), CancellationToken.None);
            var sold = await _service.QueryAsync(With(InvoiceDirection.Sold, q => q.CounterpartyTaxCode = "0100100002"), CancellationToken.None);

            Assert.That(purchase.Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("2"));
            Assert.That(sold.Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("3"));
        }

        // ---- Tìm nhanh ----

        [Test]
        public async Task QuickSearch_FindsByNumberTaxCodeOrName_IgnoringCaseAndVietnameseDiacritics()
        {
            await AddAsync(InvoiceDirection.Purchase, "128", new DateTime(2026, 1, 1), "CÔNG TY TNHH ĐÔNG Á", "0109988776");
            await AddAsync(InvoiceDirection.Purchase, "129", new DateTime(2026, 1, 2), "Hộ kinh doanh Beta", "0100100002");

            Assert.That((await _service.QueryAsync(With(q => q.QuickSearch = "tnhh dong a"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("128"));
            Assert.That((await _service.QueryAsync(With(q => q.QuickSearch = "0109988776"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("128"));
            Assert.That((await _service.QueryAsync(With(q => q.QuickSearch = "129"), CancellationToken.None)).Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("129"));
            Assert.That((await _service.QueryAsync(With(q => q.QuickSearch = "HO KINH"), CancellationToken.None)).Rows, Has.Count.EqualTo(1));
            Assert.That((await _service.QueryAsync(With(q => q.QuickSearch = "không có"), CancellationToken.None)).Rows, Is.Empty);
        }

        [TestCase("ĐÔNG Á đường", "dong a duong")]
        [TestCase("  Hà Nội ", "ha noi")]
        [TestCase(null, "")]
        public void Fold_RemovesDiacriticsAndCase(string input, string expected)
        {
            Assert.That(InvoiceArchiveService.Fold(input), Is.EqualTo(expected));
        }

        // ---- Sắp xếp / phân trang ----

        [Test]
        public async Task DefaultSort_IsNewestFirst_ThenHighestNumber()
        {
            await AddAsync(InvoiceDirection.Purchase, "9", new DateTime(2026, 1, 5));
            await AddAsync(InvoiceDirection.Purchase, "10", new DateTime(2026, 1, 5));
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 9));

            var page = await _service.QueryAsync(Q(), CancellationToken.None);

            Assert.That(page.Rows.Select(r => r.Invoice.InvoiceNumber), Is.EqualTo(new[] { "3", "10", "9" }), "Số 10 đứng trước 9 (so sánh số, không so sánh chữ)");
        }

        [Test]
        public async Task SortByNumberCounterpartyAndAmount_BothDirections()
        {
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 1), "Beta", before: 300m);
            await AddAsync(InvoiceDirection.Purchase, "10", new DateTime(2026, 1, 2), "Alpha", before: 100m);
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 3), "Đức", before: 200m);

            Assert.That(await Numbers(q => { q.SortKey = ArchiveSortKey.InvoiceNumber; q.SortDescending = false; }), Is.EqualTo(new[] { "1", "2", "10" }));
            Assert.That(await Numbers(q => { q.SortKey = ArchiveSortKey.Counterparty; q.SortDescending = false; }), Is.EqualTo(new[] { "10", "2", "1" }));
            Assert.That(await Numbers(q => { q.SortKey = ArchiveSortKey.TotalPayment; q.SortDescending = true; }), Is.EqualTo(new[] { "2", "1", "10" }));
        }

        [Test]
        public async Task Paging_NumbersRowsAcrossPages_AndClampsAnOutOfRangePage()
        {
            for (var i = 1; i <= 7; i++) await AddAsync(InvoiceDirection.Purchase, i.ToString(), new DateTime(2026, 1, i));

            var second = await _service.QueryAsync(With(q => { q.PageSize = 3; q.PageIndex = 1; }), CancellationToken.None);
            var beyond = await _service.QueryAsync(With(q => { q.PageSize = 3; q.PageIndex = 99; }), CancellationToken.None);

            Assert.That(second.TotalCount, Is.EqualTo(7));
            Assert.That(second.PageCount, Is.EqualTo(3));
            Assert.That(second.Rows.Select(r => r.Number), Is.EqualTo(new[] { 4, 5, 6 }), "STT liên tục theo trang");
            Assert.That(beyond.PageIndex, Is.EqualTo(2));
            Assert.That(beyond.Rows.Select(r => r.Number), Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public async Task EmptyArchive_IsAValidEmptyPage()
        {
            var page = await _service.QueryAsync(Q(), CancellationToken.None);

            Assert.That(page.Rows, Is.Empty);
            Assert.That(page.TotalCount, Is.EqualTo(0));
            Assert.That(page.PageCount, Is.EqualTo(1));
            Assert.That(page.Summary.Count, Is.EqualTo(0));
        }

        // ---- Tóm tắt ----

        [Test]
        public async Task Summary_CoversTheWholeFilteredSet_NotJustThePage_WithoutLosingPrecision()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), before: 0.1m, tax: 0.01m);
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), before: 0.2m, tax: 0.02m);
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 3), before: 12345678901.23m, tax: 1234567890.12m);

            var page = await _service.QueryAsync(With(q => q.PageSize = 1), CancellationToken.None);

            Assert.That(page.Rows, Has.Count.EqualTo(1));
            Assert.That(page.Summary.Count, Is.EqualTo(3));
            Assert.That(page.Summary.TotalBeforeTax, Is.EqualTo(12345678901.53m));
            Assert.That(page.Summary.TotalTax, Is.EqualTo(1234567890.15m));
            Assert.That(page.Summary.TotalPayment, Is.EqualTo(12345678901.53m + 1234567890.15m));
        }

        [Test]
        public async Task Summary_FollowsTheFilters()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), status: "1", before: 100m);
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), status: "6", before: 500m);

            var page = await _service.QueryAsync(With(q => q.StatusCode = "6"), CancellationToken.None);

            Assert.That(page.Summary.Count, Is.EqualTo(1));
            Assert.That(page.Summary.TotalBeforeTax, Is.EqualTo(500m));
        }

        // ---- Tình trạng file ----

        [Test]
        public async Task FileState_ShowsOk_Missing_AndMultiVersion()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), fileExists: false);
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 3), versions: 2);

            var rows = (await _service.QueryAsync(With(q => { q.SortKey = ArchiveSortKey.InvoiceNumber; q.SortDescending = false; }), CancellationToken.None)).Rows;

            Assert.That(rows.Select(r => r.FileState), Is.EqualTo(new[] { ArchiveFileState.Ok, ArchiveFileState.Missing, ArchiveFileState.MultiVersion }));
            Assert.That(rows[2].VersionCount, Is.EqualTo(2));
        }

        [Test]
        public async Task FileFilter_ProblemsAndMultiVersion_AndTheSummaryCountsProblems()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), fileExists: false);
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 3), versions: 2);

            var problems = await _service.QueryAsync(With(q => q.FileFilter = FileIntegrityFilter.Problems), CancellationToken.None);
            var multi = await _service.QueryAsync(With(q => q.FileFilter = FileIntegrityFilter.MultiVersion), CancellationToken.None);
            var all = await _service.QueryAsync(Q(), CancellationToken.None);

            Assert.That(problems.Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("2"));
            Assert.That(multi.Rows.Single().Invoice.InvoiceNumber, Is.EqualTo("3"));
            Assert.That(all.Summary.ProblemCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AFileThatDisappears_IsReportedMissingOnTheNextQuery()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            Assert.That((await _service.QueryAsync(Q(), CancellationToken.None)).Rows.Single().FileState, Is.EqualTo(ArchiveFileState.Ok));

            _files.Existing.Clear();

            var forced = Q();
            forced.RefreshFiles = true;
            Assert.That((await _service.QueryAsync(forced, CancellationToken.None)).Rows.Single().FileState, Is.EqualTo(ArchiveFileState.Missing));
        }

        [Test]
        public async Task FileState_IsCachedBriefly_SoRepeatedQueriesDoNotHitTheDiskAgain()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await _service.QueryAsync(Q(), CancellationToken.None);
            var checks = _files.ExistsCalls;

            await _service.QueryAsync(Q(), CancellationToken.None);
            await _service.QueryAsync(Q(), CancellationToken.None);

            Assert.That(_files.ExistsCalls, Is.EqualTo(checks), "Truy vấn lặp lại dùng bộ nhớ đệm");

            var forced = Q();
            forced.RefreshFiles = true;
            await _service.QueryAsync(forced, CancellationToken.None);

            Assert.That(_files.ExistsCalls, Is.GreaterThan(checks), "Kiểm tra file bỏ qua bộ nhớ đệm");
        }

        // ---- Năm / doanh nghiệp ----

        [Test]
        public async Task Years_AreDistinctAndNewestFirst_ForTheDirection()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2025, 12, 31));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 6, 1));
            await AddAsync(InvoiceDirection.Sold, "4", new DateTime(2024, 6, 1));

            Assert.That(await _service.YearsAsync(InvoiceDirection.Purchase, CancellationToken.None), Is.EqualTo(new[] { 2026, 2025 }));
            Assert.That(await _service.YearsAsync(InvoiceDirection.Sold, CancellationToken.None), Is.EqualTo(new[] { 2024 }));
        }

        [Test]
        public void NoCompanyOpen_IsRejected()
        {
            _context.Current = null;

            Assert.ThrowsAsync<StorageGuardException>(() => _service.QueryAsync(Q(), CancellationToken.None));
            Assert.ThrowsAsync<StorageGuardException>(() => _service.YearsAsync(InvoiceDirection.Sold, CancellationToken.None));
        }

        [Test]
        public async Task CompanyScopedRepository_KeepsEachTaxCodeInItsOwnArchive()
        {
            var context = new FakeContext { Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "A", @"C:\A", null) };
            var scoped = new CompanyScopedInvoiceRepository(context, () => new InMemoryInvoiceRepository());
            var service = new InvoiceArchiveService(scoped, _files, context);
            var invoice = new NormalizedInvoice { Direction = InvoiceDirection.Purchase, InvoiceNumber = "1", IssueDate = new DateTime(2026, 1, 1), SellerTaxCode = "0100100001" };
            await scoped.AddAsync(new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = new List<InvoiceVersionRecord>()
            }, CancellationToken.None);
            Assert.That((await service.QueryAsync(Q(), CancellationToken.None)).TotalCount, Is.EqualTo(1));

            context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "B", @"C:\B", null);

            Assert.That((await service.QueryAsync(Q(), CancellationToken.None)).TotalCount, Is.EqualTo(0), "MST khác không thấy dữ liệu của MST kia");
        }

        // ---- Hỗ trợ ----

        private ArchiveQuery With(Action<ArchiveQuery> change)
        {
            return With(InvoiceDirection.Purchase, change);
        }

        private ArchiveQuery With(InvoiceDirection direction, Action<ArchiveQuery> change)
        {
            var q = Q(direction);
            change(q);
            return q;
        }

        private async Task<string[]> Numbers(Action<ArchiveQuery> change)
        {
            return (await _service.QueryAsync(With(change), CancellationToken.None)).Rows.Select(r => r.Invoice.InvoiceNumber).ToArray();
        }

        private sealed class FakeFiles : IInvoiceFileStore
        {
            public HashSet<string> Existing = new HashSet<string>();

            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName) { throw new NotSupportedException(); }
            public void DiscardTemp(string tempPath) { }
            public int ExistsCalls;
            public bool Exists(string storageRoot, string relativePath) { ExistsCalls++; return Existing.Contains(relativePath); }
            public void Delete(string storageRoot, string relativePath) { }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }
    }
}
