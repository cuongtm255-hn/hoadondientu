using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;

namespace HoaDonDienTu.Application.Common
{
    public sealed class UiOperationState
    {
        public UiOperationState(string operationName, bool isBusy, bool wasCancelled, UserFacingError error)
        {
            OperationName = operationName;
            IsBusy = isBusy;
            WasCancelled = wasCancelled;
            Error = error;
        }

        public string OperationName { get; private set; }
        public bool IsBusy { get; private set; }
        public bool WasCancelled { get; private set; }

        /// <summary>Có giá trị khi thao tác thất bại (không phải hủy).</summary>
        public UserFacingError Error { get; private set; }
    }

    public interface IUiOperationRunner
    {
        /// <summary>
        /// Chạy một thao tác UI: chặn chạy lại cùng tên khi đang chạy, cấp CancellationToken, báo busy,
        /// gán correlation ID, quy đổi lỗi và luôn đưa UI về trạng thái không bận. Không ném exception của thao tác.
        /// </summary>
        Task RunAsync(string operationName, Func<CancellationToken, Task> operation, Action<UiOperationState> renderState);

        void CancelAll();
    }

    /// <summary>Phải được gọi từ UI thread để renderState chạy trên UI thread.</summary>
    public sealed class UiOperationRunner : IUiOperationRunner, IDisposable
    {
        private readonly IUserErrorMapper _errorMapper;
        private readonly IApplicationLogger _logger;
        private readonly Dictionary<string, CancellationTokenSource> _running = new Dictionary<string, CancellationTokenSource>();
        private readonly object _gate = new object();
        private bool _disposed;

        public UiOperationRunner(IUserErrorMapper errorMapper, IApplicationLogger logger)
        {
            if (errorMapper == null) throw new ArgumentNullException("errorMapper");
            if (logger == null) throw new ArgumentNullException("logger");
            _errorMapper = errorMapper;
            _logger = logger;
        }

        public async Task RunAsync(string operationName, Func<CancellationToken, Task> operation, Action<UiOperationState> renderState)
        {
            if (operationName == null) throw new ArgumentNullException("operationName");
            if (operation == null) throw new ArgumentNullException("operation");
            if (renderState == null) throw new ArgumentNullException("renderState");

            CancellationTokenSource cts;
            lock (_gate)
            {
                if (_disposed || _running.ContainsKey(operationName)) return;
                cts = new CancellationTokenSource();
                _running.Add(operationName, cts);
            }

            var correlationId = CorrelationId.New();
            var final = new UiOperationState(operationName, false, false, null);
            try
            {
                renderState(new UiOperationState(operationName, true, false, null));
                await operation(cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                final = new UiOperationState(operationName, false, true, null);
            }
            catch (Exception ex)
            {
                _logger.Error(operationName + " thất bại [" + correlationId + "]", ex);
                final = new UiOperationState(operationName, false, false, _errorMapper.Map(ex, operationName, correlationId));
            }
            finally
            {
                lock (_gate)
                {
                    _running.Remove(operationName);
                }

                cts.Dispose();
                try
                {
                    renderState(final);
                }
                catch (Exception ex)
                {
                    _logger.Error("Không hiển thị được trạng thái thao tác " + operationName, ex);
                }
            }
        }

        public void CancelAll()
        {
            List<CancellationTokenSource> sources;
            lock (_gate)
            {
                sources = new List<CancellationTokenSource>(_running.Values);
            }

            foreach (var source in sources)
            {
                try
                {
                    source.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Thao tác vừa kết thúc.
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
            }

            CancelAll();
        }
    }
}
