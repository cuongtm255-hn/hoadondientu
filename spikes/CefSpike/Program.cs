// Spike G-01 (ADR 0007): kiểm Chromium của CefSharp 109 trên máy này bằng trang cục bộ (KHÔNG gọi cổng Thuế).
//   CefSpike.exe          : Chromium khởi động, RequestContext riêng trong bộ nhớ, cookie manager, JavaScript.
//   CefSpike.exe assist   : chạy PortalLoginPageScript trên một trang giả dựng lại cấu trúc trang chủ cổng (cửa sổ thông báo + mục "Đăng nhập"
//                           mở hộp thoại có ô #username) và kiểm thông báo đã đóng, form đã mở, ô tên đăng nhập đang có con trỏ.
// Build bằng spikes/CefSpike/build.ps1 rồi chạy file .exe sinh ra cạnh HoaDonDienTu.exe.
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using HoaDonDienTu.WinForms.Login;

internal static class Program
{
    // Mô phỏng tối thiểu: thông báo hiện sẵn; bấm "Đăng nhập" ở menu thì hiện hộp thoại có #username sau 300 ms (như SPA dựng giao diện).
    private const string FakePortalPage =
        "<html><head><title>fake</title></head><body>" +
        "<div class='home-header-menu'><div class='home-header-menu-item'><span>Trang chủ</span></div>" +
        "<div class='home-header-menu-item'><span id='loginItem'>Đăng nhập</span></div></div>" +
        "<div class='ant-modal-mask' style='position:fixed;inset:0;background:rgba(0,0,0,.4)'></div>" +
        "<div class='ant-modal-wrap' id='notice' style='position:fixed;inset:0'><div class='ant-modal'><button class='ant-modal-close' onclick=\"document.getElementById('notice').style.display='none'\">X</button>Thông báo</div></div>" +
        "<div id='host'></div>" +
        "<script>document.getElementById('loginItem').addEventListener('click',function(){setTimeout(function(){" +
        "document.getElementById('host').innerHTML=\"<div class='ant-modal-wrap'><form><input id='username'><input id='password' type='password'></form></div>\";},300);});</script>" +
        "</body></html>";

    [STAThread]
    private static int Main(string[] args)
    {
        var settings = new CefSettings { LogSeverity = LogSeverity.Disable };
        if (!Cef.Initialize(settings, true, (IBrowserProcessHandler)null))
        {
            Console.WriteLine("FAIL: Cef.Initialize returned false");
            return 1;
        }

        var assist = args.Length > 0 && args[0] == "assist";
        var result = 2;
        var form = new Form { Width = 800, Height = 600, Text = "CefSpike (trang cục bộ, không gọi cổng Thuế)" };
        var context = new RequestContext(new RequestContextSettings());
        var address = assist
            ? "data:text/html;charset=utf-8," + Uri.EscapeDataString(FakePortalPage)
            : "data:text/html,<title>spike-ok</title><p id='x'>xin chào</p>";
        var browser = new ChromiumWebBrowser(address) { RequestContext = context, Dock = DockStyle.Fill };
        form.Controls.Add(browser);
        var timeout = new System.Windows.Forms.Timer { Interval = 30000 };
        timeout.Tick += delegate { Console.WriteLine("FAIL: timeout"); result = 3; form.Close(); };
        timeout.Start();

        browser.LoadingStateChanged += async (s, e) =>
        {
            if (e.IsLoading) return;
            try
            {
                if (assist)
                {
                    browser.GetMainFrame().ExecuteJavaScriptAsync(PortalLoginPageScript.Source);
                    await Task.Delay(3500);
                    var state = await browser.EvaluateScriptAsync(
                        "(function(){var n=document.getElementById('notice');var u=document.getElementById('username');" +
                        "return JSON.stringify({noticeHidden:getComputedStyle(n).display==='none',formOpen:!!u,focused:!!u&&document.activeElement===u," +
                        "maskWhite:getComputedStyle(document.querySelector('.ant-modal-mask')).backgroundColor});})()");
                    Console.WriteLine("state=" + state.Result);
                    var text = Convert.ToString(state.Result);
                    result = text.Contains("\"noticeHidden\":true") && text.Contains("\"formOpen\":true") && text.Contains("\"focused\":true") && text.Contains("rgb(255, 255, 255)") ? 0 : 4;
                }
                else
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
