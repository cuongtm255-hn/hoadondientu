using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.Handler;
using CefSharp.WinForms;
using HoaDonDienTu.Application.Authentication;

namespace HoaDonDienTu.WinForms.Login
{
    /// <summary>Khởi tạo và tắt Chromium một lần cho cả tiến trình (CefSharp không cho khởi tạo lại).</summary>
    public static class CefRuntimeHost
    {
        private static readonly object Gate = new object();
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            lock (Gate)
            {
                if (_initialized) return;
                var settings = new CefSettings
                {
                    LogSeverity = LogSeverity.Disable,
                    Locale = "vi",
                    PersistSessionCookies = false
                };

                // Không đặt CachePath: bộ nhớ đệm, cookie và storage chỉ tồn tại trong bộ nhớ, không ghi gì xuống đĩa.
                if (!Cef.Initialize(settings, true, (IBrowserProcessHandler)null))
                {
                    throw new InvalidOperationException("Không khởi tạo được trình duyệt nhúng (Chromium).");
                }

                _initialized = true;
            }
        }

        public static void Shutdown()
        {
            lock (Gate)
            {
                if (!_initialized) return;
                _initialized = false;
                Cef.Shutdown();
            }
        }
    }

    /// <summary>
    /// Trình duyệt đăng nhập thật bằng CefSharp 109 (ADR 0007, G-01). Người dùng tự nhập tài khoản, mật khẩu và CAPTCHA; ứng dụng chỉ đọc phản hồi
    /// <c>{"token": "..."}</c> của <c>POST /api/security-taxpayer/authenticate</c> và cookie của host cổng (api-samples mục 2, DD-01 mục 10.6a). Không đọc thân
    /// request (mật khẩu, CAPTCHA), không ghi token/cookie xuống đĩa hay nhật ký. Mỗi lần đăng nhập dùng một <c>RequestContext</c> riêng chỉ trong bộ nhớ nên
    /// cookie và storage của một MST không lọt sang MST khác.
    /// </summary>
    public sealed class CefPortalLoginBrowser : IPortalLoginBrowser
    {
        private readonly PortalLoginTraffic _traffic;
        private readonly PortalLoginTraffic.ResponseBuffer _response = new PortalLoginTraffic.ResponseBuffer();
        private ChromiumWebBrowser _browser;
        private IRequestContext _context;
        private readonly System.Windows.Forms.Timer _closeWatch = new System.Windows.Forms.Timer { Interval = 250 };
        private bool _disposed;
        private bool _watching;
        private bool _manual;
        private TaskCompletionSource<bool> _pageLoaded = new TaskCompletionSource<bool>();

        /// <summary>Thời gian tối đa chờ trang cổng tải xong và chờ mỗi lời gọi API trong trang.</summary>
        private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

        public CefPortalLoginBrowser(PortalLoginTraffic traffic)
        {
            if (traffic == null) throw new ArgumentNullException("traffic");
            _traffic = traffic;
            _closeWatch.Tick += OnCloseWatchTick;
        }

        public Control View
        {
            get { return _browser; }
        }

        public event EventHandler<LoginCandidateEventArgs> LoginCandidateDetected;
        public event EventHandler<BrowserNavigationEventArgs> NavigationChanged;
        public event EventHandler<BrowserFailureEventArgs> BrowserFailed;
        public event EventHandler LoginDialogClosed;

        public string SimulationNotice
        {
            get { return null; }
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                CefRuntimeHost.EnsureInitialized();
                CreateBrowser();
            }
            catch (Exception ex)
            {
                // Thiếu file Chromium, bị antivirus chặn...: báo thân thiện, chi tiết kỹ thuật không đưa cho người dùng.
                Raise(BrowserFailed, new BrowserFailureEventArgs("Không mở được trình duyệt đăng nhập: " + ex.GetType().Name));
            }

            return Task.FromResult(0);
        }

        public async Task ClearSessionAsync(CancellationToken cancellationToken)
        {
            _response.Clear();
            if (_context == null) return;
            var cookies = _context.GetCookieManager(null);
            if (cookies != null) await cookies.DeleteCookiesAsync(string.Empty, string.Empty).ConfigureAwait(true);

            // Trang web của cổng giữ trạng thái đăng nhập cả trong localStorage/sessionStorage; xóa cookie thôi thì giao diện vẫn hiện "đã đăng nhập".
            if (_browser != null && _browser.IsBrowserInitialized)
            {
                _browser.ExecuteScriptAsync("try{localStorage.clear();sessionStorage.clear();}catch(e){}");
            }
        }

        public Task NavigateToLoginAsync(CancellationToken cancellationToken)
        {
            // Trang tải xong mới gọi được API cùng gốc (cookie WAF được cấp ở lần tải này); GetCaptchaAsync chờ cờ này.
            _pageLoaded = new TaskCompletionSource<bool>();
            if (_browser != null) _browser.LoadUrl(_traffic.LoginUrl);
            return Task.FromResult(0);
        }

        public void EnableManualLogin()
        {
            _manual = true;
            if (!_disposed) _closeWatch.Start();
        }

        public async Task<LoginCaptcha> GetCaptchaAsync(CancellationToken cancellationToken)
        {
            await WaitForPageAsync(cancellationToken).ConfigureAwait(true);
            await RunJobAsync(PortalLoginScripts.CaptchaRequest(_traffic.CaptchaPath, PortalLoginScripts.NewRequestId()), cancellationToken).ConfigureAwait(true);
            try
            {
                if (await EvaluateBoolAsync(PortalLoginScripts.Failed).ConfigureAwait(true) || await EvaluateIntAsync(PortalLoginScripts.Status).ConfigureAwait(true) != 200)
                {
                    throw new InvalidOperationException("Cổng Thuế không cấp CAPTCHA.");
                }

                var key = await EvaluateStringAsync(PortalLoginScripts.CaptchaKey).ConfigureAwait(true);
                var svg = await EvaluateStringAsync(PortalLoginScripts.CaptchaSvg).ConfigureAwait(true);
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(svg)) throw new InvalidOperationException("Phản hồi CAPTCHA thiếu key hoặc ảnh.");
                return new LoginCaptcha(key, svg);
            }
            finally
            {
                ClearJob();
            }
        }

        public async Task<LoginSubmitOutcome> SubmitCredentialsAsync(string username, string password, LoginCaptcha captcha, string captchaText, CancellationToken cancellationToken)
        {
            if (captcha == null) throw new ArgumentNullException("captcha");
            await WaitForPageAsync(cancellationToken).ConfigureAwait(true);
            _response.Clear();
            await RunJobAsync(
                PortalLoginScripts.AuthenticateRequest(_traffic.AuthenticatePath, PortalLoginScripts.NewRequestId(), username, password, captchaText, captcha.Key),
                cancellationToken).ConfigureAwait(true);
            try
            {
                if (await EvaluateBoolAsync(PortalLoginScripts.Failed).ConfigureAwait(true)) return LoginSubmitOutcome.Unavailable;
                var status = await EvaluateIntAsync(PortalLoginScripts.Status).ConfigureAwait(true);
                if (status == 200) return LoginSubmitOutcome.Accepted;
                if (status == 400 || status == 401 || status == 403 || status == 422) return LoginSubmitOutcome.Rejected;
                return LoginSubmitOutcome.Unavailable;
            }
            finally
            {
                ClearJob();
            }
        }

        private async Task WaitForPageAsync(CancellationToken cancellationToken)
        {
            if (_browser == null) throw new InvalidOperationException("Trình duyệt chưa sẵn sàng.");
            var loaded = _pageLoaded.Task;
            var finished = await Task.WhenAny(loaded, Task.Delay(PageTimeout, cancellationToken)).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (finished != loaded) throw new TimeoutException("Trang cổng Thuế tải quá lâu.");
            await loaded.ConfigureAwait(true);
        }

        /// <summary>Chạy một đoạn script bất đồng bộ trong trang rồi hỏi đến khi nó báo xong (hoặc quá hạn).</summary>
        private async Task RunJobAsync(string script, CancellationToken cancellationToken)
        {
            var browser = _browser;
            if (browser == null || !browser.CanExecuteJavascriptInMainFrame) throw new InvalidOperationException("Trang cổng Thuế chưa sẵn sàng chạy lệnh.");
            browser.ExecuteScriptAsync(script);
            var deadline = DateTime.UtcNow + PageTimeout;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await EvaluateBoolAsync(PortalLoginScripts.IsDone).ConfigureAwait(true)) return;
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Cổng Thuế không phản hồi.");
                await Task.Delay(120, cancellationToken).ConfigureAwait(true);
            }
        }

        private void ClearJob()
        {
            var browser = _browser;
            if (browser != null && browser.CanExecuteJavascriptInMainFrame) browser.ExecuteScriptAsync(PortalLoginScripts.Clear);
        }

        private async Task<JavascriptResponse> EvaluateAsync(string expression)
        {
            var browser = _browser;
            if (browser == null) throw new InvalidOperationException("Trình duyệt đã đóng.");
            var response = await browser.EvaluateScriptAsync(expression).ConfigureAwait(true);
            if (!response.Success) throw new InvalidOperationException("Không đọc được kết quả từ trang cổng Thuế.");
            return response;
        }

        private async Task<bool> EvaluateBoolAsync(string expression)
        {
            var response = await EvaluateAsync(expression).ConfigureAwait(true);
            return response.Result is bool && (bool)response.Result;
        }

        private async Task<int> EvaluateIntAsync(string expression)
        {
            var response = await EvaluateAsync(expression).ConfigureAwait(true);
            return response.Result == null ? 0 : Convert.ToInt32(response.Result, System.Globalization.CultureInfo.InvariantCulture);
        }

        private async Task<string> EvaluateStringAsync(string expression)
        {
            var response = await EvaluateAsync(expression).ConfigureAwait(true);
            return response.Result as string;
        }

        public async Task<PortalSessionMaterial> CaptureSessionAsync(CancellationToken cancellationToken)
        {
            if (_browser == null || _context == null || !_response.HasContent) return new PortalSessionMaterial(null, null, null);

            var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
            var manager = _context.GetCookieManager(null);
            if (manager != null)
            {
                foreach (var cookie in await manager.VisitUrlCookiesAsync(_traffic.Origin, true).ConfigureAwait(true))
                {
                    cookies[cookie.Name] = cookie.Value;
                }
            }

            var agent = string.Empty;
            var script = await _browser.EvaluateScriptAsync("navigator.userAgent").ConfigureAwait(true);
            if (script.Success && script.Result != null) agent = Convert.ToString(script.Result);

            PortalAuthCapture capture;
            if (!_response.TryBuild(cookies, agent, out capture)) return new PortalSessionMaterial(null, null, null);
            return capture.ToMaterial();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _closeWatch.Stop();
            _closeWatch.Dispose();
            _response.Clear();
            if (_browser != null)
            {
                _browser.Dispose();
                _browser = null;
            }

            if (_context != null)
            {
                _context.Dispose();
                _context = null;
            }
        }

        private void CreateBrowser()
        {
            if (_browser != null) return;

            // Bối cảnh riêng chỉ trong bộ nhớ cho mỗi lần đăng nhập (CachePath rỗng).
            _context = new RequestContext(new RequestContextSettings());
            _browser = new ChromiumWebBrowser("about:blank")
            {
                RequestContext = _context,
                RequestHandler = new LoginRequestHandler(_traffic, _response, OnAuthenticateResponse),
                LifeSpanHandler = new LoginLifeSpanHandler(),
                MenuHandler = new NoContextMenuHandler(),
                Dock = DockStyle.Fill
            };
            _browser.AddressChanged += (sender, e) => Raise(NavigationChanged, new BrowserNavigationEventArgs(e.Address));
            _browser.FrameLoadEnd += (sender, e) =>
            {
                // Trang cổng xong thì mở sẵn form đăng nhập (đóng thông báo, bấm "Đăng nhập", che trang chủ); chỉ ở khung chính của trang thuộc cổng.
                if (e.Frame.IsMain && !e.Url.StartsWith("about:", StringComparison.Ordinal) && _traffic.IsNavigationAllowed(e.Url))
                {
                    _pageLoaded.TrySetResult(true);
                    // Form riêng (mặc định) không cần mở hộp thoại của trang; chỉ phương án dự phòng thủ công mới dùng đoạn hỗ trợ này.
                    if (_manual) e.Frame.ExecuteJavaScriptAsync(PortalLoginPageScript.Source);
                }
            };
            _browser.LoadError += (sender, e) =>
            {
                if (e.ErrorCode != CefErrorCode.Aborted && e.FailedUrl != null && _traffic.IsNavigationAllowed(e.FailedUrl) && !e.FailedUrl.StartsWith("about:", StringComparison.Ordinal))
                {
                    _pageLoaded.TrySetException(new InvalidOperationException("Không tải được trang cổng Thuế."));
                    Raise(BrowserFailed, new BrowserFailureEventArgs("Không tải được trang cổng Thuế (" + e.ErrorText + "). Kiểm tra kết nối mạng."));
                }
            };
        }

        /// <summary>Hỏi trang xem người dùng đã đóng hộp thoại đăng nhập chưa (cờ do <see cref="PortalLoginPageScript"/> đặt).</summary>
        private async void OnCloseWatchTick(object sender, EventArgs e)
        {
            var browser = _browser;
            if (_watching || _disposed || browser == null || !browser.CanExecuteJavascriptInMainFrame) return;
            _watching = true;
            try
            {
                var flag = await browser.EvaluateScriptAsync("window.__hddtLoginClosed === true").ConfigureAwait(true);
                if (_disposed || !flag.Success || !(flag.Result is bool) || !(bool)flag.Result) return;

                browser.ExecuteScriptAsync("window.__hddtLoginClosed = false");
                Raise(LoginDialogClosed, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Trang đang chuyển hướng hoặc trình duyệt đang đóng: lần sau hỏi lại.
            }
            finally
            {
                _watching = false;
            }
        }

        private void OnAuthenticateResponse()
        {
            // Phản hồi đăng nhập đã đủ; việc đọc token và xác minh MST do presenter làm qua CaptureSessionAsync.
            var view = _browser;
            if (view == null) return;
            view.BeginInvoke((Action)(() => Raise(LoginCandidateDetected, new LoginCandidateEventArgs())));
        }

        private void Raise<T>(EventHandler<T> handler, T args)
            where T : EventArgs
        {
            if (handler != null) handler(this, args);
        }

        private void Raise(EventHandler handler, EventArgs args)
        {
            if (handler != null) handler(this, args);
        }

        // ---- Handler của CefSharp ----

        private sealed class LoginRequestHandler : RequestHandler
        {
            private readonly PortalLoginTraffic _traffic;
            private readonly PortalLoginTraffic.ResponseBuffer _buffer;
            private readonly Action _onResponse;

            public LoginRequestHandler(PortalLoginTraffic traffic, PortalLoginTraffic.ResponseBuffer buffer, Action onResponse)
            {
                _traffic = traffic;
                _buffer = buffer;
                _onResponse = onResponse;
            }

            protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
            {
                // true = hủy: chặn điều hướng ra ngoài domain cổng Thuế.
                return !_traffic.IsNavigationAllowed(request.Url);
            }

            protected override IResourceRequestHandler GetResourceRequestHandler(
                IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
            {
                if (!_traffic.IsNavigationAllowed(request.Url)) return null;
                return _traffic.IsAuthenticateRequest(request.Method, request.Url) ? new AuthenticateResourceHandler(_buffer, _onResponse) : null;
            }
        }

        private sealed class AuthenticateResourceHandler : ResourceRequestHandler
        {
            private readonly PortalLoginTraffic.ResponseBuffer _buffer;
            private readonly Action _onResponse;
            private ResponseCollector _filter;

            public AuthenticateResourceHandler(PortalLoginTraffic.ResponseBuffer buffer, Action onResponse)
            {
                _buffer = buffer;
                _onResponse = onResponse;
            }

            protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                _buffer.Clear();
                _filter = new ResponseCollector(_buffer);
                return _filter;
            }

            protected override void OnResourceLoadComplete(
                IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
            {
                if (status == UrlRequestStatus.Success && response != null && response.StatusCode == 200 && _buffer.HasContent) _onResponse();
            }
        }

        /// <summary>Chuyển byte của phản hồi cho bộ đệm mà không đổi nội dung mà trang nhận được.</summary>
        private sealed class ResponseCollector : IResponseFilter
        {
            private readonly PortalLoginTraffic.ResponseBuffer _buffer;

            public ResponseCollector(PortalLoginTraffic.ResponseBuffer buffer)
            {
                _buffer = buffer;
            }

            public bool InitFilter()
            {
                return true;
            }

            public FilterStatus Filter(System.IO.Stream dataIn, out long dataInRead, System.IO.Stream dataOut, out long dataOutWritten)
            {
                if (dataIn == null)
                {
                    dataInRead = 0;
                    dataOutWritten = 0;
                    return FilterStatus.Done;
                }

                var copy = new byte[(int)Math.Min(dataIn.Length, int.MaxValue)];
                var read = dataIn.Read(copy, 0, copy.Length);
                _buffer.Append(copy, 0, read);
                if (dataOut != null) dataOut.Write(copy, 0, read);
                dataInRead = read;
                dataOutWritten = read;
                return FilterStatus.Done;
            }

            public void Dispose()
            {
            }
        }

        private sealed class LoginLifeSpanHandler : LifeSpanHandler
        {
            protected override bool OnBeforePopup(
                IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition,
                bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                // Không mở cửa sổ mới: tải trong chính cửa sổ này nếu vẫn thuộc cổng, còn lại bỏ.
                newBrowser = null;
                return true;
            }
        }

        private sealed class NoContextMenuHandler : CefSharp.Handler.ContextMenuHandler
        {
            protected override void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
            {
                // Không cho "Mở trong cửa sổ mới", "Xem nguồn", công cụ nhà phát triển...
                model.Clear();
            }
        }
    }
}
