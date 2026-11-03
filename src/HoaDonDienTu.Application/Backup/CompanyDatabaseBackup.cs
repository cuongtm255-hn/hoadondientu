using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Backup
{
    /// <summary>
    /// Phần của gói sao lưu đụng tới <c>company.db</c> của doanh nghiệp đang mở (ADR 0006 mục 7, DD-11): chụp bản nhất quán khi tạo gói
    /// và thay cơ sở dữ liệu khi khôi phục. Implementation nằm ở Infrastructure vì biết SQLite.
    /// </summary>
    public interface ICompanyDatabaseBackup
    {
        /// <summary>Phiên bản schema mới nhất ứng dụng hiểu; gói có schema mới hơn bị từ chối.</summary>
        int LatestSchemaVersion { get; }

        /// <summary>Chụp nhất quán cơ sở dữ liệu đang mở ra <paramref name="destinationFile"/> (ghi đè), kiểm toàn vẹn; trả về phiên bản schema của bản chụp.</summary>
        Task<int> SnapshotAsync(string destinationFile, CancellationToken cancellationToken);

        /// <summary>Kiểm một file cơ sở dữ liệu lấy từ gói (toàn vẹn, schema không mới hơn ứng dụng) mà không đụng tới dữ liệu hiện hành.</summary>
        /// <exception cref="BackupException">File hỏng hoặc có schema mới hơn ứng dụng.</exception>
        Task ValidateAsync(string sourceFile, CancellationToken cancellationToken);

        /// <summary>
        /// Thay cơ sở dữ liệu đang mở bằng <paramref name="sourceFile"/>: nâng schema nếu cũ, gắn các phiên tải về doanh nghiệp đang mở
        /// (mã doanh nghiệp khác nhau giữa các máy) rồi hoán đổi file một bước.
        /// </summary>
        /// <exception cref="BackupException"/>
        Task ReplaceAsync(string sourceFile, CancellationToken cancellationToken);
    }
}
