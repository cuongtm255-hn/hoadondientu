using System;
using System.Windows.Forms;

namespace HoaDonDienTu.WinForms.Shell
{
    public interface IMainShellView
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
        void ShowScreen(Control screen);
        void SelectNavigation(HoaDonDienTu.Application.Navigation.ScreenId screen);
        void ShowError(string title, string message);
    }
}
