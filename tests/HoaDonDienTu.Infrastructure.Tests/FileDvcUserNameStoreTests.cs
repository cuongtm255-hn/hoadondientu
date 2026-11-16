using System;
using System.IO;
using HoaDonDienTu.Infrastructure.Settings;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    [TestFixture]
    public class FileDvcUserNameStoreTests
    {
        private string _dir;
        private string _file;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hddt-dvcu-" + Guid.NewGuid().ToString("N"));
            _file = Path.Combine(_dir, "dvc-usernames.txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void NoFile_ReturnsNull()
        {
            Assert.IsNull(new FileDvcUserNameStore(_file).Get("0100000000"));
        }

        [Test]
        public void RoundTrip_PerTaxCode_KeepsOthers()
        {
            var store = new FileDvcUserNameStore(_file);
            store.Set("0100000000", "0100000000-001");
            store.Set("0200000000", "ketoan02");
            store.Set("0100000000", "0100000000-002");

            var again = new FileDvcUserNameStore(_file);
            Assert.AreEqual("0100000000-002", again.Get("0100000000"));
            Assert.AreEqual("ketoan02", again.Get("0200000000"));
        }

        [TestCase("")]
        [TestCase("  ")]
        [TestCase("a\tb")]
        [TestCase("a\nb")]
        public void InvalidUserName_IsNotStored(string name)
        {
            var store = new FileDvcUserNameStore(_file);
            store.Set("0100000000", name);

            Assert.IsNull(store.Get("0100000000"));
        }

        [Test]
        public void OverlongUserName_IsNotStored()
        {
            var store = new FileDvcUserNameStore(_file);
            store.Set("0100000000", new string('x', 101));

            Assert.IsNull(store.Get("0100000000"));
        }
    }
}
