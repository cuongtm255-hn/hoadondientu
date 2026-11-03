using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Backup
{
    /// <summary>Lỗi sao lưu/khôi phục có thông báo tiếng Việt an toàn để hiển thị cho người dùng.</summary>
    public sealed class BackupException : Exception
    {
        public BackupException(string message)
            : base(message)
        {
        }

        public BackupException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    [DataContract]
    public sealed class BackupFileEntry
    {
        /// <summary>Đường dẫn tương đối so với thư mục kho, dùng dấu "/".</summary>
        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "sha256")]
        public string Sha256 { get; set; }

        [DataMember(Name = "size")]
        public long Size { get; set; }
    }

    /// <summary>manifest.json trong gói sao lưu (FR-SET-006). Một gói là một MST.</summary>
    [DataContract]
    public sealed class BackupManifest
    {
        /// <summary>2: thêm bản chụp <c>company.db</c> (<see cref="Database"/>); định dạng 1 không có cơ sở dữ liệu vẫn đọc được.</summary>
        public const int CurrentFormat = 2;

        [DataMember(Name = "formatVersion")]
        public int FormatVersion { get; set; }

        [DataMember(Name = "taxCode")]
        public string TaxCode { get; set; }

        [DataMember(Name = "companyName")]
        public string CompanyName { get; set; }

        [DataMember(Name = "createdAtUtc")]
        public string CreatedAtUtcText { get; set; }

        [DataMember(Name = "appVersion")]
        public string AppVersion { get; set; }

        /// <summary>Gói có bản chụp <c>company.db</c> (định dạng 2); gói cũ chỉ có XML/PDF nên chỉ mục cần dựng lại sau khi khôi phục.</summary>
        [DataMember(Name = "includesDatabase")]
        public bool IncludesDatabase { get; set; }

        /// <summary>Bản chụp <c>company.db</c> (đường dẫn trong gói là <see cref="BackupService.DatabaseEntry"/>); null khi không có.</summary>
        [DataMember(Name = "database")]
        public BackupFileEntry Database { get; set; }

        /// <summary>Phiên bản schema của bản chụp cơ sở dữ liệu; 0 khi gói không có.</summary>
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion { get; set; }

        [DataMember(Name = "files")]
        public List<BackupFileEntry> Files { get; set; }

        public DateTime? CreatedAtUtc
        {
            get
            {
                DateTime value;
                return DateTime.TryParse(CreatedAtUtcText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value) ? (DateTime?)value : null;
            }
        }
    }

    public sealed class BackupProgress
    {
        public BackupProgress(int done, int total)
        {
            Done = done;
            Total = total;
        }

        public int Done { get; private set; }
        public int Total { get; private set; }
    }

    public sealed class BackupResult
    {
        public string Path { get; set; }
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
    }

    public sealed class BackupInspection
    {
        public BackupManifest Manifest { get; set; }

        /// <summary>Rỗng khi gói hợp lệ và toàn vẹn.</summary>
        public IList<string> Problems { get; set; }

        public bool IsValid
        {
            get { return Manifest != null && Problems.Count == 0; }
        }
    }

    public sealed class RestoreResult
    {
        public int Created { get; set; }
        public int Overwritten { get; set; }
        public int Unchanged { get; set; }

        /// <summary>Gói có company.db và đã được thay vào kho (chỉ mục, lịch sử tải khớp với bản sao lưu).</summary>
        public bool DatabaseRestored { get; set; }

        /// <summary>Gói sao lưu tự động dữ liệu hiện hành trước khi khôi phục; null nếu kho chưa có file nào.</summary>
        public string SafetyBackupPath { get; set; }

        public int Restored
        {
            get { return Created + Overwritten; }
        }
    }

    public interface IBackupService
    {
        /// <exception cref="BackupException"/>
        /// <exception cref="EntitlementDeniedException"/>
        Task<BackupResult> CreateAsync(string targetPath, bool overwrite, IProgress<BackupProgress> progress, CancellationToken cancellationToken);

        /// <summary>Đọc manifest và kiểm SHA-256 từng file; không đổi gì trên đĩa.</summary>
        Task<BackupInspection> InspectAsync(string backupPath, CancellationToken cancellationToken);

        /// <exception cref="BackupException">Gói hỏng, khác MST, đang có phiên tải, hoặc lỗi giữa chừng (đã trả về trạng thái trước).</exception>
        Task<RestoreResult> RestoreAsync(string backupPath, IProgress<BackupProgress> progress, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Sao lưu/khôi phục thư mục kho của MST đang mở (DD-11). Gói là một zip: <c>manifest.json</c> + các file XML/PDF dưới <c>data/</c>.
    /// Khôi phục kiểm MST và SHA-256 trước khi đổi bất cứ thứ gì, tự sao lưu dữ liệu hiện có làm điểm an toàn, ghi từng file qua file tạm và
    /// quay lại điểm an toàn nếu lỗi hoặc bị hủy. Không xóa file nằm ngoài gói.
    /// </summary>
    public sealed class BackupService : IBackupService
    {
        public const string ManifestName = "manifest.json";
        public const string DataPrefix = "data/";
        public const string DatabaseEntry = "database/company.db";
        public const string BackupsFolder = "Backups";
        public const string ExportsFolder = "Exports";

        private const long MaxManifestBytes = 32L * 1024 * 1024;

        private static readonly string[] AllowedExtensions = { ".xml", ".pdf" };

        private readonly ICompanyContextAccessor _context;
        private readonly IActiveJobService _jobs;
        private readonly IEntitlementService _entitlement;
        private readonly Func<DateTime> _utcNow;
        private readonly string _appVersion;
        private readonly ICompanyDatabaseBackup _database;

        /// <param name="database">Null khi chưa có cơ sở dữ liệu: gói chỉ gồm XML/PDF (định dạng 1).</param>
        public BackupService(ICompanyContextAccessor context, IActiveJobService jobs, IEntitlementService entitlement, string appVersion, Func<DateTime> utcNow = null, ICompanyDatabaseBackup database = null)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (jobs == null) throw new ArgumentNullException("jobs");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            _context = context;
            _jobs = jobs;
            _entitlement = entitlement;
            _appVersion = appVersion ?? string.Empty;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _database = database;
        }

        // ---- Tạo gói ----

        public async Task<BackupResult> CreateAsync(string targetPath, bool overwrite, IProgress<BackupProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) throw new ArgumentException("Thiếu đường dẫn gói sao lưu.", "targetPath");
            _entitlement.Demand(AppFeature.ReadExport);
            var company = RequireCompany();
            await RequireNoRunningDownloadAsync(company, cancellationToken).ConfigureAwait(false);

            var target = Path.GetFullPath(targetPath);
            if (File.Exists(target) && !overwrite) throw new BackupException("File sao lưu đã tồn tại: " + target);
            return await CreateCoreAsync(company, target, progress, cancellationToken).ConfigureAwait(false);
        }

        private async Task<BackupResult> CreateCoreAsync(CompanyContext company, string target, IProgress<BackupProgress> progress, CancellationToken token)
        {
            var files = EnumerateStoreFiles(company.StorageRoot).ToList();
            if (files.Count == 0 && _database == null) throw new BackupException("Kho chưa có file XML/PDF nào để sao lưu.");

            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var snapshot = temp + ".db";
            try
            {
                var manifest = new BackupManifest
                {
                    FormatVersion = _database == null ? 1 : BackupManifest.CurrentFormat,
                    TaxCode = company.TaxCode.Value,
                    CompanyName = company.DisplayName,
                    CreatedAtUtcText = _utcNow().ToString("o", CultureInfo.InvariantCulture),
                    AppVersion = _appVersion,
                    IncludesDatabase = _database != null,
                    Files = new List<BackupFileEntry>()
                };

                // Bản chụp nhất quán của company.db được lấy trước khi mở gói để lỗi chụp không để lại gói dở.
                if (_database != null) manifest.SchemaVersion = await _database.SnapshotAsync(snapshot, token).ConfigureAwait(false);

                long total = 0;
                var steps = files.Count + (_database != null ? 1 : 0);
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    var done = 0;
                    if (_database != null)
                    {
                        token.ThrowIfCancellationRequested();
                        var dbEntry = zip.CreateEntry(DatabaseEntry, CompressionLevel.Optimal);
                        string sha;
                        long size;
                        using (var input = File.OpenRead(snapshot))
                        using (var output = dbEntry.Open())
                        {
                            CopyAndHash(input, output, out sha, out size);
                        }

                        manifest.Database = new BackupFileEntry { Path = "company.db", Sha256 = sha, Size = size };
                        total += size;
                        if (progress != null) progress.Report(new BackupProgress(++done, steps));
                    }

                    foreach (var relative in files)
                    {
                        token.ThrowIfCancellationRequested();
                        var bytes = File.ReadAllBytes(Path.Combine(company.StorageRoot, relative));
                        var name = relative.Replace('\\', '/');
                        var entry = zip.CreateEntry(DataPrefix + name, CompressionLevel.Optimal);
                        using (var s = entry.Open()) s.Write(bytes, 0, bytes.Length);
                        manifest.Files.Add(new BackupFileEntry { Path = name, Sha256 = Hex(bytes), Size = bytes.LongLength });
                        total += bytes.LongLength;
                        if (progress != null) progress.Report(new BackupProgress(++done, steps));
                    }

                    var manifestEntry = zip.CreateEntry(ManifestName, CompressionLevel.Optimal);
                    using (var s = manifestEntry.Open()) WriteManifest(s, manifest);
                }

                if (File.Exists(target)) File.Delete(target);
                File.Move(temp, target);
                return new BackupResult { Path = target, FileCount = files.Count, TotalBytes = total };
            }
            catch
            {
                DeleteQuietly(temp);
                throw;
            }
            finally
            {
                DeleteQuietly(snapshot);
            }
        }

        private static void CopyAndHash(Stream input, Stream output, out string sha256, out long size)
        {
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[81920];
                int read;
                size = 0;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    output.Write(buffer, 0, read);
                    size += read;
                }

                sha.TransformFinalBlock(buffer, 0, 0);
                sha256 = HexOf(sha.Hash);
            }
        }

        // ---- Kiểm tra gói ----

        public Task<BackupInspection> InspectAsync(string backupPath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Inspect(backupPath, cancellationToken));
        }

        private static BackupInspection Inspect(string backupPath, CancellationToken token)
        {
            var problems = new List<string>();
            BackupManifest manifest = null;
            try
            {
                using (var zip = ZipFile.OpenRead(backupPath))
                {
                    var manifestEntry = zip.GetEntry(ManifestName);
                    if (manifestEntry == null)
                    {
                        problems.Add("Gói không có manifest.json: không phải gói sao lưu của ứng dụng.");
                        return new BackupInspection { Problems = problems };
                    }

                    if (manifestEntry.Length > MaxManifestBytes)
                    {
                        problems.Add("manifest.json quá lớn.");
                        return new BackupInspection { Problems = problems };
                    }

                    using (var s = manifestEntry.Open())
                    {
                        try
                        {
                            manifest = (BackupManifest)new DataContractJsonSerializer(typeof(BackupManifest)).ReadObject(s);
                        }
                        catch (SerializationException)
                        {
                            problems.Add("manifest.json không đọc được.");
                            return new BackupInspection { Problems = problems };
                        }
                    }

                    if (manifest == null || manifest.Files == null || string.IsNullOrEmpty(manifest.TaxCode))
                    {
                        problems.Add("manifest.json thiếu thông tin bắt buộc.");
                        return new BackupInspection { Problems = problems };
                    }

                    if (manifest.FormatVersion > BackupManifest.CurrentFormat)
                    {
                        problems.Add("Gói được tạo bởi phiên bản ứng dụng mới hơn (định dạng " + manifest.FormatVersion + "); hãy cập nhật ứng dụng.");
                        return new BackupInspection { Manifest = manifest, Problems = problems };
                    }

                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var file in manifest.Files)
                    {
                        token.ThrowIfCancellationRequested();
                        var unsafeReason = UnsafePath(file.Path);
                        if (unsafeReason != null)
                        {
                            problems.Add("Đường dẫn không hợp lệ trong gói (" + unsafeReason + "): " + file.Path);
                            continue;
                        }

                        if (!seen.Add(file.Path))
                        {
                            problems.Add("Gói liệt kê trùng file: " + file.Path);
                            continue;
                        }

                        var entry = zip.GetEntry(DataPrefix + file.Path);
                        if (entry == null)
                        {
                            problems.Add("Thiếu file trong gói: " + file.Path);
                            continue;
                        }

                        using (var s = entry.Open())
                        using (var sha = SHA256.Create())
                        {
                            var actual = HexOf(sha.ComputeHash(s));
                            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase)) problems.Add("Sai SHA-256: " + file.Path);
                        }
                    }

                    VerifyDatabaseEntry(zip, manifest, problems);
                }
            }
            catch (InvalidDataException)
            {
                problems.Add("File không phải gói zip hợp lệ hoặc bị hỏng.");
            }
            catch (FileNotFoundException)
            {
                problems.Add("Không tìm thấy file sao lưu.");
            }

            return new BackupInspection { Manifest = manifest, Problems = problems };
        }

        private static void VerifyDatabaseEntry(ZipArchive zip, BackupManifest manifest, IList<string> problems)
        {
            if (manifest.IncludesDatabase != (manifest.Database != null))
            {
                problems.Add("manifest.json mô tả cơ sở dữ liệu không nhất quán.");
                return;
            }

            if (manifest.Database == null) return;
            var entry = zip.GetEntry(DatabaseEntry);
            if (entry == null)
            {
                problems.Add("Thiếu cơ sở dữ liệu trong gói.");
                return;
            }

            using (var s = entry.Open())
            using (var sha = SHA256.Create())
            {
                if (!string.Equals(HexOf(sha.ComputeHash(s)), manifest.Database.Sha256, StringComparison.OrdinalIgnoreCase)) problems.Add("Sai SHA-256: cơ sở dữ liệu.");
            }
        }

        // ---- Khôi phục ----

        public async Task<RestoreResult> RestoreAsync(string backupPath, IProgress<BackupProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("Thiếu đường dẫn gói sao lưu.", "backupPath");
            _entitlement.Demand(AppFeature.ManageSettings);
            var company = RequireCompany();
            await RequireNoRunningDownloadAsync(company, cancellationToken).ConfigureAwait(false);

            // Mọi kiểm tra diễn ra TRƯỚC khi đụng vào dữ liệu hiện hành.
            var inspection = Inspect(backupPath, cancellationToken);
            if (!inspection.IsValid)
            {
                var first = inspection.Problems.First();
                throw new BackupException("Không thể khôi phục: " + first + (inspection.Problems.Count > 1 ? " (và " + (inspection.Problems.Count - 1) + " lỗi khác)" : string.Empty));
            }

            if (!string.Equals(inspection.Manifest.TaxCode, company.TaxCode.Value, StringComparison.Ordinal))
            {
                throw new BackupException("Gói sao lưu thuộc MST " + inspection.Manifest.TaxCode + ", không phải MST " + company.TaxCode.Value
                    + " đang mở. Hãy chuyển sang doanh nghiệp đúng rồi thử lại.");
            }

            var manifest = inspection.Manifest;
            if (manifest.Database != null)
            {
                if (_database == null) throw new BackupException("Gói có cơ sở dữ liệu nhưng ứng dụng này chưa khôi phục được phần đó.");
                if (manifest.SchemaVersion > _database.LatestSchemaVersion)
                {
                    throw new BackupException("Cơ sở dữ liệu trong gói có phiên bản schema " + manifest.SchemaVersion + " mới hơn ứng dụng; hãy cập nhật ứng dụng.");
                }
            }

            var databaseStage = manifest.Database == null ? null : Path.Combine(company.StorageRoot, BackupsFolder, Guid.NewGuid().ToString("N") + ".db.tmp");
            try
            {
                if (databaseStage != null)
                {
                    // Kiểm cơ sở dữ liệu trong gói TRƯỚC khi đụng vào dữ liệu hiện hành.
                    ExtractEntry(backupPath, DatabaseEntry, databaseStage);
                    await _database.ValidateAsync(databaseStage, cancellationToken).ConfigureAwait(false);
                }

                return await RestoreCoreAsync(company, backupPath, inspection, databaseStage, progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (databaseStage != null) DeleteQuietly(databaseStage);
            }
        }

        private static void ExtractEntry(string backupPath, string entryName, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (var zip = ZipFile.OpenRead(backupPath))
            using (var input = zip.GetEntry(entryName).Open())
            using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }
        }

        private async Task<RestoreResult> RestoreCoreAsync(
            CompanyContext company, string backupPath, BackupInspection inspection, string databaseStage, IProgress<BackupProgress> progress, CancellationToken cancellationToken)
        {
            var result = new RestoreResult();
            var existing = _database != null || EnumerateStoreFiles(company.StorageRoot).Any();
            if (existing)
            {
                var safety = UniqueSafetyPath(company);
                result.SafetyBackupPath = (await CreateCoreAsync(company, safety, null, cancellationToken).ConfigureAwait(false)).Path;
            }

            var created = new List<string>();
            var overwritten = new List<string>();
            var databaseReplaced = false;
            try
            {
                using (var zip = ZipFile.OpenRead(backupPath))
                {
                    var done = 0;
                    foreach (var file in inspection.Manifest.Files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var target = Path.Combine(company.StorageRoot, file.Path.Replace('/', Path.DirectorySeparatorChar));
                        byte[] bytes;
                        using (var s = zip.GetEntry(DataPrefix + file.Path).Open())
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            bytes = ms.ToArray();
                        }

                        // Byte đọc ở lượt ghi phải khớp manifest: gói có thể đổi giữa lúc kiểm tra và lúc ghi.
                        if (!string.Equals(Hex(bytes), file.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException("Sai SHA-256 khi ghi: " + file.Path);
                        }

                        if (File.Exists(target))
                        {
                            if (string.Equals(Hex(File.ReadAllBytes(target)), file.Sha256, StringComparison.OrdinalIgnoreCase)) result.Unchanged++;
                            else
                            {
                                Replace(target, bytes);
                                overwritten.Add(file.Path);
                                result.Overwritten++;
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            Replace(target, bytes);
                            created.Add(file.Path);
                            result.Created++;
                        }

                        if (progress != null) progress.Report(new BackupProgress(++done, inspection.Manifest.Files.Count));
                    }
                }

                if (databaseStage != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _database.ReplaceAsync(databaseStage, cancellationToken).ConfigureAwait(false);
                    databaseReplaced = true;
                    result.DatabaseRestored = true;
                }
            }
            catch (Exception ex)
            {
                var rolledBack = await TryRollbackAsync(company, created, overwritten, result.SafetyBackupPath, databaseReplaced).ConfigureAwait(false);
                if (ex is OperationCanceledException) throw;
                throw new BackupException(
                    "Khôi phục bị lỗi giữa chừng: " + ex.Message + (rolledBack ? " Dữ liệu đã được trả về trạng thái trước khi khôi phục." : " Không trả về được trạng thái cũ hoàn toàn; hãy dùng điểm sao lưu an toàn: " + result.SafetyBackupPath),
                    ex);
            }

            return result;
        }

        /// <summary>Tên điểm an toàn không bao giờ trùng một điểm đã có, kể cả hai lần khôi phục trong cùng một giây.</summary>
        private string UniqueSafetyPath(CompanyContext company)
        {
            var stamp = _utcNow().ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var baseName = Path.Combine(company.StorageRoot, BackupsFolder, "truoc-khoi-phuc_" + company.TaxCode.Value + "_" + stamp);
            var candidate = baseName + ".zip";
            for (var n = 2; File.Exists(candidate); n++) candidate = baseName + "_" + n.ToString(CultureInfo.InvariantCulture) + ".zip";
            return candidate;
        }

        /// <summary>Ghi qua file tạm cùng thư mục rồi đổi tên, nên file đích không bao giờ ở trạng thái ghi dở.</summary>
        private static void Replace(string target, byte[] bytes)
        {
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                // File.Replace đổi file đích trong một bước nên không có lúc nào thiếu file nếu mất điện giữa chừng.
                if (File.Exists(target)) File.Replace(temp, target, null);
                else File.Move(temp, target);
            }
            catch
            {
                DeleteQuietly(temp);
                throw;
            }
        }

        private async Task<bool> TryRollbackAsync(CompanyContext company, IList<string> created, IList<string> overwritten, string safetyPath, bool databaseReplaced)
        {
            var ok = true;
            foreach (var path in created)
            {
                try
                {
                    DeleteQuietly(Path.Combine(company.StorageRoot, path.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch (Exception)
                {
                    ok = false;
                }
            }

            if (overwritten.Count == 0 && !databaseReplaced) return ok;
            if (string.IsNullOrEmpty(safetyPath) || !File.Exists(safetyPath)) return false;
            try
            {
                using (var zip = ZipFile.OpenRead(safetyPath))
                {
                    foreach (var path in overwritten)
                    {
                        var entry = zip.GetEntry(DataPrefix + path);
                        if (entry == null)
                        {
                            ok = false;
                            continue;
                        }

                        using (var s = entry.Open())
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            Replace(Path.Combine(company.StorageRoot, path.Replace('/', Path.DirectorySeparatorChar)), ms.ToArray());
                        }
                    }
                }

                if (databaseReplaced)
                {
                    var stage = Path.Combine(company.StorageRoot, BackupsFolder, Guid.NewGuid().ToString("N") + ".db.tmp");
                    try
                    {
                        ExtractEntry(safetyPath, DatabaseEntry, stage);
                        await _database.ReplaceAsync(stage, CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        DeleteQuietly(stage);
                    }
                }
            }
            catch (Exception)
            {
                ok = false;
            }

            return ok;
        }

        // ---- Chung ----

        /// <summary>File XML/PDF trong kho (đường dẫn tương đối), bỏ Exports, Backups và mọi file tạm.</summary>
        public static IEnumerable<string> EnumerateStoreFiles(string root)
        {
            if (!Directory.Exists(root)) yield break;
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(file);
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var relative = full.Substring(prefix.Length);
                var first = relative.Split(Path.DirectorySeparatorChar)[0];
                if (string.Equals(first, BackupsFolder, StringComparison.OrdinalIgnoreCase) || string.Equals(first, ExportsFolder, StringComparison.OrdinalIgnoreCase)) continue;
                if (Array.IndexOf(AllowedExtensions, Path.GetExtension(relative).ToLowerInvariant()) < 0) continue;
                yield return relative;
            }
        }

        /// <summary>Chặn đường dẫn trong gói trỏ ra ngoài thư mục kho hoặc không phải loại file được phép; null nếu hợp lệ.</summary>
        private static string UnsafePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "rỗng";
            if (path.IndexOf('\\') >= 0) return "dùng dấu \\";
            if (path.StartsWith("/", StringComparison.Ordinal) || (path.Length > 1 && path[1] == ':')) return "đường dẫn tuyệt đối";
            if (path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0)) return "chứa .. hoặc đoạn rỗng";
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return "ký tự không hợp lệ";
            var first = path.Split('/')[0];
            if (string.Equals(first, BackupsFolder, StringComparison.OrdinalIgnoreCase) || string.Equals(first, ExportsFolder, StringComparison.OrdinalIgnoreCase)) return "thư mục dành riêng";
            if (Array.IndexOf(AllowedExtensions, Path.GetExtension(path).ToLowerInvariant()) < 0) return "loại file không được phép";
            return null;
        }

        private CompanyContext RequireCompany()
        {
            if (!_context.HasCurrent) throw new BackupException("Chưa mở doanh nghiệp nào.");
            return _context.Current;
        }

        private async Task RequireNoRunningDownloadAsync(CompanyContext company, CancellationToken token)
        {
            if (await _jobs.HasRunningDownloadAsync(company.CompanyId, token).ConfigureAwait(false))
            {
                throw new BackupException("Đang có phiên tải chạy cho doanh nghiệp này. Hãy tạm dừng hoặc chờ phiên tải xong rồi thử lại.");
            }
        }

        private static void WriteManifest(Stream stream, BackupManifest manifest)
        {
            new DataContractJsonSerializer(typeof(BackupManifest)).WriteObject(stream, manifest);
        }

        private static void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
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
            using (var sha = SHA256.Create()) return HexOf(sha.ComputeHash(content));
        }

        private static string HexOf(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
