using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.WinForms.Licensing
{
    /// <summary>
    /// SCR-12 Bản quyền: trạng thái dùng thử 30 ngày hoặc đã kích hoạt, mã máy để gửi cho đơn vị phát hành và ô nhập mã kích hoạt.
    /// Mã kích hoạt kiểm ngay trên máy (chữ ký số, đúng mã máy), không cần Internet. Lỗi nghiệp vụ đi qua <see cref="IUserErrorPresenter"/>;
    /// mã đầy đủ không bao giờ được hiển thị lại.
    /// </summary>
    public sealed class LicensePresenter : IDisposable
    {
        private readonly ILicenseView _view;
        private readonly MachineLicenseService _license;
        private readonly IEntitlementUpdater _updater;
        private readonly ICompanyCatalog _catalog;
        private readonly Func<INavigationService> _navigation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly string _deviceName;
        private readonly string _versionText;
        private bool _initialized;

        public LicensePresenter(
            ILicenseView view,
            MachineLicenseService license,
            IEntitlementUpdater updater,
            ICompanyCatalog catalog,
            Func<INavigationService> navigation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            string deviceName,
            string versionText)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (license == null) throw new ArgumentNullException("license");
            if (updater == null) throw new ArgumentNullException("updater");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _license = license;
            _updater = updater;
            _catalog = catalog;
            _navigation = navigation;
            _runner = runner;
            _errors = errors;
            _deviceName = deviceName ?? string.Empty;
            _versionText = versionText ?? string.Empty;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.ActivateRequested += OnActivate;
            _view.BackRequested += OnBack;
            Render();
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.ActivateRequested -= OnActivate;
            _view.BackRequested -= OnBack;
        }

        private void OnActivate(object sender, EventArgs e)
        {
            var key = (_view.LicenseKeyInput ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                _view.ShowMessage("Nhập mã kích hoạt do đơn vị phát hành gửi.", true);
                return;
            }

            Run("Kích hoạt bản quyền", async token =>
            {
                var status = await _license.ActivateAsync(key, token);
                var catalog = await _catalog.LoadAsync(token);
                _updater.ApplyLicense(status, catalog.HasServiceHistory);
                _view.ClearKeyInput();
                Render();
                _view.ShowMessage("Đã kích hoạt bản quyền cho máy này. Cảm ơn bạn đã sử dụng phần mềm.", false);
            });
        }

        private void OnBack(object sender, EventArgs e)
        {
            Run("Quay lại cài đặt", token => _navigation().NavigateAsync(ScreenId.Settings, null, token));
        }

        private void Render()
        {
            _view.ShowState(Build(_license.Evaluate(), _deviceName, _versionText));
        }

        /// <summary>Dựng nội dung màn hình từ kết quả đánh giá licence (tách ra để thử nghiệm không cần control).</summary>
        public static LicenseViewModel Build(LicenseSnapshot snapshot, string deviceName, string versionText)
        {
            var model = new LicenseViewModel
            {
                Device = deviceName ?? string.Empty,
                MachineCode = snapshot.MachineCode,
                Version = versionText ?? string.Empty
            };

            switch (snapshot.Status)
            {
                case LicenseStatus.Valid:
                    model.ModeLetter = "✓";
                    model.ModeTitle = "Đã kích hoạt";
                    model.ModeSubtitle = "Bản quyền theo máy " + snapshot.KeyHint;
                    model.LicenseType = "Theo máy";
                    model.ExpiresAt = "Không giới hạn";
                    model.LastValidation = snapshot.ActivatedAtUtc.HasValue ? Format(snapshot.ActivatedAtUtc.Value) : "—";
                    model.Note = "Dùng đầy đủ chức năng, có thể thêm và chuyển giữa nhiều doanh nghiệp. Mã kích hoạt chỉ đúng cho máy này.";
                    break;
                case LicenseStatus.Trial:
                    model.ModeLetter = "D";
                    model.ModeTitle = "Đang dùng thử";
                    model.ModeSubtitle = MachineLicenseService.DescribeRemaining(snapshot.RemainingTrialDays).Replace("còn", "Còn") + " (đến hết " + FormatDate(snapshot.TrialEndsUtc.AddDays(-1)) + ")";
                    model.LicenseType = "Dùng thử " + MachineLicenseService.TrialDays.ToString(CultureInfo.InvariantCulture) + " ngày";
                    model.ExpiresAt = FormatDate(snapshot.TrialEndsUtc.AddDays(-1));
                    model.LastValidation = "Chưa kích hoạt";
                    model.Note = snapshot.StoredKeyRejected
                        ? "Mã kích hoạt đã lưu không còn đúng với máy này (" + snapshot.StoredKeyProblem + ") nên đang tính thời gian dùng thử. Gửi mã máy mới cho đơn vị phát hành."
                        : "Dùng đầy đủ chức năng trong thời gian thử. Hết thời gian thử, ứng dụng yêu cầu mã kích hoạt mới cho vào; dữ liệu của bạn vẫn được giữ nguyên.";
                    break;
                default:
                    model.ModeLetter = "!";
                    model.ModeTitle = "Hết thời gian dùng thử";
                    model.ModeSubtitle = "Cần mã kích hoạt để tiếp tục";
                    model.LicenseType = "Dùng thử " + MachineLicenseService.TrialDays.ToString(CultureInfo.InvariantCulture) + " ngày";
                    model.ExpiresAt = FormatDate(snapshot.TrialEndsUtc.AddDays(-1));
                    model.LastValidation = "Chưa kích hoạt";
                    model.Note = "Nhập mã kích hoạt để tiếp tục sử dụng.";
                    break;
            }

            return model;
        }

        private static string Format(DateTime utc)
        {
            return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(DateTime utc)
        {
            return utc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state =>
            {
                _view.SetBusy(state.IsBusy);
                if (state.Error != null) _errors.Show(state.Error);
            });
        }
    }
}
