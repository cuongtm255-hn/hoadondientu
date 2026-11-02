using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Dashboard
{
    /// <summary>Screen SCR-02 Tổng quan: control + presenter. Chỉ đọc nên rời màn hình lúc nào cũng được.</summary>
    public sealed class DashboardScreen : IScreen
    {
        private readonly DashboardControl _control;
        private readonly DashboardPresenter _presenter;

        public DashboardScreen(DashboardControl control, Func<IDashboardView, DashboardPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.Dashboard; }
        }

        public Control View
        {
            get { return _control; }
        }

        public Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public void CancelOperations()
        {
        }

        public void Dispose()
        {
            _presenter.Dispose();
            _control.Dispose();
        }
    }
}
