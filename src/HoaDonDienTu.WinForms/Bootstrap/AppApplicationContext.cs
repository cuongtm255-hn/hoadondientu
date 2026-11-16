using System;
using System.Threading;
using System.Windows.Forms;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Groups;
using HoaDonDienTu.WinForms.Groups;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.WinForms.Companies;
using HoaDonDienTu.WinForms.Navigation;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms.Bootstrap
{
    /// <summary>Sở hữu vòng đời MainShellForm: hiển thị trước, khởi động sau để message loop luôn chạy.</summary>
    public sealed class AppApplicationContext : ApplicationContext
    {
        private readonly CompositionRoot _compositionRoot;
        private readonly MainShellForm _mainShell;
        private readonly NavigationService _navigation;
        private readonly MainShellPresenter _presenter;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly GroupNavigator _groups;
        private PublicServiceStubForm _publicServiceForm;
        private bool _closingFromPublicService;

        public AppApplicationContext(CompositionRoot compositionRoot)
        {
            _compositionRoot = compositionRoot;
            _mainShell = compositionRoot.CreateMainShell();
            _navigation = new NavigationService(
                compositionRoot.ScreenFactory,
                _mainShell,
                compositionRoot.Entitlement,
                compositionRoot.ContextManager,
                compositionRoot.Logger,
                compositionRoot.SessionStore);
            compositionRoot.NavigationService = _navigation;
            _presenter = new MainShellPresenter(
                _mainShell,
                compositionRoot.PortalLoginCoordinator,
                compositionRoot.ContextManager,
                compositionRoot.SessionStore,
                compositionRoot.OperationRunner,
                compositionRoot.ErrorPresenter,
                compositionRoot.ShutdownCoordinator,
                _navigation);
            _presenter.Initialize();
            compositionRoot.ShellActions = _presenter;
            compositionRoot.Entitlement.ModeChanged += OnModeChanged;

            // Đổi nhóm chức năng (DD-DVC-00): shell Hóa đơn là MainShellForm, nhóm Dịch vụ công hiện là chỗ giữ chỗ.
            _groups = new GroupNavigator(new ShellFactory(this), new HoaDonDienTu.Infrastructure.Settings.FileGroupPreferenceStore(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoaDonDienTu", "last-group.txt")));
            _mainShell.GroupSwitchRequested += (s, e) => SwitchGroup(AppGroup.PublicService);

            _mainShell.Shown += OnMainShellShown;
            _mainShell.FormClosed += OnMainShellClosed;
            MainForm = _mainShell;

        }

        private void SwitchGroup(AppGroup target)
        {
            _groups.Switch(target, reason =>
                MessageBox.Show(_mainShell, reason + Environment.NewLine + "Tiếp tục đổi nhóm?", "Đổi nhóm chức năng",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        }

        private sealed class ShellFactory : IGroupShellFactory
        {
            private readonly AppApplicationContext _owner;

            public ShellFactory(AppApplicationContext owner)
            {
                _owner = owner;
            }

            public IGroupShell Create(AppGroup group)
            {
                if (group == AppGroup.Invoice)
                {
                    return new InvoiceGroupShell(_owner._mainShell, _owner._compositionRoot.ContextManager, _owner._compositionRoot.ActiveJobs);
                }

                var form = new PublicServiceStubForm();
                _owner._publicServiceForm = form;
                form.SwitchRequested += (s, e) => _owner.SwitchGroup(AppGroup.Invoice);
                // Đóng cửa sổ nhóm Dịch vụ công = thoát ứng dụng: đóng shell Hóa đơn qua luồng đóng thông thường.
                form.FormClosed += (s, e) => _owner.CloseApplicationFromPublicService();
                return new PublicServiceGroupShell(form);
            }
        }

        private void CloseApplicationFromPublicService()
        {
            if (_closingFromPublicService) return;
            _closingFromPublicService = true;
            _publicServiceForm = null;
            _mainShell.Show();
            _mainShell.Close();
        }

        private async void OnMainShellShown(object sender, EventArgs e)
        {
            // Nhóm Hóa đơn là nhóm đang hiển thị khi mở shell.
            _groups.Open(AppGroup.Invoice);
            try
            {
                // Progress<T> báo qua SynchronizationContext nên có thể đến sau khi InitializeAsync xong.
                await _presenter.ShowAsync(ScreenId.Initializing);
                var startupCompleted = false;
                var progress = new Progress<StartupProgress>(p =>
                {
                    if (!startupCompleted) _mainShell.ShowOperation(new OperationViewState { Text = p.Message, IsBusy = true });
                });

                var result = await _compositionRoot.StartupCoordinator.InitializeAsync(progress, _cancellation.Token);
                startupCompleted = true;
                await ApplyStartupResultAsync(result);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var correlationId = CorrelationId.New();
                _compositionRoot.Logger.Error("Khởi động thất bại [" + correlationId + "]", ex);
                _compositionRoot.ErrorPresenter.Show(_compositionRoot.ErrorMapper.Map(ex, "Khởi động", correlationId));
                await ShowRecoveryAfterStartupFailureAsync();
            }
        }

        /// <summary>Khởi động lỗi giữa chừng: khóa mọi hành động và vào màn hình khôi phục thay vì kẹt ở màn hình khởi tạo.</summary>
        private async System.Threading.Tasks.Task ShowRecoveryAfterStartupFailureAsync()
        {
            try
            {
                _presenter.SetMode(new ShellModeViewState { LicenseLabel = "Cần khôi phục" });
                _mainShell.ShowOperation(new OperationViewState { Text = "Khởi động thất bại", IsBusy = false });
                await _presenter.ShowAsync(ScreenId.Recovery);
            }
            catch (Exception ex)
            {
                _compositionRoot.Logger.Error("Không hiển thị được màn hình khôi phục", ex);
            }
        }

        private async System.Threading.Tasks.Task ApplyStartupResultAsync(StartupResult result)
        {
            // Standard chỉ có một MST: mở kho ngay khi khởi động. Service/ReadOnly chờ người dùng chọn MST.
            if (result.Route == StartupRoute.StandardPortalLogin && result.StandardCompany != null)
            {
                await _compositionRoot.ContextManager.OpenAsync(result.StandardCompany, _cancellation.Token);
            }

            // Đồng bộ quyền với chế độ khởi động (licence thật hoặc giả lập); khi chế độ đổi, shell tự cập nhật qua ModeChanged.
            var updater = _compositionRoot.Entitlement as IEntitlementUpdater;
            if (updater != null && result.Route != StartupRoute.Recovery)
            {
                updater.ApplyLicense(result.LicenseStatus, result.Mode == AppMode.ReadOnlyService);
            }

            _presenter.SetMode(ShellModeMapper.WithLicense(ShellModeMapper.ToModeState(result), result.Mode, _compositionRoot.LicenseService.CurrentInfo, DateTime.UtcNow));
            _presenter.RefreshPortalSession();
            _mainShell.ShowOperation(new OperationViewState { Text = result.UserMessage ?? "Sẵn sàng", IsBusy = false });

            // Service/ReadOnly luôn mở danh sách doanh nghiệp, không tự mở MST gần nhất.
            ScreenId target;
            if (result.Route == StartupRoute.Recovery) target = ScreenId.Recovery;
            else if (result.Route == StartupRoute.StandardFirstRun) target = ScreenId.CompanyEdit;
            else if (result.Mode == AppMode.Standard) target = ScreenId.Dashboard;
            else target = ScreenId.CompanyList;
            await _presenter.ShowAsync(target, target == ScreenId.CompanyEdit ? CompanyEditRequest.FirstRun() : null);

            // Standard: mở đăng nhập ngay; hủy thì ở lại Dashboard (chưa đăng nhập) và đăng nhập sau bằng nút Đăng nhập.
            if (result.Route == StartupRoute.StandardPortalLogin && _compositionRoot.AutoLoginOnStartup)
            {
                await _presenter.LoginAsync(LoginReason.Startup);
            }
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            var mode = _compositionRoot.Entitlement.CurrentMode;
            _presenter.SetMode(ShellModeMapper.WithLicense(ShellModeMapper.ToModeState(mode), mode, _compositionRoot.LicenseService.CurrentInfo, DateTime.UtcNow));
        }

        private void OnMainShellClosed(object sender, FormClosedEventArgs e)
        {
            _cancellation.Cancel();
            _compositionRoot.Entitlement.ModeChanged -= OnModeChanged;
            _presenter.Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closingFromPublicService = true;
                _groups.Dispose();
                _navigation.Dispose();
                _cancellation.Dispose();
                _mainShell.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
