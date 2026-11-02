using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;

namespace HoaDonDienTu.Application.Downloads
{
    /// <summary>
    /// Lưu phiên, đoạn và sự kiện tải. SQLite (DD-06) sẽ thay <see cref="InMemoryDownloadRepository"/>.
    /// Mọi phương thức lưu phải bền vững trước khi trả về, vì checkpoint dựa vào đó (FR-JOB-006).
    /// </summary>
    public interface IDownloadRepository
    {
        Task CreateAsync(DownloadSession session, IList<DownloadSegment> segments, CancellationToken cancellationToken);
        Task<DownloadSession> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);
        Task<IList<DownloadSegment>> GetSegmentsAsync(Guid sessionId, CancellationToken cancellationToken);
        Task SaveSessionAsync(DownloadSession session, CancellationToken cancellationToken);
        Task SaveSegmentAsync(DownloadSegment segment, CancellationToken cancellationToken);
        Task AddEventAsync(DownloadEvent downloadEvent, CancellationToken cancellationToken);
        Task<IList<DownloadEvent>> GetEventsAsync(Guid sessionId, CancellationToken cancellationToken);

        /// <summary>Phiên của doanh nghiệp chưa kết thúc (Running do bị ngắt, PausedByUser, WaitingForLogin, Draft).</summary>
        Task<IList<DownloadSession>> GetUnfinishedAsync(CompanyId companyId, CancellationToken cancellationToken);

        /// <summary>Mọi phiên của doanh nghiệp (cho lịch sử SCR-08), mới nhất trước.</summary>
        Task<IList<DownloadSession>> GetSessionsAsync(CompanyId companyId, CancellationToken cancellationToken);
    }

    public enum InvoiceSaveResult
    {
        Saved,

        /// <summary>Đã có hóa đơn này (khóa chống trùng); không tạo bản ghi mới (NFR-REL-002).</summary>
        AlreadyExists
    }

    /// <summary>
    /// Ghi hóa đơn và XML vào kho (DD-06: file tạm → hash → commit). Cổng này cho phép engine tải chạy trước khi có SQLite.
    /// </summary>
    public interface IInvoiceStore
    {
        /// <exception cref="System.IO.IOException">Lỗi ghi/hết dung lượng; engine không mất checkpoint.</exception>
        Task<InvoiceSaveResult> UpsertAsync(DownloadSession session, PortalInvoiceDto invoice, InvoiceXmlPayload xml, CancellationToken cancellationToken);
    }

    /// <summary>Chính sách thử lại lỗi tạm thời (FR-JOB-008). Trả null khi hết lượt.</summary>
    public interface IRetryPolicy
    {
        /// <param name="attempt">Số lần đã thử, bắt đầu từ 1 cho lần thất bại đầu tiên.</param>
        TimeSpan? NextDelay(int attempt, PortalApiException error);
    }

    /// <summary>Tối đa 3 lần thử lại, chờ tăng gấp đôi; lỗi 429 chờ theo Retry-After nếu cổng cho biết.</summary>
    public sealed class DefaultRetryPolicy : IRetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;

        public DefaultRetryPolicy(int maxRetries, TimeSpan baseDelay)
        {
            _maxRetries = maxRetries;
            _baseDelay = baseDelay;
        }

        public static DefaultRetryPolicy Standard
        {
            get { return new DefaultRetryPolicy(3, TimeSpan.FromSeconds(2)); }
        }

        public TimeSpan? NextDelay(int attempt, PortalApiException error)
        {
            if (error.Kind != PortalErrorKind.Transient && error.Kind != PortalErrorKind.RateLimited) return null;
            if (attempt > _maxRetries) return null;
            if (error.Kind == PortalErrorKind.RateLimited && error.RetryAfter.HasValue) return error.RetryAfter.Value;
            return TimeSpan.FromTicks(_baseDelay.Ticks * (1L << (attempt - 1)));
        }
    }

    /// <summary>Repository trong bộ nhớ: dùng cho test và tới khi có SQLite. Trả bản sao để mô phỏng việc lưu bền vững.</summary>
    public sealed class InMemoryDownloadRepository : IDownloadRepository
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, DownloadSession> _sessions = new Dictionary<Guid, DownloadSession>();
        private readonly Dictionary<Guid, List<DownloadSegment>> _segments = new Dictionary<Guid, List<DownloadSegment>>();
        private readonly List<DownloadEvent> _events = new List<DownloadEvent>();

        public Task CreateAsync(DownloadSession session, IList<DownloadSegment> segments, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _sessions[session.Id] = session.Clone();
                _segments[session.Id] = segments.Select(s => s.Clone()).ToList();
            }

            return Task.FromResult(0);
        }

        public Task<DownloadSession> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                DownloadSession found;
                return Task.FromResult(_sessions.TryGetValue(sessionId, out found) ? found.Clone() : null);
            }
        }

        public Task<IList<DownloadSegment>> GetSegmentsAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                List<DownloadSegment> found;
                IList<DownloadSegment> result = _segments.TryGetValue(sessionId, out found)
                    ? found.OrderBy(s => s.Index).Select(s => s.Clone()).ToList()
                    : new List<DownloadSegment>();
                return Task.FromResult(result);
            }
        }

        public Task SaveSessionAsync(DownloadSession session, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _sessions[session.Id] = session.Clone();
            }

            return Task.FromResult(0);
        }

        public Task SaveSegmentAsync(DownloadSegment segment, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var list = _segments[segment.SessionId];
                var index = list.FindIndex(s => s.Id == segment.Id);
                if (index < 0) throw new InvalidOperationException("Đoạn không thuộc phiên nào đã tạo.");
                list[index] = segment.Clone();
            }

            return Task.FromResult(0);
        }

        public Task AddEventAsync(DownloadEvent downloadEvent, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _events.Add(downloadEvent);
            }

            return Task.FromResult(0);
        }

        public Task<IList<DownloadEvent>> GetEventsAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IList<DownloadEvent> result = _events.Where(e => e.SessionId == sessionId).OrderBy(e => e.TimeUtc).ToList();
                return Task.FromResult(result);
            }
        }

        public Task<IList<DownloadSession>> GetUnfinishedAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IList<DownloadSession> result = _sessions.Values
                    .Where(s => s.CompanyId.Equals(companyId) && !DownloadStateMachine.IsFinished(s.State))
                    .OrderBy(s => s.CreatedAtUtc)
                    .Select(s => s.Clone())
                    .ToList();
                return Task.FromResult(result);
            }
        }

        public Task<IList<DownloadSession>> GetSessionsAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IList<DownloadSession> result = _sessions.Values
                    .Where(s => s.CompanyId.Equals(companyId))
                    .OrderByDescending(s => s.CreatedAtUtc)
                    .Select(s => s.Clone())
                    .ToList();
                return Task.FromResult(result);
            }
        }
    }

    /// <summary>Kho hóa đơn trong bộ nhớ (khóa chống trùng theo PortalInvoiceId trong một MST). Dùng cho test/dev tới khi có DD-06.</summary>
    public sealed class InMemoryInvoiceStore : IInvoiceStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, byte[]> _saved = new Dictionary<string, byte[]>();

        public int Count
        {
            get { lock (_gate) { return _saved.Count; } }
        }

        public Task<InvoiceSaveResult> UpsertAsync(DownloadSession session, PortalInvoiceDto invoice, InvoiceXmlPayload xml, CancellationToken cancellationToken)
        {
            var key = session.TaxCode.Value + "|" + invoice.PortalInvoiceId;
            lock (_gate)
            {
                if (_saved.ContainsKey(key)) return Task.FromResult(InvoiceSaveResult.AlreadyExists);
                _saved[key] = xml.Content;
                return Task.FromResult(InvoiceSaveResult.Saved);
            }
        }
    }
}
