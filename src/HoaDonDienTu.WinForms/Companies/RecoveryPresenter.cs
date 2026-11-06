using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Companies
{
    public sealed class RecoveryRow
    {
        public string Path { get; set; }
        public string TaxCode { get; set; }
        public string Status { get; set; }

        /// <summary>Tick được và mặc định đã tick.</summary>
        public bool CanRegister { get; set; }
    }

    public interface IRecoveryView
    {
        /// <summary>Quét lại các thư mục dữ liệu mặc định.</summary>
        event EventHandler ScanRequested;

        /// <summary>Chọn thêm một thư mục rồi quét.</summary>
        event EventHandler BrowseRequested;

        /// <summary>Đăng ký các kho đang tick.</summary>
        event EventHandler RebuildRequested;

        event EventHandler RestartRequested;

        /// <summary>Đường dẫn các kho đang tick.</summary>
        IList<string> SelectedPaths { get; }

        void ShowIntro(string text);
        void ShowStores(IList<RecoveryRow> rows);
        void ShowMessage(string text, bool isError);
        void SetBusy(bool busy);

        /// <summary>Sau khi đăng ký xong: hiện nút khởi động lại và ẩn nút đăng ký.</summary>
        void ShowRestart(bool visible);
    }

    /// <summary>
    /// Màn hình khôi phục khi danh mục doanh nghiệp mất hoặc hỏng (T02-07, NFR-REL-007): quét thư mục dữ liệu, chọn các kho tìm thấy và đăng ký lại
    /// vào danh mục mới (bản hỏng được giữ lại). Không tạo kho rỗng, không đụng tới dữ liệu trong kho.
    /// </summary>
    public sealed class RecoveryPresenter : IDisposable
    {
        private readonly IRecoveryView _view;
        private readonly ICompanyRecoveryService _service;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly Func<IList<string>> _defaultFolders;
        private readonly IFolderPicker _picker;
        private readonly Action _restart;
        private readonly List<string> _extraFolders = new List<string>();
        private IList<RecoveredStore> _stores = new List<RecoveredStore>();
        private bool _initialized;
        private bool _disposed;

        public RecoveryPresenter(
            IRecoveryView view,
            ICompanyRecoveryService service,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            Func<IList<string>> defaultFolders,
            IFolderPicker picker,
            Action restart)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            if (defaultFolders == null) throw new ArgumentNullException("defaultFolders");
            if (picker == null) throw new ArgumentNullException("picker");
            if (restart == null) throw new ArgumentNullException("restart");
            _view = view;
            _service = service;
            _runner = runner;
            _errors = errors;
            _defaultFolders = defaultFolders;
            _picker = picker;
            _restart = restart;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.ScanRequested += OnScan;
            _view.BrowseRequested += OnBrowse;
            _view.RebuildRequested += OnRebuild;
            _view.RestartRequested += OnRestart;
            _view.ShowIntro(
                "Danh sách doanh nghiệp không đọc được hoặc đã mất, nên ứng dụng không mở kho nào để tránh tạo kho rỗng đè lên dữ liệu. "
                + "Dữ liệu trong các thư mục kho vẫn nguyên. Quét thư mục dữ liệu để tìm lại các kho, chọn kho cần dùng rồi đăng ký lại.");
            Scan();
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.ScanRequested -= OnScan;
            _view.BrowseRequested -= OnBrowse;
            _view.RebuildRequested -= OnRebuild;
            _view.RestartRequested -= OnRestart;
        }

        private void OnScan(object sender, EventArgs e)
        {
            Scan();
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            var folder = _picker.Pick(_defaultFolders().FirstOrDefault());
            if (folder == null) return;
            if (!_extraFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) _extraFolders.Add(folder);
            Scan();
        }

        private void OnRestart(object sender, EventArgs e)
        {
            _restart();
        }

        private void Scan()
        {
            Run("Quét thư mục kho", async token =>
            {
                _view.SetBusy(true);
                try
                {
                    _view.ShowMessage("Đang quét...", false);
                    var folders = _defaultFolders().Concat(_extraFolders).ToList();
                    var scan = await _service.ScanAsync(folders, token);
                    if (_disposed) return;

                    _stores = scan.Stores;
                    _view.ShowStores(scan.Stores.Select(Row).ToList());
                    var usable = scan.Stores.Count(s => s.CanRegister);
                    var text = scan.Stores.Count == 0
                        ? "Không tìm thấy kho nào trong: " + string.Join("; ", folders) + ". Dùng \"Chọn thư mục khác...\" nếu kho nằm ở nơi khác."
                        : "Tìm thấy " + scan.Stores.Count.ToString(CultureInfo.InvariantCulture) + " kho, " + usable.ToString(CultureInfo.InvariantCulture) + " kho đăng ký được.";
                    if (scan.Skipped.Count > 0) text += " Bỏ qua " + scan.Skipped.Count + " thư mục có file định danh hỏng: " + scan.Skipped[0];
                    _view.ShowMessage(text, scan.Stores.Count == 0);
                }
                finally
                {
                    if (!_disposed) _view.SetBusy(false);
                }
            });
        }

        private void OnRebuild(object sender, EventArgs e)
        {
            var selected = new HashSet<string>(_view.SelectedPaths ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var chosen = _stores.Where(s => s.CanRegister && selected.Contains(s.Path)).ToList();
            if (chosen.Count == 0)
            {
                _view.ShowMessage("Chưa chọn kho nào để đăng ký.", true);
                return;
            }

            Run("Đăng ký lại các kho", async token =>
            {
                _view.SetBusy(true);
                try
                {
                    var result = await _service.RebuildAsync(chosen, token);
                    if (_disposed) return;

                    var text = "Đã đăng ký " + result.Registered.ToString(CultureInfo.InvariantCulture) + " kho."
                        + (result.QuarantinedCatalog != null ? " Danh mục hỏng được giữ lại ở " + result.QuarantinedCatalog + "." : string.Empty)
                        + (result.NotRegistered.Count > 0 ? " Không đăng ký được " + result.NotRegistered.Count + " kho: " + result.NotRegistered[0] + "." : string.Empty)
                        + " Tên hiển thị đặt tạm theo MST, đổi ở màn Doanh nghiệp. Khởi động lại ứng dụng để dùng danh mục mới.";
                    _view.ShowMessage(text, result.Registered == 0);
                    if (result.Registered > 0) _view.ShowRestart(true);
                }
                finally
                {
                    if (!_disposed) _view.SetBusy(false);
                }
            });
        }

        private static RecoveryRow Row(RecoveredStore store)
        {
            return new RecoveryRow
            {
                Path = store.Path,
                TaxCode = store.TaxCode.Value,
                CanRegister = store.CanRegister,
                Status = store.AlreadyRegistered ? "Đã có trong danh mục" : store.Problem ?? "Có thể đăng ký"
            };
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
