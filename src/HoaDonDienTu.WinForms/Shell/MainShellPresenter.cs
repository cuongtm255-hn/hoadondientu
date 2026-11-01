using System;
using HoaDonDienTu.Application.Navigation;

namespace HoaDonDienTu.WinForms.Shell
{
    public sealed class MainShellPresenter : IDisposable
    {
        private readonly IMainShellView _view;
        private bool _initialized;

        public MainShellPresenter(IMainShellView view)
        {
            _view = view;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.NavigationRequested += OnNavigationRequested;
            _view.CompanySelectorRequested += OnCompanySelectorRequested;
            _view.PortalLoginRequested += OnPortalLoginRequested;
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _view.NavigationRequested -= OnNavigationRequested;
            _view.CompanySelectorRequested -= OnCompanySelectorRequested;
            _view.PortalLoginRequested -= OnPortalLoginRequested;
        }

        public void ShowInitializing()
        {
            _view.ShowScreen(new PlaceholderScreenControl("Đang khởi tạo...", "Đang xác định chế độ làm việc"));
        }

        public void Show(ScreenId target)
        {
            _view.SelectNavigation(target);
            _view.ShowScreen(new PlaceholderScreenControl(TitleOf(target), "SCR · chưa implement"));
        }

        private void OnNavigationRequested(object sender, NavigationRequestedEventArgs e)
        {
            Show(e.Target);
        }

        private void OnCompanySelectorRequested(object sender, EventArgs e)
        {
            OnNavigationRequested(sender, new NavigationRequestedEventArgs(ScreenId.CompanyList));
        }

        private void OnPortalLoginRequested(object sender, EventArgs e)
        {
            _view.ShowError("Đăng nhập cổng Thuế", "PortalLoginForm chưa implement (T01-08).");
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
