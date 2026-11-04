using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Exports;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary><see cref="IExportHistoryRepository"/> trên <c>company.db</c> của doanh nghiệp đang mở (bảng <c>export_record</c>, migration 4). Không đổi giá trị của <see cref="ExportKind"/>.</summary>
    public sealed class SqliteExportHistoryRepository : IExportHistoryRepository
    {
        private const string Columns = "id, kind, target_path, scope_text, exported_count, skipped_count, skipped_summary, created_utc";

        private readonly CompanyDatabaseProvider _provider;

        public SqliteExportHistoryRepository(CompanyDatabaseProvider provider)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            _provider = provider;
        }

        public Task AddAsync(ExportRecord record, CancellationToken cancellationToken)
        {
            if (record == null) throw new ArgumentNullException("record");
            return _provider.Current().WriteAsync(
                c => SqliteDatabase.Run(
                    c,
                    "INSERT INTO export_record (" + Columns + ") VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                    SqliteValues.Id(record.Id), (int)record.Kind, record.TargetPath ?? string.Empty, record.ScopeText ?? string.Empty,
                    record.ExportedCount, record.SkippedCount, record.SkippedSummary ?? string.Empty, SqliteValues.Time(record.CreatedAtUtc)),
                cancellationToken);
        }

        public Task<IList<ExportRecord>> ListRecentAsync(int max, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<ExportRecord> result = new List<ExportRecord>();
                    using (var command = SqliteDatabase.Command(c, "SELECT " + Columns + " FROM export_record ORDER BY created_utc DESC, rowid DESC LIMIT @p0", Math.Max(0, max)))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(
                                new ExportRecord
                                {
                                    Id = SqliteValues.ParseId(reader["id"]),
                                    Kind = (ExportKind)SqliteValues.Int(reader["kind"]),
                                    TargetPath = SqliteValues.Text(reader["target_path"]),
                                    ScopeText = SqliteValues.Text(reader["scope_text"]),
                                    ExportedCount = SqliteValues.Int(reader["exported_count"]),
                                    SkippedCount = SqliteValues.Int(reader["skipped_count"]),
                                    SkippedSummary = SqliteValues.Text(reader["skipped_summary"]),
                                    CreatedAtUtc = SqliteValues.ParseTime(reader["created_utc"])
                                });
                        }
                    }

                    return result;
                },
                cancellationToken);
        }
    }
}
