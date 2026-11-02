using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;
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

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryInvoiceRepository();
            _files = new FakeFiles();
            _company = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty", @"C:\Kho\0101234567", null);
            Context.Current = _company;
            _service = new InvoiceArchiveService(_repo, _files, Context);
            _state = new ArchiveViewStateStore();
            _view = new FakeArchiveView();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Dispose();
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
            _presenter = new ArchivePresenter(_view, _service, Context, _state, Runner, Errors, new DateTime(2026, 11, 2));
            _presenter.Initialize();
            return _presenter;
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                if (condition()) return true;
                Thread.Sleep(15);
            }

            return condition();
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
            _presenter = new ArchivePresenter(back, _service, Context, _state, Runner, Errors, new DateTime(2026, 11, 2));
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
            _presenter = new ArchivePresenter(other, _service, Context, _state, Runner, Errors, new DateTime(2026, 11, 2));
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

        // ---- Fakes ----

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
        }
    }
}
