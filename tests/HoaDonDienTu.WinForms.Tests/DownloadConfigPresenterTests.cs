using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.WinForms.Download;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class DownloadConfigPresenterTests
    {
        private const string Mst = "0101234567";
        private FakeDownloadView _view;
        private FakeContextManager _context;
        private InMemoryPortalSessionStore _session;
        private ModeEntitlement _entitlement;
        private FakeProbe _probe;
        private FakeCapabilities _capabilities;
        private FakeStarter _starter;
        private FakeConfirm _confirm;
        private FakeNavigation _navigation;
        private RecordingErrors _errors;
        private RecordingLogger _logger;
        private DownloadConfigPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeDownloadView();
            _context = new FakeContextManager(FakeContextAccessor.Context(Mst));
            _session = new InMemoryPortalSessionStore();
            _entitlement = new ModeEntitlement { Mode = AppMode.Standard };
            _probe = new FakeProbe();
            _capabilities = new FakeCapabilities("CounterpartyTaxCode", "InvoiceNumber", "StatusCode");
            _starter = new FakeStarter();
            _confirm = new FakeConfirm();
            _navigation = new FakeNavigation();
            _errors = new RecordingErrors();
            _logger = new RecordingLogger();
            _presenter = Create();
            LogIn();
        }

        private DownloadConfigPresenter Create()
        {
            var presenter = new DownloadConfigPresenter(
                _view, new PeriodSplitter(), new DownloadStartPolicy(_context, _session, _entitlement), _probe, _capabilities,
                _session, _context, _entitlement, _starter, _confirm,
                new UiOperationRunner(new UserErrorMapper(), _logger), _errors, _navigation, new FixedClock());
            presenter.Initialize();
            return presenter;
        }

        private void LogIn()
        {
            _session.Set(new HoaDonDienTu.Application.Authentication.PortalSession(
                _context.Current.CompanyId, _context.Current.TaxCode,
                new HoaDonDienTu.Application.Authentication.PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
        }

        [Test]
        public void Initialize_AppliesDefaults_ShowsOnlySupportedFilters_AndComputesYearPlan()
        {
            Assert.That(_view.DefaultsToday, Is.EqualTo(new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)));
            Assert.That(_view.SupportedFilters, Is.EquivalentTo(new[] { "CounterpartyTaxCode", "InvoiceNumber", "StatusCode" }));
            Assert.That(_view.Plan.Count, Is.EqualTo(12), "AC-02: năm 2026 → 12 dòng");
        }

        // ---- Bộ lọc theo tham số của cổng ----

        [Test]
        public void DirectionChange_RecomputesSupportedFilters_AndLabels()
        {
            _capabilities = new FakeCapabilities("CounterpartyTaxCode", "BuyerIdNumber", "StatusCode");
            _presenter.Dispose();
            _view = new FakeDownloadView();
            _presenter = Create();
            Assert.That(_view.ShownDirection, Is.EqualTo(InvoiceDirection.Purchase));
            Assert.That(_view.SupportedFilters, Does.Not.Contain("BuyerIdNumber"), "Mua vào không có CCCD người mua");

            _view.Input.Direction = InvoiceDirection.Sold;
            _view.RaiseInputChanged();

            Assert.That(_view.ShownDirection, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(_view.SupportedFilters, Does.Contain("BuyerIdNumber"));
        }

        [TestCase("123", "MST")]
        [TestCase("01012345", "MST")]
        public void InvalidTaxCode_BlocksStart_AndExplainsOnTheForm(string value, string expected)
        {
            _view.Input.CounterpartyTaxCode = value;

            _view.RaiseInputChanged();

            Assert.That(_view.CanStart, Is.False);
            Assert.That(_view.FilterMessage, Does.Contain(expected));
            Assert.That(_view.Reason, Does.Contain("Bộ lọc"));
        }

        [Test]
        public void InvalidInvoiceNumber_BlocksStart_EvenIfTheStartEventFires()
        {
            _view.Input.InvoiceNumber = "12ab";
            _view.RaiseInputChanged();

            _view.RaiseStart();

            Assert.That(_starter.Plans, Is.Empty);
            Assert.That(_view.FilterMessage, Does.Contain("Số hóa đơn"));
        }

        [Test]
        public void ValidFilters_FlowIntoThePlan_WithPortalCodes()
        {
            _capabilities = new FakeCapabilities("CounterpartyTaxCode", "BuyerIdNumber", "StatusCode", "InvoiceNumber");
            _view.Input.Direction = InvoiceDirection.Sold;
            _view.Input.CounterpartyTaxCode = "0100100001";
            _view.Input.BuyerIdNumber = "036402421225";
            _view.Input.StatusCode = "6";
            _view.Input.CheckResultCode = "5";
            _view.Input.FormSymbol = "1";
            _view.Input.InvoiceSymbol = "C26TBN";
            _view.Input.InvoiceNumber = "323";
            _view.Input.ProxyInvoice = true;
            _view.RaiseInputChanged();

            _view.RaiseStart();

            var filter = _starter.Plans.Single().Filter;
            Assert.That(filter.CounterpartyTaxCode, Is.EqualTo("0100100001"));
            Assert.That(filter.BuyerIdNumber, Is.EqualTo("036402421225"));
            Assert.That(filter.StatusCode, Is.EqualTo("6"));
            Assert.That(filter.CheckResultCode, Is.EqualTo("5"));
            Assert.That(filter.FormSymbol, Is.EqualTo("1"));
            Assert.That(filter.InvoiceSymbol, Is.EqualTo("C26TBN"));
            Assert.That(filter.InvoiceNumber, Is.EqualTo("323"));
            Assert.That(filter.ProxyInvoice, Is.True);
        }

        [Test]
        public void PurchaseDirection_DropsAStaleBuyerIdNumber_FromThePlan()
        {
            _view.Input.Direction = InvoiceDirection.Purchase;
            _view.Input.BuyerIdNumber = "036402421225";
            _view.RaiseInputChanged();

            _view.RaiseStart();

            Assert.That(_starter.Plans.Single().Filter.BuyerIdNumber, Is.Null);
        }

        [Test]
        public void AllEmptyFilters_SendNothing()
        {
            _view.RaiseStart();

            var filter = _starter.Plans.Single().Filter;
            Assert.That(filter.StatusCode, Is.Null);
            Assert.That(filter.CheckResultCode, Is.Null);
            Assert.That(filter.ProxyInvoice, Is.Null);
        }

        [Test]
        public void Year2026_Purchase_Shows12Rows_AC02()
        {
            _view.Input.Direction = InvoiceDirection.Purchase;
            _view.Input.Kind = PeriodKind.Year;
            _view.Input.Year = 2026;

            _view.RaiseInputChanged();

            Assert.That(_view.Plan.Count, Is.EqualTo(12));
            Assert.That(_view.Plan[0].ToString(), Is.EqualTo("01/01/2026–31/01/2026"));
            Assert.That(_view.Plan[11].ToString(), Is.EqualTo("01/12/2026–31/12/2026"));
        }

        [Test]
        public void Custom_15Jan_To_10Mar_Shows3Rows_AC03()
        {
            _view.Input.Kind = PeriodKind.Custom;
            _view.Input.From = new DateTime(2026, 1, 15);
            _view.Input.To = new DateTime(2026, 3, 10);

            _view.RaiseInputChanged();

            Assert.That(_view.Plan.Select(s => s.ToString()), Is.EqualTo(new[]
            {
                "15/01/2026–31/01/2026", "01/02/2026–28/02/2026", "01/03/2026–10/03/2026"
            }));
        }

        [Test]
        public void InvalidPeriod_ShowsErrorInControl_ClearsPlan_AndLocksStart()
        {
            _view.Input.Kind = PeriodKind.Custom;
            _view.Input.From = new DateTime(2026, 3, 10);
            _view.Input.To = new DateTime(2026, 3, 9);

            _view.RaiseInputChanged();

            Assert.That(_view.Plan, Is.Empty);
            Assert.That(_view.Errors.Count, Is.EqualTo(1));
            Assert.That(_view.CanStart, Is.False);
            Assert.That(_view.Reason, Is.EqualTo(_view.Errors[0]));
            Assert.That(_errors.Shown, Is.Empty, "Lỗi nhập hiển thị tại control, không bằng hộp thoại");
        }

        [Test]
        public void VeryLongRange_ShowsWarning_ButStillAllowsStart()
        {
            _view.Input.Kind = PeriodKind.Custom;
            _view.Input.From = new DateTime(2010, 1, 1);
            _view.Input.To = new DateTime(2026, 12, 31);

            _view.RaiseInputChanged();

            Assert.That(_view.Warnings.Count, Is.EqualTo(1));
            Assert.That(_view.CanStart, Is.True);
        }

        [Test]
        public void StartIsEnabled_OnlyWhenAllConditionsMet()
        {
            Assert.That(_view.CanStart, Is.True);
            Assert.That(_view.Reason, Is.Null);
        }

        [Test]
        public void NotLoggedIn_StartLocked_WithReasonNamingTaxCode_ThenEnablesAfterLogin()
        {
            _session.Clear();

            Assert.That(_view.CanStart, Is.False);
            Assert.That(_view.Reason, Does.Contain(Mst));

            LogIn();

            Assert.That(_view.CanStart, Is.True);
        }

        [Test]
        public void ReadOnlyMode_StartLocked()
        {
            _entitlement.Mode = AppMode.ReadOnlyService;

            _view.RaiseInputChanged();

            Assert.That(_view.CanStart, Is.False);
            Assert.That(_view.Reason, Does.Contain("không cho phép"));
        }

        [Test]
        public void StorageNotWritable_StartLocked_AndStatusShown()
        {
            _presenter.Dispose();
            _probe.Result = StorageProbeResult.Failed("không có quyền ghi");
            _view = new FakeDownloadView();
            _presenter = Create();

            Assert.That(_view.CanStart, Is.False);
            Assert.That(_view.Reason, Does.Contain("không có quyền ghi"));
            Assert.That(_view.StorageWritable, Is.False);
        }

        [Test]
        public void ClearFilters_ResetsFilters_WithoutChangingThePeriod()
        {
            _view.Input.Kind = PeriodKind.Month;
            _view.Input.Year = 2026;
            _view.Input.Month = 4;
            _view.Input.CounterpartyTaxCode = "0100100001";
            _view.RaiseInputChanged();
            var planBefore = _view.Plan.Select(s => s.ToString()).ToArray();

            _view.RaiseClearFilters();

            Assert.That(_view.FiltersCleared, Is.EqualTo(1));
            Assert.That(_view.Plan.Select(s => s.ToString()), Is.EqualTo(planBefore));
            Assert.That(_view.Input.Month, Is.EqualTo(4));
        }

        [Test]
        public void Start_CreatesPlanFromInput_CleansFilter_CallsStarter_AndNavigatesToProgress()
        {
            _view.Input.Direction = InvoiceDirection.Sold;
            _view.Input.Kind = PeriodKind.Quarter;
            _view.Input.Year = 2026;
            _view.Input.Quarter = 2;
            _view.Input.CounterpartyTaxCode = "  0100100001 ";
            _view.Input.InvoiceNumber = "   ";
            _view.Input.ProxyInvoice = true;

            _view.RaiseStart();

            var plan = _starter.Plans.Single();
            Assert.That(plan.Direction, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(plan.Segments.Count, Is.EqualTo(3));
            Assert.That(plan.Segments[0].Start, Is.EqualTo(new DateTime(2026, 4, 1)));
            Assert.That(plan.Filter.CounterpartyTaxCode, Is.EqualTo("0100100001"));
            Assert.That(plan.Filter.InvoiceNumber, Is.Null);
            Assert.That(plan.Filter.ProxyInvoice, Is.True);
            Assert.That(_navigation.Targets, Is.EqualTo(new[] { ScreenId.DownloadProgress }));
            Assert.That(_navigation.Parameters.Single(), Is.SameAs(plan));
        }

        [Test]
        public void Start_StandardMode_DoesNotAskConfirmation()
        {
            _view.RaiseStart();

            Assert.That(_confirm.Asked, Is.Empty);
            Assert.That(_starter.Plans.Count, Is.EqualTo(1));
        }

        [Test]
        public void Start_ServiceMode_ConfirmationNamesCompanyAndTaxCode_DeclineStopsEverything()
        {
            _entitlement.Mode = AppMode.Service;
            _confirm.Answer = false;

            _view.RaiseStart();

            Assert.That(_confirm.Asked.Single(), Does.Contain("CÔNG TY MẪU").And.Contain(Mst));
            Assert.That(_starter.Plans, Is.Empty);
            Assert.That(_navigation.Targets, Is.Empty);
        }

        [Test]
        public void Start_ServiceMode_AcceptedProceeds()
        {
            _entitlement.Mode = AppMode.Service;
            _confirm.Answer = true;

            _view.RaiseStart();

            Assert.That(_starter.Plans.Count, Is.EqualTo(1));
            Assert.That(_navigation.Targets.Count, Is.EqualTo(1));
        }

        [Test]
        public void Start_RechecksConditions_EvenIfButtonWasEnabled()
        {
            _session.Clear();
            _view.CanStart = true;

            _view.RaiseStart();

            Assert.That(_starter.Plans, Is.Empty);
            Assert.That(_view.CanStart, Is.False);
            Assert.That(_view.Reason, Does.Contain("đăng nhập"));
        }

        [Test]
        public void Start_StorageBecameUnwritable_IsCaughtAtStart()
        {
            _probe.Result = StorageProbeResult.Failed("ổ đĩa đầy");

            _view.RaiseStart();

            Assert.That(_starter.Plans, Is.Empty);
            Assert.That(_view.Reason, Does.Contain("ổ đĩa đầy"));
        }

        [Test]
        public void Start_StarterFails_ShowsFriendlyError_NoNavigation_NoTokenLeak()
        {
            _starter.Failure = new InvalidOperationException("Authorization: Bearer SECRETVALUE");

            _view.RaiseStart();

            Assert.That(_errors.Shown.Count, Is.EqualTo(1));
            Assert.That(_errors.Shown[0].Message, Does.Not.Contain("SECRETVALUE"));
            Assert.That(_navigation.Targets, Is.Empty);
        }

        [Test]
        public void Start_DoubleClick_StartsOnlyOnce()
        {
            var release = new TaskCompletionSource<bool>();
            _starter.Gate = release.Task;

            _view.RaiseStart();
            _view.RaiseStart();
            release.SetResult(true);

            Assert.That(_starter.Plans.Count, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_UnsubscribesEverything()
        {
            _presenter.Dispose();

            Assert.That(_view.Listeners, Is.EqualTo(0));
            var before = _view.Plan.Count;
            _view.Input.Kind = PeriodKind.Month;
            _view.RaiseInputChanged();
            _session.Clear();
            Assert.That(_view.Plan.Count, Is.EqualTo(before));
            Assert.That(_view.CanStart, Is.True, "Không còn cập nhật sau khi dispose");
        }

        // ---- fakes ----

        private sealed class FakeDownloadView : IDownloadConfigView
        {
            public FakeDownloadView()
            {
                Input = new DownloadConfigInput
                {
                    Direction = InvoiceDirection.Purchase,
                    Kind = PeriodKind.Year,
                    Year = 2026,
                    Month = 1,
                    Quarter = 1,
                    From = new DateTime(2026, 1, 1),
                    To = new DateTime(2026, 1, 31)
                };
                Plan = new List<DateSegment>();
                Errors = new List<string>();
                Warnings = new List<string>();
            }

            public DownloadConfigInput Input { get; private set; }
            public IList<DateSegment> Plan { get; private set; }
            public IList<string> Errors { get; private set; }
            public IList<string> Warnings { get; private set; }
            public ISet<string> SupportedFilters { get; private set; }
            public DateTime DefaultsToday { get; private set; }
            public bool CanStart { get; set; }
            public string Reason { get; private set; }
            public bool StorageWritable { get; private set; }
            public int FiltersCleared { get; private set; }

            public event EventHandler InputChanged;
            public event EventHandler ClearFiltersRequested;
            public event EventHandler StartRequested;

            public int Listeners
            {
                get { return Count(InputChanged) + Count(ClearFiltersRequested) + Count(StartRequested); }
            }

            public DownloadConfigInput ReadInput() { return Input; }
            public void ApplyDefaults(DateTime today) { DefaultsToday = today; }
            public void ShowSupportedFilters(ISet<string> supportedFields) { SupportedFilters = supportedFields; }
            public void ClearFilters() { FiltersCleared++; Input.CounterpartyTaxCode = null; Input.BuyerIdNumber = null; }
            public void ShowDirectionLabels(InvoiceDirection direction) { ShownDirection = direction; }
            public void ShowFilterMessage(string message) { FilterMessage = message; }
            public InvoiceDirection? ShownDirection;
            public string FilterMessage;
            public void ShowPlan(IList<DateSegment> segments) { Plan = segments; }

            public void ShowPeriodMessages(IList<string> errors, IList<string> warnings)
            {
                Errors = errors;
                Warnings = warnings;
            }

            public void ShowStartState(bool canStart, string reason) { CanStart = canStart; Reason = reason; }
            public void ShowStorageState(bool writable, string text) { StorageWritable = writable; }

            public void RaiseInputChanged() { Raise(InputChanged); }
            public void RaiseClearFilters() { Raise(ClearFiltersRequested); }
            public void RaiseStart() { Raise(StartRequested); }

            private static void Raise(EventHandler handler)
            {
                if (handler != null) handler(null, EventArgs.Empty);
            }

            private static int Count(Delegate d)
            {
                return d == null ? 0 : d.GetInvocationList().Length;
            }
        }

        private sealed class ModeEntitlement : IEntitlementService
        {
            public AppMode Mode { get; set; }
            public AppMode CurrentMode { get { return Mode; } }

            public bool IsAllowed(AppFeature feature)
            {
                return Mode == AppMode.ReadOnlyService ? feature == AppFeature.ReadExport : true;
            }

            public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
            public event EventHandler ModeChanged { add { } remove { } }
        }

        private sealed class FakeProbe : IStorageProbe
        {
            public StorageProbeResult Result = StorageProbeResult.Ok();
            public StorageProbeResult Check(string storageRoot) { return Result; }
        }

        private sealed class FakeCapabilities : IPortalCapabilities
        {
            private readonly HashSet<string> _fields;

            public FakeCapabilities(params string[] fields)
            {
                _fields = new HashSet<string>(fields);
            }

            /// <summary>CCCD người mua chỉ có ở tra cứu bán ra, giống cấu hình thật.</summary>
            public bool SupportsFilter(InvoiceDirection direction, string filterField)
            {
                if (direction == InvoiceDirection.Purchase && filterField == "BuyerIdNumber") return false;
                return _fields.Contains(filterField);
            }
        }

        private sealed class FakeStarter : IDownloadSessionStarter
        {
            public List<DownloadPlan> Plans = new List<DownloadPlan>();
            public Exception Failure;
            public Task Gate;

            public async Task StartAsync(DownloadPlan plan, CancellationToken cancellationToken)
            {
                Plans.Add(plan);
                if (Gate != null) await Gate;
                if (Failure != null) throw Failure;
            }
        }

        private sealed class FakeConfirm : IConfirmationService
        {
            public List<string> Asked = new List<string>();
            public bool Answer = true;

            public bool Confirm(string title, string message)
            {
                Asked.Add(message);
                return Answer;
            }
        }

        private sealed class FakeNavigation : INavigationService
        {
            public List<ScreenId> Targets = new List<ScreenId>();
            public List<object> Parameters = new List<object>();

            public ScreenId CurrentScreen { get { return ScreenId.Download; } }
            public bool CanNavigate(ScreenId target) { return true; }

            public Task<NavigationResult> NavigateAsync(ScreenId target, object parameter, CancellationToken cancellationToken)
            {
                Targets.Add(target);
                Parameters.Add(parameter);
                return Task.FromResult(NavigationResult.Success(target));
            }
        }

        private sealed class RecordingErrors : IUserErrorPresenter
        {
            public List<UserFacingError> Shown = new List<UserFacingError>();
            public void Show(UserFacingError error) { Shown.Add(error); }
        }

        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get { return new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc); } }
        }
    }
}
