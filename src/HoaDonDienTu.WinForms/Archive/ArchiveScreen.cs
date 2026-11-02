using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>Screen SCR-05 Kho hóa đơn: control + presenter. Chỉ đọc nên rời màn hình lúc nào cũng được.</summary>
    public sealed class ArchiveScreen : IScreen
    {
        private readonly ArchiveControl _control;
        private readonly ArchivePresenter _presenter;

        public ArchiveScreen(ArchiveControl control, Func<IArchiveView, ArchivePresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.InvoiceArchive; }
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
