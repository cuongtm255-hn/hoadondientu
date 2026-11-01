using System;
using System.Windows.Forms;
using HoaDonDienTu.WinForms.Navigation;

namespace HoaDonDienTu.WinForms.Shell
{
    public interface IMainShellView : INavigationView
    {
        IWin32Window OwnerWindow { get; }

        event EventHandler<NavigationRequestedEventArgs> NavigationRequested;
        event EventHandler CompanySelectorRequested;
        event EventHandler PortalLoginRequested;
        event EventHandler ShellClosingRequested;

        void ApplyMode(ShellModeViewState state);
        void ShowCompany(CompanyHeaderViewState state);
        void ShowPortalSession(PortalSessionViewState state);
        void ShowOperation(OperationViewState state);
        void ShowError(string title, string message);

        /// <summary>Đóng cửa sổ chính mà không hỏi lại; chỉ gọi sau khi quy trình đóng đã hoàn tất.</summary>
        void CloseShell();
    }
}
