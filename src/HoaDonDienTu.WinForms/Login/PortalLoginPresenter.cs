using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>
    /// Điều phối đăng nhập: khởi tạo trình duyệt, chờ người dùng, xác minh phiên + MST, rồi lưu phiên.
    /// Không log hay đưa token vào view state.
    /// </summary>
    public sealed class PortalLoginPresenter : IDisposable
    {
        private readonly IPortalLoginView _view;
        private readonly PortalLoginRequest _request;
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalLoginBrowser _browser;
        private readonly IPortalAuthenticationService _authentication;
        private readonly IPortalSessionStore _sessionStore;
        private readonly IApplicationLogger _logger;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private bool _initialized;
        private bool _browserReady;
        private bool _finished;
        private bool _disposed;

        public PortalLoginPresenter(
            IPortalLoginView view,
            PortalLoginRequest request,
            ICompanyContextAccessor context,
            IPortalLoginBrowser browser,
            IPortalAuthenticationService authentication,
            IPortalSessionStore sessionStore,
            IApplicationLogger logger)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (request == null) throw new ArgumentNullException("request");
            if (context == null) throw new ArgumentNullException("context");
            if (browser == null) throw new ArgumentNullException("browser");
            if (authentication == null) throw new ArgumentNullException("authentication");
            if (sessionStore == null) throw new ArgumentNullException("sessionStore");
            if (logger == null) throw new ArgumentNullException("logger");
            _view = view;
            _request = request;
            _context = context;
            _browser = browser;
            _authentication = authentication;
            _sessionStore = sessionStore;
            _logger = logger;
            State = PortalLoginState.Created;
        }

        public PortalLoginState State { get; private set; }

        /// <summary>Có giá trị sau khi form đóng.</summary>
        public PortalLoginResult Result { get; private set; }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.ViewShown += OnViewShown;
            _view.RetryRequested += OnRetryRequested;
            _view.CancelRequested += OnCancelRequested;
            _browser.LoginCandidateDetected += OnLoginCandidateDetected;
            _browser.BrowserFailed += OnBrowserFailed;
            _browser.LoginDialogClosed += OnLoginDialogClosed;

            var company = _context.HasCurrent ? _context.Current.DisplayName : string.Empty;
            _view.ShowRequest(company, _request.ExpectedTaxCode.Value);
        }

        public void Dispose()
        {
            _disposed = true;
            if (_initialized)
            {
                _initialized = false;
                _view.ViewShown -= OnViewShown;
                _view.RetryRequested -= OnRetryRequested;
                _view.CancelRequested -= OnCancelRequested;
                _browser.LoginCandidateDetected -= OnLoginCandidateDetected;
                _browser.BrowserFailed -= OnBrowserFailed;
                _browser.LoginDialogClosed -= OnLoginDialogClosed;
            }

            _cancellation.Cancel();
            _cancellation.Dispose();
            _browser.Dispose();
        }

        public async Task StartAsync()
        {
            if (!ContextMatchesRequest())
            {
                Finish(PortalLoginResult.Failure(PortalLoginOutcome.NotAllowed, "Chưa có doanh nghiệp phù hợp để đăng nhập."));
                return;
            }

            await PrepareAsync(!_browserReady).ConfigureAwait(true);
        }

        private async Task PrepareAsync(bool initializeBrowser)
        {
            var token = _cancellation.Token;
            try
            {
                if (initializeBrowser)
                {
                    SetState(PortalLoginState.InitializingBrowser, "Bước 1/4", "Đang chuẩn bị đăng nhập...", true, false, false);
                    await _browser.InitializeAsync(token).ConfigureAwait(true);
                    _view.HostBrowser(_browser.View);
                    _browserReady = true;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error("Không khởi tạo được trình duyệt đăng nhập", ex);
                SetState(PortalLoginState.BrowserUnavailable, "Lỗi", "Không mở được màn hình đăng nhập. Thử lại hoặc khởi động lại ứng dụng.", false, true, true);
                return;
            }

            try
            {
                SetState(PortalLoginState.ClearingPreviousSession, "Bước 2/4", "Đang làm mới phiên đăng nhập...", true, false, false);
                await _browser.ClearSessionAsync(token).ConfigureAwait(true);
                _sessionStore.Clear();

                SetState(PortalLoginState.LoadingPortalLogin, "Bước 3/4", "Đang mở trang đăng nhập cổng Thuế...", true, false, false);
                await _browser.NavigateToLoginAsync(token).ConfigureAwait(true);

                SetState(PortalLoginState.WaitingForUser, "Bước 4/4", "Đang chờ đăng nhập thành công", false, false, false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error("Không mở được trang đăng nhập cổng Thuế", ex);
                SetState(PortalLoginState.PortalUnavailable, "Lỗi", "Không mở được trang đăng nhập cổng Thuế. Kiểm tra kết nối rồi thử lại.", false, true, true);
            }
        }

        private async void OnViewShown(object sender, EventArgs e)
        {
            try
            {
                await StartAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.Error("Đăng nhập thất bại ngoài dự kiến", ex);
                SetState(PortalLoginState.BrowserUnavailable, "Lỗi", "Có lỗi không mong muốn.", false, true, true);
            }
        }

        private async void OnRetryRequested(object sender, EventArgs e)
        {
            if (_finished || State == PortalLoginState.VerifyingPortalSession) return;
            try
            {
                await PrepareAsync(!_browserReady).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.Error("Thử lại đăng nhập thất bại ngoài dự kiến", ex);
            }
        }

        private async void OnLoginCandidateDetected(object sender, LoginCandidateEventArgs e)
        {
            if (State != PortalLoginState.WaitingForUser) return;

            SetState(PortalLoginState.VerifyingPortalSession, "Xác minh", "Đang xác minh phiên và MST...", true, false, false);
            try
            {
                if (!ContextMatchesRequest())
                {
                    Finish(PortalLoginResult.Failure(PortalLoginOutcome.NotAllowed, "Doanh nghiệp đang mở đã thay đổi."));
                    return;
                }

                var material = await _browser.CaptureSessionAsync(_cancellation.Token).ConfigureAwait(true);
                var result = await _authentication.VerifyAsync(_context.Current, material, _cancellation.Token).ConfigureAwait(true);
                await HandleVerificationAsync(result).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error("Không xác minh được phiên đăng nhập", ex);
                SetState(PortalLoginState.InvalidSession, "Lỗi", "Không xác minh được phiên đăng nhập. Vui lòng đăng nhập lại.", false, true, true);
            }
        }

        private async Task HandleVerificationAsync(PortalLoginResult result)
        {
            // Người dùng đã hủy (hoặc form đã đóng) trong lúc xác minh: kết quả muộn không được lưu phiên.
            if (_finished || _disposed || _cancellation.IsCancellationRequested) return;

            if (result.Succeeded && !SessionMatchesRequest(result.Session))
            {
                result = PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, null);
            }

            switch (result.Outcome)
            {
                case PortalLoginOutcome.Success:
                    _sessionStore.Set(result.Session);
                    Finish(result);
                    break;

                case PortalLoginOutcome.TaxCodeMismatch:
                    await ClearBrowserSessionQuietlyAsync().ConfigureAwait(true);
                    _sessionStore.Clear();
                    SetState(PortalLoginState.TaxCodeMismatch, "Sai MST",
                        "Tài khoản vừa đăng nhập không thuộc MST " + _request.ExpectedTaxCode.Value + ". Vui lòng đăng nhập đúng MST.",
                        false, true, true);
                    break;

                case PortalLoginOutcome.PortalUnavailable:
                    SetState(PortalLoginState.PortalUnavailable, "Lỗi", "Cổng Thuế không phản hồi. Thử lại sau.", false, true, true);
                    break;

                default:
                    SetState(PortalLoginState.InvalidSession, "Lỗi", "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.", false, true, true);
                    break;
            }
        }

        private async void OnCancelRequested(object sender, EventArgs e)
        {
            if (_finished) return;
            _cancellation.Cancel();
            try
            {
                // Không giữ lại candidate token sau khi hủy.
                await ClearBrowserSessionQuietlyAsync().ConfigureAwait(true);
            }
            finally
            {
                _sessionStore.Clear();
                Finish(PortalLoginResult.Failure(PortalLoginOutcome.Cancelled, null));
            }
        }

        /// <summary>Thời gian chờ trước khi coi việc đóng hộp thoại đăng nhập là hủy: đăng nhập thành công cũng làm hộp thoại đóng.</summary>
        public TimeSpan ClosedDialogGrace { get; set; } = TimeSpan.FromMilliseconds(1500);

        private async void OnLoginDialogClosed(object sender, EventArgs e)
        {
            if (_finished || _disposed || State != PortalLoginState.WaitingForUser) return;
            try
            {
                if (ClosedDialogGrace > TimeSpan.Zero) await Task.Delay(ClosedDialogGrace, _cancellation.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Đã thấy phản hồi đăng nhập trong thời gian chờ (đang xác minh hoặc đã xong) thì không hủy.
            if (_finished || _disposed || State != PortalLoginState.WaitingForUser) return;
            OnCancelRequested(this, EventArgs.Empty);
        }

        private void OnBrowserFailed(object sender, BrowserFailureEventArgs e)
        {
            if (_finished) return;
            _logger.Warning("Trình duyệt đăng nhập báo lỗi");
            SetState(PortalLoginState.BrowserUnavailable, "Lỗi", "Không mở được trang đăng nhập cổng Thuế. Kiểm tra kết nối mạng rồi thử lại.", false, true, true);
        }

        private async Task ClearBrowserSessionQuietlyAsync()
        {
            try
            {
                await _browser.ClearSessionAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.Warning("Không xóa được phiên trình duyệt: " + ex.GetType().Name);
            }
        }

        private bool ContextMatchesRequest()
        {
            return _context.HasCurrent
                && _context.Current.CompanyId.Equals(_request.CompanyId)
                && _context.Current.TaxCode.Equals(_request.ExpectedTaxCode);
        }

        private bool SessionMatchesRequest(PortalSession session)
        {
            return session != null
                && session.CompanyId.Equals(_request.CompanyId)
                && session.VerifiedTaxCode.Equals(_request.ExpectedTaxCode);
        }

        private void Finish(PortalLoginResult result)
        {
            if (_finished) return;
            _finished = true;
            Result = result;
            State = result.Succeeded ? PortalLoginState.Succeeded : PortalLoginState.Cancelled;
            _view.CloseView();
        }

        private void SetState(PortalLoginState state, string step, string message, bool busy, bool error, bool canRetry)
        {
            if (_finished || _disposed) return;
            State = state;
            _view.ShowState(new PortalLoginViewState
            {
                State = state,
                StepText = step,
                Message = message,
                IsBusy = busy,
                IsError = error,
                CanRetry = canRetry
            });
        }
    }
}
