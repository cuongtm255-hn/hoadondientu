#if DEBUG
using System;
using System.IO;
using System.Linq;
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
using HoaDonDienTu.Infrastructure.Invoices;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using HoaDonDienTu.Infrastructure.Tests.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Invoices
{
    /// <summary>
    /// Engine tải chạy trên cổng GIẢ (MOCK, không gọi cổng Thuế thật) nhưng lưu vào SQLite company.db thật và file thật trên thư mục tạm:
    /// kiểm tra metadata bền vững khớp file trên đĩa (DD-06 mục 8.2, AC-04, AC-05).
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    public class InvoiceStorageSqliteTests
    {
        private const string Mst = "0101234567";
        private static readonly CompanyId Id = CompanyId.NewId();

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-invsql-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public async Task MonthOfSoldInvoices_AreStoredInTheDatabase_WithAMatchingFileForEveryVersion()
        {
            using (var rig = new Rig(_root))
            {
                var expected = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.Completed));
                var stored = await rig.Invoices.ListAsync(CancellationToken.None);
                Assert.That(stored.Count, Is.EqualTo(expected.Count));
                Assert.That(stored.All(s => s.Versions.Count == 1 && s.Current != null), Is.True);
                Assert.That(stored.All(s => File.Exists(Path.Combine(_root, s.Current.RelativePath))), Is.True, "Mọi hóa đơn trong cơ sở dữ liệu có file XML tương ứng");
                Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            }
        }

        [Test]
        public async Task MultiPagePurchaseMonth_Stores120_AndTheyCanBeFoundAgainByBusinessKey()
        {
            using (var rig = new Rig(_root))
            {
                rig.Portal.Handler.MaxPageSize = 50;
                var id = await rig.CreateAsync(InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 7));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                var stored = await rig.Invoices.ListAsync(CancellationToken.None);
                Assert.That(stored.Count, Is.EqualTo(120));
                var sample = stored.First();
                var again = await rig.Invoices.FindAsync(sample.BusinessKey, CancellationToken.None);
                Assert.That(again.InvoiceId, Is.EqualTo(sample.InvoiceId));
            }
        }

        [Test]
        public async Task DownloadingTheSamePeriodAgain_CreatesNoDuplicatesOrNewFiles_AC04()
        {
            using (var rig = new Rig(_root))
            {
                var first = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));
                await rig.Orchestrator.RunAsync(first, null, CancellationToken.None);
                var filesBefore = Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories).Length;
                var countBefore = (await rig.Invoices.ListAsync(CancellationToken.None)).Count;

                var second = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));
                await rig.Orchestrator.RunAsync(second, null, CancellationToken.None);

                var segment = (await rig.Downloads.GetSegmentsAsync(second, CancellationToken.None)).Single();
                Assert.That(segment.XmlSavedCount, Is.EqualTo(0));
                Assert.That(Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories).Length, Is.EqualTo(filesBefore));
                var after = await rig.Invoices.ListAsync(CancellationToken.None);
                Assert.That(after.Count, Is.EqualTo(countBefore));
                Assert.That(after.All(i => i.Versions.Count == 1), Is.True);
            }
        }

        [Test]
        public async Task AChangedXmlFromThePortal_BecomesVersion2_KeepingBothFiles_AC05()
        {
            using (var rig = new Rig(_root))
            {
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 2));
                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);
                var record = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)).Single();
                record.Dto.TotalPayment = (record.Dto.TotalPayment ?? 0m) + 1m;

                var again = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 2));
                await rig.Orchestrator.RunAsync(again, null, CancellationToken.None);

                var stored = (await rig.Invoices.ListAsync(CancellationToken.None)).Single();
                Assert.That(stored.Versions.Select(v => v.VersionNo), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(stored.Versions.All(v => File.Exists(Path.Combine(_root, v.RelativePath))), Is.True);
                Assert.That(stored.Current.VersionNo, Is.EqualTo(2));
                Assert.That(stored.Versions.Count(v => v.IsCurrent), Is.EqualTo(1), "Chỉ một phiên bản hiện hành");
            }
        }

        [Test]
        public async Task CorruptExportFromThePortal_StoresNothing_InTheDatabaseOrOnDisk()
        {
            using (var rig = new Rig(_root))
            {
                rig.Portal.Handler.MalformedExportResponse = true;
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 2));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.CompletedWithErrors));
                Assert.That(Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories), Is.Empty);
                Assert.That(await rig.Invoices.ListAsync(CancellationToken.None), Is.Empty);
            }
        }

        // ---- Đĩa đầy giữa chừng (T06-09) ----

        /// <summary>Ghi nửa số byte rồi báo hết dung lượng như Windows (ERROR_DISK_FULL, HRESULT 0x80070070).</summary>
        private sealed class DiskFullStream : Stream
        {
            private readonly Stream _inner;

            public DiskFullStream(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return _inner.Length; } }
            public override long Position { get { return _inner.Position; } set { throw new NotSupportedException(); } }
            public override void Flush() { _inner.Flush(); }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count / 2);
                _inner.Flush();
                throw new IOException("Không đủ dung lượng trên đĩa.", unchecked((int)0x80070070));
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Write(buffer, offset, count);
                return Task.FromResult(0);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>Kho file thật nhưng từ lần ghi thứ <c>failOn</c> báo đĩa đầy cho tới khi <see cref="Full"/> tắt.</summary>
        private sealed class FlakyDisk
        {
            private readonly int _failOn;
            private int _writes;

            public FlakyDisk(int failOn)
            {
                _failOn = failOn;
                Full = true;
            }

            public bool Full;

            public FileSystemInvoiceFileStore Store()
            {
                return new FileSystemInvoiceFileStore(path =>
                {
                    var inner = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
                    return Full && ++_writes >= _failOn ? (Stream)new DiskFullStream(inner) : inner;
                });
            }
        }

        [Test]
        public async Task DiskFullMidWrite_LeavesNoPartialFileOrRow_PausesWithCheckpoint_AndResumesWithoutDuplicates()
        {
            // Mỗi hóa đơn ghi 2 file (XML và zip của cổng): lần ghi thứ 11 là XML của hóa đơn thứ 6.
            var disk = new FlakyDisk(11);
            using (var rig = new Rig(_root, disk.Store()))
            {
                var expected = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)).Count;
                Assert.That(expected, Is.GreaterThan(6), "Dữ liệu giả phải đủ lớn để đĩa đầy ở giữa");
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                // Hết dung lượng: tạm dừng có checkpoint (không phải lỗi), 5 hóa đơn đầu đã lưu trọn vẹn, hóa đơn thứ 6 không để lại gì.
                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
                var segment = (await rig.Downloads.GetSegmentsAsync(id, CancellationToken.None)).Single();
                Assert.That(segment.State, Is.EqualTo(DownloadSegmentState.Paused));
                Assert.That(segment.ReceivedCount, Is.EqualTo(5));
                Assert.That(segment.ItemOffset, Is.EqualTo(5));
                Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty, "Không để lại file tạm ghi dở");
                Assert.That(Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories).Length, Is.EqualTo(5));
                var partial = await rig.Invoices.ListAsync(CancellationToken.None);
                Assert.That(partial.Count, Is.EqualTo(5), "Cơ sở dữ liệu chỉ có các hóa đơn đã có file");
                Assert.That(partial.All(i => File.Exists(Path.Combine(_root, i.Current.RelativePath))), Is.True);

                // Người dùng giải phóng ổ đĩa rồi tiếp tục: đủ hóa đơn, không trùng.
                disk.Full = false;
                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.Completed));
                var all = await rig.Invoices.ListAsync(CancellationToken.None);
                Assert.That(all.Count, Is.EqualTo(expected));
                Assert.That(all.All(i => i.Versions.Count == 1), Is.True, "Tiếp tục không tạo phiên bản trùng");
                Assert.That(Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories).Length, Is.EqualTo(expected));
                Assert.That(all.All(i => File.Exists(Path.Combine(_root, i.Current.RelativePath))), Is.True);
            }
        }

        [Test]
        public async Task DiskFullWhileWritingThePortalPackage_PausesThere_AndResumeAddsTheMissingZip()
        {
            // Lần ghi thứ 10 là file zip của hóa đơn thứ 5: XML của nó đã lưu, zip thì chưa.
            var disk = new FlakyDisk(10);
            using (var rig = new Rig(_root, disk.Store()))
            {
                var expected = rig.Portal.Dataset.Query(InvoiceDirection.Sold, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)).Count;
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
                var segment = (await rig.Downloads.GetSegmentsAsync(id, CancellationToken.None)).Single();
                Assert.That(segment.ItemOffset, Is.EqualTo(4), "Hóa đơn thứ 5 chưa tính là xong vì thiếu zip");
                Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);

                disk.Full = false;
                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.Completed));
                var xmls = Directory.GetFiles(_root, "*.xml", SearchOption.AllDirectories);
                Assert.That(xmls.Length, Is.EqualTo(expected));
                Assert.That(xmls.All(x => File.Exists(Path.ChangeExtension(x, ".zip"))), Is.True, "Mọi XML đều có zip của cổng sau khi tiếp tục");
                Assert.That((await rig.Invoices.ListAsync(CancellationToken.None)).All(i => i.Versions.Count == 1), Is.True);
            }
        }

        [Test]
        public async Task DiskFull_FromTheFirstFile_StoresNothing_AndLeavesTheStoreCleanForARetry()
        {
            var disk = new FlakyDisk(1);
            using (var rig = new Rig(_root, disk.Store()))
            {
                var id = await rig.CreateAsync(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 1));

                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);

                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.PausedByUser));
                Assert.That(await rig.Invoices.ListAsync(CancellationToken.None), Is.Empty);
                var stray = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "Data" + Path.DirectorySeparatorChar)).ToList();
                Assert.That(stray, Is.Empty);

                disk.Full = false;
                await rig.Orchestrator.RunAsync(id, null, CancellationToken.None);
                Assert.That((await rig.Downloads.GetSessionAsync(id, CancellationToken.None)).State, Is.EqualTo(DownloadSessionState.Completed));
                Assert.That((await rig.Invoices.ListAsync(CancellationToken.None)).Count, Is.GreaterThan(0));
            }
        }

        private sealed class Rig : IDisposable
        {
            private readonly InMemoryPortalSessionStore _session = new InMemoryPortalSessionStore();

            public Rig(string root, FileSystemInvoiceFileStore files = null)
            {
                Portal = new TestPortal(Mst);
                var context = new FixedContext(root);
                var provider = new CompanyDatabaseProvider(context);
                Downloads = new SqliteDownloadRepository(provider);
                Invoices = new SqliteInvoiceRepository(provider);
                _session.Set(new PortalSession(Id, TaxCode.Parse(Mst), new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
                var storage = new InvoiceStorageService(Invoices, files ?? new FileSystemInvoiceFileStore(), new InvoiceNormalizer(), context, new SystemClock(), new Quiet());
                Orchestrator = new DownloadOrchestrator(
                    Downloads, context, _session, () => Portal.Client, storage, new NoLimit(), DefaultRetryPolicy.Standard,
                    new EntitlementService(AppMode.Standard, false), new SystemClock(), new Quiet(), (span, token) => Task.FromResult(0));
            }

            public TestPortal Portal { get; private set; }
            public SqliteDownloadRepository Downloads { get; private set; }
            public SqliteInvoiceRepository Invoices { get; private set; }
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
                _context = new CompanyContext(Id, TaxCode.Parse(Mst), "Công ty mẫu (mô phỏng)", root, null);
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
