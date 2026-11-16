using System;

namespace HoaDonDienTu.Application.Groups
{
    /// <summary>Khung (shell) của một nhóm chức năng, độc lập với WinForms để test được.</summary>
    public interface IGroupShell : IDisposable
    {
        void Show();

        /// <summary>Ẩn shell nhưng giữ phiên và WebView (FR-DVC-NAV-003).</summary>
        void Hide();

        /// <summary>False nếu đang có tác vụ chạy; <paramref name="reason"/> mô tả tác vụ để hỏi xác nhận.</summary>
        bool CanLeave(out string reason);

        /// <summary>Dừng các tác vụ đang chạy khi người dùng đã xác nhận rời nhóm.</summary>
        void StopRunningTasks();
    }

    /// <summary>Tạo shell của nhóm; chỉ được gọi khi nhóm lần đầu được chọn (lazy, FR-DVC-NAV-002).</summary>
    public interface IGroupShellFactory
    {
        IGroupShell Create(AppGroup group);
    }
}
