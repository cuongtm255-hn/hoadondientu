namespace HoaDonDienTu.Application.Groups
{
    /// <summary>Nhớ nhóm chức năng dùng gần nhất (cài đặt người dùng; không chứa bí mật).</summary>
    public interface IGroupPreferenceStore
    {
        /// <summary>Nhóm dùng gần nhất, hoặc null nếu chưa có hay giá trị không hợp lệ.</summary>
        AppGroup? GetLastGroup();

        void SetLastGroup(AppGroup group);
    }
}
