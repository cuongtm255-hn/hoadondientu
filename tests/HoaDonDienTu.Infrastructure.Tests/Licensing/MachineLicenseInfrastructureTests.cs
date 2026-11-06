using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Infrastructure.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Licensing
{
    /// <summary>Mã máy, kiểm chữ ký mã kích hoạt và kho trạng thái. Cặp khóa RSA ở đây là khóa TẠM sinh trong test, không phải khóa thật.</summary>
    [TestFixture]
    [Category("Licensing")]
    public class MachineLicenseInfrastructureTests
    {
        private const string Machine = "0123ABCD-4567EF01-89ABCDEF-02468ACE";
        private RSACryptoServiceProvider _rsa;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            _dir = Path.Combine(Path.GetTempPath(), "hddt-lic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            _rsa.Dispose();
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        private string MakeKey(string payload, RSACryptoServiceProvider signer = null)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            byte[] signature;
            using (var sha = new SHA256CryptoServiceProvider())
            {
                signature = (signer ?? _rsa).SignData(bytes, sha);
            }

            return LicenseConstants.KeyPrefix + "|" + B64(bytes) + "|" + B64(signature);
        }

        private static string B64(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private RsaActivationKeyVerifier Verifier()
        {
            return new RsaActivationKeyVerifier(_rsa.ToXmlString(false));
        }

        // ---- Mã máy ----

        [Test]
        public void MachineCode_IsStable_AndFormatted()
        {
            var a = MachineCodeProvider.Compute("guid-1", "ABCD1234");
            var b = MachineCodeProvider.Compute("guid-1", "ABCD1234");

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Does.Match("^[0-9A-F]{8}(-[0-9A-F]{8}){3}$"));
        }

        [Test]
        public void MachineCode_ChangesWhenTheMachineOrDriveChanges()
        {
            var baseCode = MachineCodeProvider.Compute("guid-1", "ABCD1234");

            Assert.That(MachineCodeProvider.Compute("guid-2", "ABCD1234"), Is.Not.EqualTo(baseCode));
            Assert.That(MachineCodeProvider.Compute("guid-1", "FFFF0000"), Is.Not.EqualTo(baseCode));
        }

        [Test]
        public void MachineCode_OfThisMachine_IsNotEmptyAndRepeatable()
        {
            var provider = new MachineCodeProvider();

            Assert.That(provider.GetMachineCode(), Does.Match("^[0-9A-F]{8}(-[0-9A-F]{8}){3}$"));
            Assert.That(provider.GetMachineCode(), Is.EqualTo(provider.GetMachineCode()));
        }

        [Test]
        public void Normalize_IgnoresDashesSpacesAndCase()
        {
            Assert.That(MachineCodeProvider.Normalize(" 0123abcd-4567ef01 "), Is.EqualTo("0123ABCD4567EF01"));
            Assert.That(MachineCodeProvider.Normalize(null), Is.EqualTo(string.Empty));
        }

        // ---- Mã kích hoạt ----

        [Test]
        public void AKeySignedForThisMachine_Verifies()
        {
            var key = MakeKey("product=HDDT;machine=" + Machine + ";license=PerMachine;issued=2026-11-01T08:00:00Z");

            var check = Verifier().Verify(key, Machine);

            Assert.That(check.IsValid, Is.True);
            Assert.That(check.IssuedAtUtc, Is.EqualTo(new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void TheMachineCodeInTheKeyAndTheUserInput_IgnoreDashesAndCase()
        {
            var key = MakeKey("product=HDDT;machine=" + Machine.Replace("-", string.Empty).ToLowerInvariant() + ";license=PerMachine");

            Assert.That(Verifier().Verify(key, Machine).IsValid, Is.True);
        }

        [Test]
        public void AKeyForAnotherMachine_IsRefusedAsBoundToOtherMachine()
        {
            var key = MakeKey("product=HDDT;machine=FFFFFFFF-FFFFFFFF-FFFFFFFF-FFFFFFFF;license=PerMachine");

            var check = Verifier().Verify(key, Machine);

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Failure, Is.EqualTo(LicenseFailure.KeyBoundToOtherMachine));
        }

        [Test]
        public void AKeySignedByAnotherPrivateKey_IsInvalid()
        {
            using (var other = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false })
            {
                var key = MakeKey("product=HDDT;machine=" + Machine + ";license=PerMachine", other);

                var check = Verifier().Verify(key, Machine);

                Assert.That(check.IsValid, Is.False);
                Assert.That(check.Failure, Is.EqualTo(LicenseFailure.KeyInvalid));
            }
        }

        [Test]
        public void ChangingThePayloadAfterSigning_BreaksTheSignature()
        {
            var key = MakeKey("product=HDDT;machine=" + Machine + ";license=PerMachine");
            var parts = key.Split('|');
            var forged = parts[0] + "|" + B64(Encoding.UTF8.GetBytes("product=HDDT;machine=FFFFFFFF-FFFFFFFF-FFFFFFFF-FFFFFFFF;license=PerMachine")) + "|" + parts[2];

            Assert.That(Verifier().Verify(forged, "FFFFFFFF-FFFFFFFF-FFFFFFFF-FFFFFFFF").IsValid, Is.False);
        }

        [Test]
        public void AKeyForAnotherProduct_IsRefused()
        {
            var key = MakeKey("product=EZACC;machine=" + Machine + ";license=PerMachine");

            Assert.That(Verifier().Verify(key, Machine).IsValid, Is.False);
        }

        [Test]
        public void AnUnsupportedLicenceType_IsRefused()
        {
            var key = MakeKey("product=HDDT;machine=" + Machine + ";license=Enterprise");

            Assert.That(Verifier().Verify(key, Machine).IsValid, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("HDDT1|only-two")]
        [TestCase("WRONG|YQ|YQ")]
        [TestCase("HDDT1|***|###")]
        [TestCase("HDDT1|YQ|YQ")]
        public void MalformedKeys_AreRefusedWithoutThrowing(string key)
        {
            var check = Verifier().Verify(key, Machine);

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Message, Is.Not.Empty);
        }

        [Test]
        public void ABrokenPublicKey_IsRefusedWithoutThrowing()
        {
            var key = MakeKey("product=HDDT;machine=" + Machine + ";license=PerMachine");

            Assert.That(new RsaActivationKeyVerifier("<not-xml>").Verify(key, Machine).IsValid, Is.False);
        }

        [Test]
        public void TheEmbeddedPublicKey_IsAValidRsaKey_AndRefusesAKeyFromAnotherPair()
        {
            var key = MakeKey("product=HDDT;machine=" + Machine + ";license=PerMachine");

            var check = new RsaActivationKeyVerifier(LicenseConstants.PublicKeyXml).Verify(key, Machine);

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Failure, Is.EqualTo(LicenseFailure.KeyInvalid));
        }

        [Test]
        public void TheEmbeddedPublicKey_DoesNotContainAPrivatePart()
        {
            Assert.That(LicenseConstants.PublicKeyXml, Does.Not.Contain("<D>"));
            Assert.That(LicenseConstants.PublicKeyXml, Does.Not.Contain("<P>"));
        }

        // ---- Kho trạng thái ----

        private static LicenseRecord Record(int firstRunDay, int lastSeenDay, string key = null)
        {
            return new LicenseRecord
            {
                FirstRunUtc = new DateTime(2026, 11, firstRunDay, 0, 0, 0, DateTimeKind.Utc),
                LastSeenUtc = new DateTime(2026, 11, lastSeenDay, 0, 0, 0, DateTimeKind.Utc),
                ActivationKey = key,
                ActivatedAtUtc = key == null ? (DateTime?)null : new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        [Test]
        public void ANewStore_HasNothing()
        {
            var store = new FileRegistryLicenseStateStore(Path.Combine(_dir, "a.bin"), null);

            Assert.That(store.Load(), Is.Null);
        }

        [Test]
        public void SavedState_IsReadBack_AndTheFileIsEncrypted()
        {
            var path = Path.Combine(_dir, "a.bin");
            var store = new FileRegistryLicenseStateStore(path, null);

            store.Save(Record(1, 5, "HDDT1|x|y"));

            var loaded = store.Load();
            Assert.That(loaded.FirstRunUtc, Is.EqualTo(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(loaded.LastSeenUtc, Is.EqualTo(new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(loaded.ActivationKey, Is.EqualTo("HDDT1|x|y"));
            Assert.That(loaded.ActivatedAtUtc, Is.Not.Null);
            var raw = Encoding.UTF8.GetString(File.ReadAllBytes(path));
            Assert.That(raw, Does.Not.Contain("HDDT1"), "Mã kích hoạt không được nằm dạng rõ trong file");
        }

        [Test]
        public void ACorruptStateFile_IsTreatedAsMissing_NotAsACrash()
        {
            var path = Path.Combine(_dir, "a.bin");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5 });

            Assert.That(new FileRegistryLicenseStateStore(path, null).Load(), Is.Null);
        }

        [Test]
        public void Merge_TakesTheEarliestFirstRun_TheLatestSeen_AndAnyKey()
        {
            var merged = FileRegistryLicenseStateStore.Merge(Record(5, 6), Record(2, 9, "HDDT1|k|s"));

            Assert.That(merged.FirstRunUtc.Day, Is.EqualTo(2), "Xóa một nơi không làm mới thời gian dùng thử");
            Assert.That(merged.LastSeenUtc.Day, Is.EqualTo(9));
            Assert.That(merged.ActivationKey, Is.EqualTo("HDDT1|k|s"));
        }

        [Test]
        public void Merge_WithOneSideMissing_ReturnsTheOther()
        {
            var one = Record(3, 4);

            Assert.That(FileRegistryLicenseStateStore.Merge(one, null), Is.SameAs(one));
            Assert.That(FileRegistryLicenseStateStore.Merge(null, one), Is.SameAs(one));
            Assert.That(FileRegistryLicenseStateStore.Merge(null, null), Is.Null);
        }

        [Test]
        public void TheFileAndTheRegistryCopy_SurviveDeletingEitherOne()
        {
            var path = Path.Combine(_dir, "a.bin");
            var registryPath = @"Software\HoaDonDienTuTests\" + Guid.NewGuid().ToString("N");
            try
            {
                var store = new FileRegistryLicenseStateStore(path, registryPath);
                store.Save(Record(1, 5));

                File.Delete(path);
                var afterFileDeleted = store.Load();
                Assert.That(afterFileDeleted, Is.Not.Null, "Còn bản sao ở registry");
                Assert.That(afterFileDeleted.FirstRunUtc.Day, Is.EqualTo(1));
            }
            finally
            {
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); } catch (ArgumentException) { }
                try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\HoaDonDienTuTests", false); } catch (ArgumentException) { }
            }
        }

        [Test]
        public void SavingToAnUnwritableLocation_WithNoRegistryCopy_Throws()
        {
            // "con" là tên thiết bị trên Windows: không thể tạo file thường có tên này.
            var store = new FileRegistryLicenseStateStore(Path.Combine(_dir, "no-such-dir", "x", "con:bad"), null);

            Assert.Throws<IOException>(() => store.Save(Record(1, 2)));
        }
    }
}
