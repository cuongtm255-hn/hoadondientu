using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>Kiểm tra toàn vẹn kho bằng kho dữ liệu và file GIẢ (MOCK) trong bộ nhớ; không chạm đĩa thật hay cổng Thuế.</summary>
    [TestFixture]
    [Category("Mock")]
    public class InvoiceIntegrityCheckerTests
    {
        private const string Xml = "<HDon><DLHDon><TTChung><SHDon>0000128</SHDon></TTChung></DLHDon></HDon>";

        private InMemoryInvoiceRepository _invoices;
        private FakeDisk _disk;
        private FakeContext _context;
        private InvoiceIntegrityChecker _checker;
        private int _next;

        [SetUp]
        public void SetUp()
        {
            _invoices = new InMemoryInvoiceRepository();
            _disk = new FakeDisk();
            _context = new FakeContext { Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty mẫu", @"C:\Kho", null) };
            _checker = new InvoiceIntegrityChecker(_invoices, _disk, _disk, _context);
            _next = 0;
        }

        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        private async Task<StoredInvoice> AddAsync(string path, string storedContent, string diskContent)
        {
            var number = (++_next).ToString("0000000");
            var invoice = new NormalizedInvoice
            {
                Direction = InvoiceDirection.Purchase, InvoiceNumber = number, IssueDate = new DateTime(2026, 10, 31), FormSymbol = "1", InvoiceSymbol = "C26TAA",
                SellerTaxCode = "0109988776", SellerName = "Công ty ABC (mẫu)", BuyerTaxCode = "0101234567", BuyerName = "Công ty mẫu", StatusRaw = "1", CheckResultRaw = "5"
            };
            var stored = Encoding.UTF8.GetBytes(storedContent);
            var stub = new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice,
                FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow,
                Versions = new List<InvoiceVersionRecord>
                {
                    new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = Sha(stored), RelativePath = path, SizeBytes = stored.Length, CreatedAtUtc = DateTime.UtcNow, IsCurrent = true }
                }
            };
            await _invoices.AddAsync(stub, CancellationToken.None);
            if (diskContent != null) _disk.Files[path] = Encoding.UTF8.GetBytes(diskContent);
            return stub;
        }

        [Test]
        public async Task AllFilesIntact_ReportsCleanAndCountsVersions()
        {
            await AddAsync(@"2026\10\a.xml", Xml, Xml);
            await AddAsync(@"2026\10\b.xml", Xml + " ", Xml + " ");

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.IsClean, Is.True);
            Assert.That(report.VersionsChecked, Is.EqualTo(2));
        }

        [Test]
        public async Task MissingFile_IsReportedWithTheInvoiceIdentity()
        {
            var stored = await AddAsync(@"2026\10\gone.xml", Xml, null);

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            var problem = report.Problems.Single();
            Assert.That(problem.Kind, Is.EqualTo(IntegrityProblemKind.Missing));
            Assert.That(problem.InvoiceId, Is.EqualTo(stored.InvoiceId));
            Assert.That(problem.InvoiceNumber, Is.EqualTo(stored.Invoice.InvoiceNumber));
            Assert.That(problem.VersionNo, Is.EqualTo(1));
            Assert.That(problem.RelativePath, Is.EqualTo(@"2026\10\gone.xml"));
        }

        [Test]
        public async Task ChangedContent_IsAHashMismatch()
        {
            await AddAsync(@"2026\10\a.xml", Xml, Xml.Replace("0000128", "0000129"));

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.Problems.Single().Kind, Is.EqualTo(IntegrityProblemKind.HashMismatch));
        }

        [Test]
        public async Task MatchingHashButNotXml_IsNotXml()
        {
            await AddAsync(@"2026\10\a.xml", "<HDon><broken>", "<HDon><broken>");

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.Problems.Single().Kind, Is.EqualTo(IntegrityProblemKind.NotXml));
        }

        [Test]
        public async Task LockedFile_IsUnreadable_NotMissing()
        {
            await AddAsync(@"2026\10\a.xml", Xml, Xml);
            _disk.Locked.Add(@"2026\10\a.xml");

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.Problems.Single().Kind, Is.EqualTo(IntegrityProblemKind.Unreadable));
        }

        [Test]
        public async Task XmlFileNobodyPointsTo_IsAnOrphan_AndReferencedOnesAreNot()
        {
            await AddAsync(@"2026\10\a.xml", Xml, Xml);
            _disk.Files[@"2026\10\stray.xml"] = Encoding.UTF8.GetBytes(Xml);

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.Orphans, Is.EqualTo(new[] { @"2026\10\stray.xml" }));
            Assert.That(report.Problems, Is.Empty);
        }

        [Test]
        public async Task OrphanMatchingIgnoresCaseAndSlashStyle()
        {
            await AddAsync(@"2026/10/A.xml", Xml, null);
            _disk.Files[@"2026\10\a.XML"] = Encoding.UTF8.GetBytes(Xml);

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.Orphans, Is.Empty);
        }

        [Test]
        public async Task StrayTempFiles_AreListedSeparatelyFromOrphans()
        {
            _disk.Temps.Add(@"2026\10\0f3a.tmp");

            var report = await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(report.StrayTemps, Is.EqualTo(new[] { @"2026\10\0f3a.tmp" }));
            Assert.That(report.Orphans, Is.Empty);
            Assert.That(report.IsClean, Is.False);
        }

        [Test]
        public async Task ProgressCountsEveryVersion()
        {
            await AddAsync(@"a.xml", Xml, Xml);
            await AddAsync(@"b.xml", Xml, Xml);
            var seen = new List<int>();

            await _checker.CheckAsync(new SyncProgress(seen), CancellationToken.None);

            Assert.That(seen, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public async Task Cancelled_Throws()
        {
            await AddAsync(@"a.xml", Xml, Xml);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            AsyncTestDelegate act = () => _checker.CheckAsync(null, cts.Token);

            Assert.ThrowsAsync<OperationCanceledException>(act);
        }

        [Test]
        public void WithoutACompany_IsRejected()
        {
            _context.Current = null;

            Assert.ThrowsAsync<StorageGuardException>(() => _checker.CheckAsync(null, CancellationToken.None));
        }

        [Test]
        public async Task CheckingNeverChangesTheDisk()
        {
            await AddAsync(@"a.xml", Xml, Xml.Replace("0000128", "9"));
            _disk.Files[@"orphan.xml"] = Encoding.UTF8.GetBytes(Xml);
            var before = _disk.Files.Count;

            await _checker.CheckAsync(null, CancellationToken.None);

            Assert.That(_disk.Files.Count, Is.EqualTo(before));
        }

        private sealed class SyncProgress : IProgress<int>
        {
            private readonly List<int> _seen;

            public SyncProgress(List<int> seen)
            {
                _seen = seen;
            }

            public void Report(int value)
            {
                _seen.Add(value);
            }
        }

        private sealed class FakeDisk : IInvoiceFileReader, IInvoiceFileScanner
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Locked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Temps = new List<string>();

            public byte[] Read(string storageRoot, string relativePath, long maxBytes)
            {
                var key = relativePath.Replace('/', '\\');
                if (Locked.Contains(key)) throw new IOException("locked");
                byte[] found;
                if (!Files.TryGetValue(key, out found)) throw new FileNotFoundException("missing", relativePath);
                return found;
            }

            public IEnumerable<string> EnumerateXmlFiles(string storageRoot)
            {
                return Files.Keys.ToList();
            }

            public IEnumerable<string> EnumerateTempFiles(string storageRoot)
            {
                return Temps.ToList();
            }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }
    }
}
