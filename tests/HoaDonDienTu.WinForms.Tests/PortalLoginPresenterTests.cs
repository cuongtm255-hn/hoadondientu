using System;
using System.Linq;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.WinForms.Login;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class PortalLoginPresenterTests
    {
        private const string Mst = "0101234567";
        private HoaDonDienTu.Application.Companies.CompanyContext _company;
        private FakeContextAccessor _context;
        private FakeLoginView _view;
        private FakeBrowser _browser;
        private FakeAuth _auth;
        private InMemoryPortalSessionStore _store;
        private RecordingLogger _logger;
        private PortalLoginPresenter _presenter;
        private int _storeChanges;

        [SetUp]
        public void SetUp()
        {
            _storeChanges = 0;
            _company = FakeContextAccessor.Context(Mst);
            _context = new FakeContextAccessor(_company);
            _view = new FakeLoginView();
            _browser = new FakeBrowser();
            _auth = new FakeAuth { Handler = FakeAuth.SuccessFor };
            _store = new InMemoryPortalSessionStore();
            _store.SessionChanged += (s, e) => _storeChanges++;
            _logger = new RecordingLogger();
            _presenter = Create(_company.CompanyId, Mst);
            _presenter.Initialize();
        }

        private PortalLoginPresenter Create(HoaDonDienTu.Domain.Companies.CompanyId id, string tax)
        {
            var request = new PortalLoginRequest(id, HoaDonDienTu.Domain.Companies.TaxCode.Parse(tax), LoginReason.Manual, ScreenId.Dashboard, null);
            return new PortalLoginPresenter(_view, request, _context, _browser, _auth, _store, _logger);
        }

        [Test]
        public void Initialize_ShowsCompanyAndExpectedTaxCode()
        {
            Assert.That(_view.Company, Is.EqualTo("CÔNG TY MẪU"));
            Assert.That(_view.TaxCode, Is.EqualTo(Mst));
        }

        [Test]
        public void NoContext_DoesNotOpenBrowser_AndFinishesNotAllowed()
        {
            _context.Current = null;

            _presenter.StartAsync().GetAwaiter().GetResult();

            Assert.That(_browser.InitializeCalls, Is.EqualTo(0));
            Assert.That(_presenter.Result.Outcome, Is.EqualTo(PortalLoginOutcome.NotAllowed));
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void ContextOfOtherTaxCode_DoesNotOpenBrowser()
        {
            _context.Current = FakeContextAccessor.Context("0109999999");

            _presenter.StartAsync().GetAwaiter().GetResult();

            Assert.That(_browser.InitializeCalls, Is.EqualTo(0));
            Assert.That(_presenter.Result.Outcome, Is.EqualTo(PortalLoginOutcome.NotAllowed));
        }

        [Test]
        public void Start_WalksStatesToWaitingForUser()
        {
            _view.RaiseShown();

            var states = _view.States.Select(s => s.State).ToArray();
            Assert.That(states, Is.EqualTo(new[]
            {
                PortalLoginState.InitializingBrowser,
                PortalLoginState.ClearingPreviousSession,
                PortalLoginState.LoadingPortalLogin,
                PortalLoginState.LoadingCaptcha,
                PortalLoginState.WaitingForUser
            }));
            Assert.That(_view.HostCalls, Is.EqualTo(1));
            Assert.That(_browser.ClearCalls, Is.EqualTo(1));
            Assert.That(_storeChanges, Is.EqualTo(0), "Clear trên store rỗng không phát sự kiện");
            Assert.That(_presenter.Result, Is.Null);
        }

        [Test]
        public void CorrectTaxCode_Succeeds_StoresSession_ClosesView()
        {
            _view.RaiseShown();

            _browser.RaiseCandidate();

            Assert.That(_presenter.Result.Succeeded, Is.True);
            Assert.That(_store.HasActiveSession, Is.True);
            Assert.That(_store.GetSnapshot().TaxCode.Value, Is.EqualTo(Mst));
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
            Assert.That(_presenter.State, Is.EqualTo(PortalLoginState.Succeeded));
        }

        [Test]
        public void WrongTaxCode_ClearsBrowserAndStore_ShowsMismatch_NoSession()
        {
            _auth.Handler = (c, m) => PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, null);
            _view.RaiseShown();
            var clearBefore = _browser.ClearCalls;

            _browser.RaiseCandidate();

            Assert.That(_browser.ClearCalls, Is.EqualTo(clearBefore + 1));
            Assert.That(_store.HasActiveSession, Is.False);
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.TaxCodeMismatch));
            Assert.That(_view.Last.CanRetry, Is.True);
            Assert.That(_view.Last.Message, Does.Contain(Mst));
            Assert.That(_presenter.Result, Is.Null);
            Assert.That(_view.CloseCalls, Is.EqualTo(0));
        }

        private const string Password = "P@ss-w0rd-\"bí mật\"";

        [Test]
        public void Start_ShowsTheCaptchaAndEnablesTheForm()
        {
            _view.RaiseShown();

            Assert.That(_browser.CaptchaCalls, Is.EqualTo(1));
            Assert.That(_view.CaptchaSvg, Is.EqualTo("<svg>1</svg>"));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
            Assert.That(_view.Last.CanSubmit, Is.True);
        }

        [Test]
        public void CaptchaFails_ShowsPortalUnavailable_RetryFetchesAgain()
        {
            _browser.CaptchaException = new InvalidOperationException("offline");

            _view.RaiseShown();

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.PortalUnavailable));
            Assert.That(_view.Last.CanSubmit, Is.False);
            Assert.That(_view.Last.CanRetry, Is.True);

            _browser.CaptchaException = null;
            _view.RaiseRetry();
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
            Assert.That(_view.Last.CanSubmit, Is.True);
        }

        [Test]
        public void Submit_SendsTheSelectedTaxCodeAsUsername_AndSucceeds()
        {
            _view.RaiseShown();

            _view.RaiseSubmit(Password, " AbC12 ");

            Assert.That(_browser.LastUserSent, Is.EqualTo(Mst), "Tên đăng nhập là MST đang chọn, người dùng không gõ");
            Assert.That(_browser.LastPasswordSent, Is.EqualTo(Password));
            Assert.That(_browser.LastCaptchaTextSent, Is.EqualTo("AbC12"));
            Assert.That(_browser.LastCaptchaKeySent, Is.EqualTo("key-1"));
            Assert.That(_view.SecretsCleared, Is.EqualTo(1), "Ô mật khẩu xóa ngay khi gửi");
            Assert.That(_presenter.Result.Succeeded, Is.True);
            Assert.That(_store.GetSnapshot().TaxCode.Value, Is.EqualTo(Mst));
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
            Assert.That(_auth.Calls, Is.EqualTo(1), "Sự kiện của trình duyệt và kết quả gửi chỉ xác minh một lần");
        }

        [Test]
        public void Submit_AcceptedWithoutBrowserEvent_StillVerifies()
        {
            _browser.RaiseCandidateOnAccept = false;
            _view.RaiseShown();

            _view.RaiseSubmit(Password, "abc");

            Assert.That(_presenter.Result.Succeeded, Is.True);
            Assert.That(_auth.Calls, Is.EqualTo(1));
        }

        [Test]
        public void Submit_EmptyPasswordOrCaptcha_IsNotSent()
        {
            _view.RaiseShown();

            _view.RaiseSubmit(string.Empty, "abc");
            _view.RaiseSubmit(Password, "  ");

            Assert.That(_browser.SubmitCalls, Is.EqualTo(0));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
            Assert.That(_view.Last.IsError, Is.True);
        }

        [Test]
        public void Submit_Rejected_FetchesANewCaptcha_AndAsksAgain()
        {
            _browser.NextOutcome = LoginSubmitOutcome.Rejected;
            _view.RaiseShown();

            _view.RaiseSubmit(Password, "abc");

            Assert.That(_browser.CaptchaCalls, Is.EqualTo(2), "CAPTCHA chỉ dùng một lần");
            Assert.That(_view.CaptchaSvg, Is.EqualTo("<svg>2</svg>"));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
            Assert.That(_view.Last.IsError, Is.True);
            Assert.That(_view.Last.CanSubmit, Is.True);
            Assert.That(_presenter.Result, Is.Null);
            Assert.That(_auth.Calls, Is.EqualTo(0));

            _browser.NextOutcome = LoginSubmitOutcome.Accepted;
            _view.RaiseSubmit(Password, "xyz");
            Assert.That(_presenter.Result.Succeeded, Is.True);
        }

        [Test]
        public void Submit_PortalUnavailable_FetchesANewCaptcha_WithAnotherMessage()
        {
            _browser.NextOutcome = LoginSubmitOutcome.Unavailable;
            _view.RaiseShown();

            _view.RaiseSubmit(Password, "abc");

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
            Assert.That(_view.Last.Message, Does.Contain("không phản hồi"));
        }

        [Test]
        public void Submit_BrowserThrows_ShowsRetryableError_WithoutThePassword()
        {
            _browser.SubmitException = new InvalidOperationException("boom");
            _view.RaiseShown();

            _view.RaiseSubmit(Password, "abc");

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.InvalidSession));
            Assert.That(_view.Last.CanRetry, Is.True);
            var seen = string.Join("|", _view.States.Select(s => s.Message + s.StepText)) + "|" + string.Join("|", _logger.Lines);
            Assert.That(seen, Does.Not.Contain("bí mật"), "Mật khẩu không được lọt vào thông báo hay nhật ký của presenter");
        }

        [Test]
        public void Submit_WhileNotWaiting_IsIgnored()
        {
            _view.RaiseSubmit(Password, "abc");

            Assert.That(_browser.SubmitCalls, Is.EqualTo(0));
        }

        [Test]
        public void Submit_AccountOfAnotherTaxCode_ShowsMismatch_AndRetryFetchesANewCaptcha()
        {
            var first = true;
            _auth.Handler = (c, m) => first
                ? PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, "0109999999")
                : FakeAuth.SuccessFor(c, m);
            _view.RaiseShown();

            _view.RaiseSubmit(Password, "abc");

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.TaxCodeMismatch));
            Assert.That(_store.HasActiveSession, Is.False);

            first = false;
            _view.RaiseRetry();
            Assert.That(_browser.CaptchaCalls, Is.EqualTo(2));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
        }

        [Test]
        public void RefreshCaptcha_FetchesANewOne()
        {
            _view.RaiseShown();

            _view.RaiseRefreshCaptcha();

            Assert.That(_browser.CaptchaCalls, Is.EqualTo(2));
            Assert.That(_view.CaptchaSvg, Is.EqualTo("<svg>2</svg>"));
        }

        [Test]
        public void ManualLogin_SwitchesToTheRealPage_AndStillAcceptsTheBrowserLogin()
        {
            _view.RaiseShown();

            _view.RaiseManual();

            Assert.That(_browser.ManualCalls, Is.EqualTo(1));
            Assert.That(_view.ManualModeCalls, Is.EqualTo(1));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
            Assert.That(_view.Last.CanSubmit, Is.False, "Chế độ thủ công không dùng form riêng");

            _view.RaiseSubmit(Password, "abc");
            Assert.That(_browser.SubmitCalls, Is.EqualTo(0));

            _browser.RaiseCandidate();
            Assert.That(_presenter.Result.Succeeded, Is.True);
        }

        [Test]
        public void SimulationNotice_IsShownWhenTheBrowserIsASimulation()
        {
            _browser.SimulationNotice = "MÔ PHỎNG";
            var request = new PortalLoginRequest(_company.CompanyId, HoaDonDienTu.Domain.Companies.TaxCode.Parse(Mst), LoginReason.Manual, ScreenId.Dashboard, null);
            using (var presenter = new PortalLoginPresenter(_view, request, _context, _browser, _auth, _store, _logger))
            {
                presenter.Initialize();
            }

            Assert.That(_view.Simulation, Is.EqualTo("MÔ PHỎNG"));
        }

        [Test]
        public void WrongTaxCode_NamesTheAccountLoggedIn_AndReturnsTheBrowserToTheLoginPage()
        {
            _auth.Handler = (c, m) => PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, "0109999999");
            _view.RaiseShown();
            var navigateBefore = _browser.NavigateCalls;

            _browser.RaiseCandidate();

            Assert.That(_view.Last.Message, Does.Contain("0109999999"));
            Assert.That(_browser.NavigateCalls, Is.EqualTo(navigateBefore + 1), "Không để trang hiện giao diện đã đăng nhập của tài khoản sai");
        }

        [Test]
        public void AuthSaysSuccess_ButSessionBelongsToOtherTaxCode_TreatedAsMismatch()
        {
            _auth.Handler = (c, m) => PortalLoginResult.Success(new PortalSession(
                c.CompanyId, HoaDonDienTu.Domain.Companies.TaxCode.Parse("0109999999"), m, DateTime.UtcNow));
            _view.RaiseShown();

            _browser.RaiseCandidate();

            Assert.That(_store.HasActiveSession, Is.False);
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.TaxCodeMismatch));
            Assert.That(_presenter.Result, Is.Null);
        }

        [TestCase(PortalLoginOutcome.InvalidSession, PortalLoginState.InvalidSession)]
        [TestCase(PortalLoginOutcome.PortalUnavailable, PortalLoginState.PortalUnavailable)]
        public void AuthFailure_ShowsRetryableError(PortalLoginOutcome outcome, PortalLoginState expected)
        {
            _auth.Handler = (c, m) => PortalLoginResult.Failure(outcome, null);
            _view.RaiseShown();

            _browser.RaiseCandidate();

            Assert.That(_view.LastState, Is.EqualTo(expected));
            Assert.That(_view.Last.CanRetry, Is.True);
            Assert.That(_view.Last.IsError, Is.True);
            Assert.That(_store.HasActiveSession, Is.False);
        }

        [Test]
        public void CaptureThrows_ShowsInvalidSession_AndRetryWorks()
        {
            _browser.CaptureException = new InvalidOperationException("token=" + Secrets.Token);
            _view.RaiseShown();

            _browser.RaiseCandidate();

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.InvalidSession));
            Assert.That(_view.Last.CanRetry, Is.True);

            _browser.CaptureException = null;
            _view.RaiseRetry();
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));

            _browser.RaiseCandidate();
            Assert.That(_presenter.Result.Succeeded, Is.True);
        }

        [Test]
        public void RetryAfterMismatch_ReloadsLoginWithoutReinitializingBrowser()
        {
            var first = true;
            _auth.Handler = (c, m) => first
                ? PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, null)
                : FakeAuth.SuccessFor(c, m);
            _view.RaiseShown();
            _browser.RaiseCandidate();
            first = false;

            _view.RaiseRetry();

            Assert.That(_browser.InitializeCalls, Is.EqualTo(1));
            Assert.That(_browser.NavigateCalls, Is.EqualTo(3), "mở lần đầu, về trang đăng nhập sau khi sai MST, rồi tải lại khi thử lại");
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
        }

        [Test]
        public void ClosingTheLoginDialogOnThePage_CancelsAndClosesTheForm()
        {
            _presenter.ClosedDialogGrace = TimeSpan.Zero;
            _view.RaiseShown();

            _browser.RaiseDialogClosed();

            Assert.That(_presenter.Result.Outcome, Is.EqualTo(PortalLoginOutcome.Cancelled));
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
            Assert.That(_store.HasActiveSession, Is.False);
        }

        [Test]
        public void TheDialogClosingBecauseLoginSucceeded_IsNotACancel()
        {
            _presenter.ClosedDialogGrace = TimeSpan.Zero;
            _view.RaiseShown();

            _browser.RaiseCandidate();
            _browser.RaiseDialogClosed();

            Assert.That(_presenter.Result.Succeeded, Is.True);
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void ClosingTheDialog_WhileTheLoginIsBeingChecked_IsIgnored()
        {
            _presenter.ClosedDialogGrace = TimeSpan.Zero;
            _auth.Handler = (c, m) => { _browser.RaiseDialogClosed(); return FakeAuth.SuccessFor(c, m); };
            _view.RaiseShown();

            _browser.RaiseCandidate();

            Assert.That(_presenter.Result.Succeeded, Is.True);
        }

        [Test]
        public void ClosingTheDialog_BeforeTheFormIsReady_IsIgnored()
        {
            _presenter.ClosedDialogGrace = TimeSpan.Zero;

            _browser.RaiseDialogClosed();

            Assert.That(_presenter.Result, Is.Null);
            Assert.That(_view.CloseCalls, Is.EqualTo(0));
        }

        [Test]
        public void CandidateIgnored_WhenNotWaitingForUser()
        {
            _browser.RaiseCandidate();

            Assert.That(_auth.Calls, Is.EqualTo(0));
            Assert.That(_browser.CaptureCalls, Is.EqualTo(0));
        }

        [Test]
        public void BrowserInitFails_ShowsBrowserUnavailable_RetryReinitializes()
        {
            _browser.InitializeException = new InvalidOperationException("no webview");

            _view.RaiseShown();

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.BrowserUnavailable));
            Assert.That(_view.Last.CanRetry, Is.True);
            Assert.That(_browser.NavigateCalls, Is.EqualTo(0));

            _browser.InitializeException = null;
            _view.RaiseRetry();
            Assert.That(_browser.InitializeCalls, Is.EqualTo(2));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
        }

        [Test]
        public void NavigateFails_ShowsPortalUnavailable_RetryDoesNotReinitialize()
        {
            _browser.NavigateException = new InvalidOperationException("offline");

            _view.RaiseShown();

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.PortalUnavailable));

            _browser.NavigateException = null;
            _view.RaiseRetry();
            Assert.That(_browser.InitializeCalls, Is.EqualTo(1));
            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.WaitingForUser));
        }

        [Test]
        public void BrowserFailedEvent_ShowsBrowserUnavailable()
        {
            _view.RaiseShown();

            _browser.RaiseFailure();

            Assert.That(_view.LastState, Is.EqualTo(PortalLoginState.BrowserUnavailable));
        }

        [Test]
        public void Cancel_ClearsBrowserAndStore_FinishesCancelled()
        {
            _view.RaiseShown();
            var clearBefore = _browser.ClearCalls;

            _view.RaiseCancel();

            Assert.That(_presenter.Result.Outcome, Is.EqualTo(PortalLoginOutcome.Cancelled));
            Assert.That(_browser.ClearCalls, Is.EqualTo(clearBefore + 1));
            Assert.That(_store.HasActiveSession, Is.False);
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_WhileVerifying_LateSuccessDoesNotLeaveASession()
        {
            _auth.Pending = new System.Threading.Tasks.TaskCompletionSource<PortalLoginResult>();
            _view.RaiseShown();
            _browser.RaiseCandidate();
            Assert.That(_presenter.State, Is.EqualTo(PortalLoginState.VerifyingPortalSession));

            _view.RaiseCancel();
            _auth.Pending.SetResult(FakeAuth.SuccessFor(_company, new PortalSessionMaterial(null, null, Secrets.Token)));

            Assert.That(_presenter.Result.Outcome, Is.EqualTo(PortalLoginOutcome.Cancelled));
            Assert.That(_store.HasActiveSession, Is.False, "Phiên không được lưu sau khi người dùng đã hủy");
        }

        [Test]
        public void Cancel_AfterFinish_Ignored()
        {
            _view.RaiseShown();
            _browser.RaiseCandidate();

            _view.RaiseCancel();

            Assert.That(_presenter.Result.Succeeded, Is.True);
            Assert.That(_view.CloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void ViewStateAndLog_NeverContainToken()
        {
            _browser.CaptureException = new InvalidOperationException("token=" + Secrets.Token);
            _view.RaiseShown();
            _browser.RaiseCandidate();
            _browser.CaptureException = null;
            _view.RaiseRetry();
            _browser.RaiseCandidate();

            var viewText = string.Join("|", _view.States.Select(s => s.Message + s.StepText));
            Assert.That(viewText, Does.Not.Contain(Secrets.Token));
            Assert.That(string.Join("|", _logger.Lines), Does.Not.Contain("password"));
            Assert.That(_store.GetSnapshot().ToString(), Does.Not.Contain(Secrets.Token));
        }

        [Test]
        public void Dispose_UnsubscribesEventsAndDisposesBrowser()
        {
            _presenter.Dispose();

            Assert.That(_view.Listeners, Is.EqualTo(0));
            Assert.That(_browser.CandidateListeners, Is.EqualTo(0));
            Assert.That(_browser.Disposed, Is.True);

            _browser.RaiseCandidate();
            Assert.That(_auth.Calls, Is.EqualTo(0));
        }
    }
}
