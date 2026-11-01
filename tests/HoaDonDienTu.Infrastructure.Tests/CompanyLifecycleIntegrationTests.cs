using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>
    /// Kiểm tra các thành phần thật phối hợp với nhau (DD-01 mục 19.5): context, phiên cổng Thuế, client API,
    /// chuyển MST, chốt chặn ghi và đóng ứng dụng, trên thư mục tạm. Kho SQLite, DPAPI và licence thật chờ DD-02/03/06.
    /// </summary>
    [TestFixture]
    public class CompanyLifecycleIntegrationTests
    {
        private string _root;
        private CompanyProfile _a;
        private CompanyProfile _b;
        private TrackingOpener _opener;
        private CompanyContextManager _manager;
        private InMemoryPortalSessionStore _store;
        private PortalApiClientProvider _provider;
        private FakeClientFactory _clients;
        private SwitchableEntitlement _entitlement;
        private FakeJobs _jobs;
        private CompanySwitchService _switch;
        private StorageGuard _guard;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hddt-it-" + Guid.NewGuid().ToString("N"));
            _a = new CompanyProfile(CompanyId.NewId(), "A", TaxCode.Parse("0101234561"));
            _b = new CompanyProfile(CompanyId.NewId(), "B", TaxCode.Parse("0101234562"));
            _opener = new TrackingOpener(_root);
            _manager = new CompanyContextManager(_opener);
            _store = new InMemoryPortalSessionStore();
            _clients = new FakeClientFactory();
            _provider = new PortalApiClientProvider(_store, _manager, _clients);
            _entitlement = new SwitchableEntitlement { Mode = AppMode.Service };
            _jobs = new FakeJobs();
            _switch = new CompanySwitchService(
                new FakeCatalog(_a, _b), _manager, _jobs, _store, _entitlement, new NullLogger());
            _guard = new StorageGuard(_manager, _store, _entitlement);
        }

        [TearDown]
        public void TearDown()
        {
            _provider.Dispose();
            _manager.CloseCurrent();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private CompanySwitchResult Switch(CompanyProfile target)
        {
            return _switch.SwitchAsync(target.Id, ActiveJobResolution.NotRequired, CancellationToken.None).GetAwaiter().GetResult();
        }

        private void Login(CompanyProfile company)
        {
            _store.Set(new PortalSession(company.Id, company.TaxCode, new PortalSessionMaterial(null, null, "token"), DateTime.UtcNow));
        }

        [Test]
        public void TwoCompanies_HaveIndependentStorageRoots()
        {
            Switch(_a);
            var rootA = _manager.Current.StorageRoot;
            Switch(_b);
            var rootB = _manager.Current.StorageRoot;

            Assert.That(rootA, Is.Not.EqualTo(rootB));
            Assert.That(Directory.Exists(rootA), Is.True);
            Assert.That(Directory.Exists(rootB), Is.True);
            Assert.That(rootB, Does.Not.StartWith(rootA + Path.DirectorySeparatorChar));
        }

        [Test]
        public void Service_NeverHoldsTwoContextsAtOnce_AcrossManySwitches()
        {
            Switch(_a);
            Switch(_b);
            Switch(_a);
            Switch(_b);

            Assert.That(_opener.MaxConcurrentOpen, Is.EqualTo(1));
            Assert.That(_opener.OpenNow, Is.EqualTo(1));
        }

        [Test]
        public void Switch_ClearsSession_AndDisposesApiClientOfPreviousCompany()
        {
            Switch(_a);
            Login(_a);
            var client = (FakeClient)_provider.GetClient();

            Switch(_b);

            Assert.That(_store.HasActiveSession, Is.False);
            Assert.That(client.Disposed, Is.True);
            Assert.Throws<InvalidOperationException>(() => _provider.GetClient());
        }

        [Test]
        public void NewLoginAfterSwitch_GetsFreshClient()
        {
            Switch(_a);
            Login(_a);
            var first = _provider.GetClient();
            Switch(_b);
            Login(_b);

            var second = _provider.GetClient();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(_clients.Created, Is.EqualTo(2));
        }

        [Test]
        public void OldSessionGeneration_IsInvalidatedWhenSessionReplaced()
        {
            Switch(_a);
            Login(_a);
            var first = (FakeClient)_provider.GetClient();

            Login(_a);
            var second = _provider.GetClient();

            Assert.That(first.Disposed, Is.True);
            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void SwitchFailure_RestoresPrevious_ButStillClearsSession()
        {
            Switch(_a);
            Login(_a);
            _opener.Failing.Add(_b.Id);

            var result = Switch(_b);

            Assert.That(result.Outcome, Is.EqualTo(CompanySwitchOutcome.RestoredPrevious));
            Assert.That(_manager.Current.CompanyId, Is.EqualTo(_a.Id));
            Assert.That(_store.HasActiveSession, Is.False, "Phiên không được sống sót qua một lần chuyển MST dù thất bại");
            Assert.That(_opener.MaxConcurrentOpen, Is.EqualTo(1));
        }

        [Test]
        public void StorageGuard_RejectsWritesIntoOtherCompanyFolder()
        {
            Switch(_a);
            Login(_a);
            var rootA = _manager.Current.StorageRoot;
            Switch(_b);
            Login(_b);
            var rootB = _manager.Current.StorageRoot;

            Assert.DoesNotThrow(() => _guard.EnsureCanWrite(
                _b.Id, _b.TaxCode, Path.Combine(rootB, "2026", "x.xml"), AppFeature.NewDownload, true));
            Assert.Throws<StorageGuardException>(() => _guard.EnsureCanWrite(
                _b.Id, _b.TaxCode, Path.Combine(rootA, "2026", "x.xml"), AppFeature.NewDownload, true));
            Assert.Throws<StorageGuardException>(() => _guard.EnsureCanWrite(
                _a.Id, _a.TaxCode, Path.Combine(rootB, "2026", "x.xml"), AppFeature.NewDownload, true));
        }

        [Test]
        public void ReadOnlyMode_CanOpenStore_ButNewDownloadWritesAreDenied()
        {
            _entitlement.Mode = AppMode.ReadOnlyService;
            Switch(_a);
            var root = _manager.Current.StorageRoot;

            Assert.That(_manager.HasCurrent, Is.True);
            Assert.Throws<EntitlementDeniedException>(() => _guard.EnsureCanWrite(
                _a.Id, _a.TaxCode, Path.Combine(root, "x.xml"), AppFeature.NewDownload, false));
        }

        [Test]
        public void Shutdown_ClearsSessionAndReleasesContext()
        {
            Switch(_a);
            Login(_a);
            _provider.GetClient();
            var shutdown = new ShutdownCoordinator(_manager, _jobs, _store, new StayPrompt(), new NullLogger());

            var result = shutdown.TryShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.CanClose, Is.True);
            Assert.That(_store.HasActiveSession, Is.False);
            Assert.That(_manager.HasCurrent, Is.False);
            Assert.That(_opener.OpenNow, Is.EqualTo(0));
        }

        [Test]
        public void Shutdown_WithRunningJob_PauseThenReleasesEverything()
        {
            Switch(_a);
            Login(_a);
            _jobs.Running = true;
            var shutdown = new ShutdownCoordinator(_manager, _jobs, _store, new PauseChoice(), new NullLogger());

            var result = shutdown.TryShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.CanClose, Is.True);
            Assert.That(_jobs.Paused, Is.True);
            Assert.That(_opener.OpenNow, Is.EqualTo(0));
        }

        private sealed class TrackingOpener : ICompanyStorageOpener
        {
            private readonly string _root;
            private int _open;

            public TrackingOpener(string root)
            {
                _root = root;
                Failing = new HashSet<CompanyId>();
            }

            public HashSet<CompanyId> Failing { get; private set; }
            public int MaxConcurrentOpen { get; private set; }
            public int OpenNow { get { return _open; } }

            public Task<CompanyContext> OpenAsync(CompanyProfile company, CancellationToken cancellationToken)
            {
                if (Failing.Contains(company.Id))
                {
                    throw new CompanyStorageException(CompanySwitchBlocker.DatabaseInvalid, "db");
                }

                var path = Path.Combine(_root, company.TaxCode.Value);
                Directory.CreateDirectory(path);
                _open++;
                if (_open > MaxConcurrentOpen) MaxConcurrentOpen = _open;
                return Task.FromResult(new CompanyContext(
                    company.Id, company.TaxCode, company.DisplayName, path, new Release(() => _open--)));
            }

            private sealed class Release : IDisposable
            {
                private readonly Action _action;

                public Release(Action action)
                {
                    _action = action;
                }

                public void Dispose()
                {
                    _action();
                }
            }
        }

        private sealed class FakeClient : IPortalApiClient
        {
            public bool Disposed;

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class FakeClientFactory : IPortalApiClientFactory
        {
            public int Created;

            public IPortalApiClient Create(CompanyContext company, PortalSession session)
            {
                Created++;
                return new FakeClient();
            }
        }

        private sealed class FakeCatalog : ICompanyCatalog
        {
            private readonly CatalogSnapshot _snapshot;

            public FakeCatalog(params CompanyProfile[] companies)
            {
                _snapshot = new CatalogSnapshot(CatalogLoadStatus.Loaded, companies, true);
            }

            public Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_snapshot);
            }
        }

        private sealed class FakeJobs : IActiveJobService
        {
            public bool Running;
            public bool Paused;

            public Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Running);
            }

            public Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                Running = false;
                return Task.FromResult(true);
            }

            public Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                Paused = true;
                Running = false;
                return Task.FromResult(true);
            }

            public Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken)
            {
                Running = false;
                return Task.FromResult(true);
            }
        }

        private sealed class SwitchableEntitlement : IEntitlementService
        {
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

        private sealed class StayPrompt : IShutdownPrompt
        {
            public ShutdownChoice Ask(string companyName)
            {
                return ShutdownChoice.StayOpen;
            }
        }

        private sealed class PauseChoice : IShutdownPrompt
        {
            public ShutdownChoice Ask(string companyName)
            {
                return ShutdownChoice.PauseAndExit;
            }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }
    }
}
