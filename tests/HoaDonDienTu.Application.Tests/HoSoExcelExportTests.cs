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
    /// <summary>FT-DVC-08 cho hồ sơ đã nộp. Dữ liệu "mẫu", nguồn giả; không gọi cổng thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class HoSoExcelExportTests
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly string[] Columns = { "Mã hồ sơ", "Tên TTHC", "Kỳ tính thuế", "Ngày nộp", "Trạng thái" };

        private static string Cell(byte[] bytes, string reference)
        {
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            using (var stream = zip.GetEntry("xl/worksheets/sheet1.xml").Open())
            {
                XDocument sheet = XDocument.Load(stream);
                XElement c = sheet.Descendants(Ns + "c").FirstOrDefault(x => (string)x.Attribute("r") == reference);
                if (c == null) return null;
                XElement t = c.Descendants(Ns + "t").FirstOrDefault();
                if (t != null) return t.Value;
                XElement v = c.Element(Ns + "v");
                return v == null ? null : v.Value;
            }
        }

        private static HoSoQuery Filters()
        {
            return new HoSoQuery
            {
                Source = HoSoSource.Dvc,
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 1, 31),
                ProcedureName = "1.007674",
                FileCode = "HS-MAU-0001",
                AuthorizedTaxCode = "0109999999",
                CaptchaText = "khongduocghi"
            };
        }

        private static IList<HoSoRow> Rows(int n)
        {
            var rows = new List<HoSoRow>();
            for (int i = 1; i <= n; i++) rows.Add(new HoSoRow(new[] { "HS-MAU-" + i, "TTHC mẫu " + i, "Q4/2025", "03/01/2026 15:45", "Đã chấp nhận" }));
            return rows;
        }

        private static byte[] Export(IList<HoSoRow> rows, HoSoQuery filters, string scope = "Trang 1/1")
        {
            using (var ms = new MemoryStream())
            {
                HoSoExcelExport.Write(ms, Columns, rows, filters, scope, new DateTime(2026, 11, 21, 16, 5, 0), null);
                return ms.ToArray();
            }
        }

        [Test]
        public void Layout_TitleInfoLines_DynamicColumns_AndRows()
        {
            byte[] bytes = Export(Rows(2), Filters(), "Trang 1/1");

            Assert.AreEqual(HoSoExcelExport.Title, Cell(bytes, "A1"));
            Assert.AreEqual("Nguồn: Hồ sơ đã nộp trên Dịch vụ công", Cell(bytes, "A2"));
            Assert.AreEqual("Phạm vi: Trang 1/1", Cell(bytes, "A3"));
            Assert.AreEqual("Khoảng ngày: từ 01/01/2026 đến 31/01/2026", Cell(bytes, "A4"));
            StringAssert.Contains("Tên TTHC = \"1.007674\"", Cell(bytes, "A5"));
            StringAssert.Contains("Mã hồ sơ = \"HS-MAU-0001\"", Cell(bytes, "A5"));
            Assert.AreEqual("Số bản ghi trong tệp: 2", Cell(bytes, "A6"));
            Assert.AreEqual("Thời điểm tra cứu: 21/11/2026 16:05", Cell(bytes, "A7"));
            // dòng trống 8; tiêu đề cột ở dòng 9; dữ liệu từ dòng 10
            Assert.AreEqual("STT", Cell(bytes, "A9"));
            Assert.AreEqual("Mã hồ sơ", Cell(bytes, "B9"));
            Assert.AreEqual("Trạng thái", Cell(bytes, "F9"));
            Assert.AreEqual("1", Cell(bytes, "A10"));
            Assert.AreEqual("HS-MAU-1", Cell(bytes, "B10"));
            Assert.AreEqual("TTHC mẫu 2", Cell(bytes, "C11"));
            Assert.IsNull(Cell(bytes, "A12"));
        }

        [Test]
        public void CaptchaNeverReachesTheFile_AndETaxSourceIsLabelled()
        {
            var f = Filters();
            f.Source = HoSoSource.ThueDienTu;

            byte[] bytes = Export(Rows(1), f);

            Assert.AreEqual("Nguồn: Hồ sơ đã nộp trên thuế điện tử", Cell(bytes, "A2"));
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            foreach (var entry in zip.Entries)
            using (var reader = new StreamReader(entry.Open()))
            {
                StringAssert.DoesNotContain("khongduocghi", reader.ReadToEnd());
            }
        }

        [Test]
        public void NoFilters_And_EmptyRows_StillWriteValidFile()
        {
            byte[] bytes = Export(new List<HoSoRow>(), null);

            Assert.AreEqual("Bộ lọc khác: Không rõ", Cell(bytes, "A5"));
            Assert.AreEqual("Khoảng ngày: không rõ", Cell(bytes, "A4"));
            Assert.AreEqual("STT", Cell(bytes, "A9"));
            Assert.IsNull(Cell(bytes, "A10"));
        }

        [Test]
        public void ControlCharactersAndSpecials_AreEscaped()
        {
            var rows = new List<HoSoRow> { new HoSoRow(new[] { "A & B", "<x>\u0001", "Q1", "d", "t" }) };

            byte[] bytes = Export(rows, null);

            Assert.AreEqual("A & B", Cell(bytes, "B10"));
            Assert.AreEqual("<x>", Cell(bytes, "C10"));
        }

        [Test]
        public void FileName_HasTimestamp_AndWidthsAreSensible()
        {
            Assert.AreEqual("HoSoDaNop-20261121-1605.xlsx", HoSoExcelExport.SuggestedFileName(new DateTime(2026, 11, 21, 16, 5, 59)));
            Assert.Greater(HoSoExcelExport.WidthFor("Tên TTHC"), HoSoExcelExport.WidthFor("Kỳ tính thuế"));
        }

        // ---------------- Tải tất cả các trang ----------------

        private sealed class Source : IHoSoSource
        {
            public readonly List<HoSoQuery> Calls = new List<HoSoQuery>();
            public int PageCount = 3;
            public int FailOnPage = -1;
            public HoSoFailure FailWith = HoSoFailure.WrongCaptcha;
            public int Concurrent, MaxConcurrent;

            public Task<byte[]> GetCaptchaAsync(HoSoSource source, CancellationToken cancellationToken)
            {
                return Task.FromResult(new byte[] { 1, 2, 3 });
            }

            public async Task<HoSoPage> SearchAsync(HoSoQuery query, CancellationToken cancellationToken)
            {
                int now = Interlocked.Increment(ref Concurrent);
                lock (Calls) { if (now > MaxConcurrent) MaxConcurrent = now; Calls.Add(query); }
                await Task.Delay(3, cancellationToken);
                Interlocked.Decrement(ref Concurrent);
                if (query.PageNumber == FailOnPage) throw new HoSoServiceException(FailWith);
                return new HoSoPage(Columns, new[] { new HoSoRow(new[] { "HS-P" + query.PageNumber, "t", "q", "d", "s" }) }, PageCount, query.PageNumber, PageCount, null);
            }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private static async Task<HoSoSearchSession> Searched(Source source)
        {
            var dvc = new DvcSession();
            dvc.SignedIn("u", "Tổ chức mẫu");
            var session = new HoSoSearchSession(new HoSoService(source, dvc, new NullLogger()), pageDelayMs: 0);
            await session.SelectSourceAsync(HoSoSource.Dvc, CancellationToken.None);
            await session.SearchAsync(new HoSoQuery { FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 1, 31), CaptchaText = "ma123", ProcedureName = "đã áp dụng" }, CancellationToken.None);
            source.Calls.Clear();
            return session;
        }

        [Test]
        public async Task FetchAll_SinglePage_ReturnsCurrentRows_WithoutCallingThePortal()
        {
            var source = new Source { PageCount = 1 };
            HoSoSearchSession session = await Searched(source);

            IList<HoSoRow> rows = await session.FetchAllPagesAsync(null, null, CancellationToken.None);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(0, source.Calls.Count, "Một trang: không cần mã và không gọi cổng");
        }

        [Test]
        public async Task FetchAll_ManyPages_IsSequential_ReusesTheSearchCaptcha_AndKeepsVisiblePage()
        {
            var source = new Source { PageCount = 4 };
            HoSoSearchSession session = await Searched(source);
            HoSoPage visible = session.Page;
            var progress = new List<int>();

            IList<HoSoRow> rows = await session.FetchAllPagesAsync(null, new Progress<int>(progress.Add), CancellationToken.None);

            Assert.AreEqual(4, rows.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, source.Calls.Select(c => c.PageNumber));
            Assert.IsTrue(source.Calls.All(c => c.CaptchaText == "ma123" && c.ProcedureName == "đã áp dụng"));
            Assert.AreEqual(1, source.MaxConcurrent);
            Assert.AreSame(visible, session.Page);
            Assert.IsTrue(session.CanPageWithoutNewCaptcha);
        }

        [Test]
        public async Task FetchAll_FailureOnAnyPage_ReturnsNull_ForgetsCaptcha_AndAsksForANewOne()
        {
            var source = new Source { PageCount = 4, FailOnPage = 3 };
            HoSoSearchSession session = await Searched(source);
            source.FailOnPage = 3;

            IList<HoSoRow> rows = await session.FetchAllPagesAsync(null, null, CancellationToken.None);

            Assert.IsNull(rows, "Không có kết quả dở dang");
            Assert.AreEqual(HoSoFailure.WrongCaptcha, session.LastFailure);
            Assert.IsFalse(session.CanPageWithoutNewCaptcha);
            Assert.IsFalse(session.IsBusy);

            // Gõ mã mới rồi xuất lại: dùng mã mới cho mọi trang.
            source.FailOnPage = -1;
            source.Calls.Clear();
            rows = await session.FetchAllPagesAsync("moi456", null, CancellationToken.None);
            Assert.AreEqual(4, rows.Count);
            Assert.IsTrue(source.Calls.All(c => c.CaptchaText == "moi456"));
            Assert.IsTrue(session.CanPageWithoutNewCaptcha);
        }

        [Test]
        public async Task FetchAll_WithoutAnyCaptcha_IsRefused_BeforeCallingThePortal()
        {
            var source = new Source { PageCount = 3 };
            HoSoSearchSession session = await Searched(source);
            session.ClearPagingCaptcha();

            IList<HoSoRow> rows = await session.FetchAllPagesAsync(null, null, CancellationToken.None);

            Assert.IsNull(rows);
            Assert.AreEqual(HoSoFailure.InvalidInput, session.LastFailure);
            Assert.AreEqual(0, source.Calls.Count);
        }

        [Test]
        public async Task FetchAll_BeforeAnySearch_ReturnsNull_AndCancellationLeavesSessionIdle()
        {
            var dvc = new DvcSession();
            dvc.SignedIn("u", "x");
            var fresh = new HoSoSearchSession(new HoSoService(new Source(), dvc, new NullLogger()), 0);
            Assert.IsNull(await fresh.FetchAllPagesAsync(null, null, CancellationToken.None));

            var source = new Source { PageCount = 5 };
            HoSoSearchSession session = await Searched(source);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.CatchAsync<OperationCanceledException>(() => session.FetchAllPagesAsync(null, null, cts.Token));
            }
            Assert.IsFalse(session.IsBusy);
        }

        [Test]
        public async Task AppliedFilters_AreACopy_WithoutCaptchaOrPage()
        {
            var source = new Source();
            HoSoSearchSession session = await Searched(source);

            HoSoQuery applied = session.AppliedFilters;

            Assert.AreEqual("đã áp dụng", applied.ProcedureName);
            Assert.IsNull(applied.CaptchaText);
            Assert.AreEqual(0, applied.PageNumber);
            applied.ProcedureName = "sửa";
            Assert.AreEqual("đã áp dụng", session.AppliedFilters.ProcedureName);
        }
    }
}
