using System;
using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Downloads
{
    /// <summary>SCR-03 chỉ tạo phiên; SCR-04 chạy và theo dõi nó. Tách như vậy để rời màn hình tiến độ không làm mất phiên.</summary>
    public sealed class OrchestratorSessionStarter : IDownloadSessionStarter
    {
        private readonly IDownloadOrchestrator _orchestrator;

        public OrchestratorSessionStarter(IDownloadOrchestrator orchestrator)
        {
            if (orchestrator == null) throw new ArgumentNullException("orchestrator");
            _orchestrator = orchestrator;
        }

        public Task<Guid> StartAsync(DownloadPlan plan, CancellationToken cancellationToken)
        {
            return _orchestrator.CreateSessionAsync(plan, cancellationToken);
        }
    }
}
