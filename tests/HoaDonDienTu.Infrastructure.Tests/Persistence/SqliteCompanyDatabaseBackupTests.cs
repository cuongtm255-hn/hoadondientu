using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>Chụp/thay <c>company.db</c> thật trên file tạm với hóa đơn và phiên tải GIẢ (MOCK); không gọi cổng Thuế.</summary>
    [TestFixture]
    [Category("MockData")]
    public class SqliteCompanyDatabaseBackupTests
    {
        private const string Mst = "0109213133";

        private string _base;
        private string _root;
        private Context _context;
        private CompanyDatabaseProvider _provider;
        private SqliteInvoiceRepository _invoices;
        private SqliteDownloadRepository _downloads;
        private SqliteCompanyDatabaseBackup _backup;

        [SetUp]
        public void SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-dbbackup-" + Guid.NewGuid().ToString("N"));
            _root = Path.Combine(_base, "Kho", Mst);
            Directory.CreateDirectory(_root);
            _context = new Context(new CompanyContext(CompanyId.NewId(), TaxCode.Parse(Mst), "Công ty mẫu (mô phỏng)", _root, null));
            _provider = new CompanyDatabaseProvider(_context);
            _invoices = new SqliteInvoiceRepository(_provider);
            _downloads = new SqliteDownloadRepository(_provider);
            _backup = new SqliteCompanyDatabaseBackup(_provider, _context);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_base, true);
            }
            catch (IOException)
            {
            }
        }

        // ---- Chụp ----

        [Test]
        public async Task Snapshot_IsAConsistentIntactCopy_WithTheSchemaVersion()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var file = Path.Combine(_base, "snap.db");

            var version = await _backup.SnapshotAsync(file, CancellationToken.None);

            Assert.That(version, Is.EqualTo(_backup.LatestSchemaVersion));
            Assert.That(CountInvoices(file), Is.EqualTo(1));
            Assert.That(File.Exists(file + "-wal"), Is.False);
            Assert.That(File.Exists(file + "-shm"), Is.False);
        }

        [Test]
        public async Task Snapshot_OverwritesAnExistingFile()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var file = Path.Combine(_base, "snap.db");
            File.WriteAllText(file, "rác từ lần trước");

            await _backup.SnapshotAsync(file, CancellationToken.None);

            Assert.That(CountInvoices(file), Is.EqualTo(1));
        }

        // ---- Thay ----

        [Test]
        public async Task Replace_BringsBackTheSnapshotData_AndDropsLaterChanges()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var file = Path.Combine(_base, "snap.db");
            await _backup.SnapshotAsync(file, CancellationToken.None);
            await _invoices.AddAsync(Invoice("2"), CancellationToken.None);

            await _backup.ReplaceAsync(file, CancellationToken.None);

            var listed = await _invoices.ListAsync(CancellationToken.None);
            Assert.That(listed.Select(i => i.Invoice.InvoiceNumber), Is.EqualTo(new[] { "1" }));
            Assert.That(CompanyDatabaseInitializer.IsIntact(_provider.Current()), Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(_root, "Data"), "*.tmp*"), Is.Empty, "không để lại file thay thế dở");
        }

        [Test]
        public async Task Replace_KeepsTheStoreWritable_AfterTheSwap()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var file = Path.Combine(_base, "snap.db");
            await _backup.SnapshotAsync(file, CancellationToken.None);
            await _backup.ReplaceAsync(file, CancellationToken.None);

            await _invoices.AddAsync(Invoice("3"), CancellationToken.None);

            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Replace_RebindsDownloadSessionsToTheOpenCompany()
        {
            // Gói lấy từ máy khác: mã doanh nghiệp và thư mục kho của phiên tải là của máy đó.
            var otherRoot = Path.Combine(_base, "MayKhac", Mst);
            var otherContext = new Context(new CompanyContext(CompanyId.NewId(), TaxCode.Parse(Mst), "Công ty mẫu (mô phỏng)", otherRoot, null));
            var otherProvider = new CompanyDatabaseProvider(otherContext);
            var session = new DownloadSession(Guid.NewGuid(), otherContext.Current.CompanyId, TaxCode.Parse(Mst), otherRoot, InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 10),
                new InvoiceSearchFilter(), new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc), "tester");
            await new SqliteDownloadRepository(otherProvider).CreateAsync(session, new List<DownloadSegment>(), CancellationToken.None);
            var file = Path.Combine(_base, "other.db");
            await new SqliteCompanyDatabaseBackup(otherProvider, otherContext).SnapshotAsync(file, CancellationToken.None);

            await _backup.ReplaceAsync(file, CancellationToken.None);

            var sessions = await _downloads.GetSessionsAsync(_context.Current.CompanyId, CancellationToken.None);
            Assert.That(sessions.Select(s => s.Id), Is.EqualTo(new[] { session.Id }));
            Assert.That(sessions[0].StorageRoot, Is.EqualTo(_root));
        }

        [Test]
        public async Task Replace_WithANewerSchema_IsRejected_AndLeavesTheCurrentDataAlone()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var file = Path.Combine(_base, "snap.db");
            await _backup.SnapshotAsync(file, CancellationToken.None);
            using (var connection = new SQLiteConnection("Data Source=" + file + ";Pooling=false"))
            {
                connection.Open();
                SqliteDatabase.Execute(connection, "PRAGMA user_version=" + (_backup.LatestSchemaVersion + 5));
            }

            await _invoices.AddAsync(Invoice("2"), CancellationToken.None);

            Assert.ThrowsAsync<BackupException>(() => _backup.ValidateAsync(file, CancellationToken.None));
            Assert.ThrowsAsync<BackupException>(() => _backup.ReplaceAsync(file, CancellationToken.None));
            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Replace_WithACorruptFile_IsRejected_AndLeavesTheCurrentDataAlone()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var file = Path.Combine(_base, "bad.db");
            File.WriteAllBytes(file, Enumerable.Repeat((byte)0x7f, 4096).ToArray());

            Assert.ThrowsAsync<BackupException>(() => _backup.ValidateAsync(file, CancellationToken.None));
            Assert.ThrowsAsync<BackupException>(() => _backup.ReplaceAsync(file, CancellationToken.None));

            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(Path.Combine(_root, "Data"), "*.tmp*").Concat(Directory.GetFiles(_base, "bad.db.check*")), Is.Empty);
        }

        // ---- Toàn tuyến qua BackupService ----

        [Test]
        public async Task BackupService_RoundTrip_RestoresFilesAndTheIndexTogether()
        {
            var service = new BackupService(_context, new NoJobs(), new AllowAll(), "1.0 test", null, _backup);
            Directory.CreateDirectory(Path.Combine(_root, "2026", "10"));
            File.WriteAllText(Path.Combine(_root, "2026", "10", "a.xml"), "<HDon>a (giả)</HDon>");
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var package = Path.Combine(_base, "goi.zip");

            var created = await service.CreateAsync(package, false, null, CancellationToken.None);
            var inspection = await service.InspectAsync(package, CancellationToken.None);
            await _invoices.AddAsync(Invoice("2"), CancellationToken.None);
            File.WriteAllText(Path.Combine(_root, "2026", "10", "a.xml"), "<HDon>đã sửa</HDon>");

            var restored = await service.RestoreAsync(package, null, CancellationToken.None);

            Assert.That(created.FileCount, Is.EqualTo(1));
            Assert.That(inspection.IsValid, Is.True);
            Assert.That(inspection.Manifest.FormatVersion, Is.EqualTo(2));
            Assert.That(inspection.Manifest.IncludesDatabase, Is.True);
            Assert.That(inspection.Manifest.SchemaVersion, Is.EqualTo(_backup.LatestSchemaVersion));
            Assert.That(restored.DatabaseRestored, Is.True);
            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Select(i => i.Invoice.InvoiceNumber), Is.EqualTo(new[] { "1" }));
            Assert.That(File.ReadAllText(Path.Combine(_root, "2026", "10", "a.xml")), Is.EqualTo("<HDon>a (giả)</HDon>"));
        }

        [Test]
        public async Task BackupService_RestoreOntoAnEmptyStore_RebuildsTheIndexFromThePackage()
        {
            var service = new BackupService(_context, new NoJobs(), new AllowAll(), "1.0 test", null, _backup);
            Directory.CreateDirectory(Path.Combine(_root, "2026"));
            File.WriteAllText(Path.Combine(_root, "2026", "a.xml"), "<HDon>a (giả)</HDon>");
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var package = Path.Combine(_base, "goi.zip");
            await service.CreateAsync(package, false, null, CancellationToken.None);

            // Máy mới: kho trống hoàn toàn, chưa có company.db.
            var freshRoot = Path.Combine(_base, "MayMoi", Mst);
            Directory.CreateDirectory(freshRoot);
            var freshContext = new Context(new CompanyContext(CompanyId.NewId(), TaxCode.Parse(Mst), "Công ty mẫu (mô phỏng)", freshRoot, null));
            var freshProvider = new CompanyDatabaseProvider(freshContext);
            var freshService = new BackupService(freshContext, new NoJobs(), new AllowAll(), "1.0 test", null, new SqliteCompanyDatabaseBackup(freshProvider, freshContext));

            await freshService.RestoreAsync(package, null, CancellationToken.None);

            var restored = await new SqliteInvoiceRepository(freshProvider).ListAsync(CancellationToken.None);
            Assert.That(restored.Select(i => i.Invoice.InvoiceNumber), Is.EqualTo(new[] { "1" }));
            Assert.That(File.Exists(Path.Combine(freshRoot, "2026", "a.xml")), Is.True);
        }

        // ---- Helpers ----

        private static long CountInvoices(string dbFile)
        {
            using (var connection = new SQLiteConnection("Data Source=" + dbFile + ";Pooling=false"))
            {
                connection.Open();
                return Convert.ToInt64(SqliteDatabase.Scalar(connection, "SELECT COUNT(*) FROM invoice"));
            }
        }

        private static StoredInvoice Invoice(string number)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = InvoiceDirection.Sold,
                PortalInvoiceId = Guid.NewGuid().ToString(),
                FormSymbol = "1",
                InvoiceSymbol = "C26TBN",
                InvoiceNumber = number,
                IssueDate = new DateTime(2026, 10, 31),
                SellerTaxCode = Mst,
                SellerName = "CÔNG TY GIẢ (mô phỏng)",
                BuyerIdNumber = "001000000000",
                BuyerName = "Người mua giả",
                TotalPayment = 1000m,
                Currency = "VND",
                Status = InvoiceStatus.New,
                StatusRaw = "1",
                CheckResult = InvoiceCheckResult.CodeIssued,
                CheckResultRaw = "5"
            };
            var now = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc);
            return new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(),
                BusinessKey = BusinessKey.Build(invoice),
                Invoice = invoice,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                Versions = new List<InvoiceVersionRecord>
                {
                    new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = "abc", RelativePath = @"2026\a.xml", SizeBytes = 1, CreatedAtUtc = now, IsCurrent = true, SourceSessionId = Guid.NewGuid() }
                }
            };
        }

        private sealed class Context : ICompanyContextAccessor
        {
            public Context(CompanyContext current)
            {
                Current = current;
            }

            public bool HasCurrent { get { return true; } }
            public CompanyContext Current { get; private set; }
        }

        private sealed class NoJobs : IActiveJobService
        {
            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(false); }
            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
        }

        private sealed class AllowAll : IEntitlementService
        {
            public AppMode CurrentMode { get { return AppMode.Standard; } }
            public bool IsAllowed(AppFeature feature) { return true; }
            public void Demand(AppFeature feature) { }
            public event EventHandler ModeChanged { add { } remove { } }
        }
    }
}
