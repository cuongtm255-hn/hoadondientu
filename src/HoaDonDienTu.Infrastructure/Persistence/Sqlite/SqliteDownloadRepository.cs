using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>
    /// <see cref="IDownloadRepository"/> trên <c>company.db</c> của doanh nghiệp đang mở (DD-05, DD-06 mục 4). Mỗi lần lưu là một giao dịch đã commit
    /// trước khi trả về vì checkpoint của engine tải dựa vào đó (FR-JOB-006). Trạng thái lưu bằng số của enum nên không được đổi thứ tự/giá trị enum.
    /// </summary>
    public sealed class SqliteDownloadRepository : IDownloadRepository
    {
        private const string SessionColumns =
            "id, company_id, tax_code, storage_root, direction, period_json, filter_json, state, created_utc, started_utc, finished_utc, started_by_user";

        private const string SegmentColumns =
            "id, session_id, segment_index, from_date, to_date, state, cursor, item_offset, page_index, received_count, xml_saved_count, error_count, retry_count, last_error, check_result";

        private const string EventColumns = "id, session_id, segment_id, time_utc, level, code, message";

        private const string UnfinishedStates = "(1, 4, 5, 0)";

        private readonly CompanyDatabaseProvider _provider;

        public SqliteDownloadRepository(CompanyDatabaseProvider provider)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            _provider = provider;
        }

        public Task CreateAsync(DownloadSession session, IList<DownloadSegment> segments, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(
                c =>
                {
                    UpsertSession(c, session);
                    foreach (var segment in segments) InsertSegment(c, segment);
                },
                cancellationToken);
        }

        public Task<DownloadSession> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    using (var command = SqliteDatabase.Command(c, "SELECT " + SessionColumns + " FROM download_session WHERE id = @p0", SqliteValues.Id(sessionId)))
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadSession(reader) : null;
                    }
                },
                cancellationToken);
        }

        public Task<IList<DownloadSegment>> GetSegmentsAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<DownloadSegment> result = new List<DownloadSegment>();
                    using (var command = SqliteDatabase.Command(c, "SELECT " + SegmentColumns + " FROM download_segment WHERE session_id = @p0 ORDER BY segment_index", SqliteValues.Id(sessionId)))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) result.Add(ReadSegment(reader));
                    }

                    return result;
                },
                cancellationToken);
        }

        public Task SaveSessionAsync(DownloadSession session, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(c => UpsertSession(c, session), cancellationToken);
        }

        public Task SaveSegmentAsync(DownloadSegment segment, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(
                c =>
                {
                    var rows = SqliteDatabase.Run(
                        c,
                        "UPDATE download_segment SET state = @p1, cursor = @p2, item_offset = @p3, page_index = @p4, received_count = @p5, xml_saved_count = @p6, " +
                        "error_count = @p7, retry_count = @p8, last_error = @p9, check_result = @p10 WHERE id = @p0",
                        SqliteValues.Id(segment.Id), (int)segment.State, segment.Cursor, segment.ItemOffset, segment.PageIndex, segment.ReceivedCount, segment.XmlSavedCount,
                        segment.ErrorCount, segment.RetryCount, segment.LastError, segment.CheckResultCode);
                    if (rows == 0) throw new InvalidOperationException("Đoạn không thuộc phiên nào đã tạo.");
                },
                cancellationToken);
        }

        public Task AddEventAsync(DownloadEvent downloadEvent, CancellationToken cancellationToken)
        {
            return _provider.Current().WriteAsync(
                c => SqliteDatabase.Run(
                    c,
                    "INSERT INTO download_event (" + EventColumns + ") VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                    SqliteValues.Id(downloadEvent.EventId), SqliteValues.Id(downloadEvent.SessionId),
                    downloadEvent.SegmentId.HasValue ? SqliteValues.Id(downloadEvent.SegmentId.Value) : null,
                    SqliteValues.Time(downloadEvent.TimeUtc), (int)downloadEvent.Level, downloadEvent.Code ?? string.Empty, downloadEvent.Message ?? string.Empty),
                cancellationToken);
        }

        public Task<IList<DownloadEvent>> GetEventsAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<DownloadEvent> result = new List<DownloadEvent>();
                    using (var command = SqliteDatabase.Command(c, "SELECT " + EventColumns + " FROM download_event WHERE session_id = @p0 ORDER BY time_utc, rowid", SqliteValues.Id(sessionId)))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var segment = SqliteValues.Text(reader["segment_id"]);
                            result.Add(DownloadEvent.Restore(
                                SqliteValues.ParseId(reader["id"]), SqliteValues.ParseId(reader["session_id"]),
                                segment == null ? (Guid?)null : Guid.Parse(segment),
                                SqliteValues.ParseTime(reader["time_utc"]), (DownloadEventLevel)SqliteValues.Int(reader["level"]),
                                SqliteValues.Text(reader["code"]), SqliteValues.Text(reader["message"])));
                        }
                    }

                    return result;
                },
                cancellationToken);
        }

        public Task<IList<DownloadSession>> GetUnfinishedAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            // Chưa kết thúc: Draft(0), Running(1), PausedByUser(4), WaitingForLogin(5); xem DownloadSessionState.
            return QuerySessions("company_id = @p0 AND state IN " + UnfinishedStates + " ORDER BY created_utc, rowid", companyId, cancellationToken);
        }

        public Task<IList<DownloadSession>> GetSessionsAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return QuerySessions("company_id = @p0 ORDER BY created_utc DESC, rowid DESC", companyId, cancellationToken);
        }

        public Task<IDictionary<Guid, IList<DownloadSegment>>> GetSegmentsByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IDictionary<Guid, IList<DownloadSegment>> result = new Dictionary<Guid, IList<DownloadSegment>>();
                    var company = SqliteValues.Id(companyId.Value);
                    using (var command = SqliteDatabase.Command(c, "SELECT id FROM download_session WHERE company_id = @p0", company))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) result[SqliteValues.ParseId(reader["id"])] = new List<DownloadSegment>();
                    }

                    using (var command = SqliteDatabase.Command(
                        c,
                        "SELECT " + Prefixed("s.", SegmentColumns) + " FROM download_segment s JOIN download_session d ON d.id = s.session_id WHERE d.company_id = @p0 ORDER BY s.session_id, s.segment_index",
                        company))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var segment = ReadSegment(reader);
                            result[segment.SessionId].Add(segment);
                        }
                    }

                    return result;
                },
                cancellationToken);
        }

        private Task<IList<DownloadSession>> QuerySessions(string tail, CompanyId companyId, CancellationToken cancellationToken)
        {
            return _provider.Current().ReadAsync(
                c =>
                {
                    IList<DownloadSession> result = new List<DownloadSession>();
                    using (var command = SqliteDatabase.Command(c, "SELECT " + SessionColumns + " FROM download_session WHERE " + tail, SqliteValues.Id(companyId.Value)))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read()) result.Add(ReadSession(reader));
                    }

                    return result;
                },
                cancellationToken);
        }

        private static string Prefixed(string prefix, string columns)
        {
            return prefix + columns.Replace(", ", ", " + prefix);
        }

        private static void UpsertSession(SQLiteConnection c, DownloadSession s)
        {
            // ON CONFLICT DO UPDATE (không dùng REPLACE) để không xóa đoạn và sự kiện con qua ON DELETE CASCADE.
            SqliteDatabase.Run(
                c,
                "INSERT INTO download_session (" + SessionColumns + ") VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11) " +
                "ON CONFLICT(id) DO UPDATE SET state = excluded.state, started_utc = excluded.started_utc, finished_utc = excluded.finished_utc",
                SqliteValues.Id(s.Id), SqliteValues.Id(s.CompanyId.Value), s.TaxCode.Value, s.StorageRoot, (int)s.Direction, PeriodJson(s.Period), JsonConvert.SerializeObject(s.Filter),
                (int)s.State, SqliteValues.Time(s.CreatedAtUtc), SqliteValues.Time(s.StartedAtUtc), SqliteValues.Time(s.FinishedAtUtc), s.StartedByUser);
        }

        private static void InsertSegment(SQLiteConnection c, DownloadSegment g)
        {
            SqliteDatabase.Run(
                c,
                "INSERT INTO download_segment (" + SegmentColumns + ") VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14)",
                SqliteValues.Id(g.Id), SqliteValues.Id(g.SessionId), g.Index, SqliteValues.Date(g.Range.Start), SqliteValues.Date(g.Range.End), (int)g.State, g.Cursor, g.ItemOffset,
                g.PageIndex, g.ReceivedCount, g.XmlSavedCount, g.ErrorCount, g.RetryCount, g.LastError, g.CheckResultCode);
        }

        private static DownloadSession ReadSession(SQLiteDataReader r)
        {
            return DownloadSession.Restore(
                SqliteValues.ParseId(r["id"]), new CompanyId(SqliteValues.ParseId(r["company_id"])), TaxCode.Parse(SqliteValues.Text(r["tax_code"])),
                SqliteValues.Text(r["storage_root"]), (InvoiceDirection)SqliteValues.Int(r["direction"]), ParsePeriod(SqliteValues.Text(r["period_json"])),
                JsonConvert.DeserializeObject<InvoiceSearchFilter>(SqliteValues.Text(r["filter_json"])), (DownloadSessionState)SqliteValues.Int(r["state"]),
                SqliteValues.ParseTime(r["created_utc"]), SqliteValues.ParseTimeOrNull(r["started_utc"]), SqliteValues.ParseTimeOrNull(r["finished_utc"]),
                SqliteValues.Text(r["started_by_user"]));
        }

        private static DownloadSegment ReadSegment(SQLiteDataReader r)
        {
            var segment = new DownloadSegment(
                SqliteValues.ParseId(r["id"]), SqliteValues.ParseId(r["session_id"]), SqliteValues.Int(r["segment_index"]),
                new DateSegment(SqliteValues.ParseDate(r["from_date"]), SqliteValues.ParseDate(r["to_date"])));
            segment.RestoreState((DownloadSegmentState)SqliteValues.Int(r["state"]));
            segment.Cursor = SqliteValues.Text(r["cursor"]);
            segment.ItemOffset = SqliteValues.Int(r["item_offset"]);
            segment.PageIndex = SqliteValues.Int(r["page_index"]);
            segment.ReceivedCount = SqliteValues.Int(r["received_count"]);
            segment.XmlSavedCount = SqliteValues.Int(r["xml_saved_count"]);
            segment.ErrorCount = SqliteValues.Int(r["error_count"]);
            segment.RetryCount = SqliteValues.Int(r["retry_count"]);
            segment.LastError = SqliteValues.Text(r["last_error"]);
            segment.CheckResultCode = SqliteValues.Text(r["check_result"]);
            return segment;
        }

        private static string PeriodJson(PeriodSpec p)
        {
            var json = new JObject { { "kind", p.Kind.ToString() }, { "year", p.Year } };
            if (p.Month.HasValue) json["month"] = p.Month.Value;
            if (p.Quarter.HasValue) json["quarter"] = p.Quarter.Value;
            if (p.From.HasValue) json["from"] = SqliteValues.Date(p.From.Value);
            if (p.To.HasValue) json["to"] = SqliteValues.Date(p.To.Value);
            return json.ToString(Formatting.None);
        }

        private static PeriodSpec ParsePeriod(string text)
        {
            var json = JObject.Parse(text);
            var year = (int)json["year"];
            switch ((PeriodKind)Enum.Parse(typeof(PeriodKind), (string)json["kind"]))
            {
                case PeriodKind.Month: return PeriodSpec.ForMonth(year, (int)json["month"]);
                case PeriodKind.Quarter: return PeriodSpec.ForQuarter(year, (int)json["quarter"]);
                case PeriodKind.Year: return PeriodSpec.ForYear(year);
                default:
                    return PeriodSpec.Custom(
                        json["from"] == null ? (DateTime?)null : SqliteValues.ParseDate((string)json["from"]),
                        json["to"] == null ? (DateTime?)null : SqliteValues.ParseDate((string)json["to"]));
            }
        }
    }
}
