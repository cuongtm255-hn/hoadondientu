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
    /// (khảo sát 21/11/2026). Tab Dịch vụ công: <c>GET /tthc/ho-so/search?...&amp;page=N&amp;size=10</c> với toàn bộ giá trị của form <c>form-search-advanced</c>.
    /// Tab thuế điện tử: <c>POST /tthc/tchs/thuedientu</c> (urlencoded, header CSRF) với giá trị của form <c>form_search_tdt</c>, kết quả vào
    /// <c>#bangKetQuaTraCuu_tdt</c>. Sai CAPTCHA là HTTP 400 ở cả hai.
    /// Mọi giá trị người dùng (CAPTCHA, MST ủy quyền, mã hồ sơ...) đi vào script dưới dạng literal JSON; không ghi nhật ký. Mỗi lần một yêu cầu.
    /// </summary>
    public sealed class PortalHoSoSource : IHoSoSource
    {
        public const string PagePath = "/tthc/tchs";
        public const string SearchPath = "/tthc/ho-so/search";
        public const string DvcFormId = "form-search-advanced";
        public const string ETaxPath = "/tthc/tchs/thuedientu";
        public const string ETaxFormId = "form_search_tdt";
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
            JObject result = await RunAsync(BuildCaptchaScript(FormIdFor(source)), cancellationToken).ConfigureAwait(false);
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
            JObject table = result["table"] as JObject;
            JArray summary = result["sum"] as JArray;
            if (table != null)
            {
                try
                {
                    return HoSoDomTable.ToPage(table, summary, result.Value<string>("pageInput"));
                }
                catch (HoSoParseException ex)
                {
                    string diag = (ex.Diagnostic ?? "(không rõ lý do)") + " | cấu trúc: " + HoSoHtmlParser.DescribeStructure(body);
                    throw new HoSoParseException(ex.Message, diag);
                }
            }

            // Không có bảng: cổng trả mảnh "không có dữ liệu". Tab DVC có dòng tổng với 0 bản ghi; tab thuế điện tử chỉ có khung phân trang rỗng
            // (không dòng tổng). Mảnh ngắn không có bảng và không báo có bản ghi là kết quả rỗng bình thường; mảnh dài thì là cấu trúc lạ.
            bool saysZero = summary != null && summary.Count >= 3 && summary[2].ToString() == "0";
            // Khung rỗng của tab thuế điện tử không có chữ nào nhìn thấy được (chỉ chú thích HTML): trình duyệt báo text rỗng. Có chữ mà không có dòng tổng
            // thì là cấu trúc lạ, đi tiếp sang bộ phân tích dự phòng (báo lỗi nếu không nhận ra).
            string visibleText = result.Value<string>("text");
            bool noSummaryAndNoText = summary == null && visibleText != null && visibleText.Trim().Length == 0;
            if (saysZero || noSummaryAndNoText)
            {
                return new HoSoPage(null, null, 0, 1, 0, "Không có dữ liệu");
            }

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
            if (source != HoSoSource.Dvc && source != HoSoSource.ThueDienTu) throw new NotSupportedException("Nguồn hồ sơ chưa hỗ trợ.");
        }

        private static string FormIdFor(HoSoSource source)
        {
            return source == HoSoSource.ThueDienTu ? ETaxFormId : DvcFormId;
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
        internal static string BuildCaptchaScript(string formId)
        {
            return "(async function(){" +
                "var f=document.getElementById(" + PortalTthcSource.Literal(formId) + ");if(!f)return JSON.stringify({noForm:true});" +
                "var imgs=f.querySelectorAll('img'),img=null;" +
                "for(var i=0;i<imgs.length;i++){if(/capt/i.test((imgs[i].getAttribute('src')||'')+(imgs[i].id||'')+(imgs[i].className||''))){img=imgs[i];break;}}" +
                // Mã ảnh của tab thuế điện tử đặt id là image-capt-tdt (không có chữ captcha): khớp theo capt, không thấy thì lấy ảnh duy nhất của form.
                "if(!img&&imgs.length===1)img=imgs[0];" +
                "if(!img)return JSON.stringify({png:''});" +
                "var base=(img.getAttribute('src')||'').split('?')[0];" +
                // Dùng chính hàm đổi mã của trang (refreshCaptcha) vì mỗi tab đặt địa chỉ ảnh riêng; chỉ khi trang không có hàm đó mới tự đặt lại src.
                "var done=new Promise(function(res,rej){img.onload=res;img.onerror=function(){rej(new Error('captcha-img'));};setTimeout(function(){rej(new Error('captcha-timeout'));},10000);});" +
                "if(typeof window.refreshCaptcha==='function'&&img.id){window.refreshCaptcha(img.id);}else{img.src=base+'?'+Date.now();}" +
                "await done;" +
                "var c=document.createElement('canvas');c.width=img.naturalWidth||140;c.height=img.naturalHeight||44;" +
                "c.getContext('2d').drawImage(img,0,0);" +
                "return JSON.stringify({png:c.toDataURL('image/png')});})()";
        }

        internal static string BuildSearchScript(HoSoQuery q)
        {
            return q.Source == HoSoSource.ThueDienTu ? BuildETaxSearchScript(q) : BuildDvcSearchScript(q);
        }

        private static string Fields(params string[] keyValues)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i + 1 < keyValues.Length; i += 2) parts.Add("\"" + keyValues[i] + "\":" + PortalTthcSource.Literal(keyValues[i + 1]));
            return "{" + string.Join(",", parts) + "}";
        }

        private static string Date(DateTime d)
        {
            return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static string Page(HoSoQuery q)
        {
            return q.PageNumber.ToString(CultureInfo.InvariantCulture);
        }

        private static string Size()
        {
            return PageSize.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Tab Dịch vụ công: đúng như <c>onChangePage</c> của trang (GET với toàn bộ giá trị form).</summary>
        private static string BuildDvcSearchScript(HoSoQuery q)
        {
            string fields = Fields(
                "maNghiepVu", q.ProcedureType,
                "maTTHC", CodeOrEmpty(q.ProcedureName),
                "maToKhai", q.Declaration,
                "maHoSo", q.FileCode,
                "tuNgay", Date(q.FromDate),
                "denNgay", Date(q.ToDate),
                "mstUyQuyen_tdt1", q.AuthorizedTaxCode,
                "captcha", q.CaptchaText,
                "page", Page(q),
                "size", Size());

            return "(async function(){" +
                "var f=document.getElementById(" + PortalTthcSource.Literal(DvcFormId) + ");if(!f)return JSON.stringify({noForm:true});" +
                "var p=new URLSearchParams(new FormData(f));var v=" + fields + ";" +
                "Object.keys(v).forEach(function(k){p.set(k,v[k]);});" +
                "var r=await fetch(" + PortalTthcSource.Literal(SearchPath) + "+'?'+p.toString(),{method:'GET',credentials:'same-origin'," +
                "headers:{'HX-Request':'true','HX-Target':'table-container','accept':'text/html-partial'}});" +
                ReadResultScript;
        }

        /// <summary>
        /// Tab thuế điện tử: đúng như <c>btnSearch_tdt</c>/<c>changePage_tdt</c> của trang: POST urlencoded tới địa chỉ của form với giá trị form (đã có
        /// <c>_csrf</c>), kèm header CSRF của htmx.
        /// </summary>
        private static string BuildETaxSearchScript(HoSoQuery q)
        {
            string fields = Fields(
                "maToKhai_tdt", q.Declaration,
                "maGiaoDichTthc_tdt", q.TransactionCode,
                "tuNgay_tdt", Date(q.FromDate),
                "denNgay_tdt", Date(q.ToDate),
                "scope_tdt2", "SELF",
                "mstUyQuyen_tdt2", q.AuthorizedTaxCode,
                "captcha", q.CaptchaText,
                "page", Page(q),
                "size", Size());

            return "(async function(){" +
                "var f=document.getElementById(" + PortalTthcSource.Literal(ETaxFormId) + ");if(!f)return JSON.stringify({noForm:true});" +
                "var p=new URLSearchParams(new FormData(f));var v=" + fields + ";" +
                "Object.keys(v).forEach(function(k){p.set(k,v[k]);});" +
                "var m=function(n){var e=document.querySelector('meta[name=\\\"'+n+'\\\"]');return e?e.getAttribute('content'):null;};" +
                "var h={'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8','HX-Request':'true','HX-Target':'bangKetQuaTraCuu_tdt','accept':'text/html-partial'};" +
                "var hn=m('csrf-header'),tk=m('csrf-token');if(hn&&tk)h[hn]=tk;" +
                "var r=await fetch(" + PortalTthcSource.Literal(ETaxPath) + ",{method:'POST',credentials:'same-origin',headers:h,body:p.toString()});" +
                ReadResultScript;
        }

        /// <summary>Phần chung: đọc phản hồi và để CHÍNH TRÌNH DUYỆT (DOMParser) đọc bảng, dòng tổng, ô trang hiện tại.</summary>
        private const string ReadResultScript =
            "var t=await r.text();var out={http:r.status,loginPage:/modalLdap|loginLDAP/.test(t)};" +
            "if(r.status===200&&!out.loginPage){try{" +
            "var d=new DOMParser().parseFromString(t,'text/html');" +
            "var tx=function(n){return(n.textContent||'').replace(/\\s+/g,' ').trim();};" +
            "var tb=d.querySelector('table');" +
            "var m=/Trang\\s*(\\d*)\\s*\\/\\s*(\\d+).*?b\\u1ea3n\\s*ghi\\s*:?\\s*(\\d+)/i.exec(tx(d.body));" +
            "out.sum=m?[m[1],m[2],m[3]]:null;" +
            "var pin=d.querySelector('section input')||d.querySelector('input[type=number]');out.pageInput=pin?pin.value:'';" +
            "if(tb){var hr=tb.querySelector('thead tr')||tb.querySelector('tr');" +
            "var rows=[];[].forEach.call(tb.querySelectorAll('tr'),function(tr){if(tr!==hr)rows.push([].map.call(tr.children,tx));});" +
            "out.table={heads:[].map.call(hr.children,tx),rows:rows};}" +
            "else{out.text=tx(d.body).slice(0,300);}" +
            "}catch(e){}out.body=t;}" +
            "return JSON.stringify(out);})()";

        /// <summary>Cổng nhận "Tên TTHC" dạng mã trong ô chọn; chỉ gửi khi người dùng nhập đúng dạng mã (chữ số và dấu chấm), còn lại bỏ qua.</summary>
        private static string CodeOrEmpty(string value)
        {
            return TthcService.IsValidCode(value) ? value : string.Empty;
        }
    }
}
