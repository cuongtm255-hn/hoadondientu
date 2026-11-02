#if DEBUG
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>
    /// Truy vấn Kho hóa đơn (DD-07, T07-02/T07-08) trên SQLite và trên kho bộ nhớ với cùng dữ liệu. Toàn bộ dữ liệu là DỮ LIỆU GIẢ
    /// sinh trong test (MOCK), không gọi cổng Thuế.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    public class SqliteArchiveQueryTests
    {
        private const string Mst = "0109213133";

        private string _root;
        private Context _context;
        private CompanyDatabaseProvider _provider;
        private SqliteInvoiceRepository _sqlite;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-archive-" + Guid.NewGuid().ToString("N"), "Kho", Mst);
            _context = new Context(_root);
            _provider = new CompanyDatabaseProvider(_context);
            _sqlite = new SqliteInvoiceRepository(_provider);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(_root)), true);
            }
            catch (IOException)
            {
            }
        }

        // ---- Cùng kết quả với kho bộ nhớ ----

        [TestCaseSource("Queries")]
        public async Task Sqlite_GivesTheSameRowsInTheSameOrder_AsTheInMemoryRepository(ArchiveQuery query)
        {
            var memory = new InMemoryInvoiceRepository();
            foreach (var invoice in Dataset())
            {
                await memory.AddAsync(invoice, CancellationToken.None);
                await _sqlite.AddAsync(invoice, CancellationToken.None);
            }

            var criteria = ArchiveCriteria.From(query);
            var expected = await memory.ListFileRefsAsync(criteria, CancellationToken.None);
            var actual = await _sqlite.ListFileRefsAsync(criteria, CancellationToken.None);

            Assert.That(actual.Select(r => r.InvoiceId), Is.EqualTo(expected.Select(r => r.InvoiceId)), "thứ tự và tập khớp");
            Assert.That(actual.Select(r => r.RelativePath), Is.EqualTo(expected.Select(r => r.RelativePath)));
            Assert.That(actual.Select(r => r.VersionCount), Is.EqualTo(expected.Select(r => r.VersionCount)));
            Assert.That(actual.Sum(r => r.TotalPayment), Is.EqualTo(expected.Sum(r => r.TotalPayment)));
        }

        public static IEnumerable<TestCaseData> Queries()
        {
            Func<string, Action<ArchiveQuery>, TestCaseData> make = (name, tweak) =>
            {
                var q = new ArchiveQuery { Direction = InvoiceDirection.Sold };
                tweak(q);
                return new TestCaseData(q).SetName("Query_" + name);
            };

            yield return make("SoldDefault", q => { });
            yield return make("Purchase", q => q.Direction = InvoiceDirection.Purchase);
            yield return make("DateRange", q => { q.IssueFrom = new DateTime(2026, 9, 1); q.IssueTo = new DateTime(2026, 10, 15); });
            yield return make("Status", q => q.StatusCode = "2");
            yield return make("CheckResult", q => q.CheckResultCode = "6");
            yield return make("FormSymbolIgnoresCase", q => q.FormSymbol = "1");
            yield return make("InvoiceSymbolIgnoresCase", q => q.InvoiceSymbol = "c26tbn");
            yield return make("NumberIgnoresLeadingZeros", q => q.InvoiceNumber = "00005");
            yield return make("BuyerTaxCode", q => q.CounterpartyTaxCode = "0300000001");
            yield return make("BuyerIdNumber", q => q.CounterpartyTaxCode = "001092008840");
            yield return make("PurchaseSellerTaxCode", q => { q.Direction = InvoiceDirection.Purchase; q.CounterpartyTaxCode = "0100000002"; });
            yield return make("QuickSearchWithoutAccents", q => q.QuickSearch = "dong a");
            yield return make("QuickSearchAccented", q => q.QuickSearch = "Công ty");
            yield return make("QuickSearchNumber", q => q.QuickSearch = "12");
            yield return make("QuickSearchLikeWildcards", q => q.QuickSearch = "100%");
            yield return make("QuickSearchUnderscore", q => q.QuickSearch = "xanh_la");
            yield return make("QuickSearchQuoteIsLiteral", q => q.QuickSearch = "x' OR '1'='1");
            yield return make("SortNumberAscending", q => { q.SortKey = ArchiveSortKey.InvoiceNumber; q.SortDescending = false; });
            yield return make("SortNumberDescending", q => { q.SortKey = ArchiveSortKey.InvoiceNumber; q.SortDescending = true; });
            yield return make("SortCounterpartyAscending", q => { q.SortKey = ArchiveSortKey.Counterparty; q.SortDescending = false; });
            yield return make("SortCounterpartyDescending", q => { q.SortKey = ArchiveSortKey.Counterparty; q.SortDescending = true; });
            yield return make("SortPaymentAscending", q => { q.SortKey = ArchiveSortKey.TotalPayment; q.SortDescending = false; });
            yield return make("SortPaymentDescending", q => { q.SortKey = ArchiveSortKey.TotalPayment; q.SortDescending = true; });
            yield return make("SortDateAscending", q => q.SortDescending = false);
            yield return make("Combined", q => { q.Direction = InvoiceDirection.Purchase; q.CheckResultCode = "5"; q.QuickSearch = "cong ty"; q.SortKey = ArchiveSortKey.Counterparty; });
            yield return make("NothingMatches", q => q.InvoiceNumber = "999999");
        }

        [Test]
        public async Task QuickSearchWithQuote_IsNotInjected_AndTheTableSurvives()
        {
            foreach (var invoice in Dataset()) await _sqlite.AddAsync(invoice, CancellationToken.None);
            var query = new ArchiveQuery { Direction = InvoiceDirection.Sold, QuickSearch = "'; DROP TABLE invoice; --" };

            var refs = await _sqlite.ListFileRefsAsync(ArchiveCriteria.From(query), CancellationToken.None);

            Assert.That(refs, Is.Empty);
            Assert.That(await _sqlite.ListAsync(CancellationToken.None), Has.Count.EqualTo(Dataset().Count));
        }

        [Test]
        public async Task FindMany_ReturnsFullInvoicesWithVersions_InTheRequestedOrder_AndSkipsUnknownIds()
        {
            var data = Dataset();
            foreach (var invoice in data) await _sqlite.AddAsync(invoice, CancellationToken.None);
            var withTwoVersions = data[3];
            await _sqlite.AddVersionAsync(
                withTwoVersions.InvoiceId,
                new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 2, Sha256 = "def", RelativePath = "v2.xml", SizeBytes = 1, CreatedAtUtc = DateTime.UtcNow, SourceSessionId = Guid.NewGuid() },
                withTwoVersions.Invoice,
                DateTime.UtcNow,
                CancellationToken.None);

            var ids = new List<Guid> { data[5].InvoiceId, Guid.NewGuid(), withTwoVersions.InvoiceId, data[0].InvoiceId };
            var found = await _sqlite.FindManyAsync(ids, CancellationToken.None);

            Assert.That(found.Select(f => f.InvoiceId), Is.EqualTo(new[] { data[5].InvoiceId, withTwoVersions.InvoiceId, data[0].InvoiceId }));
            Assert.That(found[1].Versions, Has.Count.EqualTo(2));
            Assert.That(found[1].Current.RelativePath, Is.EqualTo("v2.xml"));
        }

        [Test]
        public async Task FindMany_MoreIdsThanOneChunk_ReturnsAll()
        {
            var ids = new List<Guid>();
            foreach (var invoice in Bulk(700))
            {
                ids.Add(invoice.InvoiceId);
                await _sqlite.AddAsync(invoice, CancellationToken.None);
            }

            var found = await _sqlite.FindManyAsync(ids, CancellationToken.None);

            Assert.That(found.Select(f => f.InvoiceId), Is.EqualTo(ids));
        }

        [Test]
        public async Task Years_AreDistinctNewestFirst_PerDirection()
        {
            var data = Dataset();
            data[0].Invoice.IssueDate = new DateTime(2024, 3, 1);
            data[0].Invoice.Direction = InvoiceDirection.Sold;
            foreach (var invoice in data) await _sqlite.AddAsync(invoice, CancellationToken.None);

            var sold = await _sqlite.ListYearsAsync(InvoiceDirection.Sold, CancellationToken.None);

            Assert.That(sold, Is.EqualTo(new[] { 2026, 2024 }));
        }

        [Test]
        public async Task UpdatedInvoice_RefreshesTheSortAndSearchKeys()
        {
            var invoice = Dataset()[2];
            await _sqlite.AddAsync(invoice, CancellationToken.None);
            var changed = invoice.Invoice;
            changed.InvoiceNumber = "77";
            changed.BuyerName = "Công ty Hoàn Mỹ";
            changed.SellerName = "Công ty Hoàn Mỹ";
            await _sqlite.TouchAsync(invoice.InvoiceId, DateTime.UtcNow, changed, null, CancellationToken.None);

            var byNumber = await _sqlite.ListFileRefsAsync(ArchiveCriteria.From(new ArchiveQuery { Direction = changed.Direction, InvoiceNumber = "0077" }), CancellationToken.None);
            var byName = await _sqlite.ListFileRefsAsync(ArchiveCriteria.From(new ArchiveQuery { Direction = changed.Direction, QuickSearch = "hoan my" }), CancellationToken.None);

            Assert.That(byNumber.Select(r => r.InvoiceId), Is.EqualTo(new[] { invoice.InvoiceId }));
            Assert.That(byName.Select(r => r.InvoiceId), Is.EqualTo(new[] { invoice.InvoiceId }));
        }

        [Test]
        public async Task FileRefs_FollowNewVersions_AndRestoredPaths()
        {
            var invoice = Dataset()[1];
            await _sqlite.AddAsync(invoice, CancellationToken.None);
            var all = ArchiveCriteria.From(new ArchiveQuery { Direction = invoice.Invoice.Direction });

            await _sqlite.AddVersionAsync(
                invoice.InvoiceId,
                new InvoiceVersionRecord { VersionId = Guid.NewGuid(), VersionNo = 2, Sha256 = "def", RelativePath = "v2.xml", SizeBytes = 1, CreatedAtUtc = DateTime.UtcNow, SourceSessionId = Guid.NewGuid() },
                invoice.Invoice,
                DateTime.UtcNow,
                CancellationToken.None);
            var afterVersion = (await _sqlite.ListFileRefsAsync(all, CancellationToken.None)).Single(r => r.InvoiceId == invoice.InvoiceId);
            await _sqlite.TouchAsync(invoice.InvoiceId, DateTime.UtcNow, null, "restored.xml", CancellationToken.None);
            var afterRestore = (await _sqlite.ListFileRefsAsync(all, CancellationToken.None)).Single(r => r.InvoiceId == invoice.InvoiceId);

            Assert.That(afterVersion.VersionCount, Is.EqualTo(2));
            Assert.That(afterVersion.RelativePath, Is.EqualTo("v2.xml"));
            Assert.That(afterRestore.RelativePath, Is.EqualTo("restored.xml"));
            Assert.That(afterRestore.VersionCount, Is.EqualTo(2));
        }

        // ---- Migration 2: điền khóa cho dữ liệu cũ ----

        [Test]
        public void Migration2_BackfillsKeysOfInvoicesStoredBeforeIt()
        {
            var path = Path.Combine(_root, "Data", "old.db");
            var v1 = new[] { CompanyDatabaseSchema.Migrations[0] };
            new CompanyDatabaseInitializer(v1).Initialize(path);
            var old = new SqliteDatabase(path);
            using (var c = old.Open())
            {
                SqliteDatabase.Run(
                    c,
                    "INSERT INTO invoice (id, business_key, direction, issue_date, invoice_number, seller_name, buyer_name, status, check_result, search_text, first_seen_utc, last_seen_utc) " +
                    "VALUES ('a', 'k1', 0, '2026-10-01', '0000040', 'CÔNG TY ĐÔNG Á', 'Nguyễn Văn Mai', 1, 1, 'x', '2026-11-01T00:00:00Z', '2026-11-01T00:00:00Z')");
                SqliteDatabase.Run(
                    c,
                    "INSERT INTO invoice (id, business_key, direction, issue_date, invoice_number, seller_name, buyer_name, status, check_result, search_text, first_seen_utc, last_seen_utc) " +
                    "VALUES ('b', 'k2', 1, '2026-10-01', 'AB12', 'CÔNG TY ĐÔNG Á', 'Nguyễn Văn Mai', 1, 1, 'x', '2026-11-01T00:00:00Z', '2026-11-01T00:00:00Z')");
            }

            var upgraded = new CompanyDatabaseInitializer().Initialize(path);

            using (var c = upgraded.Open())
            {
                Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(CompanyDatabaseSchema.LatestVersion));
                Assert.That(SqliteDatabase.Scalar(c, "SELECT number_key FROM invoice WHERE id='a'"), Is.EqualTo("00000000000000000040"));
                Assert.That(SqliteDatabase.Scalar(c, "SELECT number_key FROM invoice WHERE id='b'"), Is.EqualTo("0000000000000000AB12"));
                var enumValue = (int)InvoiceDirection.Purchase;
                Assert.That(SqliteDatabase.Scalar(c, "SELECT counterparty_sort FROM invoice WHERE direction=" + enumValue), Is.EqualTo("cong ty dong a"));
                Assert.That(SqliteDatabase.Scalar(c, "SELECT counterparty_sort FROM invoice WHERE direction<>" + enumValue), Is.EqualTo("nguyen van mai"));
            }
        }

        // ---- T07-08: 100.000 hóa đơn (NFR-PERF-002) ----

        [Test]
        [Category("Performance")]
        public async Task HundredThousandInvoices_OpenFirstPage_AndSearchByNumberOrTaxCode_AreWithinTwoSeconds()
        {
            const int total = 100000;
            var seedTimer = Stopwatch.StartNew();
            await Seed(total);
            Console.WriteLine("[MOCK] Nạp {0:N0} hóa đơn giả: {1} ms", total, seedTimer.ElapsedMilliseconds);

            var service = new InvoiceArchiveService(_sqlite, new AlwaysThere(), _context);
            var open = await Timed("Mở trang đầu (không lọc, mới nhất trước)", () => service.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold }, CancellationToken.None));
            Assert.That(open.TotalCount, Is.EqualTo(total / 2));
            Assert.That(open.Rows, Has.Count.EqualTo(50));
            Assert.That(open.Summary.TotalPayment, Is.GreaterThan(0m));

            var byNumber = await Timed("Tìm theo số hóa đơn", () => service.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold, InvoiceNumber = "4242" }, CancellationToken.None));
            Assert.That(byNumber.TotalCount, Is.EqualTo(1));

            var byTaxCode = await Timed("Tìm theo MST đối tác", () => service.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold, CounterpartyTaxCode = "0300000008" }, CancellationToken.None));
            Assert.That(byTaxCode.TotalCount, Is.GreaterThan(0));

            var quick = await Timed("Tìm nhanh theo tên không dấu", () => service.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold, QuickSearch = "dong a 77" }, CancellationToken.None));
            Assert.That(quick.TotalCount, Is.GreaterThan(0));

            var sorted = await Timed("Sắp xếp theo đối tác, trang 100", () => service.QueryAsync(new ArchiveQuery { Direction = InvoiceDirection.Sold, SortKey = ArchiveSortKey.Counterparty, PageIndex = 99 }, CancellationToken.None));
            Assert.That(sorted.PageIndex, Is.EqualTo(99));

            var years = await Timed("Danh sách năm", () => service.YearsAsync(InvoiceDirection.Sold, CancellationToken.None));
            Assert.That(years, Is.Not.Empty);
        }


        private async Task<T> Timed<T>(string what, Func<Task<T>> run)
        {
            await run(); // lần đầu nạp bộ nhớ đệm của SQLite/JIT; số đo là lần chạy ngay sau đó, như người dùng gõ tìm kiếm liên tiếp
            var timer = Stopwatch.StartNew();
            var result = await run();
            timer.Stop();
            Console.WriteLine("[MOCK] {0}: {1} ms", what, timer.ElapsedMilliseconds);
            Assert.That(timer.ElapsedMilliseconds, Is.LessThan(2000), what);
            return result;
        }

        /// <summary>Nạp nhanh bằng một giao dịch và một lệnh dùng lại; đi qua cùng hàm khóa với đường ghi thật.</summary>
        private Task Seed(int total)
        {
            return _provider.Current().WriteAsync(
                c =>
                {
                    using (var invoice = SqliteDatabase.Command(
                        c,
                        "INSERT INTO invoice (id, business_key, direction, issue_date, invoice_number, seller_tax_code, seller_name, buyer_tax_code, buyer_name, total_payment, status, status_raw, check_result, check_result_raw, " +
                        "search_text, number_key, counterparty_sort, current_path, version_count, first_seen_utc, last_seen_utc) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, 1, '1', 1, '5', @p10, @p11, @p12, @p13, 1, '2026-11-01T00:00:00Z', '2026-11-01T00:00:00Z')",
                        new object[14]))
                    using (var version = SqliteDatabase.Command(
                        c,
                        "INSERT INTO invoice_version (id, invoice_id, version_no, sha256, relative_path, size_bytes, created_utc, is_current, source_session_id) VALUES (@p0, @p1, 1, 'h', @p2, 100, '2026-11-01T00:00:00Z', 1, @p3)",
                        new object[4]))
                    {
                        for (var n = 0; n < total; n++)
                        {
                            var direction = n % 2 == 0 ? InvoiceDirection.Sold : InvoiceDirection.Purchase;
                            var id = Guid.NewGuid();
                            var number = n.ToString();
                            var seller = "01" + (n % 2000).ToString("D8");
                            var buyer = "03" + (n % 500).ToString("D8");
                            var counterpartyName = "CÔNG TY ĐÔNG Á " + (n % 9000);
                            var counterpartyId = direction == InvoiceDirection.Purchase ? seller : buyer;
                            var date = new DateTime(2024, 1, 1).AddDays(n % 1000);
                            var probe = new NormalizedInvoice
                            {
                                Direction = direction, InvoiceNumber = number, InvoiceSymbol = "C26TBN", SellerTaxCode = seller, BuyerTaxCode = buyer,
                                SellerName = counterpartyName, BuyerName = counterpartyName
                            };
                            var values = invoice.Parameters;
                            values["@p0"].Value = SqliteValues.Id(id);
                            values["@p1"].Value = "k" + n;
                            values["@p2"].Value = (int)direction;
                            values["@p3"].Value = SqliteValues.Date(date);
                            values["@p4"].Value = number;
                            values["@p5"].Value = seller;
                            values["@p6"].Value = counterpartyName;
                            values["@p7"].Value = buyer;
                            values["@p8"].Value = counterpartyName;
                            values["@p9"].Value = (long)(1000 + n) * CompanyDatabaseSchema.MoneyScale;
                            values["@p10"].Value = ArchiveKeys.SearchText(probe);
                            values["@p11"].Value = ArchiveKeys.NumberKey(number);
                            values["@p12"].Value = ArchiveKeys.CounterpartySort(probe);
                            values["@p13"].Value = "f" + n + ".xml";
                            invoice.ExecuteNonQuery();

                            version.Parameters["@p0"].Value = SqliteValues.Id(Guid.NewGuid());
                            version.Parameters["@p1"].Value = SqliteValues.Id(id);
                            version.Parameters["@p2"].Value = @"2026\10\f" + n + ".xml";
                            version.Parameters["@p3"].Value = SqliteValues.Id(Guid.Empty);
                            version.ExecuteNonQuery();
                        }
                    }
                },
                CancellationToken.None);
        }

        // ---- Dữ liệu ----

        private static List<StoredInvoice> Dataset()
        {
            var names = new[] { "CÔNG TY ĐÔNG Á", "Công ty An Phát", "Nguyễn Văn Mai", "Đỗ Thị Lan", "CTY 100% xanh_lá" };
            var list = new List<StoredInvoice>();
            for (var k = 0; k < 24; k++)
            {
                var direction = (InvoiceDirection)(k % 2);
                var number = k % 2 == 0 ? (k + 1).ToString() : (k + 1).ToString().PadLeft(5, '0');
                var invoice = new NormalizedInvoice
                {
                    Direction = direction,
                    PortalInvoiceId = Guid.NewGuid().ToString(),
                    FormSymbol = k % 3 == 0 ? "2" : "1",
                    InvoiceSymbol = k % 4 == 0 ? "c26tbn" : "C26TBN",
                    InvoiceNumber = number,
                    IssueDate = new DateTime(2026, 8 + k % 4, 1 + k % 27),
                    SellerTaxCode = k % 5 == 0 ? "0100000002" : "0100000001",
                    SellerName = names[k % names.Length],
                    BuyerTaxCode = k % 3 == 0 ? "0300000001" : null,
                    BuyerIdNumber = k % 3 == 0 ? null : "001092008840",
                    BuyerName = names[(k + 2) % names.Length],
                    TotalBeforeTax = k * 1000.5m,
                    TotalTax = k * 100m,
                    TotalPayment = k % 6 == 0 ? (decimal?)null : k * 1100.5m,
                    Currency = "VND",
                    Status = InvoiceStatus.New,
                    StatusRaw = k % 2 == 0 ? "1" : "2",
                    CheckResult = InvoiceCheckResult.CodeIssued,
                    CheckResultRaw = new[] { "5", "6", "8" }[k % 3]
                };
                list.Add(Stored(invoice, k));
            }

            return list;
        }

        private static IEnumerable<StoredInvoice> Bulk(int count)
        {
            for (var k = 0; k < count; k++)
            {
                yield return Stored(
                    new NormalizedInvoice
                    {
                        Direction = InvoiceDirection.Sold, FormSymbol = "1", InvoiceSymbol = "C26TBN", InvoiceNumber = (k + 1).ToString(), IssueDate = new DateTime(2026, 10, 1),
                        SellerTaxCode = Mst, BuyerTaxCode = "0300000001", BuyerName = "Khách " + k, StatusRaw = "1", CheckResultRaw = "5"
                    },
                    k);
            }
        }

        private static StoredInvoice Stored(NormalizedInvoice invoice, int k)
        {
            var now = new DateTime(2026, 11, 1, 2, 3, 4, DateTimeKind.Utc);
            return new StoredInvoice
            {
                InvoiceId = Guid.NewGuid(),
                BusinessKey = BusinessKey.Build(invoice),
                Invoice = invoice,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                Versions = new List<InvoiceVersionRecord>
                {
                    new InvoiceVersionRecord
                    {
                        VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = "h" + k, RelativePath = @"2026\10\f" + k + ".xml", SizeBytes = 100, CreatedAtUtc = now, IsCurrent = true, SourceSessionId = Guid.NewGuid()
                    }
                }
            };
        }

        private sealed class Context : ICompanyContextAccessor
        {
            private readonly CompanyContext _context;

            public Context(string root)
            {
                _context = new CompanyContext(CompanyId.NewId(), TaxCode.Parse(Mst), "Công ty mẫu", root, null);
            }

            public bool HasCurrent { get { return true; } }
            public CompanyContext Current { get { return _context; } }
        }

        private sealed class AlwaysThere : IInvoiceFileStore
        {
            public Task<string> WriteTempAsync(string storageRoot, string relativeFolder, byte[] content, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public string Commit(string storageRoot, string tempPath, string relativeFolder, string fileName) { throw new NotSupportedException(); }
            public void DiscardTemp(string tempPath) { }
            public bool Exists(string storageRoot, string relativePath) { return true; }
            public void Delete(string storageRoot, string relativePath) { }
        }
    }
}
#endif
