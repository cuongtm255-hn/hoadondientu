using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using HoaDonDienTu.Infrastructure.PublicService;
using Newtonsoft.Json;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>T-DVC03-09/10 (DD-DVC-03): bộ phân tích và nguồn hồ sơ đã nộp thật. Mẫu HTML giả dựng lại từ cấu trúc cổng; runner giả.</summary>
    [TestFixture]
    [Category("Fixture")]
    public class HoSoRealSourceTests
    {
        private static string Fixture()
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "hoso-list.sample.html"));
        }

        private static string Ok(string body, bool loginPage = false)
        {
            return JsonConvert.SerializeObject(new { http = 200, loginPage, body });
        }

        // ---------------- Bộ phân tích ----------------

        [Test]
        public void T_DVC03_09_Parser_ReadsColumns_Rows_Totals_AndActivePage()
        {
            HoSoPage page = HoSoHtmlParser.ParseList(Fixture());

            CollectionAssert.AreEqual(
                new[] { "Mã hồ sơ", "Tên TTHC", "Tờ khai", "Kỳ tính thuế", "Loại tờ khai", "Lần nộp bổ sung", "Lần nộp (Tờ khai)/File số (DLVC)", "Cơ quan thuế tiếp nhận", "Ngày nộp", "Trạng thái", "Hồ sơ đính kèm" },
                page.Columns, "Bỏ cột STT và Thao tác, giữ đúng thứ tự cổng");
            Assert.AreEqual(2, page.Rows.Count);
            Assert.AreEqual("000.000.00.G00-260101-000000000001", page.Rows[0].Cells[0]);
            Assert.AreEqual("Q4/2025", page.Rows[0].Cells[3]);
            Assert.AreEqual("03/01/2026 15:45", page.Rows[0].Cells[8]);
            Assert.AreEqual("Đã chấp nhận", page.Rows[0].Cells[9]);
            Assert.AreEqual("Bổ sung", page.Rows[1].Cells[4]);
            Assert.AreEqual("tệp-mẫu.xml", page.Rows[1].Cells[10]);
            Assert.AreEqual(page.Columns.Count, page.Rows[0].Cells.Count);
            Assert.AreEqual(23, page.TotalRecords);
            Assert.AreEqual(3, page.PageCount);
            Assert.AreEqual(1, page.PageNumber, "Trang hiện tại lấy từ mục active của phân trang");
        }

        [Test]
        public void T_DVC03_09_Parser_EmptyMessage_IsValidEmptyPage()
        {
            HoSoPage page = HoSoHtmlParser.ParseList("<div id='table-container'><div class='alert'>Không có dữ liệu</div></div>");

            Assert.AreEqual(0, page.Rows.Count);
            Assert.AreEqual("Không có dữ liệu", page.EmptyMessage);
            Assert.AreEqual(0, page.TotalRecords);
        }

        [Test]
        public void T_DVC03_09_Parser_DropsScriptAndHandlers_AndBadShapeThrows()
        {
            string html = Fixture().Replace("tệp-mẫu.xml", "tệp<script>evil()</script>-mẫu").Replace("Đã chấp nhận", "Đã chấp nhận<img src=x onerror=bad()>");
            HoSoPage page = HoSoHtmlParser.ParseList(html);
            string all = string.Join("|", page.Rows.SelectMany(r => r.Cells));
            StringAssert.DoesNotContain("evil", all);
            StringAssert.DoesNotContain("onerror", all);
            StringAssert.DoesNotContain("bad(", all);
            StringAssert.DoesNotContain("viewFile", all);

            Assert.Throws<HoSoParseException>(() => HoSoHtmlParser.ParseList(""));
            Assert.Throws<HoSoParseException>(() => HoSoHtmlParser.ParseList("<div>" + new string('x', 400) + "</div>"), "Mảnh dài không bảng không phải thông điệp rỗng");
            Assert.Throws<HoSoParseException>(() => HoSoHtmlParser.ParseList("<table><thead><tr><th>A</th><th>B</th></tr></thead><tbody><tr><td>1</td></tr></tbody></table>"), "Dòng lệch cột");
            Assert.Throws<HoSoParseException>(() => HoSoHtmlParser.ParseList("<table><thead><tr><th>A</th></tr></thead><tbody><tr><td>1</td></tr></tbody></table>"), "Thiếu dòng tổng");
        }

        [Test]
        public void T_DVC03_09_Parser_FirstColumnAsTh_And_NoTheadNoTbody_StillParse()
        {
            const string html =
                "<div><span>Trang <span></span>/<span>1</span> - Tổng số bản ghi: <span>1</span></span>" +
                "<table class='table table-bordered'>" +
                "<tr><th>STT</th><th>Thao tác</th><th>Mã hồ sơ</th><th>Trạng thái</th></tr>" +
                "<tr><th scope='row'>1</th><td><i></i></td><td>HS-MAU-1</td><td>Đã chấp nhận</td></tr>" +
                "</table><ul class='pagination'><li class='page-item active'><a>1</a></li></ul></div>";

            HoSoPage page = HoSoHtmlParser.ParseList(html);

            CollectionAssert.AreEqual(new[] { "Mã hồ sơ", "Trạng thái" }, page.Columns);
            Assert.AreEqual(1, page.Rows.Count);
            Assert.AreEqual("HS-MAU-1", page.Rows[0].Cells[0]);
            Assert.AreEqual(1, page.TotalRecords);
        }

        [Test]
        public void T_DVC03_09_ParseError_ExplainsWhy_WithCounts()
        {
            var ex = Assert.Throws<HoSoParseException>(() => HoSoHtmlParser.ParseList(
                "<table><thead><tr><th>A</th><th>B</th></tr></thead><tbody><tr><td>1</td></tr></tbody></table>"));

            StringAssert.Contains("1 ô", ex.Message);
            StringAssert.Contains("2 cột", ex.Message);
        }

        [Test]
        public void DescribeStructure_KeepsShapeOnly_NoRealText()
        {
            string html = "<div class='x'><table><tr><td class='c'>Hồ sơ 12345 bí mật</td></tr></table><script>secret()</script></div>";

            string text = HoSoHtmlParser.DescribeStructure(html);

            StringAssert.Contains("table", text);
            StringAssert.Contains("td.c", text);
            StringAssert.DoesNotContain("bí mật", text);
            StringAssert.DoesNotContain("12345", text);
            StringAssert.DoesNotContain("secret", text);
            Assert.LessOrEqual(text.Length, 3500);
        }

        [Test]
        public void Source_ParseError_CarriesStructureButNoContent()
        {
            var runner = new FakeRunner { Reply = Ok("<div>" + new string('x', 400) + " NOIDUNGBIMAT</div>") };

            var ex = Assert.ThrowsAsync<HoSoParseException>(() => new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None));

            StringAssert.Contains("cấu trúc:", ex.Message);
            StringAssert.DoesNotContain("NOIDUNGBIMAT", ex.Message);
        }

        // ---------------- Nguồn thật (runner giả) ----------------

        private sealed class FakeRunner : IDvcPageRunner
        {
            public readonly List<string> Scripts = new List<string>();
            public readonly List<string> Paths = new List<string>();
            public string Reply = string.Empty;

            public Task<string> RunOnPageAsync(string pagePath, string script, CancellationToken cancellationToken)
            {
                Paths.Add(pagePath);
                Scripts.Add(script);
                return Task.FromResult(Reply);
            }
        }

        private static HoSoQuery Query(int page = 1)
        {
            return new HoSoQuery
            {
                Source = HoSoSource.Dvc,
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 1, 31),
                CaptchaText = "kY7Q2",
                AuthorizedTaxCode = "0109999999",
                FileCode = "HS-MAU-0001",
                ProcedureName = "1.007674",
                PageNumber = page
            };
        }

        [Test]
        public async Task T_DVC03_10_Search_RunsOnEfilePage_AndParsesResult()
        {
            var runner = new FakeRunner { Reply = Ok(Fixture()) };
            var source = new PortalHoSoSource(runner);

            HoSoPage page = await source.SearchAsync(Query(2), CancellationToken.None);

            Assert.AreEqual(PortalHoSoSource.PagePath, runner.Paths[0]);
            Assert.AreEqual(23, page.TotalRecords);
            string script = runner.Scripts[0];
            StringAssert.Contains("/tthc/ho-so/search", script);
            StringAssert.Contains("\"tuNgay\":\"01/01/2026\"", script);
            StringAssert.Contains("\"denNgay\":\"31/01/2026\"", script);
            StringAssert.Contains("\"page\":\"2\"", script);
            StringAssert.Contains("\"size\":\"10\"", script);
            StringAssert.Contains("\"captcha\":\"kY7Q2\"", script);
            StringAssert.Contains("\"maTTHC\":\"1.007674\"", script);
            StringAssert.Contains("new FormData(f)", script, "Gửi như onChangePage: bắt đầu từ FormData của form");
            StringAssert.DoesNotContain("document.cookie", script);
            StringAssert.DoesNotContain("localStorage", script);
        }

        [Test]
        public async Task Search_FreeTextProcedureName_IsNotSentAsCode()
        {
            var runner = new FakeRunner { Reply = Ok(Fixture()) };
            var q = Query();
            q.ProcedureName = "Khai thuế giá trị gia tăng";

            await new PortalHoSoSource(runner).SearchAsync(q, CancellationToken.None);

            StringAssert.Contains("\"maTTHC\":\"\"", runner.Scripts[0]);
        }

        [Test]
        public void Search_UserValues_AreJsonLiterals_NotRawStrings()
        {
            var runner = new FakeRunner { Reply = Ok(Fixture()) };
            var q = Query();
            q.CaptchaText = "\"; alert(1); //";
            q.AuthorizedTaxCode = "</script><script>x</script>\n";
            q.FileCode = "\u2028\u2029";

            new PortalHoSoSource(runner).SearchAsync(q, CancellationToken.None).GetAwaiter().GetResult();

            string script = runner.Scripts[0];
            StringAssert.DoesNotContain("</script", script);
            StringAssert.DoesNotContain("\n", script);
            StringAssert.DoesNotContain("\u2028", script);
            StringAssert.DoesNotContain("\u2029", script);
            StringAssert.Contains("\"captcha\":" + PortalTthcSource.Literal(q.CaptchaText), script);
        }

        private static string DomReply(object table, string[] sum, string pageInput = "1", string text = null)
        {
            return JsonConvert.SerializeObject(new { http = 200, loginPage = false, sum, pageInput, table, text, body = "<html></html>" });
        }

        private static readonly string[] Heads13 =
        {
            "STT", "Thao tác", "Mã hồ sơ", "Tên TTHC", "Tờ khai", "Kỳ tính thuế", "Loại tờ khai", "Lần nộp bổ sung",
            "Lần nộp (Tờ khai)/File số (DLVC)", "Cơ quan thuế tiếp nhận", "Ngày nộp", "Trạng thái", "Hồ sơ đính kèm"
        };

        [Test]
        public async Task Dom_Table_IsUsedDirectly_ColumnsAndRows()
        {
            var row = new[] { "1", "", "MA-MAU-1", "1.000001 - TTHC mẫu", "01/MAU - Tờ khai mẫu", "Q4/2025", "Chính thức", "", "1", "00000 - CQ mẫu", "03/01/2026 15:45", "Đã chấp nhận", "" };
            var runner = new FakeRunner { Reply = DomReply(new { heads = Heads13, rows = new[] { row } }, new[] { "", "1", "1" }, "1") };

            HoSoPage page = await new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None);

            Assert.AreEqual(11, page.Columns.Count);
            Assert.AreEqual("Mã hồ sơ", page.Columns[0]);
            Assert.AreEqual("MA-MAU-1", page.Rows[0].Cells[0]);
            Assert.AreEqual("Đã chấp nhận", page.Rows[0].Cells[9]);
            Assert.AreEqual(1, page.TotalRecords);
            Assert.AreEqual(1, page.PageNumber);
            Assert.AreEqual(1, page.PageCount);
        }

        [Test]
        public async Task Dom_Row_MissingTrailingEmptyCell_IsPadded_NotRejected()
        {
            // Hình dạng thật đã gặp: dòng 12 ô, tiêu đề 13 cột (ô cuối rỗng bị cổng bỏ).
            var row = new[] { "1", "", "MA-MAU-1", "1.000001 - TTHC mẫu", "01/MAU - Tờ khai mẫu", "Q4/2025", "Chính thức", "1", "1", "00000 - CQ mẫu", "03/01/2026 15:45", "Đã chấp nhận" };
            var runner = new FakeRunner { Reply = DomReply(new { heads = Heads13, rows = new[] { row } }, new[] { "", "1", "1" }) };

            HoSoPage page = await new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None);

            Assert.AreEqual(page.Columns.Count, page.Rows[0].Cells.Count);
            Assert.AreEqual(string.Empty, page.Rows[0].Cells[10], "Ô cuối được bù rỗng");
            Assert.AreEqual("Đã chấp nhận", page.Rows[0].Cells[9]);
        }

        [Test]
        public void Dom_Row_WithTooFewOrTooManyCells_IsRejected_WithCountsInTheDiagnostic()
        {
            var tooFew = new[] { "1", "", "MA" };
            var runner = new FakeRunner { Reply = DomReply(new { heads = Heads13, rows = new[] { tooFew } }, new[] { "", "1", "1" }) };

            var ex = Assert.ThrowsAsync<HoSoParseException>(() => new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None));

            StringAssert.Contains("3 ô", ex.Diagnostic);
            StringAssert.Contains("13 cột", ex.Diagnostic);
        }

        [Test]
        public async Task NoTable_WithZeroTotal_IsTheEmptyResult_WithFriendlyMessage()
        {
            var runner = new FakeRunner { Reply = DomReply(null, new[] { "", "0", "0" }, "", "Trang /0 - Tổng số bản ghi: 0") };

            HoSoPage page = await new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None);

            Assert.AreEqual(0, page.Rows.Count);
            Assert.AreEqual(0, page.Columns.Count);
            Assert.AreEqual("Không có dữ liệu", page.EmptyMessage);
            Assert.AreEqual(0, page.TotalRecords);
        }

        [Test]
        public async Task Dom_PageInput_GivesTheCurrentPage()
        {
            var row = new[] { "11", "", "MA-MAU-11", "x", "y", "Q1/2026", "Chính thức", "", "1", "00000 - CQ", "03/01/2026 15:45", "Đã chấp nhận", "" };
            var runner = new FakeRunner { Reply = DomReply(new { heads = Heads13, rows = new[] { row } }, new[] { "", "3", "23" }, "2") };

            HoSoPage page = await new PortalHoSoSource(runner).SearchAsync(Query(2), CancellationToken.None);

            Assert.AreEqual(2, page.PageNumber);
            Assert.AreEqual(3, page.PageCount);
            Assert.AreEqual(23, page.TotalRecords);
        }

        [Test]
        public void Search_Http400_IsWrongCaptcha()
        {
            var runner = new FakeRunner { Reply = JsonConvert.SerializeObject(new { http = 400, loginPage = false, body = "" }) };

            var ex = Assert.ThrowsAsync<HoSoServiceException>(() => new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None));

            Assert.AreEqual(HoSoFailure.WrongCaptcha, ex.Failure);
        }

        [TestCase(401)]
        [TestCase(403)]
        public void Search_Http401or403_IsSessionExpired(int http)
        {
            var runner = new FakeRunner { Reply = JsonConvert.SerializeObject(new { http, loginPage = false, body = "" }) };

            Assert.ThrowsAsync<DvcSessionExpiredException>(() => new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None));
        }

        [Test]
        public void Search_LoginPageBody_OrMissingForm_IsSessionExpired()
        {
            Assert.ThrowsAsync<DvcSessionExpiredException>(() => new PortalHoSoSource(new FakeRunner { Reply = Ok("<form>modalLdap</form>", loginPage: true) })
                .SearchAsync(Query(), CancellationToken.None));
            Assert.ThrowsAsync<DvcSessionExpiredException>(() => new PortalHoSoSource(new FakeRunner { Reply = "{\"noForm\":true}" })
                .SearchAsync(Query(), CancellationToken.None));
        }

        [Test]
        public void Search_Http500_IsPortalHttpException_AndWrongHtmlIsParseError()
        {
            var runner = new FakeRunner { Reply = JsonConvert.SerializeObject(new { http = 500, loginPage = false, body = "" }) };
            var ex = Assert.ThrowsAsync<DvcPortalHttpException>(() => new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None));
            Assert.AreEqual(500, ex.HttpStatus);

            runner.Reply = Ok("<div>" + new string('x', 500) + "</div>");
            Assert.ThrowsAsync<HoSoParseException>(() => new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None));
        }

        private static HoSoQuery ETaxQuery(int page = 1)
        {
            return new HoSoQuery
            {
                Source = HoSoSource.ThueDienTu,
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 1, 31),
                CaptchaText = "kY7Q2",
                TransactionCode = "GD-MAU-0001",
                Declaration = "01",
                AuthorizedTaxCode = "0109999999",
                PageNumber = page
            };
        }

        private static readonly string[] ETaxHeads =
        {
            "STT", "Mã giao dịch", "Tờ khai/Phụ lục", "Kỳ tính thuế", "Loại tờ khai", "Lần nộp", "Lần bổ sung", "Ngày nộp", "Nơi nộp",
            "Tiến trình giải quyết hồ sơ (Trạng thái)", "Thao tác"
        };

        [Test]
        public async Task ETax_Search_PostsTheFormToTheETaxEndpoint_WithCsrfAndFields()
        {
            var runner = new FakeRunner { Reply = DomReply(new { heads = ETaxHeads, rows = new string[0][] }, new[] { "1", "1", "0" }) };

            await new PortalHoSoSource(runner).SearchAsync(ETaxQuery(2), CancellationToken.None);

            string script = runner.Scripts[0];
            Assert.AreEqual(PortalHoSoSource.PagePath, runner.Paths[0]);
            StringAssert.Contains("form_search_tdt", script);
            StringAssert.Contains("/tthc/tchs/thuedientu", script);
            StringAssert.Contains("method:'POST'", script);
            StringAssert.Contains("application/x-www-form-urlencoded", script);
            StringAssert.Contains("bangKetQuaTraCuu_tdt", script);
            StringAssert.Contains("csrf-header", script);
            StringAssert.Contains("csrf-token", script);
            StringAssert.Contains("\"tuNgay_tdt\":\"01/01/2025\"", script);
            StringAssert.Contains("\"denNgay_tdt\":\"31/01/2025\"", script);
            StringAssert.Contains("\"maGiaoDichTthc_tdt\":\"GD-MAU-0001\"", script);
            StringAssert.Contains("\"maToKhai_tdt\":\"01\"", script);
            StringAssert.Contains("\"scope_tdt2\":\"SELF\"", script);
            StringAssert.Contains("\"mstUyQuyen_tdt2\":\"0109999999\"", script);
            StringAssert.Contains("\"captcha\":\"kY7Q2\"", script);
            StringAssert.Contains("\"page\":\"2\"", script);
            StringAssert.Contains("\"size\":\"10\"", script);
            StringAssert.DoesNotContain("document.cookie", script);
            StringAssert.DoesNotContain("localStorage", script);
        }

        [Test]
        public async Task ETax_Table_IsParsed_With9DataColumns()
        {
            var row = new[] { "1", "9999", "01/MAU - Tờ khai mẫu", "Q4/2024", "Chính thức", "1", "0", "10/01/2025 09:15:00", "Thuế cơ sở mẫu", "Cơ quan thuế chấp nhận hồ sơ", "" };
            var runner = new FakeRunner { Reply = DomReply(new { heads = ETaxHeads, rows = new[] { row } }, new[] { "1", "1", "1" }, "") };

            HoSoPage page = await new PortalHoSoSource(runner).SearchAsync(ETaxQuery(), CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "Mã giao dịch", "Tờ khai/Phụ lục", "Kỳ tính thuế", "Loại tờ khai", "Lần nộp", "Lần bổ sung", "Ngày nộp", "Nơi nộp", "Tiến trình giải quyết hồ sơ (Trạng thái)" },
                page.Columns);
            Assert.AreEqual("9999", page.Rows[0].Cells[0]);
            Assert.AreEqual("10/01/2025 09:15:00", page.Rows[0].Cells[6]);
            Assert.AreEqual(1, page.TotalRecords);
            Assert.AreEqual(1, page.PageNumber, "Ô trang trống thì lấy từ dòng tổng");
        }

        [Test]
        public async Task ETax_EmptyResult_NoTable_IsFriendlyEmptyPage()
        {
            var runner = new FakeRunner { Reply = DomReply(null, new[] { "", "0", "0" }, "", "Trang /0 - Tổng số bản ghi: 0") };

            HoSoPage page = await new PortalHoSoSource(runner).SearchAsync(ETaxQuery(), CancellationToken.None);

            Assert.AreEqual(0, page.Rows.Count);
            Assert.AreEqual("Không có dữ liệu", page.EmptyMessage);
        }

        [Test]
        public void ETax_Http400_IsWrongCaptcha_And401IsSessionExpired()
        {
            var bad = new FakeRunner { Reply = JsonConvert.SerializeObject(new { http = 400, loginPage = false, body = "" }) };
            var ex = Assert.ThrowsAsync<HoSoServiceException>(() => new PortalHoSoSource(bad).SearchAsync(ETaxQuery(), CancellationToken.None));
            Assert.AreEqual(HoSoFailure.WrongCaptcha, ex.Failure);

            var gone = new FakeRunner { Reply = JsonConvert.SerializeObject(new { http = 401, loginPage = false, body = "" }) };
            Assert.ThrowsAsync<DvcSessionExpiredException>(() => new PortalHoSoSource(gone).SearchAsync(ETaxQuery(), CancellationToken.None));
        }

        [Test]
        public async Task ETax_Captcha_ComesFromTheETaxFormImage()
        {
            var runner = new FakeRunner { Reply = JsonConvert.SerializeObject(new { png = "data:image/png;base64," + Convert.ToBase64String(new byte[] { 5, 6 }) }) };

            byte[] bytes = await new PortalHoSoSource(runner).GetCaptchaAsync(HoSoSource.ThueDienTu, CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 5, 6 }, bytes);
            StringAssert.Contains("form_search_tdt", runner.Scripts[0]);
            StringAssert.DoesNotContain("form-search-advanced", runner.Scripts[0]);
        }

        [Test]
        public async Task ETax_Script_UsesOnlyTheWhitelistedUrl_AndUserValuesAreLiterals()
        {
            var runner = new FakeRunner { Reply = DomReply(new { heads = ETaxHeads, rows = new string[0][] }, new[] { "1", "1", "0" }) };
            var q = ETaxQuery();
            q.CaptchaText = "\"; alert(1); //";
            q.TransactionCode = "</script><script>x</script>\n";

            await new PortalHoSoSource(runner).SearchAsync(q, CancellationToken.None);

            string script = runner.Scripts[0];
            StringAssert.DoesNotContain("</script", script);
            StringAssert.DoesNotContain("\n", script);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(script, @"fetch\((""[^""]*"")"))
            {
                Assert.AreEqual("/tthc/tchs/thuedientu", JsonConvert.DeserializeObject<string>(m.Groups[1].Value));
            }
        }

        [Test]
        public async Task Captcha_DecodesPng_FromFormImage()
        {
            var runner = new FakeRunner { Reply = JsonConvert.SerializeObject(new { png = "data:image/png;base64," + Convert.ToBase64String(new byte[] { 9, 8, 7 }) }) };

            byte[] bytes = await new PortalHoSoSource(runner).GetCaptchaAsync(HoSoSource.Dvc, CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, bytes);
            StringAssert.Contains("form-search-advanced", runner.Scripts[0]);
            StringAssert.Contains("toDataURL", runner.Scripts[0]);
        }

        [Test]
        public async Task Captcha_Script_MatchesImageIdsLikeImageCaptTdt_AndFallsBackToTheOnlyImage()
        {
            var runner = new FakeRunner { Reply = JsonConvert.SerializeObject(new { png = "data:image/png;base64,AQID" }) };

            await new PortalHoSoSource(runner).GetCaptchaAsync(HoSoSource.ThueDienTu, CancellationToken.None);

            string script = runner.Scripts[0];
            StringAssert.Contains("/capt/i", script, "id image-capt-tdt không chứa chữ captcha");
            StringAssert.Contains("imgs.length===1", script);
        }

        [Test]
        public void Captcha_NoImage_IsError_AndMissingFormIsSessionExpired()
        {
            Assert.CatchAsync<InvalidOperationException>(() => new PortalHoSoSource(new FakeRunner { Reply = "{\"png\":\"\"}" }).GetCaptchaAsync(HoSoSource.Dvc, CancellationToken.None));
            Assert.ThrowsAsync<DvcSessionExpiredException>(() => new PortalHoSoSource(new FakeRunner { Reply = "{\"noForm\":true}" }).GetCaptchaAsync(HoSoSource.Dvc, CancellationToken.None));
        }

        [Test]
        public void Script_UsesOnlyTheWhitelistedUrl()
        {
            var runner = new FakeRunner { Reply = Ok(Fixture()) };
            new PortalHoSoSource(runner).SearchAsync(Query(), CancellationToken.None).GetAwaiter().GetResult();

            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(runner.Scripts[0], @"fetch\((""[^""]*"")"))
            {
                Assert.AreEqual("/tthc/ho-so/search", JsonConvert.DeserializeObject<string>(m.Groups[1].Value));
            }
        }
    }
}
