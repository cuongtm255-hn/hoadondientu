using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Invoices
{
    public enum FileIntegrityFilter
    {
        All,

        /// <summary>Hóa đơn có file XML hiện hành bị thiếu trên đĩa (FR-ARCH-009).</summary>
        Problems,

        /// <summary>Hóa đơn có nhiều phiên bản XML.</summary>
        MultiVersion
    }

    public enum ArchiveSortKey
    {
        IssueDate,
        InvoiceNumber,
        Counterparty,
        TotalPayment
    }

    public enum ArchiveFileState
    {
        Ok,
        Missing,
        MultiVersion
    }

    /// <summary>Điều kiện tra cứu kho. Bộ lọc khớp form tra cứu của cổng (DD-04 mục 5.1a); trống = không lọc.</summary>
    public sealed class ArchiveQuery
    {
        public ArchiveQuery()
        {
            SortKey = ArchiveSortKey.IssueDate;
            SortDescending = true;
            PageSize = 50;
        }

        public InvoiceDirection Direction { get; set; }
        public DateTime? IssueFrom { get; set; }
        public DateTime? IssueTo { get; set; }
        public string StatusCode { get; set; }
        public string CheckResultCode { get; set; }
        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }
        public string InvoiceNumber { get; set; }
        public string CounterpartyTaxCode { get; set; }

        /// <summary>Tìm nhanh trong số hóa đơn, MST/CCCD và tên đối tác (không phân biệt hoa thường và dấu).</summary>
        public string QuickSearch { get; set; }

        public FileIntegrityFilter FileFilter { get; set; }
        public ArchiveSortKey SortKey { get; set; }
        public bool SortDescending { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }

        /// <summary>true bỏ qua bộ nhớ đệm tình trạng file và kiểm tra lại đĩa (nút Kiểm tra file).</summary>
        public bool RefreshFiles { get; set; }

        public ArchiveQuery Clone()
        {
            return (ArchiveQuery)MemberwiseClone();
        }
    }

    public sealed class ArchiveRow
    {
        /// <summary>STT hiển thị theo trang và sắp xếp, không phải khóa (BR-015).</summary>
        public int Number { get; set; }

        public Guid InvoiceId { get; set; }
        public NormalizedInvoice Invoice { get; set; }
        public ArchiveFileState FileState { get; set; }
        public int VersionCount { get; set; }
        public string RelativePath { get; set; }
    }

    public sealed class ArchiveSummary
    {
        public int Count { get; set; }
        public decimal TotalBeforeTax { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalPayment { get; set; }
        public int ProblemCount { get; set; }
    }

    public sealed class ArchivePage
    {
        public IList<ArchiveRow> Rows { get; set; }
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public ArchiveSummary Summary { get; set; }

        public int PageCount
        {
            get { return PageSize <= 0 ? 1 : Math.Max(1, (TotalCount + PageSize - 1) / PageSize); }
        }
    }

    public interface IInvoiceArchiveService
    {
        /// <summary>Tra cứu kho của doanh nghiệp đang mở; PageIndex quá cuối được kéo về trang cuối.</summary>
        /// <exception cref="StorageGuardException">Chưa mở doanh nghiệp nào.</exception>
        Task<ArchivePage> QueryAsync(ArchiveQuery query, CancellationToken cancellationToken);

        /// <summary>Các năm lập hóa đơn có dữ liệu của chiều đã chọn, mới nhất trước.</summary>
        Task<IList<int>> YearsAsync(InvoiceDirection direction, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Tra cứu kho trên metadata đã lưu (SQL ở DD-06/G-08 sẽ thay phần lọc trong bộ nhớ). Không nạp XML vào bộ nhớ: tình trạng file
    /// chỉ kiểm tra sự tồn tại của file trên đĩa.
    /// </summary>
    public sealed class InvoiceArchiveService : IInvoiceArchiveService
    {
        private readonly IInvoiceRepository _repository;
        private readonly IInvoiceFileStore _files;
        private readonly ICompanyContextAccessor _context;
        private readonly object _cacheGate = new object();
        private readonly Dictionary<string, CachedExistence> _existence = new Dictionary<string, CachedExistence>(StringComparer.OrdinalIgnoreCase);

        private sealed class CachedExistence
        {
            public bool Exists;
            public DateTime CheckedAtUtc;
        }

        /// <summary>Gõ tìm kiếm liên tục không được kéo theo hàng chục nghìn lần kiểm tra đĩa: kết quả tồn tại file được nhớ ngắn hạn.</summary>
        private static readonly TimeSpan ExistenceTtl = TimeSpan.FromSeconds(10);

        private const int ExistenceCacheLimit = 200000;

        public InvoiceArchiveService(IInvoiceRepository repository, IInvoiceFileStore files, ICompanyContextAccessor context)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (files == null) throw new ArgumentNullException("files");
            if (context == null) throw new ArgumentNullException("context");
            _repository = repository;
            _files = files;
            _context = context;
        }

        public async Task<ArchivePage> QueryAsync(ArchiveQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException("query");
            var root = RequireRoot();
            var refresh = query.RefreshFiles;

            // Lọc và sắp xếp ở repository; ở đây chỉ còn kiểm tra file trên đĩa (không có trong cơ sở dữ liệu) và nạp đầy đủ đúng một trang.
            var refs = await _repository.ListFileRefsAsync(ArchiveCriteria.From(query), cancellationToken).ConfigureAwait(false);
            var states = new List<ArchiveFileState>(refs.Count);
            foreach (var r in refs) states.Add(StateOf(root, r, refresh));

            var matched = new List<InvoiceFileRef>(refs.Count);
            var matchedStates = new List<ArchiveFileState>(refs.Count);
            for (var n = 0; n < refs.Count; n++)
            {
                if (!FileMatches(states[n], query.FileFilter)) continue;
                matched.Add(refs[n]);
                matchedStates.Add(states[n]);
            }

            var pageSize = Math.Max(1, query.PageSize);
            var pageCount = Math.Max(1, (matched.Count + pageSize - 1) / pageSize);
            var pageIndex = Math.Max(0, Math.Min(query.PageIndex, pageCount - 1));
            var first = pageIndex * pageSize;
            var take = Math.Min(pageSize, matched.Count - first);

            var ids = new List<Guid>(take);
            for (var n = 0; n < take; n++) ids.Add(matched[first + n].InvoiceId);
            var slice = take > 0 ? await _repository.FindManyAsync(ids, cancellationToken).ConfigureAwait(false) : new List<StoredInvoice>();

            var rows = new List<ArchiveRow>(slice.Count);
            foreach (var inv in slice)
            {
                var position = ids.IndexOf(inv.InvoiceId);
                rows.Add(new ArchiveRow
                {
                    Number = first + position + 1,
                    InvoiceId = inv.InvoiceId,
                    Invoice = inv.Invoice,
                    FileState = matchedStates[first + position],
                    VersionCount = inv.Versions.Count,
                    RelativePath = inv.Current == null ? null : inv.Current.RelativePath
                });
            }

            var summary = new ArchiveSummary { Count = matched.Count };
            for (var n = 0; n < matched.Count; n++)
            {
                summary.TotalBeforeTax += matched[n].TotalBeforeTax;
                summary.TotalTax += matched[n].TotalTax;
                summary.TotalPayment += matched[n].TotalPayment;
                if (matchedStates[n] == ArchiveFileState.Missing) summary.ProblemCount++;
            }

            return new ArchivePage { Rows = rows, TotalCount = matched.Count, PageIndex = pageIndex, PageSize = pageSize, Summary = summary };
        }

        public Task<IList<int>> YearsAsync(InvoiceDirection direction, CancellationToken cancellationToken)
        {
            RequireRoot();
            return _repository.ListYearsAsync(direction, cancellationToken);
        }

        private string RequireRoot()
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            return _context.Current.StorageRoot;
        }

        private ArchiveFileState StateOf(string root, InvoiceFileRef invoice, bool refresh)
        {
            if (invoice.RelativePath == null || !FileExists(root, invoice.RelativePath, refresh)) return ArchiveFileState.Missing;
            return invoice.VersionCount > 1 ? ArchiveFileState.MultiVersion : ArchiveFileState.Ok;
        }

        private bool FileExists(string root, string relativePath, bool refresh)
        {
            var key = root + "|" + relativePath;
            var now = DateTime.UtcNow;
            lock (_cacheGate)
            {
                CachedExistence cached;
                if (!refresh && _existence.TryGetValue(key, out cached) && now - cached.CheckedAtUtc < ExistenceTtl) return cached.Exists;
            }

            var exists = _files.Exists(root, relativePath);
            lock (_cacheGate)
            {
                if (_existence.Count >= ExistenceCacheLimit) _existence.Clear();
                _existence[key] = new CachedExistence { Exists = exists, CheckedAtUtc = now };
            }

            return exists;
        }

        private static bool FileMatches(ArchiveFileState state, FileIntegrityFilter filter)
        {
            switch (filter)
            {
                case FileIntegrityFilter.Problems: return state == ArchiveFileState.Missing;
                case FileIntegrityFilter.MultiVersion: return state == ArchiveFileState.MultiVersion;
                default: return true;
            }
        }

        /// <summary>Chữ thường, bỏ dấu tiếng Việt (kể cả đ) để "cong ty" tìm được "CÔNG TY".</summary>
        public static string Fold(string value)
        {
            return ArchiveKeys.Fold(value);
        }
    }

    /// <summary>Metadata theo từng doanh nghiệp: mỗi MST một kho riêng, đổi MST không lẫn dữ liệu (BR-018).</summary>
    public sealed class CompanyScopedInvoiceRepository : IInvoiceRepository
    {
        private readonly ICompanyContextAccessor _context;
        private readonly Func<IInvoiceRepository> _factory;
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, IInvoiceRepository> _byCompany = new Dictionary<Guid, IInvoiceRepository>();

        public CompanyScopedInvoiceRepository(ICompanyContextAccessor context, Func<IInvoiceRepository> factory)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (factory == null) throw new ArgumentNullException("factory");
            _context = context;
            _factory = factory;
        }

        public Task<StoredInvoice> FindAsync(string businessKey, CancellationToken cancellationToken)
        {
            return Current().FindAsync(businessKey, cancellationToken);
        }

        public Task<StoredInvoice> FindByIdAsync(Guid invoiceId, CancellationToken cancellationToken)
        {
            return Current().FindByIdAsync(invoiceId, cancellationToken);
        }

        public Task AddAsync(StoredInvoice invoice, CancellationToken cancellationToken)
        {
            return Current().AddAsync(invoice, cancellationToken);
        }

        public Task AddVersionAsync(Guid invoiceId, InvoiceVersionRecord version, NormalizedInvoice latest, DateTime nowUtc, CancellationToken cancellationToken)
        {
            return Current().AddVersionAsync(invoiceId, version, latest, nowUtc, cancellationToken);
        }

        public Task TouchAsync(Guid invoiceId, DateTime nowUtc, NormalizedInvoice latest, string restoredRelativePath, CancellationToken cancellationToken)
        {
            return Current().TouchAsync(invoiceId, nowUtc, latest, restoredRelativePath, cancellationToken);
        }

        public Task<IList<StoredInvoice>> ListAsync(CancellationToken cancellationToken)
        {
            return Current().ListAsync(cancellationToken);
        }

        public Task<IList<InvoiceFileRef>> ListFileRefsAsync(ArchiveCriteria criteria, CancellationToken cancellationToken)
        {
            return Current().ListFileRefsAsync(criteria, cancellationToken);
        }

        public Task<IList<StoredInvoice>> FindManyAsync(IList<Guid> ids, CancellationToken cancellationToken)
        {
            return Current().FindManyAsync(ids, cancellationToken);
        }

        public Task<IList<int>> ListYearsAsync(InvoiceDirection direction, CancellationToken cancellationToken)
        {
            return Current().ListYearsAsync(direction, cancellationToken);
        }

        private IInvoiceRepository Current()
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var id = _context.Current.CompanyId.Value;
            lock (_gate)
            {
                IInvoiceRepository repo;
                if (!_byCompany.TryGetValue(id, out repo))
                {
                    repo = _factory();
                    _byCompany[id] = repo;
                }

                return repo;
            }
        }
    }
}
