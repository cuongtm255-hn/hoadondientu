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
    /// CHƯA được thử với Chromium thật trong phiên làm việc này (Debug không nạp Chromium): cần thử tay ở bản Release.
    /// </summary>
    public sealed class CefDvcPageRunner : IDvcPageRunner, IDisposable
    {
        private const string Origin = "https://dichvucong.gdt.gov.vn";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly SynchronizationContext _ui;
        private Form _host;
        private ChromiumWebBrowser _browser;
        private TaskCompletionSource<bool> _loaded;
        private string _currentPath;
        private bool _disposed;

        /// <summary>Phải tạo trên luồng giao diện.</summary>
        public CefDvcPageRunner()
        {
            _ui = SynchronizationContext.Current;
            if (_ui == null) throw new InvalidOperationException("CefDvcPageRunner phải được tạo trên luồng giao diện.");
        }

        public Task<string> RunOnPageAsync(string pagePath, string script, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<string>();
            _ui.Post(async state =>
            {
                try
                {
                    tcs.TrySetResult(await RunOnUiAsync(pagePath, script, cancellationToken));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, null);
            return tcs.Task;
        }

        private async Task<string> RunOnUiAsync(string pagePath, string script, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException("CefDvcPageRunner");
            EnsureBrowser(pagePath);
            await EnsurePageAsync(pagePath, cancellationToken);

            var run = _browser.EvaluateScriptAsPromiseAsync("return await " + script + ";");
            var finished = await Task.WhenAny(run, Task.Delay(Timeout, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            if (finished != run) throw new TimeoutException("Cổng Dịch vụ công không phản hồi.");

            JavascriptResponse response = await run;
            if (!response.Success) throw new InvalidOperationException("Trang cổng không chạy được lệnh.");
            return response.Result as string;
        }

        private void EnsureBrowser(string pagePath)
        {
            if (_browser != null) return;
            CefRuntimeHost.EnsureInitialized();

            // Hồ sơ riêng chỉ trong bộ nhớ: cookie và storage của cổng DVC không dùng chung với cổng Hóa đơn.
            _loaded = new TaskCompletionSource<bool>();
            _browser = new ChromiumWebBrowser(Origin + pagePath, new RequestContext());
            _browser.LoadingStateChanged += OnLoadingStateChanged;

            // Chứa WebView trong một cửa sổ ẩn ngoài màn hình để có handle; người dùng không thấy.
            _host = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Size = new Size(1024, 768)
            };
            _browser.Dock = DockStyle.Fill;
            _host.Controls.Add(_browser);
            _host.Show();
            _currentPath = null;
        }

        private void OnLoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            if (e.IsLoading) return;
            _currentPath = SafePath(_browser.Address);
            var loaded = _loaded;
            if (loaded != null) loaded.TrySetResult(true);
        }

        private async Task EnsurePageAsync(string pagePath, CancellationToken cancellationToken)
        {
            // Chờ lần nạp đầu (hoặc nạp lại) hoàn tất; chỉ nạp lại khi đang ở trang khác.
            if (_currentPath != null && _currentPath == pagePath && !_browser.IsLoading) return;
            if (_currentPath != null && _currentPath != pagePath)
            {
                _loaded = new TaskCompletionSource<bool>();
                _browser.LoadUrl(Origin + pagePath);
            }

            var loaded = _loaded.Task;
            var finished = await Task.WhenAny(loaded, Task.Delay(Timeout, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            if (finished != loaded) throw new TimeoutException("Trang cổng Dịch vụ công tải quá lâu.");
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
            if (_browser != null)
            {
                _browser.LoadingStateChanged -= OnLoadingStateChanged;
                _browser.Dispose();
            }
            if (_host != null) _host.Dispose();
        }
    }
}
