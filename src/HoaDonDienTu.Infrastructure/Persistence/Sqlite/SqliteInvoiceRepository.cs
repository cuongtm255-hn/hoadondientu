using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
                    catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
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
                "INSERT INTO invoice (" + InvoiceColumns + ", search_text) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18, " +
                "@p19, @p20, @p21, @p22, @p23, @p24, @p25, @p26, @p27)",
                SqliteValues.Id(s.InvoiceId), s.BusinessKey, (int)i.Direction, i.PortalInvoiceId, i.FormSymbol, i.InvoiceSymbol, i.InvoiceNumber, SqliteValues.Date(i.IssueDate),
                i.SellerTaxCode, i.SellerName, i.BuyerTaxCode, i.BuyerIdNumber, i.BuyerName, i.BuyerAddress,
                SqliteValues.Money(i.TotalBeforeTax), SqliteValues.Money(i.TotalTax), SqliteValues.Money(i.TotalDiscount), SqliteValues.Money(i.TotalFee), SqliteValues.Money(i.TotalPayment),
                i.Currency, SqliteValues.Rate(i.ExchangeRate), (int)i.Status, i.StatusRaw, (int)i.CheckResult, i.CheckResultRaw,
                SqliteValues.Time(s.FirstSeenAtUtc), SqliteValues.Time(s.LastSeenAtUtc), SearchText(i));
        }

        /// <summary>Cập nhật metadata mới nhất của cổng (không đổi khóa nghiệp vụ, chiều hay thời điểm thấy lần đầu) và thời điểm thấy lần cuối.</summary>
        private static void UpdateInvoice(SQLiteConnection c, Guid invoiceId, NormalizedInvoice i, DateTime nowUtc)
        {
            SqliteDatabase.Run(
                c,
                "UPDATE invoice SET portal_invoice_id = @p1, form_symbol = @p2, invoice_symbol = @p3, invoice_number = @p4, issue_date = @p5, seller_tax_code = @p6, seller_name = @p7, " +
                "buyer_tax_code = @p8, buyer_id_number = @p9, buyer_name = @p10, buyer_address = @p11, total_before_tax = @p12, total_tax = @p13, total_discount = @p14, total_fee = @p15, " +
                "total_payment = @p16, currency = @p17, exchange_rate = @p18, status = @p19, status_raw = @p20, check_result = @p21, check_result_raw = @p22, search_text = @p23, last_seen_utc = @p24 " +
                "WHERE id = @p0",
                SqliteValues.Id(invoiceId), i.PortalInvoiceId, i.FormSymbol, i.InvoiceSymbol, i.InvoiceNumber, SqliteValues.Date(i.IssueDate), i.SellerTaxCode, i.SellerName,
                i.BuyerTaxCode, i.BuyerIdNumber, i.BuyerName, i.BuyerAddress,
                SqliteValues.Money(i.TotalBeforeTax), SqliteValues.Money(i.TotalTax), SqliteValues.Money(i.TotalDiscount), SqliteValues.Money(i.TotalFee), SqliteValues.Money(i.TotalPayment),
                i.Currency, SqliteValues.Rate(i.ExchangeRate), (int)i.Status, i.StatusRaw, (int)i.CheckResult, i.CheckResultRaw, SearchText(i), SqliteValues.Time(nowUtc));
        }

        private static void InsertVersion(SQLiteConnection c, Guid invoiceId, InvoiceVersionRecord v)
        {
            SqliteDatabase.Run(
                c,
                "INSERT INTO invoice_version (" + VersionColumns + ") VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                SqliteValues.Id(v.VersionId), SqliteValues.Id(invoiceId), v.VersionNo, v.Sha256, v.RelativePath, v.SizeBytes, SqliteValues.Time(v.CreatedAtUtc),
                v.IsCurrent ? 1 : 0, SqliteValues.Id(v.SourceSessionId));
        }

        /// <summary>Cột tìm nhanh: chữ thường, bỏ dấu (SQLite không hạ/so khớp chữ có dấu, ADR 0006 mục 5).</summary>
        private static string SearchText(NormalizedInvoice i)
        {
            var name = i.Direction == InvoiceDirection.Purchase ? i.SellerName : i.BuyerName;
            return InvoiceArchiveService.Fold(string.Join(" ", i.InvoiceNumber, i.InvoiceSymbol, i.CounterpartyIdentifier, name));
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
