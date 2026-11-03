using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Downloads
{
    /// <summary>Contract theo DD-05 mục 4. Tiếp tục một phiên chính là gọi lại <see cref="RunAsync"/>.</summary>
    public interface IDownloadOrchestrator
    {
        /// <exception cref="EntitlementDeniedException"/>
        /// <exception cref="StorageGuardException">Không có doanh nghiệp đang mở hoặc phiên cổng Thuế không khớp MST.</exception>
        Task<Guid> CreateSessionAsync(DownloadPlan plan, CancellationToken cancellationToken);

        /// <summary>Chạy hoặc tiếp tục từ checkpoint: phiên Draft, đang bị ngắt, PausedByUser hoặc WaitingForLogin.</summary>
        Task RunAsync(Guid sessionId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken);

        /// <summary>Dừng an toàn sau hóa đơn hiện tại, ghi checkpoint; không chờ dừng xong.</summary>
        Task PauseAsync(Guid sessionId, CancellationToken cancellationToken);

        /// <summary>Hủy sau hóa đơn hiện tại; dữ liệu đã lưu được giữ nguyên (BR-011).</summary>
        Task CancelAsync(Guid sessionId, CancellationToken cancellationToken);

        /// <summary>Đặt lại một đoạn lỗi về đầu rồi chạy tiếp phiên; khóa chống trùng ngăn bản ghi trùng.</summary>
        Task RetrySegmentAsync(Guid sessionId, Guid segmentId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken);

        /// <summary>Phiên chưa hoàn tất của doanh nghiệp đang mở (FR-JOB-010).</summary>
        Task<IList<DownloadSessionSummary>> GetUnfinishedAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Lúc mở doanh nghiệp: phiên còn ghi "đang chạy" nhưng không có lượt chạy nào (tiến trình đã thoát hoặc mất điện) được đưa về
        /// tạm dừng cùng đoạn dở dang, giữ checkpoint để người dùng bấm Tiếp tục (FR-JOB-010, AC-07). Trả số phiên đã chuyển.
        /// </summary>
        Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Thực thi kế hoạch tải tuần tự theo đoạn tháng (DD-05 mục 5): lặp trang đến hết cursor, ghi checkpoint sau từng hóa đơn,
    /// thử lại lỗi tạm thời, dừng chờ đăng nhập khi hết token. Không chạy song song (FR-JOB-002).
    /// </summary>
    public sealed class DownloadOrchestrator : IDownloadOrchestrator
    {
        private enum SegmentOutcome
        {
            Completed,
            Failed,
            Paused,
            Cancelled,
            WaitingForLogin,
            StorageBlocked,
            Fatal
        }

        private sealed class RunEntry
        {
            public Guid SessionId;
            public CompanyId CompanyId;
            public volatile bool PauseRequested;
            public volatile bool CancelRequested;
            public readonly TaskCompletionSource<bool> Done = new TaskCompletionSource<bool>();
        }

        private sealed class RunState
        {
            public DownloadSession Session;
            public IList<DownloadSegment> Segments;
            public RunEntry Entry;
            public IProgress<DownloadProgress> Progress;
        }

        private readonly IDownloadRepository _repository;
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalSessionStore _portalSession;
        private readonly Func<IPortalInvoiceClient> _clientFactory;
        private readonly IInvoiceStore _store;
        private readonly IRateLimiter _rateLimiter;
        private readonly IRetryPolicy _retry;
        private readonly IEntitlementService _entitlement;
        private readonly IClock _clock;
        private readonly IApplicationLogger _logger;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly ConcurrentDictionary<Guid, RunEntry> _active = new ConcurrentDictionary<Guid, RunEntry>();

        public DownloadOrchestrator(
            IDownloadRepository repository,
            ICompanyContextAccessor context,
            IPortalSessionStore portalSession,
            Func<IPortalInvoiceClient> clientFactory,
            IInvoiceStore store,
            IRateLimiter rateLimiter,
            IRetryPolicy retry,
            IEntitlementService entitlement,
            IClock clock,
            IApplicationLogger logger,
            Func<TimeSpan, CancellationToken, Task> delay)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (context == null) throw new ArgumentNullException("context");
            if (portalSession == null) throw new ArgumentNullException("portalSession");
            if (clientFactory == null) throw new ArgumentNullException("clientFactory");
            if (store == null) throw new ArgumentNullException("store");
            if (rateLimiter == null) throw new ArgumentNullException("rateLimiter");
            if (retry == null) throw new ArgumentNullException("retry");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (clock == null) throw new ArgumentNullException("clock");
            if (logger == null) throw new ArgumentNullException("logger");
            _repository = repository;
            _context = context;
            _portalSession = portalSession;
            _clientFactory = clientFactory;
            _store = store;
            _rateLimiter = rateLimiter;
            _retry = retry;
            _entitlement = entitlement;
            _clock = clock;
            _logger = logger;
            _delay = delay ?? ((span, token) => Task.Delay(span, token));
        }

        // ---- Tạo phiên ----

        public async Task<Guid> CreateSessionAsync(DownloadPlan plan, CancellationToken cancellationToken)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            _entitlement.Demand(AppFeature.NewDownload);
            var company = RequireCompany(null);

            var session = new DownloadSession(
                Guid.NewGuid(), company.CompanyId, company.TaxCode, company.StorageRoot, plan.Direction, plan.Period, plan.Filter,
                _clock.UtcNow, Environment.UserName);
            var segments = ExpandSegments(session, plan);
            await _repository.CreateAsync(session, segments, cancellationToken).ConfigureAwait(false);
            await EventAsync(session, null, DownloadEventLevel.Info, "SESSION_CREATED", "Tạo phiên tải gồm " + segments.Count + " đoạn.").ConfigureAwait(false);
            return session.Id;
        }

        /// <summary>
        /// Mỗi tháng một đoạn; riêng mua vào không chọn kết quả kiểm tra thì mỗi tháng ba đoạn (<c>ttxly</c> 5, 6, 8) vì cổng không có "Tất cả" (T05-12).
        /// </summary>
        private static List<DownloadSegment> ExpandSegments(DownloadSession session, DownloadPlan plan)
        {
            var passes = string.IsNullOrWhiteSpace(plan.Filter.CheckResultCode)
                ? PortalOptions.CheckResultPasses(plan.Direction)
                : new List<string>();
            var segments = new List<DownloadSegment>();
            foreach (var range in plan.Segments)
            {
                if (passes.Count == 0)
                {
                    segments.Add(new DownloadSegment(Guid.NewGuid(), session.Id, segments.Count, range));
                    continue;
                }

                foreach (var code in passes)
                {
                    segments.Add(new DownloadSegment(Guid.NewGuid(), session.Id, segments.Count, range) { CheckResultCode = code });
                }
            }

            return segments;
        }

        /// <summary>Bộ lọc của phiên, thay mã kết quả kiểm tra bằng mã riêng của đoạn khi có.</summary>
        private static InvoiceSearchFilter FilterFor(DownloadSession session, DownloadSegment segment)
        {
            if (string.IsNullOrEmpty(segment.CheckResultCode)) return session.Filter;
            var filter = session.Filter.Clone();
            filter.CheckResultCode = segment.CheckResultCode;
            return filter;
        }

        // ---- Chạy / tiếp tục ----

        public async Task RunAsync(Guid sessionId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            await RunCoreAsync(sessionId, null, progress, cancellationToken).ConfigureAwait(false);
        }

        public async Task RetrySegmentAsync(Guid sessionId, Guid segmentId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            await RunCoreAsync(sessionId, segmentId, progress, cancellationToken).ConfigureAwait(false);
        }

        private async Task RunCoreAsync(Guid sessionId, Guid? retrySegmentId, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            _entitlement.Demand(AppFeature.NewDownload);
            var session = await RequireSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            RequireCompany(session);
            var entry = Register(session);
            var state = new RunState { Session = session, Entry = entry, Progress = progress };
            try
            {
                state.Segments = await _repository.GetSegmentsAsync(sessionId, cancellationToken).ConfigureAwait(false);
                if (retrySegmentId.HasValue) await ResetSegmentForRetryAsync(state, retrySegmentId.Value, cancellationToken).ConfigureAwait(false);

                if (session.State != DownloadSessionState.Running)
                {
                    session.TransitionTo(DownloadSessionState.Running, _clock.UtcNow);
                    await _repository.SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
                }

                await EventAsync(session, null, DownloadEventLevel.Info, "RUN_STARTED", "Bắt đầu hoặc tiếp tục phiên tải.").ConfigureAwait(false);
                Report(state, -1);

                if (_portalSession.GetSnapshot() == null)
                {
                    await EnterWaitingForLoginAsync(state, null, "Chưa đăng nhập cổng Thuế.").ConfigureAwait(false);
                    return;
                }

                await ExecuteAsync(state, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Đóng ứng dụng hoặc người dùng hủy thao tác: giữ checkpoint và để phiên ở trạng thái tạm dừng để tiếp tục sau.
                await SafePauseAsync(state).ConfigureAwait(false);
                throw;
            }
            finally
            {
                RunEntry removed;
                _active.TryRemove(sessionId, out removed);
                entry.Done.TrySetResult(true);
            }
        }

        private async Task ExecuteAsync(RunState state, CancellationToken ct)
        {
            var session = state.Session;
            foreach (var segment in state.Segments.OrderBy(s => s.Index).ToList())
            {
                if (segment.State == DownloadSegmentState.Completed || segment.State == DownloadSegmentState.Skipped || segment.State == DownloadSegmentState.Failed)
                {
                    continue;
                }

                var outcome = await RunSegmentAsync(state, segment, ct).ConfigureAwait(false);
                switch (outcome)
                {
                    case SegmentOutcome.Completed:
                    case SegmentOutcome.Failed:
                        continue;
                    case SegmentOutcome.Paused:
                        await EndAsync(state, DownloadSessionState.PausedByUser, "PAUSED", "Đã tạm dừng, checkpoint được giữ.").ConfigureAwait(false);
                        return;
                    case SegmentOutcome.StorageBlocked:
                        await EndAsync(state, DownloadSessionState.PausedByUser, "STORAGE_BLOCKED", "Không ghi được vào kho (hết dung lượng hoặc không có quyền); đã tạm dừng.").ConfigureAwait(false);
                        return;
                    case SegmentOutcome.WaitingForLogin:
                        await EndAsync(state, DownloadSessionState.WaitingForLogin, "AUTH_EXPIRED", "Phiên cổng Thuế hết hạn; chờ đăng nhập lại.").ConfigureAwait(false);
                        return;
                    case SegmentOutcome.Cancelled:
                        await SkipRemainingAsync(state).ConfigureAwait(false);
                        await EndAsync(state, DownloadSessionState.Cancelled, "CANCELLED", "Đã hủy phiên; dữ liệu đã lưu được giữ nguyên.").ConfigureAwait(false);
                        return;
                    default:
                        await SkipRemainingAsync(state).ConfigureAwait(false);
                        await EndAsync(state, DownloadSessionState.Failed, "FATAL", "Không thể tiếp tục: cấu hình hoặc bộ lọc bị cổng Thuế từ chối.").ConfigureAwait(false);
                        return;
                }
            }

            var anyFailed = state.Segments.Any(s => s.State == DownloadSegmentState.Failed);
            await EndAsync(
                state,
                anyFailed ? DownloadSessionState.CompletedWithErrors : DownloadSessionState.Completed,
                anyFailed ? "COMPLETED_WITH_ERRORS" : "COMPLETED",
                anyFailed ? "Hoàn tất nhưng có đoạn lỗi cần thử lại." : "Hoàn tất.").ConfigureAwait(false);
        }

        // ---- Một đoạn ----

        private async Task<SegmentOutcome> RunSegmentAsync(RunState state, DownloadSegment segment, CancellationToken ct)
        {
            var session = state.Session;
            if (segment.State != DownloadSegmentState.Running)
            {
                segment.TransitionTo(DownloadSegmentState.Running);
            }

            await _repository.SaveSegmentAsync(segment, ct).ConfigureAwait(false);
            Report(state, segment.Index);

            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var control = CheckControl(state.Entry);
                    if (control.HasValue) return await StopSegmentAsync(state, segment, control.Value).ConfigureAwait(false);

                    await _rateLimiter.WaitAsync(ct).ConfigureAwait(false);
                    var request = new InvoiceSearchRequest(session.Direction, segment.Range.Start, segment.Range.End)
                    {
                        Filter = FilterFor(session, segment),
                        Cursor = segment.Cursor
                    };
                    var page = await CallAsync(client => client.SearchAsync(request, ct), state, segment, ct).ConfigureAwait(false);

                    var items = page.Items;
                    for (var i = segment.ItemOffset; i < items.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        control = CheckControl(state.Entry);
                        if (control.HasValue) return await StopSegmentAsync(state, segment, control.Value).ConfigureAwait(false);

                        var invoice = items[i];
                        InvoiceSaveResult? saved = null;
                        try
                        {
                            var xml = await CallAsync(
                                client => client.DownloadXmlAsync(new PortalInvoiceRef(session.Direction, invoice), ct), state, segment, ct).ConfigureAwait(false);
                            saved = await _store.UpsertAsync(session, invoice, xml, ct).ConfigureAwait(false);
                        }
                        catch (PortalApiException ex) when (ex.Kind != PortalErrorKind.AuthExpired && ex.Kind != PortalErrorKind.FilterRejected && ex.Kind != PortalErrorKind.Fatal)
                        {
                            // Một hóa đơn lỗi không làm mất cả đoạn: ghi nhận lỗi và đi tiếp; đoạn sẽ ở trạng thái Failed để thử lại (BR-007).
                            segment.ErrorCount++;
                            segment.LastError = Describe(ex);
                            await EventAsync(session, segment, DownloadEventLevel.Error, "INVOICE_FAILED", segment.LastError).ConfigureAwait(false);
                        }
                        catch (HoaDonDienTu.Application.Invoices.InvoiceStorageException ex)
                        {
                            // XML hỏng hoặc đường dẫn quá dài: chỉ hóa đơn này bị từ chối; đoạn sẽ ở trạng thái lỗi để xem lại (FR-STORE-010).
                            segment.ErrorCount++;
                            segment.LastError = ex.Message;
                            await EventAsync(session, segment, DownloadEventLevel.Error, "INVOICE_REJECTED", ex.Message).ConfigureAwait(false);
                        }
                        catch (IOException ex)
                        {
                            segment.LastError = "Không ghi được vào kho";
                            _logger.Error("Ghi kho thất bại khi tải hóa đơn", ex);
                            await EventAsync(session, segment, DownloadEventLevel.Error, "STORAGE_WRITE_FAILED", segment.LastError).ConfigureAwait(false);
                            segment.TransitionTo(DownloadSegmentState.Paused);
                            await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                            return SegmentOutcome.StorageBlocked;
                        }

                        segment.ReceivedCount++;
                        if (saved == InvoiceSaveResult.Saved) segment.XmlSavedCount++;
                        segment.ItemOffset = i + 1;
                        await _repository.SaveSegmentAsync(segment, ct).ConfigureAwait(false);
                        Report(state, segment.Index);
                    }

                    segment.Cursor = page.NextCursor;
                    segment.ItemOffset = 0;
                    segment.PageIndex++;
                    await _repository.SaveSegmentAsync(segment, ct).ConfigureAwait(false);
                    Report(state, segment.Index);

                    if (!page.HasMore) break;
                }

                var failed = segment.ErrorCount > 0;
                segment.TransitionTo(failed ? DownloadSegmentState.Failed : DownloadSegmentState.Completed);
                await _repository.SaveSegmentAsync(segment, ct).ConfigureAwait(false);
                await EventAsync(
                    session, segment, failed ? DownloadEventLevel.Warning : DownloadEventLevel.Info,
                    failed ? "SEGMENT_FAILED" : "SEGMENT_COMPLETED",
                    segment.Range + ": nhận " + segment.ReceivedCount + ", lưu " + segment.XmlSavedCount + ", lỗi " + segment.ErrorCount).ConfigureAwait(false);
                Report(state, segment.Index);
                return failed ? SegmentOutcome.Failed : SegmentOutcome.Completed;
            }
            catch (PortalApiException ex)
            {
                return await HandlePortalErrorAsync(state, segment, ex).ConfigureAwait(false);
            }
        }

        private async Task<SegmentOutcome> HandlePortalErrorAsync(RunState state, DownloadSegment segment, PortalApiException ex)
        {
            segment.LastError = Describe(ex);
            switch (ex.Kind)
            {
                case PortalErrorKind.AuthExpired:
                    segment.TransitionTo(DownloadSegmentState.Paused);
                    await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                    return SegmentOutcome.WaitingForLogin;

                case PortalErrorKind.FilterRejected:
                case PortalErrorKind.Fatal:
                    segment.ErrorCount++;
                    segment.TransitionTo(DownloadSegmentState.Failed);
                    await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                    await EventAsync(state.Session, segment, DownloadEventLevel.Error, "SEGMENT_FATAL", segment.LastError).ConfigureAwait(false);
                    return SegmentOutcome.Fatal;

                default:
                    // Transient hết lượt thử, RateLimited hết lượt, SchemaChanged: dừng đoạn này, các đoạn sau vẫn chạy.
                    segment.ErrorCount++;
                    segment.TransitionTo(DownloadSegmentState.Failed);
                    await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                    await EventAsync(
                        state.Session, segment, DownloadEventLevel.Error,
                        ex.Kind == PortalErrorKind.SchemaChanged ? "SCHEMA_CHANGED" : "SEGMENT_FAILED", segment.LastError).ConfigureAwait(false);
                    Report(state, segment.Index);
                    return SegmentOutcome.Failed;
            }
        }

        private async Task<SegmentOutcome> StopSegmentAsync(RunState state, DownloadSegment segment, SegmentOutcome requested)
        {
            segment.TransitionTo(DownloadSegmentState.Paused);
            await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
            return requested;
        }

        private static SegmentOutcome? CheckControl(RunEntry entry)
        {
            if (entry.CancelRequested) return SegmentOutcome.Cancelled;
            if (entry.PauseRequested) return SegmentOutcome.Paused;
            return null;
        }

        /// <summary>Gọi cổng Thuế với chính sách thử lại; AuthExpired không thử lại mà nổi lên để dừng chờ đăng nhập.</summary>
        private async Task<T> CallAsync<T>(Func<IPortalInvoiceClient, Task<T>> operation, RunState state, DownloadSegment segment, CancellationToken ct)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    IPortalInvoiceClient client;
                    try
                    {
                        client = _clientFactory();
                    }
                    catch (InvalidOperationException)
                    {
                        // Phiên cổng Thuế đã bị xóa hoặc không còn khớp doanh nghiệp: coi như hết hạn đăng nhập.
                        throw new PortalApiException(PortalErrorKind.AuthExpired, "Chưa có phiên cổng Thuế hợp lệ", null, null);
                    }

                    return await operation(client).ConfigureAwait(false);
                }
                catch (PortalApiException ex)
                {
                    attempt++;
                    var delay = _retry.NextDelay(attempt, ex);
                    if (delay == null) throw;
                    segment.RetryCount++;
                    await EventAsync(state.Session, segment, DownloadEventLevel.Warning, "RETRY",
                        "Thử lại lần " + attempt + " sau " + (int)delay.Value.TotalSeconds + " giây (" + ex.Kind + ").").ConfigureAwait(false);
                    await _delay(delay.Value, ct).ConfigureAwait(false);
                }
            }
        }

        // ---- Kết thúc / dừng ----

        private async Task EndAsync(RunState state, DownloadSessionState target, string code, string message)
        {
            var session = state.Session;
            session.TransitionTo(target, _clock.UtcNow);
            await _repository.SaveSessionAsync(session, CancellationToken.None).ConfigureAwait(false);
            await EventAsync(
                session, null, target == DownloadSessionState.Failed ? DownloadEventLevel.Error : DownloadEventLevel.Info, code, message).ConfigureAwait(false);
            Report(state, -1);
        }

        private async Task EnterWaitingForLoginAsync(RunState state, DownloadSegment segment, string message)
        {
            await EndAsync(state, DownloadSessionState.WaitingForLogin, "AUTH_REQUIRED", message).ConfigureAwait(false);
        }

        private async Task SkipRemainingAsync(RunState state)
        {
            foreach (var segment in state.Segments)
            {
                if (segment.State == DownloadSegmentState.Pending || segment.State == DownloadSegmentState.Paused || segment.State == DownloadSegmentState.Running)
                {
                    segment.TransitionTo(DownloadSegmentState.Skipped);
                    await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        private async Task SafePauseAsync(RunState state)
        {
            try
            {
                if (state.Session == null || state.Segments == null) return;
                foreach (var segment in state.Segments.Where(s => s.State == DownloadSegmentState.Running))
                {
                    segment.TransitionTo(DownloadSegmentState.Paused);
                    await _repository.SaveSegmentAsync(segment, CancellationToken.None).ConfigureAwait(false);
                }

                if (state.Session.State == DownloadSessionState.Running)
                {
                    state.Session.TransitionTo(DownloadSessionState.PausedByUser, _clock.UtcNow);
                    await _repository.SaveSessionAsync(state.Session, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Không ghi được checkpoint khi bị ngắt", ex);
            }
        }

        // ---- Tạm dừng / hủy / thử lại ----

        public async Task PauseAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            RunEntry entry;
            if (_active.TryGetValue(sessionId, out entry))
            {
                entry.PauseRequested = true;
                return;
            }

            var session = await RequireSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.State == DownloadSessionState.Running)
            {
                // Phiên bị ngắt giữa chừng (tiến trình đã thoát): đánh dấu tạm dừng để người dùng tiếp tục.
                session.TransitionTo(DownloadSessionState.PausedByUser, _clock.UtcNow);
                await _repository.SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task CancelAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            RunEntry entry;
            if (_active.TryGetValue(sessionId, out entry))
            {
                entry.CancelRequested = true;
                return;
            }

            var session = await RequireSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (DownloadStateMachine.IsFinished(session.State)) return;

            var segments = await _repository.GetSegmentsAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var state = new RunState { Session = session, Segments = segments };
            await SkipRemainingAsync(state).ConfigureAwait(false);
            session.TransitionTo(DownloadSessionState.Cancelled, _clock.UtcNow);
            await _repository.SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
            await EventAsync(session, null, DownloadEventLevel.Info, "CANCELLED", "Đã hủy phiên; dữ liệu đã lưu được giữ nguyên.").ConfigureAwait(false);
        }

        private async Task ResetSegmentForRetryAsync(RunState state, Guid segmentId, CancellationToken ct)
        {
            var segment = state.Segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment == null) throw new InvalidDownloadStateException("Không tìm thấy đoạn cần thử lại.");
            if (segment.State != DownloadSegmentState.Failed) throw new InvalidDownloadStateException("Chỉ thử lại được đoạn đang lỗi.");

            // Bắt đầu lại từ đầu: hóa đơn lỗi đã được bỏ qua nên cursor cũ không còn đủ; khóa chống trùng giữ cho dữ liệu không nhân đôi.
            segment.TransitionTo(DownloadSegmentState.Pending);
            segment.Cursor = null;
            segment.ItemOffset = 0;
            segment.PageIndex = 0;
            segment.ReceivedCount = 0;
            segment.XmlSavedCount = 0;
            segment.ErrorCount = 0;
            segment.LastError = null;
            segment.RetryCount++;
            await _repository.SaveSegmentAsync(segment, ct).ConfigureAwait(false);
        }

        public async Task<IList<DownloadSessionSummary>> GetUnfinishedAsync(CancellationToken cancellationToken)
        {
            var result = new List<DownloadSessionSummary>();
            if (!_context.HasCurrent) return result;

            var sessions = await _repository.GetUnfinishedAsync(_context.Current.CompanyId, cancellationToken).ConfigureAwait(false);
            foreach (var session in sessions)
            {
                var segments = await _repository.GetSegmentsAsync(session.Id, cancellationToken).ConfigureAwait(false);
                result.Add(new DownloadSessionSummary(
                    session,
                    segments.Count,
                    segments.Count(s => s.State == DownloadSegmentState.Completed),
                    segments.Sum(s => s.ReceivedCount),
                    segments.Sum(s => s.XmlSavedCount),
                    segments.Sum(s => s.ErrorCount)));
            }

            return result;
        }

        public async Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken)
        {
            if (!_context.HasCurrent) return 0;
            var companyId = _context.Current.CompanyId;
            var recovered = 0;
            var sessions = await _repository.GetUnfinishedAsync(companyId, cancellationToken).ConfigureAwait(false);
            foreach (var session in sessions)
            {
                if (session.State != DownloadSessionState.Running) continue;

                // Giữ chỗ phiên trong _active suốt lúc sửa: lượt Tiếp tục bấm đúng lúc này bị từ chối thay vì bị ghi đè trạng thái.
                // Phiên đang chạy thật đã nằm sẵn trong _active nên TryAdd thất bại và không bị đụng tới.
                var entry = new RunEntry { SessionId = session.Id, CompanyId = companyId };
                if (!_active.TryAdd(session.Id, entry)) continue;
                try
                {
                    var segments = await _repository.GetSegmentsAsync(session.Id, cancellationToken).ConfigureAwait(false);
                    foreach (var segment in segments.Where(s => s.State == DownloadSegmentState.Running))
                    {
                        segment.TransitionTo(DownloadSegmentState.Paused);
                        await _repository.SaveSegmentAsync(segment, cancellationToken).ConfigureAwait(false);
                    }

                    session.TransitionTo(DownloadSessionState.PausedByUser, _clock.UtcNow);
                    await _repository.SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
                    await EventAsync(session, null, DownloadEventLevel.Warning, "INTERRUPTED",
                        "Phiên bị ngắt giữa chừng khi ứng dụng tắt; đã tạm dừng, bấm Tiếp tục để chạy tiếp từ checkpoint.").ConfigureAwait(false);
                    recovered++;
                }
                finally
                {
                    RunEntry removed;
                    _active.TryRemove(session.Id, out removed);
                    entry.Done.TrySetResult(true);
                }
            }

            return recovered;
        }

        // ---- Phiên đang chạy (cho chuyển MST / đóng ứng dụng) ----

        public bool HasActiveRun(CompanyId companyId)
        {
            return _active.Values.Any(e => e.CompanyId.Equals(companyId));
        }

        /// <summary>Yêu cầu các phiên đang chạy của doanh nghiệp dừng (tạm dừng hoặc hủy) rồi chờ chúng dừng hẳn.</summary>
        public async Task StopActiveAsync(CompanyId companyId, bool cancel, CancellationToken cancellationToken)
        {
            var entries = _active.Values.Where(e => e.CompanyId.Equals(companyId)).ToList();
            foreach (var entry in entries)
            {
                if (cancel) entry.CancelRequested = true;
                else entry.PauseRequested = true;
            }

            await WaitAsync(entries, cancellationToken).ConfigureAwait(false);
        }

        public async Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            await WaitAsync(_active.Values.Where(e => e.CompanyId.Equals(companyId)).ToList(), cancellationToken).ConfigureAwait(false);
        }

        private static async Task WaitAsync(IList<RunEntry> entries, CancellationToken cancellationToken)
        {
            foreach (var entry in entries)
            {
                var finished = await Task.WhenAny(entry.Done.Task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
                if (finished != entry.Done.Task) cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // ---- Hỗ trợ ----

        private RunEntry Register(DownloadSession session)
        {
            var entry = new RunEntry { SessionId = session.Id, CompanyId = session.CompanyId };
            if (_active.Values.Any(e => e.CompanyId.Equals(session.CompanyId)))
            {
                throw new InvalidDownloadStateException("Doanh nghiệp này đang có một phiên tải chạy; chỉ chạy tuần tự từng phiên.");
            }

            if (!_active.TryAdd(session.Id, entry)) throw new InvalidDownloadStateException("Phiên tải này đang chạy.");
            return entry;
        }

        /// <summary>Doanh nghiệp đang mở và phiên cổng Thuế phải khớp (FR-JOB-013, FR-JOB-014).</summary>
        private CompanyContext RequireCompany(DownloadSession session)
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var company = _context.Current;
            if (session != null && !session.CompanyId.Equals(company.CompanyId))
            {
                throw new StorageGuardException("Phiên tải thuộc doanh nghiệp khác với doanh nghiệp đang mở.");
            }

            var portal = _portalSession.GetSnapshot();
            if (portal != null && !portal.TaxCode.Equals(company.TaxCode))
            {
                throw new StorageGuardException("Phiên cổng Thuế không thuộc doanh nghiệp đang mở.");
            }

            return company;
        }

        private async Task<DownloadSession> RequireSessionAsync(Guid sessionId, CancellationToken ct)
        {
            var session = await _repository.GetSessionAsync(sessionId, ct).ConfigureAwait(false);
            if (session == null) throw new InvalidDownloadStateException("Không tìm thấy phiên tải.");
            return session;
        }

        private Task EventAsync(DownloadSession session, DownloadSegment segment, DownloadEventLevel level, string code, string message)
        {
            return _repository.AddEventAsync(
                new DownloadEvent(session.Id, segment == null ? (Guid?)null : segment.Id, _clock.UtcNow, level, code, message), CancellationToken.None);
        }

        private static string Describe(PortalApiException ex)
        {
            return ex.Kind + (ex.HttpStatus.HasValue ? " (HTTP " + ex.HttpStatus.Value + ")" : string.Empty);
        }

        private void Report(RunState state, int segmentIndex)
        {
            if (state.Progress == null) return;
            var segments = state.Segments ?? new List<DownloadSegment>();
            var current = segmentIndex >= 0 ? segments.FirstOrDefault(s => s.Index == segmentIndex) : null;
            state.Progress.Report(new DownloadProgress(
                state.Session.Id,
                state.Session.State,
                segmentIndex,
                segments.Count,
                current == null ? string.Empty : current.Range + (string.IsNullOrEmpty(current.CheckResultCode) ? string.Empty : " · " + PortalOptions.CheckResultLabel(current.CheckResultCode)),
                segments.Sum(s => s.ReceivedCount),
                segments.Sum(s => s.XmlSavedCount),
                segments.Sum(s => s.ErrorCount)));
        }
    }

    /// <summary>Cầu nối engine tải với việc chuyển MST và đóng ứng dụng (DD-01 mục 9, AC-15).</summary>
    public sealed class DownloadJobService : IActiveJobService
    {
        private readonly DownloadOrchestrator _orchestrator;

        public DownloadJobService(DownloadOrchestrator orchestrator)
        {
            if (orchestrator == null) throw new ArgumentNullException("orchestrator");
            _orchestrator = orchestrator;
        }

        public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_orchestrator.HasActiveRun(companyId));
        }

        public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return _orchestrator.WaitForCompletionAsync(companyId, cancellationToken);
        }

        public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return _orchestrator.StopActiveAsync(companyId, false, cancellationToken);
        }

        public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return _orchestrator.StopActiveAsync(companyId, true, cancellationToken);
        }
    }
}
