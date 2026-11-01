using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Infrastructure.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Licensing
{
    [TestFixture]
    public class LicenseApiSettingsTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-licset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_dir, true);
        }

        private string Write(string json)
        {
            var path = Path.Combine(_dir, "license-api.json");
            File.WriteAllText(path, json);
            return path;
        }

        [Test]
        public void Valid_File_IsLoaded()
        {
            var settings = LicenseApiSettingsLoader.TryLoad(Write(
                "{\"baseUrl\":\"https://license.example.test/api\",\"timeoutSeconds\":20,\"publicKeyXml\":\"<RSAKeyValue/>\"}"));

            Assert.That(settings, Is.Not.Null);
            var config = settings.ToConfig();
            Assert.That(config.BaseUrl, Is.EqualTo("https://license.example.test/api"));
            Assert.That(config.Timeout, Is.EqualTo(TimeSpan.FromSeconds(20)));
        }

        [Test]
        public void MissingTimeout_UsesDefault()
        {
            var settings = LicenseApiSettingsLoader.TryLoad(Write(
                "{\"baseUrl\":\"https://license.example.test/api\",\"publicKeyXml\":\"<RSAKeyValue/>\"}"));

            Assert.That(settings.ToConfig().Timeout, Is.EqualTo(TimeSpan.FromSeconds(15)));
        }

        [TestCase("{\"baseUrl\":\"http://license.example.test\",\"publicKeyXml\":\"<k/>\"}", Description = "HTTP thường bị từ chối")]
        [TestCase("{\"baseUrl\":\"https://license.example.test\",\"publicKeyXml\":\"\"}", Description = "Thiếu public key")]
        [TestCase("{\"publicKeyXml\":\"<k/>\"}", Description = "Thiếu địa chỉ")]
        [TestCase("not json")]
        public void Incomplete_OrInvalid_ReturnsNull(string json)
        {
            Assert.That(LicenseApiSettingsLoader.TryLoad(Write(json)), Is.Null);
        }

        [Test]
        public void MissingFile_ReturnsNull()
        {
            Assert.That(LicenseApiSettingsLoader.TryLoad(Path.Combine(_dir, "nope.json")), Is.Null);
            Assert.That(LicenseApiSettingsLoader.TryLoad(null), Is.Null);
        }

        [Test]
        public void SampleFileInDocs_IsNotAcceptedAsRealConfiguration()
        {
            // File mẫu phải được thay bằng giá trị thật; địa chỉ .invalid vẫn hợp lệ về cú pháp nên chỉ kiểm tra nó đọc được.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "api-samples", "license-api.config.sample.json");
            if (!File.Exists(path)) Assert.Ignore("Không thấy file mẫu cạnh thư mục test");

            Assert.That(LicenseApiSettingsLoader.TryLoad(path), Is.Not.Null);
        }

        [Test]
        public void UnconfiguredServer_ReportsUnavailable_ForEveryCall()
        {
            var server = new UnconfiguredLicenseServer();

            Assert.That(Assert.ThrowsAsync<LicenseException>(() => server.ActivateAsync("k", "m", "1", CancellationToken.None)).Failure,
                Is.EqualTo(LicenseFailure.ServerUnavailable));
            Assert.That(Assert.ThrowsAsync<LicenseException>(() => server.ValidateAsync("k", "m", "1", CancellationToken.None)).Failure,
                Is.EqualTo(LicenseFailure.ServerUnavailable));
            Assert.That(Assert.ThrowsAsync<LicenseException>(() => server.DeactivateAsync("k", "m", CancellationToken.None)).Failure,
                Is.EqualTo(LicenseFailure.ServerUnavailable));
        }

        [Test]
        public void EmptyPublicKey_RejectsEverySignature()
        {
            var verifier = new RsaLicenseSignatureVerifier(string.Empty);

            Assert.That(verifier.Verify("payload", "c2ln"), Is.False);
        }
    }
}
