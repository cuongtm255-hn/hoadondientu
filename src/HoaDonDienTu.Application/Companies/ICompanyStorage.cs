using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    /// <summary>Kết quả xem xét một thư mục kho mà không thay đổi gì.</summary>
    public sealed class StorageInspection
    {
        public bool Exists { get; set; }

        /// <summary>Thư mục có thể ghi (hoặc có thể tạo nếu chưa tồn tại).</summary>
        public bool Writable { get; set; }

        public string WriteProblem { get; set; }
        public bool IsEmpty { get; set; }
        public long? FreeBytes { get; set; }

        /// <summary>Có file định danh kho (MST + mã doanh nghiệp).</summary>
        public bool HasIdentity { get; set; }

        public TaxCode IdentityTaxCode { get; set; }
        public CompanyId? IdentityCompanyId { get; set; }

        /// <summary>Có file định danh nhưng không đọc được hoặc sai phiên bản.</summary>
        public string IdentityProblem { get; set; }

        /// <summary>
        /// Có <c>Data\company.db</c> nhưng không dùng được (không phải cơ sở dữ liệu, hỏng đầu file, hoặc schema mới hơn ứng dụng).
        /// Chỉ là kiểm tra nhanh phần đầu file; kiểm toàn vẹn đầy đủ chạy khi cơ sở dữ liệu được dùng lần đầu.
        /// </summary>
        public string DatabaseProblem { get; set; }
    }

    public sealed class StorageInitResult
    {
        public StorageInitResult(bool createdDirectory, bool createdIdentity)
        {
            CreatedDirectory = createdDirectory;
            CreatedIdentity = createdIdentity;
        }

        public bool CreatedDirectory { get; private set; }
        public bool CreatedIdentity { get; private set; }
    }

    /// <summary>
    /// Thư mục kho của một MST. Cấu trúc bên trong (company.db, XML, PDF...) do DD-06 bổ sung; ở đây chỉ đảm bảo
    /// thư mục tồn tại và có file định danh để không mở nhầm kho của MST khác.
    /// </summary>
    public interface ICompanyStorage
    {
        StorageInspection Inspect(string path);

        /// <exception cref="System.InvalidOperationException">Thư mục đã có file định danh: không ghi đè dữ liệu hiện hữu.</exception>
        StorageInitResult Initialize(string path, CompanyId companyId, TaxCode taxCode);

        /// <summary>Hoàn tác <see cref="Initialize"/> khi bước sau thất bại: chỉ xóa những gì chính lần khởi tạo đó đã tạo.</summary>
        void Rollback(string path, StorageInitResult result);
    }
}
