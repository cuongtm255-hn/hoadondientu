using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>Thông báo tiếng Việt cho từng loại lỗi đăng nhập DVC (FR-DVC-AUTH-005). Không chứa dữ liệu người dùng nhập.</summary>
    public static class DvcLoginMessages
    {
        public static string For(DvcLoginFailure failure)
        {
            switch (failure)
            {
                case DvcLoginFailure.MissingInput:
                    return "Hãy nhập đủ tên đăng nhập, mật khẩu và mã xác nhận.";
                case DvcLoginFailure.WrongPassword:
                    return "Tên đăng nhập hoặc mật khẩu không đúng.";
                case DvcLoginFailure.WrongCaptcha:
                    return "Mã xác nhận không đúng. Hãy nhập mã mới.";
                case DvcLoginFailure.Locked:
                    return "Tài khoản đang bị khóa. Hãy liên hệ cơ quan thuế hoặc thử lại sau.";
                case DvcLoginFailure.AnotherTab:
                    return "Tài khoản đang được dùng ở một tab hoặc cửa sổ khác. Hãy đóng nơi đó rồi đăng nhập lại.";
                case DvcLoginFailure.Network:
                    return "Không kết nối được cổng Dịch vụ công. Kiểm tra mạng rồi thử lại, hoặc đăng nhập trên trang cổng.";
                default:
                    return "Đăng nhập không thành công. Có thể dùng nút đăng nhập trên trang cổng.";
            }
        }
    }
}
