using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Login;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Token dùng trong test; không được xuất hiện ở view state hay log.</summary>
    internal static class Secrets
    {
        public const string Token = "SECRET-TOKEN-123";
    }

    internal sealed class RecordingLogger : IApplicationLogger
    {
        public List<string> Lines = new List<string>();

        public void Info(string message) { Lines.Add(message); }
        public void Warning(string message) { Lines.Add(message); }
        public void Error(string message, Exception exception)
        {
            Lines.Add(message + (exception == null ? string.Empty : " | " + exception.Message));
        }
    }

    internal sealed class FakeContextAccessor : ICompanyContextAccessor
    {
        public FakeContextAccessor(CompanyContext current)
        {
            Current = current;
        }

        public bool HasCurrent { get { return Current != null; } }
        public CompanyContext Current { get; set; }

        public static CompanyContext Context(string taxCode)
        {
            return new CompanyContext(CompanyId.NewId(), TaxCode.Parse(taxCode), "CÔNG TY MẪU", @"C:\data\" + taxCode, null);
        }
    }

    internal sealed class FakeContextManager : ICompanyContextManager
    {
        public FakeContextManager(CompanyContext current)
        {
            Current = current;
        }

        public bool HasCurrent { get { return Current != null; } }
        public CompanyContext Current { get; set; }
        public int CloseCalls { get; private set; }
        public event EventHandler ContextChanged { add { } remove { } }

        public Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void CloseCurrent()
        {
            CloseCalls++;
            Current = null;
        }
    }

    internal sealed class FakeEntitlement : IEntitlementService
    {
        public bool Allowed = true;

        public AppMode CurrentMode { get { return AppMode.Standard; } }
        public bool IsAllowed(AppFeature feature) { return Allowed; }
        public void Demand(AppFeature feature) { if (!Allowed) throw new EntitlementDeniedException(feature); }
        public event EventHandler ModeChanged { add { } remove { } }
    }

    internal sealed class FakeAuth : IPortalAuthenticationService
    {
        public int Calls;
        public Func<CompanyContext, PortalSessionMaterial, PortalLoginResult> Handler;
        public TaskCompletionSource<PortalLoginResult> Pending;

        public Task<PortalLoginResult> VerifyAsync(CompanyContext company, PortalSessionMaterial candidate, CancellationToken cancellationToken)
        {
            Calls++;
            if (Pending != null) return Pending.Task;
            return Task.FromResult(Handler(company, candidate));
        }

        public static PortalLoginResult SuccessFor(CompanyContext company, PortalSessionMaterial material)
        {
            return PortalLoginResult.Success(new PortalSession(company.CompanyId, company.TaxCode, material, DateTime.UtcNow));
        }
    }

    /// <summary>Mọi thao tác hoàn tất đồng bộ để test không cần message loop.</summary>
    internal sealed class FakeBrowser : IPortalLoginBrowser
    {
        private EventHandler<LoginCandidateEventArgs> _candidate;
        private EventHandler<BrowserFailureEventArgs> _failed;

        public Control View { get; private set; } = new Control();
        public int InitializeCalls;
        public int ClearCalls;
        public int NavigateCalls;
        public int CaptureCalls;
        public bool Disposed;
        public Exception InitializeException;
        public Exception NavigateException;
        public Exception CaptureException;

        public event EventHandler<LoginCandidateEventArgs> LoginCandidateDetected
        {
            add { _candidate += value; }
            remove { _candidate -= value; }
        }

        public event EventHandler<BrowserNavigationEventArgs> NavigationChanged { add { } remove { } }

        public event EventHandler<BrowserFailureEventArgs> BrowserFailed
        {
            add { _failed += value; }
            remove { _failed -= value; }
        }

        public int CandidateListeners { get { return _candidate == null ? 0 : _candidate.GetInvocationList().Length; } }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCalls++;
            if (InitializeException != null) throw InitializeException;
            return Task.FromResult(true);
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken)
        {
            ClearCalls++;
            return Task.FromResult(true);
        }

        public Task NavigateToLoginAsync(CancellationToken cancellationToken)
        {
            NavigateCalls++;
            if (NavigateException != null) throw NavigateException;
            return Task.FromResult(true);
        }

        public Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken)
        {
            CaptureCalls++;
            if (CaptureException != null) throw CaptureException;
            return Task.FromResult(new PortalSessionMaterial(null, null, Secrets.Token));
        }

        public void RaiseCandidate()
        {
            var handler = _candidate;
            if (handler != null) handler(this, new LoginCandidateEventArgs());
        }

        public void RaiseFailure()
        {
            var handler = _failed;
            if (handler != null) handler(this, new BrowserFailureEventArgs("boom"));
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    internal sealed class FakeLoginView : IPortalLoginView
    {
        public List<PortalLoginViewState> States = new List<PortalLoginViewState>();
        public int CloseCalls;
        public int HostCalls;
        public string Company;
        public string TaxCode;

        /// <summary>Mô phỏng người dùng thao tác trong lúc form đang mở.</summary>
        public Action<FakeLoginView> OnShowModal;

        public event EventHandler ViewShown;
        public event EventHandler RetryRequested;
        public event EventHandler ClearSessionRequested;
        public event EventHandler CancelRequested;

        public int Listeners
        {
            get
            {
                return Count(ViewShown) + Count(RetryRequested) + Count(ClearSessionRequested) + Count(CancelRequested);
            }
        }

        public PortalLoginState LastState { get { return States[States.Count - 1].State; } }
        public PortalLoginViewState Last { get { return States[States.Count - 1]; } }

        public void ShowRequest(string companyName, string expectedTaxCode)
        {
            Company = companyName;
            TaxCode = expectedTaxCode;
        }

        public void ShowState(PortalLoginViewState state) { States.Add(state); }
        public void HostBrowser(Control browserView) { HostCalls++; }
        public void CloseView() { CloseCalls++; }

        public void ShowModal(IWin32Window owner)
        {
            if (OnShowModal != null) OnShowModal(this);
        }

        public void RaiseShown() { Raise(ViewShown); }
        public void RaiseRetry() { Raise(RetryRequested); }
        public void RaiseClearSession() { Raise(ClearSessionRequested); }
        public void RaiseCancel() { Raise(CancelRequested); }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private static int Count(EventHandler handler)
        {
            return handler == null ? 0 : handler.GetInvocationList().Length;
        }
    }

    internal sealed class FakeScreen : IScreen
    {
        private readonly System.Collections.Generic.List<string> _log;

        public FakeScreen(ScreenId id, System.Collections.Generic.List<string> log)
        {
            Id = id;
            _log = log;
            View = new Control();
        }

        public ScreenId Id { get; private set; }
        public Control View { get; private set; }
        public bool CanClose = true;
        public Exception CanCloseException;
        public Task<bool> PendingCanClose;
        public bool Cancelled;
        public bool Disposed;

        public Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            if (CanCloseException != null) throw CanCloseException;
            if (PendingCanClose != null) return PendingCanClose;
            return Task.FromResult(CanClose);
        }

        public void CancelOperations()
        {
            Cancelled = true;
            _log.Add("cancel:" + Id);
        }

        public void Dispose()
        {
            Disposed = true;
            _log.Add("dispose:" + Id);
        }
    }

    internal sealed class FakeScreenFactory : IScreenFactory
    {
        public System.Collections.Generic.List<string> Log = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<FakeScreen> Created = new System.Collections.Generic.List<FakeScreen>();
        public System.Collections.Generic.HashSet<ScreenId> Failing = new System.Collections.Generic.HashSet<ScreenId>();

        public IScreen Create(ScreenId id, object parameter)
        {
            Log.Add("create:" + id);
            if (Failing.Contains(id)) throw new InvalidOperationException("cannot create " + id);
            var screen = new FakeScreen(id, Log);
            Created.Add(screen);
            return screen;
        }
    }
}
