#if DEBUG
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Downloads;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>Chưa có bộ điều phối tải (DD-05): chấp nhận kế hoạch và không làm gì. Chỉ có ở Debug.</summary>
    public sealed class PendingDownloadSessionStarter : IDownloadSessionStarter
    {
        public Task StartAsync(DownloadPlan plan, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
#endif
