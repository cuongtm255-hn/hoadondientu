using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>
    /// Chụp và thay <c>company.db</c> của doanh nghiệp đang mở cho sao lưu/khôi phục (ADR 0006 mục 7, DD-11). Bản chụp dùng backup API của SQLite
    /// nên nhất quán dù đang có người ghi; khôi phục chuẩn bị file thay thế (nâng schema, kiểm toàn vẹn, gắn lại mã doanh nghiệp) bên cạnh file thật
    /// rồi hoán đổi một bước trong lúc giữ khóa ghi, nên ngắt giữa chừng không để lại cơ sở dữ liệu dở.
    /// </summary>
    public sealed class SqliteCompanyDatabaseBackup : ICompanyDatabaseBackup
    {
        private const int SwapAttempts = 10;
        private const int SwapRetryMs = 200;

        private readonly CompanyDatabaseProvider _provider;
        private readonly ICompanyContextAccessor _context;
        private readonly CompanyDatabaseInitializer _initializer;

        public SqliteCompanyDatabaseBackup(CompanyDatabaseProvider provider, ICompanyContextAccessor context)
            : this(provider, context, new CompanyDatabaseInitializer())
        {
        }

        public SqliteCompanyDatabaseBackup(CompanyDatabaseProvider provider, ICompanyContextAccessor context, CompanyDatabaseInitializer initializer)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            if (context == null) throw new ArgumentNullException("context");
            if (initializer == null) throw new ArgumentNullException("initializer");
            _provider = provider;
            _context = context;
            _initializer = initializer;
        }

        public int LatestSchemaVersion
        {
            get { return _initializer.LatestVersion; }
        }

        public Task<int> SnapshotAsync(string destinationFile, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                source =>
                {
                    DeleteWithSidecars(destinationFile);
                    var builder = new SQLiteConnectionStringBuilder { DataSource = destinationFile, Version = 3, Pooling = false };
                    using (var target = new SQLiteConnection(builder.ConnectionString))
                    {
                        target.Open();
                        source.BackupDatabase(target, "main", "main", -1, null, 0);
                    }

                    if (!CompanyDatabaseInitializer.IsIntact(new SqliteDatabase(destinationFile)))
                    {
                        DeleteWithSidecars(destinationFile);
                        throw new BackupException("Bản chụp cơ sở dữ liệu không qua được kiểm tra toàn vẹn; không tạo gói sao lưu.");
                    }

                    return SchemaVersionReader.Read(source);
                },
                cancellationToken);
        }

        public Task ValidateAsync(string sourceFile, CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    var probe = sourceFile + ".check";
                    try
                    {
                        File.Copy(sourceFile, probe, true);
                        Prepare(probe);
                    }
                    finally
                    {
                        DeleteWithSidecars(probe);
                    }
                },
                cancellationToken);
        }

        public Task ReplaceAsync(string sourceFile, CancellationToken cancellationToken)
        {
            var database = _provider.Current();
            var company = _context.Current;
            return Task.Run(
                () =>
                {
                    var stage = database.Path + ".restore.tmp";
                    try
                    {
                        DeleteWithSidecars(stage);
                        File.Copy(sourceFile, stage, true);
                        var staged = Prepare(stage);

                        // Mã doanh nghiệp là mã cục bộ của từng máy: gắn lại để lịch sử tải của gói hiện ra ở doanh nghiệp đang mở.
                        staged.WriteAsync(
                            c => SqliteDatabase.Run(
                                c, "UPDATE download_session SET company_id = @p0, storage_root = @p1", SqliteValues.Id(company.CompanyId.Value), company.StorageRoot),
                            CancellationToken.None).GetAwaiter().GetResult();
                        using (var connection = staged.Open()) SqliteDatabase.Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");

                        cancellationToken.ThrowIfCancellationRequested();
                        database.ExclusiveAsync(() => Swap(stage, database.Path), CancellationToken.None).GetAwaiter().GetResult();
                    }
                    finally
                    {
                        DeleteWithSidecars(stage);
                    }
                },
                cancellationToken);
        }

        /// <summary>Nâng schema và kiểm toàn vẹn một file đã sao chép; lỗi quy về <see cref="BackupException"/> với thông báo an toàn.</summary>
        private SqliteDatabase Prepare(string file)
        {
            try
            {
                return _initializer.Initialize(file);
            }
            catch (CompanyDatabaseException ex)
            {
                throw new BackupException("Cơ sở dữ liệu trong gói không dùng được: " + ex.Message, ex);
            }
        }

        private static void Swap(string stage, string target)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    // Tệp -wal/-shm cũ thuộc về cơ sở dữ liệu cũ; để lại sẽ làm hỏng cơ sở dữ liệu mới.
                    DeleteIfExists(target + "-wal");
                    DeleteIfExists(target + "-shm");
                    File.Replace(stage, target, null);
                    return;
                }
                catch (IOException) when (attempt < SwapAttempts)
                {
                    // Một lượt đọc đang giữ file; thử lại ngay sau đó.
                    Thread.Sleep(SwapRetryMs);
                }
            }
        }

        private static void DeleteWithSidecars(string path)
        {
            DeleteIfExists(path);
            DeleteIfExists(path + "-wal");
            DeleteIfExists(path + "-shm");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
