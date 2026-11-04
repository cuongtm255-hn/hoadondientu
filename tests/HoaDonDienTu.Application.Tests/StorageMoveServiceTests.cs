using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>Đổi thư mục kho (T11-01): chép + kiểm + liên kết lại, thư mục cũ luôn được giữ. Toàn bộ dùng thư mục tạm và dữ liệu giả.</summary>
    [TestFixture]
    public class StorageMoveServiceTests
    {
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private string _base;
        private string _oldRoot;
        private CompanyId _id;
        private FakeRegistry _registry;
        private FakeManager _manager;
        private FakeStorage _storage;
        private FakeJobs _jobs;
        private FakeEntitlement _entitlement;
        private FakeDatabase _database;
        private StorageMoveService _service;

        [SetUp]
        public void SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-move-" + Guid.NewGuid().ToString("N"));
            _oldRoot = Path.Combine(_base, "cu");
            Directory.CreateDirectory(_oldRoot);
            _id = CompanyId.NewId();
            _registry = new FakeRegistry();
            _registry.Items.Add(new CompanyCatalogItem(_id, Mst, "Công ty mẫu", _oldRoot, null, null, 0, 0, CompanyCatalogStatus.Active));
            _manager = new FakeManager(_registry) { Current = new CompanyContext(_id, Mst, "Công ty mẫu", _oldRoot, null) };
            _storage = new FakeStorage();
            _jobs = new FakeJobs();
            _entitlement = new FakeEntitlement();
            _database = new FakeDatabase();
            _service = new StorageMoveService(_manager, _registry, _storage, _jobs, _entitlement, _database, new NullLogger());
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

        private string NewRoot(string name = "moi")
        {
            return Path.Combine(_base, name);
        }

        private void Put(string relative, string content)
        {
            var path = Path.Combine(_oldRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        }

        private void SeedStore()
        {
            Put("2026\\10\\a.xml", "<HDon>a</HDon>");
            Put("2026\\10\\b.xml", "<HDon>b - tiếng Việt</HDon>");
            Put("2026\\11\\c.pdf", "%PDF-giả");
            Put("Backups\\goi.zip", "zip giả");
            Put("Data\\company.db", "DB-CU");
            Put("store.identity", "{\"mst\":\"0101234567\"}");
            Put("2026\\10\\dang-ghi.tmp", "rác");
        }

        // ---- Kiểm tra thư mục mới ----

        [Test]
        public async Task Check_AcceptsAnEmptyOrMissingFolder()
        {
            Assert.That(await _service.CheckAsync(NewRoot(), CancellationToken.None), Is.Empty);
            Directory.CreateDirectory(NewRoot("rong"));
            Assert.That(await _service.CheckAsync(NewRoot("rong"), CancellationToken.None), Is.Empty);
        }

        [Test]
        public async Task Check_RefusesTheCurrentFolder_NestedFolders_AndInvalidPaths()
        {
            Assert.That((await _service.CheckAsync(_oldRoot, CancellationToken.None)).Single(), Does.Contain("đang là thư mục kho hiện tại"));
            Assert.That((await _service.CheckAsync(_oldRoot + "\\", CancellationToken.None)).Single(), Does.Contain("đang là thư mục kho hiện tại"));
            Assert.That((await _service.CheckAsync(Path.Combine(_oldRoot, "con"), CancellationToken.None)).Single(), Does.Contain("nằm trong"));
            Assert.That((await _service.CheckAsync(_base, CancellationToken.None)).Single(), Does.Contain("chứa thư mục kho hiện tại"));
            Assert.That((await _service.CheckAsync("   ", CancellationToken.None)).Single(), Does.Contain("không hợp lệ"));
            Assert.That((await _service.CheckAsync("D:\\a<>b|c", CancellationToken.None)).Single(), Does.Contain("không hợp lệ"));
        }

        [Test]
        public async Task Check_RefusesANonEmptyFolder_AnUnwritableOne_AndTooLittleSpace()
        {
            Directory.CreateDirectory(NewRoot("co-du-lieu"));
            File.WriteAllText(Path.Combine(NewRoot("co-du-lieu"), "x.txt"), "x");
            Assert.That((await _service.CheckAsync(NewRoot("co-du-lieu"), CancellationToken.None)).Single(), Does.Contain("phải trống"));

            _storage.Writable = false;
            _storage.WriteProblem = "bị từ chối quyền";
            Assert.That((await _service.CheckAsync(NewRoot("ro"), CancellationToken.None)).Single(), Does.Contain("Không ghi được").And.Contain("bị từ chối quyền"));

            _storage.Writable = true;
            _storage.Free = 10L * 1024 * 1024;
            SeedStore();
            Assert.That((await _service.CheckAsync(NewRoot("day"), CancellationToken.None)).Single(), Does.Contain("không đủ chỗ"));
        }

        [Test]
        public async Task Check_RefusesAFolderThatOverlapsAnotherCompany()
        {
            var other = Path.Combine(_base, "khac");
            _registry.Items.Add(new CompanyCatalogItem(CompanyId.NewId(), TaxCode.Parse("0109988776"), "Công ty khác", other, null, null, 0, 0, CompanyCatalogStatus.Active));

            Assert.That((await _service.CheckAsync(other, CancellationToken.None)).Single(), Does.Contain("Công ty khác"));
            Assert.That((await _service.CheckAsync(Path.Combine(other, "con"), CancellationToken.None)).Single(), Does.Contain("Công ty khác"));
        }

        // ---- Đổi thư mục ----

        [Test]
        public async Task Move_CopiesTheStore_SnapshotsTheDatabase_Relinks_AndKeepsTheOldFolderUntouched()
        {
            SeedStore();
            var seen = new List<int>();
            var total = 0;

            var result = await _service.MoveAsync(NewRoot(), new SyncProgress(p => { seen.Add(p.Done); total = p.Total; }), CancellationToken.None);

            var moved = NewRoot();
            Assert.That(File.ReadAllText(Path.Combine(moved, "2026\\10\\a.xml")), Is.EqualTo("<HDon>a</HDon>"));
            Assert.That(File.ReadAllText(Path.Combine(moved, "2026\\10\\b.xml")), Is.EqualTo("<HDon>b - tiếng Việt</HDon>"));
            Assert.That(File.ReadAllText(Path.Combine(moved, "2026\\11\\c.pdf")), Is.EqualTo("%PDF-giả"));
            Assert.That(File.Exists(Path.Combine(moved, "Backups\\goi.zip")), Is.True, "Gói sao lưu cũng đi theo kho");
            Assert.That(File.Exists(Path.Combine(moved, "store.identity")), Is.True, "File định danh kho đi theo");
            Assert.That(File.ReadAllBytes(Path.Combine(moved, "Data\\company.db")), Is.EqualTo(_database.Content), "Cơ sở dữ liệu là bản chụp nhất quán, không phải chép nguyên file đang mở");
            Assert.That(File.Exists(Path.Combine(moved, "2026\\10\\dang-ghi.tmp")), Is.False, "Không chép file tạm");
            Assert.That(Directory.GetFiles(moved, "*.copying", SearchOption.AllDirectories), Is.Empty);

            // Thư mục cũ còn nguyên, kể cả cơ sở dữ liệu cũ.
            Assert.That(File.ReadAllText(Path.Combine(_oldRoot, "Data\\company.db")), Is.EqualTo("DB-CU"));
            Assert.That(File.ReadAllText(Path.Combine(_oldRoot, "2026\\10\\a.xml")), Is.EqualTo("<HDon>a</HDon>"));
            Assert.That(File.Exists(Path.Combine(_oldRoot, "2026\\10\\dang-ghi.tmp")), Is.True);

            Assert.That(_registry.Items.Single().StoragePath, Is.EqualTo(moved));
            Assert.That(_manager.Current.StorageRoot, Is.EqualTo(moved), "Đã mở lại kho ở thư mục mới");
            Assert.That(result.OldPath, Is.EqualTo(_oldRoot));
            Assert.That(result.NewPath, Is.EqualTo(moved));
            Assert.That(result.FileCount, Is.EqualTo(6), "5 file + bản chụp cơ sở dữ liệu");
            Assert.That(seen.Last(), Is.EqualTo(total));
            Assert.That(seen, Is.Ordered);
        }

        [Test]
        public async Task Move_IntoAnExistingEmptyFolder_KeepsThatFolder()
        {
            SeedStore();
            Directory.CreateDirectory(NewRoot("co-san"));

            await _service.MoveAsync(NewRoot("co-san"), null, CancellationToken.None);

            Assert.That(File.Exists(Path.Combine(NewRoot("co-san"), "2026\\10\\a.xml")), Is.True);
        }

        [Test]
        public async Task Move_Cancelled_RemovesWhatWasCopied_AndKeepsUsingTheOldFolder()
        {
            SeedStore();
            var cts = new CancellationTokenSource();

            Assert.ThrowsAsync<OperationCanceledException>(() => _service.MoveAsync(NewRoot(), new SyncProgress(p => { if (p.Done == 3) cts.Cancel(); }), cts.Token));

            Assert.That(Directory.Exists(NewRoot()), Is.False, "Thư mục mới do ứng dụng tạo thì được xóa hẳn");
            Assert.That(_registry.Items.Single().StoragePath, Is.EqualTo(_oldRoot));
            Assert.That(_manager.Current.StorageRoot, Is.EqualTo(_oldRoot));
            Assert.That(File.ReadAllText(Path.Combine(_oldRoot, "Data\\company.db")), Is.EqualTo("DB-CU"));
        }

        [Test]
        public async Task Move_Cancelled_IntoAnExistingEmptyFolder_LeavesItEmpty()
        {
            SeedStore();
            Directory.CreateDirectory(NewRoot("co-san"));
            var cts = new CancellationTokenSource();

            Assert.ThrowsAsync<OperationCanceledException>(() => _service.MoveAsync(NewRoot("co-san"), new SyncProgress(p => { if (p.Done == 3) cts.Cancel(); }), cts.Token));

            Assert.That(Directory.Exists(NewRoot("co-san")), Is.True);
            Assert.That(Directory.GetFileSystemEntries(NewRoot("co-san")), Is.Empty);
        }

        [Test]
        public async Task Move_WhenTheDatabaseSnapshotFails_CleansUp_AndSaysTheOldFolderIsStillUsed()
        {
            SeedStore();
            _database.SnapshotFails = true;

            var ex = Assert.ThrowsAsync<StorageMoveException>(() => _service.MoveAsync(NewRoot(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("vẫn dùng thư mục cũ"));
            Assert.That(Directory.Exists(NewRoot()), Is.False);
            Assert.That(_registry.Items.Single().StoragePath, Is.EqualTo(_oldRoot));
            Assert.That(_manager.Current.StorageRoot, Is.EqualTo(_oldRoot));
        }

        [Test]
        public async Task Move_WhenTheNewStoreCannotBeOpened_GoesBackToTheOldFolder_AndCleansUp()
        {
            SeedStore();
            _manager.FailOpenAt = NewRoot();

            var ex = Assert.ThrowsAsync<StorageMoveException>(() => _service.MoveAsync(NewRoot(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("quay lại dùng thư mục cũ"));
            Assert.That(_registry.Items.Single().StoragePath, Is.EqualTo(_oldRoot), "Danh mục được trả về như cũ");
            Assert.That(_manager.HasCurrent, Is.True);
            Assert.That(_manager.Current.StorageRoot, Is.EqualTo(_oldRoot), "Đã mở lại kho cũ");
            Assert.That(Directory.Exists(NewRoot()), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(_oldRoot, "2026\\10\\a.xml")), Is.EqualTo("<HDon>a</HDon>"));
        }

        [Test]
        public async Task Move_WhileADownloadRuns_IsRefused_AndChangesNothing()
        {
            SeedStore();
            _jobs.Running = true;

            var ex = Assert.ThrowsAsync<StorageMoveException>(() => _service.MoveAsync(NewRoot(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("phiên tải"));
            Assert.That(Directory.Exists(NewRoot()), Is.False);
            Assert.That(_registry.Items.Single().StoragePath, Is.EqualTo(_oldRoot));
        }

        [Test]
        public async Task Move_WithoutTheSettingsEntitlement_IsDenied_AndChangesNothing()
        {
            SeedStore();
            _entitlement.Denied.Add(AppFeature.ManageSettings);

            Assert.ThrowsAsync<EntitlementDeniedException>(() => _service.MoveAsync(NewRoot(), null, CancellationToken.None));

            Assert.That(Directory.Exists(NewRoot()), Is.False);
        }

        [Test]
        public async Task Move_ToAnInvalidFolder_IsRefusedBeforeCopyingAnything()
        {
            SeedStore();

            var ex = Assert.ThrowsAsync<StorageMoveException>(() => _service.MoveAsync(_oldRoot, null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("thư mục kho hiện tại"));
            Assert.That(_database.Snapshots, Is.EqualTo(0));
        }

        // ---- Fakes ----

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private sealed class SyncProgress : IProgress<BackupProgress>
        {
            private readonly Action<BackupProgress> _action;

            public SyncProgress(Action<BackupProgress> action)
            {
                _action = action;
            }

            public void Report(BackupProgress value)
            {
                _action(value);
            }
        }

        private sealed class FakeRegistry : ICompanyRegistry
        {
            public readonly List<CompanyCatalogItem> Items = new List<CompanyCatalogItem>();

            public Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task<IList<CompanyCatalogItem>> GetItemsAsync(CancellationToken cancellationToken) { return Task.FromResult<IList<CompanyCatalogItem>>(Items.ToList()); }
            public Task AddAsync(CompanyCatalogItem item, CancellationToken cancellationToken) { Items.Add(item); return Task.FromResult(0); }

            public Task UpdateAsync(CompanyCatalogItem item, CancellationToken cancellationToken)
            {
                var index = Items.FindIndex(i => i.Id.Equals(item.Id));
                if (index < 0) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "x");
                Items[index] = item;
                return Task.FromResult(0);
            }

            public Task RemoveAsync(CompanyId id, CancellationToken cancellationToken) { Items.RemoveAll(i => i.Id.Equals(id)); return Task.FromResult(0); }
        }

        private sealed class FakeManager : ICompanyContextManager
        {
            private readonly FakeRegistry _registry;

            public FakeManager(FakeRegistry registry)
            {
                _registry = registry;
            }

            public string FailOpenAt;

            public event EventHandler ContextChanged;

            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }

            public Task<CompanyContext> OpenAsync(CompanyProfile target, CancellationToken cancellationToken)
            {
                if (Current != null) throw new InvalidOperationException("Phải đóng trước.");
                var path = _registry.Items.First(i => i.Id.Equals(target.Id)).StoragePath;
                if (FailOpenAt != null && string.Equals(path, FailOpenAt, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CompanyStorageException(CompanySwitchBlocker.StorageUnavailable, "kho mới không mở được (giả)");
                }

                Current = new CompanyContext(target.Id, target.TaxCode, target.DisplayName, path, null);
                Raise();
                return Task.FromResult(Current);
            }

            public void CloseCurrent()
            {
                Current = null;
                Raise();
            }

            private void Raise()
            {
                var handler = ContextChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        /// <summary>Kiểm tra thật trên đĩa (tồn tại/trống) còn quyền ghi và chỗ trống do test quyết định.</summary>
        private sealed class FakeStorage : ICompanyStorage
        {
            public bool Writable = true;
            public string WriteProblem;
            public long? Free = 500L * 1024 * 1024 * 1024;

            public StorageInspection Inspect(string path)
            {
                var exists = Directory.Exists(path);
                return new StorageInspection
                {
                    Exists = exists,
                    IsEmpty = !exists || !Directory.EnumerateFileSystemEntries(path).Any(),
                    Writable = Writable,
                    WriteProblem = WriteProblem,
                    FreeBytes = Free
                };
            }

            public StorageInitResult Initialize(string path, CompanyId companyId, TaxCode taxCode) { throw new NotSupportedException(); }
            public void Rollback(string path, StorageInitResult result) { throw new NotSupportedException(); }
        }

        private sealed class FakeJobs : IActiveJobService
        {
            public bool Running;
            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(Running); }
            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
        }

        private sealed class FakeEntitlement : IEntitlementService
        {
            public readonly HashSet<AppFeature> Denied = new HashSet<AppFeature>();

            public AppMode CurrentMode { get { return AppMode.Standard; } }
            public bool IsAllowed(AppFeature feature) { return !Denied.Contains(feature); }
            public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }

        private sealed class FakeDatabase : ICompanyDatabaseBackup
        {
            public byte[] Content = new byte[] { 9, 8, 7, 6, 5 };
            public bool SnapshotFails;
            public int Snapshots;

            public int LatestSchemaVersion { get { return 4; } }

            public Task<int> SnapshotAsync(string destinationFile, CancellationToken cancellationToken)
            {
                Snapshots++;
                if (SnapshotFails) throw new BackupException("Không chụp được cơ sở dữ liệu (giả).");
                File.WriteAllBytes(destinationFile, Content);
                return Task.FromResult(4);
            }

            public Task ValidateAsync(string sourceFile, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task ReplaceAsync(string sourceFile, CancellationToken cancellationToken) { throw new NotSupportedException(); }
        }
    }
}
