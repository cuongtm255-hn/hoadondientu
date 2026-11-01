using System;
using System.Security.Cryptography;
using System.Text;
using HoaDonDienTu.Application.Licensing;
using Microsoft.Win32;

namespace HoaDonDienTu.Infrastructure.Licensing
{
    /// <summary>Mã máy = SHA-256(MachineGuid + muối cố định). Mã gốc không rời khỏi lớp này (NFR-SEC-008).</summary>
    public sealed class MachineIdentityProvider : IMachineIdentity
    {
        private const string Salt = "HoaDonDienTu|machine|v1";
        private string _cached;

        public string GetMachineIdHash()
        {
            if (_cached == null) _cached = Hash(ReadMachineGuid());
            return _cached;
        }

        public static string Hash(string raw)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + "|" + raw));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static string ReadMachineGuid()
        {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                var value = key == null ? null : key.GetValue("MachineGuid") as string;
                if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("Không đọc được mã máy");
                return value;
            }
        }
    }
}
