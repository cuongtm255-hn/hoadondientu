using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Authentication
{
    public enum PortalLoginOutcome
    {
        Success,
        Cancelled,
        InvalidSession,
        TaxCodeMismatch,
        PortalUnavailable,
        BrowserUnavailable,

        /// <summary>Không đủ điều kiện mở đăng nhập: chưa có doanh nghiệp, MST không khớp hoặc chế độ chỉ đọc.</summary>
        NotAllowed,

        /// <summary>Đã có cửa sổ đăng nhập khác đang hoạt động.</summary>
        AlreadyInProgress
    }

    public sealed class PortalLoginResult
    {
        private PortalLoginResult(PortalLoginOutcome outcome, PortalSession session, string message)
        {
            Outcome = outcome;
            Session = session;
            Message = message;
        }

        public PortalLoginOutcome Outcome { get; private set; }

        /// <summary>Chỉ có giá trị khi Success.</summary>
        public PortalSession Session { get; private set; }

        public string Message { get; private set; }

        public bool Succeeded
        {
            get { return Outcome == PortalLoginOutcome.Success; }
        }

        public static PortalLoginResult Success(PortalSession session)
        {
            return new PortalLoginResult(PortalLoginOutcome.Success, session, null);
        }

        public static PortalLoginResult Failure(PortalLoginOutcome outcome, string message)
        {
            return new PortalLoginResult(outcome, null, message);
        }
    }

    /// <summary>
    /// Xác minh ứng viên phiên bằng API cổng Thuế và đối chiếu MST với kho đang mở.
    /// Điều hướng khỏi trang login không đủ để coi là thành công.
    /// </summary>
    public interface IPortalAuthenticationService
    {
        Task<PortalLoginResult> VerifyAsync(
            CompanyContext company,
            PortalSessionMaterial candidate,
            CancellationToken cancellationToken);
    }
}
