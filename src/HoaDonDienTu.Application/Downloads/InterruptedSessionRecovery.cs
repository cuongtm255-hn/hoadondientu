using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Downloads
{
    /// <summary>
    /// Mỗi lần một doanh nghiệp được mở (kể cả lúc khởi động) đưa các phiên tải bị ngắt giữa chừng về tạm dừng để Tổng quan và SCR-04
    /// báo đúng trạng thái và cho phép Tiếp tục (FR-JOB-010). Chạy nền; lỗi chỉ ghi nhật ký vì không được làm hỏng việc mở kho.
    /// </summary>
    public sealed class InterruptedSessionRecovery
    {
        private readonly ICompanyContextManager _context;
        private readonly IDownloadOrchestrator _orchestrator;
        private readonly IApplicationLogger _logger;

        public InterruptedSessionRecovery(ICompanyContextManager context, IDownloadOrchestrator orchestrator, IApplicationLogger logger)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (orchestrator == null) throw new ArgumentNullException("orchestrator");
            if (logger == null) throw new ArgumentNullException("logger");
            _context = context;
            _orchestrator = orchestrator;
            _logger = logger;
        }

        /// <summary>Lượt khôi phục gần nhất (để chờ trong test); null khi chưa có doanh nghiệp nào được mở.</summary>
        public Task LastRun { get; private set; }

        public void Attach()
        {
            _context.ContextChanged += OnContextChanged;
        }

        public void Detach()
        {
            _context.ContextChanged -= OnContextChanged;
        }

        private void OnContextChanged(object sender, EventArgs e)
        {
            if (!_context.HasCurrent) return;
            LastRun = RecoverAsync();
        }

        private async Task RecoverAsync()
        {
            try
            {
                var count = await _orchestrator.RecoverInterruptedAsync(CancellationToken.None).ConfigureAwait(false);
                if (count > 0) _logger.Warning("Đã đưa " + count + " phiên tải bị ngắt giữa chừng về tạm dừng.");
            }
            catch (Exception ex)
            {
                _logger.Error("Không khôi phục được phiên tải bị ngắt", ex);
            }
        }
    }
}
