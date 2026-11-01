using System;
using System.IO;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Startup;
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

            return new CompositionRoot
            {
                Clock = clock,
                Logger = new FileApplicationLogger(logDirectory, clock),
                StartupCoordinator = CreateStartupCoordinator()
            };
        }

        public MainShellForm CreateMainShell()
        {
            return new MainShellForm();
        }

        public void Dispose()
        {
        }

        private static IStartupCoordinator CreateStartupCoordinator()
        {
#if DEBUG
            // HDDT_DEV_MODE = Standard | Service | ReadOnly để thử từng chế độ khi chưa có StartupCoordinator thật (T01-06).
            var mode = HoaDonDienTu.Domain.Common.AppMode.Standard;
            var text = Environment.GetEnvironmentVariable("HDDT_DEV_MODE");
            if (string.Equals(text, "Service", StringComparison.OrdinalIgnoreCase)) mode = HoaDonDienTu.Domain.Common.AppMode.Service;
            else if (string.Equals(text, "ReadOnly", StringComparison.OrdinalIgnoreCase)) mode = HoaDonDienTu.Domain.Common.AppMode.ReadOnlyService;
            return new HoaDonDienTu.Infrastructure.Development.DevelopmentStartupCoordinator(mode);
#else
            throw new NotSupportedException("StartupCoordinator production chưa được implement (T01-06).");
#endif
        }
    }
}
