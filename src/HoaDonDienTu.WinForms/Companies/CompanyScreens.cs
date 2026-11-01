using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>Screen SCR-10 danh sách doanh nghiệp: control + presenter.</summary>
    public sealed class CompanyListScreen : IScreen
    {
        private readonly CompanyListControl _control;
        private readonly CompanyListPresenter _presenter;

        public CompanyListScreen(CompanyListControl control, Func<ICompanyListView, CompanyListPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.CompanyList; }
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

    /// <summary>Screen SCR-11 thêm/sửa doanh nghiệp (và khai báo lần đầu). Rời đi khi đang nhập dở được phép: dữ liệu chưa ghi thì chưa có gì để mất.</summary>
    public sealed class CompanyEditScreen : IScreen
    {
        private readonly CompanyEditControl _control;
        private readonly CompanyEditPresenter _presenter;

        public CompanyEditScreen(CompanyEditControl control, Func<ICompanyEditView, CompanyEditPresenter> presenterFactory)
        {
            _control = control;
            _presenter = presenterFactory(control);
            _presenter.Initialize();
        }

        public ScreenId Id
        {
            get { return ScreenId.CompanyEdit; }
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
