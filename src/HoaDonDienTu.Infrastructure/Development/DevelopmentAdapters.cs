#if DEBUG
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>Licence giả cho môi trường phát triển (cho tới khi có DD-03). Chỉ có ở Debug.</summary>
    public sealed class DevelopmentLicenseService : ILicenseService
    {
        private readonly LicenseStatus _status;

        public DevelopmentLicenseService(LicenseStatus status)
        {
            _status = status;
        }

        public Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_status);
        }

        public Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_status);
        }

        public Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(_status);
        }

        public Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LicenseStatus.None);
        }

        public LicenseInfo CurrentInfo
        {
            get { return new LicenseInfo(_status, LicenseIssue.None, "Development", string.Empty, null, null, null); }
        }

        public event EventHandler InfoChanged
        {
            add { }
            remove { }
        }
    }

    /// <summary>Catalog giả cho môi trường phát triển (cho tới khi có DD-02). Chỉ có ở Debug.</summary>
    public sealed class DevelopmentCompanyCatalog : ICompanyCatalog
    {
        private readonly CatalogSnapshot _snapshot;

        public DevelopmentCompanyCatalog(CatalogSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshot);
        }

        public static CompanyProfile SampleCompany(string name, string taxCode)
        {
            return new CompanyProfile(CompanyId.NewId(), name, TaxCode.Parse(taxCode));
        }

        public static IList<CompanyProfile> SampleCompanies(int count)
        {
            var list = new List<CompanyProfile>();
            for (var i = 0; i < count; i++)
            {
                list.Add(SampleCompany("CÔNG TY MẪU " + (i + 1), "01012345" + (60 + i).ToString("00")));
            }

            return list;
        }
    }
}
#endif
