using System.IO;
using System.Linq;
using HoaDonDienTu.Infrastructure.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Invoices
{
    /// <summary>Quét thư mục kho GIẢ (MOCK) tạo trong thư mục tạm; không gọi cổng Thuế.</summary>
    [TestFixture]
    [Category("Mock")]
    public class FileSystemInvoiceFileScannerTests
    {
        private string _root;
        private readonly FileSystemInvoiceFileScanner _scanner = new FileSystemInvoiceFileScanner();

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt_scan_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private void Touch(string relative)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
        }

        [Test]
        public void FindsXmlAtAnyDepth_AsPathsRelativeToTheRoot()
        {
            Touch(@"2026\10\a.xml");
            Touch(@"2026\11\C1\b.xml");
            Touch(@"top.xml");
            Touch(@"2026\10\readme.txt");

            var found = _scanner.EnumerateXmlFiles(_root).OrderBy(p => p).ToList();

            Assert.That(found, Is.EqualTo(new[] { @"2026\10\a.xml", @"2026\11\C1\b.xml", "top.xml" }));
        }

        [Test]
        public void SkipsTheReservedFolders()
        {
            Touch(@"Data\company.db");
            Touch(@"Data\leftover.xml");
            Touch(@"Backups\x.xml");
            Touch(@"Exports\y.xml");
            Touch(@"2026\a.xml");

            var found = _scanner.EnumerateXmlFiles(_root).ToList();

            Assert.That(found, Is.EqualTo(new[] { @"2026\a.xml" }));
        }

        [Test]
        public void ListsStrayTempFilesOnly()
        {
            Touch(@"2026\10\0f3a.tmp");
            Touch(@"2026\10\a.xml");

            var temps = _scanner.EnumerateTempFiles(_root).ToList();

            Assert.That(temps, Is.EqualTo(new[] { @"2026\10\0f3a.tmp" }));
        }

        [Test]
        public void MissingRoot_YieldsNothing()
        {
            Assert.That(_scanner.EnumerateXmlFiles(Path.Combine(_root, "none")), Is.Empty);
            Assert.That(_scanner.EnumerateXmlFiles(null), Is.Empty);
        }
    }
}
