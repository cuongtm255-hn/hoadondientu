using System;
using System.IO;
using HoaDonDienTu.Infrastructure.Persistence;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    [TestFixture]
    public class AtomicJsonWriterTests
    {
        private string _dir;
        private string _path;
        private AtomicJsonWriter _writer;

        private static bool LooksLikeJson(string text)
        {
            var t = text.Trim();
            return t.StartsWith("{") && t.EndsWith("}");
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-atomic-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "sub", "companies.json");
            _writer = new AtomicJsonWriter();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Write_NewFile_CreatesDirectoryAndFile_NoTempLeft()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);

            Assert.That(File.ReadAllText(_path), Is.EqualTo("{\"v\":1}"));
            Assert.That(File.Exists(_path + AtomicJsonWriter.TempSuffix), Is.False);
            Assert.That(File.Exists(_path + AtomicJsonWriter.BackupSuffix), Is.False);
        }

        [Test]
        public void Write_Overwrite_KeepsPreviousGoodVersionAsBak()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);
            _writer.Write(_path, "{\"v\":2}", LooksLikeJson);

            Assert.That(File.ReadAllText(_path), Is.EqualTo("{\"v\":2}"));
            Assert.That(File.ReadAllText(_path + AtomicJsonWriter.BackupSuffix), Is.EqualTo("{\"v\":1}"));

            _writer.Write(_path, "{\"v\":3}", LooksLikeJson);
            Assert.That(File.ReadAllText(_path + AtomicJsonWriter.BackupSuffix), Is.EqualTo("{\"v\":2}"));
        }

        [Test]
        public void Write_InvalidContent_Throws_OriginalUntouched_NoTempLeft()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);

            Assert.Throws<AtomicWriteException>(() => _writer.Write(_path, "not json", LooksLikeJson));

            Assert.That(File.ReadAllText(_path), Is.EqualTo("{\"v\":1}"));
            Assert.That(File.Exists(_path + AtomicJsonWriter.TempSuffix), Is.False);
            Assert.That(File.Exists(_path + AtomicJsonWriter.BackupSuffix), Is.False);
        }

        [Test]
        public void Write_WhenCurrentCorrupt_KeepsGoodBak_AndSavesCorruptCopy()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);
            _writer.Write(_path, "{\"v\":2}", LooksLikeJson);
            File.WriteAllText(_path, "garbage{{");

            _writer.Write(_path, "{\"v\":3}", LooksLikeJson);

            Assert.That(File.ReadAllText(_path), Is.EqualTo("{\"v\":3}"));
            Assert.That(File.ReadAllText(_path + AtomicJsonWriter.BackupSuffix), Is.EqualTo("{\"v\":1}"));
            Assert.That(File.ReadAllText(_path + AtomicJsonWriter.CorruptSuffix), Is.EqualTo("garbage{{"));
        }

        [Test]
        public void Write_UsesUtf8WithoutBom_AndRoundTripsVietnamese()
        {
            var content = "{\"ten\":\"CÔNG TY TNHH MẪU\"}";

            _writer.Write(_path, content, LooksLikeJson);

            var bytes = File.ReadAllBytes(_path);
            Assert.That(bytes[0], Is.EqualTo((byte)'{'));
            Assert.That(_writer.Read(_path, LooksLikeJson).Content, Is.EqualTo(content));
        }

        [Test]
        public void Write_IgnoresStaleTempFromCrash()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path + AtomicJsonWriter.TempSuffix, "{\"partial\":");

            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);

            Assert.That(File.ReadAllText(_path), Is.EqualTo("{\"v\":1}"));
            Assert.That(File.Exists(_path + AtomicJsonWriter.TempSuffix), Is.False);
        }

        [Test]
        public void Read_Missing_NotFound()
        {
            Assert.That(_writer.Read(_path, LooksLikeJson).Status, Is.EqualTo(AtomicReadStatus.NotFound));
        }

        [Test]
        public void Read_Good_Ok()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);

            var result = _writer.Read(_path, LooksLikeJson);

            Assert.That(result.Status, Is.EqualTo(AtomicReadStatus.Ok));
            Assert.That(result.Content, Is.EqualTo("{\"v\":1}"));
        }

        [Test]
        public void Read_PrimaryCorrupt_RecoversFromBak_WithoutModifyingFiles()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);
            _writer.Write(_path, "{\"v\":2}", LooksLikeJson);
            File.WriteAllText(_path, "garbage");

            var result = _writer.Read(_path, LooksLikeJson);

            Assert.That(result.Status, Is.EqualTo(AtomicReadStatus.RecoveredFromBackup));
            Assert.That(result.Content, Is.EqualTo("{\"v\":1}"));
            Assert.That(File.ReadAllText(_path), Is.EqualTo("garbage"));
        }

        [Test]
        public void Read_PrimaryMissing_BakValid_Recovers()
        {
            _writer.Write(_path, "{\"v\":1}", LooksLikeJson);
            _writer.Write(_path, "{\"v\":2}", LooksLikeJson);
            File.Delete(_path);

            Assert.That(_writer.Read(_path, LooksLikeJson).Status, Is.EqualTo(AtomicReadStatus.RecoveredFromBackup));
        }

        [Test]
        public void Read_BothInvalid_Corrupt()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, "garbage");
            File.WriteAllText(_path + AtomicJsonWriter.BackupSuffix, "also garbage");

            var result = _writer.Read(_path, LooksLikeJson);

            Assert.That(result.Status, Is.EqualTo(AtomicReadStatus.Corrupt));
            Assert.That(result.Content, Is.Null);
        }

        [Test]
        public void Read_NeverReadsTemp()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path + AtomicJsonWriter.TempSuffix, "{\"v\":99}");

            Assert.That(_writer.Read(_path, LooksLikeJson).Status, Is.EqualTo(AtomicReadStatus.NotFound));
        }

        [Test]
        public void Read_ValidatorThrows_TreatedAsInvalid()
        {
            _writer.Write(_path, "{\"v\":1}", null);

            var result = _writer.Read(_path, t => { throw new FormatException("parse"); });

            Assert.That(result.Status, Is.EqualTo(AtomicReadStatus.Corrupt));
        }

        [Test]
        public void Read_InvalidUtf8_TreatedAsInvalid()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllBytes(_path, new byte[] { 0x7B, 0xFF, 0xFE, 0x7D });

            Assert.That(_writer.Read(_path, LooksLikeJson).Status, Is.EqualTo(AtomicReadStatus.Corrupt));
        }
    }
}
