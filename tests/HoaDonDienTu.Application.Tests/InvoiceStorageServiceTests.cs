using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class InvoiceStorageServiceTests
    {
        private const string Root = @"C:\Kho\0101234567";
        private static readonly CompanyId Id = CompanyId.NewId();

        private InMemoryInvoiceRepository _repo;
        private FakeFiles _files;
        private FakeContext _context;
        private InvoiceStorageService _service;
        private DownloadSession _session;

        [SetUp]
        public void SetUp()
        {
            _repo = new InMemoryInvoiceRepository();
            _files = new FakeFiles();
            _context = new FakeContext { Current = new CompanyContext(Id, TaxCode.Parse("0101234567"), "Công ty", Root, null) };
            _service = new InvoiceStorageService(_repo, _files, new InvoiceNormalizer(), _context, new Clock(), new Quiet());
            _session = NewSession(Root);
        }

        private static DownloadSession NewSession(string root)
        {
            return new DownloadSession(Guid.NewGuid(), Id, TaxCode.Parse("0101234567"), root, InvoiceDirection.Sold,
                PeriodSpec.ForMonth(2026, 10), new InvoiceSearchFilter(), DateTime.UtcNow, "t");
        }

        private static PortalInvoiceDto Dto(string number = "40")
        {
            return new PortalInvoiceDto
            {
                PortalInvoiceId = "d2240f37", FormSymbol = "1", InvoiceSymbol = "C26TBN", InvoiceNumber = number,
                IssueDate = new DateTime(2026, 10, 31), SellerTaxCode = "0101234567", SellerName = " Công ty ", BuyerTaxCode = "0101654633",
                BuyerName = "Người mua", TotalBeforeTax = 100m, TotalTax = 10m, TotalPayment = 110m, Currency = "VND",
                StatusRaw = "1", CheckResultRaw = "5"
            };
        }

        private static InvoiceXmlPayload Xml(string body = "<HDon><DLHDon><TTChung><SHDon>40</SHDon></TTChung></DLHDon></HDon>")
        {
            return new InvoiceXmlPayload(Encoding.UTF8.GetBytes(body), "x.xml");
        }

        // ---- Chuẩn hóa ----

        [Test]
        public void Normalizer_MapsFieldsCodesAndTrimsText()
        {
            var invoice = new InvoiceNormalizer().Normalize(Dto(), InvoiceDirection.Sold);

            Assert.That(invoice.SellerName, Is.EqualTo("Công ty"));
            Assert.That(invoice.InvoiceNumber, Is.EqualTo("40"));
            Assert.That(invoice.Status, Is.EqualTo(InvoiceStatus.New));
            Assert.That(invoice.StatusRaw, Is.EqualTo("1"));
            Assert.That(invoice.CheckResult, Is.EqualTo(InvoiceCheckResult.CodeIssued));
            Assert.That(invoice.TotalPayment, Is.EqualTo(110m));
            Assert.That(invoice.IssueDate, Is.EqualTo(new DateTime(2026, 10, 31)));
        }

        [Test]
        public void Normalizer_EmptyOptionalTextBecomesNull_AndUnknownCodesAreKeptRaw()
        {
            var dto = Dto();
            dto.BuyerTaxCode = "  ";
            dto.StatusRaw = "99";

            var invoice = new InvoiceNormalizer().Normalize(dto, InvoiceDirection.Purchase);

            Assert.That(invoice.BuyerTaxCode, Is.Null);
            Assert.That(invoice.Status, Is.EqualTo(InvoiceStatus.Unknown));
            Assert.That(invoice.StatusRaw, Is.EqualTo("99"), "Giữ giá trị nguồn để không mất thông tin");
            Assert.That(invoice.Direction, Is.EqualTo(InvoiceDirection.Purchase));
        }

        // ---- Lưu: mới / trùng / phiên bản ----

        [Test]
        public async Task NewInvoice_IsSaved_WithFileMetadataAndVersion1()
        {
            var result = await _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None);

            Assert.That(result, Is.EqualTo(InvoiceSaveResult.Saved));
            var stored = (await _repo.ListAsync(CancellationToken.None)).Single();
            Assert.That(stored.Versions, Has.Count.EqualTo(1));
            Assert.That(stored.Current.VersionNo, Is.EqualTo(1));
            Assert.That(stored.Current.Sha256, Has.Length.EqualTo(64));
            Assert.That(stored.Current.RelativePath, Does.EndWith("20261031_0101654633_1_C26TBN_40_v1.xml"));
            Assert.That(stored.Current.SourceSessionId, Is.EqualTo(_session.Id));
            Assert.That(_files.Committed, Has.Count.EqualTo(1));
            Assert.That(_files.TempCount, Is.EqualTo(0), "Không để lại file tạm");
        }

        [Test]
        public async Task SameContentAgain_OnlyUpdatesLastSeen_AC04()
        {
            await _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None);
            var first = (await _repo.ListAsync(CancellationToken.None)).Single().LastSeenAtUtc;

            var result = await _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None);

            Assert.That(result, Is.EqualTo(InvoiceSaveResult.AlreadyExists));
            var stored = (await _repo.ListAsync(CancellationToken.None)).Single();
            Assert.That(stored.Versions, Has.Count.EqualTo(1));
            Assert.That(_files.Committed, Has.Count.EqualTo(1), "Không ghi thêm file");
            Assert.That(stored.LastSeenAtUtc, Is.GreaterThan(first));
        }

        [Test]
        public async Task ChangedContent_CreatesANewCurrentVersion_AndKeepsTheOldOne_AC05()
        {
            await _service.UpsertAsync(_session, Dto(), Xml("<HDon><a>1</a></HDon>"), CancellationToken.None);

            var result = await _service.UpsertAsync(_session, Dto(), Xml("<HDon><a>2</a></HDon>"), CancellationToken.None);

            Assert.That(result, Is.EqualTo(InvoiceSaveResult.Saved));
            var stored = (await _repo.ListAsync(CancellationToken.None)).Single();
            Assert.That(stored.Versions.Select(v => v.VersionNo), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(stored.Versions.Count(v => v.IsCurrent), Is.EqualTo(1));
            Assert.That(stored.Current.VersionNo, Is.EqualTo(2));
            Assert.That(stored.Current.RelativePath, Does.EndWith("_v2.xml"));
            Assert.That(_files.Committed, Has.Count.EqualTo(2), "Giữ file của bản cũ");
        }

        [Test]
        public async Task LeadingZerosInTheNumber_AreTheSameInvoice()
        {
            await _service.UpsertAsync(_session, Dto("40"), Xml(), CancellationToken.None);

            var result = await _service.UpsertAsync(_session, Dto("0000040"), Xml(), CancellationToken.None);

            Assert.That(result, Is.EqualTo(InvoiceSaveResult.AlreadyExists));
            Assert.That(await _repo.ListAsync(CancellationToken.None), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DifferentInvoices_AreStoredSeparately()
        {
            await _service.UpsertAsync(_session, Dto("40"), Xml(), CancellationToken.None);
            await _service.UpsertAsync(_session, Dto("41"), Xml(), CancellationToken.None);

            Assert.That(await _repo.ListAsync(CancellationToken.None), Has.Count.EqualTo(2));
        }

        [Test]
        public async Task LatestMetadata_ReplacesTheOldOnes_WhenANewVersionArrives()
        {
            await _service.UpsertAsync(_session, Dto(), Xml("<HDon>a</HDon>"), CancellationToken.None);
            var changed = Dto();
            changed.StatusRaw = "6";

            await _service.UpsertAsync(_session, changed, Xml("<HDon>b</HDon>"), CancellationToken.None);

            Assert.That((await _repo.ListAsync(CancellationToken.None)).Single().Invoice.Status, Is.EqualTo(InvoiceStatus.Cancelled));
        }

        // ---- Lỗi: không tạo file chính thức, không báo thành công ----

        [TestCase("not xml at all")]
        [TestCase("<HDon><unclosed></HDon>")]
        [TestCase("<Other/>")]
        [TestCase("<?xml version=\"1.0\"?><!DOCTYPE HDon [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><HDon>&x;</HDon>")]
        public void BrokenOrUnsafeXml_IsRejected_AndLeavesNothingBehind(string body)
        {
            Assert.ThrowsAsync<InvoiceStorageException>(() => _service.UpsertAsync(_session, Dto(), Xml(body), CancellationToken.None));

            Assert.That(_files.Committed, Is.Empty);
            Assert.That(_files.TempCount, Is.EqualTo(0));
            Assert.That(_repo.ListAsync(CancellationToken.None).Result, Is.Empty);
        }

        [Test]
        public void TooLongPath_IsRejected_BeforeAnythingIsWritten()
        {
            var session = NewSession(@"C:\" + new string('a', 245));
            _context.Current = new CompanyContext(Id, TaxCode.Parse("0101234567"), "Công ty", session.StorageRoot, null);

            Assert.ThrowsAsync<InvoiceStorageException>(() => _service.UpsertAsync(session, Dto(), Xml(), CancellationToken.None));

            Assert.That(_files.TempCount, Is.EqualTo(0));
        }

        [Test]
        public void WriteFailure_PropagatesAsIo_AndNothingIsRecorded()
        {
            _files.FailWrite = true;

            Assert.ThrowsAsync<System.IO.IOException>(() => _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None));

            Assert.That(_repo.ListAsync(CancellationToken.None).Result, Is.Empty);
            Assert.That(_files.Committed, Is.Empty);
        }

        [Test]
        public void CommitFailure_DiscardsTheTempFile()
        {
            _files.FailCommit = true;

            Assert.ThrowsAsync<System.IO.IOException>(() => _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None));

            Assert.That(_files.TempCount, Is.EqualTo(0));
            Assert.That(_repo.ListAsync(CancellationToken.None).Result, Is.Empty);
        }

        [Test]
        public void MetadataFailure_DeletesTheFileThatWasJustCommitted()
        {
            var service = new InvoiceStorageService(new FailingRepo(), _files, new InvoiceNormalizer(), _context, new Clock(), new Quiet());

            Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None));

            Assert.That(_files.Committed, Is.Empty, "Không để file mồ côi");
        }

        // ---- Guard: đúng MST / đúng kho ----

        [Test]
        public void NoCompanyOpen_IsRejected()
        {
            _context.Current = null;

            Assert.ThrowsAsync<StorageGuardException>(() => _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None));
        }

        [Test]
        public void SessionOfAnotherCompany_IsRejected_NFR_SEC_006()
        {
            _context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0109999999"), "Khác", @"C:\Kho\0109999999", null);

            Assert.ThrowsAsync<StorageGuardException>(() => _service.UpsertAsync(_session, Dto(), Xml(), CancellationToken.None));
            Assert.That(_files.Committed, Is.Empty);
        }

        [Test]
        public void SessionBoundToAnotherStorageRoot_IsRejected()
        {
            var session = NewSession(@"C:\Kho\Khac");

            Assert.ThrowsAsync<StorageGuardException>(() => _service.UpsertAsync(session, Dto(), Xml(), CancellationToken.None));
        }

        // ---- Fakes ----

        private sealed class FakeFiles : IInvoiceFileStore
        {
            private readonly HashSet<string> _temps = new HashSet<string>();
            public List<string> Committed = new List<string>();
            public bool FailWrite;
            public bool FailCommit;

            public int TempCount
            {
                get { return _temps.Count; }
            }

            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken)
            {
                if (FailWrite) throw new System.IO.IOException("disk full");
                var temp = relativeFolder + "\\" + Guid.NewGuid().ToString("N") + ".tmp";
                _temps.Add(temp);
                return Task.FromResult(temp);
            }

            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName)
            {
                if (FailCommit) throw new System.IO.IOException("locked");
                _temps.Remove(tempPath);
                var path = relativeFolder + "\\" + fileName;
                Committed.Add(path);
                return path;
            }

            public void DiscardTemp(string tempPath)
            {
                _temps.Remove(tempPath);
            }

            public bool Exists(string storageRoot, string relativePath)
            {
                return Committed.Contains(relativePath);
            }

            public void Delete(string storageRoot, string relativePath)
            {
                Committed.Remove(relativePath);
            }
        }

        private sealed class FailingRepo : IInvoiceRepository
        {
            public Task<StoredInvoice> FindAsync(string businessKey, CancellationToken cancellationToken) { return Task.FromResult<StoredInvoice>(null); }
            public Task AddAsync(StoredInvoice invoice, CancellationToken cancellationToken) { throw new InvalidOperationException("db"); }
            public Task AddVersionAsync(Guid invoiceId, InvoiceVersionRecord version, NormalizedInvoice latest, DateTime nowUtc, CancellationToken cancellationToken) { throw new InvalidOperationException("db"); }
            public Task TouchAsync(Guid invoiceId, DateTime nowUtc, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task<IList<StoredInvoice>> ListAsync(CancellationToken cancellationToken) { return Task.FromResult<IList<StoredInvoice>>(new List<StoredInvoice>()); }
        }

        private sealed class FakeContext : ICompanyContextAccessor
        {
            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; set; }
        }

        private sealed class Clock : IClock
        {
            private DateTime _now = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

            public DateTime UtcNow
            {
                get
                {
                    _now = _now.AddSeconds(1);
                    return _now;
                }
            }
        }

        private sealed class Quiet : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
