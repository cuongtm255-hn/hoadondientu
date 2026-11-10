# ADR 0011 — Form đăng nhập riêng: WebView chạy ngầm, người dùng nhập mật khẩu và CAPTCHA trên form của ứng dụng

- Ngày: 10/11/2026
- Trạng thái: Chấp nhận (thay một phần ADR 0007 và FR-AUTH-001..003, SRS mục 3.2 và mục 7.x "không chèn script"; chưa thử trên cổng thật)

## Bối cảnh

Cho tới nay người dùng đăng nhập trong chính WebView hiển thị trang cổng Thuế. Hai vấn đề thực tế:

- Người dùng có thể đăng nhập bằng một MST khác với doanh nghiệp đang chọn. App chỉ phát hiện sau khi cổng đã trả token (kiểm claim `sub`), và trang cổng vẫn hiện giao diện "đã đăng nhập" của tài khoản sai.
- Giao diện đăng nhập phụ thuộc hoàn toàn vào trang cổng (hộp thoại, quảng cáo, thông báo), phải chèn script để mở và che trang.

## Quyết định

1. WebView (CefSharp) vẫn dùng nhưng **chạy ngầm** (một khung 2 px trong form), chỉ để giữ cookie/phiên WAF và gọi API từ trong trang.
2. App tự mở trang cổng để lấy cookie, gọi `GET /api/captcha` (api-samples mục 0) và hiển thị ảnh SVG của cổng trên **form đăng nhập riêng** gồm: nhãn MST doanh nghiệp đang chọn (in đậm), ô mật khẩu (ẩn ký tự), ô CAPTCHA, nút Đổi mã khác, nút Đăng nhập.
3. **Tên đăng nhập luôn là MST đang chọn**, người dùng không gõ được tên khác. Kiểm tra `sub` của token sau đăng nhập vẫn giữ làm lớp bảo vệ cuối.
4. Khi bấm Đăng nhập, app chạy `fetch POST /api/security-taxpayer/authenticate` **trong trang cổng** với `{username, password, cvalue, ckey}` (cùng cookie với CAPTCHA). Việc bắt token (`IResponseFilter`), cookie, User-Agent và xác minh MST không đổi.
5. Cổng từ chối (400/401/403/422) hoặc lỗi: lấy CAPTCHA mới (mã chỉ dùng một lần) và báo người dùng nhập lại.
6. **Giữ phương án dự phòng**: nút "Đăng nhập trên trang cổng Thuế..." chuyển form sang chế độ cũ (WebView lớn, người dùng tự thao tác), để cổng đổi API hay thêm bước xác thực (OTP) thì vẫn đăng nhập được.

## Ràng buộc mật khẩu (giữ nguyên các cam kết về bí mật)

- Người dùng vẫn **tự gõ** mật khẩu và CAPTCHA; app không giải CAPTCHA, không lưu mật khẩu, không nhớ mật khẩu giữa các lần.
- Mật khẩu chỉ đi: ô nhập → `LoginSubmitEventArgs` → `SubmitCredentialsAsync` → script `fetch` trong trang cổng. Ô nhập bị xóa ngay khi gửi; presenter không giữ lại; không ghi nhật ký, không vào `company.db`, không vào tham số dòng lệnh hay URL.
- Script dựng bằng `PortalLoginScripts.Quote` (thoát dấu nháy, gạch chéo, ký tự điều khiển, U+2028/9, dấu `<`). Token không được lưu vào biến nào của trang: thân phản hồi chỉ được đọc hết rồi bỏ.
- Giới hạn còn lại: `TextBox.Text` là `string` .NET (không xóa được khỏi bộ nhớ ngay), chấp nhận cho ứng dụng máy tính cá nhân.

## Hệ quả

- Sửa SRS: FR-AUTH-001..003, mục 3.1/3.2 và quy tắc WebView (mục "Không chèn script...") để phản ánh việc app gửi thông tin đăng nhập thay người dùng. Người dùng vẫn là bên nhập mật khẩu và CAPTCHA.
- Mất lớp bảo vệ của trang cổng: nếu cổng đổi API đăng nhập/CAPTCHA hoặc thêm OTP, form riêng sẽ lỗi; đã có phương án dự phòng.
- Ảnh CAPTCHA SVG được dựng bằng `CaptchaSvgRenderer` (chỉ M/L/H/V/C/Q/Z). Ảnh dùng lệnh khác thì form báo "bấm Đổi mã khác", không sập.
- Chưa kiểm trên cổng thật: số lần sai tối đa, thời gian sống của `key`, 400 hay 401 cho sai mật khẩu/CAPTCHA, có bắt buộc header `action`/`end-point`/`request-id` hay không. Bản Debug dùng trình duyệt giả (`DevelopmentPortalLoginBrowser`) có nhãn MÔ PHỎNG.
