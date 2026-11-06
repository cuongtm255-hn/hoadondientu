using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;
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
    /// <summary>
    /// NFR-PERF-001: UI không được treo khi tính hash, xuất file hay sao lưu. Chạy thao tác trên một "luồng giao diện" giả (vòng lặp thông điệp một luồng
    /// với SynchronizationContext như WinForms) và đo độ trễ của các thông điệp đều đặn 10 ms gửi vào luồng đó: nếu thao tác chặn luồng, độ trễ vọt lên.
    /// Dữ liệu GIẢ trong thư mục tạm; không gọi cổng Thuế.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    [Category("Performance")]
    public class UiResponsivenessTests
    {
        /// <summary>Ngưỡng một lần đứng hình chấp nhận được (người dùng bắt đầu thấy giật ở khoảng 100–200 ms).</summary>
        private static readonly TimeSpan MaxStall = TimeSpan.FromMilliseconds(250);

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-ui-stall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
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

        // ---- Vòng lặp giao diện giả ----

        private sealed class FakeUiThread : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
            private readonly Thread _thread;

            public FakeUiThread()
            {
                _thread = new Thread(Loop) { IsBackground = true, Name = "FakeUi" };
                _thread.Start();
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                if (!_queue.IsAddingCompleted) _queue.Add(() => d(state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                var done = new ManualResetEventSlim();
                Post(s => { try { d(s); } finally { done.Set(); } }, state);
                done.Wait();
            }

            private void Loop()
            {
                SetSynchronizationContext(this);
                foreach (var action in _queue.GetConsumingEnumerable()) action();
            }

            public void Dispose()
            {
                _queue.CompleteAdding();
                _thread.Join(2000);
            }
        }

        /// <summary>Chạy thao tác bắt đầu từ luồng giao diện giả; trả về độ trễ lớn nhất của thông điệp đều đặn và thời gian cả thao tác.</summary>
        private static async Task<KeyValuePair<TimeSpan, TimeSpan>> MeasureAsync(Func<Task> operation)
        {
            using (var ui = new FakeUiThread())
            {
                long maxTicks = 0;
                using (var heartbeat = new Timer(_ =>
                {
                    var posted = Stopwatch.GetTimestamp();
                    ui.Post(__ =>
                    {
                        var late = Stopwatch.GetTimestamp() - posted;
                        long seen;
                        while (late > (seen = Interlocked.Read(ref maxTicks)) && Interlocked.CompareExchange(ref maxTicks, late, seen) != seen) { }
                    }, null);
                }, null, 0, 10))
                {
                    var watch = Stopwatch.StartNew();
                    var finished = new TaskCompletionSource<bool>();
                    ui.Post(async _ =>
                    {
                        try
                        {
                            await operation();
                            finished.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            finished.TrySetException(ex);
                        }
                    }, null);
                    await finished.Task;
                    watch.Stop();

                    // Cho thông điệp cuối cùng kịp xử lý rồi mới đọc kết quả.
                    await Task.Delay(50);
                    var stall = TimeSpan.FromSeconds(Interlocked.Read(ref maxTicks) / (double)Stopwatch.Frequency);
                    return new KeyValuePair<TimeSpan, TimeSpan>(stall, watch.Elapsed);
                }
            }
        }

        private static void Report(string name, KeyValuePair<TimeSpan, TimeSpan> result)
        {
            TestContext.WriteLine("PERF {0}: operation {1:N0} ms, worst UI stall {2:N0} ms", name, result.Value.TotalMilliseconds, result.Key.TotalMilliseconds);
        }

        // ---- Thao tác ----

        [Test]
        public async Task ExportingTwentyThousandInvoicesToExcel_DoesNotBlockTheUiThread()
        {
            var service = Export(20000, 0, out var repository);
            var request = new ExportRequest { Scope = ExportScope.ForQuery(new ArchiveQuery { Direction = InvoiceDirection.Purchase }), TargetPath = Path.Combine(_dir, "lon.xlsx"), Overwrite = true };

            var result = await MeasureAsync(() => service.ExportExcelAsync(request, new Progress<ExportProgress>(), CancellationToken.None));

            Report("Excel 20,000", result);
            Assert.That(result.Value, Is.GreaterThan(TimeSpan.FromMilliseconds(200)), "Thao tác phải đủ lâu để phép đo có ý nghĩa");
            Assert.That(result.Key, Is.LessThan(MaxStall), "NFR-PERF-001");
            Assert.That(new FileInfo(request.TargetPath).Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task ZippingThreeHundredInvoicesWithHashing_DoesNotBlockTheUiThread()
        {
            var service = Export(300, 300, out var repository);
            var request = new ExportRequest { Scope = ExportScope.ForQuery(new ArchiveQuery { Direction = InvoiceDirection.Purchase }), TargetPath = Path.Combine(_dir, "goi.zip"), Overwrite = true };

            var result = await MeasureAsync(() => service.ExportZipAsync(request, ZipFailurePolicy.SkipAndRecord, new Progress<ExportProgress>(), CancellationToken.None));

            Report("ZIP 300", result);
            Assert.That(result.Key, Is.LessThan(MaxStall), "NFR-PERF-001");
            Assert.That(new FileInfo(request.TargetPath).Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task BackupAndRestoreOfFiveHundredFiles_DoNotBlockTheUiThread()
        {
            var root = Path.Combine(_dir, "kho");
            for (var i = 0; i < 500; i++)
            {
                var path = Path.Combine(root, "2026", "10", "hd" + i + ".xml");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes(new string('x', 20000) + i));
            }

            var context = new FixedContext(new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty mẫu (mô phỏng)", root, null));
            // Như bản thật: chụp/thay cơ sở dữ liệu chạy bằng Task.Run (SqliteCompanyDatabaseBackup), nên phần còn lại của sao lưu/khôi phục tiếp nối ở thread pool.
            var service = new BackupService(context, new NoJobs(), new AllowAll(), "1.0 test", () => new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc), new PoolDatabase());
            var target = Path.Combine(_dir, "sao-luu.zip");

            var backup = await MeasureAsync(() => service.CreateAsync(target, true, new Progress<BackupProgress>(), CancellationToken.None));
            Report("Backup 500 files", backup);
            Assert.That(backup.Key, Is.LessThan(MaxStall), "NFR-PERF-001 (sao lưu)");

            // Xóa vài file rồi khôi phục: phần ghi lại + kiểm SHA-256 cũng không được chặn luồng giao diện.
            foreach (var file in Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories).Take(250)) File.Delete(file);
            var restore = await MeasureAsync(() => service.RestoreAsync(target, new Progress<BackupProgress>(), CancellationToken.None));
            Report("Restore 500 files", restore);
            Assert.That(restore.Key, Is.LessThan(MaxStall), "NFR-PERF-001 (khôi phục)");
            Assert.That(Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories).Length, Is.EqualTo(500));
        }

        [Test]
        public async Task ZipExport_StaysOffTheUiThread_EvenWhenTheStoreCompletesSynchronously()
        {
            // Đối chứng gia cố: repository trả kết quả ngay (không có điểm bất đồng bộ thật) thì việc đọc file, băm SHA-256 và nén vẫn phải ở thread pool.
            // (Excel không có biến thể này vì việc nạp 20.000 hóa đơn từ kho trong bộ nhớ đã tốn ~0,4 s ngay trên luồng gọi: đó là chi phí của kho giả, không phải của SQLite.)
            var zip = Export(300, 300, out var repository, false);
            var request = new ExportRequest { Scope = ExportScope.ForQuery(new ArchiveQuery { Direction = InvoiceDirection.Purchase }), TargetPath = Path.Combine(_dir, "dong-bo.zip"), Overwrite = true };

            var result = await MeasureAsync(() => zip.ExportZipAsync(request, ZipFailurePolicy.SkipAndRecord, new Progress<ExportProgress>(), CancellationToken.None));

            Report("ZIP 300 (sync store)", result);
            Assert.That(result.Key, Is.LessThan(MaxStall), "ZIP không được chạy trên luồng giao diện");
        }

        [Test]
        public async Task TheProbeItself_DetectsABlockedUiThread()
        {
            // Đối chứng: một thao tác chặn luồng giao diện 600 ms phải bị phát hiện, nếu không thì các phép đo trên vô nghĩa.
            var result = await MeasureAsync(() =>
            {
                Thread.Sleep(600);
                return Task.FromResult(0);
            });

            Assert.That(result.Key, Is.GreaterThan(TimeSpan.FromMilliseconds(400)));
        }

        // ---- Dựng dữ liệu ----

        private InvoiceExportService Export(int invoices, int files, out InMemoryInvoiceRepository repository, bool asynchronousStore = true)
        {
            repository = new InMemoryInvoiceRepository();
            var store = new MemoryFiles();
            var context = new FixedContext(new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty mẫu (mô phỏng)", @"C:\Kho", null));
            for (var n = 1; n <= invoices; n++)
            {
                var invoice = new NormalizedInvoice
                {
                    Direction = InvoiceDirection.Purchase, InvoiceNumber = n.ToString("D7"), IssueDate = new DateTime(2026, 1, 1).AddDays(n % 300), FormSymbol = "1", InvoiceSymbol = "C26TAA",
                    SellerTaxCode = "0109988776", SellerName = "Công ty ABC (mô phỏng)", BuyerTaxCode = "0101234567", BuyerName = "Công ty TNHH Mẫu",
                    StatusRaw = "1", CheckResultRaw = "5", TotalBeforeTax = 1000m, TotalTax = 100m, TotalPayment = 1100m, Currency = "VND"
                };
                var versions = new List<InvoiceVersionRecord>();
                if (n <= files)
                {
                    var bytes = Encoding.UTF8.GetBytes("<HDon><DLHDon><TTChung><SHDon>" + n + "</SHDon></TTChung></DLHDon>" + new string('x', 30000) + "</HDon>");
                    var path = @"X\" + invoice.InvoiceNumber + "_v1.xml";
                    store.Put(path, bytes);
                    string sha;
                    using (var hash = SHA256.Create()) sha = string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2")));
                    versions.Add(new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = sha, RelativePath = path, IsCurrent = true, SizeBytes = bytes.Length });
                }

                repository.AddAsync(
                    new StoredInvoice
                    {
                        InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = versions,
                        FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
                    },
                    CancellationToken.None).GetAwaiter().GetResult();
            }

            // Repository SQLite thật chạy truy vấn ở thread pool nên các tiếp nối sau await rời luồng giao diện; mô phỏng đúng điều đó.
            IInvoiceRepository pooled = asynchronousStore ? (IInvoiceRepository)new PoolRepository(repository) : repository;
            var archive = new InvoiceArchiveService(pooled, store, context);
            return new InvoiceExportService(pooled, archive, store, context, new AllowAll());
        }

        // ---- Fakes ----

        private sealed class PoolDatabase : ICompanyDatabaseBackup
        {
            public int LatestSchemaVersion { get { return 4; } }

            public Task<int> SnapshotAsync(string destinationFile, CancellationToken cancellationToken)
            {
                return Task.Run(() => { File.WriteAllBytes(destinationFile, new byte[] { 1, 2, 3, 4 }); return 4; });
            }

            public Task ValidateAsync(string sourceFile, CancellationToken cancellationToken)
            {
                return Task.Run(() => { });
            }

            public Task ReplaceAsync(string sourceFile, CancellationToken cancellationToken)
            {
                return Task.Run(() => { });
            }
        }

        /// <summary>Bọc repository trong bộ nhớ để mọi lời gọi hoàn tất ở thread pool, như truy vấn SQLite thật.</summary>
        private sealed class PoolRepository : IInvoiceRepository
        {
            private readonly IInvoiceRepository _inner;

            public PoolRepository(IInvoiceRepository inner)
            {
                _inner = inner;
            }

            public Task<StoredInvoice> FindAsync(string businessKey, CancellationToken cancellationToken) { return Task.Run(() => _inner.FindAsync(businessKey, cancellationToken).GetAwaiter().GetResult()); }
            public Task<StoredInvoice> FindByIdAsync(Guid invoiceId, CancellationToken cancellationToken) { return Task.Run(() => _inner.FindByIdAsync(invoiceId, cancellationToken).GetAwaiter().GetResult()); }
            public Task AddAsync(StoredInvoice invoice, CancellationToken cancellationToken) { return Task.Run(() => _inner.AddAsync(invoice, cancellationToken).GetAwaiter().GetResult()); }
            public Task AddVersionAsync(Guid invoiceId, InvoiceVersionRecord version, NormalizedInvoice latest, DateTime nowUtc, CancellationToken cancellationToken) { return Task.Run(() => _inner.AddVersionAsync(invoiceId, version, latest, nowUtc, cancellationToken).GetAwaiter().GetResult()); }
            public Task TouchAsync(Guid invoiceId, DateTime nowUtc, NormalizedInvoice latest, string restoredRelativePath, CancellationToken cancellationToken) { return Task.Run(() => _inner.TouchAsync(invoiceId, nowUtc, latest, restoredRelativePath, cancellationToken).GetAwaiter().GetResult()); }
            public Task<IList<StoredInvoice>> ListAsync(CancellationToken cancellationToken) { return Task.Run(() => _inner.ListAsync(cancellationToken).GetAwaiter().GetResult()); }
            public Task<IList<InvoiceFileRef>> ListFileRefsAsync(ArchiveCriteria criteria, CancellationToken cancellationToken) { return Task.Run(() => _inner.ListFileRefsAsync(criteria, cancellationToken).GetAwaiter().GetResult()); }
            public Task<IList<StoredInvoice>> FindManyAsync(IList<Guid> ids, CancellationToken cancellationToken) { return Task.Run(() => _inner.FindManyAsync(ids, cancellationToken).GetAwaiter().GetResult()); }
            public Task<IList<int>> ListYearsAsync(InvoiceDirection direction, CancellationToken cancellationToken) { return Task.Run(() => _inner.ListYearsAsync(direction, cancellationToken).GetAwaiter().GetResult()); }
            public Task SaveIntegrityAsync(IDictionary<Guid, IntegrityProblemKind?> results, DateTime checkedAtUtc, CancellationToken cancellationToken) { return Task.Run(() => _inner.SaveIntegrityAsync(results, checkedAtUtc, cancellationToken).GetAwaiter().GetResult()); }
            public Task<InvoiceOverview> GetOverviewAsync(int year, int recentCount, CancellationToken cancellationToken) { return Task.Run(() => _inner.GetOverviewAsync(year, recentCount, cancellationToken).GetAwaiter().GetResult()); }
        }

        private sealed class MemoryFiles : IInvoiceFileStore, IInvoiceFileReader
        {
            private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            public void Put(string path, byte[] bytes)
            {
                _files[path] = bytes;
            }

            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName) { throw new NotSupportedException(); }
            public void DiscardTemp(string tempPath) { }
            public bool Exists(string storageRoot, string relativePath) { return _files.ContainsKey(relativePath); }
            public void Delete(string storageRoot, string relativePath) { }

            public byte[] Read(string storageRoot, string relativePath, long maxBytes)
            {
                byte[] bytes;
                if (!_files.TryGetValue(relativePath, out bytes)) throw new FileNotFoundException(relativePath);
                return bytes;
            }
        }

        private sealed class FixedContext : ICompanyContextAccessor
        {
            public FixedContext(CompanyContext current)
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
