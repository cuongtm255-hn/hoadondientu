using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.WinForms.Login;

namespace HoaDonDienTu.WinForms.Shell
{
    public sealed class MainShellPresenter : IDisposable, IShellActions
    {
        private readonly IMainShellView _view;
        private readonly IPortalLoginCoordinator _loginCoordinator;
        private readonly ICompanyContextManager _context;
        private ShellModeViewState _baseMode = new ShellModeViewState();
        private readonly IPortalSessionStore _sessionStore;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly ShutdownCoordinator _shutdown;
        private readonly INavigationService _navigation;
        private bool _initialized;

        public MainShellPresenter(
            IMainShellView view,
            IPortalLoginCoordinator loginCoordinator,
            ICompanyContextManager context,
            IPortalSessionStore sessionStore,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            ShutdownCoordinator shutdown,
            INavigationService navigation)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (loginCoordinator == null) throw new ArgumentNullException("loginCoordinator");
            if (context == null) throw new ArgumentNullException("context");
            if (sessionStore == null) throw new ArgumentNullException("sessionStore");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            if (shutdown == null) throw new ArgumentNullException("shutdown");
            if (navigation == null) throw new ArgumentNullException("navigation");
            _view = view;
            _loginCoordinator = loginCoordinator;
            _context = context;
            _sessionStore = sessionStore;
            _runner = runner;
            _errors = errors;
            _shutdown = shutdown;
            _navigation = navigation;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.NavigationRequested += OnNavigationRequested;
            _view.CompanySelectorRequested += OnCompanySelectorRequested;
            _view.PortalLoginRequested += OnPortalLoginRequested;
            _view.ShellClosingRequested += OnShellClosingRequested;
            _sessionStore.SessionChanged += OnSessionChanged;
            _context.ContextChanged += OnContextChanged;
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.NavigationRequested -= OnNavigationRequested;
            _view.CompanySelectorRequested -= OnCompanySelectorRequested;
            _view.PortalLoginRequested -= OnPortalLoginRequested;
            _view.ShellClosingRequested -= OnShellClosingRequested;
            _sessionStore.SessionChanged -= OnSessionChanged;
            _context.ContextChanged -= OnContextChanged;
        }

        /// <summary>Điều hướng không qua event của view (dùng khi khởi động).</summary>
        public Task<NavigationResult> ShowAsync(ScreenId target)
        {
            return ShowAsync(target, null);
        }

        public Task<NavigationResult> ShowAsync(ScreenId target, object parameter)
        {
            return _navigation.NavigateAsync(target, parameter, CancellationToken.None);
        }

        /// <summary>
        /// Đặt khả năng theo chế độ (Standard/Service/ReadOnly). Việc đăng nhập và tải còn cần đã mở doanh nghiệp nên
        /// presenter tự khóa chúng khi chưa có doanh nghiệp và mở lại khi context thay đổi.
        /// </summary>
        public void SetMode(ShellModeViewState mode)
        {
            _baseMode = mode;
            ApplyModeWithContext();
            RefreshCompany();
        }

        public void RefreshCompany()
        {
            if (_context.HasCurrent)
            {
                _view.ShowCompany(new CompanyHeaderViewState
                {
                    CompanyName = _context.Current.DisplayName,
                    TaxCode = _context.Current.TaxCode.Value
                });
            }
            else
            {
                _view.ShowCompany(new CompanyHeaderViewState { CompanyName = "Chưa chọn doanh nghiệp", TaxCode = string.Empty });
            }
        }

        private void ApplyModeWithContext()
        {
            var has = _context.HasCurrent;
            _view.ApplyMode(new ShellModeViewState
            {
                ShowCompanySelector = _baseMode.ShowCompanySelector,
                ShowCompanyMenu = _baseMode.ShowCompanyMenu,
                CanAddCompany = _baseMode.CanAddCompany,
                LicenseLabel = _baseMode.LicenseLabel,
                CanPortalLogin = _baseMode.CanPortalLogin && has,
                CanDownload = _baseMode.CanDownload && has
            });
        }

        private void OnContextChanged(object sender, EventArgs e)
        {
            ApplyModeWithContext();
            RefreshCompany();
        }

        public void RefreshPortalSession()
        {
            var snapshot = _sessionStore.GetSnapshot();
            _view.ShowPortalSession(snapshot == null
                ? new PortalSessionViewState { IsLoggedIn = false, Text = "Chưa đăng nhập" }
                : new PortalSessionViewState { IsLoggedIn = true, Text = "Đã đăng nhập" });
        }

        private void OnNavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            Navigate(e.Target);
        }

        private void OnCompanySelectorRequested(object sender, EventArgs e)
        {
            Navigate(ScreenId.CompanyList);
        }

        private async void Navigate(ScreenId target)
        {
            await _runner.RunAsync(
                "Điều hướng",
                async token =>
                {
                    var result = await _navigation.NavigateAsync(target, null, token);
                    if (!result.Succeeded && !string.IsNullOrEmpty(result.Reason))
                    {
                        _view.ShowError("Điều hướng", result.Reason);
                    }
                },
                state => { if (state.Error != null) _errors.Show(state.Error); });
        }

        private void OnSessionChanged(object sender, EventArgs e)
        {
            RefreshPortalSession();
        }

        private async void OnShellClosingRequested(object sender, EventArgs e)
        {
            var canClose = false;
            await _runner.RunAsync(
                "Đóng ứng dụng",
                async token =>
                {
                    var result = await _shutdown.TryShutdownAsync(token);
                    canClose = result.CanClose;
                    if (!result.CanClose && !string.IsNullOrEmpty(result.Message))
                    {
                        _view.ShowError("Đóng ứng dụng", result.Message);
                    }
                },
                state =>
                {
                    _view.ShowOperation(new OperationViewState
                    {
                        Text = state.IsBusy ? "Đang đóng ứng dụng..." : "Sẵn sàng",
                        IsBusy = state.IsBusy
                    });
                    if (state.Error != null) _errors.Show(state.Error);
                });

            if (canClose) _view.CloseShell();
        }

        private async void OnPortalLoginRequested(object sender, EventArgs e)
        {
            await LoginAsync(LoginReason.Manual);
        }

        /// <summary>Mở đăng nhập cổng Thuế cho doanh nghiệp đang mở; trả về sau khi form đăng nhập đã đóng.</summary>
        public async Task LoginAsync(LoginReason reason)
        {
            if (!_context.HasCurrent)
            {
                _view.ShowError("Đăng nhập cổng Thuế", "Hãy chọn doanh nghiệp trước khi đăng nhập.");
                return;
            }

            var company = _context.Current;
            var request = new PortalLoginRequest(
                company.CompanyId, company.TaxCode, reason, _navigation.CurrentScreen, null);

            await _runner.RunAsync(
                "Đăng nhập cổng Thuế",
                async token =>
                {
                    var result = await _loginCoordinator.LoginAsync(request, _view.OwnerWindow, token);
                    if (!result.Succeeded && result.Outcome != PortalLoginOutcome.Cancelled && !string.IsNullOrEmpty(result.Message))
                    {
                        _view.ShowError("Đăng nhập cổng Thuế", result.Message);
                    }
                },
                state =>
                {
                    _view.ShowOperation(new OperationViewState
                    {
                        Text = state.IsBusy ? state.OperationName + "..." : "Sẵn sàng",
                        IsBusy = state.IsBusy
                    });
                    if (state.Error != null) _errors.Show(state.Error);
                });

            RefreshPortalSession();
        }
    }
}
