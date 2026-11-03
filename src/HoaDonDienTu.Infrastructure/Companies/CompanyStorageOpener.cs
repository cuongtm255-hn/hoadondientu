using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Infrastructure.Companies
{
    /// <summary>
    /// Mở kho của một MST: thư mục phải còn, có file định danh và khớp đúng MST + mã doanh nghiệp. Không yêu cầu ghi được
    /// (ReadOnlyService vẫn xem dữ liệu cũ). Cập nhật thời điểm mở gần nhất vào danh mục nhưng lỗi ghi cache không làm hỏng việc mở.
    /// </summary>
    public sealed class CompanyStorageOpener : ICompanyStorageOpener
    {
        private readonly ICompanyRegistry _registry;
        private readonly ICompanyStorage _storage;
        private readonly IClock _clock;
        private readonly IApplicationLogger _logger;

        public CompanyStorageOpener(ICompanyRegistry registry, ICompanyStorage storage, IClock clock, IApplicationLogger logger)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (storage == null) throw new ArgumentNullException("storage");
            if (clock == null) throw new ArgumentNullException("clock");
            if (logger == null) throw new ArgumentNullException("logger");
            _registry = registry;
            _storage = storage;
            _clock = clock;
            _logger = logger;
        }

        public async Task<CompanyContext> OpenAsync(HoaDonDienTu.Domain.Companies.CompanyProfile company, CancellationToken cancellationToken)
        {
            var items = await _registry.GetItemsAsync(cancellationToken).ConfigureAwait(true);
            var item = items.FirstOrDefault(i => i.Id.Equals(company.Id));
            if (item == null)
            {
                throw new CompanyStorageException(CompanySwitchBlocker.StorageUnavailable, "Doanh nghiệp không còn trong danh mục.");
            }

            var inspection = _storage.Inspect(item.StoragePath);
            if (!inspection.Exists)
            {
                throw new CompanyStorageException(CompanySwitchBlocker.StorageUnavailable, "Thư mục kho không còn tồn tại: " + item.StoragePath);
            }

            if (!string.IsNullOrEmpty(inspection.IdentityProblem) || !inspection.HasIdentity)
            {
                throw new CompanyStorageException(CompanySwitchBlocker.DatabaseInvalid, "Thư mục không phải kho dữ liệu hợp lệ của doanh nghiệp này.");
            }

            if (!inspection.IdentityTaxCode.Equals(item.TaxCode) || !inspection.IdentityCompanyId.Value.Equals(item.Id))
            {
                throw new CompanyStorageException(CompanySwitchBlocker.DatabaseInvalid, "Kho dữ liệu thuộc MST khác; không mở để tránh lẫn dữ liệu.");
            }

            if (!string.IsNullOrEmpty(inspection.DatabaseProblem))
            {
                throw new CompanyStorageException(CompanySwitchBlocker.DatabaseInvalid, "Cơ sở dữ liệu của kho không dùng được: " + inspection.DatabaseProblem + ".");
            }

            try
            {
                await _registry.UpdateAsync(
                    item.WithCache(_clock.UtcNow, item.LastDownloadedAt, item.CachedInvoiceCount, item.CachedStorageBytes), cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (CompanyCatalogException ex)
            {
                _logger.Warning("Không cập nhật được thời điểm mở gần nhất: " + ex.Kind);
            }

            return new CompanyContext(item.Id, item.TaxCode, item.DisplayName, item.StoragePath, null);
        }
    }
}
