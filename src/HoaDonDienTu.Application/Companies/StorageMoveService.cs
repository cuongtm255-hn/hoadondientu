using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Companies
{
    public sealed class StorageMoveResult
    {
        public string OldPath { get; set; }
        public string NewPath { get; set; }
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
    }

    /// <summary>Đổi thư mục kho không thành công; thông báo nói rõ ứng dụng vẫn đang dùng thư mục nào.</summary>
    public sealed class StorageMoveException : Exception
    {
        public StorageMoveException(string message)
            : base(message)
        {
        }

        public StorageMoveException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public interface IStorageMoveService
    {
        /// <summary>Các vấn đề của thư mục mới (rỗng nghĩa là dùng được). Không thay đổi gì.</summary>
        Task<IList<string>> CheckAsync(string newPath, CancellationToken cancellationToken);

        /// <summary>
        /// Chép toàn bộ kho của doanh nghiệp đang mở sang thư mục mới, kiểm SHA-256 từng file, rồi liên kết doanh nghiệp sang thư mục mới và mở lại.
        /// Thư mục cũ được giữ nguyên, không bao giờ bị xóa. Hủy hoặc lỗi trước khi liên kết thì thư mục mới được dọn và vẫn dùng thư mục cũ.
        /// </summary>
        /// <exception cref="StorageMoveException"/>
        Task<StorageMoveResult> MoveAsync(string newPath, IProgress<BackupProgress> progress, CancellationToken cancellationToken);
    }

    /// <summary>Đổi thư mục kho (DD-11, FR-SET-003): chép, kiểm, liên kết lại; không xóa dữ liệu cũ.</summary>
    public sealed class StorageMoveService : IStorageMoveService
    {
        private const string TempSuffix = ".copying";

        // company.db đang mở nên không chép nguyên file: bản chụp nhất quán được lấy bằng ICompanyDatabaseBackup.
        private static readonly string[] DatabaseFiles = { "company.db", "company.db-wal", "company.db-shm", "company.db-journal" };

        private readonly ICompanyContextManager _context;
        private readonly ICompanyRegistry _registry;
        private readonly ICompanyStorage _storage;
        private readonly IActiveJobService _jobs;
        private readonly IEntitlementService _entitlement;
        private readonly ICompanyDatabaseBackup _database;
        private readonly IApplicationLogger _logger;

        public StorageMoveService(
            ICompanyContextManager context,
            ICompanyRegistry registry,
            ICompanyStorage storage,
            IActiveJobService jobs,
            IEntitlementService entitlement,
            ICompanyDatabaseBackup database,
            IApplicationLogger logger)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (registry == null) throw new ArgumentNullException("registry");
            if (storage == null) throw new ArgumentNullException("storage");
            if (jobs == null) throw new ArgumentNullException("jobs");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (database == null) throw new ArgumentNullException("database");
            if (logger == null) throw new ArgumentNullException("logger");
            _context = context;
            _registry = registry;
            _storage = storage;
            _jobs = jobs;
            _entitlement = entitlement;
            _database = database;
            _logger = logger;
        }

        public async Task<IList<string>> CheckAsync(string newPath, CancellationToken cancellationToken)
        {
            var problems = new List<string>();
            if (!_context.HasCurrent)
            {
                problems.Add("Chưa mở doanh nghiệp nào.");
                return problems;
            }

            var company = _context.Current;
            string target;
            if (!TryNormalize(newPath, out target))
            {
                problems.Add("Đường dẫn thư mục không hợp lệ.");
                return problems;
            }

            var oldRoot = Normalize(company.StorageRoot);
            if (string.Equals(target, oldRoot, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("Đây đang là thư mục kho hiện tại.");
                return problems;
            }

            if (IsUnder(target, oldRoot)) problems.Add("Thư mục mới không được nằm trong thư mục kho hiện tại.");
            if (IsUnder(oldRoot, target)) problems.Add("Thư mục mới không được chứa thư mục kho hiện tại.");

            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var other in items.Where(i => !i.Id.Equals(company.CompanyId)))
            {
                var otherRoot = Normalize(other.StoragePath);
                if (IsUnder(target, otherRoot) || IsUnder(otherRoot, target))
                {
                    problems.Add("Thư mục trùng hoặc lồng với kho của doanh nghiệp khác: " + other.DisplayName + " (MST " + other.TaxCode.Value + ").");
                }
            }

            if (problems.Count > 0) return problems;

            var inspection = _storage.Inspect(target);
            if (inspection.Exists && !inspection.IsEmpty) problems.Add("Thư mục mới phải trống để không lẫn với dữ liệu khác.");
            if (!inspection.Writable) problems.Add("Không ghi được vào thư mục mới" + (string.IsNullOrEmpty(inspection.WriteProblem) ? "." : ": " + inspection.WriteProblem));

            if (inspection.FreeBytes.HasValue)
            {
                var needed = await Task.Run(() => TotalSize(oldRoot), cancellationToken).ConfigureAwait(false);
                // Dư 5% và 64 MB cho bản chụp cơ sở dữ liệu và file tạm.
                var required = needed + needed / 20 + 64L * 1024 * 1024;
                if (inspection.FreeBytes.Value < required)
                {
                    problems.Add("Ổ đĩa mới không đủ chỗ: cần khoảng " + FormatMb(required) + ", còn trống " + FormatMb(inspection.FreeBytes.Value) + ".");
                }
            }

            return problems;
        }

        public async Task<StorageMoveResult> MoveAsync(string newPath, IProgress<BackupProgress> progress, CancellationToken cancellationToken)
        {
            // Không dùng ConfigureAwait(false) trong phương thức này: đóng/mở context phát ContextChanged nên phải chạy trên luồng của người gọi (UI).
            _entitlement.Demand(AppFeature.ManageSettings);
            var problems = await CheckAsync(newPath, cancellationToken);
            if (problems.Count > 0) throw new StorageMoveException(problems[0] + (problems.Count > 1 ? " (và " + (problems.Count - 1) + " vấn đề khác)" : string.Empty));

            var company = _context.Current;
            if (await _jobs.HasRunningDownloadAsync(company.CompanyId, cancellationToken))
            {
                throw new StorageMoveException("Đang có phiên tải chạy cho doanh nghiệp này. Hãy tạm dừng hoặc chờ phiên tải xong rồi thử lại.");
            }

            var oldRoot = Normalize(company.StorageRoot);
            var newRoot = Normalize(newPath);
            var created = new List<string>();
            var createdRoot = !Directory.Exists(newRoot);
            StorageMoveResult result;

            try
            {
                result = await Task.Run(() => Copy(oldRoot, newRoot, created, progress, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cleanup(newRoot, created, createdRoot);
                throw;
            }
            catch (Exception ex)
            {
                Cleanup(newRoot, created, createdRoot);
                _logger.Error("Đổi thư mục kho: chép thất bại", ex);
                throw new StorageMoveException("Không chép được kho sang thư mục mới: " + ex.Message + " Ứng dụng vẫn dùng thư mục cũ và thư mục mới đã được dọn.", ex);
            }

            // Từ đây không hủy giữa chừng: danh mục và context phải luôn khớp nhau.
            var item = (await _registry.GetItemsAsync(CancellationToken.None)).First(i => i.Id.Equals(company.CompanyId));
            _context.CloseCurrent();
            try
            {
                await _registry.UpdateAsync(item.WithStoragePath(newRoot).WithStatus(CompanyCatalogStatus.Active), CancellationToken.None);
                await _context.OpenAsync(item.ToProfile(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Error("Đổi thư mục kho: không mở được kho mới, quay lại thư mục cũ", ex);
                await RestoreOldAsync(item);
                Cleanup(newRoot, created, createdRoot);
                throw new StorageMoveException("Không mở được kho ở thư mục mới: " + ex.Message + " Đã quay lại dùng thư mục cũ và dọn thư mục mới.", ex);
            }

            result.OldPath = oldRoot;
            result.NewPath = newRoot;
            _logger.Info("Đổi thư mục kho: " + result.FileCount + " file sang thư mục mới (thư mục cũ được giữ nguyên)");
            return result;
        }

        private async Task RestoreOldAsync(CompanyCatalogItem original)
        {
            try
            {
                await _registry.UpdateAsync(original, CancellationToken.None);
                if (!_context.HasCurrent) await _context.OpenAsync(original.ToProfile(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Error("Đổi thư mục kho: không khôi phục được thư mục cũ", ex);
            }
        }

        // ---- Chép và kiểm ----

        private StorageMoveResult Copy(string oldRoot, string newRoot, List<string> created, IProgress<BackupProgress> progress, CancellationToken token)
        {
            var files = EnumerateFiles(oldRoot).ToList();
            var total = files.Count + 1;
            var done = 0;
            long bytes = 0;

            Directory.CreateDirectory(newRoot);

            // Cơ sở dữ liệu trước: bản chụp nhất quán, kiểm toàn vẹn và schema trước khi chép phần còn lại.
            var databaseTarget = Path.Combine(newRoot, "Data", "company.db");
            Directory.CreateDirectory(Path.GetDirectoryName(databaseTarget));
            created.Add(databaseTarget);
            _database.SnapshotAsync(databaseTarget, token).GetAwaiter().GetResult();
            _database.ValidateAsync(databaseTarget, token).GetAwaiter().GetResult();
            bytes += new FileInfo(databaseTarget).Length;
            Report(progress, ++done, total);

            foreach (var relative in files)
            {
                token.ThrowIfCancellationRequested();
                var source = Path.Combine(oldRoot, relative);
                var target = Path.Combine(newRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                var temp = target + TempSuffix;
                created.Add(temp);
                File.Copy(source, temp, false);
                if (!string.Equals(Hash(source), Hash(temp), StringComparison.Ordinal))
                {
                    throw new IOException("File " + relative + " chép sang không khớp SHA-256.");
                }

                created.Remove(temp);
                created.Add(target);
                File.Move(temp, target);
                bytes += new FileInfo(target).Length;
                Report(progress, ++done, total);
            }

            return new StorageMoveResult { FileCount = total, TotalBytes = bytes };
        }

        /// <summary>Chỉ xóa đúng những file lần chép này đã tạo, rồi các thư mục con đã rỗng; không đụng tới thứ gì khác.</summary>
        private static void Cleanup(string newRoot, IEnumerable<string> created, bool createdRoot)
        {
            foreach (var file in created)
            {
                TryDelete(file);
                TryDelete(file + TempSuffix);
            }

            try
            {
                if (!Directory.Exists(newRoot)) return;
                foreach (var directory in Directory.GetDirectories(newRoot, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                }

                if (createdRoot && !Directory.EnumerateFileSystemEntries(newRoot).Any()) Directory.Delete(newRoot);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static IEnumerable<string> EnumerateFiles(string root)
        {
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(prefix.Length);
                if (IsDatabaseFile(relative) || relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase)) continue;
                yield return relative;
            }
        }

        private static bool IsDatabaseFile(string relative)
        {
            var directory = Path.GetDirectoryName(relative);
            return string.Equals(directory, "Data", StringComparison.OrdinalIgnoreCase)
                && DatabaseFiles.Contains(Path.GetFileName(relative), StringComparer.OrdinalIgnoreCase);
        }

        private static long TotalSize(string root)
        {
            long sum = 0;
            if (!Directory.Exists(root)) return 0;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    sum += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }
            }

            return sum;
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return BitConverter.ToString(sha.ComputeHash(stream));
            }
        }

        private static void Report(IProgress<BackupProgress> progress, int done, int total)
        {
            if (progress != null) progress.Report(new BackupProgress(done, total));
        }

        // ---- Đường dẫn ----

        private static bool TryNormalize(string path, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                normalized = Normalize(path);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
        }

        private static string Normalize(string path)
        {
            var full = Path.GetFullPath(path.Trim());
            var root = Path.GetPathRoot(full);
            return full.Length > root.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
        }

        /// <summary>true nếu <paramref name="child"/> trùng hoặc nằm trong <paramref name="parent"/>.</summary>
        private static bool IsUnder(string child, string parent)
        {
            var c = child.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var p = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatMb(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " MB";
        }
    }
}
