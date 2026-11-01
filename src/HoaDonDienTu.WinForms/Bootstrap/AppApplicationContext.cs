using System;
using System.Threading;
using System.Windows.Forms;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms.Bootstrap
{
    /// <summary>Sở hữu vòng đời MainShellForm: hiển thị trước, khởi động sau để message loop luôn chạy.</summary>
    public sealed class AppApplicationContext : ApplicationContext
    {
        private readonly CompositionRoot _compositionRoot;
        private readonly MainShellForm _mainShell;
        private readonly MainShellPresenter _presenter;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        public AppApplicationContext(CompositionRoot compositionRoot)
        {
            _compositionRoot = compositionRoot;
            _mainShell = compositionRoot.CreateMainShell();
            _presenter = new MainShellPresenter(
                _mainShell,
                compositionRoot.PortalLoginCoordinator,
                compositionRoot.ContextManager,
                compositionRoot.SessionStore,
                compositionRoot.OperationRunner,
                compositionRoot.ErrorPresenter);
            _presenter.Initialize();
            _presenter.ShowInitializing();

            _mainShell.Shown += OnMainShellShown;
            _mainShell.FormClosed += OnMainShellClosed;
            MainForm = _mainShell;
        }

        private async void OnMainShellShown(object sender, EventArgs e)
        {
            try
            {
                // Progress<T> báo qua SynchronizationContext nên có thể đến sau khi InitializeAsync xong.
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
            }
        }

        private async System.Threading.Tasks.Task ApplyStartupResultAsync(StartupResult result)
        {
            // Standard chỉ có một MST: mở kho ngay khi khởi động. Service/ReadOnly chờ người dùng chọn MST.
            if (result.Route == StartupRoute.StandardPortalLogin && result.StandardCompany != null)
            {
                await _compositionRoot.ContextManager.OpenAsync(result.StandardCompany, _cancellation.Token);
            }

            _mainShell.ApplyMode(ShellModeMapper.ToModeState(result));
            _mainShell.ShowCompany(ShellModeMapper.ToCompanyState(result));
            _presenter.RefreshPortalSession();
            _mainShell.ShowOperation(new OperationViewState { Text = result.UserMessage ?? "Sẵn sàng", IsBusy = false });

            // Service/ReadOnly luôn mở danh sách doanh nghiệp, không tự mở MST gần nhất.
            ScreenId target;
            if (result.Route == StartupRoute.Recovery) target = ScreenId.Recovery;
            else if (result.Mode == AppMode.Standard) target = ScreenId.Dashboard;
            else target = ScreenId.CompanyList;
            _presenter.Show(target);
        }

        private void OnMainShellClosed(object sender, FormClosedEventArgs e)
        {
            _cancellation.Cancel();
            _presenter.Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellation.Dispose();
                _mainShell.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
