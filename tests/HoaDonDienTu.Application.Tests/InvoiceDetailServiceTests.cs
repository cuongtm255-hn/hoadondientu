using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class InvoiceDetailServiceTests
    {
        private const string Xml = "<HDon><DLHDon><TTChung><KHMSHDon>1</KHMSHDon><SHDon>0000128</SHDon></TTChung><NDHDon><NBan><Ten>Công ty ABC</Ten></NBan></NDHDon></DLHDon></HDon>";
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");

        private InMemoryInvoiceRepository _invoices;
        private InMemoryDownloadRepository _downloads;
        private FakeFiles _files;
        private FakeContext _context;
        private CompanyId _companyId;
        private InvoiceDetailService _service;

        [SetUp]
        public void SetUp()
        {
            _invoices = new InMemoryInvoiceRepository();
            _downloads = new InMemoryDownloadRepository();
            _files = new FakeFiles();
            _companyId = CompanyId.NewId();
            _context = new FakeContext { Current = new CompanyContext(_companyId, Mst, "Công ty mẫu", @"C:\Kho", null) };
            _service = new InvoiceDetailService(_invoices, _files, _files, new DownloadHistoryService(_downloads, _context), _context);
        }

        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }

        private async Task<DownloadSession> AddSessionAsync(DateTime createdUtc)
        {
            var session = new DownloadSession(Guid.NewGuid(), _companyId, Mst, @"C:\Kho", InvoiceDirection.Purchase, PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(), createdUtc, "u");
            await _downloads.CreateAsync(session, new List<DownloadSegment>(), CancellationToken.None);
            return session;
        }

        private async Task<StoredInvoice> AddInvoiceAsync(string[] contents, Guid[] sessions = null, bool[] present = null)
        {
            var invoice = new NormalizedInvoice
            {
                Direction = InvoiceDirection.Purchase, InvoiceNumber = "0000128", IssueDate = new DateTime(2026, 10, 31), FormSymbol = "1", InvoiceSymbol = "C26TAA",
                SellerTaxCode = "0109988776", SellerName = "Công ty ABC", BuyerTaxCode = "0101234567", BuyerName = "Công ty mẫu", StatusRaw = "1", CheckResultRaw = "5"
            };
            var versions = new List<InvoiceVersionRecord>();
            for (var i = 0; i < contents.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(contents[i]);
                var path = @"X\128_v" + (i + 1) + ".xml";
                versions.Add(new InvoiceVersionRecord
                {
                    VersionId = Guid.NewGuid(), VersionNo = i + 1, Sha256 = Sha(bytes), RelativePath = path, SizeBytes = bytes.Length,
                    CreatedAtUtc = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc).AddDays(i), IsCurrent = i == contents.Length - 1,
                    SourceSessionId = sessions == null ? Guid.NewGuid() : sessions[i]
                });
                if (present == null || present[i]) _files.Put(path, bytes);
            }

            var stored = new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(), BusinessKey = BusinessKey.Build(invoice), Invoice = invoice, Versions = versions,
                FirstSeenAtUtc = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc), LastSeenAtUtc = new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc)
            };
            await _invoices.AddAsync(stored, CancellationToken.None);
            return stored;
        }

        // ---- GetAsync ----

        [Test]
        public async Task Get_UnknownInvoice_ReturnsNull()
        {
            Assert.That(await _service.GetAsync(Guid.NewGuid(), CancellationToken.None), Is.Null);
        }

        [Test]
        public void Get_WithoutACompany_IsRejected()
        {
            _context.Current = null;

            Assert.ThrowsAsync<StorageGuardException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
        }

        [Test]
        public async Task Get_ListsVersionsNewestFirst_WithFileStateAndCurrent()
        {
            var stored = await AddInvoiceAsync(new[] { Xml, Xml + " ", Xml + "  " }, present: new[] { true, false, true });

            var detail = await _service.GetAsync(stored.InvoiceId, CancellationToken.None);

            Assert.That(detail.Versions.Select(v => v.VersionNo), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(detail.Current.VersionNo, Is.EqualTo(3));
            Assert.That(detail.Versions.Select(v => v.FileState), Is.EqualTo(new[] { VersionFileState.Ok, VersionFileState.Missing, VersionFileState.Ok }));
            Assert.That(detail.Invoice.InvoiceNumber, Is.EqualTo("0000128"));
            Assert.That(detail.FirstSeenAtUtc, Is.EqualTo(stored.FirstSeenAtUtc));
        }

        [Test]
        public async Task Get_ResolvesTheSourceSession_AndToleratesAnUnknownOne()
        {
            var known = await AddSessionAsync(new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc));
            var stored = await AddInvoiceAsync(new[] { Xml, Xml + " " }, sessions: new[] { known.Id, Guid.NewGuid() });

            var detail = await _service.GetAsync(stored.InvoiceId, CancellationToken.None);

            var v1 = detail.Versions.Single(v => v.VersionNo == 1);
            Assert.That(v1.SessionCode, Does.StartWith("DL-2026-11"));
            Assert.That(v1.SessionPeriod, Is.EqualTo("Tháng 10/2026"));
            Assert.That(v1.SessionCreatedAtUtc, Is.Not.Null);
            var v2 = detail.Versions.Single(v => v.VersionNo == 2);
            Assert.That(v2.SessionCode, Is.Null);
            Assert.That(v2.SessionPeriod, Is.Null);
        }

        // ---- OpenXmlAsync ----

        [Test]
        public async Task OpenXml_ValidFile_GivesTreeRawTextAndMatchingHash()
        {
            var stored = await AddInvoiceAsync(new[] { Xml });

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.Problem, Is.Null);
            Assert.That(view.HashMatches, Is.True);
            Assert.That(view.RawText, Is.EqualTo(Xml));
            Assert.That(view.Bytes, Is.EqualTo(Encoding.UTF8.GetBytes(Xml)));
            Assert.That(view.Tree.Name, Is.EqualTo("HDon"));
            Assert.That(view.Tree.Children[0].Children[0].Children.Select(c => c.Name), Is.EqualTo(new[] { "KHMSHDon", "SHDon" }));
            Assert.That(view.Tree.Children[0].Children[0].Children[1].Value, Is.EqualTo("0000128"));
        }

        [Test]
        public async Task OpenXml_StripsTheUtf8Bom_ForDisplayOnly()
        {
            var stored = await AddInvoiceAsync(new[] { Xml });
            var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Xml)).ToArray();
            _files.Put(stored.Versions[0].RelativePath, withBom);
            stored.Versions[0].Sha256 = Sha(withBom);

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.RawText, Is.EqualTo(Xml));
            Assert.That(view.Bytes, Is.EqualTo(withBom), "Bản sao giữ nguyên byte gốc");
        }

        [Test]
        public async Task OpenXml_ChangedFile_WarnsAboutTheHash_ButStillShowsIt()
        {
            var stored = await AddInvoiceAsync(new[] { Xml });
            _files.Put(stored.Versions[0].RelativePath, Encoding.UTF8.GetBytes(Xml.Replace("0000128", "0000999")));

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.HashMatches, Is.False);
            Assert.That(view.Problem, Does.Contain("SHA-256"));
            Assert.That(view.Tree, Is.Not.Null);
            Assert.That(view.RawText, Does.Contain("0000999"));
        }

        [Test]
        public async Task OpenXml_MissingFile_ReportsItWithoutThrowing()
        {
            var stored = await AddInvoiceAsync(new[] { Xml }, present: new[] { false });

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.Problem, Does.Contain("không còn trong thư mục kho"));
            Assert.That(view.Bytes, Is.Null);
            Assert.That(view.RawText, Is.Null);
            Assert.That(view.Tree, Is.Null);
        }

        [Test]
        public async Task OpenXml_TooLargeFile_IsNotLoaded()
        {
            var stored = await AddInvoiceAsync(new[] { Xml });
            _files.TooLarge.Add(stored.Versions[0].RelativePath);

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.Problem, Does.Contain("quá lớn"));
            Assert.That(view.Bytes, Is.Null);
        }

        [Test]
        public async Task OpenXml_BrokenXml_ShowsRawTextOnly()
        {
            var stored = await AddInvoiceAsync(new[] { "<HDon><broken>" });

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.Tree, Is.Null);
            Assert.That(view.RawText, Is.EqualTo("<HDon><broken>"));
            Assert.That(view.Problem, Does.Contain("không đọc được"));
            Assert.That(view.Bytes, Is.Not.Null, "Vẫn lưu bản sao được");
        }

        [Test]
        public async Task OpenXml_FileWithADoctype_IsRefusedAsATree()
        {
            var evil = "<!DOCTYPE HDon [<!ENTITY x SYSTEM \"file:///C:/Windows/win.ini\">]><HDon>&x;</HDon>";
            var stored = await AddInvoiceAsync(new[] { evil });

            var view = await _service.OpenXmlAsync(stored.InvoiceId, 1, CancellationToken.None);

            Assert.That(view.Tree, Is.Null, "DTD bị cấm, không resolve thực thể ngoài");
            Assert.That(view.Problem, Is.Not.Null);
            Assert.That(view.RawText, Does.Not.Contain("[extensions]"), "Nội dung win.ini không bị đọc");
        }

        [Test]
        public async Task OpenXml_UnknownInvoiceOrVersion_IsRejected()
        {
            var stored = await AddInvoiceAsync(new[] { Xml });

            Assert.ThrowsAsync<InvoiceStorageException>(() => _service.OpenXmlAsync(Guid.NewGuid(), 1, CancellationToken.None));
            Assert.ThrowsAsync<InvoiceStorageException>(() => _service.OpenXmlAsync(stored.InvoiceId, 9, CancellationToken.None));
        }

        // ---- XmlTreeBuilder ----

        [Test]
        public void Tree_KeepsAttributesTextAndEmptyElements()
        {
            var tree = XmlTreeBuilder.Build(Encoding.UTF8.GetBytes("<HDon a=\"1\" b=\"x y\"><Ten>  Công ty  </Ten><Rong/><Khoi><A>1</A><A>2</A></Khoi></HDon>"));

            Assert.That(tree.Attributes, Is.EqualTo("a=\"1\" b=\"x y\""));
            Assert.That(tree.Children.Select(c => c.Name), Is.EqualTo(new[] { "Ten", "Rong", "Khoi" }));
            Assert.That(tree.Children[0].Value, Is.EqualTo("  Công ty  "), "Giữ nguyên nội dung chữ");
            Assert.That(tree.Children[1].Children, Is.Empty);
            Assert.That(tree.Children[1].Value, Is.Null);
            Assert.That(tree.Children[2].Children.Select(c => c.Value), Is.EqualTo(new[] { "1", "2" }));
        }

        [Test]
        public void Tree_RejectsExcessiveDepth()
        {
            var open = string.Concat(Enumerable.Repeat("<a>", XmlTreeBuilder.MaxDepth + 1));
            var close = string.Concat(Enumerable.Repeat("</a>", XmlTreeBuilder.MaxDepth + 1));

            Assert.Throws<XmlException>(() => XmlTreeBuilder.Build(Encoding.UTF8.GetBytes(open + close)));
        }

        [Test]
        public void Tree_RejectsDocumentsWithoutARoot()
        {
            Assert.Throws<XmlException>(() => XmlTreeBuilder.Build(Encoding.UTF8.GetBytes("   ")));
        }

        private sealed class FakeFiles : IInvoiceFileStore, IInvoiceFileReader
        {
            private readonly Dictionary<string, byte[]> _content = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> TooLarge = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public void Put(string path, byte[] bytes)
            {
                _content[path] = bytes;
            }

            public byte[] Read(string storageRoot, string relativePath, long maxBytes)
            {
                if (TooLarge.Contains(relativePath)) throw new InvoiceFileTooLargeException(maxBytes + 1, maxBytes);
                byte[] found;
                if (!_content.TryGetValue(relativePath, out found)) throw new FileNotFoundException("missing", relativePath);
                return found;
            }

            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName) { throw new NotSupportedException(); }
            public void DiscardTemp(string tempPath) { }
            public bool Exists(string storageRoot, string relativePath) { return _content.ContainsKey(relativePath); }
            public void Delete(string storageRoot, string relativePath) { }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }
    }
}
