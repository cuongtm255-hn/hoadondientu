using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>File vượt giới hạn hiển thị; người dùng vẫn mở được bằng ứng dụng bên ngoài sau khi lưu bản sao.</summary>
    public sealed class InvoiceFileTooLargeException : Exception
    {
        public InvoiceFileTooLargeException(long size, long limit)
            : base("File XML lớn hơn giới hạn hiển thị (" + size + " > " + limit + " byte).")
        {
        }
    }

    /// <summary>Đọc nguyên văn file XML đã lưu. Chỉ đọc; đường dẫn phải nằm trong thư mục kho.</summary>
    public interface IInvoiceFileReader
    {
        /// <exception cref="FileNotFoundException">File không còn trong kho.</exception>
        /// <exception cref="InvoiceFileTooLargeException"/>
        byte[] Read(string storageRoot, string relativePath, long maxBytes);
    }

    public enum VersionFileState
    {
        Ok,
        Missing
    }

    public sealed class InvoiceVersionDetail
    {
        public int VersionNo { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string Sha256 { get; set; }
        public long SizeBytes { get; set; }
        public bool IsCurrent { get; set; }
        public VersionFileState FileState { get; set; }

        /// <summary>Phiên tải đã mang phiên bản này về; rỗng nếu không còn thông tin phiên.</summary>
        public Guid SessionId { get; set; }

        public string SessionCode { get; set; }
        public string SessionPeriod { get; set; }
        public DateTime? SessionCreatedAtUtc { get; set; }
    }

    public sealed class InvoiceDetail
    {
        public Guid InvoiceId { get; set; }
        public NormalizedInvoice Invoice { get; set; }
        public DateTime FirstSeenAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }

        /// <summary>Mới nhất trước.</summary>
        public IList<InvoiceVersionDetail> Versions { get; set; }

        public InvoiceVersionDetail Current
        {
            get { return Versions.FirstOrDefault(v => v.IsCurrent); }
        }
    }

    public sealed class XmlTreeNode
    {
        public XmlTreeNode()
        {
            Children = new List<XmlTreeNode>();
        }

        public string Name { get; set; }

        /// <summary>Nội dung chữ của phần tử lá; null khi phần tử có con.</summary>
        public string Value { get; set; }

        public string Attributes { get; set; }
        public IList<XmlTreeNode> Children { get; private set; }
    }

    /// <summary>Nội dung XML đã mở để xem, kèm cảnh báo cụ thể khi file mất, lớn, hỏng hoặc sai SHA-256 (FR-STORE-009).</summary>
    public sealed class InvoiceXmlView
    {
        public Guid InvoiceId { get; set; }
        public int VersionNo { get; set; }
        public string Sha256 { get; set; }
        public long SizeBytes { get; set; }

        /// <summary>Nội dung nguyên văn; null khi không đọc được file.</summary>
        public string RawText { get; set; }

        /// <summary>Byte nguyên văn dùng cho "Lưu bản sao"; null khi không đọc được file.</summary>
        public byte[] Bytes { get; set; }

        /// <summary>null khi XML không đọc được (vẫn xem được nội dung thô nếu có).</summary>
        public XmlTreeNode Tree { get; set; }

        public bool HashMatches { get; set; }

        /// <summary>File quá lớn để hiển thị nên không nạp vào bộ nhớ; vẫn lưu bản sao được qua <see cref="IInvoiceDetailService.ReadBytesAsync"/>.</summary>
        public bool TooLarge { get; set; }

        /// <summary>Cảnh báo bằng tiếng Việt; null khi file đủ, đọc được và đúng hash.</summary>
        public string Problem { get; set; }
    }

    public interface IInvoiceDetailService
    {
        /// <summary>null khi hóa đơn không còn trong kho của doanh nghiệp đang mở.</summary>
        /// <exception cref="StorageGuardException">Chưa mở doanh nghiệp nào.</exception>
        Task<InvoiceDetail> GetAsync(Guid invoiceId, CancellationToken cancellationToken);

        /// <exception cref="InvoiceStorageException">Hóa đơn hoặc phiên bản không tồn tại.</exception>
        Task<InvoiceXmlView> OpenXmlAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken);

        /// <summary>
        /// Đọc XML của một phiên bản thành mô hình để dựng PDF/in. Không dựng khi file mất, quá lớn, hỏng hoặc sai SHA-256 (tránh thể hiện sai chứng từ):
        /// khi đó <see cref="InvoicePrintResult.Problem"/> nói rõ lý do.
        /// </summary>
        /// <exception cref="InvoiceStorageException">Hóa đơn hoặc phiên bản không tồn tại.</exception>
        Task<InvoicePrintResult> PreparePrintAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken);

        /// <summary>Byte nguyên văn của file XML, không giới hạn cỡ: dùng cho "Lưu bản sao" khi file quá lớn để hiển thị.</summary>
        /// <exception cref="InvoiceStorageException">Hóa đơn, phiên bản hoặc file không còn.</exception>
        Task<byte[]> ReadBytesAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken);
    }

    public sealed class InvoiceDetailService : IInvoiceDetailService
    {
        /// <summary>Không nạp XML quá lớn vào bộ nhớ để hiển thị (DD-08 mục 4).</summary>
        public const long MaxDisplayBytes = 5L * 1024 * 1024;

        private readonly IInvoiceRepository _repository;
        private readonly IInvoiceFileStore _files;
        private readonly IInvoiceFileReader _reader;
        private readonly IDownloadHistoryService _history;
        private readonly ICompanyContextAccessor _context;

        public InvoiceDetailService(
            IInvoiceRepository repository,
            IInvoiceFileStore files,
            IInvoiceFileReader reader,
            IDownloadHistoryService history,
            ICompanyContextAccessor context)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (files == null) throw new ArgumentNullException("files");
            if (reader == null) throw new ArgumentNullException("reader");
            if (history == null) throw new ArgumentNullException("history");
            if (context == null) throw new ArgumentNullException("context");
            _repository = repository;
            _files = files;
            _reader = reader;
            _history = history;
            _context = context;
        }

        public async Task<InvoiceDetail> GetAsync(Guid invoiceId, CancellationToken cancellationToken)
        {
            var root = RequireRoot();
            var stored = await _repository.FindByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            if (stored == null) return null;

            var sessions = new Dictionary<Guid, HistoryRow>();
            var versions = new List<InvoiceVersionDetail>();
            foreach (var v in stored.Versions.OrderByDescending(x => x.VersionNo))
            {
                var detail = new InvoiceVersionDetail
                {
                    VersionNo = v.VersionNo,
                    CreatedAtUtc = v.CreatedAtUtc,
                    Sha256 = v.Sha256,
                    SizeBytes = v.SizeBytes,
                    IsCurrent = v.IsCurrent,
                    FileState = _files.Exists(root, v.RelativePath) ? VersionFileState.Ok : VersionFileState.Missing,
                    SessionId = v.SourceSessionId
                };

                HistoryRow session;
                if (!sessions.TryGetValue(v.SourceSessionId, out session))
                {
                    session = await _history.FindAsync(v.SourceSessionId, cancellationToken).ConfigureAwait(false);
                    sessions[v.SourceSessionId] = session;
                }

                if (session != null)
                {
                    detail.SessionCode = session.Code;
                    detail.SessionPeriod = session.PeriodText;
                    detail.SessionCreatedAtUtc = session.CreatedAtUtc;
                }

                versions.Add(detail);
            }

            return new InvoiceDetail
            {
                InvoiceId = stored.InvoiceId,
                Invoice = stored.Invoice,
                FirstSeenAtUtc = stored.FirstSeenAtUtc,
                LastSeenAtUtc = stored.LastSeenAtUtc,
                Versions = versions
            };
        }

        public async Task<InvoiceXmlView> OpenXmlAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken)
        {
            var root = RequireRoot();
            var stored = await _repository.FindByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            if (stored == null) throw new InvoiceStorageException("Hóa đơn không còn trong kho.");
            var version = stored.Versions.FirstOrDefault(v => v.VersionNo == versionNo);
            if (version == null) throw new InvoiceStorageException("Không có phiên bản " + versionNo + " của hóa đơn này.");

            var view = new InvoiceXmlView { InvoiceId = invoiceId, VersionNo = versionNo, Sha256 = version.Sha256, SizeBytes = version.SizeBytes };

            byte[] bytes;
            try
            {
                bytes = _reader.Read(root, version.RelativePath, MaxDisplayBytes);
            }
            catch (FileNotFoundException)
            {
                view.Problem = "File XML của phiên bản " + versionNo + " không còn trong thư mục kho.";
                return view;
            }
            catch (DirectoryNotFoundException)
            {
                view.Problem = "File XML của phiên bản " + versionNo + " không còn trong thư mục kho.";
                return view;
            }
            catch (InvoiceFileTooLargeException)
            {
                view.TooLarge = true;
                view.Problem = "File XML quá lớn để hiển thị (giới hạn " + MaxDisplayBytes / (1024 * 1024) + " MB). Hãy lưu bản sao để mở bằng ứng dụng khác.";
                return view;
            }

            view.Bytes = bytes;
            view.SizeBytes = bytes.LongLength;
            view.RawText = Decode(bytes);
            view.HashMatches = string.Equals(Hex(bytes), version.Sha256, StringComparison.OrdinalIgnoreCase);

            try
            {
                view.Tree = XmlTreeBuilder.Build(bytes);
            }
            catch (XmlException)
            {
                view.Problem = "XML không đọc được (file hỏng hoặc không đúng định dạng); chỉ xem được nội dung thô.";
                return view;
            }

            if (!view.HashMatches) view.Problem = "Nội dung file khác với lúc tải về (sai SHA-256). Hãy tải lại hóa đơn để thay thế file này.";
            return view;
        }

        public async Task<InvoicePrintResult> PreparePrintAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken)
        {
            var view = await OpenXmlAsync(invoiceId, versionNo, cancellationToken).ConfigureAwait(false);
            if (view.Bytes == null)
            {
                return new InvoicePrintResult { Problem = view.TooLarge ? "File XML quá lớn để dựng bản PDF/in." : view.Problem ?? "Không đọc được file XML." };
            }

            if (!view.HashMatches)
            {
                return new InvoicePrintResult { Problem = "Nội dung file XML khác với lúc tải về (sai SHA-256) nên không dựng PDF/in để tránh thể hiện sai chứng từ. Hãy tải lại hóa đơn." };
            }

            try
            {
                return new InvoicePrintResult { Model = InvoicePrintModelParser.Parse(view.Bytes) };
            }
            catch (InvoicePrintException ex)
            {
                return new InvoicePrintResult { Problem = ex.Message };
            }
        }

        public async Task<byte[]> ReadBytesAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken)
        {
            var root = RequireRoot();
            var stored = await _repository.FindByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            if (stored == null) throw new InvoiceStorageException("Hóa đơn không còn trong kho.");
            var version = stored.Versions.FirstOrDefault(v => v.VersionNo == versionNo);
            if (version == null) throw new InvoiceStorageException("Không có phiên bản " + versionNo + " của hóa đơn này.");
            try
            {
                return _reader.Read(root, version.RelativePath, long.MaxValue);
            }
            catch (FileNotFoundException)
            {
                throw new InvoiceStorageException("File XML của phiên bản " + versionNo + " không còn trong thư mục kho.");
            }
            catch (DirectoryNotFoundException)
            {
                throw new InvoiceStorageException("File XML của phiên bản " + versionNo + " không còn trong thư mục kho.");
            }
        }

        private string RequireRoot()
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            return _context.Current.StorageRoot;
        }

        private static string Decode(byte[] bytes)
        {
            // BOM UTF-8 không hiển thị; nội dung còn lại giữ nguyên.
            var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
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

    /// <summary>
    /// Dựng cây phần tử để hiển thị. Chỉ đọc bằng XmlReader không xử lý DTD và không resolve tài nguyên ngoài (NFR-SEC-003);
    /// giới hạn độ sâu và số nút để XML độc hại không làm treo hoặc cạn bộ nhớ.
    /// </summary>
    public static class XmlTreeBuilder
    {
        public const int MaxDepth = 64;
        public const int MaxNodes = 50000;

        /// <exception cref="XmlException">XML không hợp lệ hoặc chứa DTD.</exception>
        public static XmlTreeNode Build(byte[] content)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true, IgnoreProcessingInstructions = true };
            using (var reader = XmlReader.Create(new MemoryStream(content), settings))
            {
                var count = 0;
                XmlTreeNode root = null;
                var stack = new Stack<XmlTreeNode>();
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                        {
                            if (++count > MaxNodes) throw new XmlException("XML có quá nhiều phần tử để hiển thị.");
                            if (stack.Count >= MaxDepth) throw new XmlException("XML lồng quá sâu để hiển thị.");
                            var node = new XmlTreeNode { Name = reader.Name, Attributes = ReadAttributes(reader) };
                            if (stack.Count > 0) stack.Peek().Children.Add(node);
                            else root = node;
                            if (!reader.IsEmptyElement) stack.Push(node);
                            break;
                        }

                        case XmlNodeType.Text:
                        case XmlNodeType.CDATA:
                            if (stack.Count > 0) stack.Peek().Value = (stack.Peek().Value ?? string.Empty) + reader.Value;
                            break;

                        case XmlNodeType.EndElement:
                            if (stack.Count > 0) stack.Pop();
                            break;
                    }
                }

                if (root == null) throw new XmlException("XML không có phần tử gốc.");
                return root;
            }
        }

        private static string ReadAttributes(XmlReader reader)
        {
            if (!reader.HasAttributes) return null;
            var parts = new List<string>();
            while (reader.MoveToNextAttribute()) parts.Add(reader.Name + "=\"" + reader.Value + "\"");
            reader.MoveToElement();
            return string.Join(" ", parts);
        }
    }
}
