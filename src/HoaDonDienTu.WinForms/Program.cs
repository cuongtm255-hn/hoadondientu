using System;
using System.Net;
using System.Windows.Forms;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // TLS 1.2 phải bật trước mọi kết nối tới cổng Thuế (net45 chưa có hằng SecurityProtocolType.Tls12).
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            // Tạm thời: dựng thẳng shell với dữ liệu mẫu cho tới khi có CompositionRoot/StartupCoordinator (T01-04, T01-06).
            using (var shell = new MainShellForm())
            using (var presenter = new MainShellPresenter(shell))
            {
                presenter.Initialize();
                shell.ApplyMode(new ShellModeViewState
                {
                    ShowCompanySelector = true,
                    ShowCompanyMenu = true,
                    CanPortalLogin = true,
                    CanDownload = true,
                    CanAddCompany = true,
                    LicenseLabel = "Bản dịch vụ"
                });
                shell.ShowCompany(new CompanyHeaderViewState { CompanyName = "CÔNG TY TNHH MẪU", TaxCode = "0101234567" });
                shell.ShowPortalSession(new PortalSessionViewState { IsLoggedIn = false, Text = "Chưa đăng nhập" });
                System.Windows.Forms.Application.Run(shell);
            }
        }
    }
}
