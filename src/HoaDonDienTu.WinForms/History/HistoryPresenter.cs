using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.WinForms.Download;

namespace HoaDonDienTu.WinForms.History
{
    /// <summary>Giữ bộ lọc và trang của SCR-08 trong phiên; mỗi doanh nghiệp một trạng thái.</summary>
    public sealed class HistoryViewStateStore
    {
        private Guid _companyId;
        private HistoryFilterInput _filter;
        private HistoryQuery _query;

        public bool TryGet(CompanyId company, out HistoryFilterInput filter, out HistoryQuery query)
        {
            if (_filter != null && _companyId == company.Value)
            {
                filter = _filter;
                query = _query.Clone();
                return true;
            }

            filter = null;
            query = null;
            return false;
        }

        public void Save(CompanyId company, HistoryFilterInput filter, HistoryQuery query)
        {
            _companyId = company.Value;
            _filter = filter;
            _query = query.Clone();
        }
    }

    /// <summary>
    /// SCR-08 Lịch sử tải (DD-10): danh sách phiên của MST đang mở, lọc theo chiều/trạng thái/ngày, mở phiên sang SCR-04.
    /// Chỉ đọc nên dùng được ở mọi chế độ; việc tiếp tục phiên dở nằm ở SCR-04.
    /// </summary>
    public sealed class HistoryPresenter : IDisposable
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly IHistoryView _view;
        private readonly IDownloadHistoryService _service;
        private readonly ICompanyContextAccessor _context;
        private readonly INavigationService _navigation;
        private readonly HistoryViewStateStore _store;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private HistoryQuery _query = new HistoryQuery();
        private bool _initialized;
        private bool _disposed;

        // Runner bỏ qua thao tác cùng tên đang chạy, nên thay đổi bộ lọc/trang được gom vào một vòng làm mới tuần tự:
        // thay đổi đến khi đang tải sẽ được áp dụng ngay sau lần tải hiện tại, không bị mất.
        private bool _refreshing;
        private bool _dirty;
        private bool _resetPage;
        private bool _clearRequested;

        public HistoryPresenter(
            IHistoryView view,
            IDownloadHistoryService service,
            ICompanyContextAccessor context,
            INavigationService navigation,
            HistoryViewStateStore store,
            IUiOperationRunner runner,
            IUserErrorPresenter errors)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (context == null) throw new ArgumentNullException("context");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (store == null) throw new ArgumentNullException("store");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _service = service;
            _context = context;
            _navigation = navigation;
            _store = store;
            _runner = runner;
            _errors = errors;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.FilterChanged += OnFilterChanged;
            _view.ClearRequested += OnClear;
            _view.NewSessionRequested += OnNewSession;
            _view.PageRequested += OnPage;
            _view.SessionRequested += OnSession;

            _refreshing = true;
            Run("Mở lịch sử tải", async token =>
            {
                try
                {
                    if (!_context.HasCurrent)
                    {
                        _view.ShowMessage("Chưa chọn doanh nghiệp. Chọn doanh nghiệp để xem lịch sử tải.", false);
                        return;
                    }

                    HistoryFilterInput filter;
                    HistoryQuery saved;
                    if (_store.TryGet(_context.Current.CompanyId, out filter, out saved))
                    {
                        _query = saved;
                        _view.ShowFilter(filter);
                    }

                    _dirty = true;
                    await DrainAsync(token);
                }
                finally
                {
                    _refreshing = false;
                }
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.FilterChanged -= OnFilterChanged;
            _view.ClearRequested -= OnClear;
            _view.NewSessionRequested -= OnNewSession;
            _view.PageRequested -= OnPage;
            _view.SessionRequested -= OnSession;
        }

        // ---- Sự kiện ----

        private void OnFilterChanged(object sender, EventArgs e)
        {
            _resetPage = true;
            Refresh();
        }

        private void OnClear(object sender, EventArgs e)
        {
            _clearRequested = true;
            _resetPage = true;
            Refresh();
        }

        private void OnPage(object sender, PageRequestedEventArgs e)
        {
            _query.PageIndex = Math.Max(0, _query.PageIndex + e.Delta);
            Refresh();
        }

        private void OnSession(object sender, SessionRequestedEventArgs e)
        {
            Run("Mở phiên tải", token => _navigation.NavigateAsync(ScreenId.DownloadProgress, e.SessionId, token));
        }

        private void OnNewSession(object sender, EventArgs e)
        {
            Run("Tạo phiên tải mới", token => _navigation.NavigateAsync(ScreenId.Download, null, token));
        }

        // ---- Tra cứu và hiển thị ----

        private void Refresh()
        {
            _dirty = true;
            if (_refreshing) return;
            _refreshing = true;
            Run("Làm mới lịch sử tải", async token =>
            {
                try
                {
                    await DrainAsync(token);
                }
                finally
                {
                    _refreshing = false;
                }
            });
        }

        private async Task DrainAsync(CancellationToken token)
        {
            while (_dirty && !_disposed)
            {
                _dirty = false;
                await RefreshAsync(token);
            }
        }

        private async Task RefreshAsync(CancellationToken token)
        {
            if (!_context.HasCurrent)
            {
                _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                return;
            }

            var company = _context.Current.CompanyId;
            if (_clearRequested)
            {
                _clearRequested = false;
                _view.ShowFilter(new HistoryFilterInput());
            }

            var filter = _view.ReadFilter();
            var invalidRange = filter.From.HasValue && filter.To.HasValue && filter.From.Value > filter.To.Value;
            if (invalidRange)
            {
                // Khoảng ngày ngược: báo rõ thay vì trả danh sách rỗng khó hiểu; giữ nguyên kết quả đang hiển thị.
                _resetPage = false;
                _view.ShowMessage("\"Từ ngày\" đang lớn hơn \"Đến ngày\". Hãy đổi lại khoảng ngày.", true);
                return;
            }

            Apply(filter);
            if (_resetPage)
            {
                _query.PageIndex = 0;
                _resetPage = false;
            }

            var page = await _service.QueryAsync(_query, token);
            if (_disposed) return;

            _query.PageIndex = page.PageIndex;
            _store.Save(company, filter, _query);
            _view.ShowResult(Build(page));
            _view.ShowMessage(string.Empty, false);
        }

        private void Apply(HistoryFilterInput f)
        {
            _query.Search = string.IsNullOrWhiteSpace(f.Search) ? null : f.Search.Trim();
            _query.Direction = f.Direction;
            _query.Status = f.Status;
            _query.From = f.From;
            _query.To = f.To;
        }

        private static HistoryViewModel Build(HistoryPage page)
        {
            var first = page.TotalCount == 0 ? 0 : page.PageIndex * page.PageSize + 1;
            var last = page.TotalCount == 0 ? 0 : first + page.Rows.Count - 1;
            return new HistoryViewModel
            {
                TotalSessions = page.Summary.Total.ToString("N0", Vi),
                CompletedSessions = page.Summary.Completed.ToString("N0", Vi),
                ActiveSessions = page.Summary.Active.ToString("N0", Vi),
                AttentionSessions = page.Summary.NeedsAttention.ToString("N0", Vi),
                FooterText = page.TotalCount == 0
                    ? "Chưa có phiên tải nào khớp."
                    : "Hiển thị " + first.ToString("N0", Vi) + "–" + last.ToString("N0", Vi) + " / " + page.TotalCount.ToString("N0", Vi) + " phiên",
                PageText = "Trang " + (page.PageIndex + 1) + "/" + page.PageCount,
                CanPrevious = page.PageIndex > 0,
                CanNext = page.PageIndex < page.PageCount - 1,
                Rows = page.Rows.Select(Row).ToList()
            };
        }

        private static HistoryRowViewModel Row(HistoryRow r)
        {
            return new HistoryRowViewModel
            {
                SessionId = r.SessionId,
                Code = r.Code,
                Started = r.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                Direction = r.Direction == InvoiceDirection.Purchase ? "Mua vào" : "Bán ra",
                Period = r.PeriodText,
                Status = StatusText(r),
                Tone = Tone(r.State),
                NewCount = r.NewCount.ToString("N0", Vi),
                ExistingCount = r.ExistingCount.ToString("N0", Vi),
                ErrorCount = r.ErrorCount.ToString("N0", Vi),
                User = r.StartedBy,
                ActionText = ActionText(r.State)
            };
        }

        public static string StatusText(HistoryRow r)
        {
            var text = DownloadProgressPresenter.StateText(r.State);
            if (r.State == DownloadSessionState.Running && r.SegmentCount > 0)
            {
                text += " " + (r.CompletedSegments * 100 / r.SegmentCount) + "%";
            }

            return text;
        }

        public static HistoryStatusTone Tone(DownloadSessionState state)
        {
            switch (DownloadHistoryService.Group(state))
            {
                case HistoryStatusFilter.Completed: return HistoryStatusTone.Good;
                case HistoryStatusFilter.InProgress: return HistoryStatusTone.Warning;
                case HistoryStatusFilter.CompletedWithErrors: return HistoryStatusTone.Bad;
                default: return state == DownloadSessionState.Failed ? HistoryStatusTone.Bad : HistoryStatusTone.Neutral;
            }
        }

        public static string ActionText(DownloadSessionState state)
        {
            switch (DownloadHistoryService.Group(state))
            {
                case HistoryStatusFilter.InProgress: return "Xem tiến độ";
                case HistoryStatusFilter.CompletedWithErrors: return "Xử lý lỗi";
                default: return state == DownloadSessionState.Failed ? "Xử lý lỗi" : "Xem kết quả";
            }
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
