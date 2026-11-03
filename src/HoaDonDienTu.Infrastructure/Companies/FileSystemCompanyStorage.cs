using System;
using System.IO;
using System.Data.SQLite;
using System.Linq;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Persistence;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Companies
{
    /// <summary>
    /// Kho của một MST trên đĩa. File <c>company.identity.json</c> ghi mã doanh nghiệp + MST để không mở nhầm kho của MST khác
    /// và để liên kết lại được kho đã ngừng quản lý. company.db và các thư mục XML/PDF do DD-06 thêm vào cùng thư mục.
    /// </summary>
    public sealed class FileSystemCompanyStorage : ICompanyStorage
    {
        public const string IdentityFileName = "company.identity.json";

        private readonly AtomicJsonWriter _writer = new AtomicJsonWriter();

        public StorageInspection Inspect(string path)
        {
            var result = new StorageInspection { Exists = Directory.Exists(path) };
            result.IsEmpty = !result.Exists || !Directory.EnumerateFileSystemEntries(path).Any();

            string problem;
            result.Writable = CheckWritable(path, out problem);
            result.WriteProblem = problem;
            result.FreeBytes = FreeBytes(path);

            if (result.Exists)
            {
                ReadIdentity(path, result);
                result.DatabaseProblem = CheckDatabase(Path.Combine(path, CompanyDatabaseProvider.RelativePath));
            }

            return result;
        }

        private static string CheckDatabase(string file)
        {
            if (!File.Exists(file)) return null;
            try
            {
                var builder = new SQLiteConnectionStringBuilder { DataSource = file, Version = 3, ReadOnly = true, Pooling = false };
                using (var connection = new SQLiteConnection(builder.ConnectionString))
                {
                    connection.Open();
                    var version = SchemaVersionReader.Read(connection);
                    return version > CompanyDatabaseSchema.Migrations.Count
                        ? "cơ sở dữ liệu được tạo bởi phiên bản ứng dụng mới hơn, hãy cập nhật ứng dụng"
                        : null;
                }
            }
            catch (SQLiteException ex)
            {
                // Chỉ báo khi chắc file hỏng; lỗi khác (đang bị khóa, đĩa chỉ đọc không có -shm) để lần dùng đầu xử lý.
                return ex.ResultCode == SQLiteErrorCode.NotADb || ex.ResultCode == SQLiteErrorCode.Corrupt
                    ? "file cơ sở dữ liệu company.db bị hỏng"
                    : null;
            }
        }

        public StorageInitResult Initialize(string path, CompanyId companyId, TaxCode taxCode)
        {
            var identityPath = Path.Combine(path, IdentityFileName);
            if (File.Exists(identityPath) || File.Exists(identityPath + AtomicJsonWriter.BackupSuffix))
            {
                throw new InvalidOperationException("Thư mục đã có file định danh kho; không ghi đè dữ liệu hiện hữu.");
            }

            var createdDirectory = !Directory.Exists(path);
            Directory.CreateDirectory(path);

            var identity = new JObject
            {
                { "schemaVersion", 1 },
                { "companyId", companyId.ToString() },
                { "taxCode", taxCode.Value }
            };
            _writer.Write(identityPath, identity.ToString(Newtonsoft.Json.Formatting.Indented), IsValidIdentity);
            return new StorageInitResult(createdDirectory, true);
        }

        public void Rollback(string path, StorageInitResult result)
        {
            if (result == null) return;
            try
            {
                if (result.CreatedIdentity)
                {
                    var identityPath = Path.Combine(path, IdentityFileName);
                    foreach (var file in new[] { identityPath, identityPath + AtomicJsonWriter.BackupSuffix, identityPath + AtomicJsonWriter.TempSuffix })
                    {
                        if (File.Exists(file)) File.Delete(file);
                    }
                }

                // Chỉ xóa thư mục nếu chính lần khởi tạo này tạo ra nó và nó vẫn rỗng.
                if (result.CreatedDirectory && Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void ReadIdentity(string path, StorageInspection result)
        {
            var read = _writer.Read(Path.Combine(path, IdentityFileName), IsValidIdentity);
            switch (read.Status)
            {
                case AtomicReadStatus.NotFound:
                    return;

                case AtomicReadStatus.Corrupt:
                    result.IdentityProblem = "file định danh bị hỏng";
                    return;

                default:
                    var json = JObject.Parse(read.Content);
                    result.HasIdentity = true;
                    result.IdentityTaxCode = TaxCode.Parse((string)json["taxCode"]);
                    result.IdentityCompanyId = new CompanyId(Guid.Parse((string)json["companyId"]));
                    return;
            }
        }

        private static bool IsValidIdentity(string text)
        {
            var json = JObject.Parse(text);
            Guid id;
            TaxCode tax;
            return (int?)json["schemaVersion"] == 1
                && Guid.TryParse((string)json["companyId"], out id)
                && TaxCode.TryParse((string)json["taxCode"], out tax);
        }

        private static bool CheckWritable(string path, out string problem)
        {
            problem = null;
            try
            {
                // Thư mục chưa có thì thử trên thư mục cha gần nhất đang tồn tại (nơi sẽ tạo thư mục kho).
                var probeDir = path;
                while (!string.IsNullOrEmpty(probeDir) && !Directory.Exists(probeDir)) probeDir = Path.GetDirectoryName(probeDir);
                if (string.IsNullOrEmpty(probeDir))
                {
                    problem = "ổ đĩa hoặc thư mục cha không tồn tại";
                    return false;
                }

                var probe = Path.Combine(probeDir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                problem = "không có quyền ghi";
                return false;
            }
            catch (IOException)
            {
                problem = "không ghi được";
                return false;
            }
            catch (ArgumentException)
            {
                problem = "đường dẫn không hợp lệ";
                return false;
            }
        }

        private static long? FreeBytes(string path)
        {
            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal)) return null;
                return new DriveInfo(root).AvailableFreeSpace;
            }
            catch (IOException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
