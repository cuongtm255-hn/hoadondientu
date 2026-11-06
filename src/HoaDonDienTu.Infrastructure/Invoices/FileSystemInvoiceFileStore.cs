using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Infrastructure.Invoices
{
    /// <summary>
    /// Ghi file XML vào thư mục kho của MST: ghi file tạm cùng thư mục đích rồi đổi tên (atomic trên cùng volume), nên file chính thức
    /// không bao giờ ở trạng thái ghi dở. Nếu tên đã tồn tại thì thêm hậu tố, không ghi đè file nào (DD-06 mục 3, 6).
    /// </summary>
    public sealed class FileSystemInvoiceFileStore : IInvoiceFileStore, IInvoiceFileReader, IInvoiceDirectoryLister
    {
        public const string TempSuffix = ".tmp";

        public async Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken)
        {
            var folder = Path.Combine(storageRoot, relativeFolder);
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(folder, Guid.NewGuid().ToString("N") + TempSuffix);
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await stream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                DiscardTemp(temp);
                throw;
            }

            return temp;
        }

        public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName)
        {
            var folder = Path.Combine(storageRoot, relativeFolder);
            var name = fileName;
            for (var attempt = 2; ; attempt++)
            {
                var target = Path.Combine(folder, name);
                if (!File.Exists(target))
                {
                    try
                    {
                        // File.Move không ghi đè: nếu có tiến trình khác vừa tạo tên này thì thử hậu tố tiếp theo.
                        File.Move(tempPath, target);
                        return Path.Combine(relativeFolder, name);
                    }
                    catch (IOException) when (File.Exists(target))
                    {
                    }
                }

                if (attempt > 99) throw new IOException("Không tìm được tên file trống cho hóa đơn.");
                name = InvoiceFileNamer.WithCollisionSuffix(fileName, attempt);
            }
        }

        public byte[] Read(string storageRoot, string relativePath, long maxBytes)
        {
            var path = ResolveInsideRoot(storageRoot, relativePath);
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("File không còn trong kho.", relativePath);
            if (info.Length > maxBytes) throw new InvoiceFileTooLargeException(info.Length, maxBytes);
            return File.ReadAllBytes(path);
        }

        /// <summary>Đường dẫn lấy từ metadata không được trỏ ra ngoài thư mục kho (NFR-SEC-006).</summary>
        private static string ResolveInsideRoot(string storageRoot, string relativePath)
        {
            var root = Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(storageRoot, relativePath ?? string.Empty));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("Đường dẫn nằm ngoài thư mục kho.", relativePath);
            return full;
        }

        public ISet<string> ListFileNames(string storageRoot, string relativeDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(relativeDirectory)) return null;
                var path = ResolveInsideRoot(storageRoot, relativeDirectory);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!Directory.Exists(path)) return names;
                foreach (var file in Directory.EnumerateFiles(path)) names.Add(Path.GetFileName(file));
                return names;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public bool Exists(string storageRoot, string relativePath)
        {
            try
            {
                return !string.IsNullOrEmpty(relativePath) && File.Exists(Path.Combine(storageRoot, relativePath));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public void DiscardTemp(string tempPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public void Delete(string storageRoot, string relativePath)
        {
            try
            {
                var path = Path.Combine(storageRoot, relativePath);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
