using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Dashboard;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.WinForms.Download;

namespace HoaDonDienTu.WinForms.Dashboard
{
    /// <summary>
    /// SCR-02 Tổng quan (DD-10): số liệu kho, phiên đang chạy, việc cần xử lý và hóa đơn mới tải của MST đang mở. Chỉ đọc;
    /// mỗi thẻ mở đúng màn hình đã lọc (FR-DASH-003). Nút tải bị khóa theo quyền (ReadOnlyService).
    /// </summary>
    public sealed class DashboardPresenter : IDisposable
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly IDashboardView _view;
        private readonly IDashboardService _service;
        private readonly ICompanyContextAccessor _context;
        private readonly IEntitlementService _entitlement;
        private readonly INavigationService _navigation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private bool _initialized;
        private bool _disposed;
        private DashboardSnapshot _snapshot;

        public DashboardPresenter(
            IDashboardView view,
            IDashboardService service,
            ICompanyContextAccessor context,
            IEntitlementService entitlement,
            INavigationService navigation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (context == null) throw new ArgumentNullException("context");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _service = service;
            _context = context;
            _entitlement = entitlement;
            _navigation = navigation;
            _runner = runner;
            _errors = errors;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.NewDownloadRequested += OnNewDownload;
            _view.ActiveSessionRequested += OnActiveSession;
            _view.OpenArchiveRequested += OnOpenArchive;
            _view.AttentionRequested += OnAttention;
            _entitlement.ModeChanged += OnModeChanged;

            ApplyMode();
            Load();
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.NewDownloadRequested -= OnNewDownload;
            _view.ActiveSessionRequested -= OnActiveSession;
            _view.OpenArchiveRequested -= OnOpenArchive;
            _view.AttentionRequested -= OnAttention;
            _entitlement.ModeChanged -= OnModeChanged;
        }

        // ---- Sự kiện ----

        private void OnNewDownload(object sender, EventArgs e)
        {
            Go("Mở màn hình tải", ScreenId.Download, null);
        }

        private void OnActiveSession(object sender, EventArgs e)
        {
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Active.Count == 0) return;
            Go("Mở phiên đang chạy", ScreenId.DownloadProgress, snapshot.Active[0].SessionId);
        }

        private void OnOpenArchive(object sender, EventArgs e)
        {
            Go("Mở kho hóa đơn", ScreenId.InvoiceArchive, null);
        }

        private void OnAttention(object sender, AttentionRequestedEventArgs e)
        {
            var snapshot = _snapshot;
            if (snapshot == null || e.Index < 0 || e.Index >= snapshot.Attention.Count) return;
            var item = snapshot.Attention[e.Index];
            if (item.Kind == AttentionKind.MissingFiles)
            {
                Go("Mở hóa đơn thiếu file", ScreenId.InvoiceArchive, new ArchiveNavigationRequest(item.Direction, FileIntegrityFilter.Problems));
            }
            else if (item.SessionId.HasValue)
            {
                Go("Mở phiên cần xử lý", ScreenId.DownloadProgress, item.SessionId.Value);
            }
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            ApplyMode();
        }

        // ---- Nạp và hiển thị ----

        private void ApplyMode()
        {
            var allowed = _entitlement.IsAllowed(AppFeature.NewDownload);
            _view.ShowMode(allowed, _entitlement.CurrentMode == AppMode.ReadOnlyService ? "Chỉ đọc – cần gia hạn licence" : null);
        }

        private void Load()
        {
            Run("Nạp tổng quan", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp. Chọn doanh nghiệp để xem tổng quan.", false);
                    return;
                }

                var snapshot = await _service.GetAsync(token);
                if (_disposed) return;
                _snapshot = snapshot;
                _view.ShowResult(Build(snapshot));
                _view.ShowMessage(string.Empty, false);
            });
        }

        public static DashboardViewModel Build(DashboardSnapshot s)
        {
            var model = new DashboardViewModel
            {
                TotalInvoices = s.TotalInvoices.ToString("N0", Vi),
                TotalSub = "Mua vào " + s.PurchaseCount.ToString("N0", Vi) + " · Bán ra " + s.SoldCount.ToString("N0", Vi),
                YearCaption = "TỔNG THANH TOÁN NĂM " + s.Year,
                YearPayment = ArchivePresenter.Compact(s.YearPayment),
                YearSub = "Theo dữ liệu đã tải",
                AttentionValue = s.AttentionCount.ToString("N0", Vi),
                AttentionSub = AttentionSub(s),
                Attention = s.Attention.Select(Attention).ToList(),
                Recent = s.Recent.Select(Recent).ToList()
            };

            if (s.LastSyncAtUtc.HasValue)
            {
                model.SyncValue = s.LastSyncAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                model.SyncSub = DownloadProgressPresenter.StateText(s.LastSyncState.Value) + " · " + s.LastSyncInvoices.ToString("N0", Vi) + " hóa đơn";
            }
            else
            {
                model.SyncValue = "—";
                model.SyncSub = "Chưa có lần đồng bộ nào";
            }

            if (s.Active.Count > 0)
            {
                var a = s.Active[0];
                var percent = a.SegmentCount == 0 ? 0 : a.CompletedSegments * 100 / a.SegmentCount;
                model.HasActiveSession = true;
                model.SessionBadge = DownloadProgressPresenter.StateText(a.State);
                model.SessionTitle = (a.Direction == InvoiceDirection.Purchase ? "Hóa đơn mua vào" : "Hóa đơn bán ra") + " · " + a.PeriodText;
                model.SessionDetail = s.Active.Count > 1 ? "Còn " + (s.Active.Count - 1) + " phiên khác chưa kết thúc" : "Phiên đang được theo dõi";
                model.ProgressPercent = percent;
                model.ProgressText = a.CompletedSegments + "/" + a.SegmentCount + " đoạn";
                model.SessionStats = a.InvoiceCount.ToString("N0", Vi) + " hóa đơn" + (a.ErrorCount > 0 ? " · " + a.ErrorCount + " lỗi" : string.Empty);
            }

            return model;
        }

        private static string AttentionSub(DashboardSnapshot s)
        {
            var parts = new List<string>();
            if (s.MissingFileCount > 0) parts.Add(s.MissingFileCount.ToString("N0", Vi) + " XML thiếu");
            if (s.ErrorSessionCount > 0) parts.Add(s.ErrorSessionCount + " phiên lỗi");
            return parts.Count == 0 ? "Không có việc cần xử lý" : string.Join(" · ", parts);
        }

        private static DashboardAttentionRowViewModel Attention(AttentionItem item)
        {
            if (item.Kind == AttentionKind.MissingFiles)
            {
                return new DashboardAttentionRowViewModel
                {
                    Badge = "File thiếu",
                    Tone = DashboardTone.Bad,
                    Text = item.Count.ToString("N0", Vi) + " XML không còn trong thư mục kho",
                    ActionText = "Kiểm tra"
                };
            }

            return new DashboardAttentionRowViewModel
            {
                Badge = "Phiên lỗi",
                Tone = DashboardTone.Warning,
                Text = item.PeriodText + (item.State == DownloadSessionState.Failed ? " không thể tiếp tục" : " cần thử lại"),
                ActionText = "Mở"
            };
        }

        private static DashboardRecentRowViewModel Recent(RecentInvoice r)
        {
            var status = r.FileState == ArchiveFileState.Missing ? "Thiếu XML" : r.FileState == ArchiveFileState.MultiVersion ? "Có phiên bản mới" : "Đã lưu XML";
            return new DashboardRecentRowViewModel
            {
                IssueDate = r.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                Direction = r.Direction == InvoiceDirection.Purchase ? "Mua vào" : "Bán ra",
                Partner = string.IsNullOrEmpty(r.CounterpartyId) ? r.CounterpartyName : r.CounterpartyName + " · " + r.CounterpartyId,
                Number = r.InvoiceNumber,
                Status = status,
                Tone = r.FileState == ArchiveFileState.Missing ? DashboardTone.Bad : r.FileState == ArchiveFileState.MultiVersion ? DashboardTone.Warning : DashboardTone.Good,
                Payment = ArchivePresenter.Money(r.TotalPayment)
            };
        }

        private void Go(string name, ScreenId target, object parameter)
        {
            Run(name, token => _navigation.NavigateAsync(target, parameter, token));
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
