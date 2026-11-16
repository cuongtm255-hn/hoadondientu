using System;

namespace HoaDonDienTu.Application.PublicService
{
    public enum DvcSessionState
    {
        NotLoggedIn,
        LoggedIn,
        Expired,
        TakenByOtherTab
    }

    /// <summary>Trạng thái phiên cổng Dịch vụ công (chỉ trong bộ nhớ). Không dùng chung với phiên cổng Hóa đơn.</summary>
    public sealed class DvcSession
    {
        public DvcSessionState State { get; private set; }

        public string UserName { get; private set; }

        public string OrganizationName { get; private set; }

        public bool IsLoggedIn
        {
            get { return State == DvcSessionState.LoggedIn; }
        }

        public event EventHandler StateChanged;

        public void SignedIn(string userName, string organizationName)
        {
            UserName = userName;
            OrganizationName = organizationName;
            Set(DvcSessionState.LoggedIn);
        }

        public void Reset()
        {
            UserName = null;
            OrganizationName = null;
            Set(DvcSessionState.NotLoggedIn);
        }

        /// <summary>Trang trả về trang đăng nhập: hết phiên. Không tự đăng nhập lại (FR-DVC-AUTH-007).</summary>
        public void MarkExpired()
        {
            Set(DvcSessionState.Expired);
        }

        /// <summary>Cổng báo tài khoản đang dùng ở tab khác.</summary>
        public void MarkTakenByOtherTab()
        {
            Set(DvcSessionState.TakenByOtherTab);
        }

        private void Set(DvcSessionState state)
        {
            if (State == state) return;
            State = state;
            var handler = StateChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
