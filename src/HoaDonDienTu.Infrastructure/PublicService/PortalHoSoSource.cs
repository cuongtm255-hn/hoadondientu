using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.PublicService
{
    /// <summary>
    /// Nguồn hồ sơ đã nộp thật (Release), tab Dịch vụ công: chạy trong trang <c>/tthc/tchs</c> của WebView ngầm đã đăng nhập, theo đúng cách trang gọi
    /// (khảo sát 21/11/2026): <c>GET /tthc/ho-so/search?...&amp;page=N&amp;size=10</c> với toàn bộ giá trị của form <c>form-search-advanced</c>. Sai CAPTCHA là
    /// HTTP 400. Tab thuế điện tử chưa khảo sát nên chưa hỗ trợ (<see cref="HoSoFailure.NotSupported"/>).
    /// Mọi giá trị người dùng (CAPTCHA, MST ủy quyền, mã hồ sơ...) đi vào script dưới dạng literal JSON; không ghi nhật ký. Mỗi lần một yêu cầu.
    /// </summary>
    public sealed class PortalHoSoSource : IHoSoSource
    {
        public const string PagePath = "/tthc/tchs";
        public const string SearchPath = "/tthc/ho-so/search";
        public const string DvcFormId = "form-search-advanced";
        public const int PageSize = 10;

        private readonly IDvcPageRunner _runner;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public PortalHoSoSource(IDvcPageRunner runner)
        {
            if (runner == null) throw new ArgumentNullException("runner");
            _runner = runner;
        }

        public async Task<byte[]> GetCaptchaAsync(HoSoSource source, CancellationToken cancellationToken)
        {
            RequireSupported(source);
            JObject result = await RunAsync(BuildCaptchaScript(), cancellationToken).ConfigureAwait(false);
            RequireLoggedIn(result);
            string png = result.Value<string>("png");
            const string prefix = "data:image/png;base64,";
            if (string.IsNullOrEmpty(png) || !png.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Không lấy được ảnh CAPTCHA.");
            return Convert.FromBase64String(png.Substring(prefix.Length));
        }

        public async Task<HoSoPage> SearchAsync(HoSoQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException("query");
            RequireSupported(query.Source);
            if (query.PageNumber < 1) throw new ArgumentOutOfRangeException("query", "Số trang phải từ 1.");

            JObject result = await RunAsync(BuildSearchScript(query), cancellationToken).ConfigureAwait(false);
            RequireLoggedIn(result);

            int http = result.Value<int?>("http") ?? -1;
            if (http == 400) throw new HoSoServiceException(HoSoFailure.WrongCaptcha);
            if (http == 401 || http == 403) throw new DvcSessionExpiredException();
            if (http != 200) throw new DvcPortalHttpException(http);
            // Cổng có thể trả trang đăng nhập (HTTP 200) khi hết phiên.
            if (result.Value<bool?>("loginPage") == true) throw new DvcSessionExpiredException();

            string body = result.Value<string>("body");
            try
            {
                return HoSoHtmlParser.ParseList(body);
            }
            catch (HoSoParseException ex)
            {
                // Kèm cấu trúc HTML (chỉ hình dạng, không có dữ liệu) để biết cổng đổi gì mà sửa bộ phân tích.
                string diagnostic = (ex.Diagnostic ?? "(không rõ lý do)") + " | cấu trúc: " + HoSoHtmlParser.DescribeStructure(body);
                throw new HoSoParseException(ex.Message + " | cấu trúc: " + HoSoHtmlParser.DescribeStructure(body), diagnostic);
            }
        }

        private static void RequireSupported(HoSoSource source)
        {
            if (source != HoSoSource.Dvc) throw new NotSupportedException("Tab thuế điện tử chưa hỗ trợ ở bản này.");
        }

        /// <summary>Thiếu form tra cứu nghĩa là trang đã chuyển về đăng nhập (hết phiên hoặc chưa đăng nhập).</summary>
        private static void RequireLoggedIn(JObject result)
        {
            if (result.Value<bool?>("noForm") == true) throw new DvcSessionExpiredException();
        }

        private async Task<JObject> RunAsync(string script, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string raw = await _runner.RunOnPageAsync(PagePath, script, cancellationToken).ConfigureAwait(false);
                try
                {
                    return JObject.Parse(raw ?? string.Empty);
                }
                catch (JsonReaderException)
                {
                    throw new HoSoParseException("Kết quả script không phải JSON.");
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        // ---- Script dựng sẵn: mọi giá trị đi vào qua Literal() (JSON) ----

        /// <summary>
        /// Lấy CAPTCHA mới ngay trên ảnh của form (đặt lại <c>src</c> với tem thời gian, chờ tải xong, vẽ ra canvas rồi trả PNG), để ảnh app hiện
        /// đúng là mã đang gắn với phiên của form.
        /// </summary>
        internal static string BuildCaptchaScript()
        {
            return "(async function(){" +
                "var f=document.getElementById(" + PortalTthcSource.Literal(DvcFormId) + ");if(!f)return JSON.stringify({noForm:true});" +
                "var imgs=f.querySelectorAll('img'),img=null;" +
                "for(var i=0;i<imgs.length;i++){if(/captcha/i.test((imgs[i].getAttribute('src')||'')+(imgs[i].id||'')+(imgs[i].className||''))){img=imgs[i];break;}}" +
                "if(!img)return JSON.stringify({png:''});" +
                "var base=(img.getAttribute('src')||'').split('?')[0];" +
                "await new Promise(function(res,rej){img.onload=res;img.onerror=rej;img.src=base+'?'+Date.now();});" +
                "var c=document.createElement('canvas');c.width=img.naturalWidth||140;c.height=img.naturalHeight||44;" +
                "c.getContext('2d').drawImage(img,0,0);" +
                "return JSON.stringify({png:c.toDataURL('image/png')});})()";
        }

        internal static string BuildSearchScript(HoSoQuery q)
        {
            string fields = "{" +
                "\"maNghiepVu\":" + PortalTthcSource.Literal(q.ProcedureType) + "," +
                "\"maTTHC\":" + PortalTthcSource.Literal(CodeOrEmpty(q.ProcedureName)) + "," +
                "\"maToKhai\":" + PortalTthcSource.Literal(q.Declaration) + "," +
                "\"maHoSo\":" + PortalTthcSource.Literal(q.FileCode) + "," +
                "\"tuNgay\":" + PortalTthcSource.Literal(q.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)) + "," +
                "\"denNgay\":" + PortalTthcSource.Literal(q.ToDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)) + "," +
                "\"mstUyQuyen_tdt1\":" + PortalTthcSource.Literal(q.AuthorizedTaxCode) + "," +
                "\"captcha\":" + PortalTthcSource.Literal(q.CaptchaText) + "," +
                "\"page\":" + PortalTthcSource.Literal(q.PageNumber.ToString(CultureInfo.InvariantCulture)) + "," +
                "\"size\":" + PortalTthcSource.Literal(PageSize.ToString(CultureInfo.InvariantCulture)) +
                "}";

            return "(async function(){" +
                "var f=document.getElementById(" + PortalTthcSource.Literal(DvcFormId) + ");if(!f)return JSON.stringify({noForm:true});" +
                // Đúng như onChangePage của trang: lấy FormData của form rồi đặt page, size; ở đây ghi đè thêm các trường của người dùng.
                "var p=new URLSearchParams(new FormData(f));var v=" + fields + ";" +
                "Object.keys(v).forEach(function(k){p.set(k,v[k]);});" +
                "var r=await fetch(" + PortalTthcSource.Literal(SearchPath) + "+'?'+p.toString(),{method:'GET',credentials:'same-origin'," +
                "headers:{'HX-Request':'true','HX-Target':'table-container','accept':'text/html-partial'}});" +
                "var t=await r.text();" +
                "return JSON.stringify({http:r.status,loginPage:/modalLdap|loginLDAP/.test(t),body:r.status===200?t:''});})()";
        }

        /// <summary>Cổng nhận "Tên TTHC" dạng mã trong ô chọn; chỉ gửi khi người dùng nhập đúng dạng mã (chữ số và dấu chấm), còn lại bỏ qua.</summary>
        private static string CodeOrEmpty(string value)
        {
            return TthcService.IsValidCode(value) ? value : string.Empty;
        }
    }
}
