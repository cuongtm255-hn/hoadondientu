#if DEBUG
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>
    /// Tác vụ tải giả để thử luồng đóng/chuyển MST khi chưa có DD-05. Chỉ có ở Debug.
    /// Đặt biến môi trường HDDT_DEV_JOB=1 để mô phỏng một tác vụ tải đang chạy.
    /// </summary>
    public sealed class DevelopmentActiveJobService : IActiveJobService
    {
        private volatile bool _running;

        public DevelopmentActiveJobService(bool running)
        {
            _running = running;
        }

        public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_running);
        }

        public async Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            await Task.Delay(2000, cancellationToken).ConfigureAwait(true);
            _running = false;
        }

        public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            _running = false;
            return Task.FromResult(true);
        }

        public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            _running = false;
            return Task.FromResult(true);
        }
    }
}
#endif
