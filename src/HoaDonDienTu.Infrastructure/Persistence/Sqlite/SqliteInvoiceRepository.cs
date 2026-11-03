using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>
    /// <see cref="IInvoiceRepository"/> trên <c>company.db</c> của doanh nghiệp đang mở (ADR 0006, DD-06 mục 4). Mỗi thao tác ghi là một
    /// giao dịch đã commit trước khi trả về; khóa nghiệp vụ trùng trả <see cref="InvalidOperationException"/> như bản trong bộ nhớ.
    /// </summary>
    public sealed class SqliteInvoiceRepository : IInvoiceRepository
    {
        private const string InvoiceColumns =
            "id, business_key, direction, portal_invoice_id, form_symbol, invoice_symbol, invoice_number, issue_date, seller_tax_code, seller_name, " +
            "buyer_tax_code, buyer_id_number, buyer_name, buyer_address, total_before_tax, total_tax, total_discount, total_fee, total_payment, " +
            "currency, exchange_rate, status, status_raw, check_result, check_result_raw, first_seen_utc, last_seen_utc";

        private const string VersionColumns = "id, invoice_id, version_no, sha256, relative_path, size_bytes, created_utc, is_current, source_session_id";

        private const int IdChunk = 300;

        private readonly CompanyDatabaseProvider _provider;

        public SqliteInvoiceRepository(CompanyDatabaseProvider provider)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            _provider = provider;
        }

        public Task<StoredInvoice> FindAsync(string businessKey, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(c => LoadOne(c, "business_key = @p0", businessKey), cancellationToken);
        }

        public Task<StoredInvoice> FindByIdAsync(Guid invoiceId, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(c => LoadOne(c, "id = @p0", SqliteValues.Id(invoiceId)), cancellationToken);
        }

        public Task AddAsync(StoredInvoice invoice, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(
                c =>
                {
                    try
                    {
                        InsertInvoice(c, invoice);
                    }
                    catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint && IsBusinessKeyConflict(ex))
                    {
                        throw new InvalidOperationException("Khóa nghiệp vụ đã tồn tại.", ex);
                    }

                    foreach (var version in invoice.Versions) InsertVersion(c, invoice.InvoiceId, version);
                },
                cancellationToken);
        }

        public Task AddVersionAsync(Guid invoiceId, InvoiceVersionRecord version, NormalizedInvoice latest, DateTime nowUtc, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(
                c =>
                {
                    RequireInvoice(c, invoiceId);

                    // Bản cũ bỏ cờ hiện hành trước khi thêm bản mới: chỉ mục duy nhất cho phép một bản hiện hành mỗi hóa đơn.
                    SqliteDatabase.Run(c, "UPDATE invoice_version SET is_current = 0 WHERE invoice_id = @p0", SqliteValues.Id(invoiceId));
                    version.IsCurrent = true;
                    InsertVersion(c, invoiceId, version);
                    SqliteDatabase.Run(c, "UPDATE invoice SET current_path = @p1, version_count = version_count + 1 WHERE id = @p0", SqliteValues.Id(invoiceId), version.RelativePath);
                    UpdateInvoice(c, invoiceId, latest, nowUtc);
                },
                cancellationToken);
        }

        public Task TouchAsync(Guid invoiceId, DateTime nowUtc, NormalizedInvoice latest, string restoredRelativePath, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(
                c =>
                {
                    RequireInvoice(c, invoiceId);
                    if (latest != null) UpdateInvoice(c, invoiceId, latest, nowUtc);
                    else SqliteDatabase.Run(c, "UPDATE invoice SET last_seen_utc = @p0 WHERE id = @p1", SqliteValues.Time(nowUtc), SqliteValues.Id(invoiceId));

                    if (restoredRelativePath != null)
                    {
                        SqliteDatabase.Run(
                            c, "UPDATE invoice_version SET relative_path = @p0 WHERE invoice_id = @p1 AND is_current = 1", restoredRelativePath, SqliteValues.Id(invoiceId));
                        SqliteDatabase.Run(c, "UPDATE invoice SET current_path = @p0 WHERE id = @p1", restoredRelativePath, SqliteValues.Id(invoiceId));
                    }
                },
                cancellationToken);
        }

        public Task<IList<StoredInvoice>> ListAsync(CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<StoredInvoice> result = new List<StoredInvoice>();
                    var byId = new Dictionary<Guid, StoredInvoice>();
                    using (var command = SqliteDatabase.Command(c, "SELECT " + InvoiceColumns + " FROM invoice"))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var stored = ReadInvoice(reader);
                            byId[stored.InvoiceId] = stored;
                            result.Add(stored);
                        }
                    }

                    using (var command = SqliteDatabase.Command(c, "SELECT " + VersionColumns + " FROM invoice_version ORDER BY invoice_id, version_no"))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var version = ReadVersion(reader);
                            StoredInvoice owner;
                            if (byId.TryGetValue(SqliteValues.ParseId(reader["invoice_id"]), out owner)) owner.Versions.Add(version);
                        }
                    }

                    return result;
                },
                cancellationToken);
        }

        public Task<IList<InvoiceFileRef>> ListFileRefsAsync(ArchiveCriteria criteria, CancellationToken cancellationToken)
        {
            var filter = Filter(criteria);
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<InvoiceFileRef> refs = new List<InvoiceFileRef>();
                    var sql =
                        "SELECT i.id, i.total_before_tax, i.total_tax, i.total_payment, i.current_path AS relative_path, i.version_count " +
                        "FROM invoice i WHERE " + filter.Where + " ORDER BY " + OrderBy(criteria);
                    using (var command = SqliteDatabase.Command(c, sql, filter.Values.ToArray()))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            refs.Add(
                                new InvoiceFileRef
                                {
                                    InvoiceId = SqliteValues.ParseId(reader["id"]),
                                    RelativePath = SqliteValues.Text(reader["relative_path"]),
                                    VersionCount = SqliteValues.Int(reader["version_count"]),
                                    TotalBeforeTax = SqliteValues.ParseMoney(reader["total_before_tax"]) ?? 0m,
                                    TotalTax = SqliteValues.ParseMoney(reader["total_tax"]) ?? 0m,
                                    TotalPayment = SqliteValues.ParseMoney(reader["total_payment"]) ?? 0m
                                });
                        }
                    }

                    return refs;
                },
                cancellationToken);
        }

        public Task<IList<StoredInvoice>> FindManyAsync(IList<Guid> ids, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    var byId = new Dictionary<Guid, StoredInvoice>();
                    for (var offset = 0; offset < ids.Count; offset += IdChunk)
                    {
                        var chunk = new List<object>();
                        for (var n = offset; n < Math.Min(ids.Count, offset + IdChunk); n++) chunk.Add(SqliteValues.Id(ids[n]));
                        var marks = string.Join(", ", chunk.Select((_, n) => "@p" + n.ToString(CultureInfo.InvariantCulture)));
                        using (var command = SqliteDatabase.Command(c, "SELECT " + InvoiceColumns + " FROM invoice WHERE id IN (" + marks + ")", chunk.ToArray()))
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var stored = ReadInvoice(reader);
                                byId[stored.InvoiceId] = stored;
                            }
                        }

                        using (var command = SqliteDatabase.Command(c, "SELECT " + VersionColumns + " FROM invoice_version WHERE invoice_id IN (" + marks + ") ORDER BY invoice_id, version_no", chunk.ToArray()))
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                StoredInvoice owner;
                                if (byId.TryGetValue(SqliteValues.ParseId(reader["invoice_id"]), out owner)) owner.Versions.Add(ReadVersion(reader));
                            }
                        }
                    }

                    IList<StoredInvoice> ordered = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                    return ordered;
                },
                cancellationToken);
        }

        public Task<IList<int>> ListYearsAsync(InvoiceDirection direction, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<int> years = new List<int>();
                    using (var command = SqliteDatabase.Command(c, "SELECT DISTINCT CAST(substr(issue_date, 1, 4) AS INTEGER) AS y FROM invoice WHERE direction = @p0 ORDER BY y DESC", (int)direction))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) years.Add(SqliteValues.Int(reader["y"]));
                    }

                    return years;
                },
                cancellationToken);
        }

        private sealed class SqlFilter
        {
            public string Where;
            public readonly List<object> Values = new List<object>();

            public string Add(object value)
            {
                Values.Add(value);
                return "@p" + (Values.Count - 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Dịch <see cref="ArchiveCriteria"/> sang mệnh đề WHERE có tham số; giá trị người dùng không bao giờ được nối vào câu lệnh.</summary>
        private static SqlFilter Filter(ArchiveCriteria q)
        {
            var f = new SqlFilter();
            var clauses = new List<string>();
            clauses.Add("i.direction = " + f.Add((int)q.Direction));
            if (q.IssueFrom.HasValue) clauses.Add("i.issue_date >= " + f.Add(SqliteValues.Date(q.IssueFrom.Value)));
            if (q.IssueTo.HasValue) clauses.Add("i.issue_date <= " + f.Add(SqliteValues.Date(q.IssueTo.Value)));
            if (q.StatusCode != null) clauses.Add("i.status_raw = " + f.Add(q.StatusCode));
            if (q.CheckResultCode != null) clauses.Add("i.check_result_raw = " + f.Add(q.CheckResultCode));
            if (q.FormSymbol != null) clauses.Add("i.form_symbol = " + f.Add(q.FormSymbol) + " COLLATE NOCASE");
            if (q.InvoiceSymbol != null) clauses.Add("i.invoice_symbol = " + f.Add(q.InvoiceSymbol) + " COLLATE NOCASE");
            if (q.NumberKey != null) clauses.Add("i.number_key = " + f.Add(q.NumberKey));
            if (q.CounterpartyId != null)
            {
                // MST/CCCD là chữ số nên so sánh nhị phân (dùng được chỉ mục); người mua cá nhân chỉ có CCCD khi không có MST.
                var id = f.Add(q.CounterpartyId);
                clauses.Add(
                    q.Direction == InvoiceDirection.Purchase
                        ? "i.seller_tax_code = " + id
                        : "(i.buyer_tax_code = " + id + " OR (COALESCE(i.buyer_tax_code, '') = '' AND i.buyer_id_number = " + id + "))");
            }

            if (q.QuickSearchFolded != null) clauses.Add("i.search_text LIKE " + f.Add("%" + EscapeLike(q.QuickSearchFolded) + "%") + " ESCAPE '\\'");

            f.Where = string.Join(" AND ", clauses);
            return f;
        }

        private static string EscapeLike(string text)
        {
            return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        /// <summary>Thứ tự khớp <see cref="ArchiveCriteria.Compare"/>: khóa chính, rồi số hóa đơn, rồi mã cho ổn định giữa các trang.</summary>
        private static string OrderBy(ArchiveCriteria q)
        {
            string primary;
            switch (q.SortKey)
            {
                case ArchiveSortKey.InvoiceNumber: primary = "i.number_key"; break;
                case ArchiveSortKey.Counterparty: primary = "i.counterparty_sort"; break;
                case ArchiveSortKey.TotalPayment: primary = "COALESCE(i.total_payment, 0)"; break;
                default: primary = "i.issue_date"; break;
            }

            var direction = q.SortDescending ? " DESC" : " ASC";
            return primary + direction + ", i.number_key" + direction + ", i.id ASC";
        }

        private static StoredInvoice LoadOne(SQLiteConnection c, string where, string parameter)
        {
            StoredInvoice stored;
            using (var command = SqliteDatabase.Command(c, "SELECT " + InvoiceColumns + " FROM invoice WHERE " + where, parameter))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                stored = ReadInvoice(reader);
            }

            using (var command = SqliteDatabase.Command(c, "SELECT " + VersionColumns + " FROM invoice_version WHERE invoice_id = @p0 ORDER BY version_no", SqliteValues.Id(stored.InvoiceId)))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) stored.Versions.Add(ReadVersion(reader));
            }

            return stored;
        }

        private static void RequireInvoice(SQLiteConnection c, Guid invoiceId)
        {
            if (Convert.ToInt64(SqliteDatabase.Scalar(c, "SELECT COUNT(*) FROM invoice WHERE id = '" + SqliteValues.Id(invoiceId) + "'")) == 0)
            {
                throw new InvalidOperationException("Hóa đơn không tồn tại trong kho.");
            }
        }

        private static void InsertInvoice(SQLiteConnection c, StoredInvoice s)
        {
            var i = s.Invoice;
            SqliteDatabase.Run(
                c,
                "INSERT INTO invoice (" + InvoiceColumns + ", search_text, number_key, counterparty_sort, current_path, version_count) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18, " +
                "@p19, @p20, @p21, @p22, @p23, @p24, @p25, @p26, @p27, @p28, @p29, @p30, @p31)",
                SqliteValues.Id(s.InvoiceId), s.BusinessKey, (int)i.Direction, i.PortalInvoiceId, i.FormSymbol, i.InvoiceSymbol, i.InvoiceNumber, SqliteValues.Date(i.IssueDate),
                i.SellerTaxCode, i.SellerName, i.BuyerTaxCode, i.BuyerIdNumber, i.BuyerName, i.BuyerAddress,
                SqliteValues.Money(i.TotalBeforeTax), SqliteValues.Money(i.TotalTax), SqliteValues.Money(i.TotalDiscount), SqliteValues.Money(i.TotalFee), SqliteValues.Money(i.TotalPayment),
                i.Currency, SqliteValues.Rate(i.ExchangeRate), (int)i.Status, i.StatusRaw, (int)i.CheckResult, i.CheckResultRaw,
                SqliteValues.Time(s.FirstSeenAtUtc), SqliteValues.Time(s.LastSeenAtUtc), ArchiveKeys.SearchText(i), ArchiveKeys.NumberKey(i.InvoiceNumber), ArchiveKeys.CounterpartySort(i),
                CurrentPathOf(s), s.Versions.Count);
        }

        /// <summary>Chỉ vi phạm UNIQUE trên khóa nghiệp vụ hoặc mã hóa đơn mới là "đã tồn tại"; ràng buộc khác là lỗi dữ liệu thật và được ném nguyên.</summary>
        private static bool IsBusinessKeyConflict(SQLiteException ex)
        {
            return ex.Message.IndexOf("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) >= 0
                && (ex.Message.IndexOf("business_key", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("invoice.id", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static object CurrentPathOf(StoredInvoice s)
        {
            return s.Current == null ? null : s.Current.RelativePath;
        }

        /// <summary>Cập nhật metadata mới nhất của cổng (không đổi khóa nghiệp vụ, chiều hay thời điểm thấy lần đầu) và thời điểm thấy lần cuối.</summary>
        private static void UpdateInvoice(SQLiteConnection c, Guid invoiceId, NormalizedInvoice i, DateTime nowUtc)
        {
            SqliteDatabase.Run(
                c,
                "UPDATE invoice SET portal_invoice_id = @p1, form_symbol = @p2, invoice_symbol = @p3, invoice_number = @p4, issue_date = @p5, seller_tax_code = @p6, seller_name = @p7, " +
                "buyer_tax_code = @p8, buyer_id_number = @p9, buyer_name = @p10, buyer_address = @p11, total_before_tax = @p12, total_tax = @p13, total_discount = @p14, total_fee = @p15, " +
                "total_payment = @p16, currency = @p17, exchange_rate = @p18, status = @p19, status_raw = @p20, check_result = @p21, check_result_raw = @p22, search_text = @p23, last_seen_utc = @p24, number_key = @p25, counterparty_sort = @p26 " +
                "WHERE id = @p0",
                SqliteValues.Id(invoiceId), i.PortalInvoiceId, i.FormSymbol, i.InvoiceSymbol, i.InvoiceNumber, SqliteValues.Date(i.IssueDate), i.SellerTaxCode, i.SellerName,
                i.BuyerTaxCode, i.BuyerIdNumber, i.BuyerName, i.BuyerAddress,
                SqliteValues.Money(i.TotalBeforeTax), SqliteValues.Money(i.TotalTax), SqliteValues.Money(i.TotalDiscount), SqliteValues.Money(i.TotalFee), SqliteValues.Money(i.TotalPayment),
                i.Currency, SqliteValues.Rate(i.ExchangeRate), (int)i.Status, i.StatusRaw, (int)i.CheckResult, i.CheckResultRaw, ArchiveKeys.SearchText(i), SqliteValues.Time(nowUtc),
                ArchiveKeys.NumberKey(i.InvoiceNumber), ArchiveKeys.CounterpartySort(i));
        }

        private static void InsertVersion(SQLiteConnection c, Guid invoiceId, InvoiceVersionRecord v)
        {
            SqliteDatabase.Run(
                c,
                "INSERT INTO invoice_version (" + VersionColumns + ") VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                SqliteValues.Id(v.VersionId), SqliteValues.Id(invoiceId), v.VersionNo, v.Sha256, v.RelativePath, v.SizeBytes, SqliteValues.Time(v.CreatedAtUtc),
                v.IsCurrent ? 1 : 0, SqliteValues.Id(v.SourceSessionId));
        }

        private static StoredInvoice ReadInvoice(SQLiteDataReader r)
        {
            return new StoredInvoice
            {
                InvoiceId = SqliteValues.ParseId(r["id"]),
                BusinessKey = SqliteValues.Text(r["business_key"]),
                FirstSeenAtUtc = SqliteValues.ParseTime(r["first_seen_utc"]),
                LastSeenAtUtc = SqliteValues.ParseTime(r["last_seen_utc"]),
                Versions = new List<InvoiceVersionRecord>(),
                Invoice = new NormalizedInvoice
                {
                    Direction = (InvoiceDirection)SqliteValues.Int(r["direction"]),
                    PortalInvoiceId = SqliteValues.Text(r["portal_invoice_id"]),
                    FormSymbol = SqliteValues.Text(r["form_symbol"]),
                    InvoiceSymbol = SqliteValues.Text(r["invoice_symbol"]),
                    InvoiceNumber = SqliteValues.Text(r["invoice_number"]),
                    IssueDate = SqliteValues.ParseDate(r["issue_date"]),
                    SellerTaxCode = SqliteValues.Text(r["seller_tax_code"]),
                    SellerName = SqliteValues.Text(r["seller_name"]),
                    BuyerTaxCode = SqliteValues.Text(r["buyer_tax_code"]),
                    BuyerIdNumber = SqliteValues.Text(r["buyer_id_number"]),
                    BuyerName = SqliteValues.Text(r["buyer_name"]),
                    BuyerAddress = SqliteValues.Text(r["buyer_address"]),
                    TotalBeforeTax = SqliteValues.ParseMoney(r["total_before_tax"]),
                    TotalTax = SqliteValues.ParseMoney(r["total_tax"]),
                    TotalDiscount = SqliteValues.ParseMoney(r["total_discount"]),
                    TotalFee = SqliteValues.ParseMoney(r["total_fee"]),
                    TotalPayment = SqliteValues.ParseMoney(r["total_payment"]),
                    Currency = SqliteValues.Text(r["currency"]),
                    ExchangeRate = SqliteValues.ParseRate(r["exchange_rate"]),
                    Status = (InvoiceStatus)SqliteValues.Int(r["status"]),
                    StatusRaw = SqliteValues.Text(r["status_raw"]),
                    CheckResult = (InvoiceCheckResult)SqliteValues.Int(r["check_result"]),
                    CheckResultRaw = SqliteValues.Text(r["check_result_raw"])
                }
            };
        }

        private static InvoiceVersionRecord ReadVersion(SQLiteDataReader r)
        {
            return new InvoiceVersionRecord
            {
                VersionId = SqliteValues.ParseId(r["id"]),
                VersionNo = SqliteValues.Int(r["version_no"]),
                Sha256 = SqliteValues.Text(r["sha256"]),
                RelativePath = SqliteValues.Text(r["relative_path"]),
                SizeBytes = Convert.ToInt64(r["size_bytes"]),
                CreatedAtUtc = SqliteValues.ParseTime(r["created_utc"]),
                IsCurrent = SqliteValues.Int(r["is_current"]) == 1,
                SourceSessionId = SqliteValues.ParseId(r["source_session_id"])
            };
        }
    }
}
