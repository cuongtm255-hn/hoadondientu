using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.WinForms.Licensing
{
    /// <summary>
    /// SCR-12 Bản quyền (DD-03 mục 6). Hiển thị trạng thái licence, kích hoạt, xác thực lại và hủy kích hoạt.
    /// Lỗi nghiệp vụ đi qua <see cref="IUserErrorPresenter"/>; key đầy đủ không bao giờ được hiển thị lại.
    /// </summary>
    public sealed class LicensePresenter : IDisposable
    {
        private readonly ILicenseView _view;
        private readonly ILicenseService _license;
        private readonly IEntitlementService _entitlement;
        private readonly IEntitlementUpdater _updater;
        private readonly ICompanyCatalog _catalog;
        private readonly IMachineIdentity _machine;
        private readonly Func<INavigationService> _navigation;
        private readonly IConfirmationService _confirmation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly string _deviceName;
        private readonly string _versionText;
        private bool _initialized;

        public LicensePresenter(
            ILicenseView view,
            ILicenseService license,
            IEntitlementService entitlement,
            IEntitlementUpdater updater,
            ICompanyCatalog catalog,
            IMachineIdentity machine,
            Func<INavigationService> navigation,
            IConfirmationService confirmation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            string deviceName,
            string versionText)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (license == null) throw new ArgumentNullException("license");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (updater == null) throw new ArgumentNullException("updater");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (machine == null) throw new ArgumentNullException("machine");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _license = license;
            _entitlement = entitlement;
            _updater = updater;
            _catalog = catalog;
            _machine = machine;
            _navigation = navigation;
            _confirmation = confirmation;
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
            _view.ValidateRequested += OnValidate;
            _view.DeactivateRequested += OnDeactivate;
            _view.BackRequested += OnBack;

            Render();
            Run("Đọc thông tin licence", async token =>
            {
                await _license.ValidateLocalAsync(token);
                Render();
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.ActivateRequested -= OnActivate;
            _view.ValidateRequested -= OnValidate;
            _view.DeactivateRequested -= OnDeactivate;
            _view.BackRequested -= OnBack;
        }

        private void OnActivate(object sender, EventArgs e)
        {
            var key = (_view.LicenseKeyInput ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                _view.ShowMessage("Nhập licence key do nhà cung cấp gửi.", true);
                return;
            }

            Run("Kích hoạt licence", async token =>
            {
                var status = await _license.ActivateAsync(key, token);
                await ApplyAsync(status, token);
                _view.ClearKeyInput();
                _view.ShowMessage("Đã kích hoạt licence. Các chức năng được cấp phép đã mở, không cần cài đặt lại.", false);
                if (_entitlement.IsAllowed(AppFeature.MultiCompany))
                {
                    await _navigation().NavigateAsync(ScreenId.CompanyList, null, token);
                }
            });
        }

        private void OnValidate(object sender, EventArgs e)
        {
            Run("Xác thực licence", async token =>
            {
                var status = await _license.ValidateOnlineAsync(token);
                await ApplyAsync(status, token);
                _view.ShowMessage(DescribeValidation(_license.CurrentInfo), _license.CurrentInfo.Status != LicenseStatus.Valid);
            });
        }

        private void OnDeactivate(object sender, EventArgs e)
        {
            var info = _license.CurrentInfo;
            if (!_confirmation.Confirm(
                    "Hủy kích hoạt licence",
                    "Giải phóng licence " + info.KeyHint + " khỏi máy này? Dữ liệu hiện có được giữ nguyên nhưng ứng dụng chuyển sang chế độ không có licence."))
            {
                return;
            }

            Run("Hủy kích hoạt licence", async token =>
            {
                var status = await _license.DeactivateAsync(token);
                await ApplyAsync(status, token);
                _view.ShowMessage("Đã hủy kích hoạt. Bạn có thể kích hoạt licence này trên máy khác.", false);
            });
        }

        private void OnBack(object sender, EventArgs e)
        {
            Run("Quay lại cài đặt", token => _navigation().NavigateAsync(ScreenId.Settings, null, token));
        }

        private async Task ApplyAsync(LicenseStatus status, CancellationToken token)
        {
            var snapshot = await _catalog.LoadAsync(token);
            _updater.ApplyLicense(status, snapshot.HasServiceHistory);
            Render();
        }

        private void Render()
        {
            _view.ShowState(Build(_license.CurrentInfo, _entitlement.CurrentMode));
        }

        private LicenseViewModel Build(LicenseInfo info, AppMode mode)
        {
            var model = new LicenseViewModel
            {
                Device = _deviceName,
                MachineShort = ShortMachine(_machine.GetMachineIdHash()),
                LicenseType = string.IsNullOrEmpty(info.LicenseType) ? "—" : info.LicenseType,
                ExpiresAt = info.ExpiresAtUtc.HasValue ? Format(info.ExpiresAtUtc.Value) : (info.Status == LicenseStatus.None ? "—" : "Không giới hạn"),
                LastValidation = info.LastOnlineValidationUtc.HasValue ? Format(info.LastOnlineValidationUtc.Value) : "Chưa kích hoạt",
                Version = _versionText,
                HasLicense = info.Status != LicenseStatus.None || info.Issue == LicenseIssue.Corrupt
            };

            switch (mode)
            {
                case AppMode.Service:
                    model.ModeLetter = "D";
                    model.ModeTitle = "Chế độ dịch vụ";
                    model.ModeSubtitle = info.Status == LicenseStatus.GracePeriod && info.MustReconnectBeforeUtc.HasValue
                        ? "Đang dùng ngoại tuyến, cần kết nối lại trước " + Format(info.MustReconnectBeforeUtc.Value)
                        : "Licence " + model.LicenseType + " đang hiệu lực " + info.KeyHint;
                    model.Note = info.Status == LicenseStatus.GracePeriod
                        ? "Máy chủ licence chưa xác thực lại gần đây. Licence đã xác thực vẫn hoạt động ngoại tuyến tối đa 30 ngày."
                        : "Có thể thêm và chuyển giữa nhiều doanh nghiệp. Hủy kích hoạt để chuyển licence sang máy khác.";
                    break;
                case AppMode.ReadOnlyService:
                    model.ModeLetter = "R";
                    model.ModeTitle = "Chỉ đọc – cần gia hạn licence";
                    model.ModeSubtitle = IssueText(info.Issue);
                    model.Note = "Vẫn xem, tìm, in, xuất và sao lưu dữ liệu cũ. Đăng nhập cổng Thuế, tải mới và thêm doanh nghiệp bị khóa tới khi licence hợp lệ.";
                    break;
                default:
                    model.ModeLetter = "S";
                    model.ModeTitle = "Chế độ chuẩn";
                    model.ModeSubtitle = info.Issue == LicenseIssue.None || info.Issue == LicenseIssue.NotActivated
                        ? "Không có licence mở rộng trên máy này"
                        : IssueText(info.Issue);
                    model.Note = "Mục kích hoạt luôn hiển thị. Các chức năng tương ứng với licence chỉ xuất hiện sau khi key được máy chủ xác thực thành công.";
                    break;
            }

            if (info.Issue == LicenseIssue.ClockTampered)
            {
                model.Note = "Đồng hồ hệ thống có dấu hiệu bị chỉnh lùi. Bấm Xác thực ngay (cần Internet) trước khi dùng chế độ dịch vụ.";
            }

            return model;
        }

        private static string IssueText(LicenseIssue issue)
        {
            switch (issue)
            {
                case LicenseIssue.Expired: return "Licence đã hết hạn";
                case LicenseIssue.Revoked: return "Licence đã bị thu hồi";
                case LicenseIssue.OfflineGraceExceeded: return "Quá 30 ngày chưa xác thực trực tuyến";
                case LicenseIssue.MachineMismatch: return "Licence gắn với máy khác; hủy kích hoạt ở máy cũ rồi kích hoạt lại";
                case LicenseIssue.SignatureInvalid: return "Chữ ký licence không hợp lệ";
                case LicenseIssue.Corrupt: return "File licence bị hỏng; kích hoạt lại để khôi phục";
                case LicenseIssue.ClockTampered: return "Đồng hồ hệ thống bất thường; cần xác thực trực tuyến";
                default: return "Chưa có licence hợp lệ";
            }
        }

        private static string DescribeValidation(LicenseInfo info)
        {
            switch (info.Status)
            {
                case LicenseStatus.Valid: return "Licence hợp lệ, đã xác thực trực tuyến.";
                case LicenseStatus.GracePeriod: return "Chưa kết nối được máy chủ licence; vẫn dùng được ngoại tuyến tới " + Format(info.MustReconnectBeforeUtc ?? DateTime.UtcNow) + ".";
                case LicenseStatus.None: return "Máy này chưa kích hoạt licence.";
                default: return IssueText(info.Issue) + ".";
            }
        }

        /// <summary>Hiện 4 ký tự đầu và 4 ký tự cuối của mã đã băm, đủ để đối chiếu với nhà cung cấp mà không lộ toàn bộ.</summary>
        public static string ShortMachine(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length < 8) return hash ?? string.Empty;
            return hash.Substring(0, 4).ToUpperInvariant() + "-••••-" + hash.Substring(hash.Length - 4).ToUpperInvariant();
        }

        private static string Format(DateTime utc)
        {
            return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
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
