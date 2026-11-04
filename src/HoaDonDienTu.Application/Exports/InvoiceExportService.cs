using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Exports
{
    public enum ZipFailurePolicy
    {
        /// <summary>Gặp file thiếu/lỗi thì dừng và để người dùng quyết định (mặc định).</summary>
        Ask,

        /// <summary>Bỏ qua file thiếu/lỗi, vẫn xuất phần hợp lệ và ghi rõ vào manifest.</summary>
        SkipAndRecord,

        /// <summary>Gặp file thiếu/lỗi thì hủy toàn bộ.</summary>
        Abort
    }

    public enum ExportProblemKind
    {
        MissingFile,
        HashMismatch,
        TooLarge,
        NoCurrentVersion
    }

    public sealed class ExportProblem
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string InvoiceSymbol { get; set; }
        public ExportProblemKind Kind { get; set; }

        public string Reason
        {
            get
            {
                switch (Kind)
                {
                    case ExportProblemKind.MissingFile: return "File XML không còn trong thư mục kho";
                    case ExportProblemKind.HashMismatch: return "Nội dung file khác lúc tải (sai SHA-256)";
                    case ExportProblemKind.TooLarge: return "File XML quá lớn để đóng gói";
                    default: return "Hóa đơn chưa có file XML";
                }
            }
        }
    }

    /// <summary>Phạm vi xuất: toàn bộ kết quả của một truy vấn kho, hoặc đúng các hóa đơn đã chọn.</summary>
    public sealed class ExportScope
    {
        private ExportScope()
        {
        }

        public ArchiveQuery Query { get; private set; }
        public IList<Guid> SelectedInvoiceIds { get; private set; }

        public bool IsSelection
        {
            get { return SelectedInvoiceIds != null; }
        }

        public static ExportScope ForQuery(ArchiveQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            return new ExportScope { Query = query.Clone() };
        }

        public static ExportScope ForSelection(IEnumerable<Guid> invoiceIds)
        {
            if (invoiceIds == null) throw new ArgumentNullException("invoiceIds");
            return new ExportScope { SelectedInvoiceIds = invoiceIds.ToList() };
        }
    }

    public sealed class ExportRequest
    {
        public ExportScope Scope { get; set; }
        public string TargetPath { get; set; }

        /// <summary>false: file đích đã có thì báo lỗi thay vì ghi đè (FR-EXPORT-008). Người dùng đã xác nhận ghi đè thì đặt true.</summary>
        public bool Overwrite { get; set; }
    }

    public sealed class ExportProgress
    {
        public ExportProgress(int done, int total)
        {
            Done = done;
            Total = total;
        }

        public int Done { get; private set; }
        public int Total { get; private set; }
    }

    public sealed class ExportResult
    {
        public string Path { get; set; }

        /// <summary>Số hóa đơn đã ghi vào file.</summary>
        public int Exported { get; set; }

        /// <summary>Hóa đơn bị bỏ qua khi xuất ZIP (đã ghi trong manifest).</summary>
        public IList<ExportProblem> Skipped { get; set; }
    }

    public sealed class ExportTargetExistsException : Exception
    {
        public ExportTargetExistsException(string path)
            : base("File đã tồn tại: " + path)
        {
            Path = path;
        }

        public string Path { get; private set; }
    }

    /// <summary>ZIP có file thiếu/lỗi và chính sách là hỏi: chưa ghi gì ra đĩa, người dùng chọn xuất phần hợp lệ hoặc hủy.</summary>
    public sealed class ExportNeedsDecisionException : Exception
    {
        public ExportNeedsDecisionException(IList<ExportProblem> problems, int validCount)
            : base(problems.Count + " hóa đơn thiếu hoặc lỗi file XML.")
        {
            Problems = problems;
            ValidCount = validCount;
        }

        public IList<ExportProblem> Problems { get; private set; }
        public int ValidCount { get; private set; }
    }

    public sealed class ExportAbortedException : Exception
    {
        public ExportAbortedException(IList<ExportProblem> problems)
            : base("Đã hủy xuất vì " + problems.Count + " hóa đơn thiếu hoặc lỗi file XML.")
        {
            Problems = problems;
        }

        public IList<ExportProblem> Problems { get; private set; }
    }

    public static class ExportFileNamer
    {
        /// <summary>Tên file: &lt;loại&gt;_&lt;phạm-vi&gt;_&lt;yyyyMMdd-HHmmss&gt;.&lt;đuôi&gt; (FR-EXPORT-007), đã bỏ ký tự không hợp lệ.</summary>
        public static string FileName(string kind, string scopeLabel, DateTime localTime, string extension)
        {
            var name = Clean(kind) + "_" + Clean(scopeLabel) + "_" + localTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "." + extension.TrimStart('.');
            return name;
        }

        public static string ScopeLabel(ExportScope scope)
        {
            if (scope.IsSelection) return "Da-chon-" + scope.SelectedInvoiceIds.Count;
            var q = scope.Query;
            var parts = new List<string> { q.Direction == InvoiceDirection.Purchase ? "Mua-vao" : "Ban-ra" };
            if (q.IssueFrom.HasValue && q.IssueTo.HasValue)
            {
                parts.Add(q.IssueFrom.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + q.IssueTo.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }

            return string.Join("_", parts);
        }

        private static string Clean(string value)
        {
            var sb = new StringBuilder();
            foreach (var c in value ?? string.Empty)
            {
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || char.IsWhiteSpace(c) ? '-' : c);
            }

            return sb.ToString();
        }
    }

    public interface IInvoiceExportService
    {
        /// <exception cref="ExportTargetExistsException"/>
        /// <exception cref="InvoiceStorageException">Không có hóa đơn nào để xuất.</exception>
        /// <exception cref="EntitlementDeniedException"/>
        Task<ExportResult> ExportExcelAsync(ExportRequest request, IProgress<ExportProgress> progress, CancellationToken cancellationToken);

        /// <exception cref="ExportNeedsDecisionException">Chính sách Ask và có file thiếu/lỗi.</exception>
        /// <exception cref="ExportAbortedException">Chính sách Abort và có file thiếu/lỗi.</exception>
        Task<ExportResult> ExportZipAsync(ExportRequest request, ZipFailurePolicy policy, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Xuất bảng kê Excel và ZIP XML từ kho (DD-09). Ghi file tạm cùng thư mục rồi đổi tên nên file đích không bao giờ ở trạng thái dở;
    /// hủy hoặc lỗi thì xóa file tạm. Cần quyền ReadExport nên dùng được cả ở chế độ chỉ đọc.
    /// </summary>
    public sealed class InvoiceExportService : IInvoiceExportService
    {
        /// <summary>Mỗi hóa đơn đóng gói tối đa từng này byte (XML hóa đơn thường chỉ vài chục KB).</summary>
        public const long MaxEntryBytes = 50L * 1024 * 1024;

        public const string ManifestName = "manifest.csv";

        private readonly IInvoiceRepository _repository;
        private readonly IInvoiceArchiveService _archive;
        private readonly IInvoiceFileReader _reader;
        private readonly ICompanyContextAccessor _context;
        private readonly IEntitlementService _entitlement;
        private readonly IExportHistoryRepository _history;
        private readonly IClock _clock;

        /// <param name="history">Nơi ghi lịch sử xuất; null thì không ghi.</param>
        /// <param name="clock">Giờ ghi vào lịch sử; mặc định là giờ hệ thống.</param>
        public InvoiceExportService(
            IInvoiceRepository repository,
            IInvoiceArchiveService archive,
            IInvoiceFileReader reader,
            ICompanyContextAccessor context,
            IEntitlementService entitlement,
            IExportHistoryRepository history = null,
            IClock clock = null)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (archive == null) throw new ArgumentNullException("archive");
            if (reader == null) throw new ArgumentNullException("reader");
            if (context == null) throw new ArgumentNullException("context");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            _repository = repository;
            _archive = archive;
            _reader = reader;
            _context = context;
            _entitlement = entitlement;
            _history = history;
            _clock = clock;
        }

        /// <summary>
        /// Ghi lịch sử sau khi file đã đổi tên xong. Lưu lịch sử hỏng không được làm hỏng một lần xuất đã thành công,
        /// nên lỗi ở đây chỉ được ghi vết.
        /// </summary>
        private async Task RecordAsync(ExportKind kind, ExportRequest request, ExportScope scope, int scopeCount, int exported, IList<ExportProblem> skipped)
        {
            if (_history == null) return;
            try
            {
                var lines = skipped.Take(ExportRecord.MaxSkippedLines)
                    .Select(p => "Số " + p.InvoiceNumber + " · " + p.InvoiceSymbol + ": " + p.Reason);
                await _history.AddAsync(
                    new ExportRecord
                    {
                        Id = Guid.NewGuid(),
                        Kind = kind,
                        TargetPath = request.TargetPath,
                        ScopeText = ScopeText(scope, scopeCount),
                        ExportedCount = exported,
                        SkippedCount = skipped.Count,
                        SkippedSummary = string.Join("\n", lines),
                        CreatedAtUtc = _clock != null ? _clock.UtcNow : DateTime.UtcNow
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Không ghi được lịch sử xuất: " + ex.Message);
            }
        }

        // ---- Excel ----

        public async Task<ExportResult> ExportExcelAsync(ExportRequest request, IProgress<ExportProgress> progress, CancellationToken cancellationToken)
        {
            Prepare(request);
            var invoices = await ResolveAsync(request.Scope, cancellationToken).ConfigureAwait(false);
            var total = invoices.Count;

            var temp = BeginWrite(request);
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    InvoiceExcelWriter.Write(
                        stream,
                        ScopeText(request.Scope, total),
                        RangeText(request.Scope, invoices),
                        invoices.Select(i => i.Invoice),
                        done =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Report(progress, done, total);
                        });
                }

                Commit(temp, request);
                var skippedNone = new List<ExportProblem>();
                await RecordAsync(ExportKind.Excel, request, request.Scope, total, total, skippedNone).ConfigureAwait(false);
                return new ExportResult { Path = request.TargetPath, Exported = total, Skipped = skippedNone };
            }
            catch
            {
                Discard(temp);
                throw;
            }
        }

        // ---- ZIP XML ----

        public async Task<ExportResult> ExportZipAsync(ExportRequest request, ZipFailurePolicy policy, IProgress<ExportProgress> progress, CancellationToken cancellationToken)
        {
            Prepare(request);
            var root = _context.Current.StorageRoot;
            var invoices = await ResolveAsync(request.Scope, cancellationToken).ConfigureAwait(false);
            var total = invoices.Count;

            var temp = BeginWrite(request);
            try
            {
                var problems = new List<ExportProblem>();
                var manifest = new List<ManifestLine>();
                var exported = 0;
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ManifestName };

                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    var done = 0;
                    foreach (var stored in invoices)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var line = Pack(zip, root, stored, usedNames, invoices.Count == 1);
                        manifest.Add(line);
                        if (line.Problem != null) problems.Add(line.Problem);
                        else exported++;
                        Report(progress, ++done, total);

                        if (problems.Count > 0 && policy == ZipFailurePolicy.Abort) break;
                    }

                    if (problems.Count > 0 && policy != ZipFailurePolicy.SkipAndRecord)
                    {
                        // Bỏ gói đang ghi dở: chưa có gì ở vị trí đích.
                        throw policy == ZipFailurePolicy.Abort
                            ? (Exception)new ExportAbortedException(problems)
                            : new ExportNeedsDecisionException(problems, exported);
                    }

                    if (exported == 0) throw new InvoiceStorageException("Không có hóa đơn nào có file XML hợp lệ để xuất.");
                    if (invoices.Count > 1) WriteManifest(zip, manifest);
                }

                Commit(temp, request);
                await RecordAsync(ExportKind.Zip, request, request.Scope, total, exported, problems).ConfigureAwait(false);
                return new ExportResult { Path = request.TargetPath, Exported = exported, Skipped = problems };
            }
            catch
            {
                Discard(temp);
                throw;
            }
        }

        private sealed class ManifestLine
        {
            public StoredInvoice Invoice;
            public string FileName;
            public string Sha256;
            public ExportProblem Problem;
        }

        private ManifestLine Pack(ZipArchive zip, string root, StoredInvoice stored, HashSet<string> usedNames, bool single)
        {
            var line = new ManifestLine { Invoice = stored };
            var current = stored.Current;
            if (current == null)
            {
                line.Problem = Problem(stored, ExportProblemKind.NoCurrentVersion);
                return line;
            }

            byte[] bytes;
            try
            {
                bytes = _reader.Read(root, current.RelativePath, MaxEntryBytes);
            }
            catch (FileNotFoundException)
            {
                line.Problem = Problem(stored, ExportProblemKind.MissingFile);
                return line;
            }
            catch (DirectoryNotFoundException)
            {
                line.Problem = Problem(stored, ExportProblemKind.MissingFile);
                return line;
            }
            catch (InvoiceFileTooLargeException)
            {
                line.Problem = Problem(stored, ExportProblemKind.TooLarge);
                return line;
            }

            line.Sha256 = Hex(bytes);
            if (!string.Equals(line.Sha256, current.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                // Không đóng gói file đã bị sửa hoặc hỏng; manifest ghi hash thực tế để đối chiếu.
                line.Problem = Problem(stored, ExportProblemKind.HashMismatch);
                return line;
            }

            // Gói giống gói "Xuất xml" của cổng: invoice.xml + invoice.html + details.js + sign-check.jpg + viewinvoice-bg.jpg. Một hóa đơn thì năm file nằm ngay trong zip như cổng;
            // nhiều hóa đơn thì mỗi hóa đơn một thư mục (tên theo file XML trong kho) để trang xem của hóa đơn nào mở được ngay trong thư mục của nó.
            var folder = single ? string.Empty : UniqueName(Path.GetFileNameWithoutExtension(current.RelativePath), usedNames) + "/";
            line.FileName = folder + InvoiceViewerPackage.XmlName;
            foreach (var file in InvoiceViewerPackage.Files(bytes))
            {
                var entry = zip.CreateEntry(folder + file.Name, CompressionLevel.Optimal);
                using (var s = entry.Open())
                {
                    s.Write(file.Content, 0, file.Content.Length);
                }
            }

            return line;
        }

        private static string UniqueName(string name, HashSet<string> used)
        {
            if (string.IsNullOrEmpty(name)) name = "invoice.xml";
            var candidate = name;
            var n = 1;
            while (!used.Add(candidate))
            {
                candidate = Path.GetFileNameWithoutExtension(name) + "_" + (++n) + Path.GetExtension(name);
            }

            return candidate;
        }

        private static ExportProblem Problem(StoredInvoice stored, ExportProblemKind kind)
        {
            return new ExportProblem { InvoiceId = stored.InvoiceId, InvoiceNumber = stored.Invoice.InvoiceNumber, InvoiceSymbol = stored.Invoice.InvoiceSymbol, Kind = kind };
        }

        /// <summary>manifest.csv (UTF-8 có BOM để Excel đọc đúng tiếng Việt): mỗi hóa đơn một dòng, file hợp lệ và file bị bỏ qua (FR-EXPORT-005, FR-EXPORT-006).</summary>
        private static void WriteManifest(ZipArchive zip, IList<ManifestLine> lines)
        {
            var entry = zip.CreateEntry(ManifestName, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var w = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                w.Write("so_hoa_don,ky_hieu,ngay_lap,mst_nguoi_ban,ten_file,sha256,trang_thai,ly_do\r\n");
                foreach (var l in lines)
                {
                    var i = l.Invoice.Invoice;
                    w.Write(string.Join(",", new[]
                    {
                        Csv(i.InvoiceNumber), Csv(i.InvoiceSymbol), Csv(i.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(i.SellerTaxCode),
                        Csv(l.FileName), Csv(l.Sha256), Csv(l.Problem == null ? "OK" : "BO_QUA"), Csv(l.Problem == null ? string.Empty : l.Problem.Reason)
                    }));
                    w.Write("\r\n");
                }
            }
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;

            // Chữ bắt đầu bằng = + - @ có thể bị Excel hiểu là công thức khi mở manifest.
            if (value.Length > 0 && "=+-@".IndexOf(value[0]) >= 0) value = "'" + value;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        // ---- Chung ----

        private void Prepare(ExportRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Scope == null) throw new ArgumentException("Thiếu phạm vi xuất.", "request");
            if (string.IsNullOrWhiteSpace(request.TargetPath)) throw new ArgumentException("Thiếu đường dẫn file xuất.", "request");
            _entitlement.Demand(AppFeature.ReadExport);
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
        }

        private const int LoadChunk = 500;

        private async Task<IList<StoredInvoice>> ResolveAsync(ExportScope scope, CancellationToken token)
        {
            var result = new List<StoredInvoice>();
            IEnumerable<Guid> ids;
            if (scope.IsSelection)
            {
                ids = scope.SelectedInvoiceIds.Distinct().ToList();
            }
            else
            {
                var query = scope.Query.Clone();
                query.PageIndex = 0;
                query.PageSize = int.MaxValue;
                var page = await _archive.QueryAsync(query, token).ConfigureAwait(false);
                ids = page.Rows.Select(r => r.InvoiceId).ToList();
            }

            // Nạp theo lô thay vì từng hóa đơn một: 100.000 hóa đơn chỉ cần vài trăm truy vấn. FindManyAsync giữ nguyên thứ tự id.
            var idList = ids.ToList();
            for (var offset = 0; offset < idList.Count; offset += LoadChunk)
            {
                token.ThrowIfCancellationRequested();
                var chunk = idList.GetRange(offset, Math.Min(LoadChunk, idList.Count - offset));
                var found = await _repository.FindManyAsync(chunk, token).ConfigureAwait(false);

                // Hóa đơn của doanh nghiệp khác hoặc đã biến mất khỏi kho thì không xuất (FindManyAsync không trả về).
                result.AddRange(found);
            }

            if (result.Count == 0) throw new InvoiceStorageException("Không có hóa đơn nào để xuất.");
            return result;
        }

        private static string ScopeText(ExportScope scope, int count)
        {
            if (scope.IsSelection) return "Phạm vi: các hóa đơn đã chọn (" + count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " hóa đơn)";
            return "Phạm vi: toàn bộ kết quả đang lọc (" + (scope.Query.Direction == InvoiceDirection.Purchase ? "mua vào" : "bán ra") + ", "
                + count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " hóa đơn)";
        }

        private static string RangeText(ExportScope scope, IList<StoredInvoice> invoices)
        {
            DateTime from;
            DateTime to;
            if (!scope.IsSelection && scope.Query.IssueFrom.HasValue && scope.Query.IssueTo.HasValue)
            {
                from = scope.Query.IssueFrom.Value;
                to = scope.Query.IssueTo.Value;
            }
            else
            {
                from = invoices.Min(i => i.Invoice.IssueDate);
                to = invoices.Max(i => i.Invoice.IssueDate);
            }

            return "Từ ngày " + from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " đến ngày " + to.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static void Report(IProgress<ExportProgress> progress, int done, int total)
        {
            if (progress != null) progress.Report(new ExportProgress(done, total));
        }

        private static string BeginWrite(ExportRequest request)
        {
            var target = Path.GetFullPath(request.TargetPath);
            if (File.Exists(target) && !request.Overwrite) throw new ExportTargetExistsException(target);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            return target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        }

        private static void Commit(string temp, ExportRequest request)
        {
            var target = Path.GetFullPath(request.TargetPath);
            if (File.Exists(target))
            {
                // Chỉ tới đây khi người dùng đã xác nhận ghi đè.
                if (!request.Overwrite) throw new ExportTargetExistsException(target);
                File.Delete(target);
            }

            File.Move(temp, target);
        }

        private static void Discard(string temp)
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string Hex(byte[] content)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(content);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
