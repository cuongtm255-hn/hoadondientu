using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Companies
{
    /// <summary>Dịch vụ quản lý doanh nghiệp chạy trên danh mục JSON + kho thật (thư mục tạm), không mock.</summary>
    [TestFixture]
    public class CompanyManagementServiceTests
    {
        private string _root;
        private JsonCompanyCatalog _catalog;
        private FileSystemCompanyStorage _storage;
        private Mode _entitlement;
        private Accessor _context;
        private CompanyManagementService _service;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-mgmt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _catalog = new JsonCompanyCatalog(Path.Combine(_root, "cfg", "companies.json"));
            _storage = new FileSystemCompanyStorage();
            _entitlement = new Mode { Value = AppMode.Service };
            _context = new Accessor();
            _service = new CompanyManagementService(_catalog, _storage, _context, _entitlement, new Silent());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string Folder(string name)
        {
            return Path.Combine(_root, "data", name);
        }

        private static CompanyInput Input(string mst, string name, string path)
        {
            return new CompanyInput { TaxCodeText = mst, DisplayName = name, StoragePath = path };
        }

        private CompanyCatalogItem Create(string mst, string name)
        {
            return _service.CreateAsync(Input(mst, name, Folder(mst)), CancellationToken.None).GetAwaiter().GetResult();
        }

        private IList<CompanyCatalogItem> Items()
        {
            return _catalog.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        // ---- kiểm tra trước khi tạo ----

        [Test]
        public void Validate_ValidInput_PassesAllFiveChecks_WithHumanMessages()
        {
            var validation = _service.ValidateAsync(Input("0101234567", "Công ty Mẫu", Folder("0101234567")), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Checks.Select(c => c.Key), Is.EquivalentTo(Enum.GetValues(typeof(CompanyCheckKey)).Cast<CompanyCheckKey>()));
            Assert.That(validation.Checks.Single(c => c.Key == CompanyCheckKey.Folder).Message, Does.Contain("còn trống"));
        }

        [TestCase("123", "Tên", "ok", CompanyCheckKey.TaxCodeFormat)]
        [TestCase("0101234567", "   ", "ok", CompanyCheckKey.DisplayName)]
        [TestCase("0101234567", "Tên", "relative\\path", CompanyCheckKey.Folder)]
        [TestCase("0101234567", "Tên", "", CompanyCheckKey.Folder)]
        public void Validate_BadField_FailsThatCheck(string mst, string name, string pathMarker, CompanyCheckKey failing)
        {
            var path = pathMarker == "ok" ? Folder("x") : pathMarker;

            var validation = _service.ValidateAsync(Input(mst, name, path), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Checks.Single(c => c.Key == failing).Passed, Is.False);
        }

        [Test]
        public void Validate_DuplicateTaxCode_NamesTheExistingCompany()
        {
            Create("0100000001", "Công ty Một");

            var validation = _service.ValidateAsync(Input("0100000001", "Khác", Folder("other")), CancellationToken.None).GetAwaiter().GetResult();

            var check = validation.Checks.Single(c => c.Key == CompanyCheckKey.TaxCodeUnique);
            Assert.That(check.Passed, Is.False);
            Assert.That(check.Message, Does.Contain("Công ty Một"));
        }

        [Test]
        public void Validate_FolderNestedInsideOrAroundAnotherCompany_IsRejected()
        {
            Create("0100000001", "Một");

            var inside = _service.ValidateAsync(Input("0100000002", "Hai", Path.Combine(Folder("0100000001"), "sub")), CancellationToken.None).GetAwaiter().GetResult();
            var around = _service.ValidateAsync(Input("0100000002", "Hai", Path.GetDirectoryName(Folder("0100000001"))), CancellationToken.None).GetAwaiter().GetResult();
            var same = _service.ValidateAsync(Input("0100000002", "Hai", Folder("0100000001") + "\\"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(inside.IsValid, Is.False);
            Assert.That(around.IsValid, Is.False);
            Assert.That(same.IsValid, Is.False);
            Assert.That(inside.FirstError, Does.Contain("Một"));
        }

        [Test]
        public void Validate_NonEmptyFolderThatIsNotAStorage_IsRejected()
        {
            var path = Folder("junk");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "my-documents.docx"), "x");

            var validation = _service.ValidateAsync(Input("0100000001", "A", path), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(validation.Checks.Single(c => c.Key == CompanyCheckKey.Folder).Passed, Is.False);
        }

        [Test]
        public void Validate_FolderBelongingToAnotherTaxCode_IsRejected_NamingBothTaxCodes()
        {
            var path = Folder("belongs");
            _storage.Initialize(path, CompanyId.NewId(), TaxCode.Parse("0109999999"));

            var validation = _service.ValidateAsync(Input("0100000001", "A", path), CancellationToken.None).GetAwaiter().GetResult();

            var message = validation.Checks.Single(c => c.Key == CompanyCheckKey.Folder).Message;
            Assert.That(message, Does.Contain("0109999999").And.Contain("0100000001"));
        }

        [Test]
        public void Validate_StandardWithOneCompany_LicenceCheckFails()
        {
            Create("0100000001", "Một");
            _entitlement.Value = AppMode.Standard;

            var validation = _service.ValidateAsync(Input("0100000002", "Hai", Folder("two")), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(validation.Checks.Single(c => c.Key == CompanyCheckKey.Licence).Passed, Is.False);
        }

        // ---- tạo (hai pha) ----

        [Test]
        public void Create_FirstRunInStandard_Succeeds_CreatesStorageAndCatalogEntry()
        {
            _entitlement.Value = AppMode.Standard;

            var item = Create("0101234567", "  Công ty Mẫu  ");

            Assert.That(item.DisplayName, Is.EqualTo("Công ty Mẫu"));
            Assert.That(item.Status, Is.EqualTo(CompanyCatalogStatus.Active));
            Assert.That(Items().Single().TaxCode.Value, Is.EqualTo("0101234567"));
            var info = _storage.Inspect(item.StoragePath);
            Assert.That(info.IdentityTaxCode.Value, Is.EqualTo("0101234567"));
            Assert.That(info.IdentityCompanyId, Is.EqualTo(item.Id));
        }

        [Test]
        public void Create_SecondCompanyInStandard_IsDenied_AC12_AndLeavesNoTraceOnDisk()
        {
            _entitlement.Value = AppMode.Standard;
            Create("0100000001", "Một");

            Assert.Throws<EntitlementDeniedException>(() => Create("0100000002", "Hai"));

            Assert.That(Items().Count, Is.EqualTo(1));
            Assert.That(Directory.Exists(Folder("0100000002")), Is.False);
        }

        [Test]
        public void Create_ReadOnly_SecondCompanyIsDenied()
        {
            Create("0100000001", "Một");
            _entitlement.Value = AppMode.ReadOnlyService;

            Assert.Throws<EntitlementDeniedException>(() => Create("0100000002", "Hai"));
        }

        [Test]
        public void Create_InvalidInput_ThrowsValidationException_AndCreatesNothing()
        {
            var ex = Assert.Throws<CompanyValidationException>(() => Create("abc", "A"));

            Assert.That(ex.Validation.IsValid, Is.False);
            Assert.That(Items(), Is.Empty);
            Assert.That(Directory.Exists(Folder("abc")), Is.False);
        }

        [Test]
        public void Create_ServiceMode_ManyCompanies_EachWithItsOwnStorage_AndServiceHistory()
        {
            var a = Create("0100000001", "A");
            var b = Create("0100000002", "B");

            Assert.That(a.StoragePath, Is.Not.EqualTo(b.StoragePath));
            Assert.That(_catalog.LoadAsync(CancellationToken.None).GetAwaiter().GetResult().HasServiceHistory, Is.True);
            Assert.That(_storage.Inspect(a.StoragePath).IdentityTaxCode.Value, Is.EqualTo("0100000001"));
            Assert.That(_storage.Inspect(b.StoragePath).IdentityTaxCode.Value, Is.EqualTo("0100000002"));
        }

        [Test]
        public void Create_CatalogWriteFails_RollsBackTheStorageItJustCreated()
        {
            var failing = new CompanyManagementService(new FailingAddRegistry(_catalog), _storage, _context, _entitlement, new Silent());
            var path = Folder("0100000001");

            Assert.Throws<IOException>(() => failing.CreateAsync(Input("0100000001", "A", path), CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(Directory.Exists(path), Is.False, "Không để lại kho nửa vời");
            Assert.That(Items(), Is.Empty);
        }

        [Test]
        public void Create_CatalogWriteFails_KeepsPreExistingEmptyFolder_AndDoesNotTouchOtherCompanies()
        {
            var other = Create("0100000009", "Khác");
            var existing = Folder("0100000001");
            Directory.CreateDirectory(existing);
            var failing = new CompanyManagementService(new FailingAddRegistry(_catalog), _storage, _context, _entitlement, new Silent());

            Assert.Throws<IOException>(() => failing.CreateAsync(Input("0100000001", "A", existing), CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(Directory.Exists(existing), Is.True, "Thư mục có sẵn của người dùng được giữ");
            Assert.That(_storage.Inspect(existing).HasIdentity, Is.False);
            Assert.That(_storage.Inspect(other.StoragePath).HasIdentity, Is.True);
        }

        // ---- đổi tên / ngừng quản lý / liên kết lại ----

        [Test]
        public void Rename_ChangesOnlyTheDisplayName_NotTheTaxCode()
        {
            var item = Create("0100000001", "Cũ");

            var renamed = _service.RenameAsync(item.Id, "  Mới  ", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(renamed.DisplayName, Is.EqualTo("Mới"));
            Assert.That(Items().Single().TaxCode.Value, Is.EqualTo("0100000001"));
            Assert.That(_storage.Inspect(item.StoragePath).IdentityTaxCode.Value, Is.EqualTo("0100000001"));
        }

        [Test]
        public void Rename_EmptyName_OrReadOnlyMode_IsRejected()
        {
            var item = Create("0100000001", "Cũ");

            Assert.Throws<ArgumentException>(() => _service.RenameAsync(item.Id, " ", CancellationToken.None).GetAwaiter().GetResult());

            _entitlement.Value = AppMode.ReadOnlyService;
            Assert.Throws<EntitlementDeniedException>(() => _service.RenameAsync(item.Id, "Mới", CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void Unlink_RemovesOnlyTheEntry_FolderAndDataStayIntact()
        {
            var a = Create("0100000001", "A");
            Create("0100000002", "B");
            File.WriteAllText(Path.Combine(a.StoragePath, "company.db"), "invoices");

            _service.UnlinkAsync(a.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(Items().Select(i => i.TaxCode.Value), Is.EqualTo(new[] { "0100000002" }));
            Assert.That(File.ReadAllText(Path.Combine(a.StoragePath, "company.db")), Is.EqualTo("invoices"));
            Assert.That(_storage.Inspect(a.StoragePath).HasIdentity, Is.True);
        }

        [Test]
        public void Unlink_TheOpenCompany_IsRefused()
        {
            var a = Create("0100000001", "A");
            _context.Current = new CompanyContext(a.Id, a.TaxCode, a.DisplayName, a.StoragePath, null);

            Assert.Throws<CompanyInUseException>(() => _service.UnlinkAsync(a.Id, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(Items().Count, Is.EqualTo(1));
        }

        [Test]
        public void Unlink_StandardOrReadOnly_IsDeniedByTheService_NotJustHiddenInUi()
        {
            var a = Create("0100000001", "A");

            _entitlement.Value = AppMode.Standard;
            Assert.Throws<EntitlementDeniedException>(() => _service.UnlinkAsync(a.Id, CancellationToken.None).GetAwaiter().GetResult());
            _entitlement.Value = AppMode.ReadOnlyService;
            Assert.Throws<EntitlementDeniedException>(() => _service.UnlinkAsync(a.Id, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(Items().Count, Is.EqualTo(1));
        }

        [Test]
        public void UnlinkThenAddSameFolder_AdoptsTheSameCompanyId_AndKeepsData_AC17()
        {
            var a = Create("0100000001", "A");
            File.WriteAllText(Path.Combine(a.StoragePath, "company.db"), "invoices");
            Create("0100000002", "B");
            _service.UnlinkAsync(a.Id, CancellationToken.None).GetAwaiter().GetResult();

            var again = _service.CreateAsync(Input("0100000001", "A (liên kết lại)", a.StoragePath), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(again.Id, Is.EqualTo(a.Id), "Nhận lại mã doanh nghiệp của kho");
            Assert.That(File.ReadAllText(Path.Combine(a.StoragePath, "company.db")), Is.EqualTo("invoices"));
            Assert.That(Items().Count, Is.EqualTo(2));
        }

        [Test]
        public void Relink_AfterFolderMoved_ReturnsToActive()
        {
            var a = Create("0100000001", "A");
            Create("0100000002", "B");
            var moved = Folder("moved-A");
            Directory.Move(a.StoragePath, moved);
            Assert.That(Items().Single(i => i.Id.Equals(a.Id)).Status, Is.EqualTo(CompanyCatalogStatus.PathMissing));

            var relinked = _service.RelinkAsync(a.Id, moved, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(relinked.StoragePath, Is.EqualTo(moved));
            Assert.That(Items().Single(i => i.Id.Equals(a.Id)).Status, Is.EqualTo(CompanyCatalogStatus.Active));
        }

        [Test]
        public void Relink_ToFolderOfAnotherTaxCode_OrWithoutIdentity_IsRejected()
        {
            var a = Create("0100000001", "A");
            var b = Create("0100000002", "B");
            var plain = Folder("plain");
            Directory.CreateDirectory(plain);

            Assert.Throws<CompanyValidationException>(() => _service.RelinkAsync(a.Id, b.StoragePath, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<CompanyValidationException>(() => _service.RelinkAsync(a.Id, plain, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(Items().Single(i => i.Id.Equals(a.Id)).StoragePath, Is.EqualTo(a.StoragePath));
        }

        [Test]
        public void NormalizePath_RequiresAbsolute_AndTrimsTrailingSeparator()
        {
            Assert.Throws<ArgumentException>(() => CompanyManagementService.NormalizePath("relative\\x"));
            Assert.That(CompanyManagementService.NormalizePath(@"C:\data\A\"), Is.EqualTo(@"C:\data\A"));
            Assert.That(CompanyManagementService.NormalizePath(@"C:\data\.\B\..\A"), Is.EqualTo(@"C:\data\A"));
        }

        // ---- fakes ----

        private sealed class Mode : IEntitlementService
        {
            public AppMode Value { get; set; }
            public AppMode CurrentMode { get { return Value; } }

            public bool IsAllowed(AppFeature feature)
            {
                switch (Value)
                {
                    case AppMode.Standard: return feature != AppFeature.MultiCompany;
                    case AppMode.Service: return true;
                    default: return feature == AppFeature.ReadExport;
                }
            }

            public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }

        private sealed class Accessor : ICompanyContextAccessor
        {
            public CompanyContext Current { get; set; }
            public bool HasCurrent { get { return Current != null; } }
        }

        private sealed class Silent : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private sealed class FailingAddRegistry : ICompanyRegistry
        {
            private readonly ICompanyRegistry _inner;

            public FailingAddRegistry(ICompanyRegistry inner)
            {
                _inner = inner;
            }

            public Task<CatalogSnapshot> LoadAsync(CancellationToken ct) { return _inner.LoadAsync(ct); }
            public Task<IList<CompanyCatalogItem>> GetItemsAsync(CancellationToken ct) { return _inner.GetItemsAsync(ct); }
            public Task AddAsync(CompanyCatalogItem item, CancellationToken ct) { throw new IOException("disk full"); }
            public Task UpdateAsync(CompanyCatalogItem item, CancellationToken ct) { return _inner.UpdateAsync(item, ct); }
            public Task RemoveAsync(CompanyId id, CancellationToken ct) { return _inner.RemoveAsync(id, ct); }
        }
    }
}
