using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Downloads
{
    /// <summary>Nhóm trạng thái trên bộ lọc của SCR-08.</summary>
    public enum HistoryStatusFilter
    {
        All,
        Completed,
        CompletedWithErrors,

        /// <summary>Chưa kết thúc: nháp, đang chạy, tạm dừng, chờ đăng nhập.</summary>
        InProgress,

        /// <summary>Hủy hoặc thất bại.</summary>
        Stopped
    }

    public sealed class HistoryQuery
    {
        public HistoryQuery()
        {
            PageSize = 20;
        }

        /// <summary>Khớp mã phiên, kỳ, người dùng (không phân biệt hoa thường).</summary>
        public string Search { get; set; }

        public InvoiceDirection? Direction { get; set; }
        public HistoryStatusFilter Status { get; set; }

        /// <summary>Ngày (giờ máy) bắt đầu phiên, gồm cả hai đầu.</summary>
        public DateTime? From { get; set; }

        public DateTime? To { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }

        public HistoryQuery Clone()
        {
            return (HistoryQuery)MemberwiseClone();
        }
    }

    public sealed class HistoryRow
    {
        public Guid SessionId { get; set; }

        /// <summary>Mã dễ đọc dạng DL-2026-1101-001 (ngày tạo theo giờ máy + số thứ tự trong ngày).</summary>
        public string Code { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public InvoiceDirection Direction { get; set; }
        public string PeriodText { get; set; }
        public DownloadSessionState State { get; set; }
        public int SegmentCount { get; set; }
        public int CompletedSegments { get; set; }

        /// <summary>Hóa đơn mới lưu.</summary>
        public int NewCount { get; set; }

        /// <summary>Hóa đơn nhận về nhưng đã có sẵn trong kho.</summary>
        public int ExistingCount { get; set; }

        public int ErrorCount { get; set; }
        public string StartedBy { get; set; }
    }

    /// <summary>Số liệu toàn bộ lịch sử của doanh nghiệp (không phụ thuộc bộ lọc).</summary>
    public sealed class HistorySummary
    {
        public int Total { get; set; }
        public int Completed { get; set; }

        /// <summary>Đang chạy, tạm dừng hoặc chờ đăng nhập; phiên nháp chưa từng chạy không tính.</summary>
        public int Active { get; set; }

        /// <summary>Hoàn tất có lỗi hoặc thất bại.</summary>
        public int NeedsAttention { get; set; }
    }

    public sealed class HistoryPage
    {
        public IList<HistoryRow> Rows { get; set; }
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int PageCount { get; set; }
        public HistorySummary Summary { get; set; }
    }

    public interface IDownloadHistoryService
    {
        /// <summary>Lịch sử của doanh nghiệp đang mở, mới nhất trước. Không có doanh nghiệp thì trả trang rỗng.</summary>
        Task<HistoryPage> QueryAsync(HistoryQuery query, CancellationToken cancellationToken);
    }

    public sealed class DownloadHistoryService : IDownloadHistoryService
    {
        private readonly IDownloadRepository _repository;
        private readonly ICompanyContextAccessor _context;

        public DownloadHistoryService(IDownloadRepository repository, ICompanyContextAccessor context)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (context == null) throw new ArgumentNullException("context");
            _repository = repository;
            _context = context;
        }

        public async Task<HistoryPage> QueryAsync(HistoryQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException("query");
            var all = await LoadAsync(cancellationToken).ConfigureAwait(false);

            var summary = new HistorySummary
            {
                Total = all.Count,
                Completed = all.Count(r => r.State == DownloadSessionState.Completed),
                Active = all.Count(r => Group(r.State) == HistoryStatusFilter.InProgress && r.State != DownloadSessionState.Draft),
                NeedsAttention = all.Count(r => r.State == DownloadSessionState.CompletedWithErrors || r.State == DownloadSessionState.Failed)
            };

            var filtered = all.Where(r => Matches(r, query)).OrderByDescending(r => r.CreatedAtUtc).ToList();
            var size = Math.Max(1, query.PageSize);
            var pageCount = Math.Max(1, (filtered.Count + size - 1) / size);
            var index = Math.Min(Math.Max(0, query.PageIndex), pageCount - 1);

            return new HistoryPage
            {
                Rows = filtered.Skip(index * size).Take(size).ToList(),
                TotalCount = filtered.Count,
                PageIndex = index,
                PageSize = size,
                PageCount = pageCount,
                Summary = summary
            };
        }

        public static HistoryStatusFilter Group(DownloadSessionState state)
        {
            switch (state)
            {
                case DownloadSessionState.Completed: return HistoryStatusFilter.Completed;
                case DownloadSessionState.CompletedWithErrors: return HistoryStatusFilter.CompletedWithErrors;
                case DownloadSessionState.Cancelled:
                case DownloadSessionState.Failed: return HistoryStatusFilter.Stopped;
                default: return HistoryStatusFilter.InProgress;
            }
        }

        public static string PeriodText(PeriodSpec period)
        {
            switch (period.Kind)
            {
                case PeriodKind.Month: return "Tháng " + period.Month.GetValueOrDefault().ToString("00") + "/" + period.Year;
                case PeriodKind.Quarter: return "Q" + period.Quarter + "/" + period.Year;
                case PeriodKind.Year: return "Năm " + period.Year;
                default:
                    return (period.From.HasValue ? period.From.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "?")
                        + " – " + (period.To.HasValue ? period.To.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "?");
            }
        }

        private async Task<IList<HistoryRow>> LoadAsync(CancellationToken token)
        {
            var rows = new List<HistoryRow>();
            if (!_context.HasCurrent) return rows;

            var sessions = (await _repository.GetSessionsAsync(_context.Current.CompanyId, token).ConfigureAwait(false))
                .OrderBy(s => s.CreatedAtUtc).ToList();
            var segmentsBySession = await _repository.GetSegmentsByCompanyAsync(_context.Current.CompanyId, token).ConfigureAwait(false);
            var perDay = new Dictionary<string, int>();
            foreach (var session in sessions)
            {
                IList<DownloadSegment> segments;
                if (!segmentsBySession.TryGetValue(session.Id, out segments)) segments = new List<DownloadSegment>();
                var saved = segments.Sum(s => s.XmlSavedCount);
                var errors = segments.Sum(s => s.ErrorCount);
                var received = segments.Sum(s => s.ReceivedCount);

                var day = session.CreatedAtUtc.ToLocalTime().ToString("yyyy-MMdd", CultureInfo.InvariantCulture);
                int sequence;
                perDay.TryGetValue(day, out sequence);
                perDay[day] = ++sequence;

                rows.Add(new HistoryRow
                {
                    SessionId = session.Id,
                    Code = "DL-" + day + "-" + sequence.ToString("000", CultureInfo.InvariantCulture),
                    CreatedAtUtc = session.CreatedAtUtc,
                    FinishedAtUtc = session.FinishedAtUtc,
                    Direction = session.Direction,
                    PeriodText = PeriodText(session.Period),
                    State = session.State,
                    SegmentCount = segments.Count,
                    CompletedSegments = segments.Count(s => s.State == DownloadSegmentState.Completed),
                    NewCount = saved,
                    ExistingCount = Math.Max(0, received - saved - errors),
                    ErrorCount = errors,
                    StartedBy = session.StartedByUser
                });
            }

            return rows;
        }

        private static bool Matches(HistoryRow row, HistoryQuery q)
        {
            if (q.Direction.HasValue && row.Direction != q.Direction.Value) return false;
            if (q.Status != HistoryStatusFilter.All && Group(row.State) != q.Status) return false;

            var local = row.CreatedAtUtc.ToLocalTime().Date;
            if (q.From.HasValue && local < q.From.Value.Date) return false;
            if (q.To.HasValue && local > q.To.Value.Date) return false;

            var term = (q.Search ?? string.Empty).Trim();
            if (term.Length > 0)
            {
                var hay = (row.Code + " " + row.PeriodText + " " + row.StartedBy).ToLowerInvariant();
                if (hay.IndexOf(term.ToLowerInvariant(), StringComparison.Ordinal) < 0) return false;
            }

            return true;
        }
    }
}
