#if DEBUG
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Invoices;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using HoaDonDienTu.Infrastructure.Tests.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Invoices
{
    /// <summary>Kho file thật trên thư mục tạm + engine tải chạy trên cổng giả (dữ liệu giả, không gọi cổng Thuế thật).</summary>
    [TestFixture]
    public class InvoiceStorageTests
    {
        private const string Mst = "0101234567";
        private static readonly CompanyId Id = CompanyId.NewId();

        private string _root;
        private FileSystemInvoiceFileStore _files;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-inv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _files = new FileSystemInvoiceFileStore();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // ---- Kho file ----

        [Test]
        public async Task WriteThenCommit_MovesTheTempFileToItsFinalName_Atomically()
        {
            var folder = Path.Combine("2026", "10", "BanRa", "XML");
            var temp = await _files.WriteTempAsync(_root, folder, new byte[] { 1, 2, 3 }, CancellationToken.None);

            Assert.That(Path.GetDirectoryName(temp), Is.EqualTo(Path.Combine(_root, folder)), "Tạm cùng thư mục đích để đổi tên là atomic");
            Assert.That(File.Exists(temp), Is.True);

            var relative = _files.Commit(_root, temp, folder, "a_v1.xml");

            Assert.That(relative, Is.EqualTo(Path.Combine(folder, "a_v1.xml")));
            Assert.That(File.ReadAllBytes(Path.Combine(_root, relative)), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(File.Exists(temp), Is.False);
        }

        [Test]
        public async Task Commit_NeverOverwritesAnExistingFile_AddsACollisionSuffix()
        {
            var folder = "x";
            var first = _files.Commit(_root, await _files.WriteTempAsync(_root, folder, new byte[] { 1 }, CancellationToken.None), folder, "a.xml");
            var second = _files.Commit(_root, await _files.WriteTempAsync(_root, folder, new byte[] { 2 }, CancellationToken.None), folder, "a.xml");
            var third = _files.Commit(_root, await _files.WriteTempAsync(_root, folder, new byte[] { 3 }, CancellationToken.None), folder, "a.xml");

            Assert.That(new[] { first, second, third }, Is.EqualTo(new[] { Path.Combine("x", "a.xml"), Path.Combine("x", "a_2.xml"), Path.Combine("x", "a_3.xml") }));
            Assert.That(File.ReadAllBytes(Path.Combine(_root, first)), Is.EqualTo(new byte[] { 1 }));
        }

        [Test]
        public async Task DiscardTemp_RemovesTheFile_AndIsSafeToRepeat()
        {
            var temp = await _files.WriteTempAsync(_root, "x", new byte[] { 1 }, CancellationToken.None);

            _files.DiscardTemp(temp);
            Assert.DoesNotThrow(() => _files.DiscardTemp(temp));
            Assert.DoesNotThrow(() => _files.DiscardTemp(null));

            Assert.That(File.Exists(temp), Is.False);
        }

        [Test]
        public async Task Delete_RemovesACommittedFile_AndIgnoresMissingOnes()
        {
            var relative = _files.Commit(_root, await _files.WriteTempAsync(_root, "x", new byte[] { 1 }, CancellationToken.None), "x", "a.xml");

            _files.Delete(_root, relative);

            Assert.That(File.Exists(Path.Combine(_root, relative)), Is.False);
            Assert.DoesNotThrow(() => _files.Delete(_root, relative));
        }

        [Test]
        public void WriteTemp_IntoALockedLocation_Fails_WithoutLeavingATempFile()
        {
            var blocker = Path.Combine(_root, "blocked");
            File.WriteAllText(blocker, "this is a file, not a folder");

            Assert.CatchAsync<IOException>(() => _files.WriteTempAsync(_root, "blocked", new byte[] { 1 }, CancellationToken.None));
        }

        // ---- XML thật của cổng được lưu nguyên văn ----

        [Test]
        public async Task RealInvoiceXml_IsStoredByteForByte_WithAMatchingSha256()
        {
            var zip = Find("invoice.zip");
            if (zip == null) Assert.Ignore("Không thấy docs/api-samples/invoice.zip");
            var xml = PortalResponseParser.ExtractXml(PortalApiConfigDefaults.Create().Download, File.ReadAllBytes(zip));
            var repo = new InMemoryInvoiceRepository();
            var service = Service(repo);
            var dto = new PortalInvoiceDto
            {
                PortalInvoiceId = "real", FormSymbol = "1", InvoiceSymbol = "C26TBN", InvoiceNumber = "40", IssueDate = new DateTime(2026, 10, 31),
                SellerTaxCode = Mst, BuyerTaxCode = "0101654633", BuyerName = "Người mua", StatusRaw = "1", CheckResultRaw = "5"
            };

            var result = await service.UpsertAsync(NewSession(), dto, new InvoiceXmlPayload(xml, "invoice.xml"), CancellationToken.None);

            Assert.That(result, Is.EqualTo(InvoiceSaveResult.Saved));
            var stored = (await repo.ListAsync(CancellationToken.None)).Single();
            var onDisk = File.ReadAllBytes(Path.Combine(_root, stored.Current.RelativePath));
            Assert.That(onDisk, Is.EqualTo(xml), "Giữ nguyên byte để chữ ký số còn kiểm tra được");
            Assert.That(stored.Current.SizeBytes, Is.EqualTo(xml.LongLength));
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                Assert.That(stored.Current.Sha256, Is.EqualTo(BitConverter.ToString(sha.ComputeHash(xml)).Replace("-", string.Empty).ToLowerInvariant()));
            }
        }

        [Test]
        public void ListFileNames_ReturnsTheFilesOfOneFolder_AndRefusesPathsOutsideTheStore()
        {
            var folder = Path.Combine(_root, "2026", "01", "BanRa");
            Directory.CreateDirectory(Path.Combine(folder, "con"));
            File.WriteAllText(Path.Combine(folder, "A_0001_v1.xml"), "x");
            File.WriteAllText(Path.Combine(folder, "B_0002_v1.xml"), "x");
            File.WriteAllText(Path.Combine(folder, "con", "khong-tinh.xml"), "x");

            var names = _files.ListFileNames(_root, @"2026\01\BanRa");

            Assert.That(names, Is.EquivalentTo(new[] { "A_0001_v1.xml", "B_0002_v1.xml" }), "Chỉ file của đúng thư mục, không lấy thư mục con");
            Assert.That(names.Contains("a_0001_V1.XML"), Is.True, "Không phân biệt hoa thường như hệ thống file Windows");
            Assert.That(_files.ListFileNames(_root, @"2026\02\BanRa"), Is.Empty, "Thư mục không có thì rỗng, không lỗi");
            Assert.That(_files.ListFileNames(_root, @"..\ngoai"), Is.Null, "Ra ngoài thư mục kho thì không liệt kê");
            Assert.That(_files.ListFileNames(_root, string.Empty), Is.Null, "Gốc kho để kiểm tra từng file");
        }

        // ---- Engine tải + kho trên cổng giả ----

        [Test]
        public async Task EndToEnd_MonthOfSoldInvoices_AreAllWrittenToTheCompanyFolder()
        {
            using (var rig = new Rig(_root))
            {
                var expected = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                var session = await rig.Downloads.GetSessionAsync(id, CancellationToken.None);
                Assert.That(session.State, Is.EqualTo(DownloadSessionState.Completed));
                var files = Directory.GetFiles(Path.Combine(_root, "2026", "01", "BanRa", "XML"), "*.xml");
                Assert.That(files.Length, Is.EqualTo(expected.Count));
                Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);

                var first = expected.First();
                var path = files.Single(f => f.EndsWith("_" + first.Dto.InvoiceNumber + "_v1.xml", StringComparison.Ordinal) && f.Contains(first.Dto.InvoiceSymbol));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(MockInvoiceDataset.BuildXml(first)));

                // Cổng trả về file zip: lưu nguyên cạnh XML (cùng tên, đuôi .zip) để xem và in đúng như cổng.
                var zips = Directory.GetFiles(Path.Combine(_root, "2026", "01", "BanRa", "XML"), "*.zip");
                Assert.That(zips.Length, Is.EqualTo(expected.Count));
                var zipPath = Path.ChangeExtension(path, ".zip");
                Assert.That(File.Exists(zipPath), Is.True);
                using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    var names = zip.Entries.Select(e => e.Name).ToList();
                    Assert.That(names, Does.Contain("invoice.xml"));
                    Assert.That(names, Does.Contain("invoice.html"));
                }
            }
        }

        [Test]
        public async Task EndToEnd_MultiPagePurchaseMonth_StoresAll120()
        {
            using (var rig = new Rig(_root))
            {
                rig.Portal.Handler.MaxPageSize = 50;
                var id = await rig.CreateAsync(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 7));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That(Directory.GetFiles(Path.Combine(_root, "2026", "07", "MuaVao", "XML"), "*.xml").Length, Is.EqualTo(120));
                Assert.That((await rig.Repository.ListAsync(CancellationToken.None)).Count, Is.EqualTo(120));
            }
        }

        [Test]
        public async Task EndToEnd_DownloadingTheSamePeriodAgain_CreatesNoDuplicatesOrNewFiles_AC04()
        {
            using (var rig = new Rig(_root))
            {
                var first = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));
                await rig.Orchestrator.RunAsync(first, null, CancellationToken.None);
                var filesBefore = Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories).Length;

                var second = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));
                await rig.Orchestrator.RunAsync(second, null, CancellationToken.None);

                var segment = (await rig.Downloads.GetSegmentsAsync(second, CancellationToken.None)).Single();
                Assert.That(segment.XmlSavedCount, Is.EqualTo(0));
                Assert.That(segment.ReceivedCount, Is.GreaterThan(0));
                Assert.That(Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories).Length, Is.EqualTo(filesBefore));
                Assert.That((await rig.Repository.ListAsync(CancellationToken.None)).All(i => i.Versions.Count == 1), Is.True);
            }
        }

        [Test]
        public async Task EndToEnd_AChangedXmlFromThePortal_BecomesVersion2_WithBothFilesKept_AC05()
        {
            using (var rig = new Rig(_root))
            {
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 2));
                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);
                var record = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)).Single();
                record.Dto.TotalPayment = (record.Dto.TotalPayment ?? 0m) + 1m;

                var again = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 2));
                await rig.Orchestrator.RunAsync(again, null, CancellationToken.None);

                var stored = (await rig.Repository.ListAsync(CancellationToken.None)).Single();
                Assert.That(stored.Versions.Select(v => v.VersionNo), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(stored.Versions.All(v => File.Exists(Path.Combine(_root, v.RelativePath))), Is.True);
                Assert.That(stored.Current.VersionNo, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task EndToEnd_CorruptExportFromThePortal_StoresNothing_AndMarksTheSegmentFailed()
        {
            using (var rig = new Rig(_root))
            {
                rig.Portal.Handler.MalformedExportResponse = true;
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 2));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.CompletedWithErrors));
                Assert.That(Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories), Is.Empty);
                Assert.That(await rig.Repository.ListAsync(CancellationToken.None), Is.Empty);
            }
        }

        [Test]
        public async Task EndToEnd_AnInvoiceWhoseXmlIsRejected_DoesNotStopTheRestOfTheSegment()
        {
            using (var rig = new Rig(_root))
            {
                rig.Portal.Handler.CorruptXmlForInvoiceNumber = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)).First().Dto.InvoiceNumber;
                var expected = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)).Count;
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                var segment = (await rig.Downloads.GetSegmentsAsync(id, CancellationToken.None)).Single();
                Assert.That(segment.ErrorCount, Is.EqualTo(1));
                Assert.That(segment.XmlSavedCount, Is.EqualTo(expected - 1));
                Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Failed));
                var events = await rig.Downloads.GetEventsAsync(id, CancellationToken.None);
                Assert.That(events.Select(e => e.Code), Does.Contain("INVOICE_REJECTED"));
            }
        }

        // ---- Hỗ trợ ----

        private InvoiceStorageService Service(IInvoiceRepository repo)
        {
            return new InvoiceStorageService(repo, _files, new InvoiceNormalizer(), new FixedContext(_root), new SystemClock(), new Quiet());
        }

        private DownloadSession NewSession()
        {
            return new DownloadSession(Guid.NewGuid(), Id, TaxCode.Parse(Mst), _root, InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 10),
                new InvoiceSearchFilter(), DateTime.UtcNow, "t");
        }

        private static string Find(string name)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "api-samples", name);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            return null;
        }

        private sealed class Rig : IDisposable
        {
            private readonly InMemoryPortalSessionStore _session = new InMemoryPortalSessionStore();

            public Rig(string root)
            {
                Portal = new TestPortal(Mst);
                Downloads = new InMemoryDownloadRepository();
                Repository = new InMemoryInvoiceRepository();
                var context = new FixedContext(root);
                _session.Set(new PortalSession(Id, TaxCode.Parse(Mst), new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
                var storage = new InvoiceStorageService(Repository, new FileSystemInvoiceFileStore(), new InvoiceNormalizer(), context, new SystemClock(), new Quiet());
                Orchestrator = new DownloadOrchestrator(
                    Downloads, context, _session, () => Portal.Client, storage, new NoLimit(), DefaultRetryPolicy.Standard,
                    new EntitlementService(AppMode.Standard, false), new SystemClock(), new Quiet(), (span, token) => Task.FromResult(0));
            }

            public TestPortal Portal { get; private set; }
            public InMemoryDownloadRepository Downloads { get; private set; }
            public InMemoryInvoiceRepository Repository { get; private set; }
            public DownloadOrchestrator Orchestrator { get; private set; }

            public Task<Guid> CreateAsync(InvoiceDirection direction, PeriodSpec period)
            {
                var plan = DownloadPlan.Create(direction, period, new InvoiceSearchFilter(), new PeriodSplitter());
                return Orchestrator.CreateSessionAsync(plan, CancellationToken.None);
            }

            public void Dispose()
            {
                Portal.Dispose();
            }
        }

        private sealed class FixedContext : ICompanyContextAccessor
        {
            private readonly CompanyContext _context;

            public FixedContext(string root)
            {
                _context = new CompanyContext(Id, TaxCode.Parse(Mst), "Công ty mẫu", root, null);
            }

            public bool HasCurrent { get { return true; } }
            public CompanyContext Current { get { return _context; } }
        }

        private sealed class NoLimit : IRateLimiter
        {
            public Task WaitAsync(CancellationToken cancellationToken) { return Task.FromResult(0); }
        }

        private sealed class Quiet : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
#endif
