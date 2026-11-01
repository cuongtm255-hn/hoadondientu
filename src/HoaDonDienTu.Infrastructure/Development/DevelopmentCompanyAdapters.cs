#if DEBUG
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>Mở "kho" giả dưới thư mục tạm; không tạo SQLite. Thay bằng implementation DD-02/DD-06.</summary>
    public sealed class DevelopmentCompanyStorageOpener : ICompanyStorageOpener
    {
        public Task<CompanyContext> OpenAsync(CompanyProfile company, CancellationToken cancellationToken)
        {
            var root = Path.Combine(Path.GetTempPath(), "HoaDonDienTu", "dev", company.TaxCode.Value);
            return Task.FromResult(new CompanyContext(company.Id, company.TaxCode, company.DisplayName, root, null));
        }
    }

    /// <summary>Quyền giả theo ma trận DD-03 mục 3.3, tới khi có DD-03.</summary>
    public sealed class DevelopmentEntitlementService : IEntitlementService
    {
        private readonly AppMode _mode;

        public DevelopmentEntitlementService(AppMode mode)
        {
            _mode = mode;
        }

        public AppMode CurrentMode
        {
            get { return _mode; }
        }

        public event EventHandler ModeChanged
        {
            add { }
            remove { }
        }

        public bool IsAllowed(AppFeature feature)
        {
            switch (_mode)
            {
                case AppMode.Standard:
                    return feature != AppFeature.MultiCompany;
                case AppMode.Service:
                    return true;
                default:
                    return feature == AppFeature.ReadExport;
            }
        }

        public void Demand(AppFeature feature)
        {
            if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature);
        }
    }
}
#endif
