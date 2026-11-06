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

        private sealed class Rig : IDisposable
        {
            private readonly InMemoryPortalSessionStore _session = new InMemoryPortalSessionStore();

            public Rig(string root)
            {
                Portal = new TestPortal(Mst);
                var context = new FixedContext(root);
                var provider = new CompanyDatabaseProvider(context);
                Downloads = new SqliteDownloadRepository(provider);
                Invoices = new SqliteInvoiceRepository(provider);
                _session.Set(new PortalSession(Id, TaxCode.Parse(Mst), new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
                var storage = new InvoiceStorageService(Invoices, new FileSystemInvoiceFileStore(), new InvoiceNormalizer(), context, new SystemClock(), new Quiet());
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
