using System;
using System.IO;
using HoaDonDienTu.Application.Groups;
using HoaDonDienTu.Infrastructure.Settings;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    [TestFixture]
    public class FileGroupPreferenceStoreTests
    {
        private string _dir;
        private string _file;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-grp-" + Guid.NewGuid().ToString("N"));
            _file = Path.Combine(_dir, "sub", "last-group.txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void NoFile_ReturnsNull()
        {
            Assert.IsNull(new FileGroupPreferenceStore(_file).GetLastGroup());
        }

        [Test]
        public void SetThenGet_RoundTrips_AndCreatesDirectory()
        {
            var store = new FileGroupPreferenceStore(_file);
            store.SetLastGroup(AppGroup.PublicService);

            Assert.AreEqual(AppGroup.PublicService, new FileGroupPreferenceStore(_file).GetLastGroup());
        }

        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("7")]
        [TestCase("1")]
        public void InvalidContent_ReturnsNull(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file));
            File.WriteAllText(_file, content);

            Assert.IsNull(new FileGroupPreferenceStore(_file).GetLastGroup());
        }
    }
}
