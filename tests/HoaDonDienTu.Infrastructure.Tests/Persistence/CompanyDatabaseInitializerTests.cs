using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>Tạo/nâng cấp <c>company.db</c> trên file tạm (ADR 0006); không liên quan cổng Thuế.</summary>
    [TestFixture]
    public class CompanyDatabaseInitializerTests
    {
        private string _folder;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "hddt-sqlite-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_folder, "Kho Công ty", "Data", "company.db");
        }

        [TearDown]
        public void TearDown()
        {
            SQLiteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Initialize_NewFile_CreatesFolderAndLatestSchema()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);

            Assert.That(File.Exists(_path), Is.True);
            using (var c = database.Open())
            {
                Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(CompanyDatabaseSchema.LatestVersion));
                foreach (var table in new[] { "invoice", "invoice_version", "download_session", "download_segment", "download_event" })
                {
                    Assert.That(SqliteDatabase.Scalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='" + table + "'"), Is.EqualTo(1L), table);
                }
            }
        }

        [Test]
        public void Open_UsesWalAndForeignKeys()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);

            using (var c = database.Open())
            {
                Assert.That(SqliteDatabase.Scalar(c, "PRAGMA journal_mode").ToString(), Is.EqualTo("wal").IgnoreCase);
                Assert.That(SqliteDatabase.Scalar(c, "PRAGMA foreign_keys"), Is.EqualTo(1L));
                Assert.That(SqliteDatabase.Scalar(c, "PRAGMA busy_timeout"), Is.EqualTo(5000L));
            }
        }

        [Test]
        public void Initialize_Twice_KeepsDataAndVersion()
        {
            var initializer = new CompanyDatabaseInitializer();
            var database = initializer.Initialize(_path);
            using (var c = database.Open()) InsertInvoice(c, "id-1", "P|1|1|A|1");

            database = initializer.Initialize(_path);

            using (var c = database.Open())
            {
                Assert.That(SqliteDatabase.Scalar(c, "SELECT COUNT(*) FROM invoice"), Is.EqualTo(1L));
                Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(CompanyDatabaseSchema.LatestVersion));
            }
        }

        [Test]
        public void Initialize_NewerSchemaThanApplication_IsRejectedAndLeftUntouched()
        {
            var initializer = new CompanyDatabaseInitializer();
            var database = initializer.Initialize(_path);
            using (var c = database.Open()) SqliteDatabase.Execute(c, "PRAGMA user_version=" + (CompanyDatabaseSchema.LatestVersion + 5));

            var ex = Assert.Throws<CompanyDatabaseException>(() => initializer.Initialize(_path));

            Assert.That(ex.Message, Does.Contain("mới hơn"));
            using (var c = database.Open()) Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(CompanyDatabaseSchema.LatestVersion + 5));
        }

        [Test]
        public void Initialize_OlderSchema_AppliesOnlyMissingMigrations()
        {
            var first = new SchemaMigration(1, "v1", "CREATE TABLE a (id INTEGER PRIMARY KEY)");
            var second = new SchemaMigration(2, "v2", "ALTER TABLE a ADD COLUMN note TEXT", "INSERT INTO a (id, note) VALUES (1, 'x')");
            new CompanyDatabaseInitializer(new List<SchemaMigration> { first }).Initialize(_path);

            var database = new CompanyDatabaseInitializer(new List<SchemaMigration> { first, second }).Initialize(_path);

            using (var c = database.Open())
            {
                Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(2));
                Assert.That(SqliteDatabase.Scalar(c, "SELECT note FROM a WHERE id=1"), Is.EqualTo("x"));
            }
        }

        [Test]
        public void Initialize_Upgrade_KeepsACopyOfTheDatabaseAsItWasBeforeUpgrading()
        {
            var first = new SchemaMigration(1, "v1", "CREATE TABLE a (id INTEGER PRIMARY KEY)");
            var second = new SchemaMigration(2, "v2", "ALTER TABLE a ADD COLUMN note TEXT");
            var before = new CompanyDatabaseInitializer(new List<SchemaMigration> { first }).Initialize(_path);
            using (var c = before.Open()) SqliteDatabase.Execute(c, "INSERT INTO a (id) VALUES (7)");

            new CompanyDatabaseInitializer(new List<SchemaMigration> { first, second }).Initialize(_path);

            var copy = CompanyDatabaseInitializer.UpgradeCopyPath(_path, 1);
            Assert.That(File.Exists(copy), Is.True);
            using (var c = new SqliteDatabase(copy).Open())
            {
                Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(1), "bản sao giữ nguyên phiên bản cũ");
                Assert.That(Convert.ToInt64(SqliteDatabase.Scalar(c, "SELECT COUNT(*) FROM a")), Is.EqualTo(1));
            }

            Assert.That(File.Exists(copy + ".tmp"), Is.False);
        }

        [Test]
        public void Initialize_NewFileOrUpToDate_LeavesNoUpgradeCopy()
        {
            var first = new SchemaMigration(1, "v1", "CREATE TABLE a (id INTEGER PRIMARY KEY)");
            var initializer = new CompanyDatabaseInitializer(new List<SchemaMigration> { first });
            initializer.Initialize(_path);
            initializer.Initialize(_path);

            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path), "*.bak"), Is.Empty);
        }

        [Test]
        public void Initialize_FailingMigration_RollsBackToPreviousVersion()
        {
            var first = new SchemaMigration(1, "v1", "CREATE TABLE a (id INTEGER PRIMARY KEY)");
            var broken = new SchemaMigration(2, "v2", "ALTER TABLE a ADD COLUMN note TEXT", "THIS IS NOT SQL");
            new CompanyDatabaseInitializer(new List<SchemaMigration> { first }).Initialize(_path);

            Assert.Throws<CompanyDatabaseException>(() => new CompanyDatabaseInitializer(new List<SchemaMigration> { first, broken }).Initialize(_path));

            using (var c = new SqliteDatabase(_path).Open())
            {
                Assert.That(SchemaVersionReader.Read(c), Is.EqualTo(1));
                Assert.That(SqliteDatabase.Scalar(c, "SELECT COUNT(*) FROM pragma_table_info('a') WHERE name='note'"), Is.EqualTo(0L), "cột đã thêm phải được hoàn tác");
            }
        }

        [Test]
        public void Initialize_MigrationsNotNumberedFromOne_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new CompanyDatabaseInitializer(new List<SchemaMigration> { new SchemaMigration(2, "x", "SELECT 1") }));
            Assert.Throws<ArgumentException>(() => new CompanyDatabaseInitializer(new List<SchemaMigration>()));
        }

        [Test]
        public void Initialize_FileThatIsNotADatabase_ThrowsSafeMessage()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "đây không phải SQLite, chỉ là văn bản dài hơn một trăm byte. " + new string('x', 200));

            var ex = Assert.Throws<CompanyDatabaseException>(() => new CompanyDatabaseInitializer().Initialize(_path));

            Assert.That(ex.Message, Does.Not.Contain(_folder), "thông báo không lộ đường dẫn");
        }

        [Test]
        public void IsIntact_HealthyDatabase_IsTrue_AndGarbageIsFalse()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);
            Assert.That(CompanyDatabaseInitializer.IsIntact(database), Is.True);

            var garbage = new SqliteDatabase(Path.Combine(_folder, "rac.db"));
            File.WriteAllText(garbage.Path, new string('y', 500));
            Assert.That(CompanyDatabaseInitializer.IsIntact(garbage), Is.False);
        }

        [Test]
        public void Schema_BusinessKeyIsUnique()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);
            using (var c = database.Open())
            {
                InsertInvoice(c, "id-1", "P|0101234567|1|C26TAA|1");
                Assert.Throws<SQLiteException>(() => InsertInvoice(c, "id-2", "P|0101234567|1|C26TAA|1"));
            }
        }

        [Test]
        public void Schema_OnlyOneCurrentVersionPerInvoice()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);
            using (var c = database.Open())
            {
                InsertInvoice(c, "id-1", "k1");
                InsertVersion(c, "v1", "id-1", 1, true);
                InsertVersion(c, "v2", "id-1", 2, false);

                Assert.Throws<SQLiteException>(() => InsertVersion(c, "v3", "id-1", 3, true));
                Assert.Throws<SQLiteException>(() => InsertVersion(c, "v4", "id-1", 2, false), "trùng số phiên bản");
            }
        }

        [Test]
        public void Schema_DeletingInvoiceCascadesToVersions_AndOrphanVersionsAreRejected()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);
            using (var c = database.Open())
            {
                Assert.Throws<SQLiteException>(() => InsertVersion(c, "v0", "khong-co", 1, true), "foreign key bật");

                InsertInvoice(c, "id-1", "k1");
                InsertVersion(c, "v1", "id-1", 1, true);
                SqliteDatabase.Execute(c, "DELETE FROM invoice WHERE id='id-1'");

                Assert.That(SqliteDatabase.Scalar(c, "SELECT COUNT(*) FROM invoice_version"), Is.EqualTo(0L));
            }
        }

        [Test]
        public void Schema_StoresVietnameseTextAndExactMoney()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);
            using (var c = database.Open())
            {
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = InsertInvoiceSql + ";UPDATE invoice SET seller_name=@n, total_payment=@p WHERE id='id-1'";
                    AddInvoiceParams(cmd, "id-1", "k1");
                    cmd.Parameters.AddWithValue("@n", "CÔNG TY TNHH ĐÔNG Á");
                    cmd.Parameters.AddWithValue("@p", 1234567890123L * CompanyDatabaseSchema.MoneyScale + 5000);
                    cmd.ExecuteNonQuery();
                }

                Assert.That(SqliteDatabase.Scalar(c, "SELECT seller_name FROM invoice WHERE id='id-1'"), Is.EqualTo("CÔNG TY TNHH ĐÔNG Á"));
                Assert.That(SqliteDatabase.Scalar(c, "SELECT total_payment FROM invoice WHERE id='id-1'"), Is.EqualTo(12345678901235000L));
            }
        }

        [Test]
        public void Schema_SessionDeleteCascadesToSegmentsAndEvents()
        {
            var database = new CompanyDatabaseInitializer().Initialize(_path);
            using (var c = database.Open())
            {
                SqliteDatabase.Execute(c, "INSERT INTO download_session VALUES ('s1','c1','0101234567','D:\\Kho',1,'{}','{}',1,'2026-01-01T00:00:00Z',NULL,NULL,'u')");
                SqliteDatabase.Execute(c, "INSERT INTO download_segment VALUES ('g1','s1',0,'2026-01-01','2026-01-31',0,NULL,0,0,0,0,0,0,NULL,'5')");
                SqliteDatabase.Execute(c, "INSERT INTO download_event VALUES ('e1','s1','g1','2026-01-01T00:00:00Z',0,'X','m')");

                SqliteDatabase.Execute(c, "DELETE FROM download_session WHERE id='s1'");

                Assert.That(SqliteDatabase.Scalar(c, "SELECT (SELECT COUNT(*) FROM download_segment)+(SELECT COUNT(*) FROM download_event)"), Is.EqualTo(0L));
            }
        }

        private const string InsertInvoiceSql =
            "INSERT INTO invoice (id, business_key, direction, issue_date, status, check_result, search_text, first_seen_utc, last_seen_utc) " +
            "VALUES (@id, @key, 1, '2026-01-15', 1, 0, '', '2026-01-15T00:00:00Z', '2026-01-15T00:00:00Z')";

        private static void AddInvoiceParams(SQLiteCommand cmd, string id, string key)
        {
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@key", key);
        }

        private static void InsertInvoice(SQLiteConnection c, string id, string key)
        {
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = InsertInvoiceSql;
                AddInvoiceParams(cmd, id, key);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertVersion(SQLiteConnection c, string id, string invoiceId, int no, bool current)
        {
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO invoice_version VALUES (@id, @inv, @no, 'abc', 'a.xml', 1, '2026-01-15T00:00:00Z', @cur, 's')";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@inv", invoiceId);
                cmd.Parameters.AddWithValue("@no", no);
                cmd.Parameters.AddWithValue("@cur", current ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
