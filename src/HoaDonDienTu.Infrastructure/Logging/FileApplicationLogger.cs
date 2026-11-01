using System;
using System.IO;
using System.Text;
using HoaDonDienTu.Application.Abstractions;

namespace HoaDonDienTu.Infrastructure.Logging
{
    /// <summary>Log dạng văn bản, ghi tối đa một file mỗi ngày. Lỗi ghi log không bao giờ được làm hỏng ứng dụng.</summary>
    public sealed class FileApplicationLogger : IApplicationLogger
    {
        private readonly string _directory;
        private readonly IClock _clock;
        private readonly object _gate = new object();

        public FileApplicationLogger(string directory, IClock clock)
        {
            _directory = directory;
            _clock = clock;
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warning(string message)
        {
            Write("WARN", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception)
        {
            try
            {
                var now = _clock.UtcNow;
                var line = new StringBuilder()
                    .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("Z ")
                    .Append(level).Append(' ').Append(message);
                if (exception != null)
                {
                    line.Append(" | ").Append(exception.GetType().FullName).Append(": ").Append(exception.Message);
                }

                lock (_gate)
                {
                    Directory.CreateDirectory(_directory);
                    var path = Path.Combine(_directory, "app-" + now.ToString("yyyyMMdd") + ".log");
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Không có nơi an toàn để báo lỗi ghi log.
            }
        }
    }
}
