using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.WinForms.Login;
using HoaDonDienTu.WinForms.Navigation;
using HoaDonDienTu.WinForms.Shell;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainShellPresenterTests
    {
        private const string Mst = "0101234567";
        private FakeShellView _view;
        private FakeLoginCoordinator _login;
        private FakeContextManager _contextManager;
        private InMemoryPortalSessionStore _store;
        private FakeJobs _jobs;
        private FakePrompt _prompt;
        private RecordingErrors _errors;
        private RecordingLogger _logger;
        private FakeScreenFactory _screens;
        private MainShellPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeShellView();
            _login = new FakeLoginCoordinator();
            _contextManager = new FakeContextManager(FakeContextAccessor.Context(Mst));
            _store = new InMemoryPortalSessionStore();
            _jobs = new FakeJobs();
            _prompt = new FakePrompt();
            _errors = new RecordingErrors();
            _logger = new RecordingLogger();
            var shutdown = new ShutdownCoordinator(_contextManager, _jobs, _store, _prompt, _logger);
            _screens = new FakeScreenFactory();
            var navigation = new NavigationService(_screens, _view, new FakeEntitlement(), _contextManager, _logger);
            _presenter = new MainShellPresenter(
                _view, _login, _contextManager, _store,
                new UiOperationRunner(new UserErrorMapper(), _logger), _errors, shutdown, navigation);
            _presenter.Initialize();
        }

        [Test]
        public void DownloadIsEnabledOnlyWhileThereIsAPortalSessionForTheOpenCompany()
        {
            _presenter.SetMode(new ShellModeViewState { CanDownload = true, CanPortalLogin = true });
            Assert.That(_view.LastMode.CanDownload, Is.False, "chưa đăng nhập cổng Thuế");
            Assert.That(_view.LastMode.CanPortalLogin, Is.True, "vẫn đăng nhập được");

            _store.Set(new PortalSession(
                _contextManager.Current.CompanyId, _contextManager.Current.TaxCode, new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
            Assert.That(_view.LastMode.CanDownload, Is.True);

            _store.Clear();
            Assert.That(_view.LastMode.CanDownload, Is.False);
        }

        [Test]
        public void NavigationRequested_ShowsScreen_AndSelectsNavigation()
        {
            _view.RaiseNavigation(ScreenId.InvoiceArchive);

            Assert.That(_view.Selected, Is.EqualTo(ScreenId.InvoiceArchive));
            Assert.That(_view.ShownScreens.Count, Is.EqualTo(1));
        }

        [Test]
        public void NavigationRequested_WhenCurrentScreenCannotClose_ShowsReason_AndKeepsScreen()
        {
            _view.RaiseNavigation(ScreenId.InvoiceArchive);
            _screens.Created[0].CanClose = false;

            _view.RaiseNavigation(ScreenId.Settings);

            Assert.That(_view.Selected, Is.EqualTo(ScreenId.InvoiceArchive));
            Assert.That(_view.ShownScreens.Count, Is.EqualTo(1));
            Assert.That(_view.Errors.Count, Is.EqualTo(1));
            Assert.That(_screens.Created[0].Disposed, Is.False);
        }

        [Test]
        public void CompanySelectorRequested_OpensCompanyList()
        {
            _view.RaiseCompanySelector();

            Assert.That(_view.Selected, Is.EqualTo(ScreenId.CompanyList));
        }

        [Test]
        public void PortalLogin_WithoutContext_ShowsError_DoesNotCallCoordinator()
        {
            _contextManager.Current = null;

            _view.RaisePortalLogin();

            Assert.That(_login.Calls, Is.EqualTo(0));
            Assert.That(_view.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void PortalLogin_BuildsRequestForCurrentCompanyAndScreen()
        {
            _presenter.ShowAsync(ScreenId.InvoiceArchive).GetAwaiter().GetResult();

            _view.RaisePortalLogin();

            Assert.That(_login.Calls, Is.EqualTo(1));
            Assert.That(_login.LastRequest.CompanyId, Is.EqualTo(_contextManager.Current.CompanyId));
            Assert.That(_login.LastRequest.ExpectedTaxCode.Value, Is.EqualTo(Mst));
            Assert.That(_login.LastRequest.ReturnScreen, Is.EqualTo(ScreenId.InvoiceArchive));
        }

        [Test]
        public void LoginAsync_Startup_UsesStartupReason()
        {
            _presenter.LoginAsync(LoginReason.Startup).GetAwaiter().GetResult();

            Assert.That(_login.LastRequest.Reason, Is.EqualTo(LoginReason.Startup));
        }

        [Test]
        public void PortalLogin_Success_UpdatesHeader()
        {
            _login.Handler = req =>
            {
                _store.Set(new PortalSession(req.CompanyId, req.ExpectedTaxCode, new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
                return PortalLoginResult.Success(_store.GetSessionForApi());
            };

            _view.RaisePortalLogin();

            Assert.That(_view.LastSession.IsLoggedIn, Is.True);
        }

        [Test]
        public void SessionChanged_UpdatesHeaderBothWays()
        {
            _store.Set(new PortalSession(_contextManager.Current.CompanyId, _contextManager.Current.TaxCode,
                new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
            Assert.That(_view.LastSession.IsLoggedIn, Is.True);

            _store.Clear();

            Assert.That(_view.LastSession.IsLoggedIn, Is.False);
        }

        [Test]
        public void PortalLogin_FailureWithMessage_ShowsError_CancelledIsSilent()
        {
            _login.Handler = req => PortalLoginResult.Failure(PortalLoginOutcome.NotAllowed, "không được phép");
            _view.RaisePortalLogin();
            Assert.That(_view.Errors, Is.EqualTo(new[] { "không được phép" }));

            _view.Errors.Clear();
            _login.Handler = req => PortalLoginResult.Failure(PortalLoginOutcome.Cancelled, null);
            _view.RaisePortalLogin();
            Assert.That(_view.Errors, Is.Empty);
        }

        [Test]
        public void PortalLogin_CoordinatorThrows_GoesToErrorPresenter_NotCrash()
        {
            _login.Handler = req => { throw new InvalidOperationException("token=SECRET"); };

            Assert.DoesNotThrow(() => _view.RaisePortalLogin());

            Assert.That(_errors.Shown.Count, Is.EqualTo(1));
            Assert.That(_errors.Shown[0].Message, Does.Not.Contain("SECRET"));
            Assert.That(_logger.Lines.Count, Is.EqualTo(1), "lỗi được ghi log kèm mã tham chiếu");
        }

        [Test]
        public void PortalLogin_ReportsBusyThenIdle()
        {
            _view.RaisePortalLogin();

            Assert.That(_view.Operations[0].IsBusy, Is.True);
            Assert.That(_view.Operations[_view.Operations.Count - 1].IsBusy, Is.False);
        }

        [Test]
        public void ShellClosing_NoJob_ClearsSessionAndClosesShell()
        {
            _store.Set(new PortalSession(_contextManager.Current.CompanyId, _contextManager.Current.TaxCode,
                new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));

            _view.RaiseClosing();

            Assert.That(_view.CloseShellCalls, Is.EqualTo(1));
            Assert.That(_store.HasActiveSession, Is.False);
            Assert.That(_contextManager.CloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void ShellClosing_JobRunning_StayOpen_DoesNotClose()
        {
            _jobs.Running = true;
            _prompt.Choice = ShutdownChoice.StayOpen;

            _view.RaiseClosing();

            Assert.That(_view.CloseShellCalls, Is.EqualTo(0));
            Assert.That(_view.Errors, Is.Empty);
            Assert.That(_contextManager.CloseCalls, Is.EqualTo(0));
        }

        [Test]
        public void ShellClosing_PauseFails_ShowsMessage_DoesNotClose()
        {
            _jobs.Running = true;
            _jobs.PauseFails = true;
            _prompt.Choice = ShutdownChoice.PauseAndExit;

            _view.RaiseClosing();

            Assert.That(_view.CloseShellCalls, Is.EqualTo(0));
            Assert.That(_view.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_UnsubscribesAllEvents()
        {
            _presenter.Dispose();

            Assert.That(_view.Listeners, Is.EqualTo(0));
            _view.RaiseNavigation(ScreenId.Settings);
            _view.RaiseClosing();
            Assert.That(_view.ShownScreens, Is.Empty);
            Assert.That(_view.CloseShellCalls, Is.EqualTo(0));

            _store.Set(new PortalSession(_contextManager.Current.CompanyId, _contextManager.Current.TaxCode,
                new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
            Assert.That(_view.LastSession, Is.Null);
        }

        private sealed class FakeLoginCoordinator : IPortalLoginCoordinator
        {
            public int Calls;
            public PortalLoginRequest LastRequest;
            public Func<PortalLoginRequest, PortalLoginResult> Handler =
                r => PortalLoginResult.Failure(PortalLoginOutcome.Cancelled, null);

            public Task<PortalLoginResult> LoginAsync(PortalLoginRequest request, IWin32Window owner, CancellationToken cancellationToken)
            {
                Calls++;
                LastRequest = request;
                return Task.FromResult(Handler(request));
            }
        }

        private sealed class FakeJobs : IActiveJobService
        {
            public bool Running;
            public bool PauseFails;

            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Running);
            }

            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                Running = false;
                return Task.FromResult(true);
            }

            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                if (PauseFails) throw new InvalidOperationException("checkpoint");
                Running = false;
                return Task.FromResult(true);
            }

            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                Running = false;
                return Task.FromResult(true);
            }
        }

        private sealed class FakePrompt : IShutdownPrompt
        {
            public ShutdownChoice Choice = ShutdownChoice.StayOpen;

            public ShutdownChoice Ask(string companyName)
            {
                return Choice;
            }
        }

        private sealed class RecordingErrors : IUserErrorPresenter
        {
            public List<UserFacingError> Shown = new List<UserFacingError>();

            public void Show(UserFacingError error)
            {
                Shown.Add(error);
            }
        }

        private sealed class FakeShellView : IMainShellView
        {
            public List<Control> ShownScreens = new List<Control>();
            public List<string> Errors = new List<string>();
            public List<OperationViewState> Operations = new List<OperationViewState>();
            public ScreenId Selected;
            public PortalSessionViewState LastSession;
            public int CloseShellCalls;

            public IWin32Window OwnerWindow { get { return null; } }

            public event EventHandler<NavigationRequestedEventArgs> NavigationRequested;
            public event EventHandler CompanySelectorRequested;
            public event EventHandler PortalLoginRequested;
            public event EventHandler ShellClosingRequested;

            public int Listeners
            {
                get
                {
                    return Count(NavigationRequested) + Count(CompanySelectorRequested)
                        + Count(PortalLoginRequested) + Count(ShellClosingRequested);
                }
            }

            public ShellModeViewState LastMode;
            public void ApplyMode(ShellModeViewState state) { LastMode = state; }
            public void ShowCompany(CompanyHeaderViewState state) { }
            public void ShowPortalSession(PortalSessionViewState state) { LastSession = state; }
            public void ShowOperation(OperationViewState state) { Operations.Add(state); }
            public void ShowScreen(Control screen) { ShownScreens.Add(screen); }
            public void SelectNavigation(ScreenId screen) { Selected = screen; }
            public void ShowError(string title, string message) { Errors.Add(message); }
            public void CloseShell() { CloseShellCalls++; }

            public void RaiseNavigation(ScreenId id)
            {
                var h = NavigationRequested;
                if (h != null) h(this, new NavigationRequestedEventArgs(id));
            }

            public void RaiseCompanySelector() { Raise(CompanySelectorRequested); }
            public void RaisePortalLogin() { Raise(PortalLoginRequested); }
            public void RaiseClosing() { Raise(ShellClosingRequested); }

            private void Raise(EventHandler handler)
            {
                if (handler != null) handler(this, EventArgs.Empty);
            }

            private static int Count(Delegate d)
            {
                return d == null ? 0 : d.GetInvocationList().Length;
            }
        }
    }
}
