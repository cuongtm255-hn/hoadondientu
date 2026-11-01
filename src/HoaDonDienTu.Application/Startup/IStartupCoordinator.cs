using System;
using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Startup
{
    public enum StartupStage
    {
        Created,
        LoadingLocalSettings,
        ValidatingLocalLicense,
        LoadingCompanyCatalog,
        ResolvingMode,
        ValidatingLicenseOnline,
        Ready,
        RecoveryRequired
    }

    public sealed class StartupProgress
    {
        public StartupProgress(StartupStage stage, string message)
        {
            Stage = stage;
            Message = message;
        }

        public StartupStage Stage { get; private set; }
        public string Message { get; private set; }
    }

    public interface IStartupCoordinator
    {
        Task<StartupResult> InitializeAsync(
            IProgress<StartupProgress> progress,
            CancellationToken cancellationToken);
    }
}
