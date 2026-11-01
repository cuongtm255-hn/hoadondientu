using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Licensing
{
    [TestFixture]
    public class LicenseInfrastructureTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

        private static string _privateXml;
        private static string _publicXml;

        private string _dir;
        private string _path;

        [OneTimeSetUp]
        public void GenerateKeys()
        {
            using (var rsa = new RSACryptoServiceProvider(1024))
            {
                _privateXml = rsa.ToXmlString(true);
                _publicXml = rsa.ToXmlString(false);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-lic-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "license.dat");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static StoredLicense Sample(DateTime? expires = null)
        {
            var grant = new LicenseGrant("Service", "abc123", T0, expires, new[] { AppFeature.PortalLogin, AppFeature.MultiCompany });
            return new StoredLicense("ABCD-EFGH-1234", grant, "sig", T0.AddHours(-1), T0, false);
        }

        // ---- FileLicenseStore ----

        [Test]
        public void Store_RoundTripsEverything()
        {
            var store = new FileLicenseStore(_path);

            store.Save(Sample(T0.AddYears(1)));
            var loaded = store.Load();

            Assert.That(loaded.Status, Is.EqualTo(LicenseLoadStatus.Ok));
            var l = loaded.License;
            Assert.That(l.LicenseKey, Is.EqualTo("ABCD-EFGH-1234"));
            Assert.That(l.Grant.ToSigningPayload(), Is.EqualTo(Sample(T0.AddYears(1)).Grant.ToSigningPayload()));
            Assert.That(l.LastOnlineValidationUtc, Is.EqualTo(T0.AddHours(-1)));
            Assert.That(l.LastSeenUtc, Is.EqualTo(T0));
            Assert.That(l.Grant.IssuedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void Store_FileIsEncrypted_NoPlainKeyOnDisk()
        {
            new FileLicenseStore(_path).Save(Sample());

            var raw = File.ReadAllText(_path);

            Assert.That(raw, Does.Not.Contain("ABCD"));
            Assert.That(raw, Does.Not.Contain("abc123"));
        }

        [Test]
        public void Store_Missing_IsNotFound()
        {
            Assert.That(new FileLicenseStore(_path).Load().Status, Is.EqualTo(LicenseLoadStatus.NotFound));
        }

        [Test]
        public void Store_Garbage_IsCorrupt_NotNotFound()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "this is not a license");

            Assert.That(new FileLicenseStore(_path).Load().Status, Is.EqualTo(LicenseLoadStatus.Corrupt));
        }

        [Test]
        public void Store_CorruptMainFile_RecoversFromBackup()
        {
            var store = new FileLicenseStore(_path);
            store.Save(Sample());
            store.Save(Sample().WithSeen(T0.AddDays(1)));
            File.WriteAllText(_path, "damaged");

            var loaded = store.Load();

            Assert.That(loaded.Status, Is.EqualTo(LicenseLoadStatus.Ok));
            Assert.That(loaded.License.LicenseKey, Is.EqualTo("ABCD-EFGH-1234"));
        }

        [Test]
        public void Store_Delete_RemovesAllFiles()
        {
            var store = new FileLicenseStore(_path);
            store.Save(Sample());
            store.Save(Sample());

            store.Delete();

            Assert.That(Directory.GetFiles(_dir), Is.Empty);
            Assert.That(store.Load().Status, Is.EqualTo(LicenseLoadStatus.NotFound));
        }

        // ---- Signature ----

        [Test]
        public void Rsa_ValidSignature_Verifies_TamperedPayload_Fails()
        {
            var verifier = new RsaLicenseSignatureVerifier(_publicXml);
            var signature = RsaLicenseSignatureVerifier.Sign("payload", _privateXml);

            Assert.That(verifier.Verify("payload", signature), Is.True);
            Assert.That(verifier.Verify("payload2", signature), Is.False);
            Assert.That(verifier.Verify("payload", "not-base64!"), Is.False);
            Assert.That(verifier.Verify("payload", string.Empty), Is.False);
        }

        [Test]
        public void Rsa_SignatureFromOtherKey_Fails()
        {
            string otherPrivate;
            using (var rsa = new RSACryptoServiceProvider(1024)) otherPrivate = rsa.ToXmlString(true);

            var verifier = new RsaLicenseSignatureVerifier(_publicXml);

            Assert.That(verifier.Verify("payload", RsaLicenseSignatureVerifier.Sign("payload", otherPrivate)), Is.False);
        }

        // ---- Machine identity ----

        [Test]
        public void MachineId_IsStableHexHash_NotTheRawGuid()
        {
            var provider = new MachineIdentityProvider();

            var id = provider.GetMachineIdHash();

            Assert.That(id, Has.Length.EqualTo(64));
            Assert.That(id, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(new MachineIdentityProvider().GetMachineIdHash(), Is.EqualTo(id));
            Assert.That(MachineIdentityProvider.Hash("x"), Is.Not.EqualTo(MachineIdentityProvider.Hash("y")));
        }

        // ---- Full flow: real store + real RSA + simulated server ----

        [Test]
        public async Task FullFlow_Activate_Restart_Validate_Deactivate()
        {
            var clock = new MutableClock(T0);
            var machine = new MachineIdentityProvider();
            var server = new SigningServer(_privateXml, machine.GetMachineIdHash());
            var verifier = new RsaLicenseSignatureVerifier(_publicXml);

            var first = new LicenseService(new FileLicenseStore(_path), server, machine, verifier, clock, new NullLogger(), "1.0.0");
            Assert.That(await first.ActivateAsync("KEY-AAAA-0001", CancellationToken.None), Is.EqualTo(LicenseStatus.Valid));

            // "Khởi động lại": service mới đọc license.dat từ đĩa.
            clock.Now = T0.AddDays(3);
            var second = new LicenseService(new FileLicenseStore(_path), server, machine, verifier, clock, new NullLogger(), "1.0.0");
            Assert.That(await second.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.GracePeriod));
            Assert.That(await second.ValidateOnlineAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Valid));

            Assert.That(await second.DeactivateAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.None));
            Assert.That(File.Exists(_path), Is.False);
            Assert.That(server.Log, Is.EqualTo(new[] { "activate", "validate", "deactivate" }));
        }

        [Test]
        public async Task FullFlow_LicenseFileCopiedFromAnotherMachine_IsRejected()
        {
            var clock = new MutableClock(T0);
            var verifier = new RsaLicenseSignatureVerifier(_publicXml);
            var other = new FixedMachine("other-machine-hash");
            var server = new SigningServer(_privateXml, "other-machine-hash");
            var otherService = new LicenseService(new FileLicenseStore(_path), server, other, verifier, clock, new NullLogger(), "1.0.0");
            await otherService.ActivateAsync("KEY-AAAA-0001", CancellationToken.None);

            var thisMachine = new LicenseService(new FileLicenseStore(_path), server, new MachineIdentityProvider(), verifier, clock, new NullLogger(), "1.0.0");

            Assert.That(await thisMachine.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Invalid));
            Assert.That(thisMachine.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.MachineMismatch));
        }

        [Test]
        public async Task ServerRequest_ContainsOnlyKeyMachineAndVersion()
        {
            var clock = new MutableClock(T0);
            var machine = new MachineIdentityProvider();
            var server = new SigningServer(_privateXml, machine.GetMachineIdHash());
            var service = new LicenseService(new FileLicenseStore(_path), server, machine, new RsaLicenseSignatureVerifier(_publicXml), clock, new NullLogger(), "1.2.3");

            await service.ActivateAsync("KEY-AAAA-0001", CancellationToken.None);

            Assert.That(server.LastArguments, Is.EqualTo(new[] { "KEY-AAAA-0001", machine.GetMachineIdHash(), "1.2.3" }));
        }

        private sealed class MutableClock : IClock
        {
            public MutableClock(DateTime now)
            {
                Now = now;
            }

            public DateTime Now { get; set; }

            public DateTime UtcNow
            {
                get { return Now; }
            }
        }

        private sealed class FixedMachine : IMachineIdentity
        {
            private readonly string _hash;

            public FixedMachine(string hash)
            {
                _hash = hash;
            }

            public string GetMachineIdHash()
            {
                return _hash;
            }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message)
            {
            }

            public void Warning(string message)
            {
            }

            public void Error(string message, Exception exception)
            {
            }
        }

        /// <summary>Server giả ký bằng RSA thật; cấp grant cho đúng mã máy gửi lên.</summary>
        private sealed class SigningServer : ILicenseServer
        {
            private readonly string _privateXml;
            private readonly string _machine;

            public SigningServer(string privateXml, string machine)
            {
                _privateXml = privateXml;
                _machine = machine;
                Log = new System.Collections.Generic.List<string>();
            }

            public System.Collections.Generic.List<string> Log { get; private set; }
            public string[] LastArguments { get; private set; }

            public Task<LicenseServerResponse> ActivateAsync(string key, string machine, string version, CancellationToken ct)
            {
                Log.Add("activate");
                return Issue(key, machine, version);
            }

            public Task<LicenseServerResponse> ValidateAsync(string key, string machine, string version, CancellationToken ct)
            {
                Log.Add("validate");
                return Issue(key, machine, version);
            }

            public Task DeactivateAsync(string key, string machine, CancellationToken ct)
            {
                Log.Add("deactivate");
                return Task.FromResult(0);
            }

            private Task<LicenseServerResponse> Issue(string key, string machine, string version)
            {
                LastArguments = new[] { key, machine, version };
                var grant = new LicenseGrant("Service", machine, T0, null, new[] { AppFeature.MultiCompany, AppFeature.PortalLogin, AppFeature.NewDownload, AppFeature.ReadExport, AppFeature.ManageSettings });
                return Task.FromResult(new LicenseServerResponse(grant, RsaLicenseSignatureVerifier.Sign(grant.ToSigningPayload(), _privateXml)));
            }
        }
    }
}
