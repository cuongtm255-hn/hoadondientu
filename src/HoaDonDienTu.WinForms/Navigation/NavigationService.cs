using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.WinForms.Navigation
{
    /// <summary>
    /// Điều hướng giữa các screen (DD-01 mục 8.6–8.7): kiểm tra quyền, hỏi screen hiện tại có đóng được không,
    /// hủy operation, dispose screen cũ rồi mới tạo screen mới. Chỉ một lần điều hướng chạy tại một thời điểm.
    /// </summary>
    public sealed class NavigationService : INavigationService, IDisposable
    {
        private readonly IScreenFactory _factory;
        private readonly INavigationView _view;
        private readonly IEntitlementService _entitlement;
        private readonly ICompanyContextAccessor _context;
        private readonly IApplicationLogger _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private IScreen _current;

        public NavigationService(
            IScreenFactory factory,
            INavigationView view,
            IEntitlementService entitlement,
            ICompanyContextAccessor context,
            IApplicationLogger logger)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            if (view == null) throw new ArgumentNullException("view");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (context == null) throw new ArgumentNullException("context");
            if (logger == null) throw new ArgumentNullException("logger");
            _factory = factory;
            _view = view;
            _entitlement = entitlement;
            _context = context;
            _logger = logger;
            CurrentScreen = ScreenId.Initializing;
        }

        public ScreenId CurrentScreen { get; private set; }

        public bool CanNavigate(ScreenId target)
        {
            return GetBlockReason(target) == null;
        }

        public async Task<NavigationResult> NavigateAsync(ScreenId target, object parameter, CancellationToken cancellationToken)
        {
            if (!await _gate.WaitAsync(0).ConfigureAwait(true))
            {
                return NavigationResult.Blocked(CurrentScreen, "Đang chuyển màn hình.");
            }

            try
            {
                var reason = GetBlockReason(target);
                if (reason != null)
                {
                    _view.SelectNavigation(CurrentScreen);
                    return NavigationResult.Blocked(CurrentScreen, reason);
                }

                if (_current != null && target == CurrentScreen && parameter == null)
                {
                    _view.SelectNavigation(CurrentScreen);
                    return NavigationResult.Success(CurrentScreen);
                }

                if (_current != null)
                {
                    var canClose = await CanCloseCurrentAsync(cancellationToken).ConfigureAwait(true);
                    if (!canClose)
                    {
                        _view.SelectNavigation(CurrentScreen);
                        return NavigationResult.Blocked(CurrentScreen, "Màn hình hiện tại còn thao tác chưa hoàn tất.");
                    }

                    DisposeCurrent();
                }

                var next = CreateOrRecover(ref target, parameter);
                _current = next;
                CurrentScreen = target;
                _view.ShowScreen(next.View);
                _view.SelectNavigation(target);
                return NavigationResult.Success(target);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            DisposeCurrent();
            _gate.Dispose();
        }

        private string GetBlockReason(ScreenId target)
        {
            switch (target)
            {
                case ScreenId.Initializing:
                case ScreenId.Recovery:
                case ScreenId.Settings:
                case ScreenId.License:
                    return null;

                case ScreenId.CompanyList:
                case ScreenId.CompanyEdit:
                    // Standard chưa có doanh nghiệp được vào màn hình khai báo lần đầu; còn lại cần quyền đa MST
                    // (ReadOnlyService chỉ được xem/chuyển giữa các MST đã có).
                    if (_entitlement.CurrentMode == AppMode.ReadOnlyService
                        || _entitlement.IsAllowed(AppFeature.MultiCompany)
                        || !_context.HasCurrent)
                    {
                        return null;
                    }

                    return "Chế độ hiện tại chỉ quản lý một doanh nghiệp.";

                case ScreenId.Download:
                case ScreenId.DownloadProgress:
                    if (!_context.HasCurrent) return "Hãy chọn doanh nghiệp trước khi mở màn hình này.";
                    return _entitlement.IsAllowed(AppFeature.NewDownload)
                        ? null
                        : "Chế độ hiện tại không cho phép tải hóa đơn mới.";

                default:
                    return _context.HasCurrent ? null : "Hãy chọn doanh nghiệp trước khi mở màn hình này.";
            }
        }

        private async Task<bool> CanCloseCurrentAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _current.CanCloseAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Không xác định được thì giữ nguyên screen để không mất dữ liệu đang nhập.
                _logger.Error("Không xác định được screen hiện tại có đóng được không", ex);
                return false;
            }
        }

        private IScreen CreateOrRecover(ref ScreenId target, object parameter)
        {
            try
            {
                return _factory.Create(target, parameter);
            }
            catch (Exception ex)
            {
                _logger.Error("Không tạo được screen " + target, ex);
                if (target == ScreenId.Recovery) throw;
                target = ScreenId.Recovery;
                return _factory.Create(ScreenId.Recovery, null);
            }
        }

        private void DisposeCurrent()
        {
            var screen = _current;
            _current = null;
            if (screen == null) return;

            try
            {
                screen.CancelOperations();
            }
            catch (Exception ex)
            {
                _logger.Error("Không hủy được operation của screen " + screen.Id, ex);
            }

            try
            {
                screen.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error("Không giải phóng được screen " + screen.Id, ex);
            }
        }
    }
}
