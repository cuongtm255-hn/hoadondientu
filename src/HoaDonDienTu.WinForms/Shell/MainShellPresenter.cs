using System;
using System.Threading;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Login;

namespace HoaDonDienTu.WinForms.Shell
{
    public sealed class MainShellPresenter : IDisposable
    {
        private readonly IMainShellView _view;
        private readonly IPortalLoginCoordinator _loginCoordinator;
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalSessionStore _sessionStore;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private ScreenId _currentScreen = ScreenId.Initializing;
        private bool _initialized;

        public MainShellPresenter(
            IMainShellView view,
            IPortalLoginCoordinator loginCoordinator,
            ICompanyContextAccessor context,
            IPortalSessionStore sessionStore)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (loginCoordinator == null) throw new ArgumentNullException("loginCoordinator");
            if (context == null) throw new ArgumentNullException("context");
            if (sessionStore == null) throw new ArgumentNullException("sessionStore");
            _view = view;
            _loginCoordinator = loginCoordinator;
            _context = context;
            _sessionStore = sessionStore;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.NavigationRequested += OnNavigationRequested;
            _view.CompanySelectorRequested += OnCompanySelectorRequested;
            _view.PortalLoginRequested += OnPortalLoginRequested;
            _sessionStore.SessionChanged += OnSessionChanged;
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _cancellation.Cancel();
            _view.NavigationRequested -= OnNavigationRequested;
            _view.CompanySelectorRequested -= OnCompanySelectorRequested;
            _view.PortalLoginRequested -= OnPortalLoginRequested;
            _sessionStore.SessionChanged -= OnSessionChanged;
        }

        public void ShowInitializing()
        {
            _view.ShowScreen(new PlaceholderScreenControl("Đang khởi tạo...", "Đang xác định chế độ làm việc"));
        }

        public void Show(ScreenId target)
        {
            _currentScreen = target;
            _view.SelectNavigation(target);
            _view.ShowScreen(new PlaceholderScreenControl(TitleOf(target), "SCR · chưa implement"));
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
            Show(e.Target);
        }

        private void OnCompanySelectorRequested(object sender, EventArgs e)
        {
            Show(ScreenId.CompanyList);
        }

        private void OnSessionChanged(object sender, EventArgs e)
        {
            RefreshPortalSession();
        }

        private async void OnPortalLoginRequested(object sender, EventArgs e)
        {
            try
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowError("Đăng nhập cổng Thuế", "Hãy chọn doanh nghiệp trước khi đăng nhập.");
                    return;
                }

                var company = _context.Current;
                var request = new PortalLoginRequest(
                    company.CompanyId, company.TaxCode, LoginReason.Manual, _currentScreen, null);

                var result = await _loginCoordinator.LoginAsync(request, _view.OwnerWindow, _cancellation.Token);
                if (!result.Succeeded && result.Outcome != PortalLoginOutcome.Cancelled && !string.IsNullOrEmpty(result.Message))
                {
                    _view.ShowError("Đăng nhập cổng Thuế", result.Message);
                }

                RefreshPortalSession();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                _view.ShowError("Đăng nhập cổng Thuế", "Không thể đăng nhập. Chi tiết đã được ghi vào log.");
            }
        }

        private static string TitleOf(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Dashboard: return "Tổng quan";
                case ScreenId.Download: return "Tải hóa đơn";
                case ScreenId.InvoiceArchive: return "Kho hóa đơn";
                case ScreenId.DownloadHistory: return "Lịch sử tải";
                case ScreenId.CompanyList: return "Doanh nghiệp";
                case ScreenId.Settings: return "Cài đặt";
                default: return id.ToString();
            }
        }
    }
}
