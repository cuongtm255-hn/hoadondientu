using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Persistence;
using Newtonsoft.Json;

namespace HoaDonDienTu.Infrastructure.Licensing
{
    /// <summary>
    /// <c>license.dat</c>: JSON được DPAPI LocalMachine mã hóa, lưu dạng base64, ghi atomic (DD-03 mục 5.1).
    /// DPAPI chỉ chống đọc/sửa tùy tiện; tính xác thực dựa vào chữ ký của server trong grant.
    /// </summary>
    public sealed class FileLicenseStore : ILicenseStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HoaDonDienTu.license.v1");

        private readonly string _path;
        private readonly AtomicJsonWriter _writer = new AtomicJsonWriter();

        public FileLicenseStore(string path)
        {
            _path = path;
        }

        public string FilePath
        {
            get { return _path; }
        }

        public LicenseLoadResult Load()
        {
            var read = _writer.Read(_path, content => TryDecode(content) != null);
            switch (read.Status)
            {
                case AtomicReadStatus.NotFound:
                    return new LicenseLoadResult(LicenseLoadStatus.NotFound, null);
                case AtomicReadStatus.Corrupt:
                    return new LicenseLoadResult(LicenseLoadStatus.Corrupt, null);
                default:
                    return new LicenseLoadResult(LicenseLoadStatus.Ok, TryDecode(read.Content));
            }
        }

        public void Save(StoredLicense license)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _writer.Write(_path, Encode(license), content => TryDecode(content) != null);
        }

        public void Delete()
        {
            foreach (var suffix in new[] { string.Empty, AtomicJsonWriter.BackupSuffix, AtomicJsonWriter.TempSuffix, AtomicJsonWriter.CorruptSuffix })
            {
                var file = _path + suffix;
                if (File.Exists(file)) File.Delete(file);
            }
        }

        private static string Encode(StoredLicense license)
        {
            var dto = new Dto
            {
                SchemaVersion = 1,
                LicenseKey = license.LicenseKey,
                LicenseType = license.Grant.LicenseType,
                MachineIdHash = license.Grant.MachineIdHash,
                IssuedAtUtc = license.Grant.IssuedAtUtc,
                ExpiresAtUtc = license.Grant.ExpiresAtUtc,
                Features = license.Grant.Features.Select(f => f.ToString()).ToList(),
                Signature = license.Signature,
                LastOnlineValidationUtc = license.LastOnlineValidationUtc,
                LastSeenUtc = license.LastSeenUtc,
                Revoked = license.Revoked
            };
            var plain = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto));
            return Convert.ToBase64String(ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine));
        }

        private static StoredLicense TryDecode(string content)
        {
            try
            {
                var plain = ProtectedData.Unprotect(Convert.FromBase64String(content.Trim()), Entropy, DataProtectionScope.LocalMachine);
                var dto = JsonConvert.DeserializeObject<Dto>(Encoding.UTF8.GetString(plain));
                if (dto == null || dto.SchemaVersion != 1 || string.IsNullOrEmpty(dto.LicenseKey)) return null;

                var features = new List<AppFeature>();
                foreach (var name in dto.Features ?? new List<string>())
                {
                    AppFeature feature;
                    if (Enum.TryParse(name, out feature)) features.Add(feature);
                }

                var grant = new LicenseGrant(dto.LicenseType, dto.MachineIdHash, AsUtc(dto.IssuedAtUtc), AsUtc(dto.ExpiresAtUtc), features);
                return new StoredLicense(
                    dto.LicenseKey,
                    grant,
                    dto.Signature,
                    AsUtc(dto.LastOnlineValidationUtc),
                    AsUtc(dto.LastSeenUtc),
                    dto.Revoked);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DateTime AsUtc(DateTime value)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime? AsUtc(DateTime? value)
        {
            return value.HasValue ? AsUtc(value.Value) : (DateTime?)null;
        }

        private sealed class Dto
        {
            public int SchemaVersion { get; set; }
            public string LicenseKey { get; set; }
            public string LicenseType { get; set; }
            public string MachineIdHash { get; set; }
            public DateTime IssuedAtUtc { get; set; }
            public DateTime? ExpiresAtUtc { get; set; }
            public List<string> Features { get; set; }
            public string Signature { get; set; }
            public DateTime LastOnlineValidationUtc { get; set; }
            public DateTime LastSeenUtc { get; set; }
            public bool Revoked { get; set; }
        }
    }
}
