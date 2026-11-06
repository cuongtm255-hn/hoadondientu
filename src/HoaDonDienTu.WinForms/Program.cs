using System;
using System.Net;
using System.Windows.Forms;
using HoaDonDienTu.WinForms.Bootstrap;

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

            CompositionRoot compositionRoot;
            try
            {
                compositionRoot = CompositionRoot.Create();
            }
            catch (HoaDonDienTu.Infrastructure.Portal.Config.PortalApiConfigException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Cấu hình API cổng Thuế không hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            catch (NotSupportedException)
            {
                // Bản Release chưa có adapter production (DD-02, DD-03, T01-16): báo rõ thay vì để process chết không lời.
                MessageBox.Show(
                    "Bản này chưa có cấu hình production (licence, kho dữ liệu, đăng nhập cổng Thuế). Hãy dùng bản Debug để thử.",
                    "Hóa đơn điện tử",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (compositionRoot)
            {
                // Còn dùng thử hoặc đã kích hoạt thì vào thẳng; hết 30 ngày dùng thử mà không kích hoạt được thì không vào ứng dụng.
                if (!HoaDonDienTu.WinForms.Licensing.ActivationGate.Allow(compositionRoot.MachineLicense, new HoaDonDienTu.WinForms.Licensing.ActivationDialogPrompt()))
                {
                    compositionRoot.Logger.Info("Thoát: chưa kích hoạt bản quyền sau thời gian dùng thử");
                    return;
                }

                // Phải đăng ký trước khi tạo bất kỳ form nào (SetUnhandledExceptionMode không được gọi sau khi có window).
                GlobalExceptionHandler.Register(compositionRoot.Logger);
                using (var applicationContext = new AppApplicationContext(compositionRoot))
                {
                    System.Windows.Forms.Application.Run(applicationContext);
                }
            }
        }
    }
}
