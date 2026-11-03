using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>
    /// Lưu một hóa đơn vào kho của đúng MST (DD-06 mục 6): chuẩn hóa → kiểm tra XML → băm SHA-256 → so khóa chống trùng →
    /// ghi file tạm → đổi tên atomic → ghi metadata. Hash trùng chỉ cập nhật thời điểm thấy (BR-009); hash khác tạo phiên bản mới và giữ bản cũ (BR-010).
    /// Lỗi ở bất kỳ bước nào thì không để lại file chính thức mồ côi và không báo thành công (FR-STORE-010).
    /// </summary>
    public sealed class InvoiceStorageService : IInvoiceStore
    {
        private readonly IInvoiceRepository _repository;
        private readonly IInvoiceFileStore _files;
        private readonly IInvoiceNormalizer _normalizer;
        private readonly ICompanyContextAccessor _context;
        private readonly IClock _clock;
        private readonly IApplicationLogger _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public InvoiceStorageService(
            IInvoiceRepository repository,
            IInvoiceFileStore files,
            IInvoiceNormalizer normalizer,
            ICompanyContextAccessor context,
            IClock clock,
            IApplicationLogger logger)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (files == null) throw new ArgumentNullException("files");
            if (normalizer == null) throw new ArgumentNullException("normalizer");
            if (context == null) throw new ArgumentNullException("context");
            if (clock == null) throw new ArgumentNullException("clock");
            if (logger == null) throw new ArgumentNullException("logger");
            _repository = repository;
            _files = files;
            _normalizer = normalizer;
            _context = context;
            _clock = clock;
            _logger = logger;
        }

        /// <exception cref="StorageGuardException">Phiên tải không thuộc doanh nghiệp/kho đang mở (NFR-SEC-006).</exception>
        /// <exception cref="InvoiceStorageException">XML hỏng hoặc đường dẫn quá dài; hóa đơn bị từ chối.</exception>
        public async Task<InvoiceSaveResult> UpsertAsync(DownloadSession session, PortalInvoiceDto dto, InvoiceXmlPayload xml, CancellationToken cancellationToken)
        {
            if (session == null) throw new ArgumentNullException("session");
            if (dto == null) throw new ArgumentNullException("dto");
            if (xml == null) throw new ArgumentNullException("xml");
            Guard(session);

            var invoice = _normalizer.Normalize(dto, session.Direction);
            InvoiceXmlValidator.Validate(xml.Content);
            var sha = Hex(xml.Content);
            var key = BusinessKey.Build(invoice);

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = _clock.UtcNow;
                var existing = await _repository.FindAsync(key, cancellationToken).ConfigureAwait(false);
                if (existing != null && existing.Current != null && string.Equals(existing.Current.Sha256, sha, StringComparison.OrdinalIgnoreCase))
                {
                    var restored = await RestoreIfMissingAsync(session, existing, xml.Content, cancellationToken).ConfigureAwait(false);
                    await _repository.TouchAsync(existing.InvoiceId, now, invoice, restored, cancellationToken).ConfigureAwait(false);

                    // File vừa được ghi lại nghĩa là kho có thêm dữ liệu thật, không phải bản trùng.
                    return restored == null ? InvoiceSaveResult.AlreadyExists : InvoiceSaveResult.Saved;
                }

                var versionNo = existing == null ? 1 : existing.Versions.Max(v => v.VersionNo) + 1;
                var folder = InvoiceFileNamer.RelativeFolder(invoice);
                var fileName = InvoiceFileNamer.FileName(invoice, versionNo);
                if (!InvoiceFileNamer.FitsPathLimit(session.StorageRoot, System.IO.Path.Combine(folder, fileName)))
                {
                    throw new InvoiceStorageException("Đường dẫn kho quá dài để lưu hóa đơn này.");
                }

                var temp = await _files.WriteTempAsync(session.StorageRoot, folder, xml.Content, cancellationToken).ConfigureAwait(false);
                string relativePath;
                try
                {
                    relativePath = _files.Commit(session.StorageRoot, temp, folder, fileName);
                }
                catch
                {
                    _files.DiscardTemp(temp);
                    throw;
                }

                var version = new InvoiceVersionRecord
                {
                    VersionId = Guid.NewGuid(),
                    VersionNo = versionNo,
                    Sha256 = sha,
                    RelativePath = relativePath,
                    SizeBytes = xml.Content.LongLength,
                    CreatedAtUtc = now,
                    IsCurrent = true,
                    SourceSessionId = session.Id
                };

                try
                {
                    if (existing == null)
                    {
                        await _repository.AddAsync(new StoredInvoice
                        {
                            InvoiceId = Guid.NewGuid(),
                            BusinessKey = key,
                            Invoice = invoice,
                            FirstSeenAtUtc = now,
                            LastSeenAtUtc = now,
                            Versions = new List<InvoiceVersionRecord> { version }
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _repository.AddVersionAsync(existing.InvoiceId, version, invoice, now, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Ghi metadata thất bại: bỏ file vừa đổi tên để không có file mồ côi không ai biết tới.
                    _files.Delete(session.StorageRoot, relativePath);
                    throw;
                }

                return InvoiceSaveResult.Saved;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Hóa đơn đã có nhưng file hiện hành không còn trên đĩa: ghi lại đúng nội dung (hash trùng) vào chính vị trí cũ.</summary>
        private async Task<string> RestoreIfMissingAsync(DownloadSession session, StoredInvoice existing, byte[] content, CancellationToken cancellationToken)
        {
            var current = existing.Current;
            if (_files.Exists(session.StorageRoot, current.RelativePath)) return null;

            var folder = System.IO.Path.GetDirectoryName(current.RelativePath) ?? string.Empty;
            var temp = await _files.WriteTempAsync(session.StorageRoot, folder, content, cancellationToken).ConfigureAwait(false);
            try
            {
                return _files.Commit(session.StorageRoot, temp, folder, System.IO.Path.GetFileName(current.RelativePath));
            }
            catch
            {
                _files.DiscardTemp(temp);
                throw;
            }
        }

        private void Guard(DownloadSession session)
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var company = _context.Current;
            if (!company.CompanyId.Equals(session.CompanyId)
                || !string.Equals(company.StorageRoot, session.StorageRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new StorageGuardException("Phiên tải không thuộc kho của doanh nghiệp đang mở.");
            }
        }

        private static string Hex(byte[] content)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(content);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>Metadata trong bộ nhớ: dùng cho test và tới khi có SQLite (G-08). Trả bản sao khi đọc để mô phỏng lưu bền vững.</summary>
    public sealed class InMemoryInvoiceRepository : IInvoiceRepository
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, StoredInvoice> _byKey = new Dictionary<string, StoredInvoice>(StringComparer.Ordinal);
        private readonly Dictionary<Guid, IntegrityProblemKind> _problems = new Dictionary<Guid, IntegrityProblemKind>();
        private readonly Dictionary<Guid, DateTime> _checkedAt = new Dictionary<Guid, DateTime>();

        public Task SaveIntegrityAsync(IDictionary<Guid, IntegrityProblemKind?> results, DateTime checkedAtUtc, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                foreach (var result in results)
                {
                    _checkedAt[result.Key] = checkedAtUtc;
                    if (result.Value.HasValue) _problems[result.Key] = result.Value.Value;
                    else _problems.Remove(result.Key);
                }
            }

            return Task.FromResult(0);
        }

        public Task<StoredInvoice> FindAsync(string businessKey, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                StoredInvoice found;
                return Task.FromResult(_byKey.TryGetValue(businessKey, out found) ? Copy(found) : null);
            }
        }

        public Task<StoredInvoice> FindByIdAsync(Guid invoiceId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var found = _byKey.Values.FirstOrDefault(i => i.InvoiceId == invoiceId);
                return Task.FromResult(found == null ? null : Copy(found));
            }
        }

        public Task AddAsync(StoredInvoice invoice, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_byKey.ContainsKey(invoice.BusinessKey)) throw new InvalidOperationException("Khóa nghiệp vụ đã tồn tại.");
                _byKey[invoice.BusinessKey] = Copy(invoice);
            }

            return Task.FromResult(0);
        }

        public Task AddVersionAsync(Guid invoiceId, InvoiceVersionRecord version, NormalizedInvoice latest, DateTime nowUtc, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var stored = _byKey.Values.First(i => i.InvoiceId == invoiceId);
                foreach (var v in stored.Versions) v.IsCurrent = false;
                stored.Versions.Add(CopyVersion(version));
                stored.Invoice = latest;
                stored.LastSeenAtUtc = nowUtc;
                _problems.Remove(invoiceId);
            }

            return Task.FromResult(0);
        }

        public Task TouchAsync(Guid invoiceId, DateTime nowUtc, NormalizedInvoice latest, string restoredRelativePath, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var stored = _byKey.Values.First(i => i.InvoiceId == invoiceId);
                stored.LastSeenAtUtc = nowUtc;
                if (latest != null) stored.Invoice = latest;
                if (restoredRelativePath != null && stored.Current != null)
                {
                    stored.Current.RelativePath = restoredRelativePath;
                    _problems.Remove(invoiceId);
                }
            }

            return Task.FromResult(0);
        }

        public Task<IList<StoredInvoice>> ListAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IList<StoredInvoice> list = _byKey.Values.Select(Copy).ToList();
                return Task.FromResult(list);
            }
        }

        public Task<IList<InvoiceFileRef>> ListFileRefsAsync(ArchiveCriteria criteria, CancellationToken cancellationToken)
        {
            Dictionary<Guid, IntegrityProblemKind> problems;
            Dictionary<Guid, DateTime> checkedAt;
            lock (_gate)
            {
                problems = new Dictionary<Guid, IntegrityProblemKind>(_problems);
                checkedAt = new Dictionary<Guid, DateTime>(_checkedAt);
            }

            IList<InvoiceFileRef> refs = Matched(criteria).Select(
                s => new InvoiceFileRef
                {
                    InvoiceId = s.InvoiceId,
                    RelativePath = s.Current == null ? null : s.Current.RelativePath,
                    VersionCount = s.Versions.Count,
                    StoredProblem = problems.ContainsKey(s.InvoiceId) ? problems[s.InvoiceId] : (IntegrityProblemKind?)null,
                    CheckedAtUtc = checkedAt.ContainsKey(s.InvoiceId) ? checkedAt[s.InvoiceId] : (DateTime?)null,
                    TotalBeforeTax = s.Invoice.TotalBeforeTax ?? 0m,
                    TotalTax = s.Invoice.TotalTax ?? 0m,
                    TotalPayment = s.Invoice.TotalPayment ?? 0m
                }).ToList();
            return Task.FromResult(refs);
        }

        public Task<IList<StoredInvoice>> FindManyAsync(IList<Guid> ids, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var byId = _byKey.Values.ToDictionary(s => s.InvoiceId);
                IList<StoredInvoice> found = ids.Where(byId.ContainsKey).Select(id => Copy(byId[id])).ToList();
                return Task.FromResult(found);
            }
        }

        private static decimal YearVnd(IEnumerable<StoredInvoice> all, InvoiceDirection direction, int year)
        {
            return all
                .Where(s => s.Invoice.Direction == direction && s.Invoice.IssueDate.Year == year
                    && (string.IsNullOrEmpty(s.Invoice.Currency) || string.Equals(s.Invoice.Currency, "VND", StringComparison.Ordinal)))
                .Sum(s => s.Invoice.TotalPayment ?? 0m);
        }

        public Task<InvoiceOverview> GetOverviewAsync(int year, int recentCount, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var all = _byKey.Values.ToList();
                return Task.FromResult(
                    new InvoiceOverview
                    {
                        PurchaseCount = all.Count(s => s.Invoice.Direction == InvoiceDirection.Purchase),
                        SoldCount = all.Count(s => s.Invoice.Direction == InvoiceDirection.Sold),
                        YearPaymentPurchase = YearVnd(all, InvoiceDirection.Purchase, year),
                        YearPaymentSold = YearVnd(all, InvoiceDirection.Sold, year),
                        RecentIds = all
                            .OrderByDescending(s => s.FirstSeenAtUtc)
                            .ThenByDescending(s => s.Invoice.IssueDate)
                            .ThenBy(s => s.InvoiceId.ToString("D"), StringComparer.Ordinal)
                            .Take(recentCount)
                            .Select(s => s.InvoiceId)
                            .ToList()
                    });
            }
        }

        public Task<IList<int>> ListYearsAsync(InvoiceDirection direction, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IList<int> years = _byKey.Values.Where(s => s.Invoice.Direction == direction).Select(s => s.Invoice.IssueDate.Year).Distinct().OrderByDescending(y => y).ToList();
                return Task.FromResult(years);
            }
        }

        private List<StoredInvoice> Matched(ArchiveCriteria criteria)
        {
            lock (_gate)
            {
                var matched = _byKey.Values.Where(criteria.Matches).ToList();
                matched.Sort(criteria.Compare);
                return matched;
            }
        }

        private static StoredInvoice Copy(StoredInvoice s)
        {
            return new StoredInvoice
            {
                InvoiceId = s.InvoiceId,
                BusinessKey = s.BusinessKey,
                Invoice = s.Invoice,
                FirstSeenAtUtc = s.FirstSeenAtUtc,
                LastSeenAtUtc = s.LastSeenAtUtc,
                Versions = s.Versions.Select(CopyVersion).ToList()
            };
        }

        private static InvoiceVersionRecord CopyVersion(InvoiceVersionRecord v)
        {
            return new InvoiceVersionRecord
            {
                VersionId = v.VersionId, VersionNo = v.VersionNo, Sha256 = v.Sha256, RelativePath = v.RelativePath,
                SizeBytes = v.SizeBytes, CreatedAtUtc = v.CreatedAtUtc, IsCurrent = v.IsCurrent, SourceSessionId = v.SourceSessionId
            };
        }
    }
}
