using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Application.Exports;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.WinForms.Detail;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class ArchivePresenterTests : CompanyUiTestBase
    {
        private InMemoryInvoiceRepository _repo;
        private FakeFiles _files;
        private InvoiceArchiveService _service;
        private ArchiveViewStateStore _state;
        private FakeArchiveView _view;
        private ArchivePresenter _presenter;
        private CompanyContext _company;
        private string _storageRoot;

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryInvoiceRepository();
            _files = new FakeFiles();
            // Thư mục kho thật trên đĩa tạm: nút xuất tạo thư mục Exports trong đó.
            _storageRoot = Path.Combine(Path.GetTempPath(), "hddt-arch-" + Guid.NewGuid().ToString("N"));
            _company = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty", _storageRoot, null);
            Context.Current = _company;
            _service = new InvoiceArchiveService(_repo, _files, Context);
            _state = new ArchiveViewStateStore();
            _view = new FakeArchiveView();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Dispose();
            try
            {
                if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true);
            }
            catch (IOException)
            {
            }
        }

        private async Task AddAsync(InvoiceDirection direction, string number, DateTime date, string name = "Công ty ABC", string mst = "0100100001",
            decimal before = 100m, decimal tax = 10m, string status = "1", bool fileExists = true, int versions = 1)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = direction, InvoiceNumber = number, IssueDate = date, FormSymbol = "1", InvoiceSymbol = "C26TAA",
                SellerTaxCode = direction == InvoiceDirection.Purchase ? mst : "0101234567",
                SellerName = direction == InvoiceDirection.Purchase ? name : "Công ty mẫu",
                BuyerTaxCode = direction == InvoiceDirection.Sold ? mst : "0101234567",
                BuyerName = direction == InvoiceDirection.Sold ? name : "Công ty mẫu",
                StatusRaw = status, CheckResultRaw = "5", TotalBeforeTax = before, TotalTax = tax, TotalPayment = before + tax
            };
            var list = new List<InvoiceVersionRecord>();
            for (var v = 1; v <= versions; v++)
            {
                var path = @"X\" + number + "_" + v + ".xml";
                list.Add(new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = v, RelativePath = path, IsCurrent = v == versions });
                if (fileExists) _files.Existing.Add(path);
            }

            await _repo.AddAsync(new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = list,
                FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
            }, CancellationToken.None);
        }

        private ArchivePresenter Open()
        {
            _presenter = new ArchivePresenter(_view, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();
            return _presenter;
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
        {
            return UiWait.Until(condition, timeoutMs);
        }

        // ---- Mở màn hình ----

        [Test]
        public async Task Opens_OnPurchases_OfTheLatestYearWithData_NewestFirst()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2025, 12, 31));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 5));
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 3, 9));
            await AddAsync(InvoiceDirection.Sold, "9", new DateTime(2026, 3, 9));

            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Years, Is.EqualTo(new[] { 2026, 2025 }));
            Assert.That(_view.SelectedYear, Is.EqualTo(2026));
            Assert.That(_view.Result.Rows.Select(r => r.InvoiceNumber), Is.EqualTo(new[] { "3", "2" }));
            Assert.That(_view.Result.ResultCount, Is.EqualTo("2"));
            Assert.That(_view.Result.ResultSub, Does.Contain("mua vào năm 2026"));
            Assert.That(_view.Result.CounterpartyIdCaption, Is.EqualTo("MST người bán"));
        }

        [Test]
        public void NoCompany_ShowsAHelpfulMessage()
        {
            Context.Current = null;

            Open();

            Assert.That(WaitFor(() => _view.Message != null), Is.True);
            Assert.That(_view.Message, Does.Contain("Chưa chọn doanh nghiệp"));
            Assert.That(_view.Result, Is.Null);
        }

        [Test]
        public void EmptyArchive_ShowsZeroResults_NotAnError()
        {
            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.ResultCount, Is.EqualTo("0"));
            Assert.That(_view.Result.FooterText, Does.Contain("Không có hóa đơn"));
            Assert.That(_view.Result.Rows, Is.Empty);
            Assert.That(_view.Result.CanNext, Is.False);
            Assert.That(Errors.Shown, Is.Empty);
        }

        // ---- Lọc ----

        [Test]
        public async Task DirectionSwitch_ReloadsYearsAndRows_AndRelabelsTheCounterpartyColumn()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 5));
            await AddAsync(InvoiceDirection.Sold, "2", new DateTime(2024, 6, 5), name: "Khách lẻ", mst: "0100100009");
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.Filter.Direction = InvoiceDirection.Sold;
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Result.CounterpartyIdCaption == "MST người mua"), Is.True);
            Assert.That(_view.Years, Is.EqualTo(new[] { 2024 }));
            Assert.That(_view.SelectedYear, Is.EqualTo(2024), "Năm mới nhất của chiều mới");
            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1 && _view.Result.Rows[0].CounterpartyName == "Khách lẻ"), Is.True);
        }

        [Test]
        public async Task StatusAndQuickSearch_NarrowTheResult_AndResetToTheFirstPage()
        {
            for (var i = 1; i <= 60; i++) await AddAsync(InvoiceDirection.Purchase, i.ToString(), new DateTime(2026, 1, 1).AddDays(i % 28), status: i % 10 == 0 ? "6" : "1");
            Open();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 50), Is.True);
            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 2/2"), Is.True);

            _view.Filter.StatusCode = "6";
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Result.ResultCount == "6" && _view.Result.PageText == "Trang 1/1"), Is.True);
            Assert.That(_view.Result.Rows.All(r => r.Status == "Hóa đơn đã bị hủy"), Is.True);
        }

        [Test]
        public async Task QuickSearch_IgnoresVietnameseDiacritics()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 5), name: "CÔNG TY ĐÔNG Á");
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 6), name: "Beta");
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.Filter.QuickSearch = "dong a";
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1), Is.True);
            Assert.That(_view.Result.Rows[0].CounterpartyName, Is.EqualTo("CÔNG TY ĐÔNG Á"));
        }

        [Test]
        public async Task Clear_RestoresDefaults_ButKeepsTheDirection()
        {
            await AddAsync(InvoiceDirection.Sold, "1", new DateTime(2026, 1, 5));
            await AddAsync(InvoiceDirection.Sold, "2", new DateTime(2026, 1, 6), status: "6");
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.Direction = InvoiceDirection.Sold;
            _view.Filter.StatusCode = "6";
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.ResultCount == "1"), Is.True);

            _view.RaiseClear();

            Assert.That(WaitFor(() => _view.Result.ResultCount == "2"), Is.True);
            Assert.That(_view.Filter.StatusCode, Is.Empty);
            Assert.That(_view.Filter.Direction, Is.EqualTo(InvoiceDirection.Sold));
        }

        // ---- Phân trang / sắp xếp ----

        [Test]
        public async Task Paging_ShowsRangeAndEnablesButtonsCorrectly()
        {
            for (var i = 1; i <= 120; i++) await AddAsync(InvoiceDirection.Purchase, i.ToString(), new DateTime(2026, 1, 1).AddDays(i % 28));
            Open();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 50), Is.True);
            Assert.That(_view.Result.FooterText, Is.EqualTo("Hiển thị 1–50 / 120"));
            Assert.That(_view.Result.CanPrevious, Is.False);
            Assert.That(_view.Result.CanNext, Is.True);

            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 2/3"), Is.True);
            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 3/3"), Is.True);

            Assert.That(_view.Result.FooterText, Is.EqualTo("Hiển thị 101–120 / 120"));
            Assert.That(_view.Result.CanNext, Is.False);
            Assert.That(_view.Result.Rows.First().Number, Is.EqualTo("101"));

            _view.RaisePage(-1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 2/3"), Is.True);
        }

        [Test]
        public async Task SortHeader_TogglesDirection_AndSwitchingColumnUsesItsDefault()
        {
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 1), before: 300m);
            await AddAsync(InvoiceDirection.Purchase, "10", new DateTime(2026, 1, 2), before: 100m);
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.SortKey, Is.EqualTo(ArchiveSortKey.IssueDate));
            Assert.That(_view.Result.SortDescending, Is.True);

            _view.RaiseSort(ArchiveSortKey.IssueDate);
            Assert.That(WaitFor(() => !_view.Result.SortDescending), Is.True);
            Assert.That(_view.Result.Rows.Select(r => r.InvoiceNumber), Is.EqualTo(new[] { "2", "10" }));

            _view.RaiseSort(ArchiveSortKey.TotalPayment);
            Assert.That(WaitFor(() => _view.Result.SortKey == ArchiveSortKey.TotalPayment), Is.True);
            Assert.That(_view.Result.SortDescending, Is.True, "Số tiền mặc định giảm dần");
            Assert.That(_view.Result.Rows.First().InvoiceNumber, Is.EqualTo("2"));

            _view.RaiseSort(ArchiveSortKey.InvoiceNumber);
            Assert.That(WaitFor(() => _view.Result.SortKey == ArchiveSortKey.InvoiceNumber), Is.True);
            Assert.That(_view.Result.SortDescending, Is.False, "Số hóa đơn mặc định tăng dần");
        }

        // ---- Tóm tắt, định dạng, file ----

        [Test]
        public async Task Summary_UsesCompactVietnameseNumbers()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 5), before: 12800000000m, tax: 1280000000m);
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 6), before: 500000m, tax: 50000m);
            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.TotalBeforeTax, Is.EqualTo("12,8 tỷ"));
            Assert.That(_view.Result.TotalTax, Is.EqualTo("1,28 tỷ"));
            Assert.That(_view.Result.TotalPayment, Is.EqualTo("14,08 tỷ"));
        }

        [TestCase(0, "0")]
        [TestCase(999999, "999.999")]
        [TestCase(2500000, "2,5 triệu")]
        [TestCase(12800000000, "12,8 tỷ")]
        [TestCase(-3000000, "-3 triệu")]
        public void Compact_MatchesTheWireframeStyle(long value, string expected)
        {
            Assert.That(ArchivePresenter.Compact(value), Is.EqualTo(expected));
        }

        [Test]
        public void Money_UsesDotThousandsSeparator()
        {
            Assert.That(ArchivePresenter.Money(30000000m), Is.EqualTo("30.000.000"));
            Assert.That(ArchivePresenter.Money(null), Is.Empty);
        }

        [Test]
        public async Task FileColumn_ShowsOk_Missing_AndVersionCount()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), fileExists: false);
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 3), versions: 2);
            Open();

            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 3), Is.True);
            var byNumber = _view.Result.Rows.ToDictionary(r => r.InvoiceNumber, r => r.FileText);
            Assert.That(byNumber["1"], Is.EqualTo("XML OK"));
            Assert.That(byNumber["2"], Is.EqualTo("Thiếu XML"));
            Assert.That(byNumber["3"], Is.EqualTo("2 phiên bản"));
            Assert.That(_view.Result.ResultSub, Does.Contain("1 thiếu file"));
        }

        [Test]
        public async Task CheckFiles_ReportsMissingFiles_AndNoneWhenAllArePresent()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), fileExists: false);
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("thiếu file XML")), Is.True, _view.Message);
            Assert.That(_view.Message, Does.Contain("1 hóa đơn"));
            Assert.That(_view.MessageIsError, Is.True);

            _files.Existing.Add(@"X\2_1.xml");
            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => _view.Message.Contains("mọi file XML đều còn")), Is.True, _view.Message);
            Assert.That(_view.MessageIsError, Is.False);
        }

        // ---- Kiểm tra nội dung file (báo cáo toàn vẹn) ----

        private ArchivePresenter OpenWithIntegrity(IntegrityReport report, out FakeChecker checker, out FakeReportDialog dialog)
        {
            checker = new FakeChecker { Report = report };
            dialog = new FakeReportDialog();
            _presenter = new ArchivePresenter(_view, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2), null, null, new ArchiveIntegrityContext(checker, dialog));
            _presenter.Initialize();
            return _presenter;
        }

        [Test]
        public async Task CheckFiles_WithIntegrity_CleanReport_SaysSo_AndShowsNoDialog()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            FakeChecker checker;
            FakeReportDialog dialog;
            OpenWithIntegrity(new IntegrityReport { VersionsChecked = 1 }, out checker, out dialog);
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("toàn vẹn")), Is.True, _view.Message);
            Assert.That(_view.MessageIsError, Is.False);
            Assert.That(checker.Calls, Is.EqualTo(1));
            Assert.That(dialog.Shown, Is.Empty);
        }

        [Test]
        public async Task CheckFiles_WithIntegrity_Problems_ShowTheReportDialogAndASummaryWithCounts()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            var report = new IntegrityReport { VersionsChecked = 3 };
            report.Problems.Add(new IntegrityProblem { InvoiceNumber = "7", InvoiceSymbol = "C26TAA", VersionNo = 1, RelativePath = @"X\7_1.xml", Kind = IntegrityProblemKind.Missing });
            report.Problems.Add(new IntegrityProblem { InvoiceNumber = "8", InvoiceSymbol = "C26TAA", VersionNo = 2, RelativePath = @"X\8_2.xml", Kind = IntegrityProblemKind.HashMismatch });
            report.Orphans.Add(@"X\stray.xml");
            FakeChecker checker;
            FakeReportDialog dialog;
            OpenWithIntegrity(report, out checker, out dialog);
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => dialog.Shown.Count == 1), Is.True);
            Assert.That(dialog.Shown[0], Is.SameAs(report));
            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("thiếu file")), Is.True, _view.Message);
            Assert.That(_view.Message, Does.Contain("1 thiếu file").And.Contain("1 bị đổi nội dung").And.Contain("1 file lạ"));
            Assert.That(_view.MessageIsError, Is.True);
        }

        [Test]
        public async Task CheckFiles_WithIntegrity_StillRefreshesTheTableFirst()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            FakeChecker checker;
            FakeReportDialog dialog;
            OpenWithIntegrity(new IntegrityReport(), out checker, out dialog);
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 1), Is.True);
            _files.Existing.Clear();

            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => checker.Calls == 1), Is.True);
            Assert.That(_view.Result.Rows[0].FileText, Is.EqualTo("Thiếu XML"));
        }

        [Test]
        public void IntegrityReportText_NamesEveryKind_AndCapsTheRows()
        {
            foreach (IntegrityProblemKind kind in Enum.GetValues(typeof(IntegrityProblemKind))) Assert.That(IntegrityReportText.Describe(kind), Does.Not.Contain(kind.ToString()), kind.ToString());

            var report = new IntegrityReport();
            for (var i = 0; i < IntegrityReportText.MaxRows + 25; i++) report.Orphans.Add(@"X\" + i + ".xml");
            int hidden;
            var rows = IntegrityReportText.Rows(report, out hidden);

            Assert.That(rows.Count, Is.EqualTo(IntegrityReportText.MaxRows));
            Assert.That(hidden, Is.EqualTo(25));
        }

        [Test]
        public void IntegrityReportText_ListsInvoiceProblemsBeforeOrphansAndTemps()
        {
            var report = new IntegrityReport();
            report.Orphans.Add(@"X\stray.xml");
            report.StrayTemps.Add(@"X\a.tmp");
            report.Problems.Add(new IntegrityProblem { InvoiceNumber = "0000128", InvoiceSymbol = "C26TAA", VersionNo = 2, RelativePath = @"X\128_v2.xml", Kind = IntegrityProblemKind.Missing });
            int hidden;

            var rows = IntegrityReportText.Rows(report, out hidden);

            Assert.That(rows[0], Is.EqualTo(new[] { "Thiếu file", "Số 0000128 · C26TAA", "v2", @"X\128_v2.xml" }));
            Assert.That(rows[1][3], Is.EqualTo(@"X\stray.xml"));
            Assert.That(rows[2][0], Does.Contain("tạm"));
        }

        private sealed class FakeChecker : IInvoiceIntegrityChecker
        {
            public IntegrityReport Report;
            public int Calls;

            public Task<IntegrityReport> CheckAsync(IProgress<int> progress, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(Report);
            }
        }

        private sealed class FakeReportDialog : IIntegrityReportDialog
        {
            public readonly List<IntegrityReport> Shown = new List<IntegrityReport>();

            public void Show(IntegrityReport report)
            {
                Shown.Add(report);
            }
        }

        // ---- Giữ trạng thái khi quay lại ----

        [Test]
        public async Task ComingBack_RestoresFilterSortAndPage()
        {
            for (var i = 1; i <= 120; i++) await AddAsync(InvoiceDirection.Purchase, i.ToString(), new DateTime(2026, 1, 1).AddDays(i % 28), status: i % 2 == 0 ? "6" : "1");
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.StatusCode = "6";
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.ResultCount == "60"), Is.True);
            _view.RaiseSort(ArchiveSortKey.InvoiceNumber);
            Assert.That(WaitFor(() => _view.Result.SortKey == ArchiveSortKey.InvoiceNumber), Is.True);
            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 2/2"), Is.True);
            _presenter.Dispose();

            var back = new FakeArchiveView();
            _view = back;
            _presenter = new ArchivePresenter(back, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();

            Assert.That(WaitFor(() => back.Result != null), Is.True);
            Assert.That(back.Filter.StatusCode, Is.EqualTo("6"));
            Assert.That(back.Result.PageText, Is.EqualTo("Trang 2/2"));
            Assert.That(back.Result.SortKey, Is.EqualTo(ArchiveSortKey.InvoiceNumber));
            Assert.That(back.Result.ResultCount, Is.EqualTo("60"));
        }

        [Test]
        public async Task ChangingCompany_StartsFresh_NotWithTheOtherCompanysFilters()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.StatusCode = "6";
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.ResultCount == "0"), Is.True);
            _presenter.Dispose();

            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "Khác", @"C:\Kho\0109999999", null);
            var other = new FakeArchiveView();
            _view = other;
            _presenter = new ArchivePresenter(other, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();

            Assert.That(WaitFor(() => other.Result != null), Is.True);
            Assert.That(other.Filter.StatusCode, Is.Empty);
        }

        [Test]
        public async Task StaleQueries_DoNotOverwriteTheNewestResult()
        {
            for (var i = 1; i <= 30; i++) await AddAsync(InvoiceDirection.Purchase, i.ToString(), new DateTime(2026, 1, 1).AddDays(i % 28), name: i == 7 ? "Alpha" : "Beta");
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.Filter.QuickSearch = "alp";
            _view.RaiseFilterChanged();
            _view.Filter.QuickSearch = "alpha";
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Result.ResultCount == "1"), Is.True);
            Thread.Sleep(100);
            Assert.That(_view.Result.ResultCount, Is.EqualTo("1"));
        }

        [Test]
        public async Task FilterChangedWhileLoading_IsAppliedAfterwards_NotDropped()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 5), name: "Alpha");
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 6), name: "Beta");
            var gated = new GatedService(_service);
            _presenter = new ArchivePresenter(_view, gated, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            gated.Block();
            _view.Filter.QuickSearch = "alp";
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => gated.Entered), Is.True, "Truy vấn đầu đang chờ");
            _view.Filter.QuickSearch = "beta";
            _view.RaiseFilterChanged();
            gated.Release();

            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1 && _view.Result.Rows[0].CounterpartyName == "Beta"), Is.True, "Bộ lọc mới nhất phải được áp dụng");
            Assert.That(gated.Searches.Last(), Is.EqualTo("beta"));
        }

        [Test]
        public async Task OpeningARow_NavigatesToTheDetailOfThatInvoice_AndTheFilterIsRememberedForComingBack()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 5), name: "Alpha");
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 6), name: "Beta");
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.QuickSearch = "beta";
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1), Is.True);
            var row = _view.Result.Rows.Single();

            _view.RaiseInvoice(row.InvoiceId);

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.InvoiceDetail));
            Assert.That(((InvoiceDetailRequest)Nav.Parameters[0]).InvoiceId, Is.EqualTo(row.InvoiceId));
            Assert.That(row.InvoiceId, Is.Not.EqualTo(Guid.Empty));

            ArchiveFilterInput remembered;
            ArchiveQuery query;
            Assert.That(_state.TryGet(_company.CompanyId, out remembered, out query), Is.True);
            Assert.That(remembered.QuickSearch, Is.EqualTo("beta"));
        }

        [Test]
        public async Task CheckFiles_WithoutACompany_DoesNotClaimSuccess()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), fileExists: false);
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Context.Current = null;

            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("Chưa chọn doanh nghiệp")), Is.True, _view.Message);
            Assert.That(_view.Message, Does.Not.Contain("mọi file XML đều còn"));
            Assert.That(_view.MessageIsError, Is.True);
        }

        [Test]
        public async Task CheckFiles_BypassesTheFileCache_SoAFileDeletedMeanwhileIsFound()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            Open();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 1), Is.True);
            _files.Existing.Clear();

            _view.RaiseCheckFiles();

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("thiếu file XML")), Is.True, _view.Message);
            Assert.That(_view.Result.Rows[0].FileText, Is.EqualTo("Thiếu XML"));
        }

        [Test]
        public async Task OpenedWithARequest_StartsFresh_OnTheRequestedDirectionAndProblemsOnly_ForAllYears()
        {
            await AddAsync(InvoiceDirection.Sold, "1", new DateTime(2024, 3, 1), fileExists: false);
            await AddAsync(InvoiceDirection.Sold, "2", new DateTime(2026, 3, 1));
            await AddAsync(InvoiceDirection.Sold, "3", new DateTime(2025, 3, 1), fileExists: false);
            await AddAsync(InvoiceDirection.Purchase, "4", new DateTime(2026, 3, 1), fileExists: false);
            _state.Save(_company.CompanyId, new ArchiveFilterInput { Direction = InvoiceDirection.Purchase, StatusCode = "6" }, new ArchiveQuery { Direction = InvoiceDirection.Purchase });

            _presenter = new ArchivePresenter(
                _view, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2),
                new ArchiveNavigationRequest(InvoiceDirection.Sold, FileIntegrityFilter.Problems));
            _presenter.Initialize();

            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 2), Is.True);
            Assert.That(_view.Filter.Direction, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(_view.Filter.FileFilter, Is.EqualTo(FileIntegrityFilter.Problems));
            Assert.That(_view.Filter.StatusCode, Is.Empty, "Bộ lọc đã nhớ bị bỏ qua");
            Assert.That(_view.SelectedYear, Is.EqualTo(0), "Xem mọi năm");
            Assert.That(_view.Result.Rows.Select(r => r.InvoiceNumber), Is.EquivalentTo(new[] { "1", "3" }));
        }

        // ---- Xuất Excel / ZIP (DD-09) ----

        private FakeExportService _exportService;
        private FakePicker _exportPicker;

        private ArchivePresenter OpenWithExport(bool allowed = true)
        {
            _exportService = new FakeExportService();
            _exportPicker = new FakePicker { Result = @"C:\Xuatile.out" };
            Entitlement.Value = allowed ? AppMode.Standard : AppMode.ReadOnlyService;
            var context = new ArchiveExportContext(_exportService, _exportPicker, Confirm, new ReadExportEntitlement(allowed));
            _presenter = new ArchivePresenter(_view, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2), null, context);
            _presenter.Initialize();
            return _presenter;
        }

        [Test]
        public async Task Export_Buttons_AreLockedWithoutTheEntitlement_OrWithoutAnExportContext()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport(allowed: false);
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.ExportAllowed, Is.False);
            _presenter.Dispose();

            var plain = new FakeArchiveView();
            _view = plain;
            _presenter = new ArchivePresenter(plain, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();
            Assert.That(WaitFor(() => plain.Result != null), Is.True);
            Assert.That(plain.ExportAllowed, Is.False);
            plain.RaiseExportExcel();
            Thread.Sleep(80);
            Assert.That(_exportService.Calls, Is.Empty);
        }

        [Test]
        public async Task ExportExcel_UsesTheCurrentFilter_AsksWhereToSave_AndReportsTheResult()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), name: "Alpha");
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), name: "Beta");
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.ExportAllowed, Is.True);
            _view.Filter.QuickSearch = "beta";
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1), Is.True);
            _exportService.Result = new ExportResult { Path = @"C:\Xuatile.out", Exported = 1, Skipped = new List<ExportProblem>() };

            _view.RaiseExportExcel();

            Assert.That(WaitFor(() => _exportService.Calls.Count == 1), Is.True);
            var call = _exportService.Calls[0];
            Assert.That(call.Kind, Is.EqualTo("excel"));
            Assert.That(call.Request.TargetPath, Is.EqualTo(@"C:\Xuatile.out"));
            Assert.That(call.Request.Overwrite, Is.True, "Hộp thoại lưu đã hỏi xác nhận ghi đè");
            Assert.That(call.Request.Scope.Query.QuickSearch, Is.EqualTo("beta"));
            Assert.That(call.Request.Scope.Query.Direction, Is.EqualTo(InvoiceDirection.Purchase));
            Assert.That(_exportPicker.Extension, Is.EqualTo("xlsx"));
            Assert.That(_exportPicker.SuggestedName, Does.StartWith("DanhSachHoaDon_Mua-vao_"));
            Assert.That(_exportPicker.SuggestedName, Does.EndWith(".xlsx"));
            Assert.That(_exportPicker.Title, Does.Contain("Công ty").And.Contain("0101234567"), "Nêu rõ doanh nghiệp bị tác động");
            Assert.That(_exportPicker.InitialDirectory, Is.EqualTo(Path.Combine(_storageRoot, "Exports")));
            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("Đã xuất 1 hóa đơn")), Is.True, _view.Message);
            Assert.That(_view.MessageIsError, Is.False);
        }

        [Test]
        public async Task ExportExcel_CancellingTheSaveDialog_DoesNothing()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _exportPicker.Result = null;

            _view.RaiseExportExcel();

            Assert.That(WaitFor(() => _exportPicker.Picks == 1), Is.True);
            Thread.Sleep(80);
            Assert.That(_exportService.Calls, Is.Empty);
        }

        [Test]
        public async Task ExportZip_WithMissingFiles_AsksFirst_AndExportsTheValidPartOnYes()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _exportService.NeedsDecision = new ExportNeedsDecisionException(
                new List<ExportProblem> { new ExportProblem { InvoiceNumber = "7" }, new ExportProblem { InvoiceNumber = "8" } }, 40);
            _exportService.Result = new ExportResult
            {
                Path = @"C:\Xuatile.out", Exported = 40, Skipped = new List<ExportProblem> { new ExportProblem { InvoiceNumber = "7" }, new ExportProblem { InvoiceNumber = "8" } }
            };
            Confirm.Answer = true;

            _view.RaiseExportZip();

            Assert.That(WaitFor(() => _exportService.Calls.Count == 2), Is.True);
            Assert.That(_exportService.Calls.Select(c => c.Policy), Is.EqualTo(new[] { ZipFailurePolicy.Ask, ZipFailurePolicy.SkipAndRecord }));
            Assert.That(Confirm.Messages.Single(), Does.Contain("2 hóa đơn thiếu hoặc lỗi").And.Contain("7, 8").And.Contain("40 hóa đơn hợp lệ"));
            Assert.That(_exportPicker.Extension, Is.EqualTo("zip"));
            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("Bỏ qua 2")), Is.True, _view.Message);
            Assert.That(_view.MessageIsError, Is.True, "Có hóa đơn bị bỏ qua nên cảnh báo");
        }

        [Test]
        public async Task ExportZip_WithMissingFiles_AndTheUserDeclines_WritesNothingMore()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _exportService.NeedsDecision = new ExportNeedsDecisionException(new List<ExportProblem> { new ExportProblem { InvoiceNumber = "7" } }, 3);
            Confirm.Answer = false;

            _view.RaiseExportZip();

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("Đã hủy xuất")), Is.True, _view.Message);
            Assert.That(_exportService.Calls.Count, Is.EqualTo(1));
            Assert.That(_exportService.Calls[0].Policy, Is.EqualTo(ZipFailurePolicy.Ask));
        }

        [Test]
        public async Task Export_NothingToExport_ShowsAClearMessage()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _exportService.Failure = new InvoiceStorageException("Không có hóa đơn nào để xuất.");

            _view.RaiseExportExcel();

            Assert.That(WaitFor(() => _view.Message == "Không có hóa đơn nào để xuất."), Is.True, _view.Message);
            Assert.That(_view.MessageIsError, Is.True);
            Assert.That(Errors.Shown, Is.Empty);
        }

        [Test]
        public async Task Export_UnexpectedFailure_GoesToTheErrorPresenter()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _exportService.Failure = new IOException("disk full");

            _view.RaiseExportExcel();

            Assert.That(WaitFor(() => Errors.Shown.Count > 0), Is.True);
        }

        // ---- Chọn nhiều, cột ẩn (T07-05) ----

        [Test]
        public async Task Selecting_Rows_ShowsTheCount_AndExportUsesExactlyThoseRows()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2));
            await AddAsync(InvoiceDirection.Purchase, "3", new DateTime(2026, 1, 3));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 3), Is.True);
            var ids = _view.Result.Rows.Select(r => r.InvoiceId).ToList();

            _view.RaiseSelection(ids[2], ids[0]);

            Assert.That(_view.SelectionCount, Is.EqualTo(2));
            _view.RaiseExportExcel();
            Assert.That(WaitFor(() => _exportService.Calls.Count == 1), Is.True);
            var scope = _exportService.Calls[0].Request.Scope;
            Assert.That(scope.IsSelection, Is.True);
            Assert.That(scope.SelectedInvoiceIds, Is.EqualTo(new[] { ids[2], ids[0] }));
            Assert.That(_exportPicker.SuggestedName, Does.Contain("Da-chon-2"));
        }

        [Test]
        public async Task WithoutASelection_ExportCoversTheWholeFilteredResult()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            var id = _view.Result.Rows[0].InvoiceId;
            _view.RaiseSelection(id);
            _view.RaiseSelection();
            Assert.That(_view.SelectionCount, Is.EqualTo(0));

            _view.RaiseExportZip();

            Assert.That(WaitFor(() => _exportService.Calls.Count == 1), Is.True);
            Assert.That(_exportService.Calls[0].Request.Scope.IsSelection, Is.False);
        }

        [Test]
        public async Task ZipExport_AlsoHonoursTheSelection()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2));
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 2), Is.True);
            var id = _view.Result.Rows[1].InvoiceId;
            _view.RaiseSelection(id);

            _view.RaiseExportZip();

            Assert.That(WaitFor(() => _exportService.Calls.Count == 1), Is.True);
            Assert.That(_exportService.Calls[0].Request.Scope.SelectedInvoiceIds, Is.EqualTo(new[] { id }));
        }

        [Test]
        public async Task NewResults_DropTheOldSelection()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1), name: "Alpha");
            await AddAsync(InvoiceDirection.Purchase, "2", new DateTime(2026, 1, 2), name: "Beta");
            OpenWithExport();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 2), Is.True);
            _view.RaiseSelection(_view.Result.Rows[0].InvoiceId, _view.Result.Rows[1].InvoiceId);
            Assert.That(_view.SelectionCount, Is.EqualTo(2));

            _view.Filter.QuickSearch = "alpha";
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1), Is.True);
            Assert.That(WaitFor(() => _view.SelectionCount == 0), Is.True);
            _view.RaiseExportExcel();
            Assert.That(WaitFor(() => _exportService.Calls.Count == 1), Is.True);
            Assert.That(_exportService.Calls[0].Request.Scope.IsSelection, Is.False, "Không xuất nhầm các dòng đã chọn từ kết quả cũ");
        }

        [Test]
        public async Task HiddenColumns_AreRemembered_PerCompany()
        {
            await AddAsync(InvoiceDirection.Purchase, "1", new DateTime(2026, 1, 1));
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.RestoredHidden, Is.Empty);
            _view.HiddenColumnsValue = new List<string> { "colTax", "colForm" };
            _view.RaiseColumns();
            _presenter.Dispose();

            var back = new FakeArchiveView();
            _view = back;
            _presenter = new ArchivePresenter(back, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();
            Assert.That(WaitFor(() => back.Result != null), Is.True);
            Assert.That(back.RestoredHidden, Is.EquivalentTo(new[] { "colTax", "colForm" }));
            _presenter.Dispose();

            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "Khác", _storageRoot + "-khac", null);
            var other = new FakeArchiveView();
            _view = other;
            _presenter = new ArchivePresenter(other, _service, Context, Nav, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();
            Assert.That(WaitFor(() => other.RestoredHidden != null), Is.True);
            Assert.That(other.RestoredHidden, Is.Empty);
        }

        // ---- Fakes ----

        private sealed class ReadExportEntitlement : IEntitlementService
        {
            private readonly bool _allowed;

            public ReadExportEntitlement(bool allowed)
            {
                _allowed = allowed;
            }

            public AppMode CurrentMode { get { return AppMode.Standard; } }
            public bool IsAllowed(AppFeature feature) { return feature == AppFeature.ReadExport && _allowed; }
            public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }

        private sealed class FakePicker : IExportSavePicker
        {
            public string Result;
            public string SuggestedName;
            public string Extension;
            public string Title;
            public string InitialDirectory;
            public int Picks;

            public string Pick(string suggestedFileName, string extension, string filterDescription, string title, string initialDirectory)
            {
                Picks++;
                SuggestedName = suggestedFileName;
                Extension = extension;
                Title = title;
                InitialDirectory = initialDirectory;
                return Result;
            }
        }

        private sealed class ExportCall
        {
            public string Kind;
            public ExportRequest Request;
            public ZipFailurePolicy Policy;
        }

        private sealed class FakeExportService : IInvoiceExportService
        {
            public readonly List<ExportCall> Calls = new List<ExportCall>();
            public ExportResult Result = new ExportResult { Path = "x", Exported = 1, Skipped = new List<ExportProblem>() };
            public ExportNeedsDecisionException NeedsDecision;
            public Exception Failure;

            public Task<ExportResult> ExportExcelAsync(ExportRequest request, IProgress<ExportProgress> progress, CancellationToken cancellationToken)
            {
                lock (Calls) { Calls.Add(new ExportCall { Kind = "excel", Request = request }); }
                if (Failure != null) throw Failure;
                return Task.FromResult(Result);
            }

            public Task<ExportResult> ExportZipAsync(ExportRequest request, ZipFailurePolicy policy, IProgress<ExportProgress> progress, CancellationToken cancellationToken)
            {
                lock (Calls) { Calls.Add(new ExportCall { Kind = "zip", Request = request, Policy = policy }); }
                if (Failure != null) throw Failure;
                if (policy == ZipFailurePolicy.Ask && NeedsDecision != null) throw NeedsDecision;
                return Task.FromResult(Result);
            }
        }

        private sealed class GatedService : IInvoiceArchiveService
        {
            private readonly IInvoiceArchiveService _inner;
            private readonly object _gate = new object();
            private TaskCompletionSource<bool> _block;
            private bool _entered;

            public GatedService(IInvoiceArchiveService inner)
            {
                _inner = inner;
                Searches = new List<string>();
            }

            public List<string> Searches { get; private set; }
            public bool Entered { get { lock (_gate) { return _entered; } } }

            public void Block()
            {
                lock (_gate)
                {
                    _block = new TaskCompletionSource<bool>();
                    _entered = false;
                }
            }

            public void Release()
            {
                lock (_gate) { _block.TrySetResult(true); }
            }

            public async Task<ArchivePage> QueryAsync(ArchiveQuery query, CancellationToken cancellationToken)
            {
                TaskCompletionSource<bool> wait;
                lock (_gate)
                {
                    Searches.Add(query.QuickSearch);
                    wait = _block;
                    if (wait != null && !wait.Task.IsCompleted) _entered = true;
                }

                if (wait != null) await wait.Task;
                return await _inner.QueryAsync(query, cancellationToken);
            }

            public Task<IList<int>> YearsAsync(InvoiceDirection direction, CancellationToken cancellationToken)
            {
                return _inner.YearsAsync(direction, cancellationToken);
            }
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

        private sealed class FakeArchiveView : IArchiveView
        {
            private readonly object _gate = new object();
            private ArchiveViewModel _result;
            private string _message;
            private bool _messageIsError;

            public FakeArchiveView()
            {
                Filter = new ArchiveFilterInput { Direction = InvoiceDirection.Purchase, StatusCode = string.Empty, CheckResultCode = string.Empty };
            }

            public event EventHandler FilterChanged;
            public event EventHandler ClearRequested;
            public event EventHandler CheckFilesRequested;
            public event EventHandler<PageRequestedEventArgs> PageRequested;
            public event EventHandler<SortRequestedEventArgs> SortRequested;
            public event EventHandler<InvoiceRequestedEventArgs> InvoiceRequested;
            public event EventHandler ExportExcelRequested;
            public event EventHandler ExportZipRequested;

            public bool? ExportAllowed;

            public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
            public event EventHandler ColumnsChanged;
            public List<string> HiddenColumnsValue = new List<string>();
            public IList<string> HiddenColumns { get { return HiddenColumnsValue; } }
            public IList<string> RestoredHidden;
            public int SelectionCount = -1;

            public void ShowHiddenColumns(IList<string> hidden)
            {
                RestoredHidden = hidden;
                HiddenColumnsValue = new List<string>(hidden);
            }

            public void ShowSelectionCount(int count) { SelectionCount = count; }
            public void RaiseSelection(params Guid[] ids) { SelectionChanged(this, new SelectionChangedEventArgs(ids)); }
            public void RaiseColumns() { ColumnsChanged(this, EventArgs.Empty); }

            public void ShowExportAllowed(bool allowed) { ExportAllowed = allowed; }
            public void RaiseExportExcel() { ExportExcelRequested(this, EventArgs.Empty); }
            public void RaiseExportZip() { ExportZipRequested(this, EventArgs.Empty); }

            public ArchiveFilterInput Filter { get; private set; }
            public IList<int> Years { get; private set; }
            public int SelectedYear { get; private set; }
            public ArchiveViewModel Result { get { lock (_gate) { return _result; } } }
            public string Message { get { lock (_gate) { return _message; } } }
            public bool MessageIsError { get { lock (_gate) { return _messageIsError; } } }

            public ArchiveFilterInput ReadFilter()
            {
                return new ArchiveFilterInput
                {
                    Direction = Filter.Direction, QuickSearch = Filter.QuickSearch, Year = SelectedYear, StatusCode = Filter.StatusCode,
                    CheckResultCode = Filter.CheckResultCode, FileFilter = Filter.FileFilter
                };
            }

            public void ShowFilter(ArchiveFilterInput filter)
            {
                Filter = new ArchiveFilterInput
                {
                    Direction = filter.Direction, QuickSearch = filter.QuickSearch, Year = filter.Year, StatusCode = filter.StatusCode ?? string.Empty,
                    CheckResultCode = filter.CheckResultCode ?? string.Empty, FileFilter = filter.FileFilter
                };
                SelectedYear = filter.Year;
            }

            public void ShowYears(IList<int> years, int selected)
            {
                Years = years;
                SelectedYear = selected;
            }

            public void ShowResult(ArchiveViewModel model)
            {
                lock (_gate) { _result = model; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate)
                {
                    _message = text;
                    _messageIsError = isError;
                }
            }

            public void RaiseFilterChanged() { FilterChanged(this, EventArgs.Empty); }
            public void RaiseClear() { ClearRequested(this, EventArgs.Empty); }
            public void RaiseCheckFiles() { CheckFilesRequested(this, EventArgs.Empty); }
            public void RaisePage(int delta) { PageRequested(this, new PageRequestedEventArgs(delta)); }
            public void RaiseSort(ArchiveSortKey key) { SortRequested(this, new SortRequestedEventArgs(key)); }
            public void RaiseInvoice(Guid id) { InvoiceRequested(this, new InvoiceRequestedEventArgs(id)); }
        }
    }
}
