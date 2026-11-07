using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Exports;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class InvoiceExportServiceTests
    {
        private const string Xml = "<HDon><DLHDon><TTChung><SHDon>{0}</SHDon></TTChung></DLHDon></HDon>";
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private string _dir;
        private InMemoryInvoiceRepository _repo;
        private FakeFiles _files;
        private FakeContext _context;
        private FakeEntitlement _entitlement;
        private InvoiceExportService _service;
        private InMemoryExportHistoryRepository _history;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _repo = new InMemoryInvoiceRepository();
            _files = new FakeFiles();
            _context = new FakeContext { Current = new CompanyContext(CompanyId.NewId(), Mst, "Công ty mẫu", @"C:\Kho", null) };
            _entitlement = new FakeEntitlement();
            var archive = new InvoiceArchiveService(_repo, _files, _context);
            _history = new InMemoryExportHistoryRepository();
            _service = new InvoiceExportService(_repo, archive, _files, _context, _entitlement, _history);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }

        private async Task<StoredInvoice> AddAsync(
            string number, DateTime issue, InvoiceDirection direction = InvoiceDirection.Purchase, bool present = true, string content = null,
            Action<NormalizedInvoice> tweak = null, int versions = 1)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = direction, InvoiceNumber = number, IssueDate = issue, FormSymbol = "1", InvoiceSymbol = "C26TAA",
                SellerTaxCode = "0109988776", SellerName = "Công ty ABC", BuyerTaxCode = "0101234567", BuyerName = "Công ty TNHH Mẫu", BuyerAddress = "Hà Nội",
                StatusRaw = "1", CheckResultRaw = "5", TotalBeforeTax = 30000000m, TotalTax = 3000000m, TotalPayment = 33000000m, Currency = "VND"
            };
            if (tweak != null) tweak(invoice);

            var list = new List<InvoiceVersionRecord>();
            for (var v = 1; v <= versions; v++)
            {
                var bytes = Encoding.UTF8.GetBytes(content ?? string.Format(Xml, number + "-v" + v));
                var path = @"X\" + direction + number + "_v" + v + ".xml";
                list.Add(new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = v, Sha256 = Sha(bytes), RelativePath = path, IsCurrent = v == versions, SizeBytes = bytes.Length });
                if (present) _files.Put(path, bytes);
            }

            var stored = new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = list,
                FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
            };
            await _repo.AddAsync(stored, CancellationToken.None);
            return stored;
        }

        private ExportRequest Req(ExportScope scope, string name, bool overwrite = false)
        {
            return new ExportRequest { Scope = scope, TargetPath = Path.Combine(_dir, name), Overwrite = overwrite };
        }

        private static ExportScope All(InvoiceDirection direction = InvoiceDirection.Purchase)
        {
            return ExportScope.ForQuery(new ArchiveQuery { Direction = direction, SortKey = ArchiveSortKey.InvoiceNumber, SortDescending = false });
        }

        // ---- Dùng file zip của cổng đã lưu ----

        private static byte[] PortalZip(params KeyValuePair<string, byte[]>[] entries)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var e in entries)
                    {
                        var entry = zip.CreateEntry(e.Key);
                        using (var s = entry.Open()) s.Write(e.Value, 0, e.Value.Length);
                    }
                }

                return ms.ToArray();
            }
        }

        private static KeyValuePair<string, byte[]> Entry(string name, string text)
        {
            return new KeyValuePair<string, byte[]>(name, Encoding.UTF8.GetBytes(text));
        }

        private static IDictionary<string, string> ReadZipTexts(string path)
        {
            var result = new Dictionary<string, string>();
            using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
            {
                foreach (var e in zip.Entries)
                {
                    using (var r = new StreamReader(e.Open(), Encoding.UTF8)) result[e.FullName] = r.ReadToEnd();
                }
            }

            return result;
        }

        [Test]
        public async Task StoredPortalPackage_IsExportedAsIs_SoThePortalHtmlIsKept()
        {
            var stored = await AddAsync("40", new DateTime(2026, 10, 1));
            var xml = Encoding.UTF8.GetString(_files.Read(null, stored.Current.RelativePath, long.MaxValue));
            _files.Put(InvoiceStorageService.PackagePathFor(stored.Current.RelativePath), PortalZip(Entry("invoice.xml", xml), Entry("invoice.html", "<html>CỔNG</html>"), Entry("details.js", "js")));

            await _service.ExportZipAsync(Req(All(), "p.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            var texts = ReadZipTexts(Path.Combine(_dir, "p.zip"));
            Assert.That(texts["invoice.html"], Is.EqualTo("<html>CỔNG</html>"), "Trang xem là của cổng, không dựng lại");
            Assert.That(texts["invoice.xml"], Is.EqualTo(xml));
            Assert.That(texts.ContainsKey("details.js"), Is.True);
        }

        [Test]
        public async Task SeveralInvoices_EachGetTheirOwnFolderWithTheirPortalFiles()
        {
            var a = await AddAsync("41", new DateTime(2026, 10, 1));
            var b = await AddAsync("42", new DateTime(2026, 10, 2));
            foreach (var s in new[] { a, b })
            {
                var xml = Encoding.UTF8.GetString(_files.Read(null, s.Current.RelativePath, long.MaxValue));
                _files.Put(InvoiceStorageService.PackagePathFor(s.Current.RelativePath), PortalZip(Entry("invoice.xml", xml), Entry("invoice.html", "<html>" + s.Invoice.InvoiceNumber + "</html>")));
            }

            await _service.ExportZipAsync(Req(All(), "n.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            var texts = ReadZipTexts(Path.Combine(_dir, "n.zip"));
            Assert.That(texts.Count(t => t.Key.EndsWith("/invoice.html")), Is.EqualTo(2));
            Assert.That(texts.Values, Does.Contain("<html>41</html>"));
            Assert.That(texts.Values, Does.Contain("<html>42</html>"));
            Assert.That(texts.ContainsKey(InvoiceExportService.ManifestName), Is.True);
        }

        [Test]
        public async Task APackageWhoseXmlDiffersFromTheStoredXml_IsNotTrusted_AndTheViewerIsRebuilt()
        {
            var stored = await AddAsync("43", new DateTime(2026, 10, 1));
            _files.Put(InvoiceStorageService.PackagePathFor(stored.Current.RelativePath), PortalZip(Entry("invoice.xml", "<HDon>khác</HDon>"), Entry("invoice.html", "<html>GIẢ</html>")));

            await _service.ExportZipAsync(Req(All(), "d.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            var texts = ReadZipTexts(Path.Combine(_dir, "d.zip"));
            Assert.That(texts.Values, Does.Not.Contain("<html>GIẢ</html>"));
            Assert.That(texts["invoice.xml"], Does.Contain("43-v1"), "Vẫn xuất đúng XML đã lưu");
        }

        [Test]
        public async Task ACorruptPackage_FallsBackToTheRebuiltViewer_InsteadOfFailingTheExport()
        {
            var stored = await AddAsync("44", new DateTime(2026, 10, 1));
            _files.Put(InvoiceStorageService.PackagePathFor(stored.Current.RelativePath), new byte[] { 1, 2, 3, 4 });

            var result = await _service.ExportZipAsync(Req(All(), "c.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(1));
            Assert.That(ReadZipTexts(Path.Combine(_dir, "c.zip")).ContainsKey("invoice.xml"), Is.True);
        }

        [Test]
        public async Task EntryNamesInThePackage_CannotEscapeTheInvoiceFolder()
        {
            var stored = await AddAsync("45", new DateTime(2026, 10, 1));
            var xml = Encoding.UTF8.GetString(_files.Read(null, stored.Current.RelativePath, long.MaxValue));
            _files.Put(InvoiceStorageService.PackagePathFor(stored.Current.RelativePath), PortalZip(Entry("invoice.xml", xml), Entry("../../evil.html", "x")));

            await _service.ExportZipAsync(Req(All(), "s.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            Assert.That(ReadZipTexts(Path.Combine(_dir, "s.zip")).Keys.Any(k => k.Contains("..")), Is.False);
        }

        // ---- Đọc lại file xuất ----

        private sealed class Cell
        {
            public string Type;
            public string Text;
            public string Style;
        }

        private static Dictionary<string, Cell> ReadSheet(string path, out List<string> merges)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                var doc = new XmlDocument();
                using (var s = zip.GetEntry("xl/worksheets/sheet1.xml").Open()) doc.Load(s);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("m", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                var cells = new Dictionary<string, Cell>();
                foreach (XmlElement c in doc.SelectNodes("//m:c", ns))
                {
                    var v = c.SelectSingleNode("m:v", ns);
                    var t = c.SelectSingleNode("m:is/m:t", ns);
                    cells[c.GetAttribute("r")] = new Cell { Type = c.GetAttribute("t"), Text = t != null ? t.InnerText : v != null ? v.InnerText : null, Style = c.GetAttribute("s") };
                }

                merges = doc.SelectNodes("//m:mergeCell", ns).Cast<XmlElement>().Select(m => m.GetAttribute("ref")).ToList();
                return cells;
            }
        }

        /// <summary>Năm file của gói xem hóa đơn (như gói "Xuất xml" của cổng) trong một thư mục của ZIP.</summary>
        private static string[] ViewerEntries(string folder)
        {
            return new[] { "invoice.xml", "invoice.html", "details.js", "sign-check.jpg", "viewinvoice-bg.jpg" }.Select(n => folder + n).ToArray();
        }

        private static Dictionary<string, byte[]> ReadZip(string path)
        {
            var result = new Dictionary<string, byte[]>();
            using (var zip = ZipFile.OpenRead(path))
            {
                foreach (var e in zip.Entries)
                {
                    using (var s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        result[e.FullName] = ms.ToArray();
                    }
                }
            }

            return result;
        }

        // ---- Excel ----

        [Test]
        public async Task Excel_FollowsThePortalLayout_19Columns_TitleRangeAndData()
        {
            await AddAsync("0000128", new DateTime(2026, 10, 31));
            await AddAsync("0000127", new DateTime(2026, 10, 30), tweak: i => i.TotalDiscount = 500m);
            var request = Req(ExportScope.ForQuery(new ArchiveQuery
            {
                Direction = InvoiceDirection.Purchase, IssueFrom = new DateTime(2026, 1, 1), IssueTo = new DateTime(2026, 12, 31), SortKey = ArchiveSortKey.InvoiceNumber, SortDescending = false
            }), "a.xlsx");

            var result = await _service.ExportExcelAsync(request, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(2));
            List<string> merges;
            var cells = ReadSheet(request.TargetPath, out merges);
            Assert.That(cells["A3"].Text, Is.EqualTo("DANH SÁCH HÓA ĐƠN"));
            Assert.That(cells["A4"].Text, Is.EqualTo("Từ ngày 01/01/2026 đến ngày 31/12/2026"));
            Assert.That(merges, Is.EquivalentTo(new[] { "A2:S2", "A3:S3", "A4:S4" }));
            for (var c = 0; c < InvoiceExcelWriter.Columns.Count; c++)
            {
                Assert.That(cells[InvoiceExcelWriter.ColumnName(c + 1) + "6"].Text, Is.EqualTo(InvoiceExcelWriter.Columns[c].Title));
            }

            Assert.That(InvoiceExcelWriter.Columns.Count, Is.EqualTo(19));
            Assert.That(InvoiceExcelWriter.ColumnName(19), Is.EqualTo("S"));
            Assert.That(cells["A7"].Text, Is.EqualTo("1"));
            Assert.That(cells["D7"].Text, Is.EqualTo("0000127"), "Sắp xếp theo số hóa đơn tăng dần");
            Assert.That(cells["D8"].Text, Is.EqualTo("0000128"));
            Assert.That(cells["A8"].Text, Is.EqualTo("2"), "STT đánh lại theo thứ tự trong file");
        }

        [Test]
        public async Task Excel_KeepsLeadingZerosAsText_AndMoneyAndDatesAsNumbers()
        {
            await AddAsync("0000128", new DateTime(2026, 10, 31), tweak: i => { i.ExchangeRate = 25400.5m; i.Currency = "USD"; });
            var request = Req(All(), "b.xlsx");

            await _service.ExportExcelAsync(request, null, CancellationToken.None);

            List<string> merges;
            var cells = ReadSheet(request.TargetPath, out merges);
            Assert.That(cells["B7"].Type, Is.EqualTo("inlineStr"));
            Assert.That(cells["B7"].Text, Is.EqualTo("1"));
            Assert.That(cells["D7"].Type, Is.EqualTo("inlineStr"));
            Assert.That(cells["D7"].Text, Is.EqualTo("0000128"));
            Assert.That(cells["F7"].Text, Is.EqualTo("0109988776"));
            Assert.That(cells["E7"].Type, Is.Empty, "Ngày là số, không phải chữ");
            Assert.That(cells["E7"].Text, Is.EqualTo(((new DateTime(2026, 10, 31) - new DateTime(1899, 12, 30)).Days).ToString()));
            Assert.That(cells["K7"].Text, Is.EqualTo("30000000"));
            Assert.That(cells["L7"].Text, Is.EqualTo("3000000"));
            Assert.That(cells["O7"].Text, Is.EqualTo("33000000"));
            Assert.That(cells["Q7"].Text, Is.EqualTo("25400.5"));
            Assert.That(cells["P7"].Text, Is.EqualTo("USD"));
            Assert.That(cells["M7"].Text, Is.Null, "Chiết khấu trống khi cổng không có");
            Assert.That(cells["N7"].Text, Is.Null);
        }

        [Test]
        public async Task Excel_StatusColumnsUsePortalLabels_AndAnIndividualBuyerShowsTheIdNumber()
        {
            await AddAsync("1", new DateTime(2026, 10, 31), tweak: i =>
            {
                i.BuyerTaxCode = null;
                i.BuyerIdNumber = "001234567890";
                i.StatusRaw = "6";
                i.CheckResultRaw = "8";
            });
            var request = Req(All(), "c.xlsx");

            await _service.ExportExcelAsync(request, null, CancellationToken.None);

            List<string> merges;
            var cells = ReadSheet(request.TargetPath, out merges);
            Assert.That(cells["H7"].Text, Is.EqualTo("001234567890"));
            Assert.That(cells["R7"].Text, Is.EqualTo(HoaDonDienTu.Application.Portal.PortalOptions.StatusLabel("6")));
            Assert.That(cells["S7"].Text, Is.EqualTo(HoaDonDienTu.Application.Portal.PortalOptions.CheckResultLabel("8")));
            Assert.That(cells["R7"].Text, Is.Not.EqualTo("6"));
        }

        [Test]
        public async Task Excel_EscapesXmlAndDropsInvalidControlCharacters_KeepingVietnamese()
        {
            await AddAsync("1", new DateTime(2026, 10, 31), tweak: i => i.SellerName = "Tiệm \"Á & Â\" <Đông> \u0001 \u0002 ");
            var request = Req(All(), "d.xlsx");

            await _service.ExportExcelAsync(request, null, CancellationToken.None);

            List<string> merges;
            var cells = ReadSheet(request.TargetPath, out merges);
            Assert.That(cells["G7"].Text, Is.EqualTo("Tiệm \"Á & Â\" <Đông>   "), "Ký tự điều khiển bị bỏ, khoảng trắng đầu/cuối được giữ");
        }

        [Test]
        public async Task Excel_AllPartsAreWellFormedXml_AndRelationshipsPointAtExistingParts()
        {
            await AddAsync("1", new DateTime(2026, 10, 31));
            var request = Req(All(), "e.xlsx");

            await _service.ExportExcelAsync(request, null, CancellationToken.None);

            var parts = ReadZip(request.TargetPath);
            foreach (var part in parts)
            {
                var doc = new XmlDocument();
                Assert.DoesNotThrow(() => doc.LoadXml(new UTF8Encoding(false).GetString(part.Value)), part.Key);
            }

            Assert.That(parts.Keys, Is.SupersetOf(new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml" }));
            var rels = new XmlDocument();
            rels.LoadXml(Encoding.UTF8.GetString(parts["xl/_rels/workbook.xml.rels"]));
            foreach (XmlElement r in rels.DocumentElement.ChildNodes)
            {
                Assert.That(parts.ContainsKey("xl/" + r.GetAttribute("Target")), Is.True, r.GetAttribute("Target"));
            }

            var types = new XmlDocument();
            types.LoadXml(Encoding.UTF8.GetString(parts["[Content_Types].xml"]));
            Assert.That(types.OuterXml, Does.Contain("/xl/worksheets/sheet1.xml"));
        }

        [Test]
        public async Task Excel_SelectionKeepsTheGivenOrder_IgnoresUnknownIds_AndSaysSoInTheHeader()
        {
            var a = await AddAsync("1", new DateTime(2026, 3, 1));
            var b = await AddAsync("2", new DateTime(2026, 5, 9));
            await AddAsync("3", new DateTime(2026, 8, 1));
            var request = Req(ExportScope.ForSelection(new[] { b.InvoiceId, Guid.NewGuid(), a.InvoiceId, b.InvoiceId }), "f.xlsx");

            var result = await _service.ExportExcelAsync(request, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(2));
            List<string> merges;
            var cells = ReadSheet(request.TargetPath, out merges);
            Assert.That(cells["D7"].Text, Is.EqualTo("2"));
            Assert.That(cells["D8"].Text, Is.EqualTo("1"));
            Assert.That(cells["A2"].Text, Does.Contain("đã chọn"));
            Assert.That(cells["A4"].Text, Is.EqualTo("Từ ngày 01/03/2026 đến ngày 09/05/2026"), "Khoảng ngày lấy từ chính các hóa đơn xuất");
        }

        [Test]
        public async Task Excel_QueryScopeIgnoresPaging_AndOtherDirections()
        {
            for (var i = 1; i <= 120; i++) await AddAsync(i.ToString("0000"), new DateTime(2026, 1, 1).AddDays(i % 28));
            await AddAsync("9999", new DateTime(2026, 1, 1), direction: InvoiceDirection.Sold);
            var query = new ArchiveQuery { Direction = InvoiceDirection.Purchase, PageIndex = 2, PageSize = 50 };
            var request = Req(ExportScope.ForQuery(query), "g.xlsx");

            var result = await _service.ExportExcelAsync(request, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(120));
            List<string> merges;
            var cells = ReadSheet(request.TargetPath, out merges);
            Assert.That(cells.ContainsKey("A126"), Is.True);
            Assert.That(cells.ContainsKey("A127"), Is.False);
            Assert.That(cells["A2"].Text, Does.Contain("toàn bộ kết quả"));
        }

        [Test]
        public async Task Excel_ReportsProgressForEveryRow()
        {
            for (var i = 1; i <= 5; i++) await AddAsync(i.ToString(), new DateTime(2026, 1, i));
            var seen = new List<ExportProgress>();

            await _service.ExportExcelAsync(Req(All(), "h.xlsx"), new SyncProgress(seen), CancellationToken.None);

            Assert.That(seen.Select(p => p.Done), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(seen.All(p => p.Total == 5), Is.True);
        }

        // ---- Chung: ghi đè, hủy, quyền ----

        [Test]
        public async Task ExistingTarget_IsNotOverwrittenWithoutConfirmation()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            var request = Req(All(), "i.xlsx");
            File.WriteAllText(request.TargetPath, "dữ liệu cũ");

            var ex = Assert.ThrowsAsync<ExportTargetExistsException>(() => _service.ExportExcelAsync(request, null, CancellationToken.None));

            Assert.That(ex.Path, Is.EqualTo(Path.GetFullPath(request.TargetPath)));
            Assert.That(File.ReadAllText(request.TargetPath), Is.EqualTo("dữ liệu cũ"));
            Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(1), "Không để lại file tạm");
        }

        [Test]
        public async Task ConfirmedOverwrite_ReplacesTheFile_AndLeavesNoTempFile()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            var request = Req(All(), "j.xlsx", overwrite: true);
            File.WriteAllText(request.TargetPath, "dữ liệu cũ");

            await _service.ExportExcelAsync(request, null, CancellationToken.None);

            List<string> merges;
            Assert.That(ReadSheet(request.TargetPath, out merges)["A3"].Text, Is.EqualTo("DANH SÁCH HÓA ĐƠN"));
            Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(1));
        }

        [Test]
        public async Task Cancelling_LeavesNeitherTheTargetNorATempFile()
        {
            for (var i = 1; i <= 10; i++) await AddAsync(i.ToString(), new DateTime(2026, 1, i));
            var cts = new CancellationTokenSource();
            var request = Req(All(), "k.xlsx");

            Assert.ThrowsAsync<OperationCanceledException>(() => _service.ExportExcelAsync(request, new CallbackProgress(p => { if (p.Done == 3) cts.Cancel(); }), cts.Token));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public async Task LockedTarget_KeepsTheOldFileIntact_AndLeavesNoTempFile()
        {
            // Người dùng đang mở file cũ bằng Excel: ghi đè thất bại thì file cũ phải còn nguyên.
            await AddAsync("1", new DateTime(2026, 1, 1));
            var request = Req(All(), "khoa.xlsx", overwrite: true);
            File.WriteAllText(request.TargetPath, "dữ liệu cũ");

            using (new FileStream(request.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.ThrowsAsync<IOException>(() => _service.ExportExcelAsync(request, null, CancellationToken.None));
            }

            Assert.That(File.ReadAllText(request.TargetPath), Is.EqualTo("dữ liệu cũ"));
            Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(1), "Không để lại file tạm");
            Assert.That(await _history.ListRecentAsync(10, CancellationToken.None), Is.Empty, "Xuất thất bại thì không ghi lịch sử");
        }

        [Test]
        public async Task UnwritableTarget_FailsWithoutLeavingATempFile_ForExcelAndZip()
        {
            // Đích là một thư mục có sẵn: không thể thay bằng file, giống như khi ổ đĩa từ chối ghi.
            await AddAsync("1", new DateTime(2026, 1, 1));
            var blocked = Path.Combine(_dir, "la-thu-muc.xlsx");
            Directory.CreateDirectory(blocked);

            Assert.ThrowsAsync<IOException>(() => _service.ExportExcelAsync(Req(All(), "la-thu-muc.xlsx", overwrite: true), null, CancellationToken.None));
            Assert.ThrowsAsync<IOException>(() => _service.ExportZipAsync(Req(All(), "la-thu-muc.xlsx", overwrite: true), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
            Assert.That(await _history.ListRecentAsync(10, CancellationToken.None), Is.Empty);
        }

        [Test, Category("Performance")]
        public async Task Excel_100000Invoices_AreAllWritten_InOrder_AndFast()
        {
            const int count = 100000;
            for (var n = 1; n <= count; n++)
            {
                var invoice = new NormalizedInvoice
                {
                    Direction = InvoiceDirection.Purchase, InvoiceNumber = n.ToString("D7"), IssueDate = new DateTime(2026, 1, 1).AddDays(n % 300), FormSymbol = "1", InvoiceSymbol = "C26TAA",
                    SellerTaxCode = "0109988776", SellerName = "Công ty ABC (dữ liệu giả)", BuyerTaxCode = "0101234567", BuyerName = "Công ty TNHH Mẫu", StatusRaw = "1", CheckResultRaw = "5",
                    TotalBeforeTax = 1000m, TotalTax = 100m, TotalPayment = 1100m, Currency = "VND"
                };
                await _repo.AddAsync(
                    new StoredInvoice
                    {
                        InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = new List<InvoiceVersionRecord>(),
                        FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
                    },
                    CancellationToken.None);
            }

            var request = Req(All(), "lon.xlsx");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _service.ExportExcelAsync(request, null, CancellationToken.None);
            watch.Stop();

            Assert.That(result.Exported, Is.EqualTo(count));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(60)), "Xuất 100.000 dòng không được kéo dài");

            // Đọc dạng luồng: không dựng cả DOM của bảng tính hơn hai triệu ô.
            var rows = 0;
            string lastNumber = null;
            using (var zip = ZipFile.OpenRead(request.TargetPath))
            using (var s = zip.GetEntry("xl/worksheets/sheet1.xml").Open())
            using (var reader = XmlReader.Create(s))
            {
                string cellRef = null;
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    if (reader.LocalName == "row") rows++;
                    else if (reader.LocalName == "c") cellRef = reader.GetAttribute("r");
                    else if (reader.LocalName == "t" && cellRef != null && cellRef.StartsWith("D", StringComparison.Ordinal) && cellRef != "D6") lastNumber = reader.ReadElementContentAsString();
                }
            }

            Assert.That(rows, Is.GreaterThanOrEqualTo(count + 1), "Một dòng tiêu đề cột và 100.000 dòng dữ liệu");
            Assert.That(lastNumber, Is.EqualTo(count.ToString("D7")), "Dòng cuối là hóa đơn cuối theo số hóa đơn");
            Assert.That(Directory.GetFiles(_dir), Has.Length.EqualTo(1));
        }

        [Test]
        public void NothingToExport_IsReported_AndNoFileIsCreated()
        {
            var request = Req(All(), "l.xlsx");

            Assert.ThrowsAsync<InvoiceStorageException>(() => _service.ExportExcelAsync(request, null, CancellationToken.None));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public async Task WithoutTheExportEntitlement_NothingIsWritten()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            _entitlement.Allowed = false;

            Assert.ThrowsAsync<EntitlementDeniedException>(() => _service.ExportExcelAsync(Req(All(), "m.xlsx"), null, CancellationToken.None));
            Assert.ThrowsAsync<EntitlementDeniedException>(() => _service.ExportZipAsync(Req(All(), "m.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public async Task WithoutACompany_IsRejected()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            _context.Current = null;

            Assert.ThrowsAsync<StorageGuardException>(() => _service.ExportExcelAsync(Req(All(), "n.xlsx"), null, CancellationToken.None));
        }

        [Test]
        public async Task TargetFolderIsCreatedWhenMissing()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            var request = new ExportRequest { Scope = All(), TargetPath = Path.Combine(_dir, "Exports", "sub", "o.xlsx") };

            await _service.ExportExcelAsync(request, null, CancellationToken.None);

            Assert.That(File.Exists(request.TargetPath), Is.True);
        }

        // ---- ZIP ----

        [Test]
        public async Task Zip_ContainsTheCurrentVersionBytes_AndTheManifestHashesMatch_AC09()
        {
            var one = await AddAsync("1", new DateTime(2026, 1, 1), versions: 3);
            var two = await AddAsync("2", new DateTime(2026, 1, 2));
            var request = Req(All(), "a.zip");

            var result = await _service.ExportZipAsync(request, ZipFailurePolicy.Ask, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Empty);
            var entries = ReadZip(request.TargetPath);
            Assert.That(entries.Keys, Is.EquivalentTo(ViewerEntries("Purchase1_v3/").Concat(ViewerEntries("Purchase2_v1/")).Concat(new[] { "manifest.csv" })));
            Assert.That(entries["Purchase1_v3/invoice.xml"], Is.EqualTo(Encoding.UTF8.GetBytes(string.Format(Xml, "1-v3"))), "Chỉ phiên bản hiện hành, nguyên byte");

            var manifest = Encoding.UTF8.GetString(entries["manifest.csv"]).TrimStart('\uFEFF').Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(manifest[0], Is.EqualTo("so_hoa_don,ky_hieu,ngay_lap,mst_nguoi_ban,ten_file,sha256,trang_thai,ly_do"));
            Assert.That(manifest.Length, Is.EqualTo(3));
            foreach (var line in manifest.Skip(1))
            {
                var cols = line.Split(',');
                Assert.That(cols[6], Is.EqualTo("OK"));
                Assert.That(Sha(entries[cols[4]]), Is.EqualTo(cols[5]), "SHA-256 trong manifest khớp file trong ZIP");
            }

            Assert.That(one.Versions.Count, Is.EqualTo(3));
            Assert.That(two.InvoiceId, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public async Task Zip_AskPolicy_StopsBeforeWritingAnything_WhenAFileIsMissing()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            await AddAsync("2", new DateTime(2026, 1, 2), present: false);
            await AddAsync("3", new DateTime(2026, 1, 3));
            var request = Req(All(), "b.zip");

            var ex = Assert.ThrowsAsync<ExportNeedsDecisionException>(() => _service.ExportZipAsync(request, ZipFailurePolicy.Ask, null, CancellationToken.None));

            Assert.That(ex.ValidCount, Is.EqualTo(2));
            Assert.That(ex.Problems.Single().InvoiceNumber, Is.EqualTo("2"));
            Assert.That(ex.Problems.Single().Kind, Is.EqualTo(ExportProblemKind.MissingFile));
            Assert.That(Directory.GetFiles(_dir), Is.Empty, "Chưa có file nào ở đích, kể cả file tạm");
        }

        [Test]
        public async Task Zip_SkipAndRecord_PacksTheValidOnes_AndTheManifestListsTheSkippedWithAReason()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            await AddAsync("2", new DateTime(2026, 1, 2), present: false);
            var request = Req(All(), "c.zip");

            var result = await _service.ExportZipAsync(request, ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(1));
            Assert.That(result.Skipped.Single().InvoiceNumber, Is.EqualTo("2"));
            var entries = ReadZip(request.TargetPath);
            Assert.That(entries.Keys, Is.EquivalentTo(ViewerEntries("Purchase1_v1/").Concat(new[] { "manifest.csv" })));
            var manifest = Encoding.UTF8.GetString(entries["manifest.csv"]);
            Assert.That(manifest, Does.Contain("BO_QUA"));
            Assert.That(manifest, Does.Contain("File XML không còn trong thư mục kho"));
        }

        [Test]
        public async Task EachCompletedExport_IsRecordedWithItsScopeCountsAndSkippedInvoices()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            await AddAsync("2", new DateTime(2026, 1, 2), present: false);

            var excel = Req(All(), "h.xlsx");
            await _service.ExportExcelAsync(excel, null, CancellationToken.None);
            var zip = Req(All(), "h.zip");
            await _service.ExportZipAsync(zip, ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            var records = await _history.ListRecentAsync(10, CancellationToken.None);
            Assert.That(records.Count, Is.EqualTo(2));
            var z = records.Single(r => r.Kind == ExportKind.Zip);
            Assert.That(z.TargetPath, Is.EqualTo(zip.TargetPath));
            Assert.That(z.ExportedCount, Is.EqualTo(1));
            Assert.That(z.SkippedCount, Is.EqualTo(1));
            Assert.That(z.SkippedSummary, Does.Contain("Số 2").And.Contain("File XML không còn trong thư mục kho"));
            Assert.That(z.ScopeText, Does.Contain("toàn bộ kết quả"));
            var x = records.Single(r => r.Kind == ExportKind.Excel);
            Assert.That(x.ExportedCount, Is.EqualTo(2));
            Assert.That(x.SkippedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task FailedOrRefusedExports_AreNotRecorded()
        {
            await AddAsync("1", new DateTime(2026, 1, 1), present: false);
            var request = Req(All(), "f.zip");

            Assert.ThrowsAsync<ExportNeedsDecisionException>(() => _service.ExportZipAsync(request, ZipFailurePolicy.Ask, null, CancellationToken.None));
            Assert.ThrowsAsync<ExportAbortedException>(() => _service.ExportZipAsync(request, ZipFailurePolicy.Abort, null, CancellationToken.None));

            Assert.That(await _history.ListRecentAsync(10, CancellationToken.None), Is.Empty);
        }

        [Test]
        public async Task AFailingHistory_DoesNotFailAnExportThatAlreadySucceeded()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            var archive = new InvoiceArchiveService(_repo, _files, _context);
            var service = new InvoiceExportService(_repo, archive, _files, _context, _entitlement, new ThrowingHistory());
            var request = Req(All(), "t.xlsx");

            var result = await service.ExportExcelAsync(request, null, CancellationToken.None);

            Assert.That(result.Exported, Is.EqualTo(1));
            Assert.That(File.Exists(request.TargetPath), Is.True);
        }

        private sealed class ThrowingHistory : IExportHistoryRepository
        {
            public Task AddAsync(ExportRecord record, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("đĩa đầy");
            }

            public Task<IList<ExportRecord>> ListRecentAsync(int max, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("đĩa đầy");
            }
        }

        [Test]
        public async Task Zip_AbortPolicy_FailsOnTheFirstProblem_AndLeavesNothing()
        {
            await AddAsync("1", new DateTime(2026, 1, 1), present: false);
            await AddAsync("2", new DateTime(2026, 1, 2));
            var request = Req(All(), "d.zip");

            Assert.ThrowsAsync<ExportAbortedException>(() => _service.ExportZipAsync(request, ZipFailurePolicy.Abort, null, CancellationToken.None));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public async Task Zip_AFileWhoseHashChanged_IsNotPackaged()
        {
            var stored = await AddAsync("1", new DateTime(2026, 1, 1));
            await AddAsync("2", new DateTime(2026, 1, 2));
            _files.Put(stored.Versions[0].RelativePath, Encoding.UTF8.GetBytes("<HDon>đã bị sửa</HDon>"));

            var result = await _service.ExportZipAsync(Req(All(), "e.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None);

            Assert.That(result.Skipped.Single().Kind, Is.EqualTo(ExportProblemKind.HashMismatch));
            var entries = ReadZip(Path.Combine(_dir, "e.zip"));
            Assert.That(entries.Keys, Does.Not.Contain("Purchase1_v1/invoice.xml"));
            Assert.That(Encoding.UTF8.GetString(entries["manifest.csv"]), Does.Contain("sai SHA-256"));
        }

        [Test]
        public async Task Zip_AllFilesUnusable_FailsEvenWhenSkipping()
        {
            await AddAsync("1", new DateTime(2026, 1, 1), present: false);

            Assert.ThrowsAsync<InvoiceStorageException>(() => _service.ExportZipAsync(Req(All(), "f.zip"), ZipFailurePolicy.SkipAndRecord, null, CancellationToken.None));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public async Task Zip_EntriesWithTheSameFileNameGetUniqueNames()
        {
            var a = await AddAsync("1", new DateTime(2026, 1, 1));
            var b = await AddAsync("2", new DateTime(2026, 1, 2));
            var bytes = Encoding.UTF8.GetBytes(string.Format(Xml, "x"));
            foreach (var s in new[] { a, b })
            {
                s.Versions[0].RelativePath = @"Folder" + (s == a ? "A" : "B") + @"\same.xml";
                s.Versions[0].Sha256 = Sha(bytes);
                _files.Put(s.Versions[0].RelativePath, bytes);
            }

            await _repo.AddVersionAsync(a.InvoiceId, a.Versions[0], a.Invoice, DateTime.UtcNow, CancellationToken.None);
            await _repo.AddVersionAsync(b.InvoiceId, b.Versions[0], b.Invoice, DateTime.UtcNow, CancellationToken.None);
            var request = Req(ExportScope.ForSelection(new[] { a.InvoiceId, b.InvoiceId }), "g.zip");

            await _service.ExportZipAsync(request, ZipFailurePolicy.Ask, null, CancellationToken.None);

            Assert.That(ReadZip(request.TargetPath).Keys, Is.EquivalentTo(ViewerEntries("same/").Concat(ViewerEntries("same_2/")).Concat(new[] { "manifest.csv" })));
        }

        [Test]
        public async Task Zip_OneInvoice_IsTheSameFiveFilesAsThePortalsExportXml_WithoutAFolderOrManifest()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            var request = Req(All(), "one.zip");

            await _service.ExportZipAsync(request, ZipFailurePolicy.Ask, null, CancellationToken.None);

            var entries = ReadZip(request.TargetPath);
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { "invoice.xml", "invoice.html", "details.js", "sign-check.jpg", "viewinvoice-bg.jpg" }));
            Assert.That(entries["invoice.xml"], Is.EqualTo(Encoding.UTF8.GetBytes(string.Format(Xml, "1-v1"))));
            Assert.That(Encoding.UTF8.GetString(entries["invoice.html"]), Does.Contain("Số: 1-v1"));
        }

        [Test]
        public async Task Zip_ManifestCellsStartingWithAFormulaCharacterAreDefused_AndCommasQuoted()
        {
            await AddAsync("=1+1", new DateTime(2026, 1, 1), tweak: i => i.InvoiceSymbol = "A,\"B\"");
            await AddAsync("2", new DateTime(2026, 1, 2));

            await _service.ExportZipAsync(Req(All(), "h.zip"), ZipFailurePolicy.Ask, null, CancellationToken.None);

            var manifest = Encoding.UTF8.GetString(ReadZip(Path.Combine(_dir, "h.zip"))["manifest.csv"]);
            Assert.That(manifest, Does.Contain("'=1+1"));
            Assert.That(manifest, Does.Contain("\"A,\"\"B\"\"\""));
        }

        [Test]
        public async Task Zip_CancellingLeavesNothing()
        {
            for (var i = 1; i <= 6; i++) await AddAsync(i.ToString(), new DateTime(2026, 1, i));
            var cts = new CancellationTokenSource();

            Assert.ThrowsAsync<OperationCanceledException>(() => _service.ExportZipAsync(Req(All(), "i.zip"), ZipFailurePolicy.Ask, new CallbackProgress(p => { if (p.Done == 2) cts.Cancel(); }), cts.Token));

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }

        [Test]
        public async Task Zip_ExistingTargetNeedsConfirmation()
        {
            await AddAsync("1", new DateTime(2026, 1, 1));
            var request = Req(All(), "j.zip");
            File.WriteAllText(request.TargetPath, "cũ");

            Assert.ThrowsAsync<ExportTargetExistsException>(() => _service.ExportZipAsync(request, ZipFailurePolicy.Ask, null, CancellationToken.None));

            Assert.That(File.ReadAllText(request.TargetPath), Is.EqualTo("cũ"));
        }

        // ---- Đặt tên ----

        [Test]
        public void FileName_IsKindScopeAndTimestamp_WithoutInvalidCharacters()
        {
            var name = ExportFileNamer.FileName("DanhSach HoaDon", "Mua/vao: 2026", new DateTime(2026, 11, 2, 9, 5, 7), "xlsx");

            Assert.That(name, Is.EqualTo("DanhSach-HoaDon_Mua-vao--2026_20261102-090507.xlsx"));
        }

        [Test]
        public void ScopeLabel_DescribesDirectionAndRange_OrTheSelectionSize()
        {
            Assert.That(ExportFileNamer.ScopeLabel(ExportScope.ForQuery(new ArchiveQuery { Direction = InvoiceDirection.Sold })), Is.EqualTo("Ban-ra"));
            Assert.That(ExportFileNamer.ScopeLabel(ExportScope.ForQuery(new ArchiveQuery { Direction = InvoiceDirection.Purchase, IssueFrom = new DateTime(2026, 1, 1), IssueTo = new DateTime(2026, 12, 31) })),
                Is.EqualTo("Mua-vao_20260101-20261231"));
            Assert.That(ExportFileNamer.ScopeLabel(ExportScope.ForSelection(new[] { Guid.NewGuid(), Guid.NewGuid() })), Is.EqualTo("Da-chon-2"));
        }

        [Test]
        public void ColumnName_CoversTheAlphabetAndBeyond()
        {
            Assert.That(InvoiceExcelWriter.ColumnName(1), Is.EqualTo("A"));
            Assert.That(InvoiceExcelWriter.ColumnName(26), Is.EqualTo("Z"));
            Assert.That(InvoiceExcelWriter.ColumnName(27), Is.EqualTo("AA"));
            Assert.That(InvoiceExcelWriter.ColumnName(52), Is.EqualTo("AZ"));
            Assert.That(InvoiceExcelWriter.ColumnName(703), Is.EqualTo("AAA"));
        }

        // ---- Fakes ----

        private sealed class SyncProgress : IProgress<ExportProgress>
        {
            private readonly List<ExportProgress> _seen;

            public SyncProgress(List<ExportProgress> seen)
            {
                _seen = seen;
            }

            public void Report(ExportProgress value)
            {
                _seen.Add(value);
            }
        }

        private sealed class CallbackProgress : IProgress<ExportProgress>
        {
            private readonly Action<ExportProgress> _callback;

            public CallbackProgress(Action<ExportProgress> callback)
            {
                _callback = callback;
            }

            public void Report(ExportProgress value)
            {
                _callback(value);
            }
        }

        private sealed class FakeEntitlement : IEntitlementService
        {
            public bool Allowed = true;
            public AppMode CurrentMode { get { return AppMode.Standard; } }
            public bool IsAllowed(AppFeature feature) { return Allowed; }
            public void Demand(AppFeature feature) { if (!Allowed) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }

        private sealed class FakeFiles : IInvoiceFileStore, IInvoiceFileReader
        {
            private readonly Dictionary<string, byte[]> _content = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            public void Put(string path, byte[] bytes)
            {
                _content[path] = bytes;
            }

            public byte[] Read(string storageRoot, string relativePath, long maxBytes)
            {
                byte[] found;
                if (!_content.TryGetValue(relativePath, out found)) throw new FileNotFoundException("missing", relativePath);
                if (found.LongLength > maxBytes) throw new InvoiceFileTooLargeException(found.LongLength, maxBytes);
                return found;
            }

            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName) { throw new NotSupportedException(); }
            public void DiscardTemp(string tempPath) { }
            public bool Exists(string storageRoot, string relativePath) { return _content.ContainsKey(relativePath); }
            public void Delete(string storageRoot, string relativePath) { }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }
    }
}
