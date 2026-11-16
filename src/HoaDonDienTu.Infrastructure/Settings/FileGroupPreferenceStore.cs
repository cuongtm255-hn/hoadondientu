using System;
using System.IO;
using HoaDonDienTu.Application.Groups;

namespace HoaDonDienTu.Infrastructure.Settings
{
    /// <summary>Nhớ nhóm chức năng gần nhất trong một tệp văn bản nhỏ (chỉ tên nhóm, không có bí mật).</summary>
    public sealed class FileGroupPreferenceStore : IGroupPreferenceStore
    {
        private readonly string _path;

        public FileGroupPreferenceStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            _path = path;
        }

        public AppGroup? GetLastGroup()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                string text = File.ReadAllText(_path).Trim();
                AppGroup group;
                // Chỉ nhận tên nhóm đã biết; số hay chuỗi lạ coi như chưa có.
                if (Enum.TryParse(text, true, out group) && Enum.IsDefined(typeof(AppGroup), group) && !IsNumeric(text)) return group;
                return null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public void SetLastGroup(AppGroup group)
        {
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, group.ToString());
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static bool IsNumeric(string text)
        {
            int n;
            return int.TryParse(text, out n);
        }
    }
}
