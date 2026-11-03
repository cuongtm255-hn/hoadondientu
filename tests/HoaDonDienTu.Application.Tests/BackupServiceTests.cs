using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class BackupServiceTests
    {
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private string _base;
        private string _root;
        private FakeContext _context;
        private FakeJobs _jobs;
        private FakeEntitlement _entitlement;
        private BackupService _service;

        [SetUp]
        public void SetUp()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-backup-" + Guid.NewGuid().ToString("N"));
            _root = Path.Combine(_base, "kho");
            Directory.CreateDirectory(_root);
            _context = new FakeContext { Current = new CompanyContext(CompanyId.NewId(), Mst, "Công ty mẫu", _root, null) };
            _jobs = new FakeJobs();
            _entitlement = new FakeEntitlement { Mode = AppMode.Standard };
            _service = new BackupService(_context, _jobs, _entitlement, "1.0 test", () => new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc));
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

        private string Put(string relative, string content)
        {
            var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
            return path;
        }

        private string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
        }

        private string Target(string name = "g.zip")
        {
            return Path.Combine(_base, "out", name);
        }

        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        private void SeedStore()
        {
            Put("2026/10/a.xml", "<HDon>a</HDon>");
            Put("2026/10/b.xml", "<HDon>b - tiếng Việt</HDon>");
            Put("2026/11/c.pdf", "%PDF-giả");
        }

        /// <summary>Dựng gói bằng tay để thử các gói hỏng hoặc độc hại.</summary>
        private string Craft(string name, Action<Dictionary<string, byte[]>, BackupManifest> shape, bool withManifest = true)
        {
            var manifest = new BackupManifest
            {
                FormatVersion = 1, TaxCode = Mst.Value, CompanyName = "Công ty mẫu", CreatedAtUtcText = "2026-11-01T00:00:00.0000000Z", AppVersion = "t", Files = new List<BackupFileEntry>()
            };
            var entries = new Dictionary<string, byte[]>();
            shape(entries, manifest);
            var path = Path.Combine(_base, name);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var e in entries)
                {
                    var entry = zip.CreateEntry(e.Key);
                    using (var s = entry.Open()) s.Write(e.Value, 0, e.Value.Length);
                }

                if (withManifest)
                {
                    var m = zip.CreateEntry("manifest.json");
                    using (var s = m.Open()) new DataContractJsonSerializer(typeof(BackupManifest)).WriteObject(s, manifest);
                }
            }

            return path;
        }

        private static void AddFile(Dictionary<string, byte[]> entries, BackupManifest manifest, string path, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            entries["data/" + path] = bytes;
            manifest.Files.Add(new BackupFileEntry { Path = path, Sha256 = Sha(bytes), Size = bytes.Length });
        }

        // ---- Tạo gói ----

        [Test]
        public async Task Create_PacksOnlyXmlAndPdf_AndWritesAnAccurateManifest()
        {
            SeedStore();
            Put("Exports/bang-ke.xlsx", "x");
            Put("Exports/in-exports.xml", "<bo-qua/>");
            Put("Backups/cu.zip", "zip");
            Put("2026/10/tam.tmp", "tmp");
            Put("2026/10/ghi-chu.txt", "txt");

            var result = await _service.CreateAsync(Target(), false, null, CancellationToken.None);

            Assert.That(result.FileCount, Is.EqualTo(3));
            Assert.That(result.Path, Is.EqualTo(Path.GetFullPath(Target())));
            using (var zip = ZipFile.OpenRead(Target()))
            {
                Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[] { "data/2026/10/a.xml", "data/2026/10/b.xml", "data/2026/11/c.pdf", "manifest.json" }));
                BackupManifest manifest;
                using (var s = zip.GetEntry("manifest.json").Open()) manifest = (BackupManifest)new DataContractJsonSerializer(typeof(BackupManifest)).ReadObject(s);
                Assert.That(manifest.TaxCode, Is.EqualTo("0101234567"));
                Assert.That(manifest.CompanyName, Is.EqualTo("Công ty mẫu"));
                Assert.That(manifest.FormatVersion, Is.EqualTo(1));
                Assert.That(manifest.IncludesDatabase, Is.False, "Chưa có cơ sở dữ liệu (ADR 0004)");
                Assert.That(manifest.AppVersion, Is.EqualTo("1.0 test"));
                Assert.That(manifest.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc)));
                var a = manifest.Files.Single(f => f.Path == "2026/10/a.xml");
                Assert.That(a.Sha256, Is.EqualTo(Sha(Encoding.UTF8.GetBytes("<HDon>a</HDon>"))));
                Assert.That(a.Size, Is.EqualTo(14));
            }

            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Target())), Has.Length.EqualTo(1), "Không để lại file tạm");
        }

        [Test]
        public void Create_OfAnEmptyStore_IsRefused()
        {
            var ex = Assert.ThrowsAsync<BackupException>(() => _service.CreateAsync(Target(), false, null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("chưa có file"));
            Assert.That(File.Exists(Target()), Is.False);
        }

        [Test]
        public async Task Create_WhileADownloadRuns_IsRefused_AndWritesNothing()
        {
            SeedStore();
            _jobs.Running = true;

            var ex = Assert.ThrowsAsync<BackupException>(() => _service.CreateAsync(Target(), false, null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("phiên tải"));
            Assert.That(File.Exists(Target()), Is.False);
            await Task.FromResult(0);
        }

        [Test]
        public async Task Create_ExistingTarget_NeedsOverwriteConfirmation()
        {
            SeedStore();
            Directory.CreateDirectory(Path.GetDirectoryName(Target()));
            File.WriteAllText(Target(), "cũ");

            Assert.ThrowsAsync<BackupException>(() => _service.CreateAsync(Target(), false, null, CancellationToken.None));
            Assert.That(File.ReadAllText(Target()), Is.EqualTo("cũ"));

            await _service.CreateAsync(Target(), true, null, CancellationToken.None);
            Assert.That(_service.InspectAsync(Target(), CancellationToken.None).Result.IsValid, Is.True);
        }

        [Test]
        public async Task Create_IsAllowedInReadOnlyMode_ButNeedsTheExportEntitlement()
        {
            SeedStore();
            _entitlement.Mode = AppMode.ReadOnlyService;
            await _service.CreateAsync(Target("ro.zip"), false, null, CancellationToken.None);
            Assert.That(File.Exists(Target("ro.zip")), Is.True);

            _entitlement.Denied.Add(AppFeature.ReadExport);
            Assert.ThrowsAsync<EntitlementDeniedException>(() => _service.CreateAsync(Target("no.zip"), false, null, CancellationToken.None));
            Assert.That(File.Exists(Target("no.zip")), Is.False);
        }

        [Test]
        public async Task Create_ReportsProgress_AndCancellingLeavesNothing()
        {
            for (var i = 0; i < 6; i++) Put("2026/f" + i + ".xml", "<x>" + i + "</x>");
            var seen = new List<int>();
            await _service.CreateAsync(Target("p.zip"), false, new SyncProgress(p => seen.Add(p.Done)), CancellationToken.None);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));

            var cts = new CancellationTokenSource();
            Assert.ThrowsAsync<OperationCanceledException>(() => _service.CreateAsync(Target("c.zip"), false, new SyncProgress(p => { if (p.Done == 2) cts.Cancel(); }), cts.Token));
            Assert.That(File.Exists(Target("c.zip")), Is.False);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Target())).Select(Path.GetFileName), Is.EqualTo(new[] { "p.zip" }));
        }

        // ---- Kiểm tra gói ----

        [Test]
        public async Task Inspect_AFreshBackup_IsValid()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);

            var inspection = await _service.InspectAsync(Target(), CancellationToken.None);

            Assert.That(inspection.IsValid, Is.True);
            Assert.That(inspection.Manifest.Files.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task Inspect_DetectsATamperedFile()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            using (var zip = ZipFile.Open(Target(), ZipArchiveMode.Update))
            {
                zip.GetEntry("data/2026/10/a.xml").Delete();
                var e = zip.CreateEntry("data/2026/10/a.xml");
                using (var s = e.Open()) s.Write(new byte[] { 1, 2, 3 }, 0, 3);
            }

            var inspection = await _service.InspectAsync(Target(), CancellationToken.None);

            Assert.That(inspection.IsValid, Is.False);
            Assert.That(inspection.Problems.Single(), Does.Contain("Sai SHA-256").And.Contain("2026/10/a.xml"));
        }

        [Test]
        public async Task Inspect_DetectsAMissingEntry()
        {
            var path = Craft("thieu.zip", (e, m) =>
            {
                AddFile(e, m, "a.xml", "<a/>");
                m.Files.Add(new BackupFileEntry { Path = "b.xml", Sha256 = "x", Size = 1 });
            });

            var inspection = await _service.InspectAsync(path, CancellationToken.None);

            Assert.That(inspection.Problems, Has.Some.Contain("Thiếu file trong gói: b.xml"));
        }

        [Test]
        public async Task Inspect_RejectsNonZipMissingManifestAndUnreadableManifest()
        {
            var notZip = Path.Combine(_base, "khong-phai-zip.zip");
            File.WriteAllText(notZip, "đây không phải zip");
            Assert.That((await _service.InspectAsync(notZip, CancellationToken.None)).Problems.Single(), Does.Contain("không phải gói zip"));

            var noManifest = Craft("khong-manifest.zip", (e, m) => AddFile(e, m, "a.xml", "<a/>"), withManifest: false);
            Assert.That((await _service.InspectAsync(noManifest, CancellationToken.None)).Problems.Single(), Does.Contain("không có manifest.json"));

            var badManifest = Path.Combine(_base, "manifest-hong.zip");
            using (var zip = ZipFile.Open(badManifest, ZipArchiveMode.Create))
            using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open())) w.Write("{ không phải json");
            Assert.That((await _service.InspectAsync(badManifest, CancellationToken.None)).Problems.Single(), Does.Contain("không đọc được"));

            Assert.That((await _service.InspectAsync(Path.Combine(_base, "khong-co.zip"), CancellationToken.None)).Problems.Single(), Does.Contain("Không tìm thấy"));
        }

        [TestCase("../ngoai.xml")]
        [TestCase("a/../../ngoai.xml")]
        [TestCase("C:/Windows/x.xml")]
        [TestCase("/etc/x.xml")]
        [TestCase("a\\b.xml")]
        [TestCase("Exports/x.xml")]
        [TestCase("backups/x.xml")]
        [TestCase("chay.exe")]
        [TestCase("a//b.xml")]
        public async Task Inspect_RejectsPathsThatEscapeOrAreNotAllowed(string path)
        {
            var package = Craft("doc-" + Guid.NewGuid().ToString("N") + ".zip", (e, m) => m.Files.Add(new BackupFileEntry { Path = path, Sha256 = "x", Size = 1 }));

            var inspection = await _service.InspectAsync(package, CancellationToken.None);

            Assert.That(inspection.IsValid, Is.False);
            Assert.That(inspection.Problems.First(), Does.Contain("Đường dẫn không hợp lệ"));
        }

        [Test]
        public async Task Inspect_RejectsANewerFormat_AndDuplicateFiles()
        {
            var newer = Craft("moi.zip", (e, m) =>
            {
                m.FormatVersion = 99;
                AddFile(e, m, "a.xml", "<a/>");
            });
            Assert.That((await _service.InspectAsync(newer, CancellationToken.None)).Problems.Single(), Does.Contain("mới hơn"));

            var dup = Craft("trung.zip", (e, m) =>
            {
                AddFile(e, m, "a.xml", "<a/>");
                m.Files.Add(new BackupFileEntry { Path = "A.xml", Sha256 = "x", Size = 1 });
            });
            Assert.That((await _service.InspectAsync(dup, CancellationToken.None)).Problems, Has.Some.Contain("trùng"));
        }

        // ---- Khôi phục ----

        [Test]
        public async Task Restore_RoundTrip_BringsBackLostAndChangedFiles_AndKeepsOthers()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            File.Delete(Path.Combine(_root, "2026", "10", "a.xml"));
            Put("2026/10/b.xml", "<HDon>b đã bị sửa</HDon>");
            Put("2026/12/moi-hon.xml", "<HDon>mới hơn bản sao lưu</HDon>");

            var result = await _service.RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(result.Created, Is.EqualTo(1));
            Assert.That(result.Overwritten, Is.EqualTo(1));
            Assert.That(result.Unchanged, Is.EqualTo(1));
            Assert.That(result.Restored, Is.EqualTo(2));
            Assert.That(Read("2026/10/a.xml"), Is.EqualTo("<HDon>a</HDon>"));
            Assert.That(Read("2026/10/b.xml"), Is.EqualTo("<HDon>b - tiếng Việt</HDon>"));
            Assert.That(Read("2026/12/moi-hon.xml"), Is.EqualTo("<HDon>mới hơn bản sao lưu</HDon>"), "File ngoài gói không bị xóa");
            Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public async Task Restore_FirstTakesASafetyBackupOfTheCurrentData()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            Put("2026/10/b.xml", "<HDon>b hiện tại</HDon>");

            var result = await _service.RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(result.SafetyBackupPath, Does.Contain(Path.Combine(_root, "Backups")).And.Contain("truoc-khoi-phuc_0101234567_"));
            var safety = await _service.InspectAsync(result.SafetyBackupPath, CancellationToken.None);
            Assert.That(safety.IsValid, Is.True);
            using (var zip = ZipFile.OpenRead(result.SafetyBackupPath))
            using (var r = new StreamReader(zip.GetEntry("data/2026/10/b.xml").Open())) Assert.That(r.ReadToEnd(), Is.EqualTo("<HDon>b hiện tại</HDon>"), "Điểm an toàn giữ dữ liệu trước khi khôi phục");
        }

        [Test]
        public async Task Restore_TwiceInTheSameSecond_KeepsBothSafetyBackups()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            Put("2026/10/b.xml", "<HDon>b lần một</HDon>");
            var first = await _service.RestoreAsync(Target(), null, CancellationToken.None);
            Put("2026/10/b.xml", "<HDon>b lần hai</HDon>");

            var second = await _service.RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(second.SafetyBackupPath, Is.Not.EqualTo(first.SafetyBackupPath));
            Assert.That(File.Exists(first.SafetyBackupPath), Is.True, "Điểm an toàn đầu không bị ghi đè");
            using (var zip = ZipFile.OpenRead(first.SafetyBackupPath))
            using (var r = new StreamReader(zip.GetEntry("data/2026/10/b.xml").Open())) Assert.That(r.ReadToEnd(), Is.EqualTo("<HDon>b lần một</HDon>"));
        }

        [Test]
        public async Task Restore_OverwritingAnExistingFile_LeavesNoTempFile()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            var original = Read("2026/10/a.xml");
            Put("2026/10/a.xml", "<HDon>bị sửa</HDon>");

            await _service.RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(Read("2026/10/a.xml"), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public async Task Restore_IntoAnEmptyStore_NeedsNoSafetyBackup()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            Directory.Delete(_root, true);
            Directory.CreateDirectory(_root);

            var result = await _service.RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(result.Created, Is.EqualTo(3));
            Assert.That(result.SafetyBackupPath, Is.Null);
            Assert.That(Read("2026/11/c.pdf"), Is.EqualTo("%PDF-giả"));
        }

        [Test]
        public async Task Restore_ABackupOfAnotherCompany_IsRefusedBeforeAnythingChanges_AC11()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            var otherRoot = Path.Combine(_base, "kho-khac");
            Directory.CreateDirectory(otherRoot);
            _context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "Công ty khác", otherRoot, null);
            File.WriteAllText(Path.Combine(otherRoot, "giu.xml"), "<giu/>");

            var ex = Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(Target(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("MST 0101234567").And.Contain("MST 0109999999"));
            Assert.That(Directory.GetFiles(otherRoot, "*", SearchOption.AllDirectories).Select(Path.GetFileName), Is.EqualTo(new[] { "giu.xml" }), "Không có gì bị đổi, kể cả điểm an toàn");
        }

        [Test]
        public async Task Restore_ACorruptBackup_IsRefusedBeforeAnythingChanges()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            using (var zip = ZipFile.Open(Target(), ZipArchiveMode.Update))
            {
                zip.GetEntry("data/2026/10/a.xml").Delete();
                using (var s = zip.CreateEntry("data/2026/10/a.xml").Open()) s.WriteByte(0);
            }

            Put("2026/10/b.xml", "<HDon>b hiện tại</HDon>");
            var before = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length;

            var ex = Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(Target(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("Sai SHA-256"));
            Assert.That(Read("2026/10/b.xml"), Is.EqualTo("<HDon>b hiện tại</HDon>"));
            Assert.That(Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length, Is.EqualTo(before), "Chưa tạo điểm an toàn hay ghi gì");
        }

        [Test]
        public async Task Restore_IsBlockedWhileADownloadRuns_AndInReadOnlyMode()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);

            _jobs.Running = true;
            Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(Target(), null, CancellationToken.None));
            _jobs.Running = false;

            _entitlement.Mode = AppMode.ReadOnlyService;
            _entitlement.Denied.Add(AppFeature.ManageSettings);
            Assert.ThrowsAsync<EntitlementDeniedException>(() => _service.RestoreAsync(Target(), null, CancellationToken.None));
        }

        [Test]
        public async Task Restore_FailingHalfWay_RollsBackToTheStateBeforeRestoring()
        {
            Put("2026/a.xml", "<a>hiện tại</a>");
            var package = Craft("loi-giua-chung.zip", (e, m) =>
            {
                AddFile(e, m, "2026/a.xml", "<a>từ gói</a>");
                AddFile(e, m, "2026/moi/b.xml", "<b>từ gói</b>");
                AddFile(e, m, "2026/c.xml", "<c>từ gói</c>");
            });
            // File thứ ba không ghi được vì tên đích đang là một thư mục.
            Directory.CreateDirectory(Path.Combine(_root, "2026", "c.xml"));

            var ex = Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(package, null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("trả về trạng thái trước"));
            Assert.That(Read("2026/a.xml"), Is.EqualTo("<a>hiện tại</a>"), "File bị ghi đè đã trả về nội dung cũ");
            Assert.That(File.Exists(Path.Combine(_root, "2026", "moi", "b.xml")), Is.False, "File mới tạo bị xóa");
            Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public async Task Restore_Cancelled_RollsBackAndRethrowsTheCancellation()
        {
            Put("2026/a.xml", "<a>hiện tại</a>");
            var package = Craft("huy.zip", (e, m) =>
            {
                AddFile(e, m, "2026/a.xml", "<a>từ gói</a>");
                AddFile(e, m, "2026/b.xml", "<b>từ gói</b>");
                AddFile(e, m, "2026/c.xml", "<c>từ gói</c>");
            });
            var cts = new CancellationTokenSource();

            Assert.ThrowsAsync<OperationCanceledException>(() => _service.RestoreAsync(package, new SyncProgress(p => { if (p.Done == 2) cts.Cancel(); }), cts.Token));

            Assert.That(Read("2026/a.xml"), Is.EqualTo("<a>hiện tại</a>"));
            Assert.That(File.Exists(Path.Combine(_root, "2026", "b.xml")), Is.False);
        }

        [Test]
        public async Task Restore_ReportsProgress()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            var seen = new List<BackupProgress>();

            await _service.RestoreAsync(Target(), new SyncProgress(seen.Add), CancellationToken.None);

            Assert.That(seen.Select(p => p.Done), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(seen.All(p => p.Total == 3), Is.True);
        }

        [Test]
        public void WithoutACompany_NothingWorks()
        {
            _context.Current = null;

            Assert.ThrowsAsync<BackupException>(() => _service.CreateAsync(Target(), false, null, CancellationToken.None));
            Assert.ThrowsAsync<BackupException>(() => _service.RestoreAsync(Target(), null, CancellationToken.None));
        }

        // ---- Thống kê dung lượng ----

        [Test]
        public async Task Statistics_SplitsXmlPdfExportsAndBackups_AndFindsTheLatestBackupOfThisCompany()
        {
            Put("2026/a.xml", new string('x', 100));
            Put("2026/b.xml", new string('x', 50));
            Put("2026/c.pdf", new string('x', 30));
            Put("Exports/e1.xlsx", new string('x', 20));
            Put("Exports/sub/e2.zip", new string('x', 10));
            Put("Backups/HDDT_0101234567_20261030-100000.zip", new string('x', 7));
            Put("Backups/HDDT_0101234567_20261031-100000.zip", new string('x', 9));
            Put("Backups/HDDT_0109999999_20261101-100000.zip", new string('x', 5));
            File.SetLastWriteTimeUtc(Path.Combine(_root, "Backups", "HDDT_0101234567_20261030-100000.zip"), new DateTime(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(_root, "Backups", "HDDT_0101234567_20261031-100000.zip"), new DateTime(2026, 10, 31, 3, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(_root, "Backups", "HDDT_0109999999_20261101-100000.zip"), new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));

            var stats = await new StorageStatisticsService(_context).GetAsync(CancellationToken.None);

            Assert.That(stats.XmlBytes, Is.EqualTo(150));
            Assert.That(stats.XmlFiles, Is.EqualTo(2));
            Assert.That(stats.PdfBytes, Is.EqualTo(30));
            Assert.That(stats.PdfFiles, Is.EqualTo(1));
            Assert.That(stats.ExportBytes, Is.EqualTo(30));
            Assert.That(stats.BackupBytes, Is.EqualTo(21));
            Assert.That(stats.BackupFiles, Is.EqualTo(3));
            Assert.That(stats.TotalBytes, Is.EqualTo(231));
            Assert.That(Path.GetFileName(stats.LastBackupPath), Is.EqualTo("HDDT_0101234567_20261031-100000.zip"), "Chỉ tính gói của MST đang mở");
            Assert.That(stats.LastBackupUtc, Is.EqualTo(new DateTime(2026, 10, 31, 3, 0, 0, DateTimeKind.Utc)));
            Assert.That(stats.FreeBytes, Is.Not.Null.And.GreaterThan(0));
        }

        [Test]
        public async Task Statistics_OfAnEmptyOrMissingStore_AreZero()
        {
            Directory.Delete(_root, true);

            var stats = await new StorageStatisticsService(_context).GetAsync(CancellationToken.None);

            Assert.That(stats.TotalBytes, Is.EqualTo(0));
            Assert.That(stats.LastBackupPath, Is.Null);
        }

        [Test]
        public void Statistics_WithoutACompany_AreRefused()
        {
            _context.Current = null;

            Assert.Throws<BackupException>(() => new StorageStatisticsService(_context).GetAsync(CancellationToken.None));
        }

        // ---- Fakes ----

        private sealed class SyncProgress : IProgress<BackupProgress>
        {
            private readonly Action<BackupProgress> _callback;

            public SyncProgress(Action<BackupProgress> callback)
            {
                _callback = callback;
            }

            public void Report(BackupProgress value)
            {
                _callback(value);
            }
        }

        // ---- Gói có company.db (định dạng 2) ----

        private BackupService WithDatabase(FakeDatabase db)
        {
            return new BackupService(_context, _jobs, _entitlement, "1.0 test", () => new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc), db);
        }

        [Test]
        public async Task Create_WithADatabase_WritesAFormat2ManifestAndTheSnapshotEntry()
        {
            SeedStore();
            var db = new FakeDatabase();

            await WithDatabase(db).CreateAsync(Target(), false, null, CancellationToken.None);

            var inspection = await WithDatabase(db).InspectAsync(Target(), CancellationToken.None);
            Assert.That(inspection.IsValid, Is.True);
            Assert.That(inspection.Manifest.FormatVersion, Is.EqualTo(2));
            Assert.That(inspection.Manifest.IncludesDatabase, Is.True);
            Assert.That(inspection.Manifest.Database.Sha256, Is.EqualTo(Sha(db.Content)));
            Assert.That(inspection.Manifest.SchemaVersion, Is.EqualTo(db.Latest));
            using (var zip = ZipFile.OpenRead(Target())) Assert.That(zip.GetEntry(BackupService.DatabaseEntry), Is.Not.Null);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Target()), "*.tmp*"), Is.Empty, "không để lại bản chụp tạm");
        }

        [Test]
        public async Task Inspect_ADatabaseEntryThatWasTamperedWith_IsReported()
        {
            SeedStore();
            await WithDatabase(new FakeDatabase()).CreateAsync(Target(), false, null, CancellationToken.None);
            using (var zip = ZipFile.Open(Target(), ZipArchiveMode.Update))
            {
                zip.GetEntry(BackupService.DatabaseEntry).Delete();
                var entry = zip.CreateEntry(BackupService.DatabaseEntry);
                using (var s = entry.Open()) s.Write(new byte[] { 1, 2, 3 }, 0, 3);
            }

            var inspection = await WithDatabase(new FakeDatabase()).InspectAsync(Target(), CancellationToken.None);

            Assert.That(inspection.IsValid, Is.False);
            Assert.That(inspection.Problems.Single(), Does.Contain("cơ sở dữ liệu"));
        }

        [Test]
        public async Task Restore_ReplacesTheDatabaseAndReportsIt()
        {
            SeedStore();
            var db = new FakeDatabase { Content = new byte[] { 9, 9, 9 } };
            var service = WithDatabase(db);
            await service.CreateAsync(Target(), false, null, CancellationToken.None);
            db.Content = new byte[] { 1 };

            var result = await service.RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(result.DatabaseRestored, Is.True);
            Assert.That(db.Content, Is.EqualTo(new byte[] { 9, 9, 9 }));
        }

        [Test]
        public async Task Restore_AFormat1Package_LeavesTheDatabaseAlone()
        {
            SeedStore();
            await _service.CreateAsync(Target(), false, null, CancellationToken.None);
            var db = new FakeDatabase { Content = new byte[] { 5 } };

            var result = await WithDatabase(db).RestoreAsync(Target(), null, CancellationToken.None);

            Assert.That(result.DatabaseRestored, Is.False);
            Assert.That(db.ReplaceCalls, Is.EqualTo(0));
            Assert.That(db.Content, Is.EqualTo(new byte[] { 5 }));
        }

        [Test]
        public async Task Restore_ADatabaseWithANewerSchema_IsRejectedBeforeAnythingChanges()
        {
            SeedStore();
            var db = new FakeDatabase();
            await WithDatabase(db).CreateAsync(Target(), false, null, CancellationToken.None);
            Put("2026/10/b.xml", "<HDon>b hiện tại</HDon>");
            db.Latest = 1;

            var ex = Assert.ThrowsAsync<BackupException>(() => WithDatabase(db).RestoreAsync(Target(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("mới hơn"));
            Assert.That(Read("2026/10/b.xml"), Is.EqualTo("<HDon>b hiện tại</HDon>"));
            Assert.That(db.ReplaceCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task Restore_ACorruptDatabaseInThePackage_IsRejectedBeforeAnythingChanges()
        {
            SeedStore();
            var db = new FakeDatabase();
            await WithDatabase(db).CreateAsync(Target(), false, null, CancellationToken.None);
            Put("2026/10/b.xml", "<HDon>b hiện tại</HDon>");
            db.ValidateFails = true;

            Assert.ThrowsAsync<BackupException>(() => WithDatabase(db).RestoreAsync(Target(), null, CancellationToken.None));

            Assert.That(Read("2026/10/b.xml"), Is.EqualTo("<HDon>b hiện tại</HDon>"));
            Assert.That(db.ReplaceCalls, Is.EqualTo(0));
            Assert.That(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public async Task Restore_WhenTheDatabaseSwapFails_RollsBackTheFilesAndTheDatabase()
        {
            SeedStore();
            var db = new FakeDatabase { Content = new byte[] { 9, 9, 9 } };
            var service = WithDatabase(db);
            await service.CreateAsync(Target(), false, null, CancellationToken.None);
            Put("2026/10/a.xml", "<HDon>a hiện tại</HDon>");
            Put("2026/10/moi.xml", "<HDon>mới</HDon>");
            db.Content = new byte[] { 7 };
            db.FailReplaceAt = 1;

            var ex = Assert.ThrowsAsync<BackupException>(() => service.RestoreAsync(Target(), null, CancellationToken.None));

            Assert.That(ex.Message, Does.Contain("trả về trạng thái trước"));
            Assert.That(Read("2026/10/a.xml"), Is.EqualTo("<HDon>a hiện tại</HDon>"), "file trở lại như trước khi khôi phục");
            Assert.That(db.Content, Is.EqualTo(new byte[] { 7 }), "cơ sở dữ liệu không bị đổi");
        }

        private sealed class FakeDatabase : ICompanyDatabaseBackup
        {
            public byte[] Content = new byte[] { 1, 2, 3, 4 };
            public int Latest = 2;
            public int ReplaceCalls;
            public int FailReplaceAt;
            public bool ValidateFails;

            public int LatestSchemaVersion { get { return Latest; } }

            public Task<int> SnapshotAsync(string destinationFile, CancellationToken cancellationToken)
            {
                File.WriteAllBytes(destinationFile, Content);
                return Task.FromResult(2);
            }

            public Task ValidateAsync(string sourceFile, CancellationToken cancellationToken)
            {
                if (ValidateFails) throw new BackupException("Cơ sở dữ liệu trong gói không dùng được (giả).");
                return Task.FromResult(0);
            }

            public Task ReplaceAsync(string sourceFile, CancellationToken cancellationToken)
            {
                ReplaceCalls++;
                if (FailReplaceAt == ReplaceCalls) throw new BackupException("Không hoán đổi được (giả).");
                Content = File.ReadAllBytes(sourceFile);
                return Task.FromResult(0);
            }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
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
            public AppMode Mode;
            public readonly HashSet<AppFeature> Denied = new HashSet<AppFeature>();

            public AppMode CurrentMode { get { return Mode; } }
            public bool IsAllowed(AppFeature feature) { return !Denied.Contains(feature); }
            public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }
    }
}
