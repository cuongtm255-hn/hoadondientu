using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>Contract theo DD-03 mục 3.2; kích hoạt/hủy kích hoạt sẽ bổ sung khi implement DD-03.</summary>
    public interface ILicenseService
    {
        Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken);
        Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken);
    }
}
