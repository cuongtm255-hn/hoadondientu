using System.Collections.Generic;
using HoaDonDienTu.Application.Groups;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>T-DVC00-xx (DD-DVC-00). Dùng shell giả, không chạm cổng thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class GroupNavigatorTests
    {
        private FakeFactory _factory;
        private FakeStore _store;
        private GroupNavigator _nav;

        [SetUp]
        public void SetUp()
        {
            _factory = new FakeFactory();
            _store = new FakeStore();
            _nav = new GroupNavigator(_factory, _store);
        }

        [TearDown]
        public void TearDown()
        {
            _nav.Dispose();
        }

        [Test]
        public void T_DVC00_01_NoLastGroup_SuggestsInvoice()
        {
            Assert.AreEqual(AppGroup.Invoice, _nav.SuggestedGroup);
        }

        [Test]
        public void T_DVC00_02_LastGroupIsRemembered_AndSaved()
        {
            _store.Last = AppGroup.PublicService;
            Assert.AreEqual(AppGroup.PublicService, _nav.SuggestedGroup);

            _nav.Open(AppGroup.Invoice);
            Assert.AreEqual(AppGroup.Invoice, _store.Last);
        }

        [Test]
        public void T_DVC00_04_UnchosenGroup_IsNotCreated()
        {
            _nav.Open(AppGroup.Invoice);

            Assert.IsTrue(_nav.IsCreated(AppGroup.Invoice));
            Assert.IsFalse(_nav.IsCreated(AppGroup.PublicService));
            CollectionAssert.AreEqual(new[] { AppGroup.Invoice }, _factory.Created);
        }

        [Test]
        public void T_DVC00_05_Switch_KeepsOtherShellInstance_NotDisposed()
        {
            _nav.Open(AppGroup.Invoice);
            FakeShell invoice = _factory.Shells[AppGroup.Invoice];

            Assert.IsTrue(_nav.Switch(AppGroup.PublicService, null));
            Assert.IsTrue(invoice.Hidden);
            Assert.IsFalse(invoice.Disposed);

            Assert.IsTrue(_nav.Switch(AppGroup.Invoice, null));
            Assert.AreSame(invoice, _factory.Shells[AppGroup.Invoice]);
            Assert.AreEqual(1, _factory.Created.FindAll(g => g == AppGroup.Invoice).Count, "Không tạo lại shell cũ");
            Assert.AreEqual(AppGroup.Invoice, _nav.Active);
        }

        [Test]
        public void T_DVC00_06_Switch_WithRunningTask_Declined_Stays()
        {
            _nav.Open(AppGroup.Invoice);
            FakeShell invoice = _factory.Shells[AppGroup.Invoice];
            invoice.Busy = "Đang tải hóa đơn";
            string asked = null;

            bool switched = _nav.Switch(AppGroup.PublicService, r => { asked = r; return false; });

            Assert.IsFalse(switched);
            Assert.AreEqual("Đang tải hóa đơn", asked);
            Assert.AreEqual(AppGroup.Invoice, _nav.Active);
            Assert.IsFalse(invoice.Hidden);
            Assert.IsFalse(invoice.Stopped);
            Assert.IsFalse(_nav.IsCreated(AppGroup.PublicService));
        }

        [Test]
        public void T_DVC00_06_Switch_WithRunningTask_Confirmed_StopsAndSwitches()
        {
            _nav.Open(AppGroup.Invoice);
            FakeShell invoice = _factory.Shells[AppGroup.Invoice];
            invoice.Busy = "Đang tải hóa đơn";

            Assert.IsTrue(_nav.Switch(AppGroup.PublicService, r => true));

            Assert.IsTrue(invoice.Stopped);
            Assert.IsTrue(invoice.Hidden);
            Assert.AreEqual(AppGroup.PublicService, _nav.Active);
        }

        [Test]
        public void T_DVC00_06_Switch_Idle_DoesNotAsk()
        {
            _nav.Open(AppGroup.Invoice);
            bool asked = false;

            Assert.IsTrue(_nav.Switch(AppGroup.PublicService, r => { asked = true; return false; }));
            Assert.IsFalse(asked);
        }

        [Test]
        public void T_DVC00_07_NothingOpened_CreatesNoShell()
        {
            Assert.IsNull(_nav.Active);
            Assert.AreEqual(0, _factory.Created.Count);
        }

        [Test]
        public void Dispose_DisposesAllShells()
        {
            _nav.Open(AppGroup.Invoice);
            _nav.Switch(AppGroup.PublicService, null);
            FakeShell a = _factory.Shells[AppGroup.Invoice];
            FakeShell b = _factory.Shells[AppGroup.PublicService];

            _nav.Dispose();

            Assert.IsTrue(a.Disposed);
            Assert.IsTrue(b.Disposed);
            Assert.IsNull(_nav.Active);
        }

        private sealed class FakeStore : IGroupPreferenceStore
        {
            public AppGroup? Last;
            public AppGroup? GetLastGroup() { return Last; }
            public void SetLastGroup(AppGroup group) { Last = group; }
        }

        private sealed class FakeFactory : IGroupShellFactory
        {
            public readonly List<AppGroup> Created = new List<AppGroup>();
            public readonly Dictionary<AppGroup, FakeShell> Shells = new Dictionary<AppGroup, FakeShell>();

            public IGroupShell Create(AppGroup group)
            {
                Created.Add(group);
                var shell = new FakeShell();
                Shells[group] = shell;
                return shell;
            }
        }

        private sealed class FakeShell : IGroupShell
        {
            public string Busy;
            public bool Shown, Hidden, Stopped, Disposed;

            public void Show() { Shown = true; Hidden = false; }
            public void Hide() { Hidden = true; }
            public bool CanLeave(out string reason) { reason = Busy; return Busy == null; }
            public void StopRunningTasks() { Stopped = true; }
            public void Dispose() { Disposed = true; }
        }
    }
}
