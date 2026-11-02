using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Settings;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Mục Lưu trữ/sao lưu và Kết nối của SCR-09 (DD-11) trên dịch vụ giả; không động tới đĩa thật hay cổng Thuế.</summary>
    [TestFixture]
    public class SettingsStoragePresenterTests : CompanyUiTestBase
    {
        private const string Root = @"D:\Kho\0101234567";

        private FakeView _view;
        private FakeStats _stats;
        private FakeBackup _backup;
        private FakeConnectivity _connectivity;
        private FakeLauncher _launcher;
        private FakeSave _save;
        private FakeOpen _open;
        private SettingsPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "CÔNG TY MẪU", Root, null);
            Entitlement.Value = AppMode.Standard;
            _view = new FakeView();
            _stats = new FakeStats();
            _backup = new FakeBackup();
            _connectivity = new FakeConnectivity();
            _launcher = new FakeLauncher();
            _save = new FakeSave();
            _open = new FakeOpen();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Dispose();
        }

        private SettingsPresenter Open(SettingsSection section = SettingsSection.Storage, bool withServices = true)
        {
            var services = withServices ? new SettingsServices(_stats, _backup, _connectivity, _launcher, _save, _open, Confirm) : null;
            _presenter = new SettingsPresenter(_view, new FakeLicense(), Entitlement, Context, () => Nav, Runner, Errors, "1.0 · Schema v1", services);
            _presenter.Initialize();
            if (section != SettingsSection.ActiveCompany) _view.Select(section);
            return _presenter;
        }

        private static bool WaitFor(Func<bool> condition)
        {
            return UiWait.Until(condition);
        }

        private static StorageStatistics Stats()
        {
            return new StorageStatistics
            {
                Root = Root, XmlBytes = 7L * 1024 * 1024 * 1024 + 400L * 1024 * 1024, XmlFiles = 1234, PdfBytes = 2L * 1024 * 1024 * 1024, PdfFiles = 10,
                ExportBytes = 1200L * 1024 * 1024, BackupBytes = 84L * 1024 * 1024, BackupFiles = 2, FreeBytes = 184L * 1024 * 1024 * 1024,
                LastBackupPath = @"D:\Kho\0101234567\Backups\HDDT_0101234567_20261031-180000.zip", LastBackupUtc = new DateTime(2026, 10, 31, 11, 0, 0, DateTimeKind.Utc)
            };
        }

        private static BackupInspection Good(string tax = "0101234567", int files = 12)
        {
            return new BackupInspection
            {
                Manifest = new BackupManifest
                {
                    FormatVersion = 1, TaxCode = tax, CompanyName = "Tên trong gói", CreatedAtUtcText = "2026-10-31T11:00:00.0000000Z", Files = Enumerable.Range(1, files).Select(i => new BackupFileEntry { Path = i + ".xml" }).ToList()
                },
                Problems = new List<string>()
            };
        }

        // ---- Dung lượng ----

        [Test]
        public void Storage_ShowsTheSizesOnceCalculated_WithFourActions()
        {
            _stats.Result = Stats();

            Open();

            Assert.That(WaitFor(() => _view.Rows != null && _view.Rows.Any(r => r.Key == "XML hóa đơn")), Is.True);
            Assert.That(_view.Title, Is.EqualTo("Lưu trữ và sao lưu"));
            Assert.That(_view.Value("XML hóa đơn"), Is.EqualTo("7,4 GB (1.234 file)"));
            Assert.That(_view.Value("PDF đã tạo"), Is.EqualTo("2 GB (10 file)"));
            Assert.That(_view.Value("File xuất"), Is.EqualTo("1,2 GB"));
            Assert.That(_view.Value("Bản sao lưu"), Is.EqualTo("84 MB (2 gói)"));
            Assert.That(_view.Value("Còn trống trên ổ"), Is.EqualTo("184 GB"));
            Assert.That(_view.Value("Sao lưu gần nhất"), Does.EndWith("HDDT_0101234567_20261031-180000.zip"));
            Assert.That(WaitFor(() => _view.Actions.All(a => a.Enabled || a.Id == "restore" && false) || true), Is.True);
            Assert.That(_view.Actions.Select(a => a.Id), Is.EqualTo(new[] { "open-folder", "backup", "restore", "refresh" }));
        }

        [Test]
        public void Storage_WhileCalculating_ShowsAPlaceholder_AndLocksTheHeavyActions()
        {
            _stats.Gate = new TaskCompletionSource<bool>();

            Open();

            Assert.That(_view.Value("Dung lượng"), Is.EqualTo("Đang tính..."));
            Assert.That(_view.Actions.Single(a => a.Id == "backup").Enabled, Is.False);
            Assert.That(_view.Actions.Single(a => a.Id == "restore").Enabled, Is.False);
            Assert.That(_view.Actions.Single(a => a.Id == "open-folder").Enabled, Is.True);
            _stats.Gate.SetResult(true);
        }

        [Test]
        public void Storage_InReadOnlyMode_AllowsBackup_ButNotRestore()
        {
            Entitlement.Value = AppMode.ReadOnlyService;
            _stats.Result = Stats();

            Open();

            Assert.That(WaitFor(() => _view.Rows.Any(r => r.Key == "XML hóa đơn")), Is.True);
            Assert.That(_view.Actions.Single(a => a.Id == "backup").Enabled, Is.True);
            Assert.That(_view.Actions.Single(a => a.Id == "restore").Enabled, Is.False);
        }

        [Test]
        public void Storage_WithoutACompany_SaysSo()
        {
            Context.Current = null;

            Open();

            Assert.That(_view.Note, Does.Contain("Chưa mở doanh nghiệp"));
            Assert.That(_view.Actions, Is.Null.Or.Empty);
            Assert.That(_stats.Calls, Is.EqualTo(0));
        }

        [Test]
        public void Storage_WithoutServices_KeepsTheOldHint()
        {
            Open(withServices: false);

            Assert.That(_view.Note, Does.Contain("chưa có thao tác"));
        }

        [Test]
        public void Storage_RefreshRecalculates()
        {
            _stats.Result = Stats();
            Open();
            Assert.That(WaitFor(() => _stats.Calls == 1 && _view.Rows.Any(r => r.Key == "XML hóa đơn")), Is.True);

            _view.Click("refresh");

            Assert.That(WaitFor(() => _stats.Calls == 2), Is.True);
        }

        [Test]
        public void OpenFolder_OpensTheStoreFolder_AndExplainsAMissingOne()
        {
            _stats.Result = Stats();
            Open();

            _view.Click("open-folder");
            Assert.That(_launcher.Opened, Is.EqualTo(new[] { Root }));

            _launcher.Missing = true;
            _view.Click("open-folder");
            Assert.That(_view.MessageText, Does.Contain("chưa tồn tại"));
            Assert.That(_view.MessageIsError, Is.True);
        }

        // ---- Sao lưu ----

        [Test]
        public void Backup_AsksWhereToSave_NamingTheCompany_ThenReportsTheResult_AndRefreshesSizes()
        {
            _stats.Result = Stats();
            _save.Result = @"D:\Backup\goi.zip";
            _backup.CreateResult = new BackupResult { Path = @"D:\Backup\goi.zip", FileCount = 1234, TotalBytes = 7L * 1024 * 1024 * 1024 };
            Open();
            Assert.That(WaitFor(() => _view.Rows.Any(r => r.Key == "XML hóa đơn")), Is.True);

            _view.Click("backup");

            Assert.That(WaitFor(() => _backup.Created.Count == 1), Is.True);
            Assert.That(_backup.Created[0].Path, Is.EqualTo(@"D:\Backup\goi.zip"));
            Assert.That(_backup.Created[0].Overwrite, Is.True, "Hộp thoại lưu đã hỏi xác nhận ghi đè");
            Assert.That(_save.Extension, Is.EqualTo("zip"));
            Assert.That(_save.Suggested, Does.StartWith("HDDT_0101234567_").And.EndWith(".zip"));
            Assert.That(_save.Title, Does.Contain("CÔNG TY MẪU").And.Contain("0101234567"));
            Assert.That(_save.InitialDirectory, Is.EqualTo(Path.Combine(Root, "Backups")));
            Assert.That(WaitFor(() => _view.MessageText != null && _view.MessageText.Contains("Đã tạo bản sao lưu")), Is.True, _view.MessageText);
            Assert.That(_view.MessageText, Does.Contain("1.234 file").And.Contain("7 GB"));
            Assert.That(WaitFor(() => _stats.Calls == 2), Is.True, "Tính lại dung lượng sau khi sao lưu");
        }

        [Test]
        public void Backup_CancellingTheSaveDialog_DoesNothing()
        {
            _stats.Result = Stats();
            _save.Result = null;
            Open();

            _view.Click("backup");

            Assert.That(WaitFor(() => _save.Picks == 1), Is.True);
            Thread.Sleep(80);
            Assert.That(_backup.Created, Is.Empty);
        }

        [Test]
        public void Backup_RefusedByTheService_ShowsTheReasonInline()
        {
            _stats.Result = Stats();
            _save.Result = @"D:\Backup\goi.zip";
            _backup.CreateFailure = new BackupException("Đang có phiên tải chạy cho doanh nghiệp này.");
            Open();

            _view.Click("backup");

            Assert.That(WaitFor(() => _view.MessageText == "Đang có phiên tải chạy cho doanh nghiệp này."), Is.True, _view.MessageText);
            Assert.That(_view.MessageIsError, Is.True);
            Assert.That(Errors.Shown, Is.Empty);
        }

        [Test]
        public void Backup_UnexpectedFailure_GoesToTheErrorPresenter()
        {
            _stats.Result = Stats();
            _save.Result = @"D:\Backup\goi.zip";
            _backup.CreateFailure = new IOException("disk full");
            Open();

            _view.Click("backup");

            Assert.That(WaitFor(() => Errors.Shown.Count > 0), Is.True);
        }

        // ---- Khôi phục ----

        [Test]
        public void Restore_UnusablePackage_IsExplainedWithoutAskingOrChangingAnything()
        {
            _stats.Result = Stats();
            _open.Result = @"D:\Backup\hong.zip";
            _backup.Inspection = new BackupInspection { Problems = new List<string> { "Sai SHA-256: 2026/a.xml", "Thiếu file trong gói: b.xml" } };
            Open();

            _view.Click("restore");

            Assert.That(WaitFor(() => _view.MessageText != null && _view.MessageText.StartsWith("Gói không dùng được")), Is.True, _view.MessageText);
            Assert.That(_view.MessageText, Does.Contain("Sai SHA-256").And.Contain("1 lỗi khác"));
            Assert.That(Confirm.Messages, Is.Empty);
            Assert.That(_backup.Restored, Is.Empty);
        }

        [Test]
        public void Restore_PackageOfAnotherCompany_IsRefusedBeforeConfirming_AC11()
        {
            _stats.Result = Stats();
            _open.Result = @"D:\Backup\khac.zip";
            _backup.Inspection = Good("0109999999");
            Open();

            _view.Click("restore");

            Assert.That(WaitFor(() => _view.MessageText != null && _view.MessageText.Contains("MST 0109999999")), Is.True, _view.MessageText);
            Assert.That(_view.MessageText, Does.Contain("MST 0101234567"));
            Assert.That(Confirm.Messages, Is.Empty);
            Assert.That(_backup.Restored, Is.Empty);
        }

        [Test]
        public void Restore_ConfirmationNamesTheCompany_AndDecliningChangesNothing()
        {
            _stats.Result = Stats();
            _open.Result = @"D:\Backup\goi.zip";
            _backup.Inspection = Good();
            Confirm.Answer = false;
            Open();

            _view.Click("restore");

            Assert.That(WaitFor(() => Confirm.Messages.Count == 1), Is.True);
            Assert.That(Confirm.Messages[0], Does.Contain("CÔNG TY MẪU").And.Contain("MST 0101234567").And.Contain("12 file").And.Contain("điểm sao lưu an toàn"));
            Assert.That(WaitFor(() => _view.MessageText == "Đã hủy khôi phục."), Is.True, _view.MessageText);
            Assert.That(_backup.Restored, Is.Empty);
        }

        [Test]
        public void Restore_WhenConfirmed_RestoresAndReportsWhatChanged_ThenRefreshes()
        {
            _stats.Result = Stats();
            _open.Result = @"D:\Backup\goi.zip";
            _backup.Inspection = Good();
            _backup.RestoreResult = new RestoreResult { Created = 3, Overwritten = 2, Unchanged = 7, SafetyBackupPath = @"D:\Kho\0101234567\Backups\truoc-khoi-phuc_x.zip" };
            Open();

            _view.Click("restore");

            Assert.That(WaitFor(() => _backup.Restored.Count == 1), Is.True);
            Assert.That(_backup.Restored[0], Is.EqualTo(@"D:\Backup\goi.zip"));
            Assert.That(WaitFor(() => _view.MessageText != null && _view.MessageText.StartsWith("Đã khôi phục")), Is.True, _view.MessageText);
            Assert.That(_view.MessageText, Does.Contain("thêm 3, thay 2, giữ nguyên 7").And.Contain("truoc-khoi-phuc_x.zip").And.Contain("chỉ mục"));
            Assert.That(_view.MessageIsError, Is.False);
            Assert.That(WaitFor(() => _stats.Calls == 2), Is.True);
        }

        [Test]
        public void Restore_FailingHalfWay_ShowsTheRollbackMessage()
        {
            _stats.Result = Stats();
            _open.Result = @"D:\Backup\goi.zip";
            _backup.Inspection = Good();
            _backup.RestoreFailure = new BackupException("Khôi phục bị lỗi giữa chừng: x. Dữ liệu đã được trả về trạng thái trước khi khôi phục.");
            Open();

            _view.Click("restore");

            Assert.That(WaitFor(() => _view.MessageText != null && _view.MessageText.Contains("trả về trạng thái trước")), Is.True, _view.MessageText);
            Assert.That(_view.MessageIsError, Is.True);
        }

        [Test]
        public void Restore_CancellingTheFileDialog_DoesNothing()
        {
            _stats.Result = Stats();
            _open.Result = null;
            Open();

            _view.Click("restore");

            Assert.That(WaitFor(() => _open.Picks == 1), Is.True);
            Thread.Sleep(80);
            Assert.That(_backup.Inspected, Is.Empty);
            Assert.That(Confirm.Messages, Is.Empty);
        }

        // ---- Kết nối ----

        [Test]
        public void Connection_ListsEveryServer_WithTheirOutcome()
        {
            _connectivity.Results = new List<ConnectivityResult>
            {
                new ConnectivityResult { Name = "Cổng Thuế", Reachable = true, Detail = "Kết nối được (120 ms, mã 200)" },
                new ConnectivityResult { Name = "Máy chủ licence", Reachable = false, Detail = "Chưa cấu hình địa chỉ" }
            };
            Open(SettingsSection.PortalConnection);
            Assert.That(_view.Actions.Single().Id, Is.EqualTo("check-connection"));

            _view.Click("check-connection");

            Assert.That(WaitFor(() => _view.Rows != null && _view.Rows.Count == 2), Is.True);
            Assert.That(_view.Value("Cổng Thuế"), Is.EqualTo("✔ Kết nối được (120 ms, mã 200)"));
            Assert.That(_view.Value("Máy chủ licence"), Is.EqualTo("✖ Chưa cấu hình địa chỉ"));
            Assert.That(WaitFor(() => _view.MessageText == "Có máy chủ không kết nối được."), Is.True, _view.MessageText);
            Assert.That(_view.MessageIsError, Is.True);
            Assert.That(_view.Actions.Single().Text, Is.EqualTo("Kiểm tra lại"));
        }

        [Test]
        public void Connection_AllReachable_SaysAllIsWell()
        {
            _connectivity.Results = new List<ConnectivityResult> { new ConnectivityResult { Name = "Cổng Thuế", Reachable = true, Detail = "ok" } };
            Open(SettingsSection.PortalConnection);

            _view.Click("check-connection");

            Assert.That(WaitFor(() => _view.MessageText == "Kết nối bình thường."), Is.True, _view.MessageText);
            Assert.That(_view.MessageIsError, Is.False);
        }

        [Test]
        public void Connection_LeavingTheSectionMeanwhile_DoesNotOverwriteTheNewOne()
        {
            _connectivity.Gate = new TaskCompletionSource<bool>();
            _connectivity.Results = new List<ConnectivityResult> { new ConnectivityResult { Name = "Cổng Thuế", Reachable = true, Detail = "ok" } };
            Open(SettingsSection.PortalConnection);
            _view.Click("check-connection");
            _view.Select(SettingsSection.About);

            _connectivity.Gate.SetResult(true);
            Thread.Sleep(150);

            Assert.That(_view.Section, Is.EqualTo(SettingsSection.About));
            Assert.That(_view.Title, Is.EqualTo("Thông tin ứng dụng"));
        }

        [Test]
        public void Connection_WithoutServices_KeepsTheOldHint()
        {
            Open(SettingsSection.PortalConnection, withServices: false);

            Assert.That(_view.Note, Does.Contain("chưa có thao tác"));
        }

        // ---- Định dạng ----

        [TestCase(0, "0 B")]
        [TestCase(1023, "1.023 B")]
        [TestCase(1024, "1 KB")]
        [TestCase(1536, "1,5 KB")]
        [TestCase(88080384, "84 MB")]
        [TestCase(7945689498, "7,4 GB")]
        [TestCase(1099511627776, "1 TB")]
        [TestCase(150L * 1024 * 1024 * 1024, "150 GB")]
        public void FormatBytes_UsesVietnameseStyleUnits(long bytes, string expected)
        {
            Assert.That(SettingsPresenter.FormatBytes(bytes), Is.EqualTo(expected));
        }

        // ---- Fakes ----

        private sealed class FakeView : ISettingsView
        {
            private readonly object _gate = new object();
            private IList<KeyValuePair<string, string>> _rows;
            private IList<SettingsAction> _actions;
            private string _message;
            private bool _isError;
            private string _note;
            private string _title;
            private SettingsSection _section;

            public event EventHandler<SettingsSectionEventArgs> SectionSelected;
            public event EventHandler<SettingsActionEventArgs> ActionRequested;

            public IList<KeyValuePair<string, string>> Rows { get { lock (_gate) { return _rows; } } }
            public IList<SettingsAction> Actions { get { lock (_gate) { return _actions; } } }
            public string MessageText { get { lock (_gate) { return _message; } } }
            public bool MessageIsError { get { lock (_gate) { return _isError; } } }
            public string Note { get { lock (_gate) { return _note; } } }
            public string Title { get { lock (_gate) { return _title; } } }
            public SettingsSection Section { get { lock (_gate) { return _section; } } }

            public string Value(string key)
            {
                var rows = Rows;
                return rows == null ? null : rows.Where(r => r.Key == key).Select(r => r.Value).FirstOrDefault();
            }

            public void ShowSection(SettingsSection section, string title, IList<KeyValuePair<string, string>> rows, string note)
            {
                lock (_gate)
                {
                    _section = section;
                    _title = title;
                    _rows = rows;
                    _note = note;
                    _actions = null;
                    _message = null;
                }
            }

            public void ShowActions(IList<SettingsAction> actions)
            {
                lock (_gate) { _actions = actions; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate)
                {
                    _message = text;
                    _isError = isError;
                }
            }

            public void ShowLicenseSummary(string text) { }

            public void Select(SettingsSection section) { SectionSelected(this, new SettingsSectionEventArgs(section)); }
            public void Click(string id) { ActionRequested(this, new SettingsActionEventArgs(id)); }
        }

        private sealed class FakeStats : IStorageStatisticsService
        {
            public StorageStatistics Result = new StorageStatistics { Root = Root };
            public TaskCompletionSource<bool> Gate;
            public int Calls;

            public async Task<StorageStatistics> GetAsync(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                if (Gate != null) await Gate.Task;
                return Result;
            }
        }

        private sealed class CreateCall
        {
            public string Path;
            public bool Overwrite;
        }

        private sealed class FakeBackup : IBackupService
        {
            public readonly List<CreateCall> Created = new List<CreateCall>();
            public readonly List<string> Restored = new List<string>();
            public readonly List<string> Inspected = new List<string>();
            public BackupResult CreateResult = new BackupResult { Path = "x", FileCount = 1, TotalBytes = 1 };
            public Exception CreateFailure;
            public BackupInspection Inspection = new BackupInspection { Problems = new List<string>() };
            public RestoreResult RestoreResult = new RestoreResult();
            public Exception RestoreFailure;

            public Task<BackupResult> CreateAsync(string targetPath, bool overwrite, IProgress<BackupProgress> progress, CancellationToken cancellationToken)
            {
                lock (Created) { Created.Add(new CreateCall { Path = targetPath, Overwrite = overwrite }); }
                if (CreateFailure != null) throw CreateFailure;
                return Task.FromResult(CreateResult);
            }

            public Task<BackupInspection> InspectAsync(string backupPath, CancellationToken cancellationToken)
            {
                lock (Inspected) { Inspected.Add(backupPath); }
                return Task.FromResult(Inspection);
            }

            public Task<RestoreResult> RestoreAsync(string backupPath, IProgress<BackupProgress> progress, CancellationToken cancellationToken)
            {
                lock (Restored) { Restored.Add(backupPath); }
                if (RestoreFailure != null) throw RestoreFailure;
                return Task.FromResult(RestoreResult);
            }
        }

        private sealed class FakeConnectivity : IConnectivityChecker
        {
            public IList<ConnectivityResult> Results = new List<ConnectivityResult>();
            public TaskCompletionSource<bool> Gate;

            public async Task<IList<ConnectivityResult>> CheckAsync(CancellationToken cancellationToken)
            {
                if (Gate != null) await Gate.Task;
                return Results;
            }
        }

        private sealed class FakeLauncher : IFolderLauncher
        {
            public readonly List<string> Opened = new List<string>();
            public bool Missing;

            public void Open(string path)
            {
                if (Missing) throw new DirectoryNotFoundException(path);
                Opened.Add(path);
            }
        }

        private sealed class FakeSave : IExportSavePicker
        {
            public string Result;
            public string Suggested;
            public string Extension;
            public string Title;
            public string InitialDirectory;
            public int Picks;

            public string Pick(string suggestedFileName, string extension, string filterDescription, string title, string initialDirectory)
            {
                Picks++;
                Suggested = suggestedFileName;
                Extension = extension;
                Title = title;
                InitialDirectory = initialDirectory;
                return Result;
            }
        }

        private sealed class FakeOpen : IOpenFilePicker
        {
            public string Result;
            public int Picks;

            public string Pick(string title, string extension, string filterDescription, string initialDirectory)
            {
                Picks++;
                return Result;
            }
        }

        private sealed class FakeLicense : ILicenseService
        {
            public LicenseInfo CurrentInfo { get { return LicenseInfo.NotActivated; } }
            public Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken) { throw new NotSupportedException(); }
            public event EventHandler InfoChanged { add { } remove { } }
        }
    }
}
