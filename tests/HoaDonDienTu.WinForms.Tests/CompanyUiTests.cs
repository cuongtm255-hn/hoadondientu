using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Companies;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Companies;
using HoaDonDienTu.WinForms.Shell;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Môi trường dùng chung: danh mục + kho thật trên thư mục tạm, các thành phần UI giả.</summary>
    public abstract class CompanyUiTestBase
    {
        protected string Root;
        protected JsonCompanyCatalog Registry;
        protected FileSystemCompanyStorage Storage;
        protected ModeEntitlement Entitlement;
        protected FakeContextManager Context;
        protected InMemoryPortalSessionStore Session;
        protected RecordingNav Nav;
        protected RecordingShell Shell;
        protected RecordingErrorsUi Errors;
        protected RecordingConfirm Confirm;
        protected FakeSwitch Switch;
        protected RecordingLogger Logger;
        protected UiOperationRunner Runner;
        protected CompanyManagementService Management;
        protected CompanyWorkflow Workflow;

        [SetUp]
        public void BaseSetUp()
        {
            Root = Path.Combine(Path.GetTempPath(), "hddt-ui-co-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Registry = new JsonCompanyCatalog(Path.Combine(Root, "cfg", "companies.json"));
            Storage = new FileSystemCompanyStorage();
            Entitlement = new ModeEntitlement { Value = AppMode.Service };
            Context = new FakeContextManager(null);
            Session = new InMemoryPortalSessionStore();
            Nav = new RecordingNav();
            Shell = new RecordingShell();
            Errors = new RecordingErrorsUi();
            Confirm = new RecordingConfirm();
            Switch = new FakeSwitch(Context, Registry);
            Logger = new RecordingLogger();
            Runner = new UiOperationRunner(new UserErrorMapper(), Logger);
            Management = new CompanyManagementService(Registry, Storage, Context, Entitlement, Logger);
            Workflow = new CompanyWorkflow(Switch, Context, Session, Entitlement, Confirm, Errors, () => Nav, () => Shell);
        }

        [TearDown]
        public void BaseTearDown()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }

        protected string Folder(string name)
        {
            return Path.Combine(Root, "data", name);
        }

        protected CompanyCatalogItem Add(string mst, string name, bool createStorage = true)
        {
            var id = CompanyId.NewId();
            var item = new CompanyCatalogItem(id, TaxCode.Parse(mst), name, Folder(mst), null, null, 0, 0, CompanyCatalogStatus.Active);
            if (createStorage) Storage.Initialize(item.StoragePath, id, item.TaxCode);
            Registry.AddAsync(item, CancellationToken.None).GetAwaiter().GetResult();
            return item;
        }

        protected void MakeCurrent(CompanyCatalogItem item)
        {
            Context.Current = new CompanyContext(item.Id, item.TaxCode, item.DisplayName, item.StoragePath, null);
        }
    }

    public sealed class ModeEntitlement : IEntitlementService
    {
        public AppMode Value { get; set; }
        public AppMode CurrentMode { get { return Value; } }

        public bool IsAllowed(AppFeature feature)
        {
            switch (Value)
            {
                case AppMode.Standard: return feature != AppFeature.MultiCompany;
                case AppMode.Service: return true;
                default: return feature == AppFeature.ReadExport;
            }
        }

        public void Demand(AppFeature feature) { if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature); }
        public event EventHandler ModeChanged { add { } remove { } }
    }

    public sealed class RecordingNav : INavigationService
    {
        public List<ScreenId> Targets = new List<ScreenId>();
        public List<object> Parameters = new List<object>();
        public ScreenId CurrentScreen { get { return Targets.Count == 0 ? ScreenId.Initializing : Targets[Targets.Count - 1]; } }
        public bool CanNavigate(ScreenId target) { return true; }

        public Task<NavigationResult> NavigateAsync(ScreenId target, object parameter, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            Parameters.Add(parameter);
            return Task.FromResult(NavigationResult.Success(target));
        }
    }

    public sealed class RecordingShell : IShellActions
    {
        public List<LoginReason> Logins = new List<LoginReason>();

        public Task LoginAsync(LoginReason reason)
        {
            Logins.Add(reason);
            return Task.FromResult(true);
        }
    }

    public sealed class RecordingErrorsUi : IUserErrorPresenter
    {
        public List<UserFacingError> Shown = new List<UserFacingError>();
        public void Show(UserFacingError error) { Shown.Add(error); }
    }

    public sealed class RecordingConfirm : IConfirmationService
    {
        public List<string> Messages = new List<string>();
        public bool Answer = true;

        public bool Confirm(string title, string message)
        {
            Messages.Add(message);
            return Answer;
        }
    }

    /// <summary>ICompanySwitchService giả: ghi lại lời gọi, đổi context theo kịch bản.</summary>
    public sealed class FakeSwitch : ICompanySwitchService
    {
        private readonly FakeContextManager _context;
        private readonly ICompanyRegistry _registry;

        public FakeSwitch(FakeContextManager context, ICompanyRegistry registry)
        {
            _context = context;
            _registry = registry;
            Assessment = new CompanySwitchAssessment(CompanySwitchBlocker.None);
            Outcome = CompanySwitchOutcome.Switched;
        }

        public CompanySwitchAssessment Assessment { get; set; }
        public CompanySwitchOutcome Outcome { get; set; }
        public string Message { get; set; }
        public List<ActiveJobResolution> Switches = new List<ActiveJobResolution>();
        public List<CompanyId> Targets = new List<CompanyId>();

        public Task<CompanySwitchAssessment> AssessAsync(CompanyId targetCompanyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Assessment);
        }

        public async Task<CompanySwitchResult> SwitchAsync(CompanyId targetCompanyId, ActiveJobResolution jobResolution, CancellationToken cancellationToken)
        {
            Switches.Add(jobResolution);
            Targets.Add(targetCompanyId);
            if (Outcome == CompanySwitchOutcome.Switched)
            {
                var item = (await _registry.GetItemsAsync(cancellationToken)).First(i => i.Id.Equals(targetCompanyId));
                _context.Current = new CompanyContext(item.Id, item.TaxCode, item.DisplayName, item.StoragePath, null);
            }

            return new CompanySwitchResult(Outcome, CompanySwitchBlocker.None, _context.Current, Message);
        }
    }

    // ------------------------------------------------------------------------------------------------------

    [TestFixture]
    public class CompanyWorkflowTests : CompanyUiTestBase
    {
        [Test]
        public void Work_SwitchesThenGoesToDashboard_ThenOpensLogin()
        {
            var item = Add("0100000001", "A");

            var ok = Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(ok, Is.True);
            Assert.That(Switch.Targets, Is.EqualTo(new[] { item.Id }));
            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Dashboard }));
            Assert.That(Shell.Logins, Is.EqualTo(new[] { LoginReason.CompanySelected }));
        }

        [Test]
        public void Work_ReadOnlyMode_OpensStoreButNeverLogin()
        {
            var item = Add("0100000001", "A");
            Entitlement.Value = AppMode.ReadOnlyService;

            Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Dashboard }));
            Assert.That(Shell.Logins, Is.Empty);
        }

        [Test]
        public void Work_AlreadyCurrentAndLoggedIn_JustGoesToDashboard()
        {
            var item = Add("0100000001", "A");
            MakeCurrent(item);
            Session.Set(new PortalSession(item.Id, item.TaxCode, new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));

            Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(Switch.Targets, Is.Empty, "Không chuyển kho khi đã là doanh nghiệp hiện tại");
            Assert.That(Shell.Logins, Is.Empty);
            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Dashboard }));
        }

        [Test]
        public void Work_BlockedByEntitlement_ShowsReason_DoesNotSwitchOrNavigate()
        {
            var item = Add("0100000001", "A");
            Switch.Assessment = new CompanySwitchAssessment(CompanySwitchBlocker.LicenceReadOnly);

            var ok = Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(ok, Is.False);
            Assert.That(Switch.Targets, Is.Empty);
            Assert.That(Nav.Targets, Is.Empty);
            Assert.That(Errors.Shown.Single().Message, Does.Contain("không cho phép"));
        }

        [Test]
        public void Work_WithRunningJob_AsksOnce_AndPausesWhenAccepted()
        {
            var current = Add("0100000009", "Đang chạy");
            MakeCurrent(current);
            var item = Add("0100000001", "A");
            Switch.Assessment = new CompanySwitchAssessment(CompanySwitchBlocker.DownloadRunning);
            Confirm.Answer = true;

            Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(Confirm.Messages.Single(), Does.Contain("Đang chạy").And.Contain("0100000009"));
            Assert.That(Switch.Switches, Is.EqualTo(new[] { ActiveJobResolution.PauseAndSwitch }));
        }

        [Test]
        public void Work_WithRunningJob_Declined_ChangesNothing()
        {
            var current = Add("0100000009", "Đang chạy");
            MakeCurrent(current);
            var item = Add("0100000001", "A");
            Switch.Assessment = new CompanySwitchAssessment(CompanySwitchBlocker.DownloadRunning);
            Confirm.Answer = false;

            var ok = Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(ok, Is.False);
            Assert.That(Switch.Switches, Is.Empty);
            Assert.That(Nav.Targets, Is.Empty);
        }

        [Test]
        public void Work_OpenFailsButPreviousRestored_ExplainsAndStaysPut()
        {
            var item = Add("0100000001", "A");
            Switch.Outcome = CompanySwitchOutcome.RestoredPrevious;
            Switch.Message = "Kho hỏng";

            var ok = Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(ok, Is.False);
            Assert.That(Errors.Shown.Single().Message, Does.Contain("Kho hỏng").And.Contain("quay lại"));
            Assert.That(Nav.Targets, Is.Empty);
            Assert.That(Shell.Logins, Is.Empty);
        }

        [Test]
        public void Work_NothingCouldBeOpened_GoesToRecovery()
        {
            var item = Add("0100000001", "A");
            Switch.Outcome = CompanySwitchOutcome.RecoveryRequired;

            Workflow.StartWorkingAsync(item.Id, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Recovery }));
        }
    }

    // ------------------------------------------------------------------------------------------------------

    [TestFixture]
    public class CompanyListPresenterTests : CompanyUiTestBase
    {
        private FakeListView _view;
        private RecordingLauncher _launcher;
        private CompanyListPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeListView();
            _launcher = new RecordingLauncher();
            _presenter = new CompanyListPresenter(
                _view, Registry, Context, Entitlement, Workflow, Management, () => Nav, Confirm, _launcher, Runner, Errors);
        }

        [Test]
        public void Initialize_Service_ShowsLicenceBanner_AllowsAdd()
        {
            _presenter.Initialize();

            Assert.That(_view.BannerText, Does.Contain("Licence dịch vụ"));
            Assert.That(_view.BannerReadOnly, Is.False);
            Assert.That(_view.AddEnabled, Is.True);
        }

        [Test]
        public void Initialize_ReadOnly_ShowsWarningBanner_LocksAdd()
        {
            Entitlement.Value = AppMode.ReadOnlyService;

            _presenter.Initialize();

            Assert.That(_view.BannerReadOnly, Is.True);
            Assert.That(_view.BannerText, Does.Contain("Chỉ đọc"));
            Assert.That(_view.AddEnabled, Is.False);
        }

        [Test]
        public void Rows_ShowStatusActionsAndStats_WithoutOpeningAnyStorage()
        {
            var a = Add("0100000001", "An Phú");
            Add("0100000002", "Bình Minh");
            var missing = Add("0100000003", "Cần Thơ", createStorage: false);
            MakeCurrent(a);

            _presenter.Initialize();

            var rows = _view.Rows.ToDictionary(r => r.TaxCode);
            Assert.That(rows["0100000001"].PrimaryAction, Is.EqualTo(CompanyRowAction.Open));
            Assert.That(rows["0100000001"].StatusText, Is.EqualTo("Đang làm việc"));
            Assert.That(rows["0100000002"].PrimaryAction, Is.EqualTo(CompanyRowAction.Work));
            Assert.That(rows["0100000003"].PrimaryAction, Is.EqualTo(CompanyRowAction.Detail));
            Assert.That(rows["0100000003"].StatusText, Is.EqualTo("Mất thư mục kho"));
            Assert.That(_view.Stats.Total, Is.EqualTo(3));
            Assert.That(_view.Stats.NeedAttention, Is.EqualTo(1));
            Assert.That(_view.Stats.CurrentTaxCode, Is.EqualTo("0100000001"));
        }

        [Test]
        public void Search_ByNameOrTaxCode_AndStatusFilter()
        {
            Add("0100000001", "An Phú");
            Add("0100000002", "Bình Minh");
            Add("0100000003", "Cần Thơ", createStorage: false);
            _presenter.Initialize();

            _view.SearchText = "bình";
            _view.RaiseSearch();
            Assert.That(_view.Rows.Select(r => r.Name), Is.EqualTo(new[] { "Bình Minh" }));

            _view.SearchText = "0100000003";
            _view.RaiseSearch();
            Assert.That(_view.Rows.Single().TaxCode, Is.EqualTo("0100000003"));

            _view.SearchText = "";
            _view.StatusFilter = "Attention";
            _view.RaiseSearch();
            Assert.That(_view.Rows.Select(r => r.TaxCode), Is.EqualTo(new[] { "0100000003" }));

            _view.StatusFilter = "Active";
            _view.RaiseSearch();
            Assert.That(_view.Rows.Count, Is.EqualTo(2));
            Assert.That(_view.Stats.Total, Is.EqualTo(3), "Thống kê luôn tính trên toàn bộ danh mục");
        }

        [Test]
        public void Work_DelegatesToWorkflow()
        {
            var item = Add("0100000001", "A");
            _presenter.Initialize();

            _view.RaiseAction(item.Id, CompanyRowAction.Work);

            Assert.That(Switch.Targets, Is.EqualTo(new[] { item.Id }));
            Assert.That(Shell.Logins.Count, Is.EqualTo(1));
        }

        [Test]
        public void OpenAndDetailAndAdd_NavigateToTheRightScreens()
        {
            var item = Add("0100000001", "A");
            _presenter.Initialize();

            _view.RaiseAction(item.Id, CompanyRowAction.Open);
            _view.RaiseAction(item.Id, CompanyRowAction.Detail);
            _view.RaiseAction(item.Id, CompanyRowAction.Rename);
            _view.RaiseAdd();

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Dashboard, ScreenId.CompanyEdit, ScreenId.CompanyEdit, ScreenId.CompanyEdit }));
            Assert.That(((CompanyEditRequest)Nav.Parameters[1]).Mode, Is.EqualTo(CompanyEditMode.Edit));
            Assert.That(((CompanyEditRequest)Nav.Parameters[1]).CompanyId, Is.EqualTo(item.Id));
            Assert.That(((CompanyEditRequest)Nav.Parameters[3]).Mode, Is.EqualTo(CompanyEditMode.Add));
        }

        [Test]
        public void OpenFolder_UsesTheLauncher_AndExplainsAMissingFolder()
        {
            var good = Add("0100000001", "A");
            var gone = Add("0100000002", "B", createStorage: false);
            _presenter.Initialize();

            _view.RaiseAction(good.Id, CompanyRowAction.OpenFolder);
            _launcher.MissingPaths.Add(gone.StoragePath);
            _view.RaiseAction(gone.Id, CompanyRowAction.OpenFolder);

            Assert.That(_launcher.Opened, Is.EqualTo(new[] { good.StoragePath, gone.StoragePath }));
            Assert.That(Errors.Shown.Single().Message, Does.Contain(gone.StoragePath));
        }

        [Test]
        public void Unlink_ConfirmationNamesCompanyAndTaxCode_DeclineKeepsIt()
        {
            var item = Add("0100000001", "An Phú");
            _presenter.Initialize();
            Confirm.Answer = false;

            _view.RaiseAction(item.Id, CompanyRowAction.Unlink);

            Assert.That(Confirm.Messages.Single(), Does.Contain("An Phú").And.Contain("0100000001").And.Contain("giữ nguyên"));
            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Count, Is.EqualTo(1));
        }

        [Test]
        public void Unlink_Accepted_RemovesEntry_KeepsFolder_AndRefreshesRows()
        {
            var item = Add("0100000001", "An Phú");
            Add("0100000002", "B");
            _presenter.Initialize();

            _view.RaiseAction(item.Id, CompanyRowAction.Unlink);

            Assert.That(_view.Rows.Select(r => r.TaxCode), Is.EqualTo(new[] { "0100000002" }));
            Assert.That(Directory.Exists(item.StoragePath), Is.True);
        }

        [Test]
        public void Unlink_TheOpenCompany_ShowsFriendlyError()
        {
            var item = Add("0100000001", "An Phú");
            MakeCurrent(item);
            _presenter.Initialize();

            _view.RaiseAction(item.Id, CompanyRowAction.Unlink);

            Assert.That(Errors.Shown.Single().Code, Is.EqualTo("COMPANY_IN_USE"));
            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Count, Is.EqualTo(1));
        }

        [Test]
        public void Unlink_InReadOnlyMode_IsDeniedByTheService_NotJustHidden()
        {
            var item = Add("0100000001", "A");
            Add("0100000002", "B");
            _presenter.Initialize();
            Entitlement.Value = AppMode.ReadOnlyService;

            _view.RaiseAction(item.Id, CompanyRowAction.Unlink);

            Assert.That(Errors.Shown.Single().Category, Is.EqualTo(ErrorCategory.Licence));
            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Count, Is.EqualTo(2));
        }

        [Test]
        public void Dispose_UnsubscribesEvents()
        {
            _presenter.Initialize();
            _presenter.Dispose();

            Assert.That(_view.Listeners, Is.EqualTo(0));
        }

        [TestCase(0L, "—")]
        [TestCase(500000L, "< 1 MB")]
        [TestCase(5L * 1024 * 1024, "5 MB")]
        [TestCase(11592622080L, "10,8 GB")]
        public void FormatSize_UsesVietnameseDecimalSeparator(long bytes, string expected)
        {
            Assert.That(CompanyListPresenter.FormatSize(bytes), Is.EqualTo(expected));
        }

        private sealed class RecordingLauncher : IFolderLauncher
        {
            public List<string> Opened = new List<string>();
            public HashSet<string> MissingPaths = new HashSet<string>();

            public void Open(string path)
            {
                Opened.Add(path);
                if (MissingPaths.Contains(path)) throw new DirectoryNotFoundException(path);
            }
        }

        private sealed class FakeListView : ICompanyListView
        {
            public IList<CompanyRowViewModel> Rows = new List<CompanyRowViewModel>();
            public CompanyListStats Stats;
            public string BannerText;
            public bool BannerReadOnly;
            public bool AddEnabled;

            public string SearchText { get; set; }
            public string StatusFilter { get; set; }

            public event EventHandler SearchRequested;
            public event EventHandler AddRequested;
            public event EventHandler<CompanyRowActionEventArgs> RowActionRequested;

            public int Listeners
            {
                get
                {
                    return (SearchRequested == null ? 0 : 1) + (AddRequested == null ? 0 : 1) + (RowActionRequested == null ? 0 : 1);
                }
            }

            public void ShowBanner(string text, bool readOnly) { BannerText = text; BannerReadOnly = readOnly; }
            public void ShowAddEnabled(bool enabled) { AddEnabled = enabled; }
            public void ShowRows(IList<CompanyRowViewModel> rows, CompanyListStats stats) { Rows = rows; Stats = stats; }

            public void RaiseSearch() { if (SearchRequested != null) SearchRequested(this, EventArgs.Empty); }
            public void RaiseAdd() { if (AddRequested != null) AddRequested(this, EventArgs.Empty); }
            public void RaiseAction(CompanyId id, CompanyRowAction action)
            {
                if (RowActionRequested != null) RowActionRequested(this, new CompanyRowActionEventArgs(id, action));
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------

    [TestFixture]
    public class CompanyEditPresenterTests : CompanyUiTestBase
    {
        private FakeEditView _view;
        private FakePicker _picker;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeEditView();
            _picker = new FakePicker();
        }

        private CompanyEditPresenter Create(CompanyEditRequest request)
        {
            var presenter = new CompanyEditPresenter(
                _view, request, Registry, Management, Workflow, () => Nav, _picker, mst => Path.Combine(Root, "suggested", mst), Runner, Errors);
            presenter.Initialize();
            return presenter;
        }

        [Test]
        public void FirstRun_HasNoCancelOrBreadcrumb_AndStartsInvalid()
        {
            Entitlement.Value = AppMode.Standard;

            Create(CompanyEditRequest.FirstRun());

            Assert.That(_view.Config.ShowCancel, Is.False);
            Assert.That(_view.Config.ShowBreadcrumb, Is.False);
            Assert.That(_view.Config.Title, Is.EqualTo("Khai báo doanh nghiệp"));
            Assert.That(_view.PrimaryEnabled, Is.False);
            Assert.That(_view.Checks.Any(c => !c.Passed), Is.True);
            Assert.That(_view.Checks.Count, Is.EqualTo(4), "Chưa có MST hợp lệ thì chưa kiểm tra trùng MST");
        }

        [Test]
        public void TypingTaxCode_SuggestsFolder_UntilUserPicksOwnFolder()
        {
            Create(CompanyEditRequest.Add());

            _view.Type(tax: "0100000001");
            Assert.That(_view.Input.StoragePath, Is.EqualTo(Path.Combine(Root, "suggested", "0100000001")));

            _view.Type(tax: "0100000002");
            Assert.That(_view.Input.StoragePath, Is.EqualTo(Path.Combine(Root, "suggested", "0100000002")), "Gợi ý đi theo MST khi chưa tự chọn");

            _view.Type(path: Folder("mine"));
            _view.Type(tax: "0100000003");
            Assert.That(_view.Input.StoragePath, Is.EqualTo(Folder("mine")), "Thư mục người dùng chọn không bị ghi đè");
        }

        [Test]
        public void ValidInput_EnablesPrimary_InvalidDoesNot_AndChecksExplainWhy()
        {
            Create(CompanyEditRequest.Add());

            _view.Type(tax: "0100000001", name: "Công ty A");
            Assert.That(_view.PrimaryEnabled, Is.True);
            Assert.That(_view.Checks.All(c => c.Passed), Is.True);

            _view.Type(tax: "abc");
            Assert.That(_view.PrimaryEnabled, Is.False);
            Assert.That(_view.Checks.First(c => c.Key == CompanyCheckKey.TaxCodeFormat).Passed, Is.False);
        }

        [Test]
        public void DuplicateTaxCode_IsReportedLive()
        {
            Add("0100000001", "Đã có");
            Create(CompanyEditRequest.Add());

            _view.Type(tax: "0100000001", name: "Khác");

            Assert.That(_view.PrimaryEnabled, Is.False);
            Assert.That(_view.Checks.First(c => c.Key == CompanyCheckKey.TaxCodeUnique).Message, Does.Contain("Đã có"));
        }

        [Test]
        public void Browse_FillsPath_AndRevalidates_CancelledPickKeepsOld()
        {
            Create(CompanyEditRequest.Add());
            _view.Type(tax: "0100000001", name: "A");

            _picker.Result = Folder("picked");
            _view.RaiseBrowse();
            Assert.That(_view.Input.StoragePath, Is.EqualTo(Folder("picked")));

            _picker.Result = null;
            _view.RaiseBrowse();
            Assert.That(_view.Input.StoragePath, Is.EqualTo(Folder("picked")));
        }

        [Test]
        public void Primary_Creates_ThenWorksWithTheNewCompany()
        {
            Create(CompanyEditRequest.Add());
            _view.Type(tax: "0100000001", name: "  Công ty A  ", path: Folder("0100000001"));

            _view.RaisePrimary();

            var item = Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single();
            Assert.That(item.DisplayName, Is.EqualTo("Công ty A"));
            Assert.That(Storage.Inspect(item.StoragePath).IdentityTaxCode.Value, Is.EqualTo("0100000001"));
            Assert.That(Switch.Targets, Is.EqualTo(new[] { item.Id }));
            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Dashboard }));
            Assert.That(Shell.Logins.Count, Is.EqualTo(1));
        }

        [Test]
        public void FirstRunInStandard_CreatesTheOnlyCompany_AndLogsIn()
        {
            Entitlement.Value = AppMode.Standard;
            Create(CompanyEditRequest.FirstRun());
            _view.Type(tax: "0101234567", name: "Công ty Mẫu", path: Folder("0101234567"));

            _view.RaisePrimary();

            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Count, Is.EqualTo(1));
            Assert.That(Shell.Logins.Count, Is.EqualTo(1));
        }

        [Test]
        public void Primary_StandardSecondCompany_ShowsLicenceError_CreatesNothing()
        {
            Entitlement.Value = AppMode.Standard;
            Add("0100000001", "Một");
            Create(CompanyEditRequest.Add());
            _view.Type(tax: "0100000002", name: "Hai", path: Folder("0100000002"));

            _view.RaisePrimary();

            Assert.That(Errors.Shown.Single().Category, Is.EqualTo(ErrorCategory.Licence));
            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Count, Is.EqualTo(1));
            Assert.That(Directory.Exists(Folder("0100000002")), Is.False);
        }

        [Test]
        public void Cancel_ReturnsToList()
        {
            Create(CompanyEditRequest.Add());

            _view.RaiseCancel();

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.CompanyList }));
        }

        [Test]
        public void Edit_LoadsValues_TaxCodeReadOnly_PrimaryOnlyWhenSomethingChanged()
        {
            var item = Add("0100000001", "Cũ");

            Create(CompanyEditRequest.Edit(item.Id));

            Assert.That(_view.Config.TaxCodeReadOnly, Is.True);
            Assert.That(_view.Input.TaxCodeText, Is.EqualTo("0100000001"));
            Assert.That(_view.Input.DisplayName, Is.EqualTo("Cũ"));
            Assert.That(_view.PrimaryEnabled, Is.False, "Chưa đổi gì");

            _view.Type(name: "Mới");
            Assert.That(_view.PrimaryEnabled, Is.True);

            _view.Type(name: "   ");
            Assert.That(_view.PrimaryEnabled, Is.False);
        }

        [Test]
        public void Edit_Save_RenamesAndReturnsToList_TaxCodeUntouched()
        {
            var item = Add("0100000001", "Cũ");
            Create(CompanyEditRequest.Edit(item.Id));
            _view.Type(name: "Mới");

            _view.RaisePrimary();

            var stored = Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single();
            Assert.That(stored.DisplayName, Is.EqualTo("Mới"));
            Assert.That(stored.TaxCode.Value, Is.EqualTo("0100000001"));
            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.CompanyList }));
        }

        [Test]
        public void Edit_Relink_AfterFolderMoved_ValidatesTheNewFolder()
        {
            var item = Add("0100000001", "A");
            var moved = Folder("moved");
            Directory.Move(item.StoragePath, moved);
            Create(CompanyEditRequest.Edit(item.Id));

            _view.Type(path: Folder("nothing-here"));
            Assert.That(_view.PrimaryEnabled, Is.False, "Thư mục không phải kho của MST này");

            _view.Type(path: moved);
            Assert.That(_view.PrimaryEnabled, Is.True);
            _view.RaisePrimary();

            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single().StoragePath, Is.EqualTo(moved));
            Assert.That(Registry.GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult().Single().Status, Is.EqualTo(CompanyCatalogStatus.Active));
        }

        [Test]
        public void Dispose_UnsubscribesEvents()
        {
            var presenter = Create(CompanyEditRequest.Add());

            presenter.Dispose();

            Assert.That(_view.Listeners, Is.EqualTo(0));
        }

        private sealed class FakePicker : IFolderPicker
        {
            public string Result;
            public string Pick(string initialPath) { return Result; }
        }

        private sealed class FakeEditView : ICompanyEditView
        {
            public CompanyEditConfig Config;
            public CompanyInput Input = new CompanyInput { TaxCodeText = "", DisplayName = "", StoragePath = "" };
            public IList<CompanyCheck> Checks = new List<CompanyCheck>();
            public bool PrimaryEnabled;
            public string Message;

            public event EventHandler InputChanged;
            public event EventHandler TaxCodeEdited;
            public event EventHandler BrowseRequested;
            public event EventHandler PrimaryRequested;
            public event EventHandler CancelRequested;

            public int Listeners
            {
                get
                {
                    return Count(InputChanged) + Count(TaxCodeEdited) + Count(BrowseRequested) + Count(PrimaryRequested) + Count(CancelRequested);
                }
            }

            public CompanyInput ReadInput() { return Input; }
            public void Configure(CompanyEditConfig config) { Config = config; }

            public void ShowValues(string taxCode, string displayName, string storagePath)
            {
                Input = new CompanyInput { TaxCodeText = taxCode, DisplayName = displayName, StoragePath = storagePath };
            }

            public void SetStoragePath(string path)
            {
                Input.StoragePath = path;
                if (InputChanged != null) InputChanged(this, EventArgs.Empty);
            }

            public void ShowChecks(IList<CompanyCheck> checks) { Checks = checks; }
            public void SetPrimaryEnabled(bool enabled) { PrimaryEnabled = enabled; }
            public void ShowMessage(string message) { Message = message; }

            /// <summary>Mô phỏng người dùng gõ; bắn đúng chuỗi sự kiện của control thật.</summary>
            public void Type(string tax = null, string name = null, string path = null)
            {
                if (tax != null)
                {
                    Input.TaxCodeText = tax;
                    if (TaxCodeEdited != null) TaxCodeEdited(this, EventArgs.Empty);
                    if (InputChanged != null) InputChanged(this, EventArgs.Empty);
                }

                if (name != null)
                {
                    Input.DisplayName = name;
                    if (InputChanged != null) InputChanged(this, EventArgs.Empty);
                }

                if (path != null)
                {
                    Input.StoragePath = path;
                    if (InputChanged != null) InputChanged(this, EventArgs.Empty);
                }
            }

            public void RaiseBrowse() { if (BrowseRequested != null) BrowseRequested(this, EventArgs.Empty); }
            public void RaisePrimary() { if (PrimaryRequested != null) PrimaryRequested(this, EventArgs.Empty); }
            public void RaiseCancel() { if (CancelRequested != null) CancelRequested(this, EventArgs.Empty); }

            private static int Count(EventHandler handler)
            {
                return handler == null ? 0 : 1;
            }
        }
    }
}
