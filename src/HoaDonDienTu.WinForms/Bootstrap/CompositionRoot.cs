using System;
using System.IO;
using System.Linq;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Companies;
using HoaDonDienTu.Infrastructure.Licensing;
using HoaDonDienTu.Infrastructure.Logging;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Companies;
using HoaDonDienTu.WinForms.Download;
using HoaDonDienTu.WinForms.Licensing;
using HoaDonDienTu.WinForms.Login;
using HoaDonDienTu.WinForms.Navigation;
using HoaDonDienTu.WinForms.Settings;
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

        /// <summary>Danh mục doanh nghiệp (companies.json).</summary>
        public ICompanyRegistry Registry { get; private set; }

        /// <summary>Do <c>AppApplicationContext</c> gán sau khi tạo shell; screen lấy lười để tránh phụ thuộc vòng.</summary>
        public HoaDonDienTu.Application.Navigation.INavigationService NavigationService { get; set; }

        /// <summary>Do <c>AppApplicationContext</c> gán; workflow dùng để mở đăng nhập sau khi chọn doanh nghiệp.</summary>
        public IShellActions ShellActions { get; set; }

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

        private static string AppVersion
        {
            get { return typeof(CompositionRoot).Assembly.GetName().Version.ToString(3); }
        }

        public ILicenseService LicenseService { get; private set; }

        private void ComposeEnvironment()
        {
            // Phần riêng của môi trường: Debug chạy trên dữ liệu mẫu với cổng Thuế và licence server giả (HDDT_DEV_MODE =
            // Standard | StandardFirstRun | Service | ReadOnly | Recovery, dữ liệu ở HDDT_DATA_DIR); Release dùng dữ liệu thật của người dùng.
            string dataDirectory;
            var mode = AppMode.Standard;
            ILicenseService developmentLicense = null;
            System.Func<System.Net.Http.HttpMessageHandler> portalHandler = null;
            System.Func<IPortalLoginBrowser> loginBrowser;
            IActiveJobService jobs;
            IDownloadSessionStarter starter;
            ILicenseServer licenseServer;
            ILicenseSignatureVerifier licenseVerifier;
            PortalApiConfig portalConfig;
            try
            {
                portalConfig = LoadPortalApiConfig();
            }
            catch (PortalApiConfigException ex)
            {
                // File portal-api.json cũ (schema cũ) hoặc sai cấu trúc không được làm ứng dụng không mở được: dùng mặc định và ghi log.
                Logger.Warning("portal-api.json không hợp lệ, dùng cấu hình mặc định: " + ex.Errors.FirstOrDefault());
                portalConfig = PortalApiConfigDefaults.Create();
            }

#if DEBUG
            var text = Environment.GetEnvironmentVariable("HDDT_DEV_MODE") ?? "Standard";
            var licenseStatus = LicenseStatus.None;
            if (string.Equals(text, "Service", StringComparison.OrdinalIgnoreCase))
            {
                licenseStatus = LicenseStatus.Valid;
                mode = AppMode.Service;
            }
            else if (string.Equals(text, "ReadOnly", StringComparison.OrdinalIgnoreCase))
            {
                licenseStatus = LicenseStatus.Expired;
                mode = AppMode.ReadOnlyService;
            }

            dataDirectory = Environment.GetEnvironmentVariable("HDDT_DATA_DIR");
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                dataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoaDonDienTu", "dev-data", text);
            }

            DevelopmentDataSeeder.Seed(dataDirectory, text);
            developmentLicense = new DevelopmentLicenseService(licenseStatus);
            starter = new PendingDownloadSessionStarter();
            var mockPortal = new MockPortalHandler(portalConfig);
            portalHandler = () => mockPortal;
            jobs = new DevelopmentActiveJobService(Environment.GetEnvironmentVariable("HDDT_DEV_JOB") == "1");
            loginBrowser = () => new DevelopmentPortalLoginBrowser(
                () => ContextManager.HasCurrent ? ContextManager.Current.TaxCode.Value : string.Empty);
            var developmentServer = new DevelopmentLicenseServer(
                () => string.Equals(Environment.GetEnvironmentVariable("HDDT_DEV_LICENSE_SERVER"), "down", StringComparison.OrdinalIgnoreCase),
                Path.Combine(dataDirectory, "dev-license-key.xml"));
            licenseServer = developmentServer;
            licenseVerifier = new RsaLicenseSignatureVerifier(developmentServer.PublicKeyXml);
#else
            dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoaDonDienTu", "data");
            jobs = new IdleActiveJobService();
            starter = new UnavailableDownloadSessionStarter();
            loginBrowser = () => new UnavailablePortalLoginBrowser();
            var licenseSettings = LicenseApiSettingsLoader.TryLoad(LocateConfigFile("license-api.json"));
            if (licenseSettings == null)
            {
                Logger.Warning("Chưa có license-api.json hợp lệ: kích hoạt licence chưa dùng được, ứng dụng chạy chế độ chuẩn");
                licenseServer = new UnconfiguredLicenseServer();
                licenseVerifier = new RsaLicenseSignatureVerifier(string.Empty);
            }
            else
            {
                licenseServer = new LicenseApiClient(licenseSettings.ToConfig(), null);
                licenseVerifier = new RsaLicenseSignatureVerifier(licenseSettings.PublicKeyXml);
            }
#endif

            var registry = new JsonCompanyCatalog(Path.Combine(dataDirectory, "companies.json"));
            var storage = new FileSystemCompanyStorage();
            Registry = registry;

            // Licence thật (store DPAPI, chữ ký RSA, mã máy). Khởi động dùng nó khi đã có license.dat; chưa có thì Debug theo HDDT_DEV_MODE.
            var machineIdentity = new MachineIdentityProvider();
            var licenseService = new LicenseService(
                new FileLicenseStore(Path.Combine(dataDirectory, "license.dat")), licenseServer, machineIdentity,
                licenseVerifier, Clock, Logger, AppVersion);
            LicenseService = licenseService;

            var startupLicense = developmentLicense == null || File.Exists(Path.Combine(dataDirectory, "license.dat"))
                ? (ILicenseService)licenseService
                : developmentLicense;
            StartupCoordinator = new StartupCoordinator(startupLicense, registry, Logger);

            PortalApiConfig = portalConfig;
            var contextManager = new CompanyContextManager(new CompanyStorageOpener(registry, storage, Clock, Logger));
            var entitlement = new EntitlementService(mode, mode == AppMode.ReadOnlyService);
            Entitlement = entitlement;
            ContextManager = contextManager;
            ShutdownCoordinator = new ShutdownCoordinator(contextManager, jobs, SessionStore, new ShutdownPrompt(), Logger);
            ApiClientProvider = new PortalApiClientProvider(
                SessionStore, contextManager, new PortalApiClientFactory(portalConfig, portalHandler));

            var switchService = new CompanySwitchService(registry, contextManager, jobs, SessionStore, entitlement, Logger);
            var management = new CompanyManagementService(registry, storage, contextManager, entitlement, Logger);
            var workflow = new CompanyWorkflow(
                switchService, contextManager, SessionStore, entitlement, Confirmation, ErrorPresenter,
                () => NavigationService, () => ShellActions);
            var folderPicker = new FolderPicker();
            var folderLauncher = new ExplorerFolderLauncher();
            System.Func<string, string> suggestFolder = taxCode => Path.Combine(dataDirectory, "Companies", taxCode);

            var splitter = new PeriodSplitter();
            var startPolicy = new DownloadStartPolicy(contextManager, SessionStore, entitlement);
            var storageProbe = new FileSystemStorageProbe();
            var capabilities = new PortalApiConfigCapabilities(portalConfig);
            ScreenFactory = Navigation.ScreenFactory.CreateDefault(
                new ScreenDescriptor(
                    HoaDonDienTu.Application.Navigation.ScreenId.Download, "Tải hóa đơn", "SCR-03",
                    parameter => new DownloadConfigScreen(
                        new DownloadConfigControl(),
                        view => new DownloadConfigPresenter(
                            view, splitter, startPolicy, storageProbe, capabilities, SessionStore, contextManager,
                            entitlement, starter, Confirmation, OperationRunner, ErrorPresenter, NavigationService, Clock))),
                new ScreenDescriptor(
                    HoaDonDienTu.Application.Navigation.ScreenId.Settings, "Cài đặt", "SCR-09",
                    parameter => new SettingsScreen(
                        new SettingsControl(),
                        view => new SettingsPresenter(
                            view, licenseService, entitlement, contextManager, () => NavigationService, OperationRunner, ErrorPresenter,
                            AppVersion + " · Schema v1"))),
                new ScreenDescriptor(
                    HoaDonDienTu.Application.Navigation.ScreenId.License, "Bản quyền", "SCR-12",
                    parameter => new LicenseScreen(
                        new LicenseControl(),
                        view => new LicensePresenter(
                            view, licenseService, entitlement, entitlement, registry, machineIdentity, () => NavigationService,
                            Confirmation, OperationRunner, ErrorPresenter, Environment.MachineName, AppVersion + " · Schema v1"))),
                new ScreenDescriptor(
                    HoaDonDienTu.Application.Navigation.ScreenId.CompanyList, "Doanh nghiệp", "SCR-10",
                    parameter => new CompanyListScreen(
                        new CompanyListControl(),
                        view => new CompanyListPresenter(
                            view, registry, contextManager, entitlement, workflow, management, () => NavigationService,
                            Confirmation, folderLauncher, OperationRunner, ErrorPresenter))),
                new ScreenDescriptor(
                    HoaDonDienTu.Application.Navigation.ScreenId.CompanyEdit, "Thông tin doanh nghiệp", "SCR-11",
                    parameter => new CompanyEditScreen(
                        new CompanyEditControl(),
                        view => new CompanyEditPresenter(
                            view, parameter as CompanyEditRequest ?? CompanyEditRequest.Add(), registry, management, workflow,
                            () => NavigationService, folderPicker, suggestFolder, OperationRunner, ErrorPresenter))));

            PortalLoginCoordinator = new PortalLoginCoordinator(
                contextManager,
                entitlement,
                SessionStore,
                new PortalAuthenticationService(Clock),
                loginBrowser,
                () => new PortalLoginForm(),
                Logger);
        }

        /// <summary>Tìm file cấu hình: bản của người dùng trong %LOCALAPPDATA%\HoaDonDienTu ưu tiên, rồi bản cạnh file chạy.</summary>
        private static string LocateConfigFile(string name)
        {
            var userFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoaDonDienTu", name);
            return File.Exists(userFile) ? userFile : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        }
    }
}
