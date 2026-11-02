#if DEBUG
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>Bản Debug không bao giờ gọi cổng Thuế thật (ADR 0005): trả kết quả mô phỏng, ghi rõ là giả.</summary>
    public sealed class DevelopmentConnectivityChecker : IConnectivityChecker
    {
        public Task<IList<ConnectivityResult>> CheckAsync(CancellationToken cancellationToken)
        {
            IList<ConnectivityResult> results = new List<ConnectivityResult>
            {
                new ConnectivityResult { Name = "Cổng Thuế", Reachable = true, Detail = "Dữ liệu giả (mô phỏng), không gọi cổng thật" },
                new ConnectivityResult { Name = "Máy chủ licence", Reachable = true, Detail = "Máy chủ licence giả (mô phỏng)" }
            };
            return Task.FromResult(results);
        }
    }
}
#endif
