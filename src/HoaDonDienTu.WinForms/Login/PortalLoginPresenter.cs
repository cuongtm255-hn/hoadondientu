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
        private LoginCaptcha _captcha;
        private bool _manual;
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
            _view.SubmitRequested += OnSubmitRequested;
            _view.RefreshCaptchaRequested += OnRefreshCaptchaRequested;
            _view.ManualLoginRequested += OnManualLoginRequested;
            _browser.LoginCandidateDetected += OnLoginCandidateDetected;
            _browser.BrowserFailed += OnBrowserFailed;
            _browser.LoginDialogClosed += OnLoginDialogClosed;

            var company = _context.HasCurrent ? _context.Current.DisplayName : string.Empty;
            _view.ShowRequest(company, _request.ExpectedTaxCode.Value);
            if (!string.IsNullOrEmpty(_browser.SimulationNotice)) _view.ShowSimulationNotice(_browser.SimulationNotice);
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
                _view.SubmitRequested -= OnSubmitRequested;
                _view.RefreshCaptchaRequested -= OnRefreshCaptchaRequested;
                _view.ManualLoginRequested -= OnManualLoginRequested;
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
                    SetState(PortalLoginState.InitializingBrowser, "Bước 1/5", "Đang chuẩn bị đăng nhập...", true, false, false);
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
                SetState(PortalLoginState.ClearingPreviousSession, "Bước 2/5", "Đang làm mới phiên đăng nhập...", true, false, false);
                await _browser.ClearSessionAsync(token).ConfigureAwait(true);
                _sessionStore.Clear();

                SetState(PortalLoginState.LoadingPortalLogin, "Bước 3/5", "Đang mở trang đăng nhập cổng Thuế...", true, false, false);
                await _browser.NavigateToLoginAsync(token).ConfigureAwait(true);

                if (_manual)
                {
                    SetState(PortalLoginState.WaitingForUser, "Bước 5/5", "Đang chờ đăng nhập thành công", false, false, false);
                    return;
                }

                await LoadCaptchaAsync(null, false).ConfigureAwait(true);
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
            await VerifyCandidateAsync().ConfigureAwait(true);
        }

        /// <summary>Đọc phiên từ trình duyệt rồi xác minh token và MST. Chỉ chạy một lần: lần gọi thứ hai thấy đã sang trạng thái xác minh nên bỏ qua.</summary>
        private async Task VerifyCandidateAsync()
        {
            if (State != PortalLoginState.WaitingForUser && State != PortalLoginState.Submitting) return;

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
                result = PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, result.Session.VerifiedTaxCode.Value);
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
                    await ReturnToLoginPageQuietlyAsync().ConfigureAwait(true);
                    var actualTaxCode = string.IsNullOrEmpty(result.Message) ? string.Empty : " (tài khoản vừa đăng nhập: " + result.Message + ")";
                    SetState(PortalLoginState.TaxCodeMismatch, "Sai MST",
                        "Tài khoản vừa đăng nhập không thuộc MST " + _request.ExpectedTaxCode.Value + actualTaxCode + ". Vui lòng đăng nhập đúng MST.",
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

        /// <summary>Lấy CAPTCHA mới từ cổng và hiện lên form. <paramref name="message"/> (nếu có) là lý do phải nhập lại.</summary>
        private async Task LoadCaptchaAsync(string message, bool isError)
        {
            var token = _cancellation.Token;
            try
            {
                SetState(PortalLoginState.LoadingCaptcha, "Bước 4/5", "Đang lấy mã CAPTCHA...", true, false, false);
                _captcha = await _browser.GetCaptchaAsync(token).ConfigureAwait(true);
                if (_finished || _disposed) return;
                _view.ShowCaptcha(_captcha == null ? null : _captcha.Svg);
                SetState(
                    PortalLoginState.WaitingForUser, "Bước 5/5",
                    message ?? "Nhập mật khẩu và mã CAPTCHA rồi bấm Đăng nhập.",
                    false, isError, false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error("Không lấy được CAPTCHA của cổng Thuế", ex);
                SetState(PortalLoginState.PortalUnavailable, "Lỗi", "Không lấy được mã CAPTCHA từ cổng Thuế. Kiểm tra kết nối rồi thử lại.", false, true, true);
            }
        }

        private async void OnRefreshCaptchaRequested(object sender, EventArgs e)
        {
            if (_finished || _manual || State != PortalLoginState.WaitingForUser) return;
            await LoadCaptchaAsync(null, false).ConfigureAwait(true);
        }

        private async void OnSubmitRequested(object sender, LoginSubmitEventArgs e)
        {
            if (_finished || _disposed || _manual || State != PortalLoginState.WaitingForUser || _captcha == null) return;

            var password = e.Password;
            var captchaText = e.CaptchaText == null ? string.Empty : e.CaptchaText.Trim();
            if (string.IsNullOrEmpty(password) || captchaText.Length == 0)
            {
                SetState(PortalLoginState.WaitingForUser, "Bước 5/5", "Nhập mật khẩu và mã CAPTCHA.", false, true, false);
                return;
            }

            // Mật khẩu và CAPTCHA chỉ đi một chiều: view xóa ô ngay, presenter không giữ lại, không ghi nhật ký.
            _view.ClearSecrets();
            SetState(PortalLoginState.Submitting, "Đăng nhập", "Đang đăng nhập cổng Thuế...", true, false, false);
            try
            {
                if (!ContextMatchesRequest())
                {
                    Finish(PortalLoginResult.Failure(PortalLoginOutcome.NotAllowed, "Doanh nghiệp đang mở đã thay đổi."));
                    return;
                }

                var outcome = await _browser.SubmitCredentialsAsync(
                    _request.ExpectedTaxCode.Value, password, _captcha, captchaText, _cancellation.Token).ConfigureAwait(true);
                password = null;
                if (_finished || _disposed || _cancellation.IsCancellationRequested) return;

                switch (outcome)
                {
                    case LoginSubmitOutcome.Accepted:
                        // Sự kiện của trình duyệt có thể đã đến trước và đang xác minh; chỉ xác minh nếu chưa ai làm.
                        if (State == PortalLoginState.Submitting) await VerifyCandidateAsync().ConfigureAwait(true);
                        break;

                    case LoginSubmitOutcome.Rejected:
                        await LoadCaptchaAsync("Mật khẩu hoặc mã CAPTCHA không đúng. Nhập lại mật khẩu và mã CAPTCHA mới.", true).ConfigureAwait(true);
                        break;

                    default:
                        await LoadCaptchaAsync("Cổng Thuế không phản hồi hoặc đang lỗi. Thử lại sau ít phút.", true).ConfigureAwait(true);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error("Không gửi được thông tin đăng nhập", ex);
                SetState(PortalLoginState.InvalidSession, "Lỗi", "Không đăng nhập được. Vui lòng thử lại.", false, true, true);
            }
        }

        private async void OnManualLoginRequested(object sender, EventArgs e)
        {
            if (_finished || _disposed || _manual || State == PortalLoginState.Submitting || State == PortalLoginState.VerifyingPortalSession) return;
            _manual = true;
            _browser.EnableManualLogin();
            _view.ShowManualMode();
            try
            {
                await PrepareAsync(false).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.Error("Không chuyển sang đăng nhập thủ công", ex);
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
        public TimeSpan ClosedDialogGrace { get; set; } = TimeSpan.FromMilliseconds(400);

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

        /// <summary>Đưa trình duyệt về trang đăng nhập để không còn hiện giao diện "đã đăng nhập" của tài khoản sai.</summary>
        private async Task ReturnToLoginPageQuietlyAsync()
        {
            try
            {
                await _browser.NavigateToLoginAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.Warning("Không quay về trang đăng nhập: " + ex.GetType().Name);
            }
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
                CanRetry = canRetry,
                CanSubmit = state == PortalLoginState.WaitingForUser && !_manual
            });
        }
    }
}
