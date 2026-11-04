using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Settings
{
    /// <summary>Các dịch vụ cho mục Lưu trữ/sao lưu và Kết nối của SCR-09 (DD-11). Không truyền thì hai mục chỉ hiện ghi chú.</summary>
    public sealed class SettingsServices
    {
        public SettingsServices(
            IStorageStatisticsService statistics,
            IBackupService backup,
            IConnectivityChecker connectivity,
            IFolderLauncher launcher,
            IExportSavePicker savePicker,
            IOpenFilePicker openPicker,
            IConfirmationService confirmation,
            IStorageMoveService storageMove = null,
            IFolderPicker folderPicker = null)
        {
            if (statistics == null) throw new ArgumentNullException("statistics");
            if (backup == null) throw new ArgumentNullException("backup");
            if (connectivity == null) throw new ArgumentNullException("connectivity");
            if (launcher == null) throw new ArgumentNullException("launcher");
            if (savePicker == null) throw new ArgumentNullException("savePicker");
            if (openPicker == null) throw new ArgumentNullException("openPicker");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            Statistics = statistics;
            Backup = backup;
            Connectivity = connectivity;
            Launcher = launcher;
            SavePicker = savePicker;
            OpenPicker = openPicker;
            Confirmation = confirmation;
            StorageMove = storageMove;
            FolderPicker = folderPicker;
        }

        /// <summary>Đổi thư mục kho; thiếu một trong hai thì không có nút "Đổi thư mục kho...".</summary>
        public IStorageMoveService StorageMove { get; private set; }

        public IFolderPicker FolderPicker { get; private set; }

        public IStorageStatisticsService Statistics { get; private set; }
        public IBackupService Backup { get; private set; }
        public IConnectivityChecker Connectivity { get; private set; }
        public IFolderLauncher Launcher { get; private set; }
        public IExportSavePicker SavePicker { get; private set; }
        public IOpenFilePicker OpenPicker { get; private set; }
        public IConfirmationService Confirmation { get; private set; }
    }

    /// <summary>
    /// SCR-09 Cài đặt (DD-11): thông tin doanh nghiệp, dung lượng kho, sao lưu/khôi phục, kiểm tra kết nối, thông tin ứng dụng và lối vào
    /// Bản quyền (SCR-12). Nhật ký và chẩn đoán chưa có thao tác.
    /// </summary>
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly ISettingsView _view;
        private readonly ILicenseService _license;
        private readonly IEntitlementService _entitlement;
        private readonly ICompanyContextManager _context;
        private readonly Func<INavigationService> _navigation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly string _versionText;
        private readonly SettingsServices _services;
        private SettingsSection _current = SettingsSection.ActiveCompany;

        // Kết quả sao lưu/khôi phục phải còn hiện sau khi dung lượng được tính lại (việc dựng lại mục xóa thông báo).
        private string _keptText;
        private bool _keptIsError;
        private bool _initialized;
        private bool _disposed;

        // Việc sao lưu/khôi phục đang chạy; nút "Hủy" của mục Lưu trữ hủy nó.
        private CancellationTokenSource _running;

        public SettingsPresenter(
            ISettingsView view,
            ILicenseService license,
            IEntitlementService entitlement,
            ICompanyContextManager context,
            Func<INavigationService> navigation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            string versionText,
            SettingsServices services = null)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (license == null) throw new ArgumentNullException("license");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (context == null) throw new ArgumentNullException("context");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _license = license;
            _entitlement = entitlement;
            _context = context;
            _navigation = navigation;
            _runner = runner;
            _errors = errors;
            _versionText = versionText ?? string.Empty;
            _services = services;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.SectionSelected += OnSectionSelected;
            _view.ActionRequested += OnAction;
            _view.ShowLicenseSummary(LicenseSummary());
            Show(SettingsSection.ActiveCompany);
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.SectionSelected -= OnSectionSelected;
            _view.ActionRequested -= OnAction;
        }

        public static string ModeText(AppMode mode)
        {
            switch (mode)
            {
                case AppMode.Service: return "Chế độ dịch vụ";
                case AppMode.ReadOnlyService: return "Chỉ đọc – cần gia hạn licence";
                default: return "Chế độ chuẩn";
            }
        }

        private void OnSectionSelected(object sender, SettingsSectionEventArgs e)
        {
            if (e.Section == SettingsSection.License)
            {
                _runner.RunAsync("Mở bản quyền", token => _navigation().NavigateAsync(ScreenId.License, null, token),
                    state => { if (state.Error != null) _errors.Show(state.Error); });
                return;
            }

            Show(e.Section);
        }

        private void Show(SettingsSection section)
        {
            _current = section;
            _keptText = null;
            var rows = new List<KeyValuePair<string, string>>();
            switch (section)
            {
                case SettingsSection.ActiveCompany:
                    if (_context.HasCurrent)
                    {
                        rows.Add(Row("Tên doanh nghiệp", _context.Current.DisplayName));
                        rows.Add(Row("Mã số thuế", _context.Current.TaxCode.Value));
                        rows.Add(Row("Thư mục kho", _context.Current.StorageRoot));
                        _view.ShowSection(section, "Doanh nghiệp đang hoạt động", rows, string.Empty);
                    }
                    else
                    {
                        _view.ShowSection(section, "Doanh nghiệp đang hoạt động", rows, "Chưa mở doanh nghiệp nào. Chọn doanh nghiệp để xem thông tin kho.");
                    }

                    break;
                case SettingsSection.About:
                    rows.Add(Row("Phiên bản", _versionText));
                    rows.Add(Row("Chế độ", ModeText(_entitlement.CurrentMode)));
                    rows.Add(Row("Bản quyền", LicenseSummary()));
                    _view.ShowSection(section, "Thông tin ứng dụng", rows, string.Empty);
                    break;
                case SettingsSection.Storage:
                    ShowStorage();
                    break;
                case SettingsSection.PortalConnection:
                    if (_services == null)
                    {
                        _view.ShowSection(section, "Kết nối cổng Thuế", rows, Pending());
                    }
                    else
                    {
                        _view.ShowSection(section, "Kết nối cổng Thuế", rows, "Kiểm tra máy chủ cổng Thuế và máy chủ licence có trả lời không. Không đăng nhập và không gọi API hóa đơn.");
                        _view.ShowActions(new List<SettingsAction> { new SettingsAction("check-connection", "Kiểm tra kết nối", true, true) });
                    }

                    break;
                default:
                    _view.ShowSection(section, "Nhật ký và chẩn đoán", rows, Pending());
                    break;
            }
        }

        private string LicenseSummary()
        {
            var info = _license.CurrentInfo;
            switch (_entitlement.CurrentMode)
            {
                case AppMode.Service:
                    return string.IsNullOrEmpty(info.KeyHint) ? "Đang hiệu lực" : "Đang hiệu lực " + info.KeyHint;
                case AppMode.ReadOnlyService:
                    return "Chỉ đọc – cần gia hạn";
                default:
                    return "Chưa kích hoạt";
            }
        }

        // ---- Lưu trữ và sao lưu (DD-11) ----

        private void ShowStorage()
        {
            if (_services == null)
            {
                _view.ShowSection(SettingsSection.Storage, "Lưu trữ và sao lưu", new List<KeyValuePair<string, string>>(), Pending());
                return;
            }

            if (!_context.HasCurrent)
            {
                _view.ShowSection(SettingsSection.Storage, "Lưu trữ và sao lưu", new List<KeyValuePair<string, string>>(), "Chưa mở doanh nghiệp nào. Chọn doanh nghiệp để xem dung lượng và sao lưu.");
                return;
            }

            var rows = new List<KeyValuePair<string, string>> { Row("Thư mục kho", _context.Current.StorageRoot), Row("Dung lượng", "Đang tính...") };
            _view.ShowSection(SettingsSection.Storage, "Lưu trữ và sao lưu", rows, BackupNote());
            ShowStorageActions(true);
            LoadStatistics();
        }

        private void ShowStorageActions(bool busy)
        {
            var canBackup = !busy && _entitlement.IsAllowed(AppFeature.ReadExport);
            var canRestore = !busy && _entitlement.IsAllowed(AppFeature.ManageSettings);
            var actions = new List<SettingsAction>
            {
                new SettingsAction("open-folder", "Mở thư mục kho"),
                new SettingsAction("backup", "Tạo bản sao lưu", canBackup, true),
                new SettingsAction("restore", "Khôi phục từ gói...", canRestore),
                new SettingsAction("refresh", "Tính lại dung lượng", !busy)
            };
            if (_services.StorageMove != null && _services.FolderPicker != null)
            {
                actions.Add(new SettingsAction("change-folder", "Đổi thư mục kho...", canRestore));
            }

            _view.ShowActions(actions);
        }

        private static string BackupNote()
        {
            return "Gói sao lưu gồm bản chụp cơ sở dữ liệu (chỉ mục Kho hóa đơn, lịch sử tải), các file XML/PDF của doanh nghiệp này và manifest kiểm tra SHA-256. Khôi phục chỉ nhận gói đúng MST, không xóa file ngoài gói, và tự lưu một điểm sao lưu an toàn trước khi ghi.";
        }

        private void LoadStatistics()
        {
            Run("Tính dung lượng kho", async token =>
            {
                var stats = await _services.Statistics.GetAsync(token);
                if (_disposed || _current != SettingsSection.Storage) return;
                _view.ShowSection(SettingsSection.Storage, "Lưu trữ và sao lưu", StatisticsRows(stats), BackupNote());
                ShowStorageActions(false);
                if (_keptText != null) _view.ShowMessage(_keptText, _keptIsError);
            });
        }

        public static IList<KeyValuePair<string, string>> StatisticsRows(StorageStatistics s)
        {
            var rows = new List<KeyValuePair<string, string>>
            {
                Row("Thư mục kho", s.Root),
                Row("XML hóa đơn", FormatBytes(s.XmlBytes) + " (" + s.XmlFiles.ToString("N0", Vi) + " file)"),
                Row("PDF đã tạo", FormatBytes(s.PdfBytes) + " (" + s.PdfFiles.ToString("N0", Vi) + " file)"),
                Row("Cơ sở dữ liệu", FormatBytes(s.DatabaseBytes)),
                Row("File xuất", FormatBytes(s.ExportBytes)),
                Row("Bản sao lưu", FormatBytes(s.BackupBytes) + " (" + s.BackupFiles.ToString("N0", Vi) + " gói)")
            };
            if (s.UpgradeCopyBytes > 0) rows.Add(Row("Bản sao trước nâng cấp", FormatBytes(s.UpgradeCopyBytes) + " (có thể xóa khi đã dùng ổn định)"));
            rows.Add(Row("Còn trống trên ổ", s.FreeBytes.HasValue ? FormatBytes(s.FreeBytes.Value) : "Không xác định"));
            rows.Add(Row("Sao lưu gần nhất", s.LastBackupUtc.HasValue ? s.LastBackupUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " · " + Path.GetFileName(s.LastBackupPath) : "Chưa có"));
            return rows;
        }

        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes.ToString("N0", Vi) + " B";
            string[] units = { "KB", "MB", "GB", "TB" };
            double value = bytes;
            var unit = -1;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString(value >= 100 ? "0" : "0.#", Vi) + " " + units[unit];
        }

        private void OnAction(object sender, SettingsActionEventArgs e)
        {
            if (_services == null) return;
            switch (e.ActionId)
            {
                case "open-folder": OpenFolder(); break;
                case "refresh": LoadStatistics(); break;
                case "cancel-operation": CancelRunning(); break;
                case "change-folder": ChangeFolder(); break;
                case "backup": Backup(); break;
                case "restore": Restore(); break;
                case "check-connection": CheckConnection(); break;
            }
        }

        private void OpenFolder()
        {
            if (!_context.HasCurrent) return;
            try
            {
                _services.Launcher.Open(_context.Current.StorageRoot);
            }
            catch (DirectoryNotFoundException)
            {
                _view.ShowMessage("Thư mục kho chưa tồn tại: " + _context.Current.StorageRoot, true);
            }
        }

        /// <summary>Trong lúc sao lưu/khôi phục chỉ còn nút Hủy: các nút khác khóa để không chạy hai việc cùng lúc.</summary>
        private void ShowCancelOnly(string text)
        {
            _view.ShowActions(new List<SettingsAction> { new SettingsAction("cancel-operation", text, true) });
        }

        private void CancelRunning()
        {
            var running = _running;
            if (running == null) return;
            _view.ShowMessage("Đang hủy...", false);
            running.Cancel();
        }

        private void Backup()
        {
            Run("Tạo bản sao lưu", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                    return;
                }

                var company = _context.Current;
                var folder = Path.Combine(company.StorageRoot, BackupService.BackupsFolder);
                var suggested = "HDDT_" + company.TaxCode.Value + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".zip";

                // Hộp thoại nêu rõ doanh nghiệp bị tác động (NFR-USE-007).
                var title = "Tạo bản sao lưu — " + company.DisplayName + " (MST " + company.TaxCode.Value + ")";
                var path = _services.SavePicker.Pick(suggested, "zip", "Gói sao lưu", title, folder);
                if (path == null) return;

                var progress = new Progress<BackupProgress>(p => _view.ShowMessage("Đang sao lưu " + p.Done.ToString("N0", Vi) + "/" + p.Total.ToString("N0", Vi) + " file...", false));
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    _running = cancellation;
                    ShowCancelOnly("Hủy sao lưu");
                    try
                    {
                        var result = await _services.Backup.CreateAsync(path, true, progress, cancellation.Token);
                        if (_disposed) return;
                        Keep("Đã tạo bản sao lưu " + result.Path + " (" + result.FileCount.ToString("N0", Vi) + " file, " + FormatBytes(result.TotalBytes) + ").", false);
                    }
                    catch (BackupException ex)
                    {
                        if (!_disposed) Keep(ex.Message, true);
                    }
                    catch (OperationCanceledException)
                    {
                        // Gói ghi dở nằm ở file tạm và đã bị xóa: chưa có bản sao lưu nào ở nơi đã chọn.
                        if (!_disposed) Keep("Đã hủy sao lưu. Chưa có gói nào được tạo.", false);
                    }
                    finally
                    {
                        _running = null;
                        if (!_disposed && _current == SettingsSection.Storage) LoadStatistics();
                    }
                }
            });
        }

        private void ChangeFolder()
        {
            if (_services.StorageMove == null || _services.FolderPicker == null) return;
            Run("Đổi thư mục kho", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                    return;
                }

                var company = _context.Current;
                var folder = _services.FolderPicker.Pick(company.StorageRoot);
                if (folder == null) return;

                var problems = await _services.StorageMove.CheckAsync(folder, token);
                if (_disposed) return;
                if (problems.Count > 0)
                {
                    _view.ShowMessage("Không dùng được thư mục này: " + problems[0] + (problems.Count > 1 ? " (và " + (problems.Count - 1) + " vấn đề khác)" : string.Empty), true);
                    return;
                }

                var message = "Đổi thư mục kho cho:\r\n" + company.DisplayName + "\r\nMST " + company.TaxCode.Value + "\r\n\r\n"
                    + "Từ: " + company.StorageRoot + "\r\nSang: " + folder + "\r\n\r\n"
                    + "Ứng dụng chép toàn bộ kho (XML, PDF, bản sao lưu và cơ sở dữ liệu), kiểm SHA-256 từng file, rồi mới chuyển sang dùng thư mục mới. "
                    + "Thư mục cũ được giữ nguyên, ứng dụng không xóa; bạn có thể tự xóa khi đã yên tâm. Không đóng ứng dụng trong lúc chép.\r\n\r\nTiếp tục?";
                if (!_services.Confirmation.Confirm("Đổi thư mục kho", message))
                {
                    _view.ShowMessage("Đã hủy đổi thư mục kho.", false);
                    return;
                }

                var progress = new Progress<BackupProgress>(p => _view.ShowMessage("Đang chép kho " + p.Done.ToString("N0", Vi) + "/" + p.Total.ToString("N0", Vi) + " file...", false));
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    _running = cancellation;
                    ShowCancelOnly("Hủy đổi thư mục");
                    try
                    {
                        var result = await _services.StorageMove.MoveAsync(folder, progress, cancellation.Token);
                        if (_disposed) return;
                        Keep(
                            "Đã chép " + result.FileCount.ToString("N0", Vi) + " file (" + FormatBytes(result.TotalBytes) + ") và chuyển sang dùng " + result.NewPath
                                + ". Thư mục cũ được giữ nguyên: " + result.OldPath + " (có thể xóa thủ công khi đã yên tâm).",
                            false);
                    }
                    catch (StorageMoveException ex)
                    {
                        if (!_disposed) Keep(ex.Message, true);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!_disposed) Keep("Đã hủy đổi thư mục kho. Vẫn dùng thư mục cũ; phần đã chép sang thư mục mới được dọn.", false);
                    }
                    finally
                    {
                        _running = null;
                        if (!_disposed && _current == SettingsSection.Storage) ShowStorage();
                        if (!_disposed && _keptText != null) _view.ShowMessage(_keptText, _keptIsError);
                    }
                }
            });
        }

        private void Restore()
        {
            Run("Khôi phục dữ liệu", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                    return;
                }

                var company = _context.Current;
                var file = _services.OpenPicker.Pick("Chọn gói khôi phục — " + company.DisplayName + " (MST " + company.TaxCode.Value + ")", "zip", "Gói sao lưu",
                    Path.Combine(company.StorageRoot, BackupService.BackupsFolder));
                if (file == null) return;

                _view.ShowMessage("Đang kiểm tra gói sao lưu...", false);
                var inspection = await _services.Backup.InspectAsync(file, token);
                if (_disposed) return;
                if (!inspection.IsValid)
                {
                    _view.ShowMessage("Gói không dùng được: " + inspection.Problems.First() + (inspection.Problems.Count > 1 ? " (và " + (inspection.Problems.Count - 1) + " lỗi khác)" : string.Empty), true);
                    return;
                }

                var manifest = inspection.Manifest;
                if (!string.Equals(manifest.TaxCode, company.TaxCode.Value, StringComparison.Ordinal))
                {
                    _view.ShowMessage("Gói thuộc MST " + manifest.TaxCode + " (" + manifest.CompanyName + "), không phải MST " + company.TaxCode.Value + " đang mở. Hãy chuyển sang doanh nghiệp đúng.", true);
                    return;
                }

                var created = manifest.CreatedAtUtc.HasValue ? manifest.CreatedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "không rõ";
                var message = "Khôi phục dữ liệu cho:\r\n" + company.DisplayName + "\r\nMST " + company.TaxCode.Value + "\r\n\r\n"
                    + "Gói tạo lúc " + created + ", " + manifest.Files.Count.ToString("N0", Vi) + " file.\r\n"
                    + "File trùng tên nhưng khác nội dung sẽ bị thay bằng bản trong gói; file không có trong gói được giữ nguyên. "
                    + (manifest.IncludesDatabase
                        ? "\r\n\r\nGói có cả cơ sở dữ liệu: chỉ mục Kho hóa đơn và lịch sử tải sẽ được thay bằng bản trong gói. Hóa đơn tải sau lúc tạo gói sẽ không còn trong Kho (file XML của chúng vẫn được giữ trên đĩa).\r\n\r\n"
                        : string.Empty)
                    + "Ứng dụng tự lưu một điểm sao lưu an toàn trước khi ghi và quay lại nếu có lỗi.\r\n\r\nTiếp tục?";
                if (!_services.Confirmation.Confirm("Khôi phục dữ liệu", message))
                {
                    _view.ShowMessage("Đã hủy khôi phục.", false);
                    return;
                }

                var progress = new Progress<BackupProgress>(p => _view.ShowMessage("Đang khôi phục " + p.Done.ToString("N0", Vi) + "/" + p.Total.ToString("N0", Vi) + " file...", false));
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    _running = cancellation;
                    ShowCancelOnly("Hủy khôi phục");
                    try
                    {
                        var result = await _services.Backup.RestoreAsync(file, progress, cancellation.Token);
                        if (_disposed) return;
                        Keep(
                        "Đã khôi phục: thêm " + result.Created.ToString("N0", Vi) + ", thay " + result.Overwritten.ToString("N0", Vi) + ", giữ nguyên " + result.Unchanged.ToString("N0", Vi) + " file."
                            + (result.SafetyBackupPath != null ? " Điểm sao lưu an toàn: " + result.SafetyBackupPath : string.Empty)
                            + (result.DatabaseRestored
                                ? " Đã khôi phục cả cơ sở dữ liệu: Kho hóa đơn và lịch sử tải khớp với bản sao lưu."
                                : " Lưu ý: gói này không có cơ sở dữ liệu (định dạng cũ) nên chỉ mục Kho hóa đơn không đổi theo các file vừa khôi phục."),
                            false);
                    }
                    catch (BackupException ex)
                    {
                        if (!_disposed) Keep(ex.Message, true);
                    }
                    catch (OperationCanceledException)
                    {
                        // Dịch vụ đã trả dữ liệu về trạng thái trước khi bắt đầu (điểm sao lưu an toàn).
                        if (!_disposed) Keep("Đã hủy khôi phục. Dữ liệu được giữ như trước khi bắt đầu.", false);
                    }
                    finally
                    {
                        _running = null;
                        if (!_disposed && _current == SettingsSection.Storage) LoadStatistics();
                    }
                }
            });
        }

        private void Keep(string text, bool isError)
        {
            _keptText = text;
            _keptIsError = isError;
            _view.ShowMessage(text, isError);
        }

        private void CheckConnection()
        {
            Run("Kiểm tra kết nối", async token =>
            {
                _view.ShowMessage("Đang kiểm tra kết nối...", false);
                var results = await _services.Connectivity.CheckAsync(token);
                if (_disposed || _current != SettingsSection.PortalConnection) return;
                var rows = results.Select(r => Row(r.Name, (r.Reachable ? "✔ " : "✖ ") + r.Detail)).ToList();
                _view.ShowSection(SettingsSection.PortalConnection, "Kết nối cổng Thuế", rows, "Kết quả lúc " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ". Chỉ kiểm tra máy chủ có trả lời, không đăng nhập.");
                _view.ShowActions(new List<SettingsAction> { new SettingsAction("check-connection", "Kiểm tra lại", true, true) });
                _view.ShowMessage(results.All(r => r.Reachable) ? "Kết nối bình thường." : "Có máy chủ không kết nối được.", !results.All(r => r.Reachable));
            });
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }

        private static string Pending()
        {
            return "Mục này chưa có thao tác: sao lưu, dung lượng, kết nối và nhật ký sẽ bổ sung cùng DD-11.";
        }

        private static KeyValuePair<string, string> Row(string name, string value)
        {
            return new KeyValuePair<string, string>(name, value ?? string.Empty);
        }
    }
}
