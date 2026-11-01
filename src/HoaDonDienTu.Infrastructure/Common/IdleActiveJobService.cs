using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Infrastructure.Common
{
    /// <summary>Báo không có phiên tải nào đang chạy. Dùng cho tới khi engine tải (DD-05) có mặt; không bao giờ chặn việc chuyển MST hay đóng ứng dụng.</summary>
    public sealed class IdleActiveJobService : IActiveJobService
    {
        public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
