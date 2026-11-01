using System;
using System.Windows.Forms;

namespace HoaDonDienTu.WinForms.Login
{
    public enum PortalLoginState
    {
        Created,
        InitializingBrowser,
        ClearingPreviousSession,
        LoadingPortalLogin,
        WaitingForUser,
        VerifyingPortalSession,
        Succeeded,
        BrowserUnavailable,
        PortalUnavailable,
        InvalidSession,
        TaxCodeMismatch,
        Cancelled
    }

    /// <summary>View state không bao giờ chứa token/cookie.</summary>
    public sealed class PortalLoginViewState
    {
        public PortalLoginState State { get; set; }
        public string StepText { get; set; }
        public string Message { get; set; }
        public bool IsBusy { get; set; }
        public bool IsError { get; set; }
        public bool CanRetry { get; set; }
    }

    public interface IPortalLoginView
    {
        event EventHandler ViewShown;
        event EventHandler RetryRequested;
        event EventHandler ClearSessionRequested;
        event EventHandler CancelRequested;

        void ShowRequest(string companyName, string expectedTaxCode);
        void ShowState(PortalLoginViewState state);
        void HostBrowser(Control browserView);
        void CloseView();

        /// <summary>Hiển thị dạng modal và chỉ trả về khi view đã đóng.</summary>
        void ShowModal(IWin32Window owner);
    }
}
