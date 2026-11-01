using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>Screen SCR-03: control + presenter; Dispose hủy operation và hủy đăng ký sự kiện trước khi giải phóng control.</summary>
    public sealed class DownloadConfigScreen : IScreen
    {
        private readonly DownloadConfigControl _control;
        private readonly DownloadConfigPresenter _presenter;

        public DownloadConfigScreen(DownloadConfigControl control, Func<IDownloadConfigView, DownloadConfigPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.Download; }
        }

        public Control View
        {
            get { return _control; }
        }

        /// <summary>Màn hình chỉ chứa cấu hình, không có thao tác dở: luôn rời đi được.</summary>
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
