using System;
using System.IO;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Logging;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.WinForms.Login;
using HoaDonDienTu.WinForms.Shell;
#if DEBUG
using HoaDonDienTu.Infrastructure.Development;
#endif

namespace HoaDonDienTu.WinForms.Bootstrap
{
    /// <summary>Nơi duy nhất ghép implementation production/development.</summary>
    public sealed class CompositionRoot : IDisposable
    {
        private CompositionRoot()
        {
        }

        public IApplicationLogger Logger { get; private set; }
        public IClock Clock { get; private set; }
        public IStartupCoordinator StartupCoordinator { get; private set; }
        public ICompanyContextManager ContextManager { get; private set; }
        public IPortalSessionStore SessionStore { get; private set; }
        public IPortalLoginCoordinator PortalLoginCoordinator { get; private set; }

        public static CompositionRoot Create()
        {
            var clock = new SystemClock();
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HoaDonDienTu",
                "logs");
            var logger = new FileApplicationLogger(logDirectory, clock);

            var root = new CompositionRoot
            {
                Clock = clock,
                Logger = logger,
                SessionStore = new InMemoryPortalSessionStore()
            };
            root.ComposeEnvironment();
            return root;
        }

        public MainShellForm CreateMainShell()
        {
            return new MainShellForm();
        }

        public void Dispose()
        {
            // Phiên chỉ giữ trong bộ nhớ và phải bị xóa khi đóng app.
            if (SessionStore != null) SessionStore.Clear();
            if (ContextManager != null) ContextManager.CloseCurrent();
        }

        private void ComposeEnvironment()
        {
#if DEBUG
            // HDDT_DEV_MODE = Standard | StandardFirstRun | Service | ReadOnly | Recovery:
            // dùng licence/catalog/kho/xác thực giả cho tới khi có DD-02, DD-03 và ADR G-01/G-02.
            var text = Environment.GetEnvironmentVariable("HDDT_DEV_MODE") ?? "Standard";
            var license = LicenseStatus.None;
            var mode = AppMode.Standard;
            var catalog = new CatalogSnapshot(CatalogLoadStatus.Loaded, DevelopmentCompanyCatalog.SampleCompanies(1), false);

            if (string.Equals(text, "StandardFirstRun", StringComparison.OrdinalIgnoreCase))
            {
                catalog = CatalogSnapshot.Empty();
            }
            else if (string.Equals(text, "Service", StringComparison.OrdinalIgnoreCase))
            {
                license = LicenseStatus.Valid;
                mode = AppMode.Service;
                catalog = new CatalogSnapshot(CatalogLoadStatus.Loaded, DevelopmentCompanyCatalog.SampleCompanies(3), true);
            }
            else if (string.Equals(text, "ReadOnly", StringComparison.OrdinalIgnoreCase))
            {
                license = LicenseStatus.Expired;
                mode = AppMode.ReadOnlyService;
                catalog = new CatalogSnapshot(CatalogLoadStatus.Loaded, DevelopmentCompanyCatalog.SampleCompanies(3), true);
            }
            else if (string.Equals(text, "Recovery", StringComparison.OrdinalIgnoreCase))
            {
                catalog = new CatalogSnapshot(CatalogLoadStatus.Corrupt, null, false);
            }

            StartupCoordinator = new StartupCoordinator(
                new DevelopmentLicenseService(license),
                new DevelopmentCompanyCatalog(catalog),
                Logger);

            var contextManager = new CompanyContextManager(new DevelopmentCompanyStorageOpener());
            ContextManager = contextManager;
            PortalLoginCoordinator = new PortalLoginCoordinator(
                contextManager,
                new DevelopmentEntitlementService(mode),
                SessionStore,
                new FakePortalAuthenticationService(null, Clock),
                () => new DevelopmentPortalLoginBrowser(
                    () => contextManager.HasCurrent ? contextManager.Current.TaxCode.Value : string.Empty),
                Logger);
#else
            throw new NotSupportedException("Licence/catalog/kho/xác thực production chưa có (DD-02, DD-03, T01-16).");
#endif
        }
    }
}
