using System;
using System.IO;
using HoaDonDienTu.Domain.PublicService;
using HoaDonDienTu.Infrastructure.PublicService;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>T-DVC02-01..05 (DD-DVC-02). Dùng mẫu HTML đã làm sạch trong Fixtures, không gọi cổng thật.</summary>
    [TestFixture]
    [Category("Fixture")]
    public class TthcHtmlParserTests
    {
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name));
        }

        [Test]
        public void T_DVC02_01_List_ReadsRowsAndTotals()
        {
            TthcPage page = TthcHtmlParser.ParseList(Fixture("tthc-list.sample.html"));

            Assert.AreEqual(3, page.Items.Count);
            Assert.AreEqual("1.007607", page.Items[0].Code);
            Assert.AreEqual("Chấm dứt hiệu lực mã số thuế", page.Items[0].Name);
            Assert.AreEqual("Toàn trình", page.Items[0].Level);
            Assert.AreEqual("Cục Thuế/Thuế tỉnh, thành phố/Thuế cơ sở.", page.Items[0].Agency);
            Assert.AreEqual("1.008587", page.Items[1].Code);
            Assert.AreEqual(1, page.PageNumber);
            Assert.AreEqual(15, page.PageCount);
            Assert.AreEqual(147, page.TotalRecords);
            Assert.AreEqual(137, page.TotalProcedures);
        }

        [Test]
        public void T_DVC02_01_List_EmptyResult_WithTableAndNoSummary_IsEmptyPage()
        {
            const string html = "<div id='bangKetQuaTTHC'><table class='table'><thead><tr><th>STT</th></tr></thead><tbody></tbody></table></div>";

            TthcPage page = TthcHtmlParser.ParseList(html);

            Assert.AreEqual(0, page.Items.Count);
            Assert.AreEqual(0, page.TotalRecords);
        }

        [Test]
        public void T_DVC02_02_FilterOptions_SkipAllRow_AndTolerateMissingSelects()
        {
            TthcFilterOptions options = TthcHtmlParser.ParseFilterOptions(Fixture("tthc-filters.sample.html"));

            Assert.AreEqual(5, options.ProcedureTypes.Count);
            Assert.AreEqual("012", options.ProcedureTypes[4].Code);
            Assert.AreEqual("Hoàn thuế", options.ProcedureTypes[4].Label);
            Assert.AreEqual(3, options.Declarations.Count);
            Assert.AreEqual(3, options.Levels.Count);
            Assert.AreEqual("01", options.Levels[0].Code);
            Assert.AreEqual("Toàn trình", options.Levels[0].Label);
            Assert.AreEqual(0, options.TaxpayerTypes.Count, "Bản công khai không có ô Loại người nộp thuế");
        }

        [Test]
        public void T_DVC02_03_Detail_ReadsAll11Sections_InOrder_AsPlainText()
        {
            TthcDetail detail = TthcHtmlParser.ParseDetail(Fixture("tthc-detail.sample.html"));

            Assert.AreEqual("1.007674", detail.Code);
            StringAssert.StartsWith("Khai thuế thu nhập cá nhân", detail.Name);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Mã thủ tục", "Cấp thực hiện", "Trình tự thực hiện", "Cách thức thực hiện", "Thành phần hồ sơ", "Đối tượng thực hiện",
                    "Cơ quan thực hiện", "Địa chỉ tiếp nhận hồ sơ", "Kết quả thực hiện", "Điều kiện thực hiện", "Căn cứ pháp lý"
                },
                System.Linq.Enumerable.Select(detail.Sections, s => s.Key));

            string steps = Section(detail, "Trình tự thực hiện");
            StringAssert.StartsWith("+ Bước 1. Cá nhân nộp hồ sơ", steps);
            StringAssert.Contains("+ Bước 2. Cơ quan thuế", steps);
            Assert.AreEqual(3, steps.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Length, "Mỗi đoạn p một dòng, đoạn p rỗng bị bỏ");
            Assert.AreEqual("Cấp xã", Section(detail, "Cấp thực hiện"));
            StringAssert.DoesNotContain("<", Section(detail, "Thành phần hồ sơ"));
            StringAssert.Contains("+++ Hộ chiếu", Section(detail, "Thành phần hồ sơ"));
        }

        [Test]
        public void T_DVC02_04_Dangerous_Content_NeverReachesOutput()
        {
            const string html =
                "<div class='card-body'><h4>Tên <script>alert('xss-title')</script>an toàn</h4>" +
                "<div class='mb-3'><label>Mã thủ tục:</label><div id='maTTHC'>1.000001</div></div>" +
                "<div class='mb-3'><label>Trình tự thực hiện:</label><div id='thuTuThucHien'>" +
                "<p onclick=\"steal()\">Bước 1<script>evil()</script></p>" +
                "<style>.x{background:url(javascript:bad())}</style>" +
                "<iframe src='http://evil.example/'>iframe-text</iframe>" +
                "<a href='javascript:bad()'>liên kết</a><img src='x' onerror='bad()'/>" +
                "<object data='x'>obj-text</object></div></div></div>";

            TthcDetail detail = TthcHtmlParser.ParseDetail(html);
            string all = detail.Name + "|" + string.Join("|", System.Linq.Enumerable.Select(detail.Sections, s => s.Key + "=" + s.Value));

            foreach (string forbidden in new[] { "alert", "xss-title", "steal", "evil", "javascript", "bad(", "iframe-text", "obj-text", "onerror", "<script", "evil.example" })
            {
                StringAssert.DoesNotContain(forbidden, all);
            }
            StringAssert.Contains("Bước 1", all);
            StringAssert.Contains("liên kết", all);
        }

        [Test]
        public void T_DVC02_05_WrongShape_Throws_NotSilentEmpty()
        {
            Assert.Throws<TthcParseException>(() => TthcHtmlParser.ParseList("<div>không có bảng</div>"));
            Assert.Throws<TthcParseException>(() => TthcHtmlParser.ParseList(""));
            Assert.Throws<TthcParseException>(() => TthcHtmlParser.ParseList(
                "<table><tbody><tr><td>1</td><td>1.0</td></tr></tbody></table>"), "Dòng thiếu cột");
            Assert.Throws<TthcParseException>(() => TthcHtmlParser.ParseList(
                "<table><tbody><tr><td>1</td><td>1.0</td><td>Tên</td><td>L</td><td>Cơ quan</td><td></td></tr></tbody></table>"), "Có dòng nhưng thiếu tổng");
            Assert.Throws<TthcParseException>(() => TthcHtmlParser.ParseDetail("<div class='card-body'><h4>Chỉ có tên</h4></div>"));
            Assert.Throws<TthcParseException>(() => TthcHtmlParser.ParseFilterOptions("<form></form>"));
        }

        private static string Section(TthcDetail detail, string key)
        {
            foreach (var s in detail.Sections) if (s.Key == key) return s.Value;
            Assert.Fail("Thiếu mục " + key);
            return null;
        }
    }
}
