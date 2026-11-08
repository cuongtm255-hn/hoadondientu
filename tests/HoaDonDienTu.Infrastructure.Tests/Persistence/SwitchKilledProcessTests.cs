using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Companies;
using HoaDonDienTu.Infrastructure.Persistence.Sqlite;
using HoaDonDienTu.Infrastructure.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Persistence
{
    /// <summary>
    /// NFR-REL-006, phần "đóng tiến trình giữa lúc chuyển": một TIẾN TRÌNH THẬT (PowerShell cùng số bit) giữ giao dịch ghi dở trên <c>company.db</c> của một kho
    /// rồi bị giết (TerminateProcess, như mất điện hay Task Manager). Sau đó ứng dụng mở lại: cả hai kho phải nguyên vẹn, giao dịch dở bị bỏ, chuyển qua lại chỉ có một kho mở.
    /// Dữ liệu GIẢ, không gọi cổng Thuế.
    /// </summary>
    [TestFixture]
    [Category("MockData")]
    [Category("Reliability")]
    public class SwitchKilledProcessTests
    {
        private string _base;
        private CompanyProfile _a;
        private CompanyProfile _b;
        private JsonCompanyCatalog _catalog;
        private CompanyContextManager _manager;
        private CompanyDatabaseProvider _provider;
        private CompanySwitchService _switch;

        [SetUp]
        public async Task Prepare()
        {
            _base = Path.Combine(Path.GetTempPath(), "hddt-kill-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_base);
            var storage = new FileSystemCompanyStorage();
            _catalog = new JsonCompanyCatalog(Path.Combine(_base, "companies.json"));

            _a = await Register(storage, "0101234567", "Kho A (mô phỏng)");
            _b = await Register(storage, "0109213133", "Kho B (mô phỏng)");

            _manager = new CompanyContextManager(new CompanyStorageOpener(_catalog, storage, new FixedClock(), new NullLogger()));
            _provider = new CompanyDatabaseProvider(_manager);
            _switch = new CompanySwitchService(_catalog, _manager, new NoJobs(), new InMemoryPortalSessionStore(), new AllowAll(), new NullLogger());

            // Mở từng kho một lần để cơ sở dữ liệu của cả hai được tạo đủ lược đồ.
            await _manager.OpenAsync(_a, CancellationToken.None);
            Touch();
            await _switch.SwitchAsync(_b.Id, ActiveJobResolution.NotRequired, CancellationToken.None);
            Touch();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_manager != null) _manager.CloseCurrent();
            SQLiteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(_base, true);
            }
            catch (IOException)
            {
            }
        }

        private async Task<CompanyProfile> Register(FileSystemCompanyStorage storage, string mst, string name)
        {
            var root = Path.Combine(_base, "Kho", mst);
            var id = CompanyId.NewId();
            storage.Initialize(root, id, TaxCode.Parse(mst));
            var profile = new CompanyProfile(id, name, TaxCode.Parse(mst));
            await _catalog.AddAsync(new CompanyCatalogItem(id, profile.TaxCode, name, root, null, null, 0, 0, CompanyCatalogStatus.Active), CancellationToken.None);
            return profile;
        }

        private string DatabasePath(CompanyProfile company)
        {
            return Path.Combine(_base, "Kho", company.TaxCode.Value, CompanyDatabaseProvider.RelativePath);
        }

        private void Touch()
        {
            using (var connection = _provider.Current().Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT count(*) FROM sqlite_master";
                command.ExecuteScalar();
            }
        }

        private bool HasMarkerTable()
        {
            using (var connection = _provider.Current().Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name = 'killed_marker'";
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        /// <summary>Chạy một tiến trình con mở <paramref name="databasePath"/>, bắt đầu giao dịch ghi và treo; trả về tiến trình đang giữ khóa.</summary>
        private Process StartWriterThatHoldsTheDatabase(string databasePath)
        {
            var ready = Path.Combine(_base, "ready-" + Guid.NewGuid().ToString("N") + ".txt");
            var sqliteDll = typeof(SQLiteConnection).Assembly.Location;
            var script =
                "Add-Type -Path '" + sqliteDll.Replace("'", "''") + "'; " +
                "$c = New-Object System.Data.SQLite.SQLiteConnection('Data Source=" + databasePath.Replace("'", "''") + ";Pooling=False'); $c.Open(); " +
                "$cmd = $c.CreateCommand(); " +
                "$cmd.CommandText = 'BEGIN IMMEDIATE; CREATE TABLE killed_marker(x INTEGER); INSERT INTO killed_marker VALUES (1);'; [void]$cmd.ExecuteNonQuery(); " +
                "Set-Content -Path '" + ready.Replace("'", "''") + "' -Value ok; Start-Sleep -Seconds 600";

            var systemDir = Environment.Is64BitProcess ? "System32" : "SysWOW64";
            var powershell = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", systemDir, @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            var process = Process.Start(info);
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!File.Exists(ready))
            {
                if (process.HasExited)
                {
                    Assert.Fail("Tiến trình con thoát sớm: " + process.StandardError.ReadToEnd());
                }

                if (DateTime.UtcNow > deadline)
                {
                    process.Kill();
                    Assert.Fail("Tiến trình con không giữ được giao dịch ghi trong 60 giây.");
                }

                Thread.Sleep(100);
            }

            return process;
        }

        /// <summary>Đối chứng: tiến trình con thật sự đang giữ khóa ghi (nếu không thì bài thử chẳng chứng minh gì).</summary>
        private static void AssertDatabaseIsLocked(string databasePath)
        {
            var builder = new SQLiteConnectionStringBuilder { DataSource = databasePath, Pooling = false, DefaultTimeout = 1 };
            using (var connection = new SQLiteConnection(builder.ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "BEGIN IMMEDIATE";
                    Assert.Throws<SQLiteException>(() => command.ExecuteNonQuery(), "Tiến trình con phải đang giữ khóa ghi");
                }
            }
        }

        [TestCase("A")]
        [TestCase("B")]
        public async Task AProcessKilledMidWrite_LeavesBothStoresIntact_AndTheNextStartSwitchesNormally(string holder)
        {
            var killedStore = holder == "A" ? _a : _b;
            var otherStore = holder == "A" ? _b : _a;

            // Ứng dụng "chết": ngoài tiến trình này, một tiến trình khác đang ghi dở vào kho rồi bị giết.
            _manager.CloseCurrent();
            SQLiteConnection.ClearAllPools();
            var writer = StartWriterThatHoldsTheDatabase(DatabasePath(killedStore));
            AssertDatabaseIsLocked(DatabasePath(killedStore));

            try
            {
                writer.Kill();
                writer.WaitForExit(10000);
            }
            finally
            {
                writer.Dispose();
            }

            // Lần mở sau: chuyển qua cả hai kho; mỗi lần chỉ có một context.
            var first = await _switch.SwitchAsync(killedStore.Id, ActiveJobResolution.NotRequired, CancellationToken.None);
            Assert.That(first.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
            Assert.That(_manager.Current.CompanyId, Is.EqualTo(killedStore.Id));
            Assert.That(CompanyDatabaseInitializer.IsIntact(_provider.Current()), Is.True, "Kho bị giết giữa lúc ghi vẫn nguyên vẹn");
            Assert.That(HasMarkerTable(), Is.False, "Giao dịch ghi dở của tiến trình đã chết phải bị bỏ");

            var second = await _switch.SwitchAsync(otherStore.Id, ActiveJobResolution.NotRequired, CancellationToken.None);
            Assert.That(second.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
            Assert.That(_manager.Current.CompanyId, Is.EqualTo(otherStore.Id));
            Assert.That(CompanyDatabaseInitializer.IsIntact(_provider.Current()), Is.True);

            var back = await _switch.SwitchAsync(killedStore.Id, ActiveJobResolution.NotRequired, CancellationToken.None);
            Assert.That(back.Outcome, Is.EqualTo(CompanySwitchOutcome.Switched));
            Assert.That(HasMarkerTable(), Is.False);
        }

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get { return new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc); } }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private sealed class NoJobs : IActiveJobService
        {
            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(false); }
            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken) { return Task.FromResult(0); }
        }

        private sealed class AllowAll : IEntitlementService
        {
            public AppMode CurrentMode { get { return AppMode.Standard; } }
            public bool IsAllowed(AppFeature feature) { return true; }
            public void Demand(AppFeature feature) { }
            public event EventHandler ModeChanged { add { } remove { } }
        }
    }
}
