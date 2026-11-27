# DD-DVC-04 — Hồ sơ lưu tạm (FT-DVC-04)

**Trạng thái:** bản đọc chung đã làm (27/11/2026), **chưa kiểm chứng với dữ liệu thật** vì tài khoản thử không có hồ sơ lưu tạm. Khảo sát: [khao-sat-tthc.md, bổ sung 27/11/2026](../api-samples/khao-sat-tthc.md).

## 1. Thiết kế

Hồ sơ lưu tạm là **nguồn thứ ba** (`HoSoSource.LuuTam`) của màn "Tra cứu hồ sơ đã nộp" ([DD-DVC-03](DD-DVC-03-SUBMITTED-FILES.md)), chọn ở ô "Nguồn" (mục "Hồ sơ lưu tạm (chưa nộp)"). Dùng lại dịch vụ, phiên tìm, bảng kết quả và xuất Excel của hồ sơ đã nộp; chỉ đọc, không có nút nộp/sửa/xóa.

| Khác với hai nguồn kia | Cách xử lý |
|---|---|
| Không có CAPTCHA | `HoSoService`/`HoSoSearchSession` bỏ qua CAPTCHA cho `LuuTam` (không tải ảnh, không bắt nhập mã); form ẩn ô và ảnh mã |
| Ô lọc | Mã tờ khai (`maToKhai`), mã TTHC (`maTTHC`, chỉ nhận dạng mã), ngày từ/đến để trống được (tích cả hai ô ngày mới lọc: `HoSoQuery.IgnoreDates`) |
| Yêu cầu | `POST /tthc/tchs/draft`, multipart như htmx, toàn bộ giá trị `form_search` (gồm trường ẩn và `_csrf`) ghi đè bốn ô lọc, header CSRF; chạy trong trang `/tthc/tchs/draft` của WebView ngầm (`PortalHoSoSource.BuildDraftSearchScript`) |
| Đọc kết quả | Dùng chung bộ đọc bảng bằng DOM của trình duyệt, ở chế độ **lỏng** (`PortalHoSoSource.PageFromResult(result, lenient)`, `HoSoDomTable.ToPage(..., lenient)`): thiếu dòng tổng thì coi là **một trang** với số dòng đã đọc; mảnh ngắn (< 1500 ký tự) không bảng là kết quả rỗng và hiện chữ của cổng ("Không có dữ liệu"); mảnh dài lạ vẫn báo "cổng đổi giao diện" |

## 2. Chưa kiểm chứng (cần tài khoản có hồ sơ lưu tạm)

- Tên và số cột của bảng, cột "Thao tác" có bị bỏ đúng không.
- Có phân trang không và đổi trang thế nào. Hiện chỉ hiện **trang đầu**; nếu cổng có dòng tổng dạng "Trang x/y ... bản ghi: n" thì bộ đọc chung sẽ nhận và bật đổi trang, nhưng yêu cầu đổi trang của nguồn này chưa viết (không gửi tham số `page`).
- Thông báo của cổng khi ngày sai định dạng.

## 3. Bảng tiến độ

| Mã | Việc | Trạng thái |
|---|---|---|
| TD04-01 | Nguồn `LuuTam` trong Domain/Application (không CAPTCHA, `IgnoreDates`) | ✅ |
| TD04-02 | `PortalHoSoSource` đọc `/tthc/tchs/draft` (lỏng) | ✅ (test với runner giả) |
| TD04-03 | Màn hình: mục nguồn, ẩn CAPTCHA, ô ngày có tích chọn, ghi chú "chưa kiểm chứng" | ✅ |
| TD04-04 | Mô phỏng Debug (`MockHoSoSource.SearchDraft`, 3 dòng "(mẫu)") và xuất Excel | ✅ |
| TD04-05 | Kiểm chứng cột + phân trang với dữ liệu thật; viết đổi trang nếu có | ⬜ cần tài khoản có hồ sơ lưu tạm |
