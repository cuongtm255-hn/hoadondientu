using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Startup
{
    public enum ShutdownChoice
    {
        WaitForCompletion,
        PauseAndExit,
        CancelJobAndExit,
        StayOpen
    }

    /// <summary>Hỏi người dùng khi đóng app lúc đang có tác vụ ghi (DD-01 mục 17.2).</summary>
    public interface IShutdownPrompt
    {
        ShutdownChoice Ask(string companyName);
    }

    public sealed class ShutdownResult
    {
        private ShutdownResult(bool canClose, string message)
        {
            CanClose = canClose;
            Message = message;
        }

        public bool CanClose { get; private set; }

        /// <summary>Lý do không đóng được; null khi người dùng chủ động ở lại.</summary>
        public string Message { get; private set; }

        public static ShutdownResult Close()
        {
            return new ShutdownResult(true, null);
        }

        public static ShutdownResult Stay(string message)
        {
            return new ShutdownResult(false, message);
        }
    }

    /// <summary>
    /// Đóng app an toàn: xử lý tác vụ ghi đang chạy (chờ / tạm dừng + checkpoint / hủy), rồi xóa phiên cổng Thuế
    /// và đóng kho. Chỉ lỗi ở bước xử lý tác vụ mới chặn việc đóng; lỗi dọn dẹp được ghi log và bỏ qua để không kẹt app.
    /// </summary>
    public sealed class ShutdownCoordinator
    {
        private readonly ICompanyContextManager _contextManager;
        private readonly IActiveJobService _activeJobs;
        private readonly IPortalSessionStore _sessionStore;
        private readonly IShutdownPrompt _prompt;
        private readonly IApplicationLogger _logger;

        public ShutdownCoordinator(
            ICompanyContextManager contextManager,
            IActiveJobService activeJobs,
            IPortalSessionStore sessionStore,
            IShutdownPrompt prompt,
            IApplicationLogger logger)
        {
            if (contextManager == null) throw new ArgumentNullException("contextManager");
            if (activeJobs == null) throw new ArgumentNullException("activeJobs");
            if (sessionStore == null) throw new ArgumentNullException("sessionStore");
            if (prompt == null) throw new ArgumentNullException("prompt");
            if (logger == null) throw new ArgumentNullException("logger");
            _contextManager = contextManager;
            _activeJobs = activeJobs;
            _sessionStore = sessionStore;
            _prompt = prompt;
            _logger = logger;
        }

        public async Task<ShutdownResult> TryShutdownAsync(CancellationToken cancellationToken)
        {
            var current = _contextManager.Current;
            if (current != null)
            {
                bool running;
                try
                {
                    running = await _activeJobs.HasRunningDownloadAsync(current.CompanyId, cancellationToken).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error("Không xác định được tác vụ tải đang chạy khi đóng ứng dụng", ex);
                    return ShutdownResult.Stay("Không xác định được tác vụ tải đang chạy nên chưa thể đóng ứng dụng an toàn.");
                }

                if (running)
                {
                    var failure = await ResolveJobAsync(current, cancellationToken).ConfigureAwait(true);
                    if (failure != null) return failure;
                }
            }

            ClearSessionAndContext();
            return ShutdownResult.Close();
        }

        private async Task<ShutdownResult> ResolveJobAsync(CompanyContext current, CancellationToken cancellationToken)
        {
            var choice = _prompt.Ask(current.DisplayName);
            if (choice == ShutdownChoice.StayOpen) return ShutdownResult.Stay(null);

            try
            {
                switch (choice)
                {
                    case ShutdownChoice.WaitForCompletion:
                        await _activeJobs.WaitForCompletionAsync(current.CompanyId, cancellationToken).ConfigureAwait(true);
                        break;
                    case ShutdownChoice.PauseAndExit:
                        // Tạm dừng kèm ghi checkpoint; thất bại thì không được đóng vì sẽ mất tiến độ.
                        await _activeJobs.PauseAsync(current.CompanyId, cancellationToken).ConfigureAwait(true);
                        break;
                    case ShutdownChoice.CancelJobAndExit:
                        await _activeJobs.CancelAsync(current.CompanyId, cancellationToken).ConfigureAwait(true);
                        break;
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("Không xử lý được tác vụ tải đang chạy khi đóng ứng dụng", ex);
                return ShutdownResult.Stay("Không dừng được tác vụ tải an toàn. Ứng dụng chưa đóng để tránh mất tiến độ.");
            }
        }

        private void ClearSessionAndContext()
        {
            try
            {
                _sessionStore.Clear();
            }
            catch (Exception ex)
            {
                _logger.Error("Không xóa được phiên cổng Thuế khi đóng ứng dụng", ex);
            }

            try
            {
                _contextManager.CloseCurrent();
            }
            catch (Exception ex)
            {
                _logger.Error("Không đóng được kho dữ liệu khi đóng ứng dụng", ex);
            }
        }
    }
}
