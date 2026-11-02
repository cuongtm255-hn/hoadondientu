using System;
using System.Data.SQLite;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>
    /// Mở kết nối tới một file SQLite theo quy ước của ADR 0006: WAL, synchronous=NORMAL, foreign_keys=ON, busy_timeout ≈ 5 giây.
    /// Mỗi lần gọi trả kết nối mới đã mở; người gọi sở hữu và phải Dispose. Việc ghi tuần tự do lớp repository bảo đảm.
    /// </summary>
    public sealed class SqliteDatabase
    {
        private const int BusyTimeoutMs = 5000;

        public SqliteDatabase(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Thiếu đường dẫn cơ sở dữ liệu.", "path");
            Path = path;
        }

        public string Path { get; private set; }

        public SQLiteConnection Open()
        {
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = Path,
                Version = 3,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal,
                ForeignKeys = true,
                DefaultTimeout = BusyTimeoutMs / 1000,
                Pooling = false
            };
            var connection = new SQLiteConnection(builder.ConnectionString);
            try
            {
                connection.Open();
                Execute(connection, "PRAGMA busy_timeout=" + BusyTimeoutMs);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public static object Scalar(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return command.ExecuteScalar();
            }
        }
    }
}
