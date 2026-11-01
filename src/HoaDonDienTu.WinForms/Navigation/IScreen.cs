using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;

namespace HoaDonDienTu.WinForms.Navigation
{
    /// <summary>Một screen trong vùng nội dung: control + presenter của nó. Dispose giải phóng cả hai.</summary>
    public interface IScreen : IDisposable
    {
        ScreenId Id { get; }
        Control View { get; }

        /// <summary>False khi screen còn thao tác dở (form chưa lưu, tác vụ chưa xong...) và không được rời đi.</summary>
        Task<bool> CanCloseAsync(CancellationToken cancellationToken);

        /// <summary>Hủy các operation riêng của screen; gọi trước Dispose.</summary>
        void CancelOperations();
    }

    /// <summary>Tạo screen + presenter; handler UI không được tự <c>new</c> screen.</summary>
    public interface IScreenFactory
    {
        IScreen Create(ScreenId id, object parameter);
    }

    /// <summary>Phần của shell mà NavigationService cần.</summary>
    public interface INavigationView
    {
        void ShowScreen(Control screen);
        void SelectNavigation(ScreenId screen);
    }
}
