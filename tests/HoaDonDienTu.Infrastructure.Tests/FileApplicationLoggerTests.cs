using System;
using System.IO;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Infrastructure.Logging;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    [TestFixture]
    public class FileApplicationLoggerTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "hddt-log-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private string ReadLog()
        {
            var files = Directory.GetFiles(_directory, "*.log");
            Assert.That(files.Length, Is.EqualTo(1));
            return File.ReadAllText(files[0]);
        }

        [Test]
        public void Messages_AreSanitized()
        {
            var logger = new FileApplicationLogger(_directory, new FixedClock());

            logger.Info("Gọi API Authorization: Bearer topsecret cho MST 0101234567");
            logger.Warning("cookie: sid=COOKIEVALUE");

            var log = ReadLog();
            Assert.That(log, Does.Not.Contain("topsecret"));
            Assert.That(log, Does.Not.Contain("COOKIEVALUE"));
            Assert.That(log, Does.Contain("0101234567"));
        }

        [Test]
        public void ExceptionMessage_IsSanitized_AndStackTraceNotWritten()
        {
            var logger = new FileApplicationLogger(_directory, new FixedClock());

            logger.Error("lỗi", new InvalidOperationException("token=LEAKEDTOKEN"));

            var log = ReadLog();
            Assert.That(log, Does.Not.Contain("LEAKEDTOKEN"));
            Assert.That(log, Does.Contain("InvalidOperationException"));
            Assert.That(log, Does.Not.Contain(" at "));
        }

        [Test]
        public void NewlinesInMessage_CannotForgeLogLines()
        {
            var logger = new FileApplicationLogger(_directory, new FixedClock());

            logger.Info("dòng 1\r\n2026-01-01 ERROR giả mạo");

            Assert.That(ReadLog().Trim().Split('\n').Length, Is.EqualTo(1));
        }

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow
            {
                get { return new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc); }
            }
        }
    }
}
