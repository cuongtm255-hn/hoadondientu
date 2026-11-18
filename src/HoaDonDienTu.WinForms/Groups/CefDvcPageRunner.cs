using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.WinForms.Login;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Chạy script trong trang cổng Dịch vụ công của MỘT WebView ngầm (CefSharp 109, ADR DVC-0001, BR-DVC-06): chỉ một trang DVC, nạp lại khi cần
    /// đổi trang. Hồ sơ trình duyệt riêng (RequestContext trong bộ nhớ, không ghi cookie xuống đĩa). Chỉ dùng ở bản Release (Debug dùng nguồn giả).
    /// WebView sống trong một cửa sổ ẩn trên một luồng giao diện riêng có vòng lặp tin nhắn riêng, tách khỏi các hộp thoại của ứng dụng.
    /// </summary>
    public sealed class CefDvcPageRunner : IDvcPageRunner, IDisposable
    {
        private const string Origin = "https://dichvucong.gdt.gov.vn";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly Action<string> _log;
        private readonly object _gate = new object();
        private Thread _thread;
        private Task _started;
        private Form _host;
        private ChromiumWebBrowser _browser;
        private IRequestContext _context;
        private TaskCompletionSource<bool> _loaded = new TaskCompletionSource<bool>();
        private volatile string _currentPath;
        private volatile bool _disposed;

        /// <summary><paramref name="log"/> chỉ nhận thông điệp kỹ thuật, không có dữ liệu người dùng.</summary>
        public CefDvcPageRunner(Action<string> log = null)
        {
            _log = log ?? (m => { });
        }

        public async Task<string> RunOnPageAsync(string pagePath, string script, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException("CefDvcPageRunner");
            _log("DVC WebView: bắt đầu chạy lệnh trên " + pagePath);
            await EnsureBrowserAsync(pagePath, cancellationToken).ConfigureAwait(false);
            await EnsurePageAsync(pagePath, cancellationToken).ConfigureAwait(false);
            _log("DVC WebView: trang sẵn sàng, chạy lệnh");

            var run = _browser.EvaluateScriptAsPromiseAsync("return await " + script + ";");
            var finished = await Task.WhenAny(run, Task.Delay(Timeout, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (finished != run)
            {
                _log("DVC WebView: quá hạn chờ lệnh");
                throw new TimeoutException("Cổng Dịch vụ công không phản hồi.");
            }

            JavascriptResponse response = await run.ConfigureAwait(false);
            _log("DVC WebView: lệnh xong, thành công=" + response.Success + (response.Success ? string.Empty : ", lỗi=" + response.Message));
            if (!response.Success) throw new InvalidOperationException("Trang cổng không chạy được lệnh.");
            return response.Result as string;
        }

        /// <summary>Dựng cửa sổ ẩn + WebView một lần, trên luồng riêng (địa chỉ truyền ngay trong hàm tạo).</summary>
        private async Task EnsureBrowserAsync(string pagePath, CancellationToken cancellationToken)
        {
            Task ready;
            lock (_gate)
            {
                if (_thread == null)
                {
                    var started = new TaskCompletionSource<bool>();
                    _started = started.Task;
                    _thread = new Thread(() => HostThread(pagePath, started)) { IsBackground = true, Name = "DvcWebViewHost" };
                    _thread.SetApartmentState(ApartmentState.STA);
                    _thread.Start();
                }
                ready = _started;
            }

            var finished = await Task.WhenAny(ready, Task.Delay(Timeout, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (finished != ready)
            {
                _log("DVC WebView: quá hạn chờ dựng trình duyệt");
                throw new TimeoutException("Không dựng được trình duyệt ẩn.");
            }
            await ready.ConfigureAwait(false);
        }

        private void HostThread(string pagePath, TaskCompletionSource<bool> started)
        {
            try
            {
                _log("DVC WebView: khởi tạo Chromium");
                CefRuntimeHost.EnsureInitialized();
                _log("DVC WebView: Chromium đã khởi tạo");

                // Hồ sơ riêng chỉ trong bộ nhớ: cookie và storage của cổng DVC không dùng chung với cổng Hóa đơn.
                // Địa chỉ PHẢI truyền ngay trong hàm tạo: gọi LoadUrl ngay sau Show() (lúc trình duyệt chưa tạo xong) làm treo rồi sập libcef.dll.
                _context = new RequestContext(new RequestContextSettings());
                _browser = new ChromiumWebBrowser(Origin + pagePath) { RequestContext = _context, Dock = DockStyle.Fill };
                _browser.LoadingStateChanged += OnLoadingStateChanged;
                _browser.LoadError += (s, e) => _log("DVC WebView: lỗi nạp trang, mã=" + e.ErrorCode + ", " + e.ErrorText);
                _browser.IsBrowserInitializedChanged += (s, e) => _log("DVC WebView: trình duyệt khởi tạo xong=" + _browser.IsBrowserInitialized);

                // Cửa sổ chứa nằm ngoài màn hình, không có nút trên thanh tác vụ; người dùng không thấy.
                _host = new Form
                {
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    Size = new Size(1024, 768)
                };
                _host.Controls.Add(_browser);
                _host.Shown += (s, e) => started.TrySetResult(true);
                _log("DVC WebView: hiện cửa sổ chứa");
                System.Windows.Forms.Application.Run(_host);
            }
            catch (Exception ex)
            {
                _log("DVC WebView: lỗi dựng trình duyệt: " + ex.GetType().Name);
                started.TrySetException(ex);
            }
        }

        private void OnLoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            string path = SafePath(e.Browser.MainFrame.Url);
            _log("DVC WebView: trạng thái tải IsLoading=" + e.IsLoading + ", đường dẫn=" + path);
            if (e.IsLoading) return;
            _currentPath = path;
            var loaded = _loaded;
            if (loaded != null) loaded.TrySetResult(true);
        }

        private async Task EnsurePageAsync(string pagePath, CancellationToken cancellationToken)
        {
            // Chờ lần nạp đầu hoàn tất; chỉ nạp lại khi đang ở trang khác (một trang DVC duy nhất trong WebView).
            if (_currentPath != null && _currentPath != pagePath)
            {
                _loaded = new TaskCompletionSource<bool>();
                _browser.LoadUrl(Origin + pagePath);
            }

            var loaded = _loaded.Task;
            var finished = await Task.WhenAny(loaded, Task.Delay(Timeout, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (finished != loaded)
            {
                _log("DVC WebView: quá hạn chờ nạp trang");
                throw new TimeoutException("Trang cổng Dịch vụ công tải quá lâu.");
            }
        }

        private static string SafePath(string address)
        {
            Uri uri;
            return Uri.TryCreate(address, UriKind.Absolute, out uri) ? uri.AbsolutePath : null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var host = _host;
            if (host != null && host.IsHandleCreated)
            {
                try
                {
                    // Đóng trên luồng chủ của cửa sổ; Application.Run của luồng riêng kết thúc theo.
                    host.BeginInvoke((Action)(() =>
                    {
                        if (_browser != null) _browser.Dispose();
                        host.Close();
                    }));
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
    }
}
