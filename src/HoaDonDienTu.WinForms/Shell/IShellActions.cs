using System.Threading.Tasks;
using HoaDonDienTu.Application.Authentication;

namespace HoaDonDienTu.WinForms.Shell
{
    /// <summary>Hành động của shell mà các screen và workflow cần gọi mà không phụ thuộc presenter cụ thể.</summary>
    public interface IShellActions
    {
        /// <summary>Mở đăng nhập cổng Thuế cho doanh nghiệp đang mở; trả về sau khi form đăng nhập đóng.</summary>
        Task LoginAsync(LoginReason reason);
    }
}
