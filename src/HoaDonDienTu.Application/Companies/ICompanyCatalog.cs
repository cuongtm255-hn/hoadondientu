using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    public enum CatalogLoadStatus
    {
        /// <summary>Chưa có companies.json (lần chạy đầu).</summary>
        NotFound,
        Loaded,
        /// <summary>Có file nhưng không đọc/xác minh được; tuyệt đối không ghi đè.</summary>
        Corrupt
    }

    public sealed class CatalogSnapshot
    {
        public CatalogSnapshot(CatalogLoadStatus status, IList<CompanyProfile> companies, bool hasServiceHistory)
            : this(status, companies, hasServiceHistory, false)
        {
        }

        public CatalogSnapshot(CatalogLoadStatus status, IList<CompanyProfile> companies, bool hasServiceHistory, bool recoveredFromBackup)
        {
            RecoveredFromBackup = recoveredFromBackup;
            Status = status;
            Companies = new List<CompanyProfile>(companies ?? new List<CompanyProfile>()).AsReadOnly();
            HasServiceHistory = hasServiceHistory;
        }

        public CatalogLoadStatus Status { get; private set; }

        /// <summary>companies.json chính hỏng nên dữ liệu đọc từ bản .bak; chưa ghi lại gì.</summary>
        public bool RecoveredFromBackup { get; private set; }
        public IList<CompanyProfile> Companies { get; private set; }

        /// <summary>Từng chạy ServiceMode (đã có nhiều MST hoặc có dấu vết kích hoạt dịch vụ).</summary>
        public bool HasServiceHistory { get; private set; }

        public static CatalogSnapshot Empty()
        {
            return new CatalogSnapshot(CatalogLoadStatus.NotFound, null, false);
        }
    }

    /// <summary>Contract tối thiểu cho khởi động; CRUD catalog bổ sung ở DD-02 (T02-01).</summary>
    public interface ICompanyCatalog
    {
        Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken);
    }
}
