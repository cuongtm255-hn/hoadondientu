using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>
    /// NFR-REL-007 với thành phần thật: <c>companies.json</c> hỏng → quét thư mục <c>Companies</c> → đăng ký lại các kho có file định danh.
    /// Thư mục tạm, MST giả; không chạm dữ liệu thật.
    /// </summary>
    [TestFixture]
    public class CompanyRecoveryIntegrationTests
    {
        private string _base;
        private string _catalogPath;
        private string _companies;
        private JsonCompanyCatalog _registry;
        private FileSystemCompanyStorage _storage;
        private CompanyRecoveryService _service;

        [SetUp]
        public void SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-recovery-it-" + Guid.NewGuid().ToString("N"));
            _companies = Path.Combine(_base, "data", "Companies");
            _catalogPath = Path.Combine(_base, "data", "companies.json");
            Directory.CreateDirectory(_companies);
            _registry = new JsonCompanyCatalog(_catalogPath);
            _storage = new FileSystemCompanyStorage();
            _service = new CompanyRecoveryService(_registry, _storage, _registry, new NullLogger());
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

        private string MakeStore(string tax)
        {
            var path = Path.Combine(_companies, tax);
            _storage.Initialize(path, CompanyId.NewId(), TaxCode.Parse(tax));
            Directory.CreateDirectory(Path.Combine(path, "2026", "10"));
            File.WriteAllText(Path.Combine(path, "2026", "10", "hoa-don.xml"), "<HDon>DỮ LIỆU GIẢ</HDon>");
            return path;
        }

        [Test]
        public async Task ACorruptCatalog_IsKept_AndRebuiltFromTheStoresOnDisk()
        {
            var a = MakeStore("0101234567");
            var b = MakeStore("0109988776");
            Directory.CreateDirectory(Path.Combine(_companies, "thu-muc-thuong"));
            File.WriteAllText(_catalogPath, "{ không phải json", new UTF8Encoding(false));
            var snapshot = await _registry.LoadAsync(CancellationToken.None);
            Assert.That(snapshot.Status, Is.EqualTo(CatalogLoadStatus.Corrupt));

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);
            var result = await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            Assert.That(scan.Stores.Select(s => s.Path), Is.EquivalentTo(new[] { a, b }));
            Assert.That(result.Registered, Is.EqualTo(2));
            Assert.That(result.QuarantinedCatalog, Does.Contain("companies.json.corrupt-"));
            Assert.That(File.ReadAllText(result.QuarantinedCatalog), Is.EqualTo("{ không phải json"), "Bản hỏng được giữ nguyên để điều tra");

            // Danh mục mới đọc lại được bằng một đối tượng mới (không phải bộ nhớ đệm) và mở được từng kho.
            var reloaded = await new JsonCompanyCatalog(_catalogPath).GetItemsAsync(CancellationToken.None);
            Assert.That(reloaded.Select(i => i.StoragePath), Is.EquivalentTo(new[] { a, b }));
            Assert.That(reloaded.All(i => i.Status == CompanyCatalogStatus.Active), Is.True);
            foreach (var item in reloaded)
            {
                var inspection = _storage.Inspect(item.StoragePath);
                Assert.That(inspection.IdentityCompanyId.Value, Is.EqualTo(item.Id));
                Assert.That(inspection.IdentityTaxCode, Is.EqualTo(item.TaxCode));
            }

            // Dữ liệu trong kho không bị đụng tới.
            Assert.That(File.ReadAllText(Path.Combine(a, "2026", "10", "hoa-don.xml")), Is.EqualTo("<HDon>DỮ LIỆU GIẢ</HDon>"));
        }

        [Test]
        public async Task AMissingCatalog_CanBeRebuilt_WithoutQuarantineingAnything()
        {
            MakeStore("0101234567");
            Assert.That(File.Exists(_catalogPath), Is.False);

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);
            var result = await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            Assert.That(result.Registered, Is.EqualTo(1));
            Assert.That(result.QuarantinedCatalog, Is.Null);
            Assert.That((await _registry.GetItemsAsync(CancellationToken.None)).Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ScanningDoesNotWriteIntoUnrelatedFolders()
        {
            MakeStore("0101234567");
            var unrelated = Path.Combine(_companies, "thu-muc-thuong");
            Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated, "ghi-chu.txt"), "x");

            await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(Directory.GetFileSystemEntries(unrelated).Select(Path.GetFileName), Is.EqualTo(new[] { "ghi-chu.txt" }), "Quét chỉ đọc, không tạo file thử");
        }

        [Test]
        public async Task AStoreWithADamagedIdentityFile_IsReportedAndLeftAlone()
        {
            var good = MakeStore("0101234567");
            var damaged = Path.Combine(_companies, "0109988776");
            Directory.CreateDirectory(damaged);
            File.WriteAllText(Path.Combine(damaged, FileSystemCompanyStorage.IdentityFileName), "rác", new UTF8Encoding(false));

            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);

            Assert.That(scan.Stores.Single().Path, Is.EqualTo(good));
            Assert.That(scan.Skipped.Single(), Does.Contain("0109988776").And.Contain("hỏng"));
            Assert.That(File.ReadAllText(Path.Combine(damaged, FileSystemCompanyStorage.IdentityFileName)), Is.EqualTo("rác"));
        }

        [Test]
        public async Task QuarantineOnAHealthyCatalog_DoesNothing()
        {
            var a = MakeStore("0101234567");
            var scan = await _service.ScanAsync(new[] { _companies }, CancellationToken.None);
            await _service.RebuildAsync(scan.Stores, CancellationToken.None);

            var moved = await _registry.QuarantineCorruptAsync(CancellationToken.None);

            Assert.That(moved, Is.Null);
            Assert.That(File.Exists(_catalogPath), Is.True);
            Assert.That((await _registry.GetItemsAsync(CancellationToken.None)).Single().StoragePath, Is.EqualTo(a));
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
