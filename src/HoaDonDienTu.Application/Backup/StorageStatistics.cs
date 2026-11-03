using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Backup
{
    public sealed class StorageStatistics
    {
        public string Root { get; set; }
        public long XmlBytes { get; set; }
        public int XmlFiles { get; set; }
        public long PdfBytes { get; set; }
        public int PdfFiles { get; set; }
        public long ExportBytes { get; set; }
        public long BackupBytes { get; set; }
        public int BackupFiles { get; set; }

        /// <summary>Cơ sở dữ liệu của kho (<c>Data\company.db</c> cùng tệp -wal/-shm).</summary>
        public long DatabaseBytes { get; set; }

        /// <summary>Bản sao cơ sở dữ liệu giữ lại trước mỗi lần nâng schema (<c>*.bak</c> trong <c>Data</c>).</summary>
        public long UpgradeCopyBytes { get; set; }

        /// <summary>null khi không xác định được (ví dụ đường dẫn mạng).</summary>
        public long? FreeBytes { get; set; }

        public string LastBackupPath { get; set; }
        public DateTime? LastBackupUtc { get; set; }

        public long TotalBytes
        {
            get { return XmlBytes + PdfBytes + ExportBytes + BackupBytes + DatabaseBytes + UpgradeCopyBytes; }
        }
    }

    public interface IStorageStatisticsService
    {
        /// <exception cref="BackupException">Chưa mở doanh nghiệp nào.</exception>
        Task<StorageStatistics> GetAsync(CancellationToken cancellationToken);
    }

    /// <summary>Dung lượng thư mục kho của MST đang mở (FR-SET-004). Duyệt đĩa nên gọi ở luồng nền.</summary>
    public sealed class StorageStatisticsService : IStorageStatisticsService
    {
        /// <summary>Thư mục chứa <c>company.db</c> trong kho (DD-06 mục 3).</summary>
        public const string DataFolder = "Data";

        private readonly ICompanyContextAccessor _context;

        public StorageStatisticsService(ICompanyContextAccessor context)
        {
            if (context == null) throw new ArgumentNullException("context");
            _context = context;
        }

        public Task<StorageStatistics> GetAsync(CancellationToken cancellationToken)
        {
            if (!_context.HasCurrent) throw new BackupException("Chưa mở doanh nghiệp nào.");
            var root = _context.Current.StorageRoot;
            var tax = _context.Current.TaxCode.Value;
            return Task.Run(() => Compute(root, tax, cancellationToken), cancellationToken);
        }

        private static StorageStatistics Compute(string root, string taxCode, CancellationToken token)
        {
            var stats = new StorageStatistics { Root = root, FreeBytes = Free(root) };
            foreach (var relative in BackupService.EnumerateStoreFiles(root))
            {
                token.ThrowIfCancellationRequested();
                var size = Size(Path.Combine(root, relative));
                if (relative.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    stats.PdfBytes += size;
                    stats.PdfFiles++;
                }
                else
                {
                    stats.XmlBytes += size;
                    stats.XmlFiles++;
                }
            }

            var data = Path.Combine(root, DataFolder);
            if (Directory.Exists(data))
            {
                foreach (var file in Directory.EnumerateFiles(data, "*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();
                    if (file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) stats.UpgradeCopyBytes += Size(file);
                    else stats.DatabaseBytes += Size(file);
                }
            }

            stats.ExportBytes = FolderSize(Path.Combine(root, BackupService.ExportsFolder), token);

            var backups = Path.Combine(root, BackupService.BackupsFolder);
            if (Directory.Exists(backups))
            {
                var files = new DirectoryInfo(backups).GetFiles("*.zip", SearchOption.TopDirectoryOnly);
                stats.BackupFiles = files.Length;
                stats.BackupBytes = files.Sum(f => f.Length);
                var last = files.Where(f => f.Name.StartsWith("HDDT_" + taxCode + "_", StringComparison.OrdinalIgnoreCase)).OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                if (last != null)
                {
                    stats.LastBackupPath = last.FullName;
                    stats.LastBackupUtc = last.LastWriteTimeUtc;
                }
            }

            return stats;
        }

        private static long FolderSize(string folder, CancellationToken token)
        {
            if (!Directory.Exists(folder)) return 0;
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                total += Size(file);
            }

            return total;
        }

        private static long Size(string file)
        {
            try
            {
                return new FileInfo(file).Length;
            }
            catch (IOException)
            {
                return 0;
            }
        }

        private static long? Free(string root)
        {
            try
            {
                var drive = Path.GetPathRoot(Path.GetFullPath(root));
                return string.IsNullOrEmpty(drive) || drive.StartsWith(@"\\", StringComparison.Ordinal) ? null : (long?)new DriveInfo(drive).AvailableFreeSpace;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
