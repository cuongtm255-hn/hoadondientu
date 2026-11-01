using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>
    /// SCR-11 Thêm/sửa doanh nghiệp và khai báo lần đầu của StandardMode. Kiểm tra trước khi tạo hiển thị trực tiếp;
    /// ghi hai pha do <see cref="CompanyManagementService"/> đảm nhiệm. MST của kho đã có không sửa được.
    /// </summary>
    public sealed class CompanyEditPresenter : IDisposable
    {
        private readonly ICompanyEditView _view;
        private readonly CompanyEditRequest _request;
        private readonly ICompanyRegistry _registry;
        private readonly CompanyManagementService _management;
        private readonly CompanyWorkflow _workflow;
        private readonly Func<INavigationService> _navigation;
        private readonly IFolderPicker _picker;
        private readonly Func<string, string> _suggestFolder;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private CompanyCatalogItem _item;
        private string _lastSuggestion = string.Empty;
        private int _validationSequence;
        private int _validationRuns;
        private bool _initialized;

        public CompanyEditPresenter(
            ICompanyEditView view,
            CompanyEditRequest request,
            ICompanyRegistry registry,
            CompanyManagementService management,
            CompanyWorkflow workflow,
            Func<INavigationService> navigation,
            IFolderPicker picker,
            Func<string, string> suggestFolder,
            IUiOperationRunner runner,
            IUserErrorPresenter errors)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (request == null) throw new ArgumentNullException("request");
            if (registry == null) throw new ArgumentNullException("registry");
            if (management == null) throw new ArgumentNullException("management");
            if (workflow == null) throw new ArgumentNullException("workflow");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (picker == null) throw new ArgumentNullException("picker");
            if (suggestFolder == null) throw new ArgumentNullException("suggestFolder");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _request = request;
            _registry = registry;
            _management = management;
            _workflow = workflow;
            _navigation = navigation;
            _picker = picker;
            _suggestFolder = suggestFolder;
            _runner = runner;
            _errors = errors;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.InputChanged += OnInputChanged;
            _view.TaxCodeEdited += OnTaxCodeEdited;
            _view.BrowseRequested += OnBrowse;
            _view.PrimaryRequested += OnPrimary;
            _view.CancelRequested += OnCancel;

            _view.Configure(BuildConfig());
            Run("Tải thông tin doanh nghiệp", async token =>
            {
                if (_request.Mode == CompanyEditMode.Edit)
                {
                    var items = await _registry.GetItemsAsync(token);
                    _item = items.FirstOrDefault(i => i.Id.Equals(_request.CompanyId.Value));
                    if (_item == null) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "Không tìm thấy doanh nghiệp trong danh mục.");
                    _view.ShowValues(_item.TaxCode.Value, _item.DisplayName, _item.StoragePath);
                }
                else
                {
                    _view.ShowValues(string.Empty, string.Empty, string.Empty);
                }

                await ValidateAsync();
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.InputChanged -= OnInputChanged;
            _view.TaxCodeEdited -= OnTaxCodeEdited;
            _view.BrowseRequested -= OnBrowse;
            _view.PrimaryRequested -= OnPrimary;
            _view.CancelRequested -= OnCancel;
        }

        private CompanyEditConfig BuildConfig()
        {
            switch (_request.Mode)
            {
                case CompanyEditMode.FirstRun:
                    return new CompanyEditConfig
                    {
                        Title = "Khai báo doanh nghiệp",
                        Subtitle = "Tạo kho dữ liệu riêng cho mã số thuế của bạn",
                        PrimaryText = "Tạo và đăng nhập",
                        ShowBreadcrumb = false,
                        ShowCancel = false,
                        AfterSteps = new[]
                        {
                            "Tạo kho dữ liệu cho MST này.",
                            "Mở kho và vào Tổng quan.",
                            "Mở trang đăng nhập cổng Thuế để bạn tự đăng nhập.",
                            "Chỉ chấp nhận phiên có đúng MST đã khai báo."
                        }
                    };

                case CompanyEditMode.Edit:
                    return new CompanyEditConfig
                    {
                        Title = "Thông tin doanh nghiệp",
                        Subtitle = "Sửa tên hiển thị hoặc liên kết lại thư mục kho. MST không sửa được.",
                        PrimaryText = "Lưu thay đổi",
                        ShowBreadcrumb = true,
                        ShowCancel = true,
                        TaxCodeReadOnly = true,
                        AfterSteps = new[]
                        {
                            "Tên hiển thị được cập nhật trong danh sách.",
                            "Nếu đổi thư mục: chỉ nhận thư mục là kho của đúng MST này.",
                            "Dữ liệu hóa đơn không bị di chuyển hay xóa."
                        }
                    };

                default:
                    return new CompanyEditConfig
                    {
                        Title = "Thêm doanh nghiệp",
                        Subtitle = "Tạo hồ sơ và kho dữ liệu riêng cho một mã số thuế",
                        PrimaryText = "Tạo và chuyển sang MST này",
                        ShowBreadcrumb = true,
                        ShowCancel = true,
                        AfterSteps = new[]
                        {
                            "Dừng an toàn tác vụ của MST hiện tại nếu có.",
                            "Xóa token hiện tại khỏi bộ nhớ.",
                            "Đóng kho của MST hiện tại.",
                            "Tạo và mở kho của MST mới.",
                            "Mở trang đăng nhập cổng Thuế.",
                            "Chỉ chấp nhận phiên có đúng MST mới."
                        }
                    };
            }
        }

        private async Task ValidateAsync()
        {
            var sequence = ++_validationSequence;
            var input = _view.ReadInput();

            if (_request.Mode == CompanyEditMode.Edit)
            {
                await ValidateEditAsync(input, sequence);
                return;
            }

            var validation = await _management.ValidateAsync(input, CancellationToken.None);
            if (sequence != _validationSequence) return;
            _view.ShowChecks(validation.Checks);
            _view.SetPrimaryEnabled(validation.IsValid);
        }

        private async Task ValidateEditAsync(CompanyInput input, int sequence)
        {
            if (_item == null) return;
            var checks = new List<CompanyCheck>();
            var nameOk = !string.IsNullOrWhiteSpace(input.DisplayName) && input.DisplayName.Trim().Length <= 200;
            checks.Add(new CompanyCheck(CompanyCheckKey.DisplayName, nameOk, nameOk ? "Tên hiển thị hợp lệ" : "Tên hiển thị không được để trống và tối đa 200 ký tự."));

            var pathChanged = PathChanged(input);
            var folderOk = true;
            if (pathChanged)
            {
                var validation = await _management.ValidateRelinkAsync(_item.Id, input.StoragePath, CancellationToken.None);
                checks.AddRange(validation.Checks);
                folderOk = validation.IsValid;
            }

            if (sequence != _validationSequence) return;
            _view.ShowChecks(checks);
            var nameChanged = nameOk && !string.Equals(input.DisplayName.Trim(), _item.DisplayName, StringComparison.Ordinal);
            _view.SetPrimaryEnabled(nameOk && folderOk && (nameChanged || pathChanged));
        }

        private bool PathChanged(CompanyInput input)
        {
            return _item != null && !string.Equals((input.StoragePath ?? string.Empty).Trim(), _item.StoragePath, StringComparison.OrdinalIgnoreCase);
        }

        private void OnInputChanged(object sender, EventArgs e)
        {
            // Tên duy nhất mỗi lần để runner không bỏ qua lần nhập sau khi lần kiểm tra trước chưa xong.
            Run("Kiểm tra thông tin " + (++_validationRuns), token => ValidateAsync());
        }

        private void OnTaxCodeEdited(object sender, EventArgs e)
        {
            // Gợi ý thư mục theo MST chỉ khi người dùng chưa tự chọn thư mục khác.
            var input = _view.ReadInput();
            var path = (input.StoragePath ?? string.Empty).Trim();
            if (path.Length == 0 || string.Equals(path, _lastSuggestion, StringComparison.OrdinalIgnoreCase))
            {
                _lastSuggestion = string.IsNullOrWhiteSpace(input.TaxCodeText) ? string.Empty : _suggestFolder(input.TaxCodeText.Trim());
                _view.SetStoragePath(_lastSuggestion);
            }
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            var picked = _picker.Pick(_view.ReadInput().StoragePath);
            if (!string.IsNullOrEmpty(picked)) _view.SetStoragePath(picked);
        }

        private void OnCancel(object sender, EventArgs e)
        {
            Run("Quay lại", token => _navigation().NavigateAsync(ScreenId.CompanyList, null, token));
        }

        private void OnPrimary(object sender, EventArgs e)
        {
            Run("Lưu doanh nghiệp", SaveAsync);
        }

        private async Task SaveAsync(CancellationToken token)
        {
            var input = _view.ReadInput();
            try
            {
                if (_request.Mode == CompanyEditMode.Edit) await SaveEditAsync(input, token);
                else await CreateAsync(input, token);
            }
            catch (CompanyValidationException ex)
            {
                // Hiển thị đúng dòng kiểm tra bị lỗi; không đóng form.
                _view.ShowChecks(ex.Validation.Checks);
                _view.SetPrimaryEnabled(false);
                _view.ShowMessage(ex.Message);
            }
        }

        private async Task CreateAsync(CompanyInput input, CancellationToken token)
        {
            var item = await _management.CreateAsync(input, token);
            await _workflow.StartWorkingAsync(item.Id, token);
        }

        private async Task SaveEditAsync(CompanyInput input, CancellationToken token)
        {
            if (_item == null) return;
            if (!string.Equals(input.DisplayName.Trim(), _item.DisplayName, StringComparison.Ordinal))
            {
                _item = await _management.RenameAsync(_item.Id, input.DisplayName, token);
            }

            if (PathChanged(input))
            {
                _item = await _management.RelinkAsync(_item.Id, input.StoragePath, token);
            }

            await _navigation().NavigateAsync(ScreenId.CompanyList, null, token);
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
