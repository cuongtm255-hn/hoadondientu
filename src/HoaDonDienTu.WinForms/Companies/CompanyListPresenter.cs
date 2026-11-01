using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>
    /// SCR-10 Quản lý doanh nghiệp: danh sách đọc từ companies.json (không mở SQLite của nhiều MST), tìm kiếm, "Làm việc",
    /// mở thư mục, đổi tên/liên kết lại, ngừng quản lý. Quyền được kiểm tra ở service, UI chỉ khóa nút.
    /// </summary>
    public sealed class CompanyListPresenter : IDisposable
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly ICompanyListView _view;
        private readonly ICompanyRegistry _registry;
        private readonly ICompanyContextAccessor _context;
        private readonly IEntitlementService _entitlement;
        private readonly CompanyWorkflow _workflow;
        private readonly CompanyManagementService _management;
        private readonly Func<INavigationService> _navigation;
        private readonly IConfirmationService _confirmation;
        private readonly IFolderLauncher _launcher;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private bool _initialized;

        public CompanyListPresenter(
            ICompanyListView view,
            ICompanyRegistry registry,
            ICompanyContextAccessor context,
            IEntitlementService entitlement,
            CompanyWorkflow workflow,
            CompanyManagementService management,
            Func<INavigationService> navigation,
            IConfirmationService confirmation,
            IFolderLauncher launcher,
            IUiOperationRunner runner,
            IUserErrorPresenter errors)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (registry == null) throw new ArgumentNullException("registry");
            if (context == null) throw new ArgumentNullException("context");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (workflow == null) throw new ArgumentNullException("workflow");
            if (management == null) throw new ArgumentNullException("management");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            if (launcher == null) throw new ArgumentNullException("launcher");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _registry = registry;
            _context = context;
            _entitlement = entitlement;
            _workflow = workflow;
            _management = management;
            _navigation = navigation;
            _confirmation = confirmation;
            _launcher = launcher;
            _runner = runner;
            _errors = errors;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.SearchRequested += OnSearchRequested;
            _view.AddRequested += OnAddRequested;
            _view.RowActionRequested += OnRowActionRequested;

            var readOnly = _entitlement.CurrentMode == AppMode.ReadOnlyService;
            _view.ShowBanner(
                readOnly
                    ? "Chỉ đọc – cần gia hạn licence. Vẫn xem, in, xuất và sao lưu dữ liệu cũ."
                    : "Licence dịch vụ đang hoạt động · Không giới hạn số MST trên máy này · chỉ một MST được mở tại một thời điểm",
                readOnly);
            _view.ShowAddEnabled(_entitlement.IsAllowed(AppFeature.MultiCompany));
            Run("Tải danh sách doanh nghiệp", token => RefreshAsync());
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.SearchRequested -= OnSearchRequested;
            _view.AddRequested -= OnAddRequested;
            _view.RowActionRequested -= OnRowActionRequested;
        }

        public async Task RefreshAsync()
        {
            var items = await _registry.GetItemsAsync(CancellationToken.None);
            var currentId = _context.HasCurrent ? (HoaDonDienTu.Domain.Companies.CompanyId?)_context.Current.CompanyId : null;

            var text = (_view.SearchText ?? string.Empty).Trim();
            var filter = _view.StatusFilter;
            var filtered = items.Where(i => Matches(i, text, filter)).OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();

            var rows = filtered.Select(i => ToRow(i, currentId)).ToList();
            var current = currentId.HasValue ? items.FirstOrDefault(i => i.Id.Equals(currentId.Value)) : null;
            _view.ShowRows(rows, new CompanyListStats
            {
                Total = items.Count,
                NeedAttention = items.Count(i => i.Status != CompanyCatalogStatus.Active),
                TotalSize = FormatSize(items.Sum(i => i.CachedStorageBytes)),
                CurrentTaxCode = current == null ? null : current.TaxCode.Value,
                CurrentName = current == null ? null : current.DisplayName,
                Shown = rows.Count
            });
        }

        private static bool Matches(CompanyCatalogItem item, string text, string filter)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                var attention = item.Status != CompanyCatalogStatus.Active;
                if (filter == "Active" && attention) return false;
                if (filter == "Attention" && !attention) return false;
            }

            if (text.Length == 0) return true;
            return item.DisplayName.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0
                || item.TaxCode.Value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static CompanyRowViewModel ToRow(CompanyCatalogItem item, HoaDonDienTu.Domain.Companies.CompanyId? currentId)
        {
            var isCurrent = currentId.HasValue && currentId.Value.Equals(item.Id);
            CompanyRowAction action;
            string actionText;
            string status;
            if (item.Status != CompanyCatalogStatus.Active)
            {
                action = CompanyRowAction.Detail;
                actionText = "Chi tiết";
                status = item.Status == CompanyCatalogStatus.PathMissing ? "Mất thư mục kho" : "Cần xử lý";
            }
            else if (isCurrent)
            {
                action = CompanyRowAction.Open;
                actionText = "Mở";
                status = "Đang làm việc";
            }
            else
            {
                action = CompanyRowAction.Work;
                actionText = "Làm việc";
                status = "Sẵn sàng";
            }

            return new CompanyRowViewModel
            {
                Id = item.Id,
                Name = item.DisplayName,
                StoragePath = item.StoragePath,
                TaxCode = item.TaxCode.Value,
                LastOpened = item.LastOpenedAt.HasValue ? item.LastOpenedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Vi) : "—",
                InvoiceCount = item.CachedInvoiceCount.ToString("N0", Vi),
                Size = FormatSize(item.CachedStorageBytes),
                StatusText = status,
                Status = item.Status,
                IsCurrent = isCurrent,
                PrimaryAction = action,
                PrimaryActionText = actionText
            };
        }

        public static string FormatSize(long bytes)
        {
            const double gb = 1024.0 * 1024 * 1024;
            const double mb = 1024.0 * 1024;
            if (bytes <= 0) return "—";
            if (bytes >= gb) return (bytes / gb).ToString("0.0", Vi) + " GB";
            if (bytes >= mb) return (bytes / mb).ToString("0", Vi) + " MB";
            return "< 1 MB";
        }

        private void OnSearchRequested(object sender, EventArgs e)
        {
            Run("Tải danh sách doanh nghiệp", token => RefreshAsync());
        }

        private void OnAddRequested(object sender, EventArgs e)
        {
            Run("Thêm doanh nghiệp", token => _navigation().NavigateAsync(ScreenId.CompanyEdit, CompanyEditRequest.Add(), token));
        }

        private void OnRowActionRequested(object sender, CompanyRowActionEventArgs e)
        {
            switch (e.Action)
            {
                case CompanyRowAction.Work:
                    Run("Làm việc với doanh nghiệp", token => _workflow.StartWorkingAsync(e.Id, token));
                    break;

                case CompanyRowAction.Open:
                    Run("Mở doanh nghiệp", token => _navigation().NavigateAsync(ScreenId.Dashboard, null, token));
                    break;

                case CompanyRowAction.Detail:
                case CompanyRowAction.Rename:
                    Run("Mở thông tin doanh nghiệp", token => _navigation().NavigateAsync(ScreenId.CompanyEdit, CompanyEditRequest.Edit(e.Id), token));
                    break;

                case CompanyRowAction.OpenFolder:
                    Run("Mở thư mục kho", token => OpenFolderAsync(e));
                    break;

                case CompanyRowAction.Unlink:
                    Run("Ngừng quản lý doanh nghiệp", token => UnlinkAsync(e, token));
                    break;
            }
        }

        private async Task OpenFolderAsync(CompanyRowActionEventArgs e)
        {
            var items = await _registry.GetItemsAsync(CancellationToken.None);
            var item = items.FirstOrDefault(i => i.Id.Equals(e.Id));
            if (item == null) return;

            try
            {
                _launcher.Open(item.StoragePath);
            }
            catch (DirectoryNotFoundException)
            {
                _errors.Show(new HoaDonDienTu.Application.Abstractions.UserFacingError(
                    "FOLDER_MISSING", HoaDonDienTu.Application.Abstractions.ErrorCategory.Storage, "Mở thư mục kho",
                    "Thư mục kho không còn tồn tại: " + item.StoragePath, "Dùng Chi tiết để liên kết lại thư mục.", CorrelationId.New(), false));
            }
        }

        private async Task UnlinkAsync(CompanyRowActionEventArgs e, CancellationToken token)
        {
            var items = await _registry.GetItemsAsync(token);
            var item = items.FirstOrDefault(i => i.Id.Equals(e.Id));
            if (item == null) return;

            // Hộp thoại nêu rõ tên và MST bị tác động (NFR-USE-007).
            var message = "Ngừng quản lý " + item.DisplayName + " (MST " + item.TaxCode.Value + ")?\r\n"
                + "Chỉ bỏ khỏi danh sách; thư mục dữ liệu " + item.StoragePath + " được giữ nguyên và có thể liên kết lại sau.";
            if (!_confirmation.Confirm("Ngừng quản lý doanh nghiệp", message)) return;

            await _management.UnlinkAsync(e.Id, token);
            await RefreshAsync();
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
