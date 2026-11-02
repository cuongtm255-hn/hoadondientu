using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Download
{
    /// <summary>
    /// Logic của SCR-03: tính lại kế hoạch tải mỗi khi nhập, giữ bộ lọc tách khỏi kỳ, và chỉ bật "Bắt đầu tải" khi đủ điều kiện
    /// (FR-SEARCH-006…010). Không tham chiếu control cụ thể.
    /// </summary>
    public sealed class DownloadConfigPresenter : IDisposable
    {
        private static readonly string[] FilterFields =
        {
            "CounterpartyTaxCode", "BuyerIdNumber", "InvoiceNumber", "StatusCode", "CheckResultCode", "FormSymbol", "InvoiceSymbol", "ProxyInvoice"
        };

        private readonly IDownloadConfigView _view;
        private readonly IPeriodSplitter _splitter;
        private readonly DownloadStartPolicy _policy;
        private readonly IStorageProbe _probe;
        private readonly IPortalCapabilities _capabilities;
        private readonly IPortalSessionStore _session;
        private readonly ICompanyContextAccessor _context;
        private readonly IEntitlementService _entitlement;
        private readonly IDownloadSessionStarter _starter;
        private readonly IConfirmationService _confirmation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly INavigationService _navigation;
        private readonly IClock _clock;
        private StorageProbeResult _storage = StorageProbeResult.Failed("chưa kiểm tra");
        private bool _initialized;
        private InvoiceDirection? _shownDirection;

        public DownloadConfigPresenter(
            IDownloadConfigView view,
            IPeriodSplitter splitter,
            DownloadStartPolicy policy,
            IStorageProbe probe,
            IPortalCapabilities capabilities,
            IPortalSessionStore session,
            ICompanyContextAccessor context,
            IEntitlementService entitlement,
            IDownloadSessionStarter starter,
            IConfirmationService confirmation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            INavigationService navigation,
            IClock clock)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (splitter == null) throw new ArgumentNullException("splitter");
            if (policy == null) throw new ArgumentNullException("policy");
            if (probe == null) throw new ArgumentNullException("probe");
            if (capabilities == null) throw new ArgumentNullException("capabilities");
            if (session == null) throw new ArgumentNullException("session");
            if (context == null) throw new ArgumentNullException("context");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (starter == null) throw new ArgumentNullException("starter");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (clock == null) throw new ArgumentNullException("clock");
            _view = view;
            _splitter = splitter;
            _policy = policy;
            _probe = probe;
            _capabilities = capabilities;
            _session = session;
            _context = context;
            _entitlement = entitlement;
            _starter = starter;
            _confirmation = confirmation;
            _runner = runner;
            _errors = errors;
            _navigation = navigation;
            _clock = clock;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _view.InputChanged += OnInputChanged;
            _view.ClearFiltersRequested += OnClearFilters;
            _view.StartRequested += OnStartRequested;
            _session.SessionChanged += OnSessionChanged;

            _view.ApplyDefaults(_clock.UtcNow);
            ShowFiltersFor(_view.ReadInput().Direction);
            RefreshStorage();
            Refresh();
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.InputChanged -= OnInputChanged;
            _view.ClearFiltersRequested -= OnClearFilters;
            _view.StartRequested -= OnStartRequested;
            _session.SessionChanged -= OnSessionChanged;
        }

        /// <summary>Tính lại kế hoạch, thông báo kỳ và trạng thái nút bắt đầu từ giá trị đang nhập.</summary>
        public void Refresh()
        {
            var input = _view.ReadInput();
            ShowFiltersFor(input.Direction);
            var spec = ToSpec(input);
            var validation = _splitter.Validate(spec);

            _view.ShowPeriodMessages(validation.Errors, validation.Warnings);
            _view.ShowPlan(validation.IsValid ? _splitter.Split(spec) : new List<DateSegment>());

            var filterErrors = InvoiceFilterValidator.Validate(ToFilter(input));
            _view.ShowFilterMessage(string.Join(" ", filterErrors));

            var decision = _policy.Evaluate(validation, _storage);
            if (decision.CanStart && filterErrors.Count > 0)
            {
                _view.ShowStartState(false, "Bộ lọc có giá trị chưa hợp lệ: " + filterErrors[0]);
                return;
            }

            _view.ShowStartState(decision.CanStart, decision.Reason);
        }

        /// <summary>Mỗi chiều có bộ trường lọc riêng (CCCD người mua chỉ có ở tra cứu bán ra); chỉ cập nhật khi chiều đổi.</summary>
        private void ShowFiltersFor(InvoiceDirection direction)
        {
            if (_shownDirection == direction) return;
            _shownDirection = direction;
            var supported = new HashSet<string>();
            foreach (var field in FilterFields)
            {
                if (_capabilities.SupportsFilter(direction, field)) supported.Add(field);
            }

            _view.ShowDirectionLabels(direction);
            _view.ShowSupportedFilters(supported);
        }

        private void RefreshStorage()
        {
            _storage = _context.HasCurrent ? _probe.Check(_context.Current.StorageRoot) : StorageProbeResult.Failed("chưa chọn doanh nghiệp");
            _view.ShowStorageState(_storage.Writable, _storage.Writable ? "Thư mục kho có thể ghi." : "Thư mục kho không ghi được: " + _storage.Reason);
        }

        private void OnInputChanged(object sender, EventArgs e)
        {
            Refresh();
        }

        private void OnSessionChanged(object sender, EventArgs e)
        {
            Refresh();
        }

        private void OnClearFilters(object sender, EventArgs e)
        {
            // Xóa bộ lọc không đụng tới kỳ đã chọn (FR-SEARCH-008, 009).
            _view.ClearFilters();
            Refresh();
        }

        private async void OnStartRequested(object sender, EventArgs e)
        {
            // Kiểm tra lại ngay trước khi bắt đầu: thư mục hoặc phiên có thể đã đổi từ lần tính trước.
            RefreshStorage();
            var input = _view.ReadInput();
            var spec = ToSpec(input);
            var validation = _splitter.Validate(spec);
            var decision = _policy.Evaluate(validation, _storage);
            if (!decision.CanStart)
            {
                _view.ShowStartState(false, decision.Reason);
                return;
            }

            if (InvoiceFilterValidator.Validate(ToFilter(input)).Count > 0)
            {
                Refresh();
                return;
            }

            var plan = DownloadPlan.Create(input.Direction, spec, ToFilter(input), _splitter);
            var company = _context.Current;

            // ServiceMode: xác nhận nêu rõ tên và MST bị tác động (NFR-USE-007).
            if (_entitlement.CurrentMode == AppMode.Service)
            {
                var message = "Tải " + (plan.Direction == InvoiceDirection.Purchase ? "hóa đơn mua vào" : "hóa đơn bán ra")
                    + " (" + plan.Segments.Count + " đoạn) cho:\r\n" + company.DisplayName + "\r\nMST " + company.TaxCode.Value;
                if (!_confirmation.Confirm("Bắt đầu tải", message)) return;
            }

            await _runner.RunAsync(
                "Bắt đầu tải",
                async token =>
                {
                    var sessionId = await _starter.StartAsync(plan, token);
                    await _navigation.NavigateAsync(ScreenId.DownloadProgress, sessionId, token);
                },
                state => { if (state.Error != null) _errors.Show(state.Error); });
        }

        private static PeriodSpec ToSpec(DownloadConfigInput input)
        {
            switch (input.Kind)
            {
                case PeriodKind.Month: return PeriodSpec.ForMonth(input.Year, input.Month);
                case PeriodKind.Quarter: return PeriodSpec.ForQuarter(input.Year, input.Quarter);
                case PeriodKind.Year: return PeriodSpec.ForYear(input.Year);
                default: return PeriodSpec.Custom(input.From, input.To);
            }
        }

        private static InvoiceSearchFilter ToFilter(DownloadConfigInput input)
        {
            return new InvoiceSearchFilter
            {
                CounterpartyTaxCode = Clean(input.CounterpartyTaxCode),

                // CCCD người mua chỉ có ở tra cứu bán ra; ô bị ẩn ở chiều mua vào nên không để giá trị cũ lọt vào kế hoạch.
                BuyerIdNumber = input.Direction == InvoiceDirection.Sold ? Clean(input.BuyerIdNumber) : null,
                InvoiceNumber = Clean(input.InvoiceNumber),
                StatusCode = Clean(input.StatusCode),
                CheckResultCode = Clean(input.CheckResultCode),
                FormSymbol = Clean(input.FormSymbol),
                InvoiceSymbol = Clean(input.InvoiceSymbol),
                ProxyInvoice = input.ProxyInvoice ? true : (bool?)null
            };
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
