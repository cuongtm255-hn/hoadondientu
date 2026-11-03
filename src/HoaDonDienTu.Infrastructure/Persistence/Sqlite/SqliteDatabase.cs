using System;
using System.Data.SQLite;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>
    /// Mở kết nối tới một file SQLite theo quy ước của ADR 0006: WAL, synchronous=NORMAL, foreign_keys=ON, busy_timeout ≈ 5 giây.
    /// Mỗi lần gọi <see cref="Open"/> trả kết nối mới đã mở; người gọi sở hữu và phải Dispose. Một đối tượng dùng chung cho mọi repository
    /// của cùng một file để <see cref="WriteAsync"/> ghi tuần tự (một người ghi tại một thời điểm), còn đọc chạy song song nhờ WAL.
    /// </summary>
    public sealed class SqliteDatabase
    {
        private const int BusyTimeoutMs = 5000;

        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);

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

        /// <summary>Chạy một lần đọc trên kết nối riêng, ngoài luồng gọi (không chặn giao diện).</summary>
        public Task<T> ReadAsync<T>(Func<SQLiteConnection, T> work, CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    using (var connection = Open())
                    {
                        return work(connection);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// Chạy <paramref name="work"/> trong một giao dịch, tuần tự với mọi lần ghi khác trên file này. Thành công là đã commit
        /// (checkpoint của engine tải dựa vào đó); lỗi thì hoàn tác toàn bộ.
        /// </summary>
        public Task WriteAsync(Action<SQLiteConnection> work, CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    _writeGate.Wait(cancellationToken);
                    try
                    {
                        using (var connection = Open())
                        using (var transaction = connection.BeginTransaction())
                        {
                            work(connection);
                            transaction.Commit();
                        }
                    }
                    finally
                    {
                        _writeGate.Release();
                    }
                },
                cancellationToken);
        }

        /// <summary>Chạy <paramref name="work"/> khi giữ khóa ghi nhưng không mở kết nối, cho việc đổi chính file (khôi phục) mà không có lượt ghi nào xen vào.</summary>
        public Task ExclusiveAsync(Action work, CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    _writeGate.Wait(cancellationToken);
                    try
                    {
                        work();
                    }
                    finally
                    {
                        _writeGate.Release();
                    }
                },
                cancellationToken);
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

        /// <summary>Tạo lệnh với tham số <c>@p0, @p1…</c> theo thứ tự <paramref name="values"/>; null được lưu là NULL.</summary>
        public static SQLiteCommand Command(SQLiteConnection connection, string sql, params object[] values)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            for (var i = 0; i < values.Length; i++)
            {
                command.Parameters.AddWithValue("@p" + i.ToString(CultureInfo.InvariantCulture), values[i] ?? DBNull.Value);
            }

            return command;
        }

        public static int Run(SQLiteConnection connection, string sql, params object[] values)
        {
            using (var command = Command(connection, sql, values))
            {
                return command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Chuyển đổi giá trị theo quy ước cột của <see cref="CompanyDatabaseSchema"/>.</summary>
    public static class SqliteValues
    {
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>Giá trị thời gian của ứng dụng luôn là UTC; kiểu Unspecified được hiểu là UTC chứ không bị đổi như giờ máy.</summary>
        public static string Time(DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Unspecified) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return utc.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);
        }

        public static object Time(DateTime? utc)
        {
            return utc.HasValue ? (object)Time(utc.Value) : null;
        }

        public static DateTime ParseTime(object value)
        {
            return DateTime.ParseExact(Convert.ToString(value, CultureInfo.InvariantCulture), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        public static DateTime? ParseTimeOrNull(object value)
        {
            return value == null || value is DBNull ? (DateTime?)null : ParseTime(value);
        }

        public static string Date(DateTime date)
        {
            return date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        public static DateTime ParseDate(object value)
        {
            return DateTime.ParseExact(Convert.ToString(value, CultureInfo.InvariantCulture), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        /// <summary>Tiền lưu số nguyên × <see cref="CompanyDatabaseSchema.MoneyScale"/>; làm tròn tới bốn chữ số thập phân.</summary>
        public static object Money(decimal? amount)
        {
            return amount.HasValue ? (object)(long)Math.Round(amount.Value * CompanyDatabaseSchema.MoneyScale, MidpointRounding.AwayFromZero) : null;
        }

        public static decimal? ParseMoney(object value)
        {
            return value == null || value is DBNull ? (decimal?)null : Convert.ToInt64(value, CultureInfo.InvariantCulture) / (decimal)CompanyDatabaseSchema.MoneyScale;
        }

        public static object Rate(decimal? rate)
        {
            return rate.HasValue ? rate.Value.ToString(CultureInfo.InvariantCulture) : null;
        }

        public static decimal? ParseRate(object value)
        {
            return value == null || value is DBNull ? (decimal?)null : decimal.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        public static string Text(object value)
        {
            return value == null || value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int Int(object value)
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static string Id(Guid id)
        {
            return id.ToString("D");
        }

        public static Guid ParseId(object value)
        {
            return Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }
}
