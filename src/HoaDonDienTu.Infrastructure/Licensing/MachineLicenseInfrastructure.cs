using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using HoaDonDienTu.Application.Licensing;
using Microsoft.Win32;

namespace HoaDonDienTu.Infrastructure.Licensing
{
    /// <summary>
    /// Sản phẩm và khóa công khai để kiểm mã kích hoạt. Mã do máy chủ cdsoft-sale-management cấp
    /// (<c>POST platform/tenants/:id/machines</c>), ký bằng khóa bí mật của máy chủ; khóa bí mật không bao giờ nằm trong repo này.
    /// </summary>
    public static class LicenseConstants
    {
        /// <summary>Mã sản phẩm trong payload do máy chủ cấp (máy chủ cố định giá trị này).</summary>
        public const string ProductCode = "CDSOFT_EZACC";

        /// <summary>Định dạng mã kích hoạt: <c>EZACC1|&lt;payload base64url&gt;|&lt;chữ ký base64url&gt;</c>.</summary>
        public const string KeyPrefix = "EZACC1";

        /// <summary>Chuỗi đưa vào công thức băm mã máy. Tách khỏi <see cref="ProductCode"/> để mã máy không đổi khi đổi sản phẩm cấp mã.</summary>
        public const string FingerprintProduct = "HDDT";

        /// <summary>Khóa công khai RSA 2048 (tạo 08/11/2026) khớp khóa bí mật <c>EZACC_LICENSE_PRIVATE_KEY</c> của máy chủ cấp mã.</summary>
        public const string PublicKeyXml = "<RSAKeyValue><Modulus>1TQicbUI4Gs3vv1J4VH8Ye1xqrOOKCNIhBeHs8AN/cuGE9hP1Bh6cs7DvWBcioZKXmlliDR5HiKquF8lL5nGuCDGja3AdX8sPziubuGSJZDoNFsvesZFLv/8JwM/nVbUtxXFYfCLGYRWRuhw5J6WSDRQqnJkyO6lEfXBtLAxXfaG/VtebwV3xo/U6o5c8AdSZWfiz6/bbY1ZjPr1m+xFQ9UCz5gZ+3SYo52z0LT0rZG9HmLv96i4EN33zruQ46MqLVFpGgDd41Y+/DgBcPZbH4IkW0xXhMh4+/TLBTiJriy5TjL0we+HTpEZdD0KDgj4rcr0+I82hK7qzzE0l2HCzQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }

    /// <summary>
    /// Mã máy: băm SHA-256 của MachineGuid của Windows và số sê-ri ổ hệ thống. Cố ý không dùng tên máy, phiên bản hệ điều hành hay số CPU:
    /// đổi tên máy hoặc nâng cấp Windows không được làm mất hiệu lực mã kích hoạt đã mua.
    /// </summary>
    public sealed class MachineCodeProvider : IMachineCodeProvider
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            int nFileSystemNameSize);

        public string GetMachineCode()
        {
            return Compute(GetMachineGuid(), GetSystemDriveSerial());
        }

        /// <summary>Tách ra để thử nghiệm: cùng đầu vào luôn cho cùng mã, đổi một trong hai thì mã đổi.</summary>
        public static string Compute(string machineGuid, string systemDriveSerial)
        {
            var raw = "product=" + LicenseConstants.FingerprintProduct + "|machineGuid=" + machineGuid + "|systemDrive=" + systemDriveSerial;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var builder = new StringBuilder();
                for (var i = 0; i < 16; i++)
                {
                    if (i > 0 && i % 4 == 0) builder.Append('-');
                    builder.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        /// <summary>Bỏ dấu gạch, khoảng trắng và đổi sang chữ hoa để so sánh mã máy do người dùng gõ lại.</summary>
        public static string Normalize(string machineCode)
        {
            return (machineCode ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
        }

        private static string GetMachineGuid()
        {
            try
            {
                // Registry64: tiến trình x86 trên Windows 64-bit mặc định bị chuyển hướng sang nhánh Wow6432Node và đọc nhầm.
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    var value = key == null ? null : Convert.ToString(key.GetValue("MachineGuid"), CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            catch (Exception)
            {
            }

            return "UNKNOWN_MACHINE_GUID";
        }

        private static string GetSystemDriveSerial()
        {
            try
            {
                var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
                uint serial;
                uint maxLength;
                uint flags;
                if (!string.IsNullOrEmpty(root) && GetVolumeInformation(root, null, 0, out serial, out maxLength, out flags, null, 0))
                {
                    return serial.ToString("X8", CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
            }

            return "UNKNOWN_DRIVE";
        }
    }

    /// <summary>Kiểm mã kích hoạt bằng khóa công khai RSA (SHA-256, PKCS#1 v1.5). Ứng dụng không có khóa bí mật nên không tự sinh được mã.</summary>
    public sealed class RsaActivationKeyVerifier : IActivationKeyVerifier
    {
        private readonly string _publicKeyXml;
        private readonly string _productCode;

        public RsaActivationKeyVerifier(string publicKeyXml, string productCode = LicenseConstants.ProductCode)
        {
            _publicKeyXml = publicKeyXml ?? string.Empty;
            _productCode = productCode;
        }

        public ActivationKeyCheck Verify(string activationKey, string machineCode)
        {
            try
            {
                var parts = (activationKey ?? string.Empty).Trim().Split('|');
                if (parts.Length != 3 || !string.Equals(parts[0], LicenseConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Mã kích hoạt không đúng định dạng.");
                }

                var payloadBytes = DecodeBase64Url(parts[1]);
                var signature = DecodeBase64Url(parts[2]);

                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(_publicKeyXml);
                    using (var sha = new SHA256CryptoServiceProvider())
                    {
                        if (!rsa.VerifyData(payloadBytes, sha, signature))
                        {
                            return ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Mã kích hoạt không hợp lệ hoặc đã bị thay đổi.");
                        }
                    }
                }

                var map = ParsePayload(Encoding.UTF8.GetString(payloadBytes));
                string product;
                if (!map.TryGetValue("product", out product) || !string.Equals(product, _productCode, StringComparison.OrdinalIgnoreCase))
                {
                    return ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Mã kích hoạt không thuộc sản phẩm này.");
                }

                string machine;
                if (!map.TryGetValue("machine", out machine) || MachineCodeProvider.Normalize(machine) != MachineCodeProvider.Normalize(machineCode))
                {
                    return ActivationKeyCheck.Fail(LicenseFailure.KeyBoundToOtherMachine, "Mã kích hoạt không đúng với máy này. Hãy gửi lại mã máy hiện tại cho đơn vị phát hành.");
                }

                string type;
                if (map.TryGetValue("license", out type) && !string.Equals(type, "PerMachine", StringComparison.OrdinalIgnoreCase))
                {
                    return ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Loại bản quyền trong mã kích hoạt không được hỗ trợ.");
                }

                string issuedText;
                DateTime issued;
                DateTime? issuedUtc = null;
                if (map.TryGetValue("issued", out issuedText) && DateTime.TryParse(issuedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out issued))
                {
                    issuedUtc = issued.ToUniversalTime();
                }

                return ActivationKeyCheck.Ok(issuedUtc);
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException || ex is ArgumentException || ex is System.Security.XmlSyntaxException)
            {
                // Base64 sai, khóa công khai hỏng hoặc chữ ký sai độ dài: đều là mã không dùng được, không phải lỗi chương trình.
                return ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Mã kích hoạt không đúng định dạng hoặc không đọc được.");
            }
        }

        private static IDictionary<string, string> ParsePayload(string payload)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (payload ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var index = part.IndexOf('=');
                if (index > 0) result[part.Substring(0, index).Trim()] = part.Substring(index + 1).Trim();
            }

            return result;
        }

        public static byte[] DecodeBase64Url(string input)
        {
            var normalized = (input ?? string.Empty).Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2:
                    normalized += "==";
                    break;
                case 3:
                    normalized += "=";
                    break;
            }

            return Convert.FromBase64String(normalized);
        }
    }

    /// <summary>
    /// Lưu <see cref="LicenseRecord"/> mã hóa bằng DPAPI (phạm vi máy) vào một file ẩn và, nếu được cấu hình, cả registry của người dùng hiện tại.
    /// Đọc: lấy mốc chạy đầu tiên SỚM nhất, mốc đã thấy MUỘN nhất và mã kích hoạt có ở bất kỳ nơi nào, nên xóa một nơi không làm mất hay làm mới thời gian dùng thử.
    /// Giới hạn đã biết của cơ chế offline: xóa cả hai nơi thì thời gian dùng thử bắt đầu lại.
    /// </summary>
    public sealed class FileRegistryLicenseStateStore : ILicenseStateStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HoaDonDienTu.LicenseState.v1");

        private readonly string _filePath;
        private readonly string _registryPath;

        /// <param name="filePath">File trạng thái.</param>
        /// <param name="registryPath">Khóa con dưới HKCU để lưu bản sao thứ hai; null thì không dùng registry (Debug và test).</param>
        public FileRegistryLicenseStateStore(string filePath, string registryPath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath");
            _filePath = filePath;
            _registryPath = registryPath;
        }

        public LicenseRecord Load()
        {
            return Merge(LoadFromFile(), LoadFromRegistry());
        }

        public void Save(LicenseRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(Serialize(record)), Entropy, DataProtectionScope.LocalMachine);

            var fileFailed = false;
            try
            {
                SaveToFile(protectedBytes);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
            {
                fileFailed = true;
            }

            var registryFailed = false;
            if (_registryPath != null)
            {
                try
                {
                    SaveToRegistry(protectedBytes);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
                {
                    registryFailed = true;
                }
            }

            // Chỉ báo lỗi khi KHÔNG lưu được ở bất kỳ nơi nào.
            if (fileFailed && (_registryPath == null || registryFailed)) throw new IOException("Không lưu được trạng thái bản quyền.");
        }

        /// <summary>Gộp hai bản: mốc chạy đầu sớm nhất, mốc đã thấy muộn nhất, mã kích hoạt còn lại của bản nào có.</summary>
        public static LicenseRecord Merge(LicenseRecord first, LicenseRecord second)
        {
            if (first == null) return second;
            if (second == null) return first;
            return new LicenseRecord
            {
                FirstRunUtc = first.FirstRunUtc <= second.FirstRunUtc ? first.FirstRunUtc : second.FirstRunUtc,
                LastSeenUtc = first.LastSeenUtc >= second.LastSeenUtc ? first.LastSeenUtc : second.LastSeenUtc,
                ActivationKey = !string.IsNullOrWhiteSpace(first.ActivationKey) ? first.ActivationKey : second.ActivationKey,
                ActivatedAtUtc = first.ActivatedAtUtc ?? second.ActivatedAtUtc
            };
        }

        private LicenseRecord LoadFromFile()
        {
            try
            {
                return File.Exists(_filePath) ? Deserialize(Unprotect(File.ReadAllBytes(_filePath))) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException || ex is FormatException)
            {
                return null;
            }
        }

        private LicenseRecord LoadFromRegistry()
        {
            if (_registryPath == null) return null;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(_registryPath))
                {
                    var value = key == null ? null : Convert.ToString(key.GetValue("LicenseState"), CultureInfo.InvariantCulture);
                    return string.IsNullOrWhiteSpace(value) ? null : Deserialize(Unprotect(Convert.FromBase64String(value)));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException || ex is FormatException || ex is System.Security.SecurityException)
            {
                return null;
            }
        }

        private void SaveToFile(byte[] protectedBytes)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                TryHide(directory, true);
            }

            File.WriteAllBytes(_filePath, protectedBytes);
            TryHide(_filePath, false);
        }

        private void SaveToRegistry(byte[] protectedBytes)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(_registryPath))
            {
                if (key != null) key.SetValue("LicenseState", Convert.ToBase64String(protectedBytes), RegistryValueKind.String);
            }
        }

        private static void TryHide(string path, bool directory)
        {
            try
            {
                if (directory) new DirectoryInfo(path).Attributes |= FileAttributes.Hidden;
                else File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        private static string Unprotect(byte[] protectedBytes)
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine));
        }

        private static string Serialize(LicenseRecord record)
        {
            return string.Join(
                "\n",
                "first_run_utc=" + record.FirstRunUtc.ToString("o", CultureInfo.InvariantCulture),
                "last_seen_utc=" + record.LastSeenUtc.ToString("o", CultureInfo.InvariantCulture),
                "activation_key=" + (record.ActivationKey ?? string.Empty),
                "activated_at_utc=" + (record.ActivatedAtUtc.HasValue ? record.ActivatedAtUtc.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty));
        }

        private static LicenseRecord Deserialize(string payload)
        {
            DateTime first = DateTime.MinValue;
            DateTime seen = DateTime.MinValue;
            DateTime activated;
            var record = new LicenseRecord();
            var foundFirst = false;
            foreach (var line in (payload ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var index = line.IndexOf('=');
                if (index <= 0) continue;
                var name = line.Substring(0, index).Trim();
                var value = line.Substring(index + 1);
                switch (name)
                {
                    case "first_run_utc":
                        foundFirst = DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out first);
                        break;
                    case "last_seen_utc":
                        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out seen);
                        break;
                    case "activation_key":
                        record.ActivationKey = value;
                        break;
                    case "activated_at_utc":
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out activated)) record.ActivatedAtUtc = activated.ToUniversalTime();
                        break;
                }
            }

            // Không có mốc chạy đầu tiên thì dữ liệu hỏng: coi như không có bản này (không tự đặt mốc mới để khỏi làm mới thời gian dùng thử).
            if (!foundFirst) throw new FormatException("Thiếu first_run_utc.");
            record.FirstRunUtc = first.ToUniversalTime();
            record.LastSeenUtc = seen == DateTime.MinValue ? record.FirstRunUtc : seen.ToUniversalTime();
            return record;
        }
    }
}
