using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Dashboard
{
    public enum AttentionKind
    {
        /// <summary>Có hóa đơn mà file XML không còn trong thư mục kho.</summary>
        MissingFiles,

        /// <summary>Phiên tải kết thúc có lỗi hoặc không tiếp tục được.</summary>
        SessionNeedsAttention
    }

    public sealed class AttentionItem
    {
        public AttentionKind Kind { get; set; }

        /// <summary>Số hóa đơn thiếu file (MissingFiles).</summary>
        public int Count { get; set; }

        /// <summary>Chiều có hóa đơn thiếu file; dùng để mở Kho hóa đơn đúng chiều (MissingFiles).</summary>
        public InvoiceDirection Direction { get; set; }

        public Guid? SessionId { get; set; }
        public string PeriodText { get; set; }
        public DownloadSessionState State { get; set; }
    }

    public sealed class DashboardSession
    {
        public Guid SessionId { get; set; }
        public InvoiceDirection Direction { get; set; }
        public string PeriodText { get; set; }
        public DownloadSessionState State { get; set; }
        public int SegmentCount { get; set; }
        public int CompletedSegments { get; set; }

        /// <summary>Hóa đơn đã nhận về trong phiên (mới và đã có).</summary>
        public int InvoiceCount { get; set; }

        public int ErrorCount { get; set; }
    }

    public sealed class RecentInvoice
    {
        public DateTime IssueDate { get; set; }
        public InvoiceDirection Direction { get; set; }
        public string CounterpartyName { get; set; }
        public string CounterpartyId { get; set; }
        public string InvoiceNumber { get; set; }
        public ArchiveFileState FileState { get; set; }
        public decimal? TotalPayment { get; set; }
    }

    /// <summary>Số liệu SCR-02 của doanh nghiệp đang mở. Không chứa token hay dữ liệu đăng nhập; trạng thái đăng nhập do thanh đầu trang hiển thị.</summary>
    public sealed class DashboardSnapshot
    {
        public int TotalInvoices { get; set; }
        public int PurchaseCount { get; set; }
        public int SoldCount { get; set; }
        public int Year { get; set; }

        /// <summary>Tổng thanh toán (VND) của hóa đơn bán ra có ngày lập trong <see cref="Year"/>.</summary>
        public decimal YearPaymentSold { get; set; }

        /// <summary>Tổng thanh toán (VND) của hóa đơn mua vào có ngày lập trong <see cref="Year"/>.</summary>
        public decimal YearPaymentPurchase { get; set; }

        public DateTime? LastSyncAtUtc { get; set; }
        public DownloadSessionState? LastSyncState { get; set; }

        /// <summary>Số hóa đơn mới lưu của phiên đồng bộ gần nhất.</summary>
        public int LastSyncInvoices { get; set; }

        public IList<DashboardSession> Active { get; set; }
        public IList<AttentionItem> Attention { get; set; }
        public IList<RecentInvoice> Recent { get; set; }

        /// <summary>Tổng số việc cần xử lý: hóa đơn thiếu file cộng số phiên lỗi.</summary>
        public int AttentionCount { get; set; }

        public int MissingFileCount { get; set; }
        public int ErrorSessionCount { get; set; }
    }

    public interface IDashboardService
    {
        /// <exception cref="StorageGuardException">Chưa mở doanh nghiệp nào.</exception>
        Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken);
    }

    public sealed class DashboardService : IDashboardService
    {
        private const int RecentCount = 5;
        private const int MaxSessionAttention = 3;

        private readonly IInvoiceRepository _invoices;
        private readonly IInvoiceFileStore _files;
        private readonly IInvoiceArchiveService _archive;
        private readonly IDownloadHistoryService _history;
        private readonly ICompanyContextAccessor _context;
        private readonly IClock _clock;

        public DashboardService(
            IInvoiceRepository invoices,
            IInvoiceFileStore files,
            IInvoiceArchiveService archive,
            IDownloadHistoryService history,
            ICompanyContextAccessor context,
            IClock clock)
        {
            if (invoices == null) throw new ArgumentNullException("invoices");
            if (files == null) throw new ArgumentNullException("files");
            if (archive == null) throw new ArgumentNullException("archive");
            if (history == null) throw new ArgumentNullException("history");
            if (context == null) throw new ArgumentNullException("context");
            if (clock == null) throw new ArgumentNullException("clock");
            _invoices = invoices;
            _files = files;
            _archive = archive;
            _history = history;
            _context = context;
            _clock = clock;
        }

        public async Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken)
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var root = _context.Current.StorageRoot;
            var year = _clock.UtcNow.ToLocalTime().Year;

            // Đếm, tổng năm và danh sách mới nhất tính trong kho dữ liệu: không nạp cả kho vào bộ nhớ mỗi lần mở Tổng quan.
            var overview = await _invoices.GetOverviewAsync(year, RecentCount, cancellationToken).ConfigureAwait(false);
            var recent = await _invoices.FindManyAsync(overview.RecentIds, cancellationToken).ConfigureAwait(false);
            var sessions = await _history.QueryAsync(new HistoryQuery { PageSize = int.MaxValue }, cancellationToken).ConfigureAwait(false);
            var missingPurchase = await CountMissingAsync(InvoiceDirection.Purchase, cancellationToken).ConfigureAwait(false);
            var missingSold = await CountMissingAsync(InvoiceDirection.Sold, cancellationToken).ConfigureAwait(false);

            var snapshot = new DashboardSnapshot
            {
                TotalInvoices = overview.PurchaseCount + overview.SoldCount,
                PurchaseCount = overview.PurchaseCount,
                SoldCount = overview.SoldCount,
                Year = year,
                YearPaymentSold = overview.YearPaymentSold,
                YearPaymentPurchase = overview.YearPaymentPurchase,
                MissingFileCount = missingPurchase + missingSold
            };

            snapshot.Recent = recent.Select(i => ToRecent(root, i)).ToList();

            snapshot.Active = sessions.Rows
                .Where(r => r.State == DownloadSessionState.Running || r.State == DownloadSessionState.PausedByUser || r.State == DownloadSessionState.WaitingForLogin)
                .Select(r => new DashboardSession
                {
                    SessionId = r.SessionId, Direction = r.Direction, PeriodText = r.PeriodText, State = r.State, SegmentCount = r.SegmentCount,
                    CompletedSegments = r.CompletedSegments, InvoiceCount = r.NewCount + r.ExistingCount, ErrorCount = r.ErrorCount
                })
                .ToList();

            var lastSync = sessions.Rows
                .Where(r => (r.State == DownloadSessionState.Completed || r.State == DownloadSessionState.CompletedWithErrors) && r.FinishedAtUtc.HasValue)
                .OrderByDescending(r => r.FinishedAtUtc.Value)
                .FirstOrDefault();
            if (lastSync != null)
            {
                snapshot.LastSyncAtUtc = lastSync.FinishedAtUtc;
                snapshot.LastSyncState = lastSync.State;
                snapshot.LastSyncInvoices = lastSync.NewCount;
            }

            var attention = new List<AttentionItem>();

            // Mỗi chiều một mục để số đếm khớp đúng danh sách mở ra ở Kho hóa đơn.
            if (missingPurchase > 0) attention.Add(new AttentionItem { Kind = AttentionKind.MissingFiles, Count = missingPurchase, Direction = InvoiceDirection.Purchase });
            if (missingSold > 0) attention.Add(new AttentionItem { Kind = AttentionKind.MissingFiles, Count = missingSold, Direction = InvoiceDirection.Sold });

            var failed = sessions.Rows
                .Where(r => r.State == DownloadSessionState.CompletedWithErrors || r.State == DownloadSessionState.Failed)
                .ToList();
            snapshot.ErrorSessionCount = failed.Count;
            foreach (var r in failed.Take(MaxSessionAttention))
            {
                attention.Add(new AttentionItem { Kind = AttentionKind.SessionNeedsAttention, SessionId = r.SessionId, PeriodText = r.PeriodText, State = r.State });
            }

            snapshot.Attention = attention;
            snapshot.AttentionCount = snapshot.MissingFileCount + snapshot.ErrorSessionCount;
            return snapshot;
        }

        private async Task<int> CountMissingAsync(InvoiceDirection direction, CancellationToken token)
        {
            var page = await _archive.QueryAsync(new ArchiveQuery { Direction = direction, FileFilter = FileIntegrityFilter.Problems, PageSize = 1 }, token).ConfigureAwait(false);
            return page.TotalCount;
        }

        private RecentInvoice ToRecent(string root, StoredInvoice stored)
        {
            var i = stored.Invoice;
            var purchase = i.Direction == InvoiceDirection.Purchase;
            var current = stored.Current;
            ArchiveFileState state;
            if (current == null || !_files.Exists(root, current.RelativePath)) state = ArchiveFileState.Missing;
            else if (stored.StoredProblem.HasValue && stored.StoredProblem.Value != IntegrityProblemKind.Missing) state = ArchiveFileState.Damaged;
            else state = stored.Versions.Count > 1 ? ArchiveFileState.MultiVersion : ArchiveFileState.Ok;

            return new RecentInvoice
            {
                IssueDate = i.IssueDate,
                Direction = i.Direction,
                CounterpartyName = purchase ? i.SellerName : i.BuyerName,
                CounterpartyId = i.CounterpartyIdentifier,
                InvoiceNumber = i.InvoiceNumber,
                FileState = state,
                TotalPayment = i.TotalPayment
            };
        }
    }
}
