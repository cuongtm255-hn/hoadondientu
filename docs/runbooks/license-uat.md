# Runbook — Thử bản quyền trên máy sạch (T03-10)

Mục tiêu: kiểm bằng **bản Release thật** chuỗi dùng thử 30 ngày → hết hạn → hộp thoại kích hoạt → dán mã → vào app. Không gọi cổng Thuế; không cần đăng nhập portal.

## Chuẩn bị

- **Máy ảo hoặc máy bỏ đi** (Windows 7 SP1 trở lên, x64 hoặc x86). Lý do: bước 3 chỉnh đồng hồ hệ thống tiến 31 ngày, và cơ chế chống lùi đồng hồ khiến trạng thái "hết hạn" **không hồi lại được** khi chỉnh đồng hồ về. Sau thử phải xóa trạng thái (bước 8) hoặc bỏ máy ảo.
- Gói: `dist\HoaDonDienTu-1.0.0-win-x86.zip` (đối chiếu SHA-256 trong `.sha256`). Giải nén vào một thư mục, ví dụ `C:\HDDT`.
- Quyền `SUPER_ADMIN` trên máy chủ cdsoft-sale-management để cấp mã (bước 6). Đây cũng là lần kiểm đầu tiên khóa công khai nhúng trong app có khớp khóa ký của máy chủ.

## Các bước

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| 1 | Chạy `HoaDonDienTu.exe` lần đầu | Vào thẳng app (không hộp thoại). Màn tạo doanh nghiệp đầu tiên. Cài đặt → Bản quyền ứng dụng: **Đang dùng thử, còn 30 ngày**, có mã máy dạng `XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX` |
| 2 | Bấm **Sao chép** ở mã máy, dán vào Notepad | Đúng mã đang hiển thị |
| 3 | Đóng app. Chỉnh đồng hồ hệ thống tiến **31 ngày** (tắt đồng bộ giờ tự động). Mở lại app | Hiện cửa sổ **Đã hết 30 ngày dùng thử**; chưa thấy màn hình chính |
| 4 | Bấm **Thoát** (hoặc đóng cửa sổ) | App đóng, không vào được. Dữ liệu trong `%LOCALAPPDATA%\HoaDonDienTu\data` còn nguyên |
| 5 | Mở lại, dán mã **sai** (ví dụ `EZACC1|abc|def`) rồi **Kích hoạt** | Báo lỗi đỏ trong cửa sổ, cửa sổ giữ nguyên để nhập lại |
| 6 | Cấp mã đúng bằng máy chủ: `POST /platform/tenants/{id}/machines` với `machineCode` = mã ở bước 2 ([license-keys.md](license-keys.md) mục 0), lấy `activeKey`; dán nguyên một dòng vào cửa sổ, **Kích hoạt** | Báo thành công, vào app, dữ liệu cũ vẫn còn. Bản quyền: **Đã kích hoạt ••••xxxx**, không giới hạn |
| 7 | Đóng và mở lại app (và thử sau khi chỉnh đồng hồ tiến thêm 1 năm) | Vào thẳng, không hỏi lại |
| 8 | Thử mã của máy khác (cấp cho một mã máy giả) trên máy này, sau khi xóa trạng thái | Báo "không đúng với máy này" |

## Dọn sau khi thử

Đưa đồng hồ về đúng, bật lại đồng bộ giờ. Xóa trạng thái để thử lại từ đầu: tệp `%ProgramData%\HoaDonDienTu\Runtime\hddt-license.bin` (có thể là tệp ẩn) và khóa `HKCU\Software\HoaDonDienTu\Runtime`. Xóa **cả hai**; xóa một nơi thì trạng thái cũ vẫn được khôi phục từ nơi còn lại (đó là chủ ý).

## Ghi kết quả

Ghi vào `docs/progress/` ngày thử, hệ điều hành, các bước đạt/không đạt kèm ảnh chụp hộp thoại. Khi tất cả đạt thì đánh dấu T03-10 ✅ trong [DD-03](../detail-design/DD-03-LICENSING.md#8-theo-dõi-tiến-độ-implement) và PROJECT-PLAN.

## Cần xác minh riêng

- Ghi vào `%ProgramData%` có thể bị chặn với người dùng thường trên một số máy; khi đó app dùng bản sao registry. Thử một lần bằng tài khoản không phải admin và xác nhận dùng thử vẫn đếm đúng sau khi mở lại.
- Mã máy phải giữ nguyên sau khi đổi tên máy.
