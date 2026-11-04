using System;
using System.Collections.Generic;
using System.IO;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Invoices;

namespace HoaDonDienTu.Infrastructure.Invoices
{
    /// <summary>Duyệt thư mục kho của MST để tìm file XML và file tạm; bỏ qua các thư mục dành riêng (<c>Data</c>, <c>Backups</c>, <c>Exports</c>).</summary>
    public sealed class FileSystemInvoiceFileScanner : IInvoiceFileScanner
    {
        public IEnumerable<string> EnumerateXmlFiles(string storageRoot)
        {
            return Enumerate(storageRoot, "*.xml");
        }

        public IEnumerable<string> EnumerateTempFiles(string storageRoot)
        {
            return Enumerate(storageRoot, "*" + FileSystemInvoiceFileStore.TempSuffix);
        }

        public bool DeleteTempFile(string storageRoot, string relativePath)
        {
            if (string.IsNullOrEmpty(storageRoot) || string.IsNullOrEmpty(relativePath)) return false;
            if (!relativePath.EndsWith(FileSystemInvoiceFileStore.TempSuffix, StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                var root = Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(Path.Combine(root, relativePath));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;

                var inside = full.Substring(root.Length);
                var separator = inside.IndexOf(Path.DirectorySeparatorChar);
                if (separator >= 0 && IsReserved(inside.Substring(0, separator))) return false;
                if (!File.Exists(full)) return false;

                File.SetAttributes(full, FileAttributes.Normal);
                File.Delete(full);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static IEnumerable<string> Enumerate(string storageRoot, string pattern)
        {
            if (string.IsNullOrEmpty(storageRoot) || !Directory.Exists(storageRoot)) yield break;
            var root = Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // File ngay trong thư mục kho cũng thuộc kho; thư mục dành riêng chỉ bị loại ở cấp đầu.
            foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly)) yield return Relative(root, file);
            foreach (var folder in Directory.EnumerateDirectories(root))
            {
                if (IsReserved(Path.GetFileName(folder))) continue;
                foreach (var file in Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)) yield return Relative(root, file);
            }
        }

        private static bool IsReserved(string name)
        {
            return string.Equals(name, StorageStatisticsService.DataFolder, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, BackupService.BackupsFolder, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, BackupService.ExportsFolder, StringComparison.OrdinalIgnoreCase);
        }

        private static string Relative(string root, string file)
        {
            return file.Substring(root.Length);
        }
    }
}
