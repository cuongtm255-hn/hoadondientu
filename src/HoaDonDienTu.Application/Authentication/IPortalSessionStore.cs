using System;

namespace HoaDonDienTu.Application.Authentication
{
    /// <summary>Chỉ một phiên cổng Thuế trong bộ nhớ; không serialize, không ghi đĩa (DD-01 mục 11.1).</summary>
    public interface IPortalSessionStore
    {
        bool HasActiveSession { get; }

        /// <summary>Snapshot không chứa token; null khi chưa có phiên.</summary>
        PortalSessionSnapshot GetSnapshot();

        /// <summary>Chỉ dành cho lớp API client. UI không được dùng.</summary>
        PortalSession GetSessionForApi();

        void Set(PortalSession session);

        /// <summary>Gọi khi đổi MST, logout, token hết hạn hoặc đóng app.</summary>
        void Clear();

        /// <summary>Phát khi Set hoặc Clear.</summary>
        event EventHandler SessionChanged;
    }
}
