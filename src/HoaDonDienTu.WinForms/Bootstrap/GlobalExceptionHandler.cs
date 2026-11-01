using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Abstractions;

namespace HoaDonDienTu.WinForms.Bootstrap
{
    /// <summary>Ghi log, báo cho người dùng kèm mã tham chiếu rồi thoát; không che lỗi để chạy tiếp ở trạng thái không xác định.</summary>
    public static class GlobalExceptionHandler
    {
        private static IApplicationLogger _logger;

        public static void Register(IApplicationLogger logger)
        {
            _logger = logger;
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            var correlationId = Log("Lỗi chưa xử lý trên luồng UI", e.Exception);
            MessageBox.Show(
                "Ứng dụng gặp lỗi không mong muốn và sẽ đóng.\r\nMã tham chiếu: " + correlationId,
                "Hóa đơn điện tử",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            System.Windows.Forms.Application.Exit();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log("Lỗi chưa xử lý trên luồng nền", e.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log("Task lỗi không được quan sát", e.Exception);
            e.SetObserved();
        }

        private static string Log(string message, Exception exception)
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            if (_logger != null) _logger.Error(message + " [" + correlationId + "]", exception);
            return correlationId;
        }
    }
}
