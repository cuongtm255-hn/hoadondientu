using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    public enum CompanySwitchBlocker
    {
        None,
        DownloadRunning,
        StorageUnavailable,
        DatabaseLocked,
        DatabaseInvalid,
        LicenceReadOnly,

        /// <summary>Một lần chuyển MST khác đang chạy.</summary>
        SwitchInProgress
    }

    public enum ActiveJobResolution
    {
        NotRequired,
        WaitForCompletion,
        PauseAndSwitch,
        CancelAndSwitch
    }

    public enum CompanySwitchOutcome
    {
        Switched,
        Unchanged,
        Blocked,
        RestoredPrevious,
        RecoveryRequired
    }

    public sealed class CompanySwitchAssessment
    {
        public CompanySwitchAssessment(CompanySwitchBlocker blocker)
        {
            Blocker = blocker;
        }

        public CompanySwitchBlocker Blocker { get; private set; }

        public bool CanSwitch
        {
            get { return Blocker == CompanySwitchBlocker.None; }
        }

        /// <summary>Có tác vụ tải đang chạy: người dùng phải chọn chờ, tạm dừng hoặc hủy.</summary>
        public bool RequiresJobResolution
        {
            get { return Blocker == CompanySwitchBlocker.DownloadRunning; }
        }
    }

    public sealed class CompanySwitchResult
    {
        public CompanySwitchResult(CompanySwitchOutcome outcome, CompanySwitchBlocker blocker, CompanyContext context, string message)
        {
            Outcome = outcome;
            Blocker = blocker;
            Context = context;
            Message = message;
        }

        public CompanySwitchOutcome Outcome { get; private set; }
        public CompanySwitchBlocker Blocker { get; private set; }
        public CompanyContext Context { get; private set; }
        public string Message { get; private set; }

        public bool Succeeded
        {
            get { return Outcome == CompanySwitchOutcome.Switched || Outcome == CompanySwitchOutcome.Unchanged; }
        }
    }

    public interface ICompanySwitchService
    {
        Task<CompanySwitchAssessment> AssessAsync(CompanyId targetCompanyId, CancellationToken cancellationToken);

        Task<CompanySwitchResult> SwitchAsync(
            CompanyId targetCompanyId,
            ActiveJobResolution jobResolution,
            CancellationToken cancellationToken);
    }

    /// <summary>Tác vụ tải của MST đang mở; implementation ở DD-05.</summary>
    public interface IActiveJobService
    {
        Task<bool> HasRunningDownloadAsync(CompanyId companyId, CancellationToken cancellationToken);
        Task WaitForCompletionAsync(CompanyId companyId, CancellationToken cancellationToken);

        /// <summary>Tạm dừng và ghi checkpoint.</summary>
        Task PauseAsync(CompanyId companyId, CancellationToken cancellationToken);

        Task CancelAsync(CompanyId companyId, CancellationToken cancellationToken);
    }
}
