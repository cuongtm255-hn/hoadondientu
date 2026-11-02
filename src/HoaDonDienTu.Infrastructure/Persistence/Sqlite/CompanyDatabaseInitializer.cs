using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>Cơ sở dữ liệu hỏng hoặc có schema mới hơn ứng dụng; thông báo an toàn để hiển thị, không chứa đường dẫn hay dữ liệu.</summary>
    public sealed class CompanyDatabaseException : Exception
    {
        public CompanyDatabaseException(string message)
            : base(message)
        {
        }

        public CompanyDatabaseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public static class SchemaVersionReader
    {
        /// <summary>Phiên bản schema đang lưu (<c>PRAGMA user_version</c>); 0 là file mới chưa có bảng nào.</summary>
        public static int Read(SQLiteConnection connection)
        {
            return Convert.ToInt32(SqliteDatabase.Scalar(connection, "PRAGMA user_version"), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Tạo hoặc nâng cấp <c>company.db</c> (DD-06 mục 4, ADR 0006): mỗi migration chạy trong một giao dịch cùng việc tăng
    /// <c>user_version</c> nên bị ngắt giữa chừng thì quay về phiên bản trước. Schema mới hơn ứng dụng bị từ chối, không đụng tới file.
    /// </summary>
    public sealed class CompanyDatabaseInitializer
    {
        private readonly IList<SchemaMigration> _migrations;

        public CompanyDatabaseInitializer()
            : this(CompanyDatabaseSchema.Migrations)
        {
        }

        public CompanyDatabaseInitializer(IList<SchemaMigration> migrations)
        {
            if (migrations == null || migrations.Count == 0) throw new ArgumentException("Cần ít nhất một migration.", "migrations");
            for (var i = 0; i < migrations.Count; i++)
            {
                if (migrations[i].Version != i + 1) throw new ArgumentException("Migration phải đánh số liên tục từ 1.", "migrations");
            }

            _migrations = migrations;
        }

        public int LatestVersion
        {
            get { return _migrations[_migrations.Count - 1].Version; }
        }

        /// <summary>
        /// Tạo thư mục và file nếu chưa có, nâng cấp tới phiên bản mới nhất rồi kiểm tra toàn vẹn.
        /// <exception cref="CompanyDatabaseException">Schema mới hơn ứng dụng, file hỏng hoặc không mở được.</exception>
        /// </summary>
        public SqliteDatabase Initialize(string path)
        {
            var database = new SqliteDatabase(path);
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                using (var connection = database.Open())
                {
                    var current = SchemaVersionReader.Read(connection);
                    if (current > LatestVersion)
                    {
                        throw new CompanyDatabaseException("Cơ sở dữ liệu được tạo bởi phiên bản ứng dụng mới hơn. Hãy cập nhật ứng dụng.");
                    }

                    for (var i = current; i < _migrations.Count; i++) Apply(connection, _migrations[i]);

                    EnsureIntact(connection);
                }
            }
            catch (SQLiteException ex)
            {
                throw new CompanyDatabaseException("Không mở hoặc nâng cấp được cơ sở dữ liệu của doanh nghiệp.", ex);
            }

            return database;
        }

        /// <summary>Chạy <c>PRAGMA integrity_check</c> trên một file; dùng sau khôi phục hoặc khi mở kho. Trả false nếu có lỗi.</summary>
        public static bool IsIntact(SqliteDatabase database)
        {
            try
            {
                using (var connection = database.Open())
                {
                    return IntegrityOk(connection);
                }
            }
            catch (SQLiteException)
            {
                return false;
            }
        }

        private static void Apply(SQLiteConnection connection, SchemaMigration migration)
        {
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var statement in migration.Statements) SqliteDatabase.Execute(connection, statement);
                SqliteDatabase.Execute(connection, "PRAGMA user_version=" + migration.Version.ToString(CultureInfo.InvariantCulture));
                transaction.Commit();
            }
        }

        private static void EnsureIntact(SQLiteConnection connection)
        {
            if (!IntegrityOk(connection)) throw new CompanyDatabaseException("Cơ sở dữ liệu của doanh nghiệp bị hỏng.");
        }

        private static bool IntegrityOk(SQLiteConnection connection)
        {
            return string.Equals(Convert.ToString(SqliteDatabase.Scalar(connection, "PRAGMA integrity_check"), CultureInfo.InvariantCulture), "ok", StringComparison.OrdinalIgnoreCase);
        }
    }
}
