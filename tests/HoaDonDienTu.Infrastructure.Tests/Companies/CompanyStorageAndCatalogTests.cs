using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Companies;
using HoaDonDienTu.Infrastructure.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Companies
{
    [TestFixture]
    public class CompanyStorageAndCatalogTests
    {
        private string _root;
        private FileSystemCompanyStorage _storage;
        private string _catalogPath;
        private JsonCompanyCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-company-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _storage = new FileSystemCompanyStorage();
            _catalogPath = Path.Combine(_root, "cfg", "companies.json");
            _catalog = new JsonCompanyCatalog(_catalogPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static CompanyCatalogItem Item(string mst, string name, string path, long invoices = 0)
        {
            return new CompanyCatalogItem(CompanyId.NewId(), TaxCode.Parse(mst), name, path, null, null, invoices, 0, CompanyCatalogStatus.Active);
        }

        private string Folder(string name)
        {
            return Path.Combine(_root, "data", name);
        }

        // ---- FileSystemCompanyStorage ----

        [Test]
        public void Inspect_MissingFolder_IsEmptyAndWritable_WithFreeSpace()
        {
            var info = _storage.Inspect(Folder("A"));

            Assert.That(info.Exists, Is.False);
            Assert.That(info.IsEmpty, Is.True);
            Assert.That(info.Writable, Is.True);
            Assert.That(info.HasIdentity, Is.False);
            Assert.That(info.FreeBytes, Is.GreaterThan(0));
        }

        [Test]
        public void Initialize_CreatesFolderAndIdentity_ThatInspectReadsBack()
        {
            var id = CompanyId.NewId();

            var result = _storage.Initialize(Folder("A"), id, TaxCode.Parse("0101234567"));
            var info = _storage.Inspect(Folder("A"));

            Assert.That(result.CreatedDirectory, Is.True);
            Assert.That(info.HasIdentity, Is.True);
            Assert.That(info.IdentityTaxCode.Value, Is.EqualTo("0101234567"));
            Assert.That(info.IdentityCompanyId, Is.EqualTo(id));
            Assert.That(info.IsEmpty, Is.False);
        }

        [Test]
        public void Initialize_Twice_RefusesToOverwriteExistingStorage()
        {
            var path = Folder("A");
            _storage.Initialize(path, CompanyId.NewId(), TaxCode.Parse("0101234567"));
            File.WriteAllText(Path.Combine(path, "company.db"), "important");

            Assert.Throws<InvalidOperationException>(() => _storage.Initialize(path, CompanyId.NewId(), TaxCode.Parse("0109999999")));

            Assert.That(File.ReadAllText(Path.Combine(path, "company.db")), Is.EqualTo("important"));
            Assert.That(_storage.Inspect(path).IdentityTaxCode.Value, Is.EqualTo("0101234567"));
        }

        [Test]
        public void Rollback_RemovesOnlyWhatInitializeCreated()
        {
            var created = Folder("New");
            var result = _storage.Initialize(created, CompanyId.NewId(), TaxCode.Parse("0101234567"));
            _storage.Rollback(created, result);
            Assert.That(Directory.Exists(created), Is.False);

            var existing = Folder("Existing");
            Directory.CreateDirectory(existing);
            File.WriteAllText(Path.Combine(existing, "keep.txt"), "mine");
            var result2 = _storage.Initialize(existing, CompanyId.NewId(), TaxCode.Parse("0101234567"));
            _storage.Rollback(existing, result2);

            Assert.That(Directory.Exists(existing), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(existing, "keep.txt")), Is.EqualTo("mine"));
            Assert.That(_storage.Inspect(existing).HasIdentity, Is.False);
        }

        [Test]
        public void Inspect_CorruptIdentity_ReportsProblem_NotIdentity()
        {
            var path = Folder("A");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, FileSystemCompanyStorage.IdentityFileName), "garbage");

            var info = _storage.Inspect(path);

            Assert.That(info.HasIdentity, Is.False);
            Assert.That(info.IdentityProblem, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Inspect_NonEmptyFolderWithoutIdentity_IsNotEmpty()
        {
            var path = Folder("A");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "x.txt"), "x");

            var info = _storage.Inspect(path);

            Assert.That(info.IsEmpty, Is.False);
            Assert.That(info.HasIdentity, Is.False);
        }

        [TestCase("C:\\bad|path")]
        [TestCase("Z:\\no-such-drive\\x")]
        public void Inspect_InvalidOrUnreachablePath_DoesNotThrow(string path)
        {
            StorageInspection info = null;

            Assert.DoesNotThrow(() => info = _storage.Inspect(path));

            Assert.That(info.Writable, Is.False);
        }

        // ---- JsonCompanyCatalog ----

        [Test]
        public void Catalog_Missing_IsEmptySnapshot()
        {
            var snapshot = _catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(snapshot.Status, Is.EqualTo(CatalogLoadStatus.NotFound));
            Assert.That(snapshot.Companies, Is.Empty);
        }

        [Test]
        public void Catalog_AddLoad_RoundTripsEveryField_WithoutSensitiveData()
        {
            var opened = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
            var item = new CompanyCatalogItem(CompanyId.NewId(), TaxCode.Parse("0100000000"), "Công ty ABC", Folder("A"),
                opened, opened.AddDays(-1), 1250, 734003200, CompanyCatalogStatus.Active);

            _catalog.AddAsync(item, CancellationToken.None).GetAwaiter().GetResult();
            var loaded = _catalog.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single();

            Assert.That(loaded.Id, Is.EqualTo(item.Id));
            Assert.That(loaded.DisplayName, Is.EqualTo("Công ty ABC"));
            Assert.That(loaded.LastOpenedAt, Is.EqualTo(opened));
            Assert.That(loaded.LastDownloadedAt, Is.EqualTo(opened.AddDays(-1)));
            Assert.That(loaded.CachedInvoiceCount, Is.EqualTo(1250));
            Assert.That(loaded.CachedStorageBytes, Is.EqualTo(734003200));

            var json = JObject.Parse(File.ReadAllText(_catalogPath));
            Assert.That((int)json["schemaVersion"], Is.EqualTo(1));
            var text = json.ToString().ToLowerInvariant();
            foreach (var forbidden in new[] { "token", "password", "licensekey", "cookie" }) Assert.That(text, Does.Not.Contain(forbidden));
        }

        [Test]
        public void Catalog_ServiceHistory_IsSetByTheSecondCompany_AndNeverClearedByRemoval()
        {
            var a = Item("0100000001", "A", Folder("A"));
            var b = Item("0100000002", "B", Folder("B"));
            _catalog.AddAsync(a, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(_catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().HasServiceHistory, Is.False);

            _catalog.AddAsync(b, CancellationToken.None).GetAwaiter().GetResult();
            _catalog.RemoveAsync(b.Id, CancellationToken.None).GetAwaiter().GetResult();
            var snapshot = _catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(snapshot.Companies.Count, Is.EqualTo(1));
            Assert.That(snapshot.HasServiceHistory, Is.True, "Đã từng chạy ServiceMode, không được tự rơi về Standard");
        }

        [Test]
        public void Catalog_DuplicateTaxCode_Rejected()
        {
            _catalog.AddAsync(Item("0100000001", "A", Folder("A")), CancellationToken.None).GetAwaiter().GetResult();

            var ex = Assert.Throws<CompanyCatalogException>(() =>
                _catalog.AddAsync(Item("0100000001", "A2", Folder("A2")), CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(ex.Kind, Is.EqualTo(CompanyCatalogErrorKind.DuplicateTaxCode));
        }

        [Test]
        public void Catalog_UpdateAndRemove_UnknownId_NotFound()
        {
            var ghost = Item("0100000009", "G", Folder("G"));

            Assert.That(Assert.Throws<CompanyCatalogException>(() => _catalog.UpdateAsync(ghost, CancellationToken.None).GetAwaiter().GetResult()).Kind,
                Is.EqualTo(CompanyCatalogErrorKind.NotFound));
            Assert.That(Assert.Throws<CompanyCatalogException>(() => _catalog.RemoveAsync(ghost.Id, CancellationToken.None).GetAwaiter().GetResult()).Kind,
                Is.EqualTo(CompanyCatalogErrorKind.NotFound));
        }

        [Test]
        public void Catalog_MissingFolder_ShowsPathMissing_WithoutPersistingIt()
        {
            var item = Item("0100000001", "A", Folder("never-created"));
            _catalog.AddAsync(item, CancellationToken.None).GetAwaiter().GetResult();

            var status = _catalog.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single().Status;

            Assert.That(status, Is.EqualTo(CompanyCatalogStatus.PathMissing));
            Assert.That(File.ReadAllText(_catalogPath), Does.Contain("\"status\": \"Active\""));
        }

        [Test]
        public void Catalog_CorruptMain_RecoversFromBackup_WithoutWritingAnything()
        {
            _catalog.AddAsync(Item("0100000001", "A", Folder("A")), CancellationToken.None).GetAwaiter().GetResult();
            _catalog.AddAsync(Item("0100000002", "B", Folder("B")), CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(_catalogPath, "{ truncated");

            var snapshot = _catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(snapshot.Status, Is.EqualTo(CatalogLoadStatus.Loaded));
            Assert.That(snapshot.RecoveredFromBackup, Is.True);
            Assert.That(snapshot.Companies.Count, Is.EqualTo(1), "Bản .bak là phiên bản hợp lệ trước lần ghi cuối");
            Assert.That(File.ReadAllText(_catalogPath), Is.EqualTo("{ truncated"), "Chỉ đọc, chưa ghi lại");
        }

        [Test]
        public void Catalog_BothCorrupt_ReportsCorrupt_AndRefusesToWrite()
        {
            _catalog.AddAsync(Item("0100000001", "A", Folder("A")), CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllText(_catalogPath, "garbage");
            File.WriteAllText(_catalogPath + AtomicJsonWriter.BackupSuffix, "also garbage");

            var snapshot = _catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            var ex = Assert.Throws<CompanyCatalogException>(() =>
                _catalog.AddAsync(Item("0100000003", "C", Folder("C")), CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(snapshot.Status, Is.EqualTo(CatalogLoadStatus.Corrupt));
            Assert.That(ex.Kind, Is.EqualTo(CompanyCatalogErrorKind.CatalogCorrupt));
            Assert.That(File.ReadAllText(_catalogPath), Is.EqualTo("garbage"), "Không ghi đè danh mục hỏng");
            Assert.Throws<CompanyCatalogException>(() => _catalog.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void Catalog_UnsupportedSchema_IsTreatedAsCorrupt()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_catalogPath));
            File.WriteAllText(_catalogPath, "{\"schemaVersion\":99,\"companies\":[]}");

            Assert.That(_catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().Status, Is.EqualTo(CatalogLoadStatus.Corrupt));
        }

        [Test]
        public void Catalog_ParallelAdds_AreSerialized_NoneLost()
        {
            var tasks = Enumerable.Range(1, 12)
                .Select(i => Task.Run(() => _catalog.AddAsync(Item("01000000" + i.ToString("00"), "C" + i, Folder("C" + i)), CancellationToken.None)))
                .ToArray();

            Task.WaitAll(tasks);

            Assert.That(_catalog.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Count, Is.EqualTo(12));
        }

        // ---- CompanyStorageOpener ----

        [Test]
        public void Opener_OpensRegisteredStorage_AndRecordsLastOpened()
        {
            var item = RegisterWithStorage("0100000001", "A");
            var opener = new CompanyStorageOpener(_catalog, _storage, new FixedClock(), new SilentLogger());

            var context = opener.OpenAsync(item.ToProfile(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(context.CompanyId, Is.EqualTo(item.Id));
            Assert.That(context.StorageRoot, Is.EqualTo(item.StoragePath));
            var stored = _catalog.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single();
            Assert.That(stored.LastOpenedAt, Is.EqualTo(new FixedClock().UtcNow));
        }

        [Test]
        public void Opener_FolderGone_IsStorageUnavailable()
        {
            var item = Item("0100000001", "A", Folder("gone"));
            _catalog.AddAsync(item, CancellationToken.None).GetAwaiter().GetResult();
            var opener = new CompanyStorageOpener(_catalog, _storage, new FixedClock(), new SilentLogger());

            var ex = Assert.Throws<CompanyStorageException>(() => opener.OpenAsync(item.ToProfile(), CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(ex.Blocker, Is.EqualTo(CompanySwitchBlocker.StorageUnavailable));
        }

        [Test]
        public void Opener_FolderWithoutIdentity_OrOfAnotherTaxCode_IsRefused()
        {
            var plain = Item("0100000001", "A", Folder("plain"));
            Directory.CreateDirectory(plain.StoragePath);
            var other = Item("0100000002", "B", Folder("other"));
            _storage.Initialize(other.StoragePath, other.Id, TaxCode.Parse("0109999999"));
            _catalog.AddAsync(plain, CancellationToken.None).GetAwaiter().GetResult();
            _catalog.AddAsync(other, CancellationToken.None).GetAwaiter().GetResult();
            var opener = new CompanyStorageOpener(_catalog, _storage, new FixedClock(), new SilentLogger());

            var e1 = Assert.Throws<CompanyStorageException>(() => opener.OpenAsync(plain.ToProfile(), CancellationToken.None).GetAwaiter().GetResult());
            var e2 = Assert.Throws<CompanyStorageException>(() => opener.OpenAsync(other.ToProfile(), CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(e1.Blocker, Is.EqualTo(CompanySwitchBlocker.DatabaseInvalid));
            Assert.That(e2.Blocker, Is.EqualTo(CompanySwitchBlocker.DatabaseInvalid));
        }

        [Test]
        public void Opener_UnknownCompany_IsStorageUnavailable()
        {
            var opener = new CompanyStorageOpener(_catalog, _storage, new FixedClock(), new SilentLogger());

            Assert.Throws<CompanyStorageException>(() =>
                opener.OpenAsync(new CompanyProfile(CompanyId.NewId(), "X", TaxCode.Parse("0100000001")), CancellationToken.None).GetAwaiter().GetResult());
        }

        private CompanyCatalogItem RegisterWithStorage(string mst, string name)
        {
            var item = Item(mst, name, Folder(name));
            _storage.Initialize(item.StoragePath, item.Id, item.TaxCode);
            _catalog.AddAsync(item, CancellationToken.None).GetAwaiter().GetResult();
            return item;
        }

        private sealed class FixedClock : HoaDonDienTu.Application.Abstractions.IClock
        {
            public DateTime UtcNow { get { return new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc); } }
        }

        private sealed class SilentLogger : HoaDonDienTu.Application.Abstractions.IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
