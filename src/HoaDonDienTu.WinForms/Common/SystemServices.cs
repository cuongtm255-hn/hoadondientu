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

    /// <summary>Chọn nơi lưu một file XML; hộp thoại hỏi xác nhận trước khi ghi đè (FR-DETAIL-006).</summary>
    public interface ISaveFilePicker
    {
        /// <summary>Trả về đường dẫn đã chọn, hoặc null nếu người dùng hủy.</summary>
        string Pick(string suggestedFileName);
    }

    /// <summary>Chọn nơi lưu file xuất (Excel/ZIP); hộp thoại hỏi xác nhận trước khi ghi đè (FR-EXPORT-008).</summary>
    public interface IExportSavePicker
    {
        /// <summary>Trả về đường dẫn đã chọn, hoặc null nếu người dùng hủy.</summary>
        string Pick(string suggestedFileName, string extension, string filterDescription, string title, string initialDirectory);
    }

    /// <summary>Chọn một file có sẵn để mở (ví dụ gói sao lưu).</summary>
    public interface IOpenFilePicker
    {
        /// <summary>Trả về đường dẫn đã chọn, hoặc null nếu người dùng hủy.</summary>
        string Pick(string title, string extension, string filterDescription, string initialDirectory);
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

    public sealed class XmlSaveFilePicker : ISaveFilePicker
    {
        public string Pick(string suggestedFileName)
        {
            using (var dialog = new SaveFileDialog
            {
                FileName = suggestedFileName,
                Filter = "Hóa đơn XML (*.xml)|*.xml",
                DefaultExt = "xml",
                OverwritePrompt = true,
                Title = "Lưu bản sao XML"
            })
            {
                return dialog.ShowDialog(Form.ActiveForm) == DialogResult.OK ? dialog.FileName : null;
            }
        }
    }

    public sealed class ExportSavePicker : IExportSavePicker
    {
        public string Pick(string suggestedFileName, string extension, string filterDescription, string title, string initialDirectory)
        {
            using (var dialog = new SaveFileDialog
            {
                FileName = suggestedFileName,
                Filter = filterDescription + " (*." + extension + ")|*." + extension,
                DefaultExt = extension,
                OverwritePrompt = true,
                Title = title
            })
            {
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
                return dialog.ShowDialog(Form.ActiveForm) == DialogResult.OK ? dialog.FileName : null;
            }
        }
    }

    public sealed class OpenFilePicker : IOpenFilePicker
    {
        public string Pick(string title, string extension, string filterDescription, string initialDirectory)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filterDescription + " (*." + extension + ")|*." + extension,
                CheckFileExists = true,
                Multiselect = false
            })
            {
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
                return dialog.ShowDialog(Form.ActiveForm) == DialogResult.OK ? dialog.FileName : null;
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
