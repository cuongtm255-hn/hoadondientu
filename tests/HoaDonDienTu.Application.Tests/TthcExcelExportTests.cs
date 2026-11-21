using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>FT-DVC-08: xuất danh mục TTHC ra Excel. Dữ liệu "(mẫu)", không gọi cổng thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class TthcExcelExportTests
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static XDocument ReadSheet(byte[] bytes)
        {
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            using (var stream = zip.GetEntry("xl/worksheets/sheet1.xml").Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static string Cell(XDocument sheet, string reference)
        {
            XElement c = sheet.Descendants(Ns + "c").FirstOrDefault(x => (string)x.Attribute("r") == reference);
            if (c == null) return null;
            XElement t = c.Descendants(Ns + "t").FirstOrDefault();
            if (t != null) return t.Value;
            XElement v = c.Element(Ns + "v");
            return v == null ? null : v.Value;
        }

        private static byte[] Export(IList<TthcSummary> items, TthcQuery filters, TthcFilterOptions options, string scope = "Trang 1/2")
        {
            using (var ms = new MemoryStream())
            {
                TthcExcelExport.Write(ms, items, filters, options, scope, new DateTime(2026, 11, 21, 15, 30, 0), null);
                return ms.ToArray();
            }
        }

        private static IList<TthcSummary> Items(int n)
        {
            var list = new List<TthcSummary>();
            for (int i = 1; i <= n; i++) list.Add(new TthcSummary("0." + (1000 + i), "Thủ tục (mẫu) " + i, "Toàn trình", "Cơ quan (mẫu)"));
            return list;
        }

        [Test]
        public void Package_HasRequiredParts_AndFrozenHeader()
        {
            byte[] bytes = Export(Items(3), null, null);

            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            {
                foreach (string name in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml" })
                {
                    Assert.IsNotNull(zip.GetEntry(name), name);
                }
            }
            XDocument sheet = ReadSheet(bytes);
            Assert.AreEqual("frozen", (string)sheet.Descendants(Ns + "pane").First().Attribute("state"));
        }

        [Test]
        public void Layout_TitleInfoLines_HeaderAndRows()
        {
            XDocument sheet = ReadSheet(Export(Items(3), null, null, "Trang 1/2"));

            Assert.AreEqual(TthcExcelExport.Title, Cell(sheet, "A1"));
            Assert.AreEqual("Phạm vi: Trang 1/2", Cell(sheet, "A2"));
            Assert.AreEqual("Bộ lọc: Không lọc", Cell(sheet, "A3"));
            Assert.AreEqual("Số bản ghi trong tệp: 3", Cell(sheet, "A4"));
            Assert.AreEqual("Thời điểm tra cứu: 21/11/2026 15:30", Cell(sheet, "A5"));
            // một dòng trống (6), tiêu đề cột ở dòng 7, dữ liệu từ dòng 8
            Assert.AreEqual("STT", Cell(sheet, "A7"));
            Assert.AreEqual("Mã", Cell(sheet, "B7"));
            Assert.AreEqual("Tên thủ tục", Cell(sheet, "C7"));
            Assert.AreEqual("Mức độ", Cell(sheet, "D7"));
            Assert.AreEqual("Cơ quan thực hiện", Cell(sheet, "E7"));
            Assert.AreEqual("1", Cell(sheet, "A8"));
            Assert.AreEqual("0.1001", Cell(sheet, "B8"));
            Assert.AreEqual("Thủ tục (mẫu) 3", Cell(sheet, "C10"));
            Assert.AreEqual("3", Cell(sheet, "A10"));
            Assert.IsNull(Cell(sheet, "A11"), "Không có dòng thừa");
        }

        [Test]
        public void CodesKeepLeadingZeros_AsText()
        {
            var items = new List<TthcSummary> { new TthcSummary("00123", "Tên", "Toàn trình", "Cơ quan") };

            XDocument sheet = ReadSheet(Export(items, null, null));

            Assert.AreEqual("00123", Cell(sheet, "B8"));
            XElement c = sheet.Descendants(Ns + "c").First(x => (string)x.Attribute("r") == "B8");
            Assert.AreEqual("inlineStr", (string)c.Attribute("t"));
        }

        [Test]
        public void SpecialAndControlCharacters_AreEscaped_AndFileStaysValidXml()
        {
            var items = new List<TthcSummary> { new TthcSummary("1.0", "A & B <c> \"d\" \u0001\u0008 hết", "Toàn trình", "Cơ quan\nXuống dòng") };

            XDocument sheet = ReadSheet(Export(items, null, null));

            Assert.AreEqual("A & B <c> \"d\"  hết", Cell(sheet, "C8"));
            StringAssert.Contains("Xuống dòng", Cell(sheet, "E8"));
        }

        [Test]
        public void FilterDescription_UsesLabels_NotCodes()
        {
            var options = new TthcFilterOptions(
                new[] { new TthcOption("012", "Hoàn thuế") }, new[] { new TthcOption("33", "Kết quả HĐSXKD") },
                new[] { new TthcOption("01", "Toàn trình") }, null);
            var filters = new TthcQuery { Keyword = "hóa đơn", ProcedureType = "012", Declaration = "33", Level = "01" };

            string text = TthcExcelExport.DescribeFilters(filters, options);

            StringAssert.Contains("Tên/Mã TTHC = \"hóa đơn\"", text);
            StringAssert.Contains("Loại TTHC = Hoàn thuế", text);
            StringAssert.Contains("Tờ khai = Kết quả HĐSXKD", text);
            StringAssert.Contains("Mức độ = Toàn trình", text);
            StringAssert.DoesNotContain("012", text);
        }

        [Test]
        public void FilterDescription_UnknownCode_FallsBackToCode_AndEmptyIsNoFilter()
        {
            Assert.AreEqual("Mức độ = 99", TthcExcelExport.DescribeFilters(new TthcQuery { Level = "99" }, TthcFilterOptions.Empty));
            Assert.AreEqual("Không lọc", TthcExcelExport.DescribeFilters(new TthcQuery(), TthcFilterOptions.Empty));
            Assert.AreEqual("Không lọc", TthcExcelExport.DescribeFilters(null, null));
        }

        [Test]
        public void EmptyList_StillWritesHeader_AndInfo()
        {
            XDocument sheet = ReadSheet(Export(new List<TthcSummary>(), null, null));

            Assert.AreEqual("STT", Cell(sheet, "A7"));
            Assert.AreEqual("Số bản ghi trong tệp: 0", Cell(sheet, "A4"));
            Assert.IsNull(Cell(sheet, "A8"));
        }

        [Test]
        public void OnRow_ReportsProgress_Cumulatively()
        {
            var seen = new List<int>();
            using (var ms = new MemoryStream())
            {
                TthcExcelExport.Write(ms, Items(4), null, null, "x", DateTime.Now, n => seen.Add(n));
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, seen);
        }

        [Test]
        public void SuggestedFileName_HasTimestamp()
        {
            Assert.AreEqual("DanhMucTTHC-20261121-1530.xlsx", TthcExcelExport.SuggestedFileName(new DateTime(2026, 11, 21, 15, 30, 59)));
        }

        // ---- Tải tất cả các trang (TthcSearchSession.FetchAllPagesAsync) ----

        private sealed class PagingSource : ITthcSource
        {
            public readonly List<TthcQuery> Calls = new List<TthcQuery>();
            public int PageCount = 3;
            public int FailOnPage = -1;
            public int Concurrent, MaxConcurrent;

            public Task<TthcFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(TthcFilterOptions.Empty);
            }

            public async Task<TthcPage> SearchAsync(TthcQuery query, CancellationToken cancellationToken)
            {
                int now = Interlocked.Increment(ref Concurrent);
                lock (Calls) { if (now > MaxConcurrent) MaxConcurrent = now; Calls.Add(query); }
                await Task.Delay(5, cancellationToken);
                Interlocked.Decrement(ref Concurrent);
                if (query.PageNumber == FailOnPage) throw new InvalidOperationException("boom");
                var items = new List<TthcSummary> { new TthcSummary("p" + query.PageNumber + ".1", "Tên " + query.PageNumber, "Toàn trình", "CQ") };
                return new TthcPage(items, PageCount, PageCount, query.PageNumber, PageCount);
            }

            public Task<TthcDetail> GetDetailAsync(string code, CancellationToken cancellationToken)
            {
                return Task.FromResult(new TthcDetail(code, "x", null));
            }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private static TthcSearchSession NewSession(PagingSource source)
        {
            return new TthcSearchSession(new TthcService(source, null, new NullLogger()), pageDelayMs: 0);
        }

        [Test]
        public async Task FetchAll_ReadsEveryPage_InOrder_WithAppliedFilters_AndLeavesVisiblePageAlone()
        {
            var source = new PagingSource { PageCount = 4 };
            TthcSearchSession session = NewSession(source);
            await session.SearchAsync(new TthcQuery { Keyword = "đã áp dụng", Level = "01" }, CancellationToken.None);
            TthcPage visible = session.Page;
            source.Calls.Clear();
            var progress = new List<int>();

            IList<TthcSummary> all = await session.FetchAllPagesAsync(new Progress<int>(progress.Add), CancellationToken.None);

            Assert.AreEqual(4, all.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, source.Calls.Select(c => c.PageNumber));
            Assert.IsTrue(source.Calls.All(c => c.Keyword == "đã áp dụng" && c.Level == "01"));
            Assert.AreEqual(1, source.MaxConcurrent, "Tuần tự, không song song");
            Assert.AreSame(visible, session.Page, "Trang đang xem không đổi");
        }

        [Test]
        public async Task FetchAll_FailureOnAnyPage_ReturnsNull_NoPartialResult()
        {
            var source = new PagingSource { PageCount = 4, FailOnPage = 3 };
            TthcSearchSession session = NewSession(source);
            source.FailOnPage = -1;
            await session.SearchAsync(new TthcQuery(), CancellationToken.None);
            source.FailOnPage = 3;

            IList<TthcSummary> all = await session.FetchAllPagesAsync(null, CancellationToken.None);

            Assert.IsNull(all);
            Assert.AreEqual(TthcFailure.Network, session.LastFailure);
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public async Task FetchAll_BeforeAnySearch_ReturnsNull()
        {
            TthcSearchSession session = NewSession(new PagingSource());

            Assert.IsNull(await session.FetchAllPagesAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task FetchAll_Cancelled_Throws_AndSessionIsNotLeftBusy()
        {
            var source = new PagingSource { PageCount = 5 };
            TthcSearchSession session = NewSession(source);
            await session.SearchAsync(new TthcQuery(), CancellationToken.None);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.CatchAsync<OperationCanceledException>(() => session.FetchAllPagesAsync(null, cts.Token));
            }

            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public async Task AppliedFilters_AreCopy_WithoutPageNumber()
        {
            TthcSearchSession session = NewSession(new PagingSource());
            Assert.IsNull(session.AppliedFilters);

            await session.SearchAsync(new TthcQuery { Keyword = "k", PageNumber = 9 }, CancellationToken.None);
            TthcQuery applied = session.AppliedFilters;

            Assert.AreEqual("k", applied.Keyword);
            Assert.AreEqual(0, applied.PageNumber);
            applied.Keyword = "sửa";
            Assert.AreEqual("k", session.AppliedFilters.Keyword, "Sửa bản sao không ảnh hưởng bộ lọc đã áp dụng");
        }
    }
}
