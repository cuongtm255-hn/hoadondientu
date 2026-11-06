using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    /// <summary>Một thư mục kho tìm thấy khi quét (có file định danh hợp lệ).</summary>
    public sealed class RecoveredStore
    {
        public string Path { get; set; }
        public TaxCode TaxCode { get; set; }
        public CompanyId CompanyId { get; set; }

        /// <summary>Đã có trong danh mục hiện tại (cùng mã doanh nghiệp hoặc cùng thư mục): không đăng ký lại.</summary>
        public bool AlreadyRegistered { get; set; }

        /// <summary>Lý do không thể đăng ký (ví dụ trùng MST với kho khác); null nếu đăng ký được.</summary>
        public string Problem { get; set; }

        public bool CanRegister
        {
            get { return !AlreadyRegistered && Problem == null; }
        }
    }

    public sealed class RecoveryScanResult
    {
        public IList<RecoveredStore> Stores { get; set; }

        /// <summary>Thư mục có file định danh nhưng không dùng được, kèm lý do; không phải thư mục thường.</summary>
        public IList<string> Skipped { get; set; }
    }

    public sealed class RecoveryResult
    {
        public int Registered { get; set; }
        public IList<string> NotRegistered { get; set; }

        /// <summary>File danh mục hỏng đã được đổi tên để giữ lại (null nếu danh mục không hỏng).</summary>
        public string QuarantinedCatalog { get; set; }
    }

    /// <summary>Đọc định danh của một thư mục kho mà không ghi gì vào đó (quét hàng loạt thư mục lạ không được tạo file thử).</summary>
    public interface ICompanyStorageScanner
    {
        /// <summary>Chỉ điền <c>Exists</c>, <c>HasIdentity</c>, <c>IdentityTaxCode</c>, <c>IdentityCompanyId</c> và <c>IdentityProblem</c>.</summary>
        StorageInspection ReadIdentity(string path);
    }

    /// <summary>Sửa danh mục hỏng mà không làm mất bản hỏng.</summary>
    public interface ICompanyCatalogRepair
    {
        /// <summary>Nếu danh mục hỏng (không có cả bản .bak hợp lệ) thì đổi tên file hỏng để giữ lại và trả về đường dẫn mới; ngược lại trả về null.</summary>
        Task<string> QuarantineCorruptAsync(CancellationToken cancellationToken);
    }

    public interface ICompanyRecoveryService
    {
        /// <summary>Quét các thư mục (và thư mục con trực tiếp của chúng) tìm kho có file định danh hợp lệ. Chỉ đọc.</summary>
        Task<RecoveryScanResult> ScanAsync(IEnumerable<string> folders, CancellationToken cancellationToken);

        /// <summary>Đăng ký các kho đăng ký được vào danh mục (tạo danh mục mới nếu danh mục cũ hỏng, giữ bản hỏng). Không đụng tới dữ liệu trong kho.</summary>
        Task<RecoveryResult> RebuildAsync(IEnumerable<RecoveredStore> stores, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Khôi phục danh mục doanh nghiệp (T02-07, NFR-REL-007): <c>companies.json</c> mất/hỏng không được tạo kho rỗng đè lên dữ liệu;
    /// người dùng quét thư mục dữ liệu và đăng ký lại các kho hiện hữu. Tên hiển thị không có trong file định danh nên đặt tạm
    /// theo MST, đổi lại ở màn Doanh nghiệp.
    /// </summary>
    public sealed class CompanyRecoveryService : ICompanyRecoveryService
    {
        private readonly ICompanyRegistry _registry;
        private readonly ICompanyStorageScanner _scanner;
        private readonly ICompanyCatalogRepair _repair;
        private readonly IApplicationLogger _logger;

        public CompanyRecoveryService(ICompanyRegistry registry, ICompanyStorageScanner scanner, ICompanyCatalogRepair repair, IApplicationLogger logger)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (scanner == null) throw new ArgumentNullException("scanner");
            if (repair == null) throw new ArgumentNullException("repair");
            if (logger == null) throw new ArgumentNullException("logger");
            _registry = registry;
            _scanner = scanner;
            _repair = repair;
            _logger = logger;
        }

        public async Task<RecoveryScanResult> ScanAsync(IEnumerable<string> folders, CancellationToken cancellationToken)
        {
            var registered = await TryGetRegisteredAsync(cancellationToken).ConfigureAwait(true);
            var stores = new List<RecoveredStore>();
            var skipped = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in (folders ?? new string[0]).Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                foreach (var candidate in Candidates(root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string full;
                    try
                    {
                        full = System.IO.Path.GetFullPath(candidate).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    if (!seen.Add(full)) continue;
                    var identity = _scanner.ReadIdentity(full);
                    if (!string.IsNullOrEmpty(identity.IdentityProblem))
                    {
                        skipped.Add(full + ": " + identity.IdentityProblem);
                        continue;
                    }

                    if (!identity.HasIdentity) continue;

                    var store = new RecoveredStore { Path = full, TaxCode = identity.IdentityTaxCode, CompanyId = identity.IdentityCompanyId.Value };
                    store.AlreadyRegistered = registered.Any(r => r.Id.Equals(store.CompanyId) || string.Equals(Normalize(r.StoragePath), full, StringComparison.OrdinalIgnoreCase));
                    stores.Add(store);
                }
            }

            // Hai thư mục cùng MST: chỉ một đăng ký được (danh mục không cho trùng MST); thư mục đã đăng ký thắng.
            foreach (var group in stores.GroupBy(s => s.TaxCode.Value))
            {
                var winner = group.FirstOrDefault(s => s.AlreadyRegistered) ?? group.First();
                foreach (var other in group.Where(s => !ReferenceEquals(s, winner)))
                {
                    other.Problem = "Trùng MST " + other.TaxCode.Value + " với kho " + winner.Path + ".";
                }
            }

            // Cùng MST đã có trong danh mục nhưng ở thư mục khác: không tự thay thế.
            foreach (var store in stores.Where(s => !s.AlreadyRegistered && s.Problem == null))
            {
                var same = registered.FirstOrDefault(r => r.TaxCode.Equals(store.TaxCode));
                if (same != null) store.Problem = "MST " + store.TaxCode.Value + " đã có trong danh mục ở thư mục khác (" + same.StoragePath + ").";
            }

            return new RecoveryScanResult { Stores = stores, Skipped = skipped };
        }

        public async Task<RecoveryResult> RebuildAsync(IEnumerable<RecoveredStore> stores, CancellationToken cancellationToken)
        {
            var result = new RecoveryResult { NotRegistered = new List<string>() };

            var corrupt = false;
            try
            {
                await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (CompanyCatalogException ex) when (ex.Kind == CompanyCatalogErrorKind.CatalogCorrupt)
            {
                corrupt = true;
            }

            if (corrupt)
            {
                result.QuarantinedCatalog = await _repair.QuarantineCorruptAsync(cancellationToken).ConfigureAwait(true);
                if (result.QuarantinedCatalog != null) _logger.Warning("Danh mục doanh nghiệp hỏng được giữ lại trước khi dựng danh mục mới");
            }

            foreach (var store in (stores ?? new RecoveredStore[0]))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!store.CanRegister)
                {
                    result.NotRegistered.Add(store.Path + ": " + (store.AlreadyRegistered ? "đã có trong danh mục" : store.Problem));
                    continue;
                }

                try
                {
                    var item = new CompanyCatalogItem(
                        store.CompanyId, store.TaxCode, "Doanh nghiệp " + store.TaxCode.Value, store.Path, null, null, 0, 0, CompanyCatalogStatus.Active);
                    await _registry.AddAsync(item, cancellationToken).ConfigureAwait(true);
                    result.Registered++;
                }
                catch (CompanyCatalogException ex) when (ex.Kind == CompanyCatalogErrorKind.DuplicateTaxCode)
                {
                    result.NotRegistered.Add(store.Path + ": MST đã có trong danh mục");
                }
            }

            _logger.Info("Khôi phục danh mục doanh nghiệp: đăng ký " + result.Registered + " kho");
            return result;
        }

        private async Task<IList<CompanyCatalogItem>> TryGetRegisteredAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (CompanyCatalogException ex) when (ex.Kind == CompanyCatalogErrorKind.CatalogCorrupt)
            {
                return new List<CompanyCatalogItem>();
            }
        }

        /// <summary>Chính thư mục đã chọn và các thư mục con trực tiếp của nó.</summary>
        private static IEnumerable<string> Candidates(string root)
        {
            yield return root;
            string[] children;
            try
            {
                children = Directory.Exists(root) ? Directory.GetDirectories(root) : new string[0];
            }
            catch (IOException)
            {
                children = new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                children = new string[0];
            }

            foreach (var child in children) yield return child;
        }

        private static string Normalize(string path)
        {
            try
            {
                return System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            }
            catch (ArgumentException)
            {
                return path;
            }
        }
    }
}
