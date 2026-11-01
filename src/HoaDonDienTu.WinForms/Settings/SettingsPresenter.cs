using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;

namespace HoaDonDienTu.WinForms.Settings
{
    /// <summary>
    /// SCR-09 Cài đặt. Hiện có khung các mục, thông tin doanh nghiệp đang mở, thông tin ứng dụng và lối vào Bản quyền (SCR-12).
    /// Lưu trữ, sao lưu, kết nối cổng Thuế và nhật ký thuộc DD-11 nên mới hiện ghi chú, chưa có thao tác.
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
        private bool _initialized;

        public SettingsPresenter(
            ISettingsView view,
            ILicenseService license,
            IEntitlementService entitlement,
            ICompanyContextManager context,
            Func<INavigationService> navigation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            string versionText)
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
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.SectionSelected += OnSectionSelected;
            _view.ShowLicenseSummary(LicenseSummary());
            Show(SettingsSection.ActiveCompany);
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.SectionSelected -= OnSectionSelected;
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
                    _view.ShowSection(section, "Lưu trữ và sao lưu", rows, Pending());
                    break;
                case SettingsSection.PortalConnection:
                    _view.ShowSection(section, "Kết nối cổng Thuế", rows, Pending());
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
