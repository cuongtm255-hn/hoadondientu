using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>Khôi phục danh mục doanh nghiệp bằng cách quét thư mục (T02-07, NFR-REL-007). Thư mục tạm và định danh giả.</summary>
    [TestFixture]
    public class CompanyRecoveryServiceTests
    {
        private string _base;
        private string _companies;
        private FakeRegistry _registry;
        private FakeScanner _scanner;
        private FakeRepair _repair;
        private CompanyRecoveryService _service;

        [SetUp]
        public void SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-recovery-" + Guid.NewGuid().ToString("N"));
            _companies = Path.Combine(_base, "Companies");
            Directory.CreateDirectory(_companies);
            _registry = new FakeRegistry();
            _scanner = new FakeScanner();
            _repair = new FakeRepair(_registry);
            _service = new CompanyRecoveryService(_registry, _scanner, _repair, new NullLogger());
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

        private string Store(string folder, string tax, CompanyId? id = null, string problem = null)
        {
            var path = Path.Combine(_companies, folder);
            Directory.CreateDirectory(path);
            _scanner.Add(path, tax == null ? null : TaxCode.Parse(tax), id ?? CompanyId.NewId(), problem);
            return path;
        }

        [Test]
        public async Task Scan_FindsStoresWithAValidIdentity_InTheFolderAndItsDirectChildren()
        {
            var a = Store("0101234567", "0101234567");
            var b = Store("0109988776", "0109988776");
            Directory.CreateDirectory(Path.Combine(_companies, "khong-phai-kho"));
            Directory.CreateDirectory(Path.Combine(_companies, "0101234567", "con-cua-kho"));

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Select(s => s.Path), Is.EquivalentTo(new[] { a, b }));
            Assert.That(scan.Stores.All(s => s.CanRegister), Is.True);
            Assert.That(scan.Skipped, Is.Empty);
        }

        [Test]
        public async Task Scan_PicksUpAStoreChosenDirectly()
        {
            var a = Store("0101234567", "0101234567");

            var scan = await _service.ScanAsync(new[] { a }, CancellationToken.None);

            Assert.That(scan.Stores.Single().Path, Is.EqualTo(a));
        }

        [Test]
        public async Task Scan_ReportsFoldersWhoseIdentityIsDamaged_AndDoesNotOfferThem()
        {
            Store("0101234567", "0101234567");
            Store("hong", null, problem: "file định danh bị hỏng");

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Count, Is.EqualTo(1));
            Assert.That(scan.Skipped.Single(), Does.Contain("hong").And.Contain("định danh bị hỏng"));
        }

        [Test]
        public async Task Scan_MarksStoresAlreadyInTheCatalog_ByCompanyIdOrFolder()
        {
            var id = CompanyId.NewId();
            var known = Store("cu", "0101234567", id);
            var other = Store("moi", "0109988776");
            _registry.Items.Add(new CompanyCatalogItem(id, TaxCode.Parse("0101234567"), "Đã có", known, null, null, 0, 0, CompanyCatalogStatus.Active));

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Single(s => s.Path == known).AlreadyRegistered, Is.True);
            Assert.That(scan.Stores.Single(s => s.Path == known).CanRegister, Is.False);
            Assert.That(scan.Stores.Single(s => s.Path == other).CanRegister, Is.True);
        }

        [Test]
        public async Task Scan_TwoFoldersWithTheSameTaxCode_OnlyOneCanBeRegistered()
        {
            Store("a", "0101234567");
            Store("b", "0101234567");

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Count(s => s.CanRegister), Is.EqualTo(1));
            Assert.That(scan.Stores.Single(s => !s.CanRegister).Problem, Does.Contain("Trùng MST"));
        }

        [Test]
        public async Task Scan_ATaxCodeAlreadyRegisteredElsewhere_IsNotReplaced()
        {
            _registry.Items.Add(new CompanyCatalogItem(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Ở thư mục khác", Path.Combine(_base, "khac"), null, null, 0, 0, CompanyCatalogStatus.Active));
            Store("ban-sao", "0101234567");

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Single().CanRegister, Is.False);
            Assert.That(scan.Stores.Single().Problem, Does.Contain("đã có trong danh mục"));
        }

        [Test]
        public async Task Scan_WorksWhenTheCatalogIsCorrupt_AndNeverWritesAnything()
        {
            Store("0101234567", "0101234567");
            _registry.Corrupt = true;

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Single().CanRegister, Is.True);
            Assert.That(_registry.Added, Is.Empty);
            Assert.That(_repair.Quarantines, Is.EqualTo(0));
        }

        [Test]
        public async Task Scan_OfMissingOrBlankFolders_IsEmptyNotAnError()
        {
            var scan = await _service.ScanAsync(new[] { Path.Combine(_base, "khong-co"), "  ", null }, CancellationToken.None);

            Assert.That(scan.Stores, Is.Empty);
        }

        [Test]
        public async Task Rebuild_OnACorruptCatalog_KeepsTheCorruptFile_ThenRegistersTheStores()
        {
            var a = Store("0101234567", "0101234567");
            var b = Store("0109988776", "0109988776");
            _registry.Corrupt = true;
            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            var result = await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            Assert.That(result.Registered, Is.EqualTo(2));
            Assert.That(result.QuarantinedCatalog, Is.EqualTo("companies.json.corrupt-test"));
            Assert.That(_repair.Quarantines, Is.EqualTo(1));
            Assert.That(_registry.Items.Select(i => i.StoragePath), Is.EquivalentTo(new[] { a, b }));
            Assert.That(_registry.Items.All(i => i.Status == CompanyCatalogStatus.Active), Is.True);
            Assert.That(_registry.Items.Single(i => i.StoragePath == a).DisplayName, Does.Contain("0101234567"), "Tên tạm theo MST");
        }

        [Test]
        public async Task Rebuild_OnAHealthyCatalog_DoesNotQuarantineAnything_AndSkipsWhatCannotBeRegistered()
        {
            var id = CompanyId.NewId();
            var known = Store("cu", "0101234567", id);
            Store("moi", "0109988776");
            _registry.Items.Add(new CompanyCatalogItem(id, TaxCode.Parse("0101234567"), "Đã có", known, null, null, 0, 0, CompanyCatalogStatus.Active));
            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            var result = await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            Assert.That(result.Registered, Is.EqualTo(1));
            Assert.That(result.QuarantinedCatalog, Is.Null);
            Assert.That(_repair.Quarantines, Is.EqualTo(0));
            Assert.That(result.NotRegistered.Single(), Does.Contain("đã có trong danh mục"));
            Assert.That(_registry.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Rebuild_ADuplicateTaxCodeRaisedByTheCatalog_IsReportedNotThrown()
        {
            var a = Store("a", "0101234567");
            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);
            // Sau lúc quét có người đăng ký cùng MST.
            _registry.Items.Add(new CompanyCatalogItem(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Đến trước", Path.Combine(_base, "x"), null, null, 0, 0, CompanyCatalogStatus.Active));

            var result = await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            Assert.That(result.Registered, Is.EqualTo(0));
            Assert.That(result.NotRegistered.Single(), Does.Contain(a));
        }

        [Test]
        public async Task Rebuild_NeverTouchesTheStoreFolders()
        {
            var a = Store("0101234567", "0101234567");
            File.WriteAllText(Path.Combine(a, "2026.xml"), "<x/>");
            var before = Directory.GetFileSystemEntries(a, "*", SearchOption.AllDirectories).OrderBy(x => x).ToList();
            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            Assert.That(Directory.GetFileSystemEntries(a, "*", SearchOption.AllDirectories).OrderBy(x => x).ToList(), Is.EqualTo(before));
            Assert.That(File.ReadAllText(Path.Combine(a, "2026.xml")), Is.EqualTo("<x/>"));
        }

        // ---- Fakes ----

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private sealed class FakeScanner : ICompanyStorageScanner
        {
            private readonly Dictionary<string, StorageInspection> _byPath = new Dictionary<string, StorageInspection>(StringComparer.OrdinalIgnoreCase);

            public void Add(string path, TaxCode tax, CompanyId id, string problem)
            {
                _byPath[path] = new StorageInspection
                {
                    Exists = true,
                    HasIdentity = tax != null,
                    IdentityTaxCode = tax,
                    IdentityCompanyId = tax == null ? (CompanyId?)null : id,
                    IdentityProblem = problem
                };
            }

            public StorageInspection ReadIdentity(string path)
            {
                StorageInspection found;
                return _byPath.TryGetValue(path, out found) ? found : new StorageInspection { Exists = Directory.Exists(path) };
            }
        }

        private sealed class FakeRegistry : ICompanyRegistry
        {
            public readonly List<CompanyCatalogItem> Items = new List<CompanyCatalogItem>();
            public readonly List<CompanyCatalogItem> Added = new List<CompanyCatalogItem>();
            public bool Corrupt;

            public Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken) { throw new NotSupportedException(); }

            public Task<IList<CompanyCatalogItem>> GetItemsAsync(CancellationToken cancellationToken)
            {
                if (Corrupt) throw new CompanyCatalogException(CompanyCatalogErrorKind.CatalogCorrupt, "hỏng");
                return Task.FromResult<IList<CompanyCatalogItem>>(Items.ToList());
            }

            public Task AddAsync(CompanyCatalogItem item, CancellationToken cancellationToken)
            {
                if (Corrupt) throw new CompanyCatalogException(CompanyCatalogErrorKind.CatalogCorrupt, "hỏng");
                if (Items.Any(i => i.TaxCode.Equals(item.TaxCode))) throw new CompanyCatalogException(CompanyCatalogErrorKind.DuplicateTaxCode, "trùng");
                Items.Add(item);
                Added.Add(item);
                return Task.FromResult(0);
            }

            public Task UpdateAsync(CompanyCatalogItem item, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task RemoveAsync(CompanyId id, CancellationToken cancellationToken) { throw new NotSupportedException(); }
        }

        private sealed class FakeRepair : ICompanyCatalogRepair
        {
            private readonly FakeRegistry _registry;

            public FakeRepair(FakeRegistry registry)
            {
                _registry = registry;
            }

            public int Quarantines;

            public Task<string> QuarantineCorruptAsync(CancellationToken cancellationToken)
            {
                if (!_registry.Corrupt) return Task.FromResult<string>(null);
                Quarantines++;
                _registry.Corrupt = false;
                _registry.Items.Clear();
                return Task.FromResult("companies.json.corrupt-test");
            }
        }
    }
}
