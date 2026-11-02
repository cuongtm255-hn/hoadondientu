using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqliteSpike
{
    /// <summary>
    /// Spike G-08: kiểm System.Data.SQLite.Core trên net45 với các nhu cầu của dự án (WAL, backup API, integrity_check, 100.000 hóa đơn,
    /// tiếng Việt, crash an toàn, x86/x64, đường dẫn). Chỉ dùng dữ liệu giả, không gọi cổng Thuế.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "crash-writer") return CrashWriter(args[1]);
            var dir = args.Length >= 1 ? args[0] : Path.Combine(Path.GetTempPath(), "hddt-sqlite-spike-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("== Môi trường ==");
            Console.WriteLine("Tiến trình: " + (IntPtr.Size * 8) + "-bit, OS 64-bit: " + Environment.Is64BitOperatingSystem + ", CLR " + Environment.Version);
            Console.WriteLine("SQLite " + SQLiteConnection.SQLiteVersion + " (System.Data.SQLite " + SQLiteConnection.SQLiteSourceId.Substring(0, 19) + "…)");
            Console.WriteLine("Thư mục thử: " + dir);

            var db = Path.Combine(dir, "company.db");
            Basics(db);
            BulkAndQueries(db);
            Unicode(db);
            Concurrency(db);
            Backup(db, Path.Combine(dir, "backup.db"));
            Migration(db);
            Crash(Path.Combine(dir, "crash.db"));
            Paths(dir);
            Console.WriteLine("== Xong ==");
            return 0;
        }

        private static SQLiteConnection Open(string path, bool wal = true)
        {
            var cs = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3, JournalMode = wal ? SQLiteJournalModeEnum.Wal : SQLiteJournalModeEnum.Delete, SyncMode = SynchronizationModes.Normal, ForeignKeys = true, BusyTimeout = 5000 };
            var c = new SQLiteConnection(cs.ToString());
            c.Open();
            return c;
        }

        private static object Scalar(SQLiteConnection c, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, c)) return cmd.ExecuteScalar();
        }

        private static void Exec(SQLiteConnection c, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, c)) cmd.ExecuteNonQuery();
        }

        // ---- 1. Cơ bản ----

        private static void Basics(string db)
        {
            Console.WriteLine("== 1. Cơ bản: WAL, integrity_check, user_version ==");
            using (var c = Open(db))
            {
                Console.WriteLine("journal_mode = " + Scalar(c, "PRAGMA journal_mode"));
                Console.WriteLine("synchronous = " + Scalar(c, "PRAGMA synchronous") + " (1 = NORMAL)");
                Console.WriteLine("foreign_keys = " + Scalar(c, "PRAGMA foreign_keys"));
                Exec(c, "CREATE TABLE invoice (" +
                        "id TEXT PRIMARY KEY, business_key TEXT NOT NULL UNIQUE, direction INTEGER NOT NULL, issue_date INTEGER NOT NULL, " +
                        "seller_tax TEXT, seller_name TEXT, buyer_tax TEXT, buyer_name TEXT, invoice_number TEXT, symbol TEXT, " +
                        "total_payment INTEGER, status TEXT, search_text TEXT)");
                Exec(c, "CREATE INDEX ix_invoice_dir_date ON invoice(direction, issue_date DESC)");
                Exec(c, "CREATE INDEX ix_invoice_seller ON invoice(seller_tax)");
                Exec(c, "PRAGMA user_version = 1");
                Console.WriteLine("user_version = " + Scalar(c, "PRAGMA user_version"));
                Console.WriteLine("integrity_check = " + Scalar(c, "PRAGMA integrity_check"));
            }
        }

        // ---- 2. 100.000 hóa đơn ----

        private static void BulkAndQueries(string db)
        {
            Console.WriteLine("== 2. 100.000 hóa đơn: nạp và truy vấn (NFR-PERF-002) ==");
            const int n = 100000;
            var rnd = new Random(42);
            var sw = Stopwatch.StartNew();
            using (var c = Open(db))
            using (var tx = c.BeginTransaction())
            using (var cmd = new SQLiteCommand(
                "INSERT INTO invoice(id,business_key,direction,issue_date,seller_tax,seller_name,buyer_tax,buyer_name,invoice_number,symbol,total_payment,status,search_text) " +
                "VALUES(@id,@bk,@d,@date,@st,@sn,@bt,@bn,@no,@sy,@pay,@s,@txt)", c, tx))
            {
                foreach (var name in new[] { "@id", "@bk", "@d", "@date", "@st", "@sn", "@bt", "@bn", "@no", "@sy", "@pay", "@s", "@txt" }) cmd.Parameters.Add(new SQLiteParameter(name));
                for (var i = 0; i < n; i++)
                {
                    var date = new DateTime(2024, 1, 1).AddDays(rnd.Next(0, 1000));
                    var seller = "CÔNG TY TNHH ĐÔNG Á SỐ " + (i % 500);
                    cmd.Parameters["@id"].Value = Guid.NewGuid().ToString();
                    cmd.Parameters["@bk"].Value = "P|0100" + (i % 500).ToString("D6") + "|1|C26TAA|" + i;
                    cmd.Parameters["@d"].Value = i % 4 == 0 ? 2 : 1;
                    cmd.Parameters["@date"].Value = int.Parse(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    cmd.Parameters["@st"].Value = "0100" + (i % 500).ToString("D6");
                    cmd.Parameters["@sn"].Value = seller;
                    cmd.Parameters["@bt"].Value = "0101234567";
                    cmd.Parameters["@bn"].Value = "CÔNG TY TNHH MẪU";
                    cmd.Parameters["@no"].Value = i.ToString("D7");
                    cmd.Parameters["@sy"].Value = "C26TAA";
                    cmd.Parameters["@pay"].Value = (long)rnd.Next(100000, 900000000);
                    cmd.Parameters["@s"].Value = "1";
                    cmd.Parameters["@txt"].Value = "cong ty tnhh dong a so " + (i % 500) + " " + i.ToString("D7");
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }

            Console.WriteLine("Nạp " + n + " dòng trong 1 giao dịch: " + sw.ElapsedMilliseconds + " ms");
            Console.WriteLine("Kích thước file: " + (new FileInfo(db).Length / 1024 / 1024) + " MB (+ WAL " + (File.Exists(db + "-wal") ? new FileInfo(db + "-wal").Length / 1024 : 0) + " KB)");

            using (var c = Open(db))
            {
                Time("COUNT(*) chiều mua vào", () => Scalar(c, "SELECT COUNT(*) FROM invoice WHERE direction=1"));
                Time("Trang 1 (50 dòng) sắp xếp ngày lập giảm dần, chiều mua vào", () => Scalar(c, "SELECT COUNT(*) FROM (SELECT * FROM invoice WHERE direction=1 ORDER BY issue_date DESC LIMIT 50 OFFSET 0)"));
                Time("Trang 1.000 (OFFSET 50.000)", () => Scalar(c, "SELECT COUNT(*) FROM (SELECT * FROM invoice WHERE direction=1 ORDER BY issue_date DESC LIMIT 50 OFFSET 50000)"));
                Time("Tóm tắt SUM/COUNT theo năm 2025 và chiều", () => Scalar(c, "SELECT SUM(total_payment) FROM invoice WHERE direction=1 AND issue_date BETWEEN 20250101 AND 20251231"));
                Time("Tìm theo MST bên bán (có chỉ mục)", () => Scalar(c, "SELECT COUNT(*) FROM invoice WHERE seller_tax='0100000123'"));
                Time("Tìm nhanh LIKE %dong a so 42% trên cột đã bỏ dấu (quét toàn bảng)", () => Scalar(c, "SELECT COUNT(*) FROM invoice WHERE search_text LIKE '%dong a so 42%'"));
                Time("Tra theo khóa nghiệp vụ UNIQUE", () => Scalar(c, "SELECT id FROM invoice WHERE business_key='P|0100000010|1|C26TAA|10'"));

                var before = GC.GetTotalMemory(true);
                long rows = 0;
                var sw2 = Stopwatch.StartNew();
                using (var cmd = new SQLiteCommand("SELECT id, seller_name, total_payment FROM invoice WHERE direction=1", c))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) rows += r.GetInt64(2) > 0 ? 1 : 0;
                }

                Console.WriteLine("Đọc streaming 75.000 dòng: " + sw2.ElapsedMilliseconds + " ms, " + rows + " dòng, bộ nhớ managed tăng " + ((GC.GetTotalMemory(false) - before) / 1024) + " KB");
            }
        }

        private static void Time(string name, Func<object> action)
        {
            action();
            var sw = Stopwatch.StartNew();
            var result = action();
            Console.WriteLine(name + ": " + sw.ElapsedMilliseconds + " ms (kết quả " + result + ")");
        }

        // ---- 3. Unicode tiếng Việt ----

        private static void Unicode(string db)
        {
            Console.WriteLine("== 3. Tiếng Việt: lưu trữ, LOWER, NOCASE, LIKE ==");
            using (var c = Open(db))
            {
                Exec(c, "CREATE TABLE IF NOT EXISTS vn(t TEXT)");
                Exec(c, "INSERT INTO vn VALUES('CÔNG TY ĐÔNG Á'),('Công ty Đông Á'),('cong ty dong a')");
                Console.WriteLine("Lưu và đọc lại nguyên vẹn: " + Scalar(c, "SELECT t FROM vn LIMIT 1"));
                Console.WriteLine("LOWER('CÔNG TY ĐÔNG Á') = " + Scalar(c, "SELECT lower('CÔNG TY ĐÔNG Á')") + " (chỉ hạ chữ ASCII)");
                Console.WriteLine("LIKE không phân biệt hoa/thường với chữ có dấu: 'công ty đông á' khớp " + Scalar(c, "SELECT COUNT(*) FROM vn WHERE t LIKE 'công ty đông á'") + "/3 dòng");
                Console.WriteLine("LIKE ASCII 'cong ty dong a' khớp " + Scalar(c, "SELECT COUNT(*) FROM vn WHERE t LIKE 'cong ty dong a'") + "/3 dòng (không bỏ dấu)");
                Console.WriteLine("=> Tìm không dấu cần cột đã bỏ dấu (search_text) do ứng dụng tạo; ORDER BY tên cần sắp ở ứng dụng hoặc cột đã bỏ dấu.");
                Exec(c, "DROP TABLE vn");
            }
        }

        // ---- 4. Đọc/ghi đồng thời (WAL) ----

        private static void Concurrency(string db)
        {
            Console.WriteLine("== 4. Đồng thời: một người ghi, nhiều người đọc (WAL) ==");
            var stop = false;
            long reads = 0;
            long maxReadMs = 0;
            var errors = new List<string>();
            var reader = Task.Run(() =>
            {
                try
                {
                    using (var c = Open(db))
                    {
                        while (!Volatile.Read(ref stop))
                        {
                            var sw = Stopwatch.StartNew();
                            Scalar(c, "SELECT COUNT(*) FROM invoice WHERE direction=2");
                            var ms = sw.ElapsedMilliseconds;
                            if (ms > maxReadMs) maxReadMs = ms;
                            Interlocked.Increment(ref reads);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add("reader: " + ex.Message);
                }
            });

            var swWrite = Stopwatch.StartNew();
            var written = 0;
            try
            {
                using (var c = Open(db))
                {
                    for (var batch = 0; batch < 50; batch++)
                    {
                        using (var tx = c.BeginTransaction())
                        {
                            for (var i = 0; i < 200; i++)
                            {
                                Exec(c, "INSERT INTO invoice(id,business_key,direction,issue_date,total_payment) VALUES('" + Guid.NewGuid() + "','C|" + batch + "|" + i + "|" + Guid.NewGuid() + "',2,20260101,1)");
                                written++;
                            }

                            tx.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add("writer: " + ex.Message);
            }

            Volatile.Write(ref stop, true);
            reader.Wait();
            Console.WriteLine("Ghi " + written + " dòng (50 giao dịch) trong " + swWrite.ElapsedMilliseconds + " ms trong lúc đọc " + reads + " lần; lần đọc chậm nhất " + maxReadMs + " ms; lỗi: " + (errors.Count == 0 ? "không" : string.Join("; ", errors)));
        }

        // ---- 5. Sao lưu bằng backup API ----

        private static void Backup(string db, string target)
        {
            Console.WriteLine("== 5. Backup API (NFR-REL-003) ==");
            using (var src = Open(db))
            using (var dst = new SQLiteConnection("Data Source=" + target + ";Version=3;"))
            {
                dst.Open();
                var sw = Stopwatch.StartNew();
                src.BackupDatabase(dst, "main", "main", -1, null, 0);
                Console.WriteLine("BackupDatabase: " + sw.ElapsedMilliseconds + " ms, file " + (new FileInfo(target).Length / 1024 / 1024) + " MB");
                Console.WriteLine("Bản sao: integrity_check = " + Scalar(dst, "PRAGMA integrity_check") + ", số dòng = " + Scalar(dst, "SELECT COUNT(*) FROM invoice") + " (nguồn " + Scalar(src, "SELECT COUNT(*) FROM invoice") + ")");
            }

            // Sao lưu khi đang có người ghi: bản sao phải nhất quán.
            var stop = false;
            var writer = Task.Run(() =>
            {
                using (var c = Open(db))
                {
                    var k = 0;
                    while (!Volatile.Read(ref stop))
                    {
                        using (var tx = c.BeginTransaction())
                        {
                            for (var i = 0; i < 50; i++) Exec(c, "INSERT INTO invoice(id,business_key,direction,issue_date,total_payment) VALUES('" + Guid.NewGuid() + "','W|" + (k++) + "|" + Guid.NewGuid() + "',2,20260101,1)");
                            tx.Commit();
                        }
                    }
                }
            });

            Thread.Sleep(200);
            var live = target + ".live";
            using (var src = Open(db))
            using (var dst = new SQLiteConnection("Data Source=" + live + ";Version=3;"))
            {
                dst.Open();
                src.BackupDatabase(dst, "main", "main", -1, null, 0);
                Console.WriteLine("Sao lưu khi đang ghi: integrity_check = " + Scalar(dst, "PRAGMA integrity_check") + ", số dòng = " + Scalar(dst, "SELECT COUNT(*) FROM invoice"));
            }

            Volatile.Write(ref stop, true);
            writer.Wait();
        }

        // ---- 6. Migration ----

        private static void Migration(string db)
        {
            Console.WriteLine("== 6. Migration bằng user_version ==");
            using (var c = Open(db))
            {
                var v = Convert.ToInt32(Scalar(c, "PRAGMA user_version"));
                using (var tx = c.BeginTransaction())
                {
                    if (v < 2)
                    {
                        Exec(c, "ALTER TABLE invoice ADD COLUMN currency TEXT");
                        Exec(c, "PRAGMA user_version = 2");
                    }

                    tx.Commit();
                }

                Console.WriteLine("v" + v + " -> v" + Scalar(c, "PRAGMA user_version") + ", cột currency: " + Scalar(c, "SELECT COUNT(*) FROM pragma_table_info('invoice') WHERE name='currency'"));
            }
        }

        // ---- 7. Crash giữa giao dịch ----

        private static void Crash(string db)
        {
            Console.WriteLine("== 7. Crash: giết tiến trình giữa chừng, 8 lần ==");
            var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            using (var c = Open(db))
            {
                Exec(c, "CREATE TABLE t(id INTEGER PRIMARY KEY, batch INTEGER, payload TEXT)");
            }

            var bad = 0;
            for (var round = 1; round <= 8; round++)
            {
                var psi = new ProcessStartInfo(exe, "crash-writer \"" + db + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    Thread.Sleep(300 + round * 130);
                    try
                    {
                        p.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    p.WaitForExit();
                }

                using (var c = Open(db))
                {
                    var check = Scalar(c, "PRAGMA integrity_check").ToString();
                    var partial = Convert.ToInt64(Scalar(c, "SELECT COUNT(*) FROM (SELECT batch, COUNT(*) AS n FROM t GROUP BY batch HAVING n <> 100)"));
                    if (check != "ok" || partial != 0) bad++;
                    Console.WriteLine("Vòng " + round + ": integrity_check = " + check + ", giao dịch dở = " + partial + ", tổng dòng = " + Scalar(c, "SELECT COUNT(*) FROM t"));
                }
            }

            Console.WriteLine(bad == 0 ? "Không có hỏng dữ liệu hay giao dịch dở sau 8 lần bị giết." : "CÓ " + bad + " vòng bất thường!");
        }

        private static int CrashWriter(string db)
        {
            using (var c = Open(db))
            {
                var batch = Convert.ToInt32(Scalar(c, "SELECT IFNULL(MAX(batch),0) FROM t")) + 1;
                while (true)
                {
                    using (var tx = c.BeginTransaction())
                    {
                        for (var i = 0; i < 100; i++) Exec(c, "INSERT INTO t(batch,payload) VALUES(" + batch + ",'" + new string('x', 200) + "')");
                        tx.Commit();
                    }

                    batch++;
                }
            }
        }

        // ---- 8. Đường dẫn ----

        private static void Paths(string dir)
        {
            Console.WriteLine("== 8. Đường dẫn: tiếng Việt, khoảng trắng, dài ==");
            foreach (var sub in new[] { "Dữ liệu hóa đơn điện tử\\0101234567 Công ty Đông Á", new string('a', 60) + "\\" + new string('b', 60) + "\\" + new string('c', 60) })
            {
                try
                {
                    var folder = Path.Combine(dir, sub);
                    Directory.CreateDirectory(folder);
                    var path = Path.Combine(folder, "company.db");
                    using (var c = Open(path))
                    {
                        Exec(c, "CREATE TABLE x(a)");
                        Exec(c, "INSERT INTO x VALUES(1)");
                    }

                    Console.WriteLine("OK (" + path.Length + " ký tự): " + (path.Length > 80 ? path.Substring(0, 40) + "…" : path));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("LỖI: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
    }
}
