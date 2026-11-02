using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>Screen SCR-06 Chi tiết hóa đơn: control + presenter. Chỉ đọc nên rời màn hình lúc nào cũng được.</summary>
    public sealed class InvoiceDetailScreen : IScreen
    {
        private readonly InvoiceDetailControl _control;
        private readonly InvoiceDetailPresenter _presenter;

        public InvoiceDetailScreen(InvoiceDetailControl control, Func<IInvoiceDetailView, InvoiceDetailPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.InvoiceDetail; }
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

    /// <summary>Screen SCR-07 Xem chứng từ: control + presenter. Chỉ đọc nên rời màn hình lúc nào cũng được.</summary>
    public sealed class InvoiceViewerScreen : IScreen
    {
        private readonly InvoiceViewerControl _control;
        private readonly InvoiceViewerPresenter _presenter;

        public InvoiceViewerScreen(InvoiceViewerControl control, Func<IInvoiceViewerView, InvoiceViewerPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.InvoiceViewer; }
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
