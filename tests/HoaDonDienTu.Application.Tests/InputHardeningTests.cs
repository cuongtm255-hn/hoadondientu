using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>
    /// P6-03 đợt 2: XML độc hại và gói sao lưu bị làm hỏng. Mọi bộ đọc XML của hóa đơn và <c>BackupService</c> phải từ chối bằng đúng loại lỗi đã định,
    /// không treo, không đọc tài nguyên ngoài, không làm hỏng kho. Dữ liệu GIẢ trong thư mục tạm; ngẫu nhiên có hạt giống cố định để lặp lại được.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    public class InputHardeningTests
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

        private const string ValidXml =
            "<HDon><DLHDon><TTChung><PBan>2.0.0</PBan><THDon>Hóa đơn GTGT</THDon><KHMSHDon>1</KHMSHDon><KHHDon>C26TAA</KHHDon><SHDon>128</SHDon><NLap>2026-10-31</NLap><DVTTe>VND</DVTTe></TTChung>"
            + "<NDHDon><NBan><Ten>CÔNG TY GIẢ</Ten><MST>0109988776</MST></NBan><NMua><Ten>Người mua giả</Ten></NMua></NDHDon></DLHDon></HDon>";

        private static byte[] Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>Chạy một lần trên thread khác và bắt đúng ngoại lệ; quá <see cref="Limit"/> nghĩa là treo.</summary>
        private static Exception RunWithTimeout(Action action)
        {
            Exception caught = null;
            var task = Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            Assert.That(task.Wait(Limit), Is.True, "Bộ đọc bị treo trên đầu vào độc hại");
            return caught;
        }

        private static string Nested(int depth)
        {
            var sb = new StringBuilder("<HDon>");
            for (var i = 0; i < depth; i++) sb.Append("<a>");
            for (var i = 0; i < depth; i++) sb.Append("</a>");
            return sb.Append("</HDon>").ToString();
        }

        /// <summary>Các kiểu ngoại lệ mà từng bộ đọc được phép ném khi từ chối đầu vào xấu.</summary>
        private static void AssertRejectedCleanly(string name, Exception ex)
        {
            if (ex == null) return;
            Assert.That(ex, Is.TypeOf<XmlException>().Or.TypeOf<InvoiceStorageException>().Or.TypeOf<InvoicePrintException>(), name + ": ném " + ex.GetType().Name + " - " + ex.Message);
        }

        // ---- XML độc hại có chủ đích ----

        private static IEnumerable<TestCaseData> HostileDocuments()
        {
            yield return new TestCaseData("<?xml version=\"1.0\"?><!DOCTYPE lolz [<!ENTITY lol \"lol\"><!ENTITY lol2 \"&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;\"><!ENTITY lol3 \"&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;\">]><HDon>&lol3;</HDon>").SetName("BillionLaughs");
            yield return new TestCaseData("<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><HDon>&x;</HDon>").SetName("ExternalEntityFile");
            yield return new TestCaseData("<?xml version=\"1.0\"?><!DOCTYPE d SYSTEM \"http://127.0.0.1:1/evil.dtd\"><HDon/>").SetName("ExternalDtdOverHttp");
            yield return new TestCaseData("<HDon xmlns:xi=\"http://www.w3.org/2001/XInclude\"><xi:include href=\"file:///c:/windows/win.ini\" parse=\"text\"/></HDon>").SetName("XInclude");
            yield return new TestCaseData(Nested(100000)).SetName("DeeplyNested100000");
            yield return new TestCaseData("<HDon>" + new string('x', 5 * 1024 * 1024) + "</HDon>").SetName("HugeText5MB");
            yield return new TestCaseData("<HDon " + string.Join(" ", Enumerable.Range(0, 20000).Select(i => "a" + i + "=\"1\"")) + "/>").SetName("TwentyThousandAttributes");
            yield return new TestCaseData("<HDon>\0</HDon>").SetName("NullCharacter");
            yield return new TestCaseData("\uFEFF<HDon><unclosed></HDon>").SetName("MismatchedTags");
            yield return new TestCaseData("").SetName("Empty");
            yield return new TestCaseData("không phải xml").SetName("NotXml");
        }

        [TestCaseSource("HostileDocuments")]
        public void EveryXmlReader_RejectsHostileDocumentsCleanly_WithoutHangingOrReadingExternalResources(string xml)
        {
            var bytes = Bytes(xml);

            AssertRejectedCleanly("XmlTreeBuilder", RunWithTimeout(() => XmlTreeBuilder.Build(bytes)));
            AssertRejectedCleanly("InvoiceXmlValidator", RunWithTimeout(() => InvoiceXmlValidator.Validate(bytes)));
            AssertRejectedCleanly("InvoicePrintModelParser", RunWithTimeout(() => InvoicePrintModelParser.Parse(bytes)));

            // Kiểm chữ ký không bao giờ ném: kết quả luôn là một trạng thái.
            InvoiceSignatureStatus status = InvoiceSignatureStatus.NotChecked;
            var failure = RunWithTimeout(() => status = InvoiceSignatureVerifier.VerifySeller(bytes));
            Assert.That(failure, Is.Null, failure == null ? string.Empty : failure.GetType().Name + ": " + failure.Message);
            Assert.That(status, Is.Not.EqualTo(InvoiceSignatureStatus.Valid));
        }

        [Test]
        public void ExternalEntityContent_NeverReachesTheTreeOrThePrintModel()
        {
            var bytes = Bytes("<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><HDon><DLHDon><TTChung><SHDon>&x;</SHDon></TTChung></DLHDon></HDon>");

            var tree = RunWithTimeout(() => XmlTreeBuilder.Build(bytes));
            var print = RunWithTimeout(() => InvoicePrintModelParser.Parse(bytes));

            Assert.That(tree, Is.Not.Null, "DTD phải bị từ chối chứ không được xử lý");
            Assert.That(print, Is.Not.Null);
        }

        [Test]
        public void ADocumentNestedPastTheLimit_IsRejectedBeforeAnyRecursiveProcessing()
        {
            var bytes = Bytes(Nested(XmlSafety.MaxDepth + 10));

            Assert.Throws<XmlException>(() => XmlSafety.EnsureShallow(bytes));
            Assert.That(InvoiceSignatureVerifier.VerifySeller(bytes), Is.EqualTo(InvoiceSignatureStatus.NotChecked));
            Assert.Throws<InvoicePrintException>(() => InvoicePrintModelParser.Parse(bytes));
        }

        [Test]
        public void ARealisticInvoice_IsStillAccepted_ByEveryReader()
        {
            var bytes = Bytes(ValidXml);

            Assert.DoesNotThrow(() => XmlSafety.EnsureShallow(bytes));
            Assert.DoesNotThrow(() => InvoiceXmlValidator.Validate(bytes));
            Assert.That(XmlTreeBuilder.Build(bytes).Name, Is.EqualTo("HDon"));
            Assert.That(InvoicePrintModelParser.Parse(bytes).InvoiceNumber, Is.EqualTo("128"));
            Assert.That(InvoiceSignatureVerifier.VerifySeller(bytes), Is.EqualTo(InvoiceSignatureStatus.None));
        }

        // ---- Biến dạng ngẫu nhiên của một hóa đơn hợp lệ ----

        [Test]
        public void ThreeHundredRandomMutationsOfAValidInvoice_AreEitherAcceptedOrRejectedCleanly()
        {
            var random = new Random(20261106);
            var original = Bytes(ValidXml);
            for (var i = 0; i < 300; i++)
            {
                var mutated = Mutate(original, random);
                var label = "mutation " + i;

                AssertRejectedCleanly(label + " tree", RunWithTimeout(() => XmlTreeBuilder.Build(mutated)));
                AssertRejectedCleanly(label + " validator", RunWithTimeout(() => InvoiceXmlValidator.Validate(mutated)));
                AssertRejectedCleanly(label + " print", RunWithTimeout(() => InvoicePrintModelParser.Parse(mutated)));
                var failure = RunWithTimeout(() => InvoiceSignatureVerifier.VerifySeller(mutated));
                Assert.That(failure, Is.Null, label + ": kiểm chữ ký ném " + (failure == null ? string.Empty : failure.GetType().Name));
            }
        }

        private static byte[] Mutate(byte[] original, Random random)
        {
            var copy = (byte[])original.Clone();
            switch (random.Next(4))
            {
                case 0:
                    for (var n = random.Next(1, 6); n > 0; n--) copy[random.Next(copy.Length)] = (byte)random.Next(256);
                    return copy;
                case 1:
                    return copy.Take(random.Next(copy.Length)).ToArray();
                case 2:
                    var at = random.Next(copy.Length);
                    var junk = new byte[random.Next(1, 40)];
                    random.NextBytes(junk);
                    return copy.Take(at).Concat(junk).Concat(copy.Skip(at)).ToArray();
                default:
                    var start = random.Next(copy.Length);
                    return copy.Take(start).Concat(copy.Skip(start + random.Next(1, 30))).ToArray();
            }
        }

        // ---- Gói sao lưu bị làm hỏng ----

        [Test]
        public async Task TwoHundredCorruptedBackupPackages_AreRefusedCleanly_AndNeverChangeTheStore()
        {
            var dir = Path.Combine(Path.GetTempPath(), "hddt-fuzz-" + Guid.NewGuid().ToString("N"));
            try
            {
                var root = Path.Combine(dir, "kho");
                for (var i = 0; i < 8; i++)
                {
                    var path = Path.Combine(root, "2026", "10", "hd" + i + ".xml");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, Bytes(ValidXml.Replace("128", i.ToString())));
                }

                var context = new FixedContext(new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty mẫu (mô phỏng)", root, null));
                var service = new BackupService(context, new NoJobs(), new AllowAll(), "1.0 test", () => new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc), new SimpleDatabase());
                var package = Path.Combine(dir, "goc.zip");
                await service.CreateAsync(package, true, null, CancellationToken.None);
                var original = File.ReadAllBytes(package);
                var before = Snapshot(root);
                var random = new Random(20261106);

                var accepted = 0;
                for (var i = 0; i < 200; i++)
                {
                    var broken = Mutate(original, random);
                    var file = Path.Combine(dir, "hong" + i + ".zip");
                    File.WriteAllBytes(file, broken);
                    var label = "package " + i;

                    BackupInspection inspection = null;
                    var failure = RunWithTimeout(() => inspection = service.InspectAsync(file, CancellationToken.None).GetAwaiter().GetResult());
                    Assert.That(failure, Is.Null, label + ": InspectAsync ném " + (failure == null ? string.Empty : failure.ToString()));

                    if (inspection.IsValid)
                    {
                        // Biến dạng không đụng vào dữ liệu: gói vẫn hợp lệ nghĩa là mọi file qua được kiểm SHA-256.
                        accepted++;
                        continue;
                    }

                    var restoreFailure = RunWithTimeout(() => service.RestoreAsync(file, null, CancellationToken.None).GetAwaiter().GetResult());
                    Assert.That(restoreFailure, Is.TypeOf<BackupException>(), label + ": " + (restoreFailure == null ? "khôi phục lại thành công gói đã bị báo hỏng" : restoreFailure.GetType().Name + " - " + restoreFailure.Message));
                    Assert.That(Snapshot(root), Is.EqualTo(before), label + ": kho bị thay đổi bởi gói hỏng");
                    Assert.That(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories), Is.Empty, label);
                    File.Delete(file);
                }

                TestContext.WriteLine("FUZZ backup: {0} of 200 mutations left the package valid (changes in unused bytes)", accepted);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static List<string> Snapshot(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => f.Substring(root.Length) + ":" + File.ReadAllBytes(f).Length + ":" + BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(f))))
                .ToList();
        }

        private sealed class SimpleDatabase : ICompanyDatabaseBackup
        {
            public int LatestSchemaVersion { get { return 4; } }
            public Task<int> SnapshotAsync(string destinationFile, CancellationToken cancellationToken) { File.WriteAllBytes(destinationFile, new byte[] { 1, 2, 3, 4 }); return Task.FromResult(4); }
            public Task ValidateAsync(string sourceFile, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task ReplaceAsync(string sourceFile, CancellationToken cancellationToken) { return Task.FromResult(0); }
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
