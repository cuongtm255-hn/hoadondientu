using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Invoices;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>
    /// NFR-PERF-002: với kho 100.000 hóa đơn, tìm theo số/MST và mở trang đầu không quá 2 giây ở percentile 95.
    /// Dữ liệu GIẢ (MOCK) trên SQLite company.db thật và file thật ở thư mục tạm; không gọi cổng Thuế. Chạy lâu nên mặc định bị bỏ qua:
    /// đặt biến môi trường <c>HDDT_PERF=1</c> để chạy (<c>/TestCaseFilter:"FullyQualifiedName~ArchiveScaleTests"</c>); đổi số lượng bằng <c>HDDT_PERF_N</c>.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    [Category("Performance")]
    public class ArchiveScaleTests
    {
        private const string Mst = "0109213133";

        private string _base;
        private string _root;
        private int _count;
        private bool _keepData;
        private Context _context;
        private CompanyDatabaseProvider _provider;
        private SqliteInvoiceRepository _invoices;
        private InvoiceArchiveService _archive;

        [OneTimeSetUp]
        public async Task SeedOnce()
        {
            if (Environment.GetEnvironmentVariable("HDDT_PERF") != "1") Assert.Ignore("Hiệu năng trên 100.000 hóa đơn chạy lâu: đặt HDDT_PERF=1 để chạy (P6-02).");
            var text = Environment.GetEnvironmentVariable("HDDT_PERF_N");
            _count = string.IsNullOrEmpty(text) ? 100000 : int.Parse(text);

            // HDDT_PERF_DIR giữ bộ dữ liệu giữa các lần chạy (dựng 100.000 hóa đơn mất hàng chục phút); không đặt thì dùng thư mục tạm và xóa khi xong.
            var keep = Environment.GetEnvironmentVariable("HDDT_PERF_DIR");
            _keepData = !string.IsNullOrEmpty(keep);
            _base = _keepData ? keep : Path.Combine(Path.GetTempPath(), "hddt-scale-" + Guid.NewGuid().ToString("N"));
            _root = Path.Combine(_base, "Kho", Mst);
            var marker = Path.Combine(_base, "seeded-" + _count + ".txt");
            Directory.CreateDirectory(_root);
            _context = new Context(new CompanyContext(CompanyId.NewId(), TaxCode.Parse(Mst), "Công ty mẫu (mô phỏng)", _root, null));
            _provider = new CompanyDatabaseProvider(_context);
            _invoices = new SqliteInvoiceRepository(_provider);
            _archive = new InvoiceArchiveService(_invoices, new FileSystemInvoiceFileStore(), _context);

            if (File.Exists(marker))
            {
                TestContext.WriteLine("PERF reusing seeded data in {0}", _base);
                return;
            }

            var seeding = Stopwatch.StartNew();
            for (var n = 1; n <= _count; n++)
            {
                var stored = Invoice(n);
                var path = stored.Versions[0].RelativePath;
                var full = Path.Combine(_root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "<HDon>DỮ LIỆU GIẢ</HDon>");
                await _invoices.AddAsync(stored, CancellationToken.None);
            }

            seeding.Stop();
            File.WriteAllText(marker, "ok");
            TestContext.WriteLine("PERF seeded {0:N0} invoices + files in {1:N1}s", _count, seeding.Elapsed.TotalSeconds);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (_keepData) return;
            try
            {
                Directory.Delete(_base, true);
            }
            catch (IOException)
            {
            }
        }

        private static StoredInvoice Invoice(int n)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = n % 2 == 0 ? InvoiceDirection.Sold : InvoiceDirection.Purchase,
                PortalInvoiceId = Guid.NewGuid().ToString(),
                FormSymbol = "1",
                InvoiceSymbol = "C26TAA",
                InvoiceNumber = n.ToString("D7"),
                IssueDate = new DateTime(2026, 1, 1).AddDays(n % 300),
                SellerTaxCode = n % 2 == 0 ? Mst : "01" + (10000000 + n % 5000).ToString("D8"),
                SellerName = "CÔNG TY GIẢ " + (n % 5000) + " (mô phỏng)",
                BuyerTaxCode = n % 2 == 0 ? "02" + (10000000 + n % 3000).ToString("D8") : Mst,
                BuyerName = "Khách hàng giả " + (n % 3000),
                TotalBeforeTax = 1000m,
                TotalTax = 100m,
                TotalPayment = 1100m,
                Currency = "VND",
                Status = InvoiceStatus.New,
                StatusRaw = "1",
                CheckResult = InvoiceCheckResult.CodeIssued,
                CheckResultRaw = "5"
            };
            var now = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc);
            var path = Path.Combine("2026", (invoice.IssueDate.Month).ToString("D2"), invoice.Direction + "_" + invoice.InvoiceNumber + "_v1.xml");
            return new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(),
                BusinessKey = BusinessKey.Build(invoice),
                Invoice = invoice,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                Versions = new List<InvoiceVersionRecord>
                {
                    new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = "abc", RelativePath = path, SizeBytes = 25, CreatedAtUtc = now, IsCurrent = true, SourceSessionId = Guid.NewGuid() }
                }
            };
        }

        /// <summary>Mỗi lần đo dùng dịch vụ mới (bộ nhớ đệm tồn tại file rỗng) để đo đúng lúc mở màn hình lần đầu.</summary>
        private async Task<double> TimeColdAsync(Func<ArchiveQuery> query)
        {
            var archive = new InvoiceArchiveService(_invoices, new FileSystemInvoiceFileStore(), _context);
            var watch = Stopwatch.StartNew();
            var page = await archive.QueryAsync(query(), CancellationToken.None);
            watch.Stop();
            Assert.That(page.Rows.Count, Is.GreaterThan(0));
            return watch.Elapsed.TotalSeconds;
        }

        private async Task<double[]> RepeatAsync(string name, Func<ArchiveQuery> query, bool cold)
        {
            var times = new List<double>();
            for (var i = 0; i < 20; i++)
            {
                if (cold)
                {
                    times.Add(await TimeColdAsync(query));
                }
                else
                {
                    var watch = Stopwatch.StartNew();
                    await _archive.QueryAsync(query(), CancellationToken.None);
                    watch.Stop();
                    times.Add(watch.Elapsed.TotalSeconds);
                }
            }

            var sorted = times.OrderBy(t => t).ToArray();
            var p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
            TestContext.WriteLine("PERF {0} [{1}]: median {2:N3}s, p95 {3:N3}s, max {4:N3}s", Ascii(name), cold ? "cold" : "warm", sorted[sorted.Length / 2], p95, sorted.Last());
            return sorted;
        }

        private static string Ascii(string text)
        {
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c == 'đ' ? 'd' : c);
            }

            return sb.ToString();
        }

        private static double P95(double[] sorted)
        {
            return sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
        }

        [Test]
        public async Task OpenFirstPage_NoFilter_P95_UnderTwoSeconds()
        {
            var sold = await RepeatAsync("Mở trang đầu, bán ra, không lọc", () => new ArchiveQuery { Direction = InvoiceDirection.Sold }, true);
            Assert.That(P95(sold), Is.LessThan(2.0), "NFR-PERF-002");
        }

        [Test]
        public async Task SearchByInvoiceNumber_P95_UnderTwoSeconds()
        {
            var number = (_count / 2 * 2).ToString("D7");
            var times = await RepeatAsync("Tìm theo số hóa đơn", () => new ArchiveQuery { Direction = InvoiceDirection.Sold, InvoiceNumber = number }, true);
            Assert.That(P95(times), Is.LessThan(2.0), "NFR-PERF-002");
        }

        [Test]
        public async Task SearchByCounterpartyTaxCode_P95_UnderTwoSeconds()
        {
            var times = await RepeatAsync("Tìm theo MST đối tác", () => new ArchiveQuery { Direction = InvoiceDirection.Sold, CounterpartyTaxCode = "02" + (10000000 + 6).ToString("D8") }, true);
            Assert.That(P95(times), Is.LessThan(2.0), "NFR-PERF-002");
        }

        [Test]
        public async Task QuickSearch_P95_UnderTwoSeconds()
        {
            var times = await RepeatAsync("Tìm nhanh theo tên", () => new ArchiveQuery { Direction = InvoiceDirection.Sold, QuickSearch = "khach hang gia 5" }, true);
            Assert.That(P95(times), Is.LessThan(2.0), "NFR-PERF-002");
        }

        [Test]
        public async Task OpenFirstPage_WarmCache_IsFast()
        {
            await _archive.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold }, CancellationToken.None);
            var times = await RepeatAsync("Mở trang đầu, bán ra", () => new ArchiveQuery { Direction = InvoiceDirection.Sold }, false);
            Assert.That(P95(times), Is.LessThan(2.0));
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
    }
}
