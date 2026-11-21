using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.PublicService
{
    /// <summary>
    /// Đăng nhập cổng Dịch vụ công thật (Release): chạy trong trang <c>/tthc/login</c> của WebView ngầm, theo đúng luồng của trang (khảo sát 21/11/2026):
    /// ảnh CAPTCHA <c>GET /tthc/login/getCaptcha</c>, đăng nhập <c>POST /tthc/loginLDAP</c> dạng form urlencoded với <c>matKhau</c> = base64 của mật khẩu UTF-8,
    /// <c>doiTuong</c> = <c>DN</c>; phản hồi JSON <c>status</c> "200"/"201" là thành công, khác là lỗi kèm <c>desc</c>.
    /// Mật khẩu và CAPTCHA đi vào script dưới dạng literal JSON, không ghi nhật ký, không lưu. Người dùng tự gõ mọi thứ (BR-DVC-03/04).
    /// </summary>
    public sealed class PortalDvcLoginBrowser : IDvcLoginBrowser
    {
        public const string LoginPath = "/tthc/login";
        public const string HomePath = "/tthc/home";
        public const string CaptchaPath = "/tthc/login/getCaptcha";
        public const string SubmitPath = "/tthc/loginLDAP";
        public const string LogoutPath = "/tthc/logout";
        public const string EnterpriseCode = "DN";

        private const int MaxMessageLength = 200;

        private readonly IDvcPageRunner _runner;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public PortalDvcLoginBrowser(IDvcPageRunner runner)
        {
            if (runner == null) throw new ArgumentNullException("runner");
            _runner = runner;
        }

        public async Task<byte[]> GetCaptchaAsync(CancellationToken cancellationToken)
        {
            JObject result = await RunAsync(LoginPath, BuildCaptchaScript(), cancellationToken).ConfigureAwait(false);
            string contentType = result.Value<string>("ct") ?? string.Empty;
            string b64 = result.Value<string>("b64");
            if (result.Value<int?>("http") != 200 || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(b64))
            {
                throw new InvalidOperationException("Cổng không cấp ảnh CAPTCHA.");
            }
            return Convert.FromBase64String(b64);
        }

        public async Task<DvcLoginResult> SubmitAsync(DvcCredentials credentials, string captcha, CancellationToken cancellationToken)
        {
            if (credentials == null) throw new ArgumentNullException("credentials");
            JObject result = await RunAsync(LoginPath, BuildSubmitScript(credentials.UserName, credentials.Password, captcha), cancellationToken).ConfigureAwait(false);

            int http = result.Value<int?>("http") ?? -1;
            if (http != 200) throw new DvcPortalHttpException(http);

            string status = StatusText(result["status"]);
            if (status == "200" || status == "201")
            {
                return DvcLoginResult.Ok(Sanitize(result.Value<string>("org")));
            }

            string desc = Sanitize(result.Value<string>("desc"));
            return DvcLoginResult.Fail(Classify(desc), desc);
        }

        public async Task LogoutAsync(CancellationToken cancellationToken)
        {
            // Chạy trên trang chủ: token CSRF (meta) của phiên đã đăng nhập nằm ở đó.
            await RunAsync(HomePath, BuildLogoutScript(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Phân loại lỗi theo nội dung thông báo của cổng (cổng trả status "999" cho sai CAPTCHA; các lỗi khác chưa quan sát).</summary>
        public static DvcLoginFailure Classify(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return DvcLoginFailure.Unknown;
            string text = desc.ToLowerInvariant();
            if (text.Contains("captcha")) return DvcLoginFailure.WrongCaptcha;
            if (text.Contains("tab khác") || text.Contains("đang được sử dụng")) return DvcLoginFailure.AnotherTab;
            if (text.Contains("khóa") || text.Contains("khoá")) return DvcLoginFailure.Locked;
            if (text.Contains("mật khẩu") || text.Contains("tên đăng nhập")) return DvcLoginFailure.WrongPassword;
            return DvcLoginFailure.Unknown;
        }

        private async Task<JObject> RunAsync(string pagePath, string script, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string raw = await _runner.RunOnPageAsync(pagePath, script, cancellationToken).ConfigureAwait(false);
                try
                {
                    return JObject.Parse(raw ?? string.Empty);
                }
                catch (Newtonsoft.Json.JsonReaderException)
                {
                    throw new InvalidOperationException("Kết quả script không phải JSON.");
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private static string StatusText(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        /// <summary>Cắt ngắn và bỏ ký tự điều khiển; thông báo của cổng không chứa dữ liệu người dùng nhưng vẫn giữ giới hạn an toàn.</summary>
        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(Math.Min(value.Length, MaxMessageLength));
            foreach (char c in value)
            {
                if (sb.Length >= MaxMessageLength) break;
                if (char.IsControl(c)) continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        // ---- Script dựng sẵn: mọi giá trị đi vào qua Literal() (JSON), không bao giờ ghép chuỗi thô ----

        internal static string BuildCaptchaScript()
        {
            return "(async function(){" +
                "var r=await fetch(" + PortalTthcSource.Literal(CaptchaPath) + "+'?'+Date.now(),{credentials:'same-origin',cache:'no-store'});" +
                "var a=new Uint8Array(await r.arrayBuffer());var s='';" +
                "for(var i=0;i<a.length;i+=8192){s+=String.fromCharCode.apply(null,a.subarray(i,i+8192));}" +
                "return JSON.stringify({http:r.status,ct:r.headers.get('content-type')||'',b64:btoa(s)});})()";
        }

        internal static string BuildSubmitScript(string userName, string password, string captcha)
        {
            return "(async function(){" +
                "var b=new URLSearchParams();" +
                "b.set('tenDN'," + PortalTthcSource.Literal(userName) + ");" +
                // Đúng như trang: btoa(unescape(encodeURIComponent(matKhau))) = base64 của mật khẩu UTF-8.
                "b.set('matKhau',btoa(unescape(encodeURIComponent(" + PortalTthcSource.Literal(password) + "))));" +
                "b.set('doiTuong'," + PortalTthcSource.Literal(EnterpriseCode) + ");" +
                "b.set('captcha'," + PortalTthcSource.Literal(captcha) + ");" +
                "var m=function(n){var e=document.querySelector('meta[name=\"'+n+'\"]');return e?e.getAttribute('content'):null;};" +
                "var h={'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8','X-Requested-With':'XMLHttpRequest','Accept':'application/json, text/javascript, */*; q=0.01'};" +
                "var hn=m('csrf-header'),tk=m('csrf-token');if(hn&&tk)h[hn]=tk;" +
                "var r=await fetch(" + PortalTthcSource.Literal(SubmitPath) + ",{method:'POST',credentials:'same-origin',headers:h,body:b.toString()});" +
                "var t=await r.text();var j=null;try{j=JSON.parse(t);}catch(e){}" +
                "var org='';" +
                "if(r.status===200&&j&&(String(j.status)==='200'||String(j.status)==='201')){" +
                "try{var h=await(await fetch(" + PortalTthcSource.Literal(HomePath) + ",{credentials:'same-origin'})).text();" +
                "var d=new DOMParser().parseFromString(h,'text/html');var n=d.querySelector('span.ten_dn');org=n?n.textContent.trim():'';}catch(e){}}" +
                "return JSON.stringify({http:r.status,status:j?j.status:null,desc:j?j.desc:null,org:org});})()";
        }

        internal static string BuildLogoutScript()
        {
            return "(async function(){" +
                "var m=function(n){var e=document.querySelector('meta[name=\"'+n+'\"]');return e?e.getAttribute('content'):null;};" +
                "var h={'Content-Type':'application/json'};var hn=m('csrf-header'),tk=m('csrf-token');if(hn&&tk)h[hn]=tk;" +
                "var r=await fetch(" + PortalTthcSource.Literal(LogoutPath) + ",{method:'POST',headers:h,body:'{}',credentials:'same-origin'});" +
                "return JSON.stringify({http:r.status});})()";
        }
    }
}
