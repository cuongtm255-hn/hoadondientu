using System;
using System.IO;
using System.Text;

namespace HoaDonDienTu.Infrastructure.Persistence
{
    public enum AtomicReadStatus
    {
        /// <summary>Không có file chính cũng không có bản .bak.</summary>
        NotFound,
        Ok,

        /// <summary>File chính thiếu hoặc không hợp lệ; nội dung lấy từ .bak hợp lệ. Chưa ghi lại gì.</summary>
        RecoveredFromBackup,

        /// <summary>Có file nhưng cả file chính lẫn .bak đều không hợp lệ.</summary>
        Corrupt
    }

    public sealed class AtomicReadResult
    {
        public AtomicReadResult(AtomicReadStatus status, string content)
        {
            Status = status;
            Content = content;
        }

        public AtomicReadStatus Status { get; private set; }

        /// <summary>Có giá trị khi Ok hoặc RecoveredFromBackup.</summary>
        public string Content { get; private set; }
    }

    public sealed class AtomicWriteException : IOException
    {
        public AtomicWriteException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Ghi file cấu hình an toàn (DD-01 mục 16): ghi .tmp cùng thư mục, flush xuống đĩa, đọc lại và xác minh,
    /// rồi replace nguyên tử. Giữ .bak là bản hợp lệ gần nhất; file chính hỏng không bao giờ được chép đè lên .bak.
    /// Không phụ thuộc thư viện JSON: nơi gọi truyền hàm xác minh (cú pháp, schemaVersion...).
    /// </summary>
    public sealed class AtomicJsonWriter
    {
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";
        public const string CorruptSuffix = ".corrupt";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

        /// <exception cref="AtomicWriteException">Nội dung ghi ra không đọc lại được hoặc không hợp lệ; file chính giữ nguyên.</exception>
        public void Write(string path, string content, Func<string, bool> isValid)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            if (content == null) throw new ArgumentNullException("content");
            var validate = isValid ?? DefaultIsValid;

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);

            var temp = path + TempSuffix;
            var backup = path + BackupSuffix;
            try
            {
                WriteThrough(temp, content);

                // Đọc lại từ đĩa: nội dung phải đúng từng ký tự và qua kiểm tra của nơi gọi.
                var readBack = File.ReadAllText(temp, Utf8NoBom);
                if (!string.Equals(readBack, content, StringComparison.Ordinal) || !validate(readBack))
                {
                    throw new AtomicWriteException("Nội dung vừa ghi không đọc lại được hoặc không hợp lệ: " + Path.GetFileName(path));
                }

                if (!File.Exists(path))
                {
                    File.Move(temp, path);
                    return;
                }

                if (TryReadValid(path, validate) != null)
                {
                    // File hiện tại tốt: nó trở thành bản .bak, file mới thay thế nguyên tử.
                    File.Replace(temp, path, backup, true);
                }
                else
                {
                    // File hiện tại hỏng: giữ một bản để điều tra và không đụng tới .bak hiện có.
                    File.Copy(path, path + CorruptSuffix, true);
                    File.Replace(temp, path, null, true);
                }
            }
            finally
            {
                TryDelete(temp);
            }
        }

        /// <summary>Đọc file chính; nếu thiếu/hỏng thì thử .bak. Không bao giờ đọc .tmp (có thể là ghi dở).</summary>
        public AtomicReadResult Read(string path, Func<string, bool> isValid)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            var validate = isValid ?? DefaultIsValid;
            var backup = path + BackupSuffix;

            var primaryExists = File.Exists(path);
            var primary = primaryExists ? TryReadValid(path, validate) : null;
            if (primary != null) return new AtomicReadResult(AtomicReadStatus.Ok, primary);

            var backupExists = File.Exists(backup);
            var fromBackup = backupExists ? TryReadValid(backup, validate) : null;
            if (fromBackup != null) return new AtomicReadResult(AtomicReadStatus.RecoveredFromBackup, fromBackup);

            return new AtomicReadResult(
                primaryExists || backupExists ? AtomicReadStatus.Corrupt : AtomicReadStatus.NotFound,
                null);
        }

        private static string TryReadValid(string path, Func<string, bool> validate)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Utf8NoBom);
            }
            catch (DecoderFallbackException)
            {
                return null;
            }

            try
            {
                return validate(text) ? text : null;
            }
            catch (Exception)
            {
                // Hàm xác minh ném lỗi (ví dụ parse JSON) nghĩa là nội dung không hợp lệ.
                return null;
            }
        }

        private static void WriteThrough(string path, string content)
        {
            var bytes = Utf8NoBom.GetBytes(content);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static bool DefaultIsValid(string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
