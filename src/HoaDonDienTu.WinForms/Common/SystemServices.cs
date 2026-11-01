using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace HoaDonDienTu.WinForms.Common
{
    /// <summary>Chọn thư mục; presenter không gọi hộp thoại hệ thống trực tiếp (DD-01 mục 12.2).</summary>
    public interface IFolderPicker
    {
        /// <summary>Trả về đường dẫn đã chọn, hoặc null nếu người dùng hủy.</summary>
        string Pick(string initialPath);
    }

    public interface IFolderLauncher
    {
        /// <exception cref="DirectoryNotFoundException"/>
        void Open(string path);
    }

    public sealed class FolderPicker : IFolderPicker
    {
        public string Pick(string initialPath)
        {
            using (var dialog = new FolderBrowserDialog { ShowNewFolderButton = true, Description = "Chọn thư mục kho dữ liệu của doanh nghiệp" })
            {
                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath)) dialog.SelectedPath = initialPath;
                return dialog.ShowDialog(Form.ActiveForm) == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }
    }

    public sealed class ExplorerFolderLauncher : IFolderLauncher
    {
        public void Open(string path)
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
    }
}
