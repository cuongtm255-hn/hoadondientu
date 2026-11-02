using System;
using System.IO;
using System.Text;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Infrastructure.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Invoices
{
    /// <summary>Đọc file XML đã lưu trên thư mục tạm; không liên quan cổng Thuế.</summary>
    [TestFixture]
    public class FileSystemInvoiceFileReaderTests
    {
        private string _root;
        private string _outside;
        private FileSystemInvoiceFileStore _store;

        [SetUp]
        public void SetUp()
        {
            var parent = Path.Combine(Path.GetTempPath(), "hddt-reader-" + Guid.NewGuid().ToString("N"));
            _root = Path.Combine(parent, "kho");
            _outside = Path.Combine(parent, "kho-khac");
            Directory.CreateDirectory(Path.Combine(_root, "2026"));
            Directory.CreateDirectory(_outside);
            _store = new FileSystemInvoiceFileStore();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(_root), true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Read_ReturnsTheExactBytes()
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'<', (byte)'a', (byte)'/', (byte)'>', 0xE1, 0xBA, 0xA1 };
            File.WriteAllBytes(Path.Combine(_root, "2026", "a.xml"), bytes);

            Assert.That(_store.Read(_root, Path.Combine("2026", "a.xml"), 1024), Is.EqualTo(bytes));
        }

        [Test]
        public void Read_MissingFile_ThrowsFileNotFound()
        {
            Assert.Throws<FileNotFoundException>(() => _store.Read(_root, Path.Combine("2026", "none.xml"), 1024));
        }

        [Test]
        public void Read_FileLargerThanTheLimit_IsNotLoaded()
        {
            File.WriteAllBytes(Path.Combine(_root, "2026", "big.xml"), new byte[2048]);

            Assert.Throws<InvoiceFileTooLargeException>(() => _store.Read(_root, Path.Combine("2026", "big.xml"), 1024));
        }

        [Test]
        public void Read_ExactlyAtTheLimit_IsAllowed()
        {
            File.WriteAllBytes(Path.Combine(_root, "2026", "edge.xml"), new byte[1024]);

            Assert.That(_store.Read(_root, Path.Combine("2026", "edge.xml"), 1024).Length, Is.EqualTo(1024));
        }

        [Test]
        public void Read_PathThatEscapesTheRoot_IsRefused()
        {
            File.WriteAllText(Path.Combine(_outside, "secret.xml"), "<secret/>", Encoding.UTF8);

            Assert.Throws<FileNotFoundException>(() => _store.Read(_root, Path.Combine("..", "kho-khac", "secret.xml"), 1024));
            Assert.Throws<FileNotFoundException>(() => _store.Read(_root, Path.Combine(_outside, "secret.xml"), 1024), "Đường dẫn tuyệt đối cũng không được ra ngoài kho");
        }

        [Test]
        public void Read_SiblingFolderWithTheSamePrefix_IsNotInsideTheRoot()
        {
            File.WriteAllText(Path.Combine(_outside, "x.xml"), "<x/>", Encoding.UTF8);

            Assert.Throws<FileNotFoundException>(() => _store.Read(_root, Path.Combine("..", "kho-khac", "x.xml"), 1024));
        }
    }
}
