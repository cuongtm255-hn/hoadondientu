using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Infrastructure.Companies;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>
    /// P6-02: NFR-PERF-006 (danh mục lấy từ cache, không mở SQLite của nhiều MST) và NFR-REL-001 (mất điện/đóng tiến trình không mất hóa đơn
    /// đã commit). Thư mục tạm, dữ liệu GIẢ (MOCK); không gọi cổng Thuế.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    public class NfrReliabilityPerformanceTests
    {
        private string _base;

        [SetUp]
        public void SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-nfr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_base);
        }

        [TearDown]
        public void TearDown()
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(_base, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public async Task PERF_006_TheCompanyList_ComesFromTheCatalogCache_WithoutOpeningAnyStoreDatabase()
        {
            const int count = 200;
            var catalog = new JsonCompanyCatalog(Path.Combine(_base, "companies.json"));
            var storage = new FileSystemCompanyStorage();
            for (var i = 0; i < count; i++)
            {
                var tax = TaxCode.Parse("01" + (10000000 + i).ToString("D8"));
                var root = Path.Combine(_base, "Companies", tax.Value);
                var id = CompanyId.NewId();
                storage.Initialize(root, id, tax);
                await catalog.AddAsync(new CompanyCatalogItem(id, tax, "CÔNG TY MẪU " + i, root, DateTime.UtcNow, null, 1000 + i, 1024L * 1024, CompanyCatalogStatus.Active), CancellationToken.None);
            }

            var watch = Stopwatch.StartNew();
            var items = await catalog.GetItemsAsync(CancellationToken.None);
            watch.Stop();

            Assert.That(items.Count, Is.EqualTo(count));
            Assert.That(items.All(i => i.CachedInvoiceCount >= 1000), Is.True, "Số liệu tổng quan lấy từ danh mục lưu đệm");
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)), "200 doanh nghiệp phải liệt kê gần như tức thì");
            Assert.That(Directory.GetFiles(Path.Combine(_base, "Companies"), "company.db*", SearchOption.AllDirectories), Is.Empty, "Liệt kê không mở (cũng không tạo) cơ sở dữ liệu của kho nào");
            TestContext.WriteLine("PERF listed {0} companies in {1:N0} ms", count, watch.Elapsed.TotalMilliseconds);
        }

        [Test]
        public async Task REL_001_CommittedInvoices_SurviveAnAbruptStop_EvenWhileTheConnectionIsStillOpen()
        {
            // Mô phỏng mất điện/đóng tiến trình: chép các file cơ sở dữ liệu đúng lúc nó đang mở (chưa đóng, chưa checkpoint) rồi mở bản chép.
            const int count = 150;
            var liveRoot = Path.Combine(_base, "live");
            var provider = new CompanyDatabaseProvider(new FixedContext(liveRoot));
            var repository = new SqliteInvoiceRepository(provider);
            for (var n = 1; n <= count; n++) await repository.AddAsync(Invoice(n), CancellationToken.None);

            var crashRoot = Path.Combine(_base, "after-crash");
            Directory.CreateDirectory(Path.Combine(crashRoot, "Data"));
            foreach (var file in Directory.GetFiles(Path.Combine(liveRoot, "Data"), "company.db*"))
            {
                CopyWhileOpen(file, Path.Combine(crashRoot, "Data", Path.GetFileName(file)));
            }

            var recovered = new SqliteInvoiceRepository(new CompanyDatabaseProvider(new FixedContext(crashRoot)));
            var list = await recovered.ListAsync(CancellationToken.None);

            Assert.That(list.Count, Is.EqualTo(count), "Mọi hóa đơn đã commit phải còn sau khi dừng đột ngột");
            Assert.That(CompanyDatabaseInitializer.IsIntact(new CompanyDatabaseProvider(new FixedContext(crashRoot)).Current()), Is.True);
            Assert.That(list.Select(i => i.Invoice.InvoiceNumber).Distinct().Count(), Is.EqualTo(count));
        }

        [Test]
        public async Task REL_001_AnInterruptedWrite_NeverLeavesAHalfStoredInvoice()
        {
            // Một lần ghi hóa đơn thất bại giữa chừng (khóa nghiệp vụ trùng ở phiên bản thứ hai) không được để lại dòng dở dang.
            var root = Path.Combine(_base, "store");
            var repository = new SqliteInvoiceRepository(new CompanyDatabaseProvider(new FixedContext(root)));
            var first = Invoice(1);
            await repository.AddAsync(first, CancellationToken.None);
            var duplicate = Invoice(1);
            duplicate.InvoiceId = Guid.NewGuid();

            Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(duplicate, CancellationToken.None));

            var list = await repository.ListAsync(CancellationToken.None);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].InvoiceId, Is.EqualTo(first.InvoiceId));
            Assert.That(list[0].Versions.Count, Is.EqualTo(1), "Phiên bản của lần ghi thất bại không được lọt vào");
            Assert.That(await repository.FindByIdAsync(duplicate.InvoiceId, CancellationToken.None), Is.Null);
        }

        private static void CopyWhileOpen(string source, string target)
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(target, FileMode.Create, FileAccess.Write))
            {
                input.CopyTo(output);
            }
        }

        private static StoredInvoice Invoice(int n)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = InvoiceDirection.Sold, PortalInvoiceId = "p" + n, FormSymbol = "1", InvoiceSymbol = "C26TAA", InvoiceNumber = n.ToString("D7"),
                IssueDate = new DateTime(2026, 1, 1).AddDays(n % 200), SellerTaxCode = "0109213133", SellerName = "CÔNG TY GIẢ (mô phỏng)",
                BuyerIdNumber = "001000000000", BuyerName = "Người mua giả", TotalPayment = 1000m, Currency = "VND",
                Status = InvoiceStatus.New, StatusRaw = "1", CheckResult = InvoiceCheckResult.CodeIssued, CheckResultRaw = "5"
            };
            var now = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc);
            return new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, FirstSeenAtUtc = now, LastSeenAtUtc = now,
                Versions = new List<InvoiceVersionRecord>
                {
                    new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = "abc", RelativePath = @"2026\a" + n + ".xml", SizeBytes = 1, CreatedAtUtc = now, IsCurrent = true, SourceSessionId = Guid.NewGuid() }
                }
            };
        }

        private sealed class FixedContext : ICompanyContextAccessor
        {
            public FixedContext(string root)
            {
                Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109213133"), "Công ty mẫu (mô phỏng)", root, null);
            }

            public bool HasCurrent { get { return true; } }
            public CompanyContext Current { get; private set; }
        }
    }
}
