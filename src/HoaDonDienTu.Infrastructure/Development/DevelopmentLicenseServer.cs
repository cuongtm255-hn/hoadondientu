#if DEBUG
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Licensing;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>
    /// Licence server giả cho môi trường phát triển (chờ contract thật ở gate G-06). Ký bằng cặp khóa RSA sinh lúc chạy,
    /// nên <see cref="PublicKeyXml"/> là thứ duy nhất client cần. Quy ước key để thử các nhánh:
    /// <c>HDDT-SVC-…</c> hợp lệ; chứa REVOKED, EXPIRED hoặc OTHER-MACHINE thì bị từ chối tương ứng; còn lại là KeyInvalid.
    /// Đặt <c>HDDT_DEV_LICENSE_SERVER=down</c> để mô phỏng mất kết nối.
    /// </summary>
    public sealed class DevelopmentLicenseServer : ILicenseServer
    {
        private readonly string _privateKeyXml;
        private readonly Func<bool> _isDown;

        /// <param name="keyFile">Nơi giữ cặp khóa giữa các lần chạy, để license.dat đã kích hoạt vẫn xác minh được sau khi khởi động lại.</param>
        public DevelopmentLicenseServer(Func<bool> isDown, string keyFile)
        {
            _isDown = isDown ?? (() => false);
            if (!string.IsNullOrEmpty(keyFile) && File.Exists(keyFile))
            {
                _privateKeyXml = File.ReadAllText(keyFile);
            }
            else
            {
                using (var rsa = new RSACryptoServiceProvider(1024))
                {
                    _privateKeyXml = rsa.ToXmlString(true);
                }

                if (!string.IsNullOrEmpty(keyFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(keyFile));
                    File.WriteAllText(keyFile, _privateKeyXml);
                }
            }

            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(_privateKeyXml);
                PublicKeyXml = rsa.ToXmlString(false);
            }
        }

        public string PublicKeyXml { get; private set; }

        public Task<LicenseServerResponse> ActivateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken)
        {
            return Issue(licenseKey, machineIdHash);
        }

        public Task<LicenseServerResponse> ValidateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken)
        {
            return Issue(licenseKey, machineIdHash);
        }

        public Task DeactivateAsync(string licenseKey, string machineIdHash, CancellationToken cancellationToken)
        {
            ThrowIfDown();
            return Task.FromResult(0);
        }

        private Task<LicenseServerResponse> Issue(string key, string machine)
        {
            ThrowIfDown();
            var upper = (key ?? string.Empty).ToUpperInvariant();
            if (upper.Contains("REVOKED")) throw new LicenseException(LicenseFailure.Revoked, "Licence đã bị thu hồi");
            if (upper.Contains("EXPIRED")) throw new LicenseException(LicenseFailure.Expired, "Licence đã hết hạn");
            if (upper.Contains("OTHER-MACHINE")) throw new LicenseException(LicenseFailure.KeyBoundToOtherMachine, "Licence gắn với máy khác");
            if (!upper.StartsWith("HDDT-SVC-")) throw new LicenseException(LicenseFailure.KeyInvalid, "Key không hợp lệ");

            var grant = new LicenseGrant(
                "Service", machine, DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                new[] { AppFeature.MultiCompany, AppFeature.PortalLogin, AppFeature.NewDownload, AppFeature.ReadExport, AppFeature.ManageSettings });
            return Task.FromResult(new LicenseServerResponse(grant, RsaLicenseSignatureVerifier.Sign(grant.ToSigningPayload(), _privateKeyXml)));
        }

        private void ThrowIfDown()
        {
            if (_isDown()) throw new LicenseException(LicenseFailure.ServerUnavailable, "Không kết nối được máy chủ licence");
        }
    }
}
#endif
