#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using HoaDonDienTu.WinForms.Download;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>SCR-04 trên engine thật + cổng giả (dữ liệu giả, không gọi cổng Thuế thật).</summary>
    [TestFixture]
    public class DownloadProgressPresenterTests : CompanyUiTestBase
    {
        private const string Mst = "0101234567";
        private static readonly CompanyId Id = CompanyId.NewId();

        private PortalApiConfig _config;
        private MockPortalHandler _portal;
        private InMemoryDownloadRepository _downloads;
        private DownloadOrchestrator _orchestrator;
        private FakeProgressView _view;
        private InMemoryPortalSessionStore _portalSession;
        private SessionSettingShell _shell;
        private DownloadProgressPresenter _presenter;
        private CompanyContext _company;

        [SetUp]
        public void SetUp()
        {
            _config = PortalApiConfigDefaults.Create();
            _portal = new MockPortalHandler(_config);
            _downloads = new InMemoryDownloadRepository();
            _portalSession = new InMemoryPortalSessionStore();
            _company = new CompanyContext(Id, TaxCode.Parse(Mst), "Công ty mẫu", @"C:\Kho\" + Mst, null);
            Context.Current = _company;
            _nav = new RecordingNav();
            var client = new PortalHttpApiClient(_config, new PortalSessionMaterial(null, null, MockInvoiceDataset.TokenFor(Mst)), _portal);
            _orchestrator = new DownloadOrchestrator(
                _downloads, Context, _portalSession, () => client, new CountingStore(), new NoLimit(), DefaultRetryPolicy.Standard,
                new ModeEntitlement { Value = AppMode.Standard }, new SystemClock(), Logger, (span, token) => Task.FromResult(0));
            _view = new FakeProgressView();
            _shell = new SessionSettingShell(_portalSession, _company);
            LogIn();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Dispose();
        }

        private void LogIn()
        {
            _portalSession.Set(new PortalSession(Id, _company.TaxCode, new PortalSessionMaterial(null, null, MockInvoiceDataset.TokenFor(Mst)), DateTime.UtcNow));
        }

        private async Task<Guid> CreateAsync(PeriodSpec period)
        {
            var plan = DownloadPlan.Create(InvoiceDirection.Sold, period, new InvoiceSearchFilter(), new PeriodSplitter());
            return await _orchestrator.CreateSessionAsync(plan, CancellationToken.None);
        }

        private RecordingNav _nav;

        private DownloadProgressPresenter Open(Guid? sessionId)
        {
            _presenter = new DownloadProgressPresenter(
                _view, _orchestrator, _downloads, _portalSession, () => _shell, () => _nav, Confirm, Runner, Errors, new SystemClock(), sessionId);
            _presenter.Initialize();
            return _presenter;
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 20000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }

            return condition();
        }

        [Test]
        public async Task DraftSession_RunsAutomatically_AndShowsAFinishedSummary()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));

            Open(id);

            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True, _view.Message);
            var vm = _view.Last;
            Assert.That(vm.Percent, Is.EqualTo(100));
            Assert.That(vm.ProgressText, Is.EqualTo("1/1 tháng"));
            Assert.That(vm.Rows.Single().StateText, Is.EqualTo("Hoàn tất"));
            Assert.That(vm.CanPause, Is.False);
            Assert.That(vm.CanCancel, Is.False);
            Assert.That(vm.ReceivedText, Is.EqualTo(vm.SavedText), "Lần đầu: mọi hóa đơn nhận được đều được lưu mới");
            Assert.That(vm.ReceivedSub, Does.StartWith("Mới ").And.Contain("Đã có 0"));
            Assert.That(_view.Message, Does.Contain("Đã tải xong"));
            Assert.That(_view.MessageIsError, Is.False);
        }

        [Test]
        public async Task FinishedSession_OffersToOpenTheArchive_ForTheDirectionOfTheSession()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));
            Open(id);
            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_view.Last.CanOpenArchive, Is.True);
            Assert.That(_view.Last.CanNewDownload, Is.True);

            _view.RaiseOpenArchive();

            Assert.That(WaitFor(() => _nav.Targets.Count == 1), Is.True);
            Assert.That(_nav.Targets[0], Is.EqualTo(ScreenId.InvoiceArchive));
            var request = (ArchiveNavigationRequest)_nav.Parameters[0];
            Assert.That(request.Direction, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(request.FileFilter, Is.EqualTo(FileIntegrityFilter.All));
        }

        [Test]
        public async Task FinishedSession_OffersToStartAnotherDownload()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));
            Open(id);
            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True, _view.Message);

            _view.RaiseNewDownload();

            Assert.That(WaitFor(() => _nav.Targets.Count == 1), Is.True);
            Assert.That(_nav.Targets[0], Is.EqualTo(ScreenId.Download));
        }

        [Test]
        public async Task UnfinishedSession_CannotOpenTheArchiveOrStartAnother()
        {
            _portalSession.Clear();
            _shell.LoginSucceeds = false;
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));

            Open(id);

            Assert.That(WaitFor(() => _view.Last != null), Is.True);
            Assert.That(_view.Last.CanOpenArchive, Is.False);
            Assert.That(_view.Last.CanNewDownload, Is.False);
        }

        [Test]
        public void SegmentRows_UseVietnameseNames_NotPortalParameters()
        {
            var session = DownloadSession.Restore(Guid.NewGuid(), _company.CompanyId, _company.TaxCode, "x", InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 11),
                new InvoiceSearchFilter(), DownloadSessionState.Completed, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, "t");
            var segments = new[] { "5", "6", "8", null }.Select((code, i) =>
                new DownloadSegment(Guid.NewGuid(), session.Id, i, new DateSegment(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30))) { CheckResultCode = code }).ToList();

            var rows = segments.Select(s => DownloadProgressPresenter.Row(s, false)).ToList();

            Assert.That(rows.Select(r => r.Month), Is.All.EqualTo("Tháng 11/2026"));
            Assert.That(rows.Select(r => r.CheckResult), Is.EqualTo(new[] { "Đã cấp mã hóa đơn", PortalOptions.CheckResultLabel("6"), PortalOptions.CheckResultLabel("8"), "Tất cả" }));
            Assert.That(rows.Select(r => r.Month + r.CheckResult), Has.None.Contain("ttxly"));
        }

        [Test]
        public async Task Subtitle_ShowsDirectionAndRangeOfTheSession()
        {
            var id = await CreateAsync(PeriodSpec.ForQuarter(2026, 1));

            Open(id);

            Assert.That(WaitFor(() => _view.Last != null && _view.Last.Rows.Count == 3), Is.True);
            Assert.That(_view.Last.Subtitle, Does.StartWith("Bán ra · 01/01/2026–31/03/2026"));
            Assert.That(_view.Last.Rows.Select(r => r.Month), Is.EqualTo(new[] { "Tháng 01/2026", "Tháng 02/2026", "Tháng 03/2026" }));
            Assert.That(_view.Last.SessionCode, Has.Length.EqualTo(8));
        }

        [Test]
        public async Task NotLoggedIn_AsksForLoginFirst_AndDoesNotRun()
        {
            _portalSession.Clear();
            _shell.LoginSucceeds = false;
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));

            Open(id);

            Assert.That(WaitFor(() => _view.Message != null && _view.Message.Contains("Cần đăng nhập")), Is.True);
            Assert.That(_shell.Reasons, Is.EqualTo(new[] { LoginReason.TokenExpired }));
            Assert.That((await _downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.Draft));
            Assert.That(_presenter.IsRunning, Is.False);
            Assert.That(_view.MessageIsError, Is.True);
        }

        [Test]
        public async Task LoginCompleted_RunContinuesWithoutUserAction()
        {
            _portalSession.Clear();
            _shell.LoginSucceeds = true;
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));

            Open(id);

            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_shell.Reasons, Is.EqualTo(new[] { LoginReason.TokenExpired }));
        }

        [Test]
        public async Task ExpiredToken_StopsAtWaitingForLogin_ThenResumeLogsInAndFinishes_AC06()
        {
            _portal.EnqueueFailure(401);
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));
            Open(id);

            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Chờ đăng nhập lại" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_view.Last.CanResume, Is.True);
            Assert.That(_view.Message, Does.Contain("hết hạn"));
            Assert.That(_view.MessageIsError, Is.True);

            _portalSession.Clear();
            _shell.LoginSucceeds = true;
            _view.RaiseResume();

            Assert.That(WaitFor(() => _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_shell.Reasons, Is.EqualTo(new[] { LoginReason.TokenExpired }));
        }

        [Test]
        public async Task Cancel_Declined_DoesNothing()
        {
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 1));
            _portal.Latency = TimeSpan.FromMilliseconds(40);
            Confirm.Answer = false;
            Open(id);
            Assert.That(WaitFor(() => _presenter.IsRunning), Is.True);

            _view.RaiseCancel();

            Assert.That(Confirm.Messages, Has.Count.EqualTo(1));
            Assert.That(WaitFor(() => _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True);
        }

        [Test]
        public async Task Cancel_Confirmed_StopsAndKeepsSavedData()
        {
            var id = await CreateAsync(PeriodSpec.ForYear(2026));
            _portal.Latency = TimeSpan.FromMilliseconds(40);
            Open(id);
            Assert.That(WaitFor(() => _presenter.IsRunning && _view.Last != null && _view.Last.CanCancel), Is.True);

            _view.RaiseCancel();

            Assert.That(WaitFor(() => _view.Last.StateText == "Đã hủy" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_view.Message, Does.Contain("Đã hủy"));
            Assert.That(_view.Last.CanCancel, Is.False);
            Assert.That(_view.Last.Rows.Any(r => r.StateText == "Đã bỏ qua"), Is.True);
        }

        [Test]
        public async Task Pause_StopsWithACheckpoint_AndResumeFinishes()
        {
            var id = await CreateAsync(PeriodSpec.ForYear(2026));
            _portal.Latency = TimeSpan.FromMilliseconds(30);
            Open(id);
            Assert.That(WaitFor(() => _view.Last != null && _view.Last.CanPause), Is.True);

            _view.RaisePause();

            Assert.That(WaitFor(() => _view.Last.StateText == "Đã tạm dừng" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_view.Last.CanResume, Is.True);
            Assert.That(_view.Last.CheckpointText, Does.StartWith("Checkpoint gần nhất"));
            var receivedAtPause = _view.Last.ReceivedText;

            _portal.Latency = TimeSpan.Zero;
            _view.RaiseResume();

            Assert.That(WaitFor(() => _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning, 60000), Is.True, _view.Message);
            Assert.That(_view.Last.ReceivedText, Is.Not.EqualTo(receivedAtPause));
        }

        [Test]
        public async Task FailedSegment_IsShownWithRetry_AndRetryFixesIt()
        {
            _portal.MalformedExportResponse = true;
            var id = await CreateAsync(PeriodSpec.ForMonth(2026, 2));
            Open(id);
            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Hoàn tất có lỗi" && !_presenter.IsRunning), Is.True, _view.Message);
            var row = _view.Last.Rows.Single();
            Assert.That(row.CanRetry, Is.True);
            Assert.That(row.Note, Does.Contain("lỗi"));
            Assert.That(_view.Last.CanRetryFailed, Is.True);
            Assert.That(_view.MessageIsError, Is.True);

            _portal.MalformedExportResponse = false;
            _view.RaiseRetrySegment(row.Id);

            Assert.That(WaitFor(() => _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_view.Last.Rows.Single().CanRetry, Is.False);
        }

        [Test]
        public async Task RetryAllFailed_RerunsEveryFailedSegment()
        {
            _portal.MalformedExportResponse = true;
            var id = await CreateAsync(PeriodSpec.ForQuarter(2026, 1));
            Open(id);
            Assert.That(WaitFor(() => _view.Last != null && _view.Last.StateText == "Hoàn tất có lỗi" && !_presenter.IsRunning), Is.True, _view.Message);
            Assert.That(_view.Last.Rows.Count(r => r.CanRetry), Is.GreaterThanOrEqualTo(2));

            _portal.MalformedExportResponse = false;
            _view.RaiseRetryFailed();

            Assert.That(WaitFor(() => _view.Last.StateText == "Hoàn tất" && !_presenter.IsRunning, 60000), Is.True, _view.Message);
            Assert.That(_view.Last.Rows.All(r => !r.CanRetry), Is.True);
        }

        [Test]
        public async Task NoSessionId_OpensTheLatestUnfinishedSession()
        {
            var older = await CreateAsync(PeriodSpec.ForMonth(2026, 1));
            await Task.Delay(20);
            var newer = await CreateAsync(PeriodSpec.ForMonth(2026, 2));
            await _orchestrator.CancelAsync(older, CancellationToken.None);
            _shell.LoginSucceeds = false;
            _portalSession.Clear();

            Open(null);

            Assert.That(WaitFor(() => _view.Last != null), Is.True);
            Assert.That(_view.Last.Rows.Single().Month, Is.EqualTo("Tháng 02/2026"));
            Assert.That((await _downloads.GetSessionAsync(newer, CancellationToken.None)).Id, Is.EqualTo(newer));
        }

        [Test]
        public void NoSession_ShowsAHelpfulMessage()
        {
            Open(null);

            Assert.That(WaitFor(() => _view.Message != null), Is.True);
            Assert.That(_view.Message, Does.Contain("Không có phiên tải"));
            Assert.That(_view.Last, Is.Null);
        }

        [Test]
        public async Task Dispose_PausesARunningSession_SoItCanContinueLater()
        {
            var id = await CreateAsync(PeriodSpec.ForYear(2026));
            _portal.Latency = TimeSpan.FromMilliseconds(40);
            Open(id);
            Assert.That(WaitFor(() => _presenter.IsRunning), Is.True);

            _presenter.Dispose();

            Assert.That(WaitFor(() => _downloads.GetSessionAsync(id, CancellationToken.None).Result.State == DownloadSessionState.PausedByUser), Is.True);
            var resumable = await _orchestrator.GetUnfinishedAsync(CancellationToken.None);
            Assert.That(resumable.Select(s => s.Session.Id), Does.Contain(id));
        }

        [Test]
        public void StateTexts_CoverEveryState()
        {
            foreach (DownloadSessionState state in Enum.GetValues(typeof(DownloadSessionState)))
            {
                Assert.That(DownloadProgressPresenter.StateText(state), Is.Not.Empty, state.ToString());
            }
        }

        // ---- Fakes ----

        private sealed class FakeProgressView : IDownloadProgressView
        {
            private readonly object _gate = new object();
            private DownloadProgressViewModel _last;
            private string _message;
            private bool _messageIsError;

            public event EventHandler PauseRequested;
            public event EventHandler ResumeRequested;
            public event EventHandler CancelRequested;
            public event EventHandler RetryFailedRequested;
            public event EventHandler OpenArchiveRequested;
            public event EventHandler NewDownloadRequested;
            public event EventHandler<SegmentRetryEventArgs> RetrySegmentRequested;

            public DownloadProgressViewModel Last { get { lock (_gate) { return _last; } } }
            public string Message { get { lock (_gate) { return _message; } } }
            public bool MessageIsError { get { lock (_gate) { return _messageIsError; } } }

            public void Show(DownloadProgressViewModel model)
            {
                lock (_gate) { _last = model; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate)
                {
                    _message = text;
                    _messageIsError = isError;
                }
            }

            public void RaisePause() { PauseRequested(this, EventArgs.Empty); }
            public void RaiseResume() { ResumeRequested(this, EventArgs.Empty); }
            public void RaiseCancel() { CancelRequested(this, EventArgs.Empty); }
            public void RaiseRetryFailed() { RetryFailedRequested(this, EventArgs.Empty); }
            public void RaiseOpenArchive() { OpenArchiveRequested(this, EventArgs.Empty); }
            public void RaiseNewDownload() { NewDownloadRequested(this, EventArgs.Empty); }
            public void RaiseRetrySegment(Guid id) { RetrySegmentRequested(this, new SegmentRetryEventArgs(id)); }
        }

        /// <summary>Mô phỏng đăng nhập: thành công thì đặt lại phiên cổng Thuế như form đăng nhập thật.</summary>
        private sealed class SessionSettingShell : HoaDonDienTu.WinForms.Shell.IShellActions
        {
            private readonly InMemoryPortalSessionStore _store;
            private readonly CompanyContext _company;

            public SessionSettingShell(InMemoryPortalSessionStore store, CompanyContext company)
            {
                _store = store;
                _company = company;
                Reasons = new List<LoginReason>();
            }

            public bool LoginSucceeds { get; set; }
            public List<LoginReason> Reasons { get; private set; }

            public Task LoginAsync(LoginReason reason)
            {
                Reasons.Add(reason);
                if (LoginSucceeds)
                {
                    _store.Set(new PortalSession(
                        _company.CompanyId, _company.TaxCode, new PortalSessionMaterial(null, null, MockInvoiceDataset.TokenFor(Mst)), DateTime.UtcNow));
                }

                return Task.FromResult(0);
            }
        }

        private sealed class CountingStore : IInvoiceStore
        {
            public Task<InvoiceSaveResult> UpsertAsync(DownloadSession session, PortalInvoiceDto invoice, InvoiceXmlPayload xml, CancellationToken cancellationToken)
            {
                return Task.FromResult(InvoiceSaveResult.Saved);
            }
        }

        private sealed class NoLimit : IRateLimiter
        {
            public Task WaitAsync(CancellationToken cancellationToken) { return Task.FromResult(0); }
        }
    }
}
#endif
