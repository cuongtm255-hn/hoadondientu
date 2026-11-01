using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class StartupCoordinatorTests
    {
        [Test]
        public void NoLicense_NoCompany_StandardFirstRun()
        {
            var result = Run(LicenseStatus.None, CatalogSnapshot.Empty());

            Assert.That(result.Mode, Is.EqualTo(AppMode.Standard));
            Assert.That(result.Route, Is.EqualTo(StartupRoute.StandardFirstRun));
        }

        [Test]
        public void NoLicense_OneCompany_StandardPortalLogin()
        {
            var catalog = Loaded(1, false);

            var result = Run(LicenseStatus.None, catalog);

            Assert.That(result.Mode, Is.EqualTo(AppMode.Standard));
            Assert.That(result.Route, Is.EqualTo(StartupRoute.StandardPortalLogin));
            Assert.That(result.StandardCompany, Is.SameAs(catalog.Companies[0]));
        }

        [Test]
        public void ValidLicense_ServiceCompanySelection_NoCompanyAutoOpened()
        {
            var result = Run(LicenseStatus.Valid, Loaded(3, true));

            Assert.That(result.Mode, Is.EqualTo(AppMode.Service));
            Assert.That(result.Route, Is.EqualTo(StartupRoute.ServiceCompanySelection));
            Assert.That(result.StandardCompany, Is.Null);
        }

        [Test]
        public void GracePeriod_OfflineServer_ServiceWithWarning()
        {
            var license = new FakeLicense(LicenseStatus.GracePeriod) { OnlineException = new InvalidOperationException("offline") };

            var result = Run(license, Loaded(2, true));

            Assert.That(result.Mode, Is.EqualTo(AppMode.Service));
            Assert.That(result.Route, Is.EqualTo(StartupRoute.ServiceCompanySelection));
            Assert.That(result.UserMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ValidLocal_OfflineServer_StillService_NoWarning()
        {
            var license = new FakeLicense(LicenseStatus.Valid) { OnlineException = new InvalidOperationException("offline") };

            var result = Run(license, Loaded(2, true));

            Assert.That(result.Mode, Is.EqualTo(AppMode.Service));
            Assert.That(result.UserMessage, Is.Null);
        }

        [TestCase(LicenseStatus.Revoked)]
        [TestCase(LicenseStatus.Expired)]
        public void OnlineRevokedOrExpired_WithServiceHistory_ReadOnly(LicenseStatus online)
        {
            var license = new FakeLicense(LicenseStatus.Valid) { Online = online };

            var result = Run(license, Loaded(2, true));

            Assert.That(result.Mode, Is.EqualTo(AppMode.ReadOnlyService));
            Assert.That(result.Route, Is.EqualTo(StartupRoute.ReadOnlyCompanySelection));
        }

        [TestCase(LicenseStatus.Expired)]
        [TestCase(LicenseStatus.Revoked)]
        public void ExpiredLocal_WithServiceHistory_ReadOnly(LicenseStatus local)
        {
            var result = Run(local, Loaded(2, true));

            Assert.That(result.Mode, Is.EqualTo(AppMode.ReadOnlyService));
            Assert.That(result.Route, Is.EqualTo(StartupRoute.ReadOnlyCompanySelection));
        }

        [Test]
        public void MultipleCompanies_NoLicense_ReadOnly_NotHiddenAsStandard()
        {
            var result = Run(LicenseStatus.None, Loaded(2, false));

            Assert.That(result.Mode, Is.EqualTo(AppMode.ReadOnlyService));
        }

        [Test]
        public void InvalidLicense_WithManyCompanies_Recovery()
        {
            var result = Run(LicenseStatus.Invalid, Loaded(3, true));

            Assert.That(result.Route, Is.EqualTo(StartupRoute.Recovery));
            Assert.That(result.RecoveryReason, Is.EqualTo(RecoveryReason.LicenseCorrupt));
        }

        [Test]
        public void LocalLicenseThrows_TreatedAsInvalid()
        {
            var license = new FakeLicense(LicenseStatus.Valid) { LocalException = new InvalidOperationException("bad file") };

            var result = Run(license, Loaded(3, true));

            Assert.That(result.Route, Is.EqualTo(StartupRoute.Recovery));
            Assert.That(result.RecoveryReason, Is.EqualTo(RecoveryReason.LicenseCorrupt));
        }

        [Test]
        public void CorruptCatalog_Recovery()
        {
            var result = Run(LicenseStatus.Valid, new CatalogSnapshot(CatalogLoadStatus.Corrupt, null, false));

            Assert.That(result.Route, Is.EqualTo(StartupRoute.Recovery));
            Assert.That(result.RecoveryReason, Is.EqualTo(RecoveryReason.CatalogCorrupt));
        }

        [Test]
        public void CatalogThrows_Recovery()
        {
            var coordinator = new StartupCoordinator(
                new FakeLicense(LicenseStatus.None),
                new FakeCatalog(null) { Exception = new InvalidOperationException("io") },
                new NullLogger());

            var result = coordinator.InitializeAsync(null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Route, Is.EqualTo(StartupRoute.Recovery));
            Assert.That(result.RecoveryReason, Is.EqualTo(RecoveryReason.CatalogCorrupt));
        }

        [Test]
        public void Cancellation_Propagates()
        {
            var license = new FakeLicense(LicenseStatus.None) { LocalException = new OperationCanceledException() };
            var coordinator = new StartupCoordinator(license, new FakeCatalog(CatalogSnapshot.Empty()), new NullLogger());

            Assert.Throws<OperationCanceledException>(
                () => coordinator.InitializeAsync(null, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void OnlineValidation_OnlyWhenProvisionalServiceMode()
        {
            var license = new FakeLicense(LicenseStatus.None);

            Run(license, Loaded(1, false));

            Assert.That(license.OnlineCalls, Is.EqualTo(0));
        }

        private static StartupResult Run(LicenseStatus local, CatalogSnapshot catalog)
        {
            return Run(new FakeLicense(local), catalog);
        }

        private static StartupResult Run(FakeLicense license, CatalogSnapshot catalog)
        {
            var coordinator = new StartupCoordinator(license, new FakeCatalog(catalog), new NullLogger());
            return coordinator.InitializeAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static CatalogSnapshot Loaded(int count, bool history)
        {
            var list = new List<CompanyProfile>();
            for (var i = 0; i < count; i++)
            {
                list.Add(new CompanyProfile(CompanyId.NewId(), "C" + i, TaxCode.Parse("01012345" + (60 + i).ToString("00"))));
            }

            return new CatalogSnapshot(CatalogLoadStatus.Loaded, list, history);
        }

        private sealed class FakeLicense : ILicenseService
        {
            private readonly LicenseStatus _local;

            public FakeLicense(LicenseStatus local)
            {
                _local = local;
                Online = local;
            }

            public LicenseStatus Online { get; set; }
            public Exception LocalException { get; set; }
            public Exception OnlineException { get; set; }
            public int OnlineCalls { get; private set; }

            public Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken)
            {
                if (LocalException != null) throw LocalException;
                return Task.FromResult(_local);
            }

            public Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken)
            {
                OnlineCalls++;
                if (OnlineException != null) throw OnlineException;
                return Task.FromResult(Online);
            }

            public Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public LicenseInfo CurrentInfo
            {
                get { return LicenseInfo.NotActivated; }
            }

            public event EventHandler InfoChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class FakeCatalog : ICompanyCatalog
        {
            private readonly CatalogSnapshot _snapshot;

            public FakeCatalog(CatalogSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public Exception Exception { get; set; }

            public Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken)
            {
                if (Exception != null) throw Exception;
                return Task.FromResult(_snapshot);
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
