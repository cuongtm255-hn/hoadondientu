using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>Hóa đơn không lưu được vì nội dung không hợp lệ (XML hỏng, đường dẫn quá dài...). Thông báo an toàn để hiển thị/ghi sự kiện.</summary>
    public sealed class InvoiceStorageException : Exception
    {
        public InvoiceStorageException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Một phiên bản XML của hóa đơn (DD-06 mục 4: <c>InvoiceVersion</c> + <c>FileAsset</c>).</summary>
    public sealed class InvoiceVersionRecord
    {
        public Guid VersionId { get; set; }
        public int VersionNo { get; set; }

        /// <summary>SHA-256 dạng hex của byte XML nguyên văn.</summary>
        public string Sha256 { get; set; }

        /// <summary>Đường dẫn tương đối so với thư mục kho của MST, để di chuyển kho vẫn dùng được.</summary>
        public string RelativePath { get; set; }

        public long SizeBytes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsCurrent { get; set; }
        public Guid SourceSessionId { get; set; }
    }

    public sealed class StoredInvoice
    {
        public Guid InvoiceId { get; set; }
        public string BusinessKey { get; set; }
        public NormalizedInvoice Invoice { get; set; }
        public DateTime FirstSeenAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }
        public IList<InvoiceVersionRecord> Versions { get; set; }

        public InvoiceVersionRecord Current
        {
            get
            {
                foreach (var v in Versions) if (v.IsCurrent) return v;
                return null;
            }
        }
    }

    /// <summary>
    /// Lưu metadata hóa đơn của một MST (SQLite ở DD-06/G-08 sẽ thay <c>InMemoryInvoiceRepository</c>). Cập nhật phải bền vững trước khi trả về.
    /// </summary>
    public interface IInvoiceRepository
    {
        Task<StoredInvoice> FindAsync(string businessKey, CancellationToken cancellationToken);

        /// <summary>null khi không có hóa đơn nào mang mã này.</summary>
        Task<StoredInvoice> FindByIdAsync(Guid invoiceId, CancellationToken cancellationToken);

        /// <summary>Tạo hóa đơn mới cùng phiên bản đầu tiên.</summary>
        Task AddAsync(StoredInvoice invoice, CancellationToken cancellationToken);

        /// <summary>Thêm phiên bản mới, đặt nó là hiện hành và giữ các bản cũ (BR-010).</summary>
        Task AddVersionAsync(Guid invoiceId, InvoiceVersionRecord version, NormalizedInvoice latest, DateTime nowUtc, CancellationToken cancellationToken);

        /// <summary>
        /// Hash trùng (BR-009): không tạo phiên bản mới, chỉ cập nhật thời điểm thấy lần cuối và metadata mới nhất của cổng
        /// (trạng thái, kết quả kiểm tra...). <paramref name="restoredRelativePath"/> khác null khi file bị mất đã được ghi lại.
        /// </summary>
        Task TouchAsync(Guid invoiceId, DateTime nowUtc, NormalizedInvoice latest, string restoredRelativePath, CancellationToken cancellationToken);

        Task<IList<StoredInvoice>> ListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Mọi hóa đơn khớp <paramref name="criteria"/>, theo thứ tự đã chọn, ở dạng rút gọn (mã, đường dẫn file, tổng tiền): đủ để đếm,
        /// cộng tổng và kiểm tra file trên đĩa mà không nạp cả bản ghi. Trang hiển thị nạp đầy đủ bằng <see cref="FindManyAsync"/>.
        /// </summary>
        Task<IList<InvoiceFileRef>> ListFileRefsAsync(ArchiveCriteria criteria, CancellationToken cancellationToken);

        /// <summary>Các hóa đơn theo mã, đúng thứ tự <paramref name="ids"/>; mã không tồn tại bị bỏ qua.</summary>
        Task<IList<StoredInvoice>> FindManyAsync(IList<Guid> ids, CancellationToken cancellationToken);

        /// <summary>Các năm lập hóa đơn có dữ liệu của chiều đã chọn, mới nhất trước.</summary>
        Task<IList<int>> ListYearsAsync(InvoiceDirection direction, CancellationToken cancellationToken);

        /// <summary>Số liệu tổng quan tính ngay trong kho dữ liệu (đếm, cộng tổng năm, mã các hóa đơn mới nhất) để Tổng quan không phải nạp cả kho.</summary>
        Task<InvoiceOverview> GetOverviewAsync(int year, int recentCount, CancellationToken cancellationToken);
    }

    /// <summary>Kết quả <see cref="IInvoiceRepository.GetOverviewAsync"/>.</summary>
    public sealed class InvoiceOverview
    {
        public int PurchaseCount { get; set; }
        public int SoldCount { get; set; }

        /// <summary>Tổng thanh toán của các hóa đơn lập trong năm được hỏi.</summary>
        public decimal YearPayment { get; set; }

        /// <summary>Mã các hóa đơn tải về gần nhất, mới nhất trước.</summary>
        public IList<Guid> RecentIds { get; set; }
    }

    /// <summary>Ghi file XML vào thư mục kho: file tạm → kiểm tra → đổi tên atomic (DD-06 mục 6).</summary>
    public interface IInvoiceFileStore
    {
        /// <summary>Ghi byte vào file tạm cùng thư mục đích. <exception cref="System.IO.IOException">Hết dung lượng, bị khóa, không có quyền.</exception></summary>
        Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken);

        /// <summary>Đổi tên file tạm thành file chính thức; nếu trùng tên thì thêm hậu tố. Trả về đường dẫn tương đối cuối cùng.</summary>
        string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName);

        void DiscardTemp(string tempPath);

        /// <summary>File đã commit còn trên đĩa không (kiểm tra tình trạng file của kho, không đọc nội dung).</summary>
        bool Exists(string storageRoot, string relativePath);

        /// <summary>Xóa một file đã commit (khi ghi metadata thất bại ngay sau đó).</summary>
        void Delete(string storageRoot, string relativePath);
    }

    public interface IInvoiceNormalizer
    {
        NormalizedInvoice Normalize(PortalInvoiceDto dto, InvoiceDirection direction);
    }

    /// <summary>Đọc kiểm tra XML hóa đơn: phải là XML đọc được, không DTD (NFR-SEC-003), gốc là <c>HDon</c>.</summary>
    public static class InvoiceXmlValidator
    {
        public static void Validate(byte[] content)
        {
            try
            {
                var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = System.Xml.XmlReader.Create(new System.IO.MemoryStream(content), settings))
                {
                    if (!reader.ReadToFollowing("HDon")) throw new InvoiceStorageException("XML không phải hóa đơn điện tử (thiếu phần tử HDon).");
                    while (reader.Read())
                    {
                    }
                }
            }
            catch (System.Xml.XmlException)
            {
                throw new InvoiceStorageException("XML hóa đơn không hợp lệ hoặc không đọc được.");
            }
        }
    }
}
