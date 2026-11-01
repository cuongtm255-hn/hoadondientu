using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Navigation;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class NavigationServiceTests
    {
        private FakeScreenFactory _factory;
        private RecordingView _view;
        private ModeEntitlement _entitlement;
        private FakeContextAccessor _context;
        private NavigationService _navigation;

        [SetUp]
        public void SetUp()
        {
            _factory = new FakeScreenFactory();
            _view = new RecordingView();
            _entitlement = new ModeEntitlement(AppMode.Service);
            _context = new FakeContextAccessor(FakeContextAccessor.Context("0101234567"));
            _navigation = new NavigationService(_factory, _view, _entitlement, _context, new RecordingLogger());
        }

        private NavigationResult Go(ScreenId target, object parameter = null)
        {
            return _navigation.NavigateAsync(target, parameter, CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void Initially_CurrentIsInitializing()
        {
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.Initializing));
        }

        [Test]
        public void Navigate_FirstScreen_ShowsItAndSelectsMenu()
        {
            var result = Go(ScreenId.Dashboard);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.Dashboard));
            Assert.That(_view.Shown.Count, Is.EqualTo(1));
            Assert.That(_view.Selected, Is.EqualTo(ScreenId.Dashboard));
        }

        [Test]
        public void Navigate_Replace_CancelsAndDisposesOldBeforeCreatingNew()
        {
            Go(ScreenId.Dashboard);

            Go(ScreenId.InvoiceArchive);

            Assert.That(_factory.Log, Is.EqualTo(new[]
            {
                "create:Dashboard", "cancel:Dashboard", "dispose:Dashboard", "create:InvoiceArchive"
            }));
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.InvoiceArchive));
        }

        [Test]
        public void Navigate_CurrentCannotClose_Blocked_KeepsScreenAndMenu()
        {
            Go(ScreenId.Dashboard);
            _factory.Created[0].CanClose = false;

            var result = Go(ScreenId.InvoiceArchive);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Reason, Is.Not.Null.And.Not.Empty);
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.Dashboard));
            Assert.That(_view.Selected, Is.EqualTo(ScreenId.Dashboard));
            Assert.That(_factory.Created[0].Disposed, Is.False);
            Assert.That(_factory.Created[0].Cancelled, Is.False);
            Assert.That(_factory.Log, Is.EqualTo(new[] { "create:Dashboard" }));
        }

        [Test]
        public void Navigate_CanCloseThrows_TreatedAsCannotClose()
        {
            Go(ScreenId.Dashboard);
            _factory.Created[0].CanCloseException = new InvalidOperationException("boom");

            var result = Go(ScreenId.Settings);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.Dashboard));
        }

        [Test]
        public void Navigate_SameScreenWithoutParameter_IsNoOp()
        {
            Go(ScreenId.Dashboard);

            var result = Go(ScreenId.Dashboard);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_factory.Created.Count, Is.EqualTo(1));
        }

        [Test]
        public void Navigate_SameScreenWithParameter_Recreates()
        {
            Go(ScreenId.InvoiceDetail, "A");

            Go(ScreenId.InvoiceDetail, "B");

            Assert.That(_factory.Created.Count, Is.EqualTo(2));
            Assert.That(_factory.Created[0].Disposed, Is.True);
        }

        [Test]
        public void Navigate_CreateFails_FallsBackToRecovery()
        {
            _factory.Failing.Add(ScreenId.Dashboard);

            var result = Go(ScreenId.Dashboard);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.Recovery));
            Assert.That(_view.Selected, Is.EqualTo(ScreenId.Recovery));
        }

        [Test]
        public void Navigate_RecoveryCreationAlsoFails_Throws()
        {
            _factory.Failing.Add(ScreenId.Dashboard);
            _factory.Failing.Add(ScreenId.Recovery);

            Assert.Throws<InvalidOperationException>(() => Go(ScreenId.Dashboard));
        }

        [Test]
        public void Navigate_WhileAnotherNavigationRuns_IsBlocked()
        {
            Go(ScreenId.Dashboard);
            var release = new TaskCompletionSource<bool>();
            _factory.Created[0].PendingCanClose = release.Task;

            var first = _navigation.NavigateAsync(ScreenId.Settings, null, CancellationToken.None);
            var second = Go(ScreenId.InvoiceArchive);
            release.SetResult(true);
            var firstResult = first.GetAwaiter().GetResult();

            Assert.That(second.Succeeded, Is.False);
            Assert.That(firstResult.Succeeded, Is.True);
            Assert.That(_navigation.CurrentScreen, Is.EqualTo(ScreenId.Settings));
        }

        [Test]
        public void Rules_ScreensNeedingCompany_BlockedWithoutContext()
        {
            _context.Current = null;

            foreach (var id in new[] { ScreenId.Dashboard, ScreenId.Download, ScreenId.InvoiceArchive, ScreenId.DownloadHistory })
            {
                Assert.That(_navigation.CanNavigate(id), Is.False, id.ToString());
            }

            Assert.That(Go(ScreenId.Dashboard).Reason, Does.Contain("doanh nghiệp"));
            Assert.That(_factory.Created, Is.Empty);
        }

        [Test]
        public void Rules_AlwaysAllowed_Screens()
        {
            _context.Current = null;

            foreach (var id in new[] { ScreenId.Initializing, ScreenId.Recovery, ScreenId.Settings, ScreenId.License })
            {
                Assert.That(_navigation.CanNavigate(id), Is.True, id.ToString());
            }
        }

        [Test]
        public void Rules_Standard_CompanyList_BlockedWhenCompanyOpen_AllowedForFirstRun()
        {
            _entitlement.Mode = AppMode.Standard;

            Assert.That(_navigation.CanNavigate(ScreenId.CompanyList), Is.False);

            _context.Current = null;
            Assert.That(_navigation.CanNavigate(ScreenId.CompanyEdit), Is.True);
        }

        [Test]
        public void Rules_ReadOnly_CanListCompanies_ButNotDownload()
        {
            _entitlement.Mode = AppMode.ReadOnlyService;

            Assert.That(_navigation.CanNavigate(ScreenId.CompanyList), Is.True);
            Assert.That(_navigation.CanNavigate(ScreenId.Download), Is.False);
            Assert.That(_navigation.CanNavigate(ScreenId.InvoiceArchive), Is.True);
            Assert.That(Go(ScreenId.Download).Reason, Does.Contain("không cho phép"));
        }

        [Test]
        public void Dispose_CancelsAndDisposesCurrentScreen()
        {
            Go(ScreenId.Dashboard);

            _navigation.Dispose();

            Assert.That(_factory.Created[0].Cancelled, Is.True);
            Assert.That(_factory.Created[0].Disposed, Is.True);
        }

        [Test]
        public void Navigate_BlockedByRule_RevertsMenuSelectionToCurrent()
        {
            Go(ScreenId.Dashboard);
            _entitlement.Mode = AppMode.ReadOnlyService;

            Go(ScreenId.Download);

            Assert.That(_view.Selected, Is.EqualTo(ScreenId.Dashboard));
        }

        private sealed class RecordingView : INavigationView
        {
            public List<Control> Shown = new List<Control>();
            public ScreenId Selected;

            public void ShowScreen(Control screen) { Shown.Add(screen); }
            public void SelectNavigation(ScreenId screen) { Selected = screen; }
        }

        private sealed class ModeEntitlement : IEntitlementService
        {
            public ModeEntitlement(AppMode mode)
            {
                Mode = mode;
            }

            public AppMode Mode { get; set; }
            public AppMode CurrentMode { get { return Mode; } }

            public bool IsAllowed(AppFeature feature)
            {
                switch (Mode)
                {
                    case AppMode.Standard: return feature != AppFeature.MultiCompany;
                    case AppMode.Service: return true;
                    default: return feature == AppFeature.ReadExport;
                }
            }

            public void Demand(AppFeature feature)
            {
                if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature);
            }

            public event EventHandler ModeChanged { add { } remove { } }
        }
    }
}
