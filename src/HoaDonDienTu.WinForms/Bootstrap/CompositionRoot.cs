using System;
using System.IO;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Logging;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Login;
using HoaDonDienTu.WinForms.Navigation;
using HoaDonDienTu.WinForms.Shell;
#if DEBUG
using HoaDonDienTu.Infrastructure.Development;
using HoaDonDienTu.Infrastructure.Development.Mock;
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

        /// <summary>Standard: tự mở form đăng nhập sau khi khởi động (DD-01 mục 7.4). Test UI tắt để không bị chặn bởi hộp thoại modal.</summary>
        public bool AutoLoginOnStartup { get; set; } = true;

        public IUserErrorMapper ErrorMapper { get; private set; }
        public IUiOperationRunner OperationRunner { get; private set; }
        public IUserErrorPresenter ErrorPresenter { get; private set; }
        public IConfirmationService Confirmation { get; private set; }
        public ShutdownCoordinator ShutdownCoordinator { get; private set; }
        public IEntitlementService Entitlement { get; private set; }

        /// <summary>Cấu hình API cổng Thuế (bản nháp chưa đối chiếu thật cho tới khi <c>Verified</c> = true).</summary>
        public PortalApiConfig PortalApiConfig { get; private set; }

        /// <summary>Client API của phiên hiện tại cho DD-05; dùng cổng giả ở Debug.</summary>
        public PortalApiClientProvider ApiClientProvider { get; private set; }
        public IScreenFactory ScreenFactory { get; private set; }

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
            root.ErrorMapper = new UserErrorMapper();
            root.OperationRunner = new UiOperationRunner(root.ErrorMapper, logger);
            root.ErrorPresenter = new UserErrorPresenter();
            root.Confirmation = new ConfirmationService();
            root.ScreenFactory = Navigation.ScreenFactory.CreateDefault();
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
            if (ApiClientProvider != null) ApiClientProvider.Dispose();
            var runner = OperationRunner as IDisposable;
            if (runner != null) runner.Dispose();
            if (SessionStore != null) SessionStore.Clear();
            if (ContextManager != null) ContextManager.CloseCurrent();
        }

        /// <summary>Đọc portal-api.json: bản của người dùng trong %LOCALAPPDATA% ưu tiên, rồi bản cạnh file chạy, rồi bản nháp mặc định.</summary>
        private static PortalApiConfig LoadPortalApiConfig()
        {
            var userFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoaDonDienTu", "portal-api.json");
            var appFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portal-api.json");
            return PortalApiConfigLoader.LoadOrDefault(File.Exists(userFile) ? userFile : appFile);
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

            var portalConfig = LoadPortalApiConfig();
            PortalApiConfig = portalConfig;
            var mockPortal = new MockPortalHandler(portalConfig);
            var contextManager = new CompanyContextManager(new DevelopmentCompanyStorageOpener());
            var entitlement = new DevelopmentEntitlementService(mode);
            Entitlement = entitlement;
            ContextManager = contextManager;
            var jobsRunning = Environment.GetEnvironmentVariable("HDDT_DEV_JOB") == "1";
            ShutdownCoordinator = new ShutdownCoordinator(
                contextManager,
                new DevelopmentActiveJobService(jobsRunning),
                SessionStore,
                new ShutdownPrompt(),
                Logger);
            ApiClientProvider = new PortalApiClientProvider(
                SessionStore, contextManager, new PortalApiClientFactory(portalConfig, () => mockPortal));
            PortalLoginCoordinator = new PortalLoginCoordinator(
                contextManager,
                entitlement,
                SessionStore,
                new PortalAuthenticationService(portalConfig, Clock, () => mockPortal),
                () => new DevelopmentPortalLoginBrowser(
                    () => contextManager.HasCurrent ? contextManager.Current.TaxCode.Value : string.Empty),
                () => new PortalLoginForm(),
                Logger);
#else
            throw new NotSupportedException("Licence/catalog/kho/xác thực production chưa có (DD-02, DD-03, T01-16).");
#endif
        }
    }
}
