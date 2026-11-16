using System;
using System.Collections.Generic;
using System.IO;
using HoaDonDienTu.Application.PublicService;

namespace HoaDonDienTu.Infrastructure.Settings
{
    /// <summary>Nhớ tên đăng nhập DVC theo MST trong tệp văn bản (mỗi dòng: MST, tab, tên). Không bao giờ có mật khẩu.</summary>
    public sealed class FileDvcUserNameStore : IDvcUserNameStore
    {
        private const int MaxLength = 100;
        private readonly string _path;

        public FileDvcUserNameStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            _path = path;
        }

        public string Get(string taxCode)
        {
            string name;
            return Load().TryGetValue(Key(taxCode), out name) ? name : null;
        }

        public void Set(string taxCode, string userName)
        {
            string key = Key(taxCode);
            if (key.Length == 0 || !IsValid(userName)) return;
            var map = Load();
            map[key] = userName.Trim();
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var lines = new List<string>();
                foreach (var pair in map) lines.Add(pair.Key + "\t" + pair.Value);
                File.WriteAllLines(_path, lines);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string Key(string taxCode)
        {
            return (taxCode ?? string.Empty).Trim();
        }

        private static bool IsValid(string userName)
        {
            return !string.IsNullOrWhiteSpace(userName)
                && userName.Trim().Length <= MaxLength
                && userName.IndexOfAny(new[] { '\t', '\r', '\n' }) < 0;
        }

        private Dictionary<string, string> Load()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                if (!File.Exists(_path)) return map;
                foreach (string line in File.ReadAllLines(_path))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string key = line.Substring(0, tab);
                    string name = line.Substring(tab + 1);
                    if (IsValid(name)) map[key] = name.Trim();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return map;
        }
    }
}
