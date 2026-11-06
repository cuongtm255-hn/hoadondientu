using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Companies;
using HoaDonDienTu.Infrastructure.Invoices;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>
    /// NFR-PERF-007: chuyển giữa hai kho hợp lệ (không có tác vụ chạy) trong 5 giây. Dùng bộ dữ liệu 100.000 hóa đơn của <see cref="ArchiveScaleTests"/>
    /// (cần <c>HDDT_PERF=1</c> và <c>HDDT_PERF_DIR</c> đã dựng sẵn) làm kho đích và một kho nhỏ làm kho còn lại. Dữ liệu GIẢ, không gọi cổng Thuế.
    /// Đo cả hai phần: chuyển context và lần tra cứu đầu tiên (lúc đó cơ sở dữ liệu mới thật sự được mở và kiểm toàn vẹn).
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    [Category("Performance")]
    public class SwitchScaleTests
    {
        private const string BigMst = "0109213133";
        private const string SmallMst = "0101234567";

        private string _smallBase;
        private string _bigRoot;
        private string _smallRoot;
        private CompanyProfile _big;
        private CompanyProfile _small;
        private JsonCompanyCatalog _registry;
        private CompanyContextManager _manager;
        private CompanyDatabaseProvider _provider;
        private InvoiceArchiveService _archive;
        private CompanySwitchService _switch;

        [OneTimeSetUp]
        public async Task Prepare()
        {
            var dir = Environment.GetEnvironmentVariable("HDDT_PERF_DIR");
            if (Environment.GetEnvironmentVariable("HDDT_PERF") != "1" || string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "seeded-100000.txt")))
            {
                Assert.Ignore("Cần HDDT_PERF=1 và HDDT_PERF_DIR có bộ dữ liệu 100.000 hóa đơn do ArchiveScaleTests dựng (P6-02).");
            }

            _bigRoot = Path.Combine(dir, "Kho", BigMst);
            _smallBase = Path.Combine(Path.GetTempPath(), "hddt-switch-" + Guid.NewGuid().ToString("N"));
            _smallRoot = Path.Combine(_smallBase, "Kho", SmallMst);

            var storage = new FileSystemCompanyStorage();
            _registry = new JsonCompanyCatalog(Path.Combine(_smallBase, "companies.json"));
            Directory.CreateDirectory(_smallBase);

            // Kho lớn đã có sẵn dữ liệu nhưng chưa có file định danh (do bộ dựng không tạo): tạo một lần, hoặc dùng lại nếu đã có.
            var bigInspection = storage.Inspect(_bigRoot);
            var bigId = bigInspection.HasIdentity ? bigInspection.IdentityCompanyId.Value : CompanyId.NewId();
            if (!bigInspection.HasIdentity) storage.Initialize(_bigRoot, bigId, TaxCode.Parse(BigMst));
            var smallId = CompanyId.NewId();
            storage.Initialize(_smallRoot, smallId, TaxCode.Parse(SmallMst));

            _big = new CompanyProfile(bigId, "Kho lớn 100.000 (mô phỏng)", TaxCode.Parse(BigMst));
            _small = new CompanyProfile(smallId, "Kho nhỏ (mô phỏng)", TaxCode.Parse(SmallMst));
            await _registry.AddAsync(new CompanyCatalogItem(_big.Id, _big.TaxCode, _big.DisplayName, _bigRoot, null, null, 0, 0, CompanyCatalogStatus.Active), CancellationToken.None);
            await _registry.AddAsync(new CompanyCatalogItem(_small.Id, _small.TaxCode, _small.DisplayName, _smallRoot, null, null, 0, 0, CompanyCatalogStatus.Active), CancellationToken.None);

            _manager = new CompanyContextManager(new CompanyStorageOpener(_registry, storage, new FixedClock(), new NullLogger()));
            _provider = new CompanyDatabaseProvider(_manager);
            var invoices = new SqliteInvoiceRepository(_provider);
            _archive = new InvoiceArchiveService(invoices, new FileSystemInvoiceFileStore(), _manager);
            _switch = new CompanySwitchService(_registry, _manager, new NoJobs(), new InMemoryPortalSessionStore(), new AllowAll(), new NullLogger());

            await _manager.OpenAsync(_small, CancellationToken.None);
            await _archive.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold }, CancellationToken.None);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (_manager != null) _manager.CloseCurrent();
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                if (_smallBase != null) Directory.Delete(_smallBase, true);
            }
            catch (IOException)
            {
            }
        }

        private async Task<double[]> SwitchAsync(CompanyProfile target)
        {
            var watch = Stopwatch.StartNew();
            var result = await _switch.SwitchAsync(target.Id, ActiveJobResolution.NotRequired, CancellationToken.None);
            var switched = watch.Elapsed.TotalSeconds;
            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));

            // Lần tra cứu đầu tiên mở cơ sở dữ liệu của kho đích (kiểm toàn vẹn) rồi trả trang đầu.
            var page = await _archive.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold }, CancellationToken.None);
            var total = watch.Elapsed.TotalSeconds;
            Assert.That(page.TotalCount, Is.GreaterThanOrEqualTo(0));
            TestContext.WriteLine("PERF switch to {0}: context {1:N3}s, with first query {2:N3}s (rows {3})", target.TaxCode.Value, switched, total, page.TotalCount);
            return new[] { switched, total };
        }

        [Test]
        public async Task SwitchBetweenASmallAndA100000InvoiceStore_StaysUnderFiveSeconds_NFR_PERF_007()
        {
            var worst = 0.0;
            for (var i = 0; i < 5; i++)
            {
                worst = Math.Max(worst, (await SwitchAsync(_big))[1]);
                worst = Math.Max(worst, (await SwitchAsync(_small))[1]);
            }

            TestContext.WriteLine("PERF worst switch incl. first query: {0:N3}s", worst);
            Assert.That(worst, Is.LessThan(5.0), "NFR-PERF-007");
        }

        [Test]
        public async Task OnlyOneStoreContextIsOpenAtATime_AcrossTheSwitches_NFR_REL_006()
        {
            for (var i = 0; i < 3; i++)
            {
                await SwitchAsync(_big);
                Assert.That(_manager.Current.CompanyId, Is.EqualTo(_big.Id));
                await SwitchAsync(_small);
                Assert.That(_manager.Current.CompanyId, Is.EqualTo(_small.Id));
            }
        }

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get { return new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc); } }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
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
