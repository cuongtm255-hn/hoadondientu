using System;
using System.Collections.Generic;
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
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>
    /// Đổi thư mục kho (T11-01) với thành phần thật: SQLite company.db, danh mục companies.json, kho có file định danh, context manager.
    /// Dữ liệu hóa đơn GIẢ (MOCK) trên thư mục tạm; không gọi cổng Thuế.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    public class StorageMoveIntegrationTests
    {
        private const string Mst = "0109213133";

        private string _base;
        private string _oldRoot;
        private string _catalogPath;
        private CompanyId _id;
        private JsonCompanyCatalog _registry;
        private FileSystemCompanyStorage _storage;
        private CompanyContextManager _manager;
        private CompanyDatabaseProvider _provider;
        private SqliteInvoiceRepository _invoices;
        private StorageMoveService _service;

        [SetUp]
        public async Task SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-move-it-" + Guid.NewGuid().ToString("N"));
            _oldRoot = Path.Combine(_base, "Kho-cu", Mst);
            _catalogPath = Path.Combine(_base, "companies.json");
            _id = CompanyId.NewId();
            var tax = TaxCode.Parse(Mst);

            _registry = new JsonCompanyCatalog(_catalogPath);
            _storage = new FileSystemCompanyStorage();
            _storage.Initialize(_oldRoot, _id, tax);
            await _registry.AddAsync(new CompanyCatalogItem(_id, tax, "Công ty mẫu (mô phỏng)", _oldRoot, null, null, 0, 0, CompanyCatalogStatus.Active), CancellationToken.None);

            _manager = new CompanyContextManager(new CompanyStorageOpener(_registry, _storage, new FixedClock(), new NullLogger()));
            await _manager.OpenAsync(new CompanyProfile(_id, "Công ty mẫu (mô phỏng)", tax), CancellationToken.None);
            _provider = new CompanyDatabaseProvider(_manager);
            _invoices = new SqliteInvoiceRepository(_provider);
            _service = new StorageMoveService(
                _manager, _registry, _storage, new NoJobs(), new AllowAll(), new SqliteCompanyDatabaseBackup(_provider, _manager), new NullLogger());
        }

        [TearDown]
        public void TearDown()
        {
            _manager.CloseCurrent();
            SQLiteConnectionPoolClear();
            try
            {
                Directory.Delete(_base, true);
            }
            catch (IOException)
            {
            }
        }

        private static void SQLiteConnectionPoolClear()
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public async Task Move_CarriesTheRealDatabaseAndFiles_ToTheNewFolder_AndTheAppKeepsWorkingThere()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            await _invoices.AddAsync(Invoice("2"), CancellationToken.None);
            var xml = Path.Combine(_oldRoot, "2026", "10", "a.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(xml));
            File.WriteAllText(xml, "<HDon>DỮ LIỆU GIẢ</HDon>");
            var newRoot = Path.Combine(_base, "Kho-moi", Mst);

            var result = await _service.MoveAsync(newRoot, null, CancellationToken.None);

            Assert.That(result.NewPath, Is.EqualTo(newRoot));
            Assert.That(_manager.Current.StorageRoot, Is.EqualTo(newRoot));
            Assert.That(File.ReadAllText(Path.Combine(newRoot, "2026", "10", "a.xml")), Is.EqualTo("<HDon>DỮ LIỆU GIẢ</HDon>"));
            Assert.That(File.Exists(Path.Combine(newRoot, FileSystemCompanyStorage.IdentityFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(newRoot, "Data", "company.db")), Is.True);

            // Dữ liệu đọc được từ cơ sở dữ liệu MỚI sau khi đổi, và ghi tiếp được vào đó.
            var listed = await _invoices.ListAsync(CancellationToken.None);
            Assert.That(listed.Select(i => i.Invoice.InvoiceNumber).OrderBy(n => n), Is.EqualTo(new[] { "1", "2" }));
            await _invoices.AddAsync(Invoice("3"), CancellationToken.None);
            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(3));

            // Danh mục ghi ra đĩa trỏ tới thư mục mới (đọc lại bằng một danh mục mới để chắc chắn không phải bộ nhớ đệm).
            var persisted = await new JsonCompanyCatalog(_catalogPath).GetItemsAsync(CancellationToken.None);
            Assert.That(persisted.Single().StoragePath, Is.EqualTo(newRoot));

            // Thư mục cũ còn nguyên (kể cả cơ sở dữ liệu cũ, không có hóa đơn số 3).
            Assert.That(File.ReadAllText(xml), Is.EqualTo("<HDon>DỮ LIỆU GIẢ</HDon>"));
            Assert.That(File.Exists(Path.Combine(_oldRoot, "Data", "company.db")), Is.True);
        }

        [Test]
        public async Task Move_IntoAFolderThatIsAnotherStore_IsRefused_AndNothingChanges()
        {
            await _invoices.AddAsync(Invoice("1"), CancellationToken.None);
            var other = Path.Combine(_base, "Kho-khac");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "x.txt"), "x");

            var ex = Assert.ThrowsAsync<StorageMoveException>(() => _service.MoveAsync(other, null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("trống"));
            Assert.That(_manager.Current.StorageRoot, Is.EqualTo(_oldRoot));
            Assert.That((await _invoices.ListAsync(CancellationToken.None)).Count, Is.EqualTo(1));
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
                    new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = "abc", RelativePath = @"2026\10\a.xml", SizeBytes = 1, CreatedAtUtc = now, IsCurrent = true, SourceSessionId = Guid.NewGuid() }
                }
            };
        }

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get { return new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc); } }
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
