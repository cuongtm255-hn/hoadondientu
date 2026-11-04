using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>
    /// SCR-04 Tiến độ tải (DD-05 mục 6). Chạy phiên do SCR-03 tạo, hiển thị tiến độ theo tháng, cho tạm dừng/tiếp tục/hủy và thử lại đoạn lỗi.
    /// Hết phiên đăng nhập thì phiên dừng ở "Chờ đăng nhập lại"; Tiếp tục mở đăng nhập rồi chạy tiếp từ checkpoint (AC-06).
    /// Rời màn hình khi đang chạy sẽ tạm dừng an toàn và ghi checkpoint (không mất dữ liệu).
    /// </summary>
    public sealed class DownloadProgressPresenter : IDisposable
    {
        private const string RunName = "Tải hóa đơn";
        private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(150);

        private readonly IDownloadProgressView _view;
        private readonly IDownloadOrchestrator _orchestrator;
        private readonly IDownloadRepository _repository;
        private readonly IPortalSessionStore _session;
        private readonly Func<IShellActions> _shell;
        private readonly Func<INavigationService> _navigation;
        private readonly IConfirmationService _confirmation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly IClock _clock;
        private readonly Guid? _requestedSessionId;
        private readonly Stopwatch _sinceRender = Stopwatch.StartNew();
        private Guid _sessionId;
        private volatile bool _running;
        private readonly object _renderGate = new object();
        private int _renderTicket;
        private int _lastShownTicket;
        private bool _initialized;
        private bool _disposed;

        public DownloadProgressPresenter(
            IDownloadProgressView view,
            IDownloadOrchestrator orchestrator,
            IDownloadRepository repository,
            IPortalSessionStore session,
            Func<IShellActions> shell,
            Func<INavigationService> navigation,
            IConfirmationService confirmation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            IClock clock,
            Guid? sessionId)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (orchestrator == null) throw new ArgumentNullException("orchestrator");
            if (repository == null) throw new ArgumentNullException("repository");
            if (session == null) throw new ArgumentNullException("session");
            if (shell == null) throw new ArgumentNullException("shell");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            if (clock == null) throw new ArgumentNullException("clock");
            _view = view;
            _orchestrator = orchestrator;
            _repository = repository;
            _session = session;
            _shell = shell;
            _navigation = navigation;
            _confirmation = confirmation;
            _runner = runner;
            _errors = errors;
            _clock = clock;
            _requestedSessionId = sessionId;
        }

        public bool IsRunning
        {
            get { return _running; }
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.PauseRequested += OnPause;
            _view.ResumeRequested += OnResume;
            _view.CancelRequested += OnCancel;
            _view.RetryFailedRequested += OnRetryFailed;
            _view.RetrySegmentRequested += OnRetrySegment;
            _view.OpenArchiveRequested += OnOpenArchive;
            _view.NewDownloadRequested += OnNewDownload;

            Guard("Mở phiên tải", async token =>
            {
                var id = _requestedSessionId.HasValue && _requestedSessionId.Value != Guid.Empty ? _requestedSessionId.Value : await FindLatestAsync(token);
                if (id == Guid.Empty)
                {
                    _view.ShowMessage("Không có phiên tải nào đang mở. Chọn kỳ ở màn hình Tải hóa đơn để bắt đầu.", false);
                    return;
                }

                _sessionId = id;
                await RenderAsync(true);
                var session = await _repository.GetSessionAsync(id, token);

                // Phiên mới tạo (Draft) hoặc bị ngắt giữa chừng (Running mà không có lượt chạy nào) thì chạy ngay.
                if (session.State == DownloadSessionState.Draft || session.State == DownloadSessionState.Running)
                {
                    await StartRunAsync(null);
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_initialized) return;
            _view.PauseRequested -= OnPause;
            _view.ResumeRequested -= OnResume;
            _view.CancelRequested -= OnCancel;
            _view.RetryFailedRequested -= OnRetryFailed;
            _view.RetrySegmentRequested -= OnRetrySegment;
            _view.OpenArchiveRequested -= OnOpenArchive;
            _view.NewDownloadRequested -= OnNewDownload;

            // Hủy token của lượt chạy: engine ghi checkpoint và để phiên ở trạng thái tạm dừng để tiếp tục sau.
            _runner.CancelAll();
        }

        // ---- Hành động ----

        private void OnPause(object sender, EventArgs e)
        {
            Guard("Tạm dừng", async token =>
            {
                await _orchestrator.PauseAsync(_sessionId, token);
                _view.ShowMessage("Đang tạm dừng sau hóa đơn hiện tại…", false);
            });
        }

        private void OnResume(object sender, EventArgs e)
        {
            Guard("Tiếp tục", token => StartRunAsync(null));
        }

        private void OnCancel(object sender, EventArgs e)
        {
            if (!_confirmation.Confirm("Hủy phiên tải", "Hủy phiên tải này? Hóa đơn đã tải và lưu được giữ nguyên, các tháng chưa tải sẽ bị bỏ qua."))
            {
                return;
            }

            Guard("Hủy phiên", async token =>
            {
                await _orchestrator.CancelAsync(_sessionId, token);
                if (!_running) await RenderAsync(true);
            });
        }

        private void OnRetryFailed(object sender, EventArgs e)
        {
            Guard("Thử lại các lỗi", async token =>
            {
                var failed = (await _repository.GetSegmentsAsync(_sessionId, token)).Where(s => s.State == DownloadSegmentState.Failed).Select(s => s.Id).ToList();
                foreach (var id in failed)
                {
                    var segmentId = id;
                    await StartRunAsync(progress => _orchestrator.RetrySegmentAsync(_sessionId, segmentId, progress, CancellationToken.None));
                    var state = (await _repository.GetSessionAsync(_sessionId, token)).State;
                    if (state == DownloadSessionState.WaitingForLogin || state == DownloadSessionState.PausedByUser || state == DownloadSessionState.Cancelled) break;
                }
            });
        }

        /// <summary>Phiên xong thì sang Kho hóa đơn đúng chiều (mua vào/bán ra) của phiên để xem hóa đơn vừa tải (FR-JOB-011).</summary>
        private void OnOpenArchive(object sender, EventArgs e)
        {
            Guard("Mở Kho hóa đơn", async token =>
            {
                var session = await _repository.GetSessionAsync(_sessionId, token);
                if (session == null) return;
                await _navigation().NavigateAsync(ScreenId.InvoiceArchive, new ArchiveNavigationRequest(session.Direction, FileIntegrityFilter.All, SingleYearOf(session.Period)), token);
            });
        }

        /// <summary>Năm của kỳ tải khi kỳ nằm gọn trong một năm; 0 khi kỳ trải qua nhiều năm.</summary>
        private static int SingleYearOf(PeriodSpec period)
        {
            if (period == null) return 0;
            if (period.Kind != PeriodKind.Custom) return period.Year;
            return period.From.HasValue && period.To.HasValue && period.From.Value.Year == period.To.Value.Year ? period.From.Value.Year : 0;
        }

        private void OnNewDownload(object sender, EventArgs e)
        {
            Guard("Mở màn hình tải", async token =>
            {
                var result = await _navigation().NavigateAsync(ScreenId.Download, null, token);
                if (!result.Succeeded && !string.IsNullOrEmpty(result.Reason)) _view.ShowMessage(result.Reason, true);
            });
        }

        private void OnRetrySegment(object sender, SegmentRetryEventArgs e)
        {
            Guard("Thử lại đoạn", token => StartRunAsync(progress => _orchestrator.RetrySegmentAsync(_sessionId, e.SegmentId, progress, CancellationToken.None)));
        }

        // ---- Chạy phiên ----

        /// <summary>Đảm bảo đã đăng nhập, rồi chạy (hoặc thử lại đoạn) cho tới khi dừng; tự vẽ lại và báo kết quả.</summary>
        private async Task StartRunAsync(Func<IProgress<DownloadProgress>, Task> custom)
        {
            if (_running) return;
            if (_session.GetSnapshot() == null)
            {
                await _shell().LoginAsync(LoginReason.TokenExpired);
                if (_session.GetSnapshot() == null)
                {
                    _view.ShowMessage("Cần đăng nhập cổng Thuế để tiếp tục tải.", true);
                    await RenderAsync(true);
                    return;
                }
            }

            _running = true;
            await RenderAsync(true);
            try
            {
                var progress = new DirectProgress(p => Render(false));
                await _runner.RunAsync(
                    RunName,
                    token => custom != null ? custom(progress) : _orchestrator.RunAsync(_sessionId, progress, token),
                    state => { if (state.Error != null) _errors.Show(state.Error); });
            }
            finally
            {
                _running = false;
            }

            if (_disposed) return;
            await RenderAsync(true);
            var final = await _repository.GetSessionAsync(_sessionId, CancellationToken.None);
            var finalSegments = await _repository.GetSegmentsAsync(_sessionId, CancellationToken.None);
            ShowOutcome(final, finalSegments.Sum(s => s.ReceivedCount));
        }

        private void ShowOutcome(DownloadSession session, int received)
        {
            switch (session.State)
            {
                case DownloadSessionState.Completed:
                    _view.ShowMessage(
                        received == 0
                            ? "Đã tải xong nhưng cổng Thuế không trả về hóa đơn nào cho kỳ và bộ lọc đã chọn. Bấm \"Tải kỳ khác\" để thử kỳ hoặc bộ lọc khác."
                            : "Đã tải xong toàn bộ. Bấm \"Mở Kho hóa đơn\" để xem " + received.ToString("N0", CultureInfo.InvariantCulture) + " hóa đơn.",
                        false);
                    break;
                case DownloadSessionState.CompletedWithErrors:
                    _view.ShowMessage("Đã tải xong nhưng có đoạn lỗi. Bấm Thử lại để tải lại các đoạn đó.", true);
                    break;
                case DownloadSessionState.WaitingForLogin:
                    _view.ShowMessage("Phiên đăng nhập cổng Thuế đã hết hạn. Bấm Tiếp tục để đăng nhập lại và tải tiếp từ chỗ đang dở.", true);
                    break;
                case DownloadSessionState.PausedByUser:
                    _view.ShowMessage("Đã tạm dừng; checkpoint được giữ. Bấm Tiếp tục để tải tiếp.", false);
                    break;
                case DownloadSessionState.Cancelled:
                    _view.ShowMessage("Đã hủy phiên; dữ liệu đã lưu được giữ nguyên.", false);
                    break;
                case DownloadSessionState.Failed:
                    _view.ShowMessage("Không thể tiếp tục: cổng Thuế từ chối bộ lọc hoặc cấu hình. Xem lịch sử sự kiện của phiên.", true);
                    break;
            }
        }

        // ---- Hiển thị ----

        private void Render(bool force)
        {
            if (_disposed || _sessionId == Guid.Empty) return;
            if (!force && _sinceRender.Elapsed < RenderInterval) return;
            _sinceRender.Restart();
            var task = RenderAsync(false);
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task RenderAsync(bool force)
        {
            if (_disposed || _sessionId == Guid.Empty) return;

            // Số thứ tự lấy trước khi đọc: lượt vẽ bắt đầu sau luôn đọc được trạng thái mới hơn, nên lượt cũ về muộn (đọc dở trong lúc phiên vừa xong) bị bỏ
            // thay vì ghi đè lên kết quả cuối bằng trạng thái "đang chạy".
            var ticket = Interlocked.Increment(ref _renderTicket);

            // Không ConfigureAwait(false): repository thật (SQLite) hoàn tất trên luồng khác nhưng view phải được cập nhật trên luồng giao diện.
            var session = await _repository.GetSessionAsync(_sessionId, CancellationToken.None);
            var segments = await _repository.GetSegmentsAsync(_sessionId, CancellationToken.None);
            if (session == null) return;
            var model = Build(session, segments);
            lock (_renderGate)
            {
                if (ticket < _lastShownTicket) return;
                _lastShownTicket = ticket;
                _view.Show(model);
            }
        }

        private DownloadProgressViewModel Build(DownloadSession session, IList<DownloadSegment> segments)
        {
            var ordered = segments.OrderBy(s => s.Index).ToList();
            var received = ordered.Sum(s => s.ReceivedCount);
            var saved = ordered.Sum(s => s.XmlSavedCount);
            var errors = ordered.Sum(s => s.ErrorCount);
            var done = ordered.Count(s => s.State == DownloadSegmentState.Completed);
            var total = ordered.Count;
            var end = session.FinishedAtUtc ?? _clock.UtcNow;
            var elapsed = session.StartedAtUtc.HasValue ? end - session.StartedAtUtc.Value : TimeSpan.Zero;
            var running = ordered.FirstOrDefault(s => s.State == DownloadSegmentState.Running);
            var resumePoint = running ?? ordered.FirstOrDefault(s => s.State == DownloadSegmentState.Paused);

            var canRun = !_running;
            var state = session.State;
            return new DownloadProgressViewModel
            {
                SessionCode = session.Id.ToString("N").Substring(0, 8).ToUpperInvariant(),
                Subtitle = (session.Direction == InvoiceDirection.Purchase ? "Mua vào" : "Bán ra") + " · " + RangeText(ordered)
                    + (session.StartedAtUtc.HasValue ? " · Bắt đầu " + session.StartedAtUtc.Value.ToLocalTime().ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty),
                StateText = StateText(state),
                Percent = total == 0 ? 0 : done * 100 / total,
                ProgressText = done + "/" + total + " tháng",
                ReceivedText = received.ToString("N0", CultureInfo.InvariantCulture),
                ReceivedSub = "Mới " + saved.ToString("N0", CultureInfo.InvariantCulture) + " · Đã có " + Math.Max(0, received - saved - errors).ToString("N0", CultureInfo.InvariantCulture),
                SavedText = saved.ToString("N0", CultureInfo.InvariantCulture),
                SavedSub = errors == 0 ? "Không có lỗi" : errors + " hóa đơn lỗi",
                ElapsedText = Format(elapsed),
                CheckpointText = resumePoint == null ? string.Empty
                    : "Checkpoint gần nhất: tháng " + resumePoint.Range.Month.ToString("00") + " · trang " + (resumePoint.PageIndex + 1) + " · hóa đơn " + resumePoint.ItemOffset,
                CanPause = state == DownloadSessionState.Running && _running,
                CanResume = canRun && (state == DownloadSessionState.PausedByUser || state == DownloadSessionState.WaitingForLogin),
                CanCancel = !DownloadStateMachine.IsFinished(state),
                CanRetryFailed = canRun && ordered.Any(s => s.State == DownloadSegmentState.Failed),
                CanOpenArchive = canRun && (state == DownloadSessionState.Completed || state == DownloadSessionState.CompletedWithErrors),
                CanNewDownload = canRun && DownloadStateMachine.IsFinished(state),
                Rows = ordered.Select(s => Row(s, canRun)).ToList()
            };
        }

        public static SegmentRowViewModel Row(DownloadSegment s, bool canRun)
        {
            var note = string.Empty;
            if (s.State == DownloadSegmentState.Running) note = "Đang tải trang " + (s.PageIndex + 1);
            else if (s.ErrorCount > 0) note = s.ErrorCount + " lỗi" + (string.IsNullOrEmpty(s.LastError) ? string.Empty : " · " + s.LastError);
            else if (s.State == DownloadSegmentState.Completed) note = "Không có lỗi";
            else if (!string.IsNullOrEmpty(s.LastError)) note = s.LastError;

            var started = s.State != DownloadSegmentState.Pending;
            return new SegmentRowViewModel
            {
                Id = s.Id,
                Month = "Tháng " + s.Range.Month.ToString("00", CultureInfo.InvariantCulture) + "/" + s.Range.Year,
                CheckResult = string.IsNullOrEmpty(s.CheckResultCode) ? "Tất cả" : PortalOptions.CheckResultLabel(s.CheckResultCode),
                Range = s.Range.Start.ToString("dd/MM", CultureInfo.InvariantCulture) + "–" + s.Range.End.ToString("dd/MM", CultureInfo.InvariantCulture),
                StateText = SegmentStateText(s),
                Pages = started ? (s.PageIndex + (s.State == DownloadSegmentState.Completed || s.State == DownloadSegmentState.Failed ? 0 : 1)).ToString(CultureInfo.InvariantCulture) : "—",
                Received = started ? s.ReceivedCount.ToString("N0", CultureInfo.InvariantCulture) : "—",
                Saved = started ? s.XmlSavedCount.ToString("N0", CultureInfo.InvariantCulture) : "—",
                Note = note,
                CanRetry = canRun && s.State == DownloadSegmentState.Failed,
                IsRunning = s.State == DownloadSegmentState.Running,
                HasError = s.State == DownloadSegmentState.Failed
            };
        }

        public static string StateText(DownloadSessionState state)
        {
            switch (state)
            {
                case DownloadSessionState.Draft: return "Chờ bắt đầu";
                case DownloadSessionState.Running: return "Đang tải";
                case DownloadSessionState.PausedByUser: return "Đã tạm dừng";
                case DownloadSessionState.WaitingForLogin: return "Chờ đăng nhập lại";
                case DownloadSessionState.Completed: return "Hoàn tất";
                case DownloadSessionState.CompletedWithErrors: return "Hoàn tất có lỗi";
                case DownloadSessionState.Cancelled: return "Đã hủy";
                default: return "Không thể tiếp tục";
            }
        }

        public static string SegmentStateText(DownloadSegment segment)
        {
            switch (segment.State)
            {
                case DownloadSegmentState.Pending: return "Chờ";
                case DownloadSegmentState.Running: return "Đang chạy";
                case DownloadSegmentState.Completed: return "Hoàn tất";
                case DownloadSegmentState.Failed: return segment.ReceivedCount > 0 ? "Hoàn tất có lỗi" : "Lỗi";
                case DownloadSegmentState.Paused: return "Tạm dừng";
                default: return "Đã bỏ qua";
            }
        }

        private static string RangeText(IList<DownloadSegment> segments)
        {
            if (segments.Count == 0) return string.Empty;
            return segments.First().Range.Start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + "–"
                + segments.Last().Range.End.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static string Format(TimeSpan span)
        {
            return ((int)span.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" + span.Minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":" + span.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        private async Task<Guid> FindLatestAsync(CancellationToken token)
        {
            var unfinished = await _orchestrator.GetUnfinishedAsync(token);
            var latest = unfinished.OrderByDescending(u => u.Session.CreatedAtUtc).FirstOrDefault();
            return latest == null ? Guid.Empty : latest.Session.Id;
        }

        /// <summary>Chạy một thao tác điều khiển (ngắn) qua runner để lỗi được quy đổi và hiển thị thống nhất.</summary>
        private async void Guard(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }

        /// <summary>Báo tiến độ ngay trên luồng gọi (engine chạy nền); view tự chuyển về UI thread.</summary>
        private sealed class DirectProgress : IProgress<DownloadProgress>
        {
            private readonly Action<DownloadProgress> _handler;

            public DirectProgress(Action<DownloadProgress> handler)
            {
                _handler = handler;
            }

            public void Report(DownloadProgress value)
            {
                _handler(value);
            }
        }
    }
}
