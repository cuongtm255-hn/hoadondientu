using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Portal;

namespace HoaDonDienTu.Infrastructure.Portal
{
    /// <summary>Đảm bảo khoảng cách tối thiểu giữa hai request liên tiếp (FR-JOB-012). An toàn khi gọi từ nhiều luồng.</summary>
    public sealed class MinIntervalRateLimiter : IRateLimiter
    {
        private readonly long _intervalTicks;
        private readonly object _gate = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _nextAllowedTicks;

        public MinIntervalRateLimiter(int minIntervalMs)
        {
            if (minIntervalMs < 0) throw new ArgumentOutOfRangeException("minIntervalMs");
            _intervalTicks = (long)(minIntervalMs * (Stopwatch.Frequency / 1000.0));
        }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            long delayTicks;
            lock (_gate)
            {
                var now = _clock.ElapsedTicks;
                var start = Math.Max(now, _nextAllowedTicks);
                _nextAllowedTicks = start + _intervalTicks;
                delayTicks = start - now;
            }

            if (delayTicks > 0)
            {
                var delayMs = (int)Math.Ceiling(delayTicks * 1000.0 / Stopwatch.Frequency);
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
