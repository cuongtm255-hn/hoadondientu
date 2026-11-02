using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.WinForms.History;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class HistoryPresenterTests : CompanyUiTestBase
    {
        private InMemoryDownloadRepository _repo;
        private DownloadHistoryService _service;
        private HistoryViewStateStore _state;
        private FakeHistoryView _view;
        private HistoryPresenter _presenter;
        private CompanyContext _company;

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryDownloadRepository();
            _company = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty", @"C:\Kho\0101234567", null);
            Context.Current = _company;
            _service = new DownloadHistoryService(_repo, Context);
            _state = new HistoryViewStateStore();
            _view = new FakeHistoryView();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Dispose();
        }

        private async Task<DownloadSession> AddAsync(
            DateTime createdLocal, DownloadSessionState state = DownloadSessionState.Completed, InvoiceDirection direction = InvoiceDirection.Purchase,
            int received = 10, int saved = 10, int errors = 0, int segments = 1, int completedSegments = 1)
        {
            var session = new DownloadSession(
                Guid.NewGuid(), _company.CompanyId, _company.TaxCode, @"C:\Kho", direction, PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(),
                createdLocal.ToUniversalTime(), "CUONGTM");
            if (state != DownloadSessionState.Draft)
            {
                session.TransitionTo(DownloadSessionState.Running, DateTime.UtcNow);
                if (state != DownloadSessionState.Running) session.TransitionTo(state, DateTime.UtcNow);
            }

            var list = new List<DownloadSegment>();
            for (var i = 0; i < segments; i++)
            {
                var segment = new DownloadSegment(Guid.NewGuid(), session.Id, i, new DateSegment(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)))
                {
                    ReceivedCount = received / segments, XmlSavedCount = saved / segments, ErrorCount = errors / segments
                };
                if (i < completedSegments)
                {
                    segment.TransitionTo(DownloadSegmentState.Running);
                    segment.TransitionTo(DownloadSegmentState.Completed);
                }

                list.Add(segment);
            }

            await _repo.CreateAsync(session, list, CancellationToken.None);
            return session;
        }

        private HistoryPresenter Open()
        {
            _presenter = new HistoryPresenter(_view, _service, Context, Nav, _state, Runner, Errors);
            _presenter.Initialize();
            return _presenter;
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
        {
            return UiWait.Until(condition, timeoutMs);
        }

        [Test]
        public async Task Opens_WithSummaryCardsAndRowsNewestFirst()
        {
            await AddAsync(new DateTime(2026, 10, 31, 8, 22, 0));
            await AddAsync(new DateTime(2026, 11, 1, 9, 15, 0), DownloadSessionState.Running, segments: 4, completedSegments: 2);
            await AddAsync(new DateTime(2026, 10, 31, 16, 4, 0), DownloadSessionState.CompletedWithErrors, received: 20, saved: 18, errors: 2);

            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            var r = _view.Result;
            Assert.That(r.TotalSessions, Is.EqualTo("3"));
            Assert.That(r.CompletedSessions, Is.EqualTo("1"));
            Assert.That(r.ActiveSessions, Is.EqualTo("1"));
            Assert.That(r.AttentionSessions, Is.EqualTo("1"));
            Assert.That(r.Rows.Select(x => x.Status), Is.EqualTo(new[] { "Đang tải 50%", "Hoàn tất có lỗi", "Hoàn tất" }));
            Assert.That(r.Rows.Select(x => x.ActionText), Is.EqualTo(new[] { "Xem tiến độ", "Xử lý lỗi", "Xem kết quả" }));
            Assert.That(r.Rows[0].Direction, Is.EqualTo("Mua vào"));
            Assert.That(r.Rows[0].Period, Is.EqualTo("Tháng 10/2026"));
            Assert.That(r.Rows[1].ExistingCount, Is.EqualTo("0"));
            Assert.That(r.Rows[1].ErrorCount, Is.EqualTo("2"));
            Assert.That(r.FooterText, Is.EqualTo("Hiển thị 1–3 / 3 phiên"));
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
        public void EmptyHistory_ShowsZeros_NotAnError()
        {
            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.TotalSessions, Is.EqualTo("0"));
            Assert.That(_view.Result.FooterText, Does.Contain("Chưa có phiên"));
            Assert.That(_view.Result.CanNext, Is.False);
            Assert.That(Errors.Shown, Is.Empty);
        }

        [Test]
        public async Task Filters_NarrowTheResult_AndGoBackToPageOne()
        {
            for (var i = 1; i <= 30; i++) await AddAsync(new DateTime(2026, 10, 1).AddDays(i % 28).AddHours(i), direction: i % 3 == 0 ? InvoiceDirection.Sold : InvoiceDirection.Purchase);
            Open();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 20), Is.True);
            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 2/2"), Is.True);

            _view.Filter.Direction = InvoiceDirection.Sold;
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 1/1" && _view.Result.Rows.Count == 10), Is.True);
            Assert.That(_view.Result.Rows.All(x => x.Direction == "Bán ra"), Is.True);
            Assert.That(_view.Result.TotalSessions, Is.EqualTo("30"), "Thẻ tóm tắt tính toàn bộ lịch sử");
        }

        [Test]
        public async Task Clear_ResetsTheFilter()
        {
            await AddAsync(new DateTime(2026, 11, 1), DownloadSessionState.Cancelled);
            await AddAsync(new DateTime(2026, 11, 2));
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.Status = HistoryStatusFilter.Stopped;
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1), Is.True);

            _view.RaiseClear();

            Assert.That(WaitFor(() => _view.Result.Rows.Count == 2), Is.True);
            Assert.That(_view.Filter.Status, Is.EqualTo(HistoryStatusFilter.All));
        }

        [Test]
        public async Task Paging_ShowsRangeAndEnablesButtons()
        {
            for (var i = 1; i <= 45; i++) await AddAsync(new DateTime(2026, 10, 1).AddHours(i));
            Open();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 20), Is.True);
            Assert.That(_view.Result.FooterText, Is.EqualTo("Hiển thị 1–20 / 45 phiên"));
            Assert.That(_view.Result.CanPrevious, Is.False);

            _view.RaisePage(1);
            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 3/3"), Is.True);

            Assert.That(_view.Result.FooterText, Is.EqualTo("Hiển thị 41–45 / 45 phiên"));
            Assert.That(_view.Result.CanNext, Is.False);
            Assert.That(_view.Result.CanPrevious, Is.True);
        }

        [Test]
        public async Task OpeningARow_NavigatesToProgress_WithTheSessionId()
        {
            var session = await AddAsync(new DateTime(2026, 11, 1));
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseSession(_view.Result.Rows.Single().SessionId);

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.DownloadProgress));
            Assert.That(Nav.Parameters[0], Is.EqualTo(session.Id));
        }

        [Test]
        public void NewSession_NavigatesToTheDownloadScreen()
        {
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseNewSession();

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.Download));
        }

        [TestCase(DownloadSessionState.Running, "Xem tiến độ", HistoryStatusTone.Warning)]
        [TestCase(DownloadSessionState.PausedByUser, "Xem tiến độ", HistoryStatusTone.Warning)]
        [TestCase(DownloadSessionState.WaitingForLogin, "Xem tiến độ", HistoryStatusTone.Warning)]
        [TestCase(DownloadSessionState.Completed, "Xem kết quả", HistoryStatusTone.Good)]
        [TestCase(DownloadSessionState.Cancelled, "Xem kết quả", HistoryStatusTone.Neutral)]
        [TestCase(DownloadSessionState.CompletedWithErrors, "Xử lý lỗi", HistoryStatusTone.Bad)]
        [TestCase(DownloadSessionState.Failed, "Xử lý lỗi", HistoryStatusTone.Bad)]
        public void ActionAndTone_FollowTheSessionState(DownloadSessionState state, string action, HistoryStatusTone tone)
        {
            Assert.That(HistoryPresenter.ActionText(state), Is.EqualTo(action));
            Assert.That(HistoryPresenter.Tone(state), Is.EqualTo(tone));
        }

        [Test]
        public async Task ComingBack_RestoresFilterAndPage()
        {
            for (var i = 1; i <= 45; i++) await AddAsync(new DateTime(2026, 10, 1).AddHours(i), i % 5 == 0 ? DownloadSessionState.Cancelled : DownloadSessionState.Completed);
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.Status = HistoryStatusFilter.Completed;
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.TotalSessions == "45" && _view.Result.PageText == "Trang 1/2"), Is.True);
            _view.RaisePage(1);
            Assert.That(WaitFor(() => _view.Result.PageText == "Trang 2/2"), Is.True);
            _presenter.Dispose();

            var back = new FakeHistoryView();
            _view = back;
            _presenter = new HistoryPresenter(back, _service, Context, Nav, _state, Runner, Errors);
            _presenter.Initialize();

            Assert.That(WaitFor(() => back.Result != null), Is.True);
            Assert.That(back.Filter.Status, Is.EqualTo(HistoryStatusFilter.Completed));
            Assert.That(back.Result.PageText, Is.EqualTo("Trang 2/2"));
        }

        [Test]
        public async Task ChangingCompany_StartsFresh()
        {
            await AddAsync(new DateTime(2026, 11, 1));
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            _view.Filter.Status = HistoryStatusFilter.Stopped;
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => _view.Result.Rows.Count == 0), Is.True);
            _presenter.Dispose();

            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "Khác", @"C:\Kho\0109999999", null);
            var other = new FakeHistoryView();
            _view = other;
            _presenter = new HistoryPresenter(other, _service, Context, Nav, _state, Runner, Errors);
            _presenter.Initialize();

            Assert.That(WaitFor(() => other.Result != null), Is.True);
            Assert.That(other.Filter.Status, Is.EqualTo(HistoryStatusFilter.All));
        }

        [Test]
        public async Task InvertedDateRange_ShowsAClearMessage_AndKeepsTheCurrentList()
        {
            await AddAsync(new DateTime(2026, 11, 1));
            Open();
            Assert.That(WaitFor(() => _view.Result != null && _view.Result.Rows.Count == 1), Is.True);

            _view.Filter.From = new DateTime(2026, 12, 1);
            _view.Filter.To = new DateTime(2026, 11, 1);
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("lớn hơn")), Is.True, _view.Message);
            Assert.That(_view.Result.Rows.Count, Is.EqualTo(1), "Danh sách đang hiển thị được giữ nguyên");

            _view.Filter.From = new DateTime(2026, 11, 1);
            _view.RaiseFilterChanged();

            Assert.That(WaitFor(() => _view.Message == string.Empty), Is.True);
        }

        [Test]
        public async Task FilterChangedWhileLoading_IsAppliedAfterwards_NotDropped()
        {
            await AddAsync(new DateTime(2026, 11, 1), direction: InvoiceDirection.Purchase);
            await AddAsync(new DateTime(2026, 11, 2), direction: InvoiceDirection.Sold);
            var gated = new GatedHistory(_service);
            _presenter = new HistoryPresenter(_view, gated, Context, Nav, _state, Runner, Errors);
            _presenter.Initialize();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            gated.Block();
            _view.Filter.Direction = InvoiceDirection.Purchase;
            _view.RaiseFilterChanged();
            Assert.That(WaitFor(() => gated.Entered), Is.True);
            _view.Filter.Direction = InvoiceDirection.Sold;
            _view.RaiseFilterChanged();
            gated.Release();

            Assert.That(WaitFor(() => _view.Result.Rows.Count == 1 && _view.Result.Rows[0].Direction == "Bán ra"), Is.True);
        }

        private sealed class GatedHistory : IDownloadHistoryService
        {
            private readonly IDownloadHistoryService _inner;
            private readonly object _gate = new object();
            private TaskCompletionSource<bool> _block;
            private bool _entered;

            public GatedHistory(IDownloadHistoryService inner)
            {
                _inner = inner;
            }

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

            public async Task<HistoryPage> QueryAsync(HistoryQuery query, CancellationToken cancellationToken)
            {
                TaskCompletionSource<bool> wait;
                lock (_gate)
                {
                    wait = _block;
                    if (wait != null && !wait.Task.IsCompleted) _entered = true;
                }

                if (wait != null) await wait.Task;
                return await _inner.QueryAsync(query, cancellationToken);
            }
        }

        private sealed class FakeHistoryView : IHistoryView
        {
            private readonly object _gate = new object();
            private HistoryViewModel _result;
            private string _message;

            public FakeHistoryView()
            {
                Filter = new HistoryFilterInput();
            }

            public event EventHandler FilterChanged;
            public event EventHandler ClearRequested;
            public event EventHandler NewSessionRequested;
            public event EventHandler<PageRequestedEventArgs> PageRequested;
            public event EventHandler<SessionRequestedEventArgs> SessionRequested;

            public HistoryFilterInput Filter { get; private set; }
            public HistoryViewModel Result { get { lock (_gate) { return _result; } } }
            public string Message { get { lock (_gate) { return _message; } } }

            public HistoryFilterInput ReadFilter()
            {
                return new HistoryFilterInput { Search = Filter.Search, Direction = Filter.Direction, Status = Filter.Status, From = Filter.From, To = Filter.To };
            }

            public void ShowFilter(HistoryFilterInput filter)
            {
                Filter = new HistoryFilterInput { Search = filter.Search, Direction = filter.Direction, Status = filter.Status, From = filter.From, To = filter.To };
            }

            public void ShowResult(HistoryViewModel model)
            {
                lock (_gate) { _result = model; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate) { _message = text; }
            }

            public void RaiseFilterChanged() { FilterChanged(this, EventArgs.Empty); }
            public void RaiseClear() { ClearRequested(this, EventArgs.Empty); }
            public void RaiseNewSession() { NewSessionRequested(this, EventArgs.Empty); }
            public void RaisePage(int delta) { PageRequested(this, new PageRequestedEventArgs(delta)); }
            public void RaiseSession(Guid id) { SessionRequested(this, new SessionRequestedEventArgs(id)); }
        }
    }
}
