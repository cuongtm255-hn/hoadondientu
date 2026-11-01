using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Companies
{
    public sealed class CompanyInput
    {
        public string TaxCodeText { get; set; }
        public string DisplayName { get; set; }
        public string StoragePath { get; set; }
    }

    public enum CompanyCheckKey
    {
        TaxCodeFormat,
        TaxCodeUnique,
        DisplayName,
        Folder,
        Licence
    }

    /// <summary>Một dòng trong danh sách "Kiểm tra trước khi tạo" của SCR-11.</summary>
    public sealed class CompanyCheck
    {
        public CompanyCheck(CompanyCheckKey key, bool passed, string message)
        {
            Key = key;
            Passed = passed;
            Message = message;
        }

        public CompanyCheckKey Key { get; private set; }
        public bool Passed { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class CompanyValidation
    {
        public CompanyValidation(IList<CompanyCheck> checks)
        {
            Checks = new List<CompanyCheck>(checks).AsReadOnly();
        }

        public IList<CompanyCheck> Checks { get; private set; }

        public bool IsValid
        {
            get { return Checks.All(c => c.Passed); }
        }

        public string FirstError
        {
            get
            {
                var failed = Checks.FirstOrDefault(c => !c.Passed);
                return failed == null ? null : failed.Message;
            }
        }
    }

    public sealed class CompanyValidationException : Exception
    {
        public CompanyValidationException(CompanyValidation validation)
            : base(validation.FirstError ?? "Thông tin doanh nghiệp không hợp lệ.")
        {
            Validation = validation;
        }

        public CompanyValidation Validation { get; private set; }
    }

    public sealed class CompanyInUseException : Exception
    {
        public CompanyInUseException()
            : base("Doanh nghiệp đang được mở. Hãy chuyển sang doanh nghiệp khác trước khi thực hiện thao tác này.")
        {
        }
    }

    /// <summary>
    /// Thêm, đổi tên, ngừng quản lý và liên kết lại doanh nghiệp (DD-02). Ghi hai pha: chuẩn bị + xác minh kho trước,
    /// rồi mới ghi danh mục; pha sau lỗi thì hoàn tác phần pha đầu đã tạo, không để hồ sơ nửa vời. Quyền được kiểm tra
    /// ở đây chứ không chỉ ở UI (AC-12).
    /// </summary>
    public sealed class CompanyManagementService
    {
        public const long MinFreeBytes = 200L * 1024 * 1024;

        private readonly ICompanyRegistry _registry;
        private readonly ICompanyStorage _storage;
        private readonly ICompanyContextAccessor _context;
        private readonly IEntitlementService _entitlement;
        private readonly IApplicationLogger _logger;

        public CompanyManagementService(
            ICompanyRegistry registry,
            ICompanyStorage storage,
            ICompanyContextAccessor context,
            IEntitlementService entitlement,
            IApplicationLogger logger)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (storage == null) throw new ArgumentNullException("storage");
            if (context == null) throw new ArgumentNullException("context");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (logger == null) throw new ArgumentNullException("logger");
            _registry = registry;
            _storage = storage;
            _context = context;
            _entitlement = entitlement;
            _logger = logger;
        }

        public async Task<CompanyValidation> ValidateAsync(CompanyInput input, CancellationToken cancellationToken)
        {
            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            return Validate(input ?? new CompanyInput(), items);
        }

        public async Task<CompanyCatalogItem> CreateAsync(CompanyInput input, CancellationToken cancellationToken)
        {
            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            RequireAddAllowed(items);

            var validation = Validate(input, items);
            if (!validation.IsValid) throw new CompanyValidationException(validation);

            var taxCode = TaxCode.Parse(input.TaxCodeText);
            var path = NormalizePath(input.StoragePath);
            var inspection = _storage.Inspect(path);

            // Pha 1: chuẩn bị và xác minh kho. Thư mục đã là kho của đúng MST này (đã ngừng quản lý trước đó) thì nhận lại, không ghi đè.
            CompanyId id;
            StorageInitResult initialized = null;
            if (inspection.HasIdentity && inspection.IdentityCompanyId.HasValue)
            {
                id = inspection.IdentityCompanyId.Value;
            }
            else
            {
                id = CompanyId.NewId();
                initialized = _storage.Initialize(path, id, taxCode);
            }

            // Pha 2: ghi danh mục; lỗi thì hoàn tác đúng phần pha 1 đã tạo.
            try
            {
                var item = new CompanyCatalogItem(id, taxCode, input.DisplayName.Trim(), path, null, null, 0, 0, CompanyCatalogStatus.Active);
                await _registry.AddAsync(item, cancellationToken).ConfigureAwait(true);
                return item;
            }
            catch (Exception ex)
            {
                _logger.Error("Ghi danh mục doanh nghiệp thất bại, hoàn tác kho vừa tạo", ex);
                if (initialized != null) _storage.Rollback(path, initialized);
                throw;
            }
        }

        public async Task<CompanyCatalogItem> RenameAsync(CompanyId id, string displayName, CancellationToken cancellationToken)
        {
            _entitlement.Demand(AppFeature.ManageSettings);
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Tên hiển thị không được để trống.");

            var item = await FindAsync(id, cancellationToken).ConfigureAwait(true);
            var renamed = item.WithDisplayName(displayName.Trim());
            await _registry.UpdateAsync(renamed, cancellationToken).ConfigureAwait(true);
            return renamed;
        }

        public async Task UnlinkAsync(CompanyId id, CancellationToken cancellationToken)
        {
            _entitlement.Demand(AppFeature.MultiCompany);
            if (_context.HasCurrent && _context.Current.CompanyId.Equals(id)) throw new CompanyInUseException();

            await FindAsync(id, cancellationToken).ConfigureAwait(true);
            await _registry.RemoveAsync(id, cancellationToken).ConfigureAwait(true);
        }

        public async Task<CompanyCatalogItem> RelinkAsync(CompanyId id, string newPath, CancellationToken cancellationToken)
        {
            _entitlement.Demand(AppFeature.MultiCompany);
            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            var item = items.FirstOrDefault(i => i.Id.Equals(id));
            if (item == null) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "Không tìm thấy doanh nghiệp trong danh mục.");

            var path = NormalizePath(newPath);
            var folder = CheckFolder(path, item.TaxCode, items.Where(i => !i.Id.Equals(id)), true);
            if (!folder.Passed) throw new CompanyValidationException(new CompanyValidation(new[] { folder }));

            var inspection = _storage.Inspect(path);
            if (inspection.IdentityCompanyId.HasValue && !inspection.IdentityCompanyId.Value.Equals(id))
            {
                // Cùng MST nhưng kho thuộc hồ sơ khác: nhận mã của kho để dữ liệu và danh mục khớp nhau.
                _logger.Info("Liên kết lại: kho có mã doanh nghiệp khác, dùng mã của kho");
            }

            var relinked = item.WithStoragePath(path).WithStatus(CompanyCatalogStatus.Active);
            await _registry.UpdateAsync(relinked, cancellationToken).ConfigureAwait(true);
            return relinked;
        }

        /// <summary>Kiểm tra thư mục mới cho việc liên kết lại một doanh nghiệp đã có (không thay đổi gì).</summary>
        public async Task<CompanyValidation> ValidateRelinkAsync(CompanyId id, string newPath, CancellationToken cancellationToken)
        {
            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            var item = items.FirstOrDefault(i => i.Id.Equals(id));
            if (item == null) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "Không tìm thấy doanh nghiệp trong danh mục.");
            return new CompanyValidation(new[] { CheckFolder(NormalizePathOrNull(newPath), item.TaxCode, items.Where(i => !i.Id.Equals(id)), true) });
        }

        // ---- kiểm tra ----

        private CompanyValidation Validate(CompanyInput input, IList<CompanyCatalogItem> items)
        {
            var checks = new List<CompanyCheck>();

            TaxCode taxCode;
            var parsed = TaxCode.TryParse(input.TaxCodeText, out taxCode);
            checks.Add(new CompanyCheck(CompanyCheckKey.TaxCodeFormat, parsed,
                parsed ? "MST hợp lệ" : "MST không hợp lệ (10 chữ số, hoặc 10 chữ số-3 chữ số cho đơn vị phụ thuộc)."));

            if (parsed)
            {
                var existing = items.FirstOrDefault(i => i.TaxCode.Equals(taxCode));
                checks.Add(new CompanyCheck(CompanyCheckKey.TaxCodeUnique, existing == null,
                    existing == null ? "MST chưa tồn tại trong danh mục" : "MST đã có trong danh mục: " + existing.DisplayName + "."));
            }

            var nameOk = !string.IsNullOrWhiteSpace(input.DisplayName) && input.DisplayName.Trim().Length <= 200;
            checks.Add(new CompanyCheck(CompanyCheckKey.DisplayName, nameOk,
                nameOk ? "Tên hiển thị hợp lệ" : "Tên hiển thị không được để trống và tối đa 200 ký tự."));

            checks.Add(parsed
                ? CheckFolder(NormalizePathOrNull(input.StoragePath), taxCode, items, false)
                : new CompanyCheck(CompanyCheckKey.Folder, false, "Cần nhập MST hợp lệ trước khi kiểm tra thư mục."));

            var allowed = items.Count == 0 || _entitlement.IsAllowed(AppFeature.MultiCompany);
            checks.Add(new CompanyCheck(CompanyCheckKey.Licence, allowed,
                allowed ? "Được phép thêm doanh nghiệp ở chế độ hiện tại" : "Chế độ hiện tại chỉ quản lý một doanh nghiệp; cần licence dịch vụ để thêm MST."));

            return new CompanyValidation(checks);
        }

        private CompanyCheck CheckFolder(string path, TaxCode taxCode, IEnumerable<CompanyCatalogItem> others, bool relink)
        {
            if (string.IsNullOrEmpty(path)) return Folder(false, "Thư mục kho chưa hợp lệ (cần đường dẫn đầy đủ, ví dụ D:\\DuLieu\\HDDT\\0101234567).");

            foreach (var other in others)
            {
                var otherPath = NormalizePathOrNull(other.StoragePath);
                if (otherPath != null && Overlaps(path, otherPath))
                {
                    return Folder(false, "Thư mục trùng hoặc lồng với kho của " + other.DisplayName + ". Mỗi MST cần một thư mục riêng.");
                }
            }

            var inspection = _storage.Inspect(path);
            if (!string.IsNullOrEmpty(inspection.IdentityProblem))
            {
                return Folder(false, "Thư mục có file định danh kho nhưng không đọc được: " + inspection.IdentityProblem);
            }

            if (inspection.HasIdentity)
            {
                if (inspection.IdentityTaxCode == null || !inspection.IdentityTaxCode.Equals(taxCode))
                {
                    return Folder(false, "Thư mục là kho dữ liệu của MST " + (inspection.IdentityTaxCode == null ? "khác" : inspection.IdentityTaxCode.Value) + ", không phải " + taxCode.Value + ".");
                }
            }
            else if (relink)
            {
                return Folder(false, "Thư mục không phải kho dữ liệu của MST " + taxCode.Value + " (thiếu file định danh).");
            }
            else if (inspection.Exists && !inspection.IsEmpty)
            {
                return Folder(false, "Thư mục đã có nội dung nhưng không phải kho dữ liệu; hãy chọn thư mục trống.");
            }

            if (!inspection.Writable)
            {
                return Folder(false, "Thư mục không có quyền ghi" + (string.IsNullOrEmpty(inspection.WriteProblem) ? "." : ": " + inspection.WriteProblem));
            }

            if (inspection.FreeBytes.HasValue && inspection.FreeBytes.Value < MinFreeBytes)
            {
                return Folder(false, "Ổ đĩa còn quá ít dung lượng trống (" + FormatBytes(inspection.FreeBytes.Value) + ").");
            }

            var free = inspection.FreeBytes.HasValue ? " và còn trống " + FormatBytes(inspection.FreeBytes.Value) : string.Empty;
            return Folder(true, inspection.HasIdentity ? "Thư mục là kho dữ liệu hiện hữu của MST này" + free : "Thư mục có quyền đọc/ghi" + free);
        }

        private static CompanyCheck Folder(bool ok, string message)
        {
            return new CompanyCheck(CompanyCheckKey.Folder, ok, message);
        }

        private void RequireAddAllowed(IList<CompanyCatalogItem> items)
        {
            // Standard chỉ được khai báo doanh nghiệp đầu tiên; thêm MST thứ hai cần quyền đa doanh nghiệp.
            if (items.Count > 0) _entitlement.Demand(AppFeature.MultiCompany);
        }

        private async Task<CompanyCatalogItem> FindAsync(CompanyId id, CancellationToken cancellationToken)
        {
            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            var item = items.FirstOrDefault(i => i.Id.Equals(id));
            if (item == null) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "Không tìm thấy doanh nghiệp trong danh mục.");
            return item;
        }

        // ---- đường dẫn ----

        public static string NormalizePath(string path)
        {
            var normalized = NormalizePathOrNull(path);
            if (normalized == null) throw new ArgumentException("Đường dẫn không hợp lệ.");
            return normalized;
        }

        private static string NormalizePathOrNull(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathRooted(trimmed)) return null;
                var full = Path.GetFullPath(trimmed);
                var root = Path.GetPathRoot(full);
                return full.Length > root.Length ? full.TrimEnd('\\', '/') : full;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        private static bool Overlaps(string a, string b)
        {
            var sa = a.TrimEnd('\\') + "\\";
            var sb = b.TrimEnd('\\') + "\\";
            return sa.StartsWith(sb, StringComparison.OrdinalIgnoreCase) || sb.StartsWith(sa, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatBytes(long bytes)
        {
            const double gb = 1024.0 * 1024 * 1024;
            const double mb = 1024.0 * 1024;
            return bytes >= gb
                ? (bytes / gb).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " GB"
                : (bytes / mb).ToString("0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " MB";
        }
    }
}
