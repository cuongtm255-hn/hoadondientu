using System;
using System.Collections.Generic;
using System.Data.SQLite;
using HoaDonDienTu.Application.Invoices;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>Một bước nâng cấp schema; chạy trong một giao dịch cùng việc tăng <c>user_version</c>.</summary>
    public sealed class SchemaMigration
    {
        public SchemaMigration(int version, string description, params string[] statements)
        {
            Version = version;
            Description = description;
            Statements = statements;
        }

        public int Version { get; private set; }
        public string Description { get; private set; }
        public IList<string> Statements { get; private set; }

        /// <summary>Bước điền dữ liệu cho dòng đã có sau khi chạy <see cref="Statements"/>, trong cùng giao dịch (cần mã C#, không viết được bằng SQL).</summary>
        public Action<SQLiteConnection> Backfill { get; set; }
    }

    /// <summary>
    /// Schema của <c>company.db</c> (DD-06 mục 4, ADR 0006). Quy ước cột:
    /// thời điểm là text ISO 8601 UTC (<c>yyyy-MM-ddTHH:mm:ssZ</c>), ngày lập là <c>yyyy-MM-dd</c>, mã là GUID dạng chữ thường;
    /// tiền là số nguyên nhân 10.000 (bốn chữ số thập phân, không dùng REAL), tỷ giá là text; enum lưu số nguyên.
    /// Chỉ thêm migration mới ở cuối danh sách, không sửa migration đã phát hành.
    /// </summary>
    public static class CompanyDatabaseSchema
    {
        /// <summary>Hệ số nhân của các cột tiền: giá trị lưu = số tiền × <see cref="MoneyScale"/>.</summary>
        public const long MoneyScale = 10000;

        private static readonly SchemaMigration[] All =
        {
            new SchemaMigration(
                1,
                "Hóa đơn, phiên bản XML, phiên tải, đoạn tải và sự kiện",
                @"CREATE TABLE invoice (
                    id TEXT NOT NULL PRIMARY KEY,
                    business_key TEXT NOT NULL UNIQUE,
                    direction INTEGER NOT NULL,
                    portal_invoice_id TEXT,
                    form_symbol TEXT,
                    invoice_symbol TEXT,
                    invoice_number TEXT,
                    issue_date TEXT NOT NULL,
                    seller_tax_code TEXT,
                    seller_name TEXT,
                    buyer_tax_code TEXT,
                    buyer_id_number TEXT,
                    buyer_name TEXT,
                    buyer_address TEXT,
                    total_before_tax INTEGER,
                    total_tax INTEGER,
                    total_discount INTEGER,
                    total_fee INTEGER,
                    total_payment INTEGER,
                    currency TEXT,
                    exchange_rate TEXT,
                    status INTEGER NOT NULL,
                    status_raw TEXT,
                    check_result INTEGER NOT NULL,
                    check_result_raw TEXT,
                    search_text TEXT NOT NULL,
                    first_seen_utc TEXT NOT NULL,
                    last_seen_utc TEXT NOT NULL
                )",
                "CREATE INDEX ix_invoice_direction_date ON invoice (direction, issue_date DESC)",
                "CREATE INDEX ix_invoice_seller ON invoice (seller_tax_code)",
                "CREATE INDEX ix_invoice_buyer ON invoice (buyer_tax_code)",
                @"CREATE TABLE invoice_version (
                    id TEXT NOT NULL PRIMARY KEY,
                    invoice_id TEXT NOT NULL REFERENCES invoice (id) ON DELETE CASCADE,
                    version_no INTEGER NOT NULL,
                    sha256 TEXT NOT NULL,
                    relative_path TEXT NOT NULL,
                    size_bytes INTEGER NOT NULL,
                    created_utc TEXT NOT NULL,
                    is_current INTEGER NOT NULL,
                    source_session_id TEXT NOT NULL,
                    UNIQUE (invoice_id, version_no)
                )",
                "CREATE UNIQUE INDEX ux_invoice_version_current ON invoice_version (invoice_id) WHERE is_current = 1",
                @"CREATE TABLE download_session (
                    id TEXT NOT NULL PRIMARY KEY,
                    company_id TEXT NOT NULL,
                    tax_code TEXT NOT NULL,
                    storage_root TEXT NOT NULL,
                    direction INTEGER NOT NULL,
                    period_json TEXT NOT NULL,
                    filter_json TEXT NOT NULL,
                    state INTEGER NOT NULL,
                    created_utc TEXT NOT NULL,
                    started_utc TEXT,
                    finished_utc TEXT,
                    started_by_user TEXT NOT NULL
                )",
                "CREATE INDEX ix_download_session_company ON download_session (company_id, created_utc DESC)",
                @"CREATE TABLE download_segment (
                    id TEXT NOT NULL PRIMARY KEY,
                    session_id TEXT NOT NULL REFERENCES download_session (id) ON DELETE CASCADE,
                    segment_index INTEGER NOT NULL,
                    from_date TEXT NOT NULL,
                    to_date TEXT NOT NULL,
                    state INTEGER NOT NULL,
                    cursor TEXT,
                    item_offset INTEGER NOT NULL,
                    page_index INTEGER NOT NULL,
                    received_count INTEGER NOT NULL,
                    xml_saved_count INTEGER NOT NULL,
                    error_count INTEGER NOT NULL,
                    retry_count INTEGER NOT NULL,
                    last_error TEXT,
                    check_result TEXT,
                    UNIQUE (session_id, segment_index)
                )",
                @"CREATE TABLE download_event (
                    id TEXT NOT NULL PRIMARY KEY,
                    session_id TEXT NOT NULL REFERENCES download_session (id) ON DELETE CASCADE,
                    segment_id TEXT,
                    time_utc TEXT NOT NULL,
                    level INTEGER NOT NULL,
                    code TEXT NOT NULL,
                    message TEXT NOT NULL
                )",
                "CREATE INDEX ix_download_event_session ON download_event (session_id, time_utc)"),
            new SchemaMigration(
                2,
                "Khóa sắp xếp và lọc của Kho hóa đơn: số hóa đơn chuẩn hóa, tên đối tác bỏ dấu; file hiện hành và số phiên bản ngay trên hóa đơn",
                "ALTER TABLE invoice ADD COLUMN number_key TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE invoice ADD COLUMN counterparty_sort TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE invoice ADD COLUMN current_path TEXT",
                "ALTER TABLE invoice ADD COLUMN version_count INTEGER NOT NULL DEFAULT 0",
                "UPDATE invoice SET current_path = (SELECT v.relative_path FROM invoice_version v WHERE v.invoice_id = invoice.id AND v.is_current = 1), " +
                "version_count = (SELECT COUNT(*) FROM invoice_version v WHERE v.invoice_id = invoice.id)",
                "CREATE INDEX ix_invoice_direction_date_number ON invoice (direction, issue_date, number_key)",
                "CREATE INDEX ix_invoice_direction_number ON invoice (direction, number_key)")
            {
                Backfill = BackfillInvoiceKeys
            }
        };

        /// <summary>Điền <c>number_key</c> và <c>counterparty_sort</c> cho hóa đơn lưu trước migration 2, bằng đúng hàm của lúc ghi mới.</summary>
        private static void BackfillInvoiceKeys(SQLiteConnection connection)
        {
            var rows = new List<object[]>();
            using (var command = SqliteDatabase.Command(connection, "SELECT id, direction, invoice_number, seller_name, buyer_name FROM invoice"))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var purchase = SqliteValues.Int(reader["direction"]) == (int)Domain.Invoices.InvoiceDirection.Purchase;
                    rows.Add(new object[]
                    {
                        reader["id"],
                        ArchiveKeys.NumberKey(SqliteValues.Text(reader["invoice_number"])),
                        ArchiveKeys.Fold(SqliteValues.Text(reader[purchase ? "seller_name" : "buyer_name"]))
                    });
                }
            }

            using (var update = SqliteDatabase.Command(connection, "UPDATE invoice SET number_key = @p1, counterparty_sort = @p2 WHERE id = @p0", null, null, null))
            {
                foreach (var row in rows)
                {
                    update.Parameters["@p0"].Value = row[0];
                    update.Parameters["@p1"].Value = row[1];
                    update.Parameters["@p2"].Value = row[2];
                    update.ExecuteNonQuery();
                }
            }
        }

        public static IList<SchemaMigration> Migrations
        {
            get { return All; }
        }

        public static int LatestVersion
        {
            get { return All[All.Length - 1].Version; }
        }
    }
}
