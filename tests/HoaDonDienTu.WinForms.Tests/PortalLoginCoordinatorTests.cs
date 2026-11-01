using System;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.WinForms.Login;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class PortalLoginCoordinatorTests
    {
        private const string Mst = "0101234567";
        private FakeContextAccessor _context;
        private FakeEntitlement _entitlement;
        private InMemoryPortalSessionStore _store;
        private FakeAuth _auth;
        private FakeBrowser _browser;
        private FakeLoginView _view;
        private int _browserCreated;
        private PortalLoginCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _context = new FakeContextAccessor(FakeContextAccessor.Context(Mst));
            _entitlement = new FakeEntitlement();
            _store = new InMemoryPortalSessionStore();
            _auth = new FakeAuth { Handler = FakeAuth.SuccessFor };
            _browser = new FakeBrowser();
            _view = new FakeLoginView();
            _browserCreated = 0;
            _coordinator = new PortalLoginCoordinator(
                _context, _entitlement, _store, _auth,
                () => { _browserCreated++; return _browser; },
                () => _view,
                new RecordingLogger());
        }

        private PortalLoginRequest Request(string tax)
        {
            return new PortalLoginRequest(_context.Current.CompanyId, HoaDonDienTu.Domain.Companies.TaxCode.Parse(tax),
                LoginReason.Manual, ScreenId.Dashboard, null);
        }

        private PortalLoginResult Login(PortalLoginRequest request)
        {
            return _coordinator.LoginAsync(request, null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void NoContext_NotAllowed_NothingCreated()
        {
            var request = Request(Mst);
            _context.Current = null;

            var result = Login(request);

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.NotAllowed));
            Assert.That(_browserCreated, Is.EqualTo(0));
        }

        [Test]
        public void RequestForOtherTaxCode_NotAllowed()
        {
            var result = Login(Request("0109999999"));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.NotAllowed));
            Assert.That(_browserCreated, Is.EqualTo(0));
        }

        [Test]
        public void EntitlementDenied_NotAllowed_NothingCreated()
        {
            _entitlement.Allowed = false;

            var result = Login(Request(Mst));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.NotAllowed));
            Assert.That(_browserCreated, Is.EqualTo(0));
        }

        [Test]
        public void FullFlow_Succeeds_AndStoresSession()
        {
            _view.OnShowModal = v => { v.RaiseShown(); _browser.RaiseCandidate(); };

            var result = Login(Request(Mst));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_store.HasActiveSession, Is.True);
            Assert.That(_browser.Disposed, Is.True);
        }

        [Test]
        public void ViewClosedWithoutResult_IsCancelled()
        {
            var result = Login(Request(Mst));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.Cancelled));
        }

        [Test]
        public void SecondLoginWhileFirstIsOpen_IsRejected_OnlyOneViewCreated()
        {
            PortalLoginResult nested = null;
            var viewsCreated = 0;
            var coordinator = new PortalLoginCoordinator(
                _context, _entitlement, _store, _auth, () => _browser, () => { viewsCreated++; return _view; }, new RecordingLogger());
            _view.OnShowModal = v =>
                nested = coordinator.LoginAsync(Request(Mst), null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();

            coordinator.LoginAsync(Request(Mst), null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(nested.Outcome, Is.EqualTo(PortalLoginOutcome.AlreadyInProgress));
            Assert.That(viewsCreated, Is.EqualTo(1));
        }

        [Test]
        public void AfterFirstLoginEnds_NextLoginIsAllowed()
        {
            Login(Request(Mst));

            var second = Login(Request(Mst));

            Assert.That(second.Outcome, Is.Not.EqualTo(PortalLoginOutcome.AlreadyInProgress));
        }
    }
}
