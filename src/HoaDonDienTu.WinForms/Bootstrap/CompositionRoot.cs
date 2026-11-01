using System;
using System.IO;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Licensing;
#if DEBUG
using HoaDonDienTu.Infrastructure.Development;
#endif
using HoaDonDienTu.Infrastructure.Common;
using HoaDonDienTu.Infrastructure.Logging;
using HoaDonDienTu.WinForms.Shell;

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

        public static CompositionRoot Create()
        {
            var clock = new SystemClock();
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HoaDonDienTu",
                "logs");

            var logger = new FileApplicationLogger(logDirectory, clock);
            return new CompositionRoot
            {
                Clock = clock,
                Logger = logger,
                StartupCoordinator = CreateStartupCoordinator(logger)
            };
        }

        public MainShellForm CreateMainShell()
        {
            return new MainShellForm();
        }

        public void Dispose()
        {
        }

        private static IStartupCoordinator CreateStartupCoordinator(IApplicationLogger logger)
        {
#if DEBUG
            // HDDT_DEV_MODE = Standard | StandardFirstRun | Service | ReadOnly | Recovery:
            // dùng licence/catalog giả cho tới khi có DD-02/DD-03.
            var text = Environment.GetEnvironmentVariable("HDDT_DEV_MODE") ?? "Standard";
            LicenseStatus license = LicenseStatus.None;
            CatalogSnapshot catalog = new CatalogSnapshot(CatalogLoadStatus.Loaded, DevelopmentCompanyCatalog.SampleCompanies(1), false);

            if (string.Equals(text, "StandardFirstRun", StringComparison.OrdinalIgnoreCase))
            {
                catalog = CatalogSnapshot.Empty();
            }
            else if (string.Equals(text, "Service", StringComparison.OrdinalIgnoreCase))
            {
                license = LicenseStatus.Valid;
                catalog = new CatalogSnapshot(CatalogLoadStatus.Loaded, DevelopmentCompanyCatalog.SampleCompanies(3), true);
            }
            else if (string.Equals(text, "ReadOnly", StringComparison.OrdinalIgnoreCase))
            {
                license = LicenseStatus.Expired;
                catalog = new CatalogSnapshot(CatalogLoadStatus.Loaded, DevelopmentCompanyCatalog.SampleCompanies(3), true);
            }
            else if (string.Equals(text, "Recovery", StringComparison.OrdinalIgnoreCase))
            {
                catalog = new CatalogSnapshot(CatalogLoadStatus.Corrupt, null, false);
            }

            return new StartupCoordinator(
                new DevelopmentLicenseService(license),
                new DevelopmentCompanyCatalog(catalog),
                logger);
#else
            throw new NotSupportedException("ILicenseService/ICompanyCatalog production chưa có (DD-03/DD-02).");
#endif
        }
    }
}
