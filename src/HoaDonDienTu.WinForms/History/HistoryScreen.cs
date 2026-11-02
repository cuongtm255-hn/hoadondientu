using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.History
{
    /// <summary>Screen SCR-08 Lịch sử tải: control + presenter. Chỉ đọc nên rời màn hình lúc nào cũng được.</summary>
    public sealed class HistoryScreen : IScreen
    {
        private readonly HistoryControl _control;
        private readonly HistoryPresenter _presenter;

        public HistoryScreen(HistoryControl control, Func<IHistoryView, HistoryPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.DownloadHistory; }
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
