using System;
using HoaDonDienTu.Application.Navigation;

namespace HoaDonDienTu.WinForms.Shell
{
    public sealed class NavigationRequestedEventArgs : EventArgs
    {
        public NavigationRequestedEventArgs(ScreenId target)
        {
            Target = target;
        }

        public ScreenId Target { get; private set; }
    }
}
