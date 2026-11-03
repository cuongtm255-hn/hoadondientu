// Spike G-01 (ADR 0007): Chromium của CefSharp 109 có khởi động được trên máy này không. Chỉ mở một trang data: cục bộ (KHÔNG gọi cổng Thuế),
// kiểm RequestContext riêng chỉ trong bộ nhớ, cookie manager và chạy JavaScript. Build bằng spikes/CefSpike/build.ps1 rồi chạy file .exe sinh ra cạnh HoaDonDienTu.exe.
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var settings = new CefSettings { LogSeverity = LogSeverity.Disable };
        if (!Cef.Initialize(settings, true, (IBrowserProcessHandler)null))
        {
            Console.WriteLine("FAIL: Cef.Initialize returned false");
            return 1;
        }

        var result = 2;
        var form = new Form { Width = 800, Height = 600, Text = "CefSpike (trang cục bộ, không gọi cổng Thuế)" };
        var context = new RequestContext(new RequestContextSettings());
        var browser = new ChromiumWebBrowser("data:text/html,<title>spike-ok</title><p id='x'>xin chào</p>") { RequestContext = context, Dock = DockStyle.Fill };
        form.Controls.Add(browser);
        var timeout = new System.Windows.Forms.Timer { Interval = 30000 };
        timeout.Tick += delegate { Console.WriteLine("FAIL: timeout"); result = 3; form.Close(); };
        timeout.Start();

        browser.LoadingStateChanged += async (s, e) =>
        {
            if (e.IsLoading) return;
            try
            {
                var title = await browser.EvaluateScriptAsync("document.title");
                var agent = await browser.EvaluateScriptAsync("navigator.userAgent");
                var cookies = context.GetCookieManager(null);
                var list = await cookies.VisitUrlCookiesAsync("https://example.invalid", true);
                Console.WriteLine("title=" + title.Result);
                Console.WriteLine("userAgent=" + agent.Result);
                Console.WriteLine("cookies=" + list.Count);
                Console.WriteLine("cefVersion=" + Cef.CefVersion);
                result = Equals(title.Result, "spike-ok") ? 0 : 4;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.GetType().Name + ": " + ex.Message);
                result = 5;
            }

            form.BeginInvoke((Action)form.Close);
        };

        Application.Run(form);
        browser.Dispose();
        context.Dispose();
        Cef.Shutdown();
        Console.WriteLine(result == 0 ? "OK" : "FAIL code " + result);
        return result;
    }
}
