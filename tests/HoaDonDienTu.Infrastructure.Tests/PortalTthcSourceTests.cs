using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using HoaDonDienTu.Infrastructure.PublicService;
using Newtonsoft.Json;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>T-DVC02-10 (DD-DVC-02): nguồn thật chỉ tạo hai loại yêu cầu tới địa chỉ cố định. Runner giả, không chạm cổng thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class PortalTthcSourceTests
    {
        private FakeRunner _runner;
        private PortalTthcSource _source;

        [SetUp]
        public void SetUp()
        {
            _runner = new FakeRunner();
            _source = new PortalTthcSource(_runner);
        }

        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name));
        }

        private static string Ok(string body)
        {
            return JsonConvert.SerializeObject(new { status = 200, body });
        }

        [Test]
        public async Task Search_RunsOnListPage_AndParsesResult()
        {
            _runner.Reply = Ok(Fixture("tthc-list.sample.html"));

            TthcPage page = await _source.SearchAsync(new TthcQuery { Keyword = "hóa đơn", Level = "01", PageNumber = 2 }, CancellationToken.None);

            Assert.AreEqual(PortalTthcSource.ListPath, _runner.Paths[0]);
            Assert.AreEqual(3, page.Items.Count);
            Assert.AreEqual(147, page.TotalRecords);
        }

        [Test]
        public async Task Search_Script_CarriesFieldsPageSizeAndCsrfHeaderMechanism()
        {
            _runner.Reply = Ok(Fixture("tthc-list.sample.html"));

            await _source.SearchAsync(new TthcQuery { Keyword = "kw", ProcedureType = "012", Declaration = "33", Level = "02", PageNumber = 3 }, CancellationToken.None);

            string script = _runner.Scripts[0];
            StringAssert.Contains("\"maTTHC\":\"kw\"", script);
            StringAssert.Contains("\"maNghiepVu\":\"012\"", script);
            StringAssert.Contains("\"maToKhai\":\"33\"", script);
            StringAssert.Contains("\"mucDo\":\"02\"", script);
            StringAssert.Contains("\"page\":\"3\"", script);
            StringAssert.Contains("\"size\":\"10\"", script);
            StringAssert.Contains("csrf-header", script);
            StringAssert.Contains("csrf-token", script);
            StringAssert.Contains("text/html-partial", script);
            StringAssert.Contains("_csrf", script);
        }

        [TestCase("\"; alert(1); //")]
        [TestCase("</script><script>alert(1)</script>")]
        [TestCase("dòng1\ndòng2\r\n'quote'")]
        [TestCase("\u2028\u2029")]
        public async Task T_DVC02_10_Keyword_IsAlwaysAJsonStringLiteral(string keyword)
        {
            _runner.Reply = Ok(Fixture("tthc-list.sample.html"));

            await _source.SearchAsync(new TthcQuery { Keyword = keyword, PageNumber = 1 }, CancellationToken.None);

            string script = _runner.Scripts[0];
            // Không có ký tự điều khiển/dòng mới thô nào của từ khóa trong script; không có thẻ đóng script.
            StringAssert.DoesNotContain("\n", script);
            StringAssert.DoesNotContain("\r", script);
            StringAssert.DoesNotContain("\u2028", script);
            StringAssert.DoesNotContain("\u2029", script);
            StringAssert.DoesNotContain("</script", script);
            // Từ khóa nằm đúng trong literal JSON của trường maTTHC (khớp với cách mã hóa mong đợi).
            string expected = "\"maTTHC\":" + PortalTthcSource_Literal(keyword);
            StringAssert.Contains(expected, script);
        }

        private static string PortalTthcSource_Literal(string value)
        {
            string json = JsonConvert.SerializeObject(value);
            return json.Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029").Replace("<", "\\u003c");
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Search_PageBelow1_NeverReachesRunner(int page)
        {
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _source.SearchAsync(new TthcQuery { PageNumber = page }, CancellationToken.None));
            Assert.AreEqual(0, _runner.Scripts.Count);
        }

        [Test]
        public async Task Detail_FetchesOnlyTheWhitelistedDetailPath()
        {
            _runner.Reply = Ok(Fixture("tthc-detail.sample.html"));

            TthcDetail detail = await _source.GetDetailAsync("1.007674", CancellationToken.None);

            Assert.AreEqual("1.007674", detail.Code);
            StringAssert.Contains("/tthc/homelogin/chi-tiet-tthc?maTTHC=1.007674", _runner.Scripts[0]);
        }

        [TestCase("")]
        [TestCase("1.0/../x")]
        [TestCase("1.007674&x=1")]
        [TestCase("abc")]
        public void Detail_InvalidCode_NeverReachesRunner(string code)
        {
            Assert.ThrowsAsync<ArgumentException>(() => _source.GetDetailAsync(code, CancellationToken.None));
            Assert.AreEqual(0, _runner.Scripts.Count);
        }

        [Test]
        public async Task T_DVC02_10_EveryRequest_TargetsOnlyTheTwoWhitelistedPaths()
        {
            _runner.Reply = Ok(Fixture("tthc-list.sample.html"));
            await _source.SearchAsync(new TthcQuery { Keyword = "x", PageNumber = 1 }, CancellationToken.None);
            _runner.Reply = Ok(Fixture("tthc-detail.sample.html"));
            await _source.GetDetailAsync("1.007674", CancellationToken.None);
            _runner.Reply = Ok("<form id='form_search'><select name='mucDo'><option value='01'>Toàn trình</option></select></form>");
            await _source.GetFilterOptionsAsync(CancellationToken.None);

            foreach (string script in _runner.Scripts)
            {
                foreach (Match m in Regex.Matches(script, @"fetch\((""[^""]*"")"))
                {
                    string url = JsonConvert.DeserializeObject<string>(m.Groups[1].Value);
                    Assert.IsTrue(
                        url == PortalTthcSource.ListPath || Regex.IsMatch(url, @"^" + Regex.Escape(PortalTthcSource.DetailPath) + @"\?maTTHC=\d+(\.\d+)*$"),
                        "Địa chỉ ngoài danh sách trắng: " + url);
                }
                StringAssert.DoesNotContain("XMLHttpRequest", script);
                StringAssert.DoesNotContain("eval(", script);
                StringAssert.DoesNotContain("document.cookie", script);
                StringAssert.DoesNotContain("localStorage", script);
            }
            Assert.IsTrue(_runner.Paths.TrueForAll(p => p == PortalTthcSource.ListPath), "Chỉ một trang DVC trong WebView");
        }

        [Test]
        public void NonOkStatus_IsReportedAsGatewayError_NotParseError()
        {
            _runner.Reply = JsonConvert.SerializeObject(new { status = 403, body = "Request Rejected" });

            Assert.ThrowsAsync<InvalidOperationException>(() => _source.SearchAsync(new TthcQuery { PageNumber = 1 }, CancellationToken.None));
        }

        [Test]
        public void GarbageReply_IsParseError()
        {
            _runner.Reply = "không phải JSON";

            Assert.ThrowsAsync<TthcParseException>(() => _source.SearchAsync(new TthcQuery { PageNumber = 1 }, CancellationToken.None));
        }

        [Test]
        public void WrongHtml_IsParseError_NotEmptyList()
        {
            _runner.Reply = Ok("<div>Cổng đã đổi giao diện</div>");

            Assert.ThrowsAsync<TthcParseException>(() => _source.SearchAsync(new TthcQuery { PageNumber = 1 }, CancellationToken.None));
        }

        [Test]
        public async Task Requests_AreSerial_NeverOverlap()
        {
            _runner.Reply = Ok(Fixture("tthc-list.sample.html"));
            _runner.DelayMs = 60;

            var tasks = new List<Task>();
            for (int i = 1; i <= 4; i++) tasks.Add(_source.SearchAsync(new TthcQuery { PageNumber = i }, CancellationToken.None));
            await Task.WhenAll(tasks);

            Assert.AreEqual(1, _runner.MaxConcurrent);
            Assert.AreEqual(4, _runner.Scripts.Count);
        }

        [Test]
        public void Cancellation_Propagates()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.CatchAsync<OperationCanceledException>(() => _source.SearchAsync(new TthcQuery { PageNumber = 1 }, cts.Token));
            }
        }

        private sealed class FakeRunner : IDvcPageRunner
        {
            public readonly List<string> Scripts = new List<string>();
            public readonly List<string> Paths = new List<string>();
            public string Reply = string.Empty;
            public int DelayMs;
            public int MaxConcurrent;
            private int _current;

            public async Task<string> RunOnPageAsync(string pagePath, string script, CancellationToken cancellationToken)
            {
                int now = Interlocked.Increment(ref _current);
                lock (Scripts) { if (now > MaxConcurrent) MaxConcurrent = now; Paths.Add(pagePath); Scripts.Add(script); }
                try
                {
                    if (DelayMs > 0) await Task.Delay(DelayMs, cancellationToken);
                    return Reply;
                }
                finally
                {
                    Interlocked.Decrement(ref _current);
                }
            }
        }
    }
}
