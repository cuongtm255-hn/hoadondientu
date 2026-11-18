using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.PublicService
{
    /// <summary>
    /// Nguồn danh mục TTHC thật (Release): gửi form ngay trong trang công khai của cổng bằng chính cơ chế htmx/CSRF của cổng
    /// (khảo sát 18/11/2026, G-DVC-03) rồi chuyển HTML cho <see cref="TthcHtmlParser"/>. Chỉ có đúng hai loại yêu cầu (tìm, chi tiết)
    /// tới hai địa chỉ cố định (danh sách trắng, BR-DVC-02); mọi giá trị người dùng đi vào script dưới dạng chuỗi JSON đã mã hóa.
    /// Không cần đăng nhập. Mỗi lần chỉ một yêu cầu (BR-DVC-05).
    /// </summary>
    public sealed class PortalTthcSource : ITthcSource
    {
        public const string ListPath = "/tthc/homelogin/tthc";
        public const string DetailPath = "/tthc/homelogin/chi-tiet-tthc";

        private readonly IDvcPageRunner _runner;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public PortalTthcSource(IDvcPageRunner runner)
        {
            if (runner == null) throw new ArgumentNullException("runner");
            _runner = runner;
        }

        public async Task<TthcFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
        {
            string html = await RunAsync(BuildFormHtmlScript(), cancellationToken).ConfigureAwait(false);
            return TthcHtmlParser.ParseFilterOptions(html);
        }

        public async Task<TthcPage> SearchAsync(TthcQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException("query");
            if (query.PageNumber < 1) throw new ArgumentOutOfRangeException("query", "Số trang phải từ 1.");
            string html = await RunAsync(BuildSearchScript(query), cancellationToken).ConfigureAwait(false);
            return TthcHtmlParser.ParseList(html);
        }

        public async Task<TthcDetail> GetDetailAsync(string code, CancellationToken cancellationToken)
        {
            if (!HoaDonDienTu.Application.PublicService.TthcService.IsValidCode(code)) throw new ArgumentException("Mã TTHC không hợp lệ.", "code");
            string html = await RunAsync(BuildDetailScript(code), cancellationToken).ConfigureAwait(false);
            return TthcHtmlParser.ParseDetail(html);
        }

        /// <summary>Chạy script, đọc <c>{status, body}</c>; mã khác 200 là lỗi mạng/cổng (không phải lỗi mẫu HTML).</summary>
        private async Task<string> RunAsync(string script, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string raw = await _runner.RunOnPageAsync(ListPath, script, cancellationToken).ConfigureAwait(false);
                JObject result;
                try
                {
                    result = JObject.Parse(raw ?? string.Empty);
                }
                catch (JsonReaderException)
                {
                    throw new TthcParseException("Kết quả script không phải JSON.");
                }

                int status = result.Value<int?>("status") ?? -1;
                if (status != 200) throw new InvalidOperationException("Cổng trả mã " + status + ".");
                string body = result.Value<string>("body");
                if (string.IsNullOrEmpty(body)) throw new TthcParseException("Phản hồi rỗng.");
                return body;
            }
            finally
            {
                _gate.Release();
            }
        }

        // ---- Script dựng sẵn: mọi giá trị đi vào qua Literal() (JSON, không bao giờ ghép chuỗi thô) ----

        private const string Helpers =
            "var meta=function(n){var e=document.querySelector('meta[name=\"'+n+'\"]');return e?e.getAttribute('content'):null;};";

        internal static string BuildFormHtmlScript()
        {
            return "(async function(){var f=document.getElementById('form_search');" +
                   "return JSON.stringify({status:f?200:-1,body:f?f.outerHTML:''});})()";
        }

        internal static string BuildSearchScript(TthcQuery query)
        {
            string fields = "{" +
                "\"maTTHC\":" + Literal(query.Keyword) + "," +
                "\"maNghiepVu\":" + Literal(query.ProcedureType) + "," +
                "\"maToKhai\":" + Literal(query.Declaration) + "," +
                "\"mucDo\":" + Literal(query.Level) + "," +
                "\"page\":" + Literal(query.PageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)) + "," +
                "\"size\":" + Literal(TthcPage.DefaultPageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                "}";

            return "(async function(){" + Helpers +
                "var f=document.getElementById('form_search');var c=f&&f.querySelector('input[name=\"_csrf\"]');" +
                "if(!c)return JSON.stringify({status:-1,body:''});" +
                "var v=" + fields + ";v._csrf=c.value;" +
                "var b=Object.keys(v).map(function(k){return encodeURIComponent(k)+'='+encodeURIComponent(v[k]);}).join('&');" +
                "var h={'Content-Type':'application/x-www-form-urlencoded','accept':'text/html-partial','HX-Request':'true','HX-Target':'bangKetQuaTTHC'};" +
                "var hn=meta('csrf-header'),tk=meta('csrf-token');if(hn&&tk)h[hn]=tk;" +
                "var r=await fetch(" + Literal(ListPath) + ",{method:'POST',headers:h,body:b,credentials:'same-origin'});" +
                "return JSON.stringify({status:r.status,body:await r.text()});})()";
        }

        internal static string BuildDetailScript(string code)
        {
            return "(async function(){" +
                "var r=await fetch(" + Literal(DetailPath + "?maTTHC=" + code) + ",{method:'GET',credentials:'same-origin'});" +
                "return JSON.stringify({status:r.status,body:await r.text()});})()";
        }

        /// <summary>Chuỗi JSON làm literal JavaScript an toàn; thêm escape cho U+2028/U+2029 và '&lt;' để không thoát khỏi chuỗi hay thẻ script.</summary>
        internal static string Literal(string value)
        {
            string json = JsonConvert.SerializeObject(value ?? string.Empty);
            return json.Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029").Replace("<", "\\u003c");
        }
    }
}
