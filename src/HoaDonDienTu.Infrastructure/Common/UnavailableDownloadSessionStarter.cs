using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Downloads;

namespace HoaDonDienTu.Infrastructure.Common
{
    /// <summary>
    /// Bản production chưa có bộ điều phối tải (DD-05): từ chối rõ ràng thay vì giả vờ đã bắt đầu tải.
    /// </summary>
    public sealed class UnavailableDownloadSessionStarter : IDownloadSessionStarter
    {
        public Task<System.Guid> StartAsync(DownloadPlan plan, CancellationToken cancellationToken)
        {
            throw new FeatureNotAvailableException("Chức năng tải hóa đơn chưa có trong phiên bản này.");
        }
    }
}
