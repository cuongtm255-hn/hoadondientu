using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    public enum CompanyCatalogStatus
    {
        Active,

        /// <summary>Thư mục kho không còn ở đường dẫn đã đăng ký; cần liên kết lại.</summary>
        PathMissing,

        /// <summary>Kho có nhưng không khớp (MST/schema); không mở.</summary>
        SchemaMismatch,

        Unlinked
    }

    /// <summary>
    /// Một dòng của <c>companies.json</c>: thông tin định tuyến và số liệu tổng quan lưu đệm (BR-021).
    /// Không chứa hóa đơn, token hay khóa licence. Bất biến; dùng các phương thức With* để tạo bản sao đã sửa.
    /// </summary>
    public sealed class CompanyCatalogItem
    {
        public CompanyCatalogItem(
            CompanyId id,
            TaxCode taxCode,
            string displayName,
            string storagePath,
            DateTime? lastOpenedAt,
            DateTime? lastDownloadedAt,
            long cachedInvoiceCount,
            long cachedStorageBytes,
            CompanyCatalogStatus status)
        {
            if (taxCode == null) throw new ArgumentNullException("taxCode");
            if (string.IsNullOrWhiteSpace(storagePath)) throw new ArgumentException("storagePath");
            Id = id;
            TaxCode = taxCode;
            DisplayName = displayName ?? string.Empty;
            StoragePath = storagePath;
            LastOpenedAt = lastOpenedAt;
            LastDownloadedAt = lastDownloadedAt;
            CachedInvoiceCount = cachedInvoiceCount;
            CachedStorageBytes = cachedStorageBytes;
            Status = status;
        }

        public CompanyId Id { get; private set; }
        public TaxCode TaxCode { get; private set; }
        public string DisplayName { get; private set; }
        public string StoragePath { get; private set; }
        public DateTime? LastOpenedAt { get; private set; }
        public DateTime? LastDownloadedAt { get; private set; }
        public long CachedInvoiceCount { get; private set; }
        public long CachedStorageBytes { get; private set; }
        public CompanyCatalogStatus Status { get; private set; }

        public CompanyProfile ToProfile()
        {
            return new CompanyProfile(Id, DisplayName, TaxCode);
        }

        public CompanyCatalogItem WithDisplayName(string displayName)
        {
            return new CompanyCatalogItem(Id, TaxCode, displayName, StoragePath, LastOpenedAt, LastDownloadedAt, CachedInvoiceCount, CachedStorageBytes, Status);
        }

        public CompanyCatalogItem WithStoragePath(string storagePath)
        {
            return new CompanyCatalogItem(Id, TaxCode, DisplayName, storagePath, LastOpenedAt, LastDownloadedAt, CachedInvoiceCount, CachedStorageBytes, Status);
        }

        public CompanyCatalogItem WithStatus(CompanyCatalogStatus status)
        {
            return new CompanyCatalogItem(Id, TaxCode, DisplayName, StoragePath, LastOpenedAt, LastDownloadedAt, CachedInvoiceCount, CachedStorageBytes, status);
        }

        public CompanyCatalogItem WithCache(DateTime? lastOpenedAt, DateTime? lastDownloadedAt, long invoiceCount, long storageBytes)
        {
            return new CompanyCatalogItem(Id, TaxCode, DisplayName, StoragePath, lastOpenedAt, lastDownloadedAt, invoiceCount, storageBytes, Status);
        }
    }

    public enum CompanyCatalogErrorKind
    {
        DuplicateTaxCode,
        NotFound,

        /// <summary>companies.json hỏng và không có bản .bak hợp lệ: không được ghi đè.</summary>
        CatalogCorrupt
    }

    public sealed class CompanyCatalogException : Exception
    {
        public CompanyCatalogException(CompanyCatalogErrorKind kind, string message)
            : base(message)
        {
            Kind = kind;
        }

        public CompanyCatalogErrorKind Kind { get; private set; }
    }

    /// <summary>Danh mục doanh nghiệp đọc/ghi được. Mọi ghi là atomic; catalog hỏng thì từ chối ghi thay vì tạo mới.</summary>
    public interface ICompanyRegistry : ICompanyCatalog
    {
        Task<IList<CompanyCatalogItem>> GetItemsAsync(CancellationToken cancellationToken);

        /// <exception cref="CompanyCatalogException">Trùng MST hoặc catalog hỏng.</exception>
        Task AddAsync(CompanyCatalogItem item, CancellationToken cancellationToken);

        /// <exception cref="CompanyCatalogException">Không tìm thấy hoặc catalog hỏng.</exception>
        Task UpdateAsync(CompanyCatalogItem item, CancellationToken cancellationToken);

        /// <summary>Ngừng quản lý: chỉ gỡ bản ghi khỏi danh mục, không đụng tới thư mục dữ liệu.</summary>
        Task RemoveAsync(CompanyId id, CancellationToken cancellationToken);
    }
}
