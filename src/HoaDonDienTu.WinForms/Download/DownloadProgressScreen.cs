using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>Screen SCR-04 Tiến độ tải. Rời màn hình khi đang chạy thì phiên tạm dừng an toàn và tiếp tục được sau.</summary>
    public sealed class DownloadProgressScreen : IScreen
    {
        private readonly DownloadProgressControl _control;
        private readonly DownloadProgressPresenter _presenter;

        public DownloadProgressScreen(DownloadProgressControl control, Func<IDownloadProgressView, DownloadProgressPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.DownloadProgress; }
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
