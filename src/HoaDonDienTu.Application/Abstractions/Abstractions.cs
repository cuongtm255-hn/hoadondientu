using System;

namespace HoaDonDienTu.Application.Abstractions
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    /// <summary>Không được ghi token, mật khẩu hoặc dữ liệu hóa đơn vào log (NFR-SEC-002).</summary>
    public interface IApplicationLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception exception);
    }

    public enum ErrorCategory
    {
        Validation,
        Configuration,
        Licence,
        Storage,
        Database,
        Browser,
        PortalAuthentication,
        PortalApi,
        Network,
        Cancelled,
        Unexpected
    }

    public sealed class UserFacingError
    {
        public UserFacingError(
            string code,
            ErrorCategory category,
            string title,
            string message,
            string suggestedAction,
            string correlationId,
            bool canRetry)
        {
            Code = code;
            Category = category;
            Title = title;
            Message = message;
            SuggestedAction = suggestedAction;
            CorrelationId = correlationId;
            CanRetry = canRetry;
        }

        public string Code { get; private set; }
        public ErrorCategory Category { get; private set; }
        public string Title { get; private set; }
        public string Message { get; private set; }
        public string SuggestedAction { get; private set; }
        public string CorrelationId { get; private set; }
        public bool CanRetry { get; private set; }
    }
}
