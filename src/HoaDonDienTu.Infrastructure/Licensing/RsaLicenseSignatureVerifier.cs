using System;
using System.Security.Cryptography;
using System.Text;
using HoaDonDienTu.Application.Licensing;

namespace HoaDonDienTu.Infrastructure.Licensing
{
    /// <summary>
    /// RSA-SHA256 với public key nhúng trong ứng dụng (NFR-SEC-007). Thuật toán và khóa thật do gate G-06 chốt;
    /// đổi thuật toán chỉ cần thay lớp này.
    /// </summary>
    public sealed class RsaLicenseSignatureVerifier : ILicenseSignatureVerifier
    {
        private readonly string _publicKeyXml;

        public RsaLicenseSignatureVerifier(string publicKeyXml)
        {
            _publicKeyXml = publicKeyXml;
        }

        public bool Verify(string payload, string signature)
        {
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_publicKeyXml);
                    return rsa.VerifyData(Encoding.UTF8.GetBytes(payload), new SHA256CryptoServiceProvider(), Convert.FromBase64String(signature));
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Dùng cho licence server giả và test; ứng dụng thật không giữ private key.</summary>
        public static string Sign(string payload, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);
                return Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), new SHA256CryptoServiceProvider()));
            }
        }
    }
}
