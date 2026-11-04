using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>Liệt kê file XML thật sự có trong thư mục kho (để tìm file mồ côi). Đường dẫn trả về là tương đối so với thư mục kho.</summary>
    public interface IInvoiceFileScanner
    {
        /// <summary>Mọi file <c>.xml</c> dưới thư mục kho, trừ thư mục dữ liệu của ứng dụng.</summary>
        IEnumerable<string> EnumerateXmlFiles(string storageRoot);

        /// <summary>File tạm (<c>.tmp</c>) còn sót lại do ghi dở, ví dụ khi mất điện.</summary>
        IEnumerable<string> EnumerateTempFiles(string storageRoot);

        /// <summary>Xóa một file tạm (đường dẫn tương đối so với thư mục kho). Chỉ nhận file <c>.tmp</c> nằm trong thư mục kho; trả về false nếu không có file hoặc không xóa được.</summary>
        bool DeleteTempFile(string storageRoot, string relativePath);
    }

    public enum IntegrityProblemKind
    {
        /// <summary>File của phiên bản không còn trong thư mục kho.</summary>
        Missing,

        /// <summary>File có nhưng đọc không được (khóa, không có quyền, quá lớn).</summary>
        Unreadable,

        /// <summary>Nội dung khác với lúc tải về (sai SHA-256).</summary>
        HashMismatch,

        /// <summary>Đúng SHA-256 nhưng không phải XML đọc được.</summary>
        NotXml
    }

    public sealed class IntegrityProblem
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string InvoiceSymbol { get; set; }
        public int VersionNo { get; set; }
        public string RelativePath { get; set; }
        public IntegrityProblemKind Kind { get; set; }
    }

    public sealed class IntegrityReport
    {
        public IntegrityReport()
        {
            Problems = new List<IntegrityProblem>();
            Orphans = new List<string>();
            StrayTemps = new List<string>();
        }

        public int VersionsChecked { get; set; }
        public IList<IntegrityProblem> Problems { get; private set; }

        /// <summary>File XML nằm trong thư mục kho nhưng không phiên bản nào trỏ tới. Chỉ báo, không tự xóa.</summary>
        public IList<string> Orphans { get; private set; }

        /// <summary>File tạm bị bỏ lại; an toàn để dọn vì file chính thức chỉ có sau khi đổi tên.</summary>
        public IList<string> StrayTemps { get; private set; }

        public bool IsClean
        {
            get { return Problems.Count == 0 && Orphans.Count == 0 && StrayTemps.Count == 0; }
        }
    }

    /// <summary>So sánh metadata với đĩa: thiếu file, đọc không được, sai SHA-256, không phải XML, file mồ côi (DD-06 mục 7; FR-STORE-009, 010).</summary>
    public interface IInvoiceIntegrityChecker
    {
        /// <param name="progress">Số phiên bản đã kiểm; có thể null.</param>
        Task<IntegrityReport> CheckAsync(IProgress<int> progress, CancellationToken cancellationToken);

        /// <summary>Xóa các file tạm sót lại có trong <paramref name="relativePaths"/> (chỉ những file hiện vẫn là file tạm của kho); trả về các đường dẫn đã xóa. Không đụng tới file XML.</summary>
        Task<IList<string>> DeleteStrayTempsAsync(IEnumerable<string> relativePaths, CancellationToken cancellationToken);
    }

    /// <summary>Chỉ đọc: không sửa, không xóa, không tải lại file nào. Tải lại là hành động của người dùng.</summary>
    public sealed class InvoiceIntegrityChecker : IInvoiceIntegrityChecker
    {
        private const long MaxReadBytes = 256L * 1024 * 1024;

        private readonly IInvoiceRepository _repository;
        private readonly IInvoiceFileReader _reader;
        private readonly IInvoiceFileScanner _scanner;
        private readonly ICompanyContextAccessor _context;
        private readonly IClock _clock;

        public InvoiceIntegrityChecker(IInvoiceRepository repository, IInvoiceFileReader reader, IInvoiceFileScanner scanner, ICompanyContextAccessor context, IClock clock = null)
        {
            _clock = clock;
            if (repository == null) throw new ArgumentNullException("repository");
            if (reader == null) throw new ArgumentNullException("reader");
            if (scanner == null) throw new ArgumentNullException("scanner");
            if (context == null) throw new ArgumentNullException("context");
            _repository = repository;
            _reader = reader;
            _scanner = scanner;
            _context = context;
        }

        public async Task<IList<string>> DeleteStrayTempsAsync(IEnumerable<string> relativePaths, CancellationToken cancellationToken)
        {
            if (relativePaths == null) throw new ArgumentNullException("relativePaths");
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var root = _context.Current.StorageRoot;
            var wanted = new HashSet<string>(relativePaths.Select(Normalize), StringComparer.OrdinalIgnoreCase);
            return await Task.Run(() =>
            {
                var deleted = new List<string>();
                // Chỉ xóa file mà bộ quét vẫn coi là file tạm của kho, không tin đường dẫn do giao diện gửi lên.
                foreach (var temp in _scanner.EnumerateTempFiles(root).ToList())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (wanted.Contains(Normalize(temp)) && _scanner.DeleteTempFile(root, temp)) deleted.Add(temp);
                }

                return (IList<string>)deleted;
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IntegrityReport> CheckAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var root = _context.Current.StorageRoot;
            var report = new IntegrityReport();
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var invoices = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
            var results = new Dictionary<Guid, IntegrityProblemKind?>(invoices.Count);
            foreach (var invoice in invoices)
            {
                results[invoice.InvoiceId] = null;
                foreach (var version in invoice.Versions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrEmpty(version.RelativePath)) referenced.Add(Normalize(version.RelativePath));

                    var kind = await Task.Run(() => Check(root, version), cancellationToken).ConfigureAwait(false);
                    report.VersionsChecked++;
                    if (kind.HasValue)
                    {
                        if (!results[invoice.InvoiceId].HasValue) results[invoice.InvoiceId] = kind;
                        report.Problems.Add(new IntegrityProblem
                        {
                            InvoiceId = invoice.InvoiceId,
                            InvoiceNumber = invoice.Invoice.InvoiceNumber,
                            InvoiceSymbol = invoice.Invoice.InvoiceSymbol,
                            VersionNo = version.VersionNo,
                            RelativePath = version.RelativePath,
                            Kind = kind.Value
                        });
                    }

                    if (progress != null) progress.Report(report.VersionsChecked);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var orphans = await Task.Run(() => _scanner.EnumerateXmlFiles(root).Where(p => !referenced.Contains(Normalize(p))).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(), cancellationToken).ConfigureAwait(false);
            foreach (var orphan in orphans) report.Orphans.Add(orphan);

            var temps = await Task.Run(() => _scanner.EnumerateTempFiles(root).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(), cancellationToken).ConfigureAwait(false);
            foreach (var temp in temps) report.StrayTemps.Add(temp);

            // Chỉ ghi khi đã kiểm xong cả kho: bị hủy giữa chừng thì kết quả cũ còn nguyên, không có kho nửa mới nửa cũ.
            await _repository.SaveIntegrityAsync(results, _clock == null ? DateTime.UtcNow : _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return report;
        }

        private IntegrityProblemKind? Check(string root, InvoiceVersionRecord version)
        {
            byte[] bytes;
            try
            {
                bytes = _reader.Read(root, version.RelativePath, MaxReadBytes);
            }
            catch (FileNotFoundException)
            {
                return IntegrityProblemKind.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return IntegrityProblemKind.Missing;
            }
            catch (InvoiceFileTooLargeException)
            {
                return IntegrityProblemKind.Unreadable;
            }
            catch (IOException)
            {
                return IntegrityProblemKind.Unreadable;
            }
            catch (UnauthorizedAccessException)
            {
                return IntegrityProblemKind.Unreadable;
            }

            string hash;
            using (var sha = SHA256.Create()) hash = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            if (!string.Equals(hash, version.Sha256, StringComparison.OrdinalIgnoreCase)) return IntegrityProblemKind.HashMismatch;

            try
            {
                InvoiceXmlValidator.Validate(bytes);
            }
            catch (InvoiceStorageException)
            {
                return IntegrityProblemKind.NotXml;
            }
            catch (XmlException)
            {
                return IntegrityProblemKind.NotXml;
            }

            return null;
        }

        private static string Normalize(string relativePath)
        {
            return relativePath.Replace('/', '\\').TrimStart('\\');
        }
    }
}
