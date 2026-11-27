# DD-DVC-05 — Phản ánh quyết toán gửi CQT (FT-DVC-05)

**Trạng thái:** bản đọc chung đã làm (27/11/2026), **chưa kiểm chứng với dữ liệu thật** vì tài khoản thử không có phản ánh nào. Khảo sát: [khao-sat-tthc.md, bổ sung 27/11/2026 (phản ánh)](../api-samples/khao-sat-tthc.md).

## 1. Thiết kế

Nguồn thứ tư (`HoSoSource.PhanAnh`) của màn "Tra cứu hồ sơ đã nộp" ([DD-DVC-03](DD-DVC-03-SUBMITTED-FILES.md)), làm giống hồ sơ lưu tạm ([DD-DVC-04](DD-DVC-04-DRAFTS.md)). Ô Nguồn có hai mục: "Phản ánh quyết toán đã gửi trên Dịch vụ công" (`switchData=0`) và "... trên Thuế điện tử" (`switchData=1`, cờ `HoSoQuery.FeedbackOnETax`). Chỉ đọc.

| Việc | Cách xử lý |
|---|---|
| CAPTCHA | Không có (`HoSoSources.RequiresCaptcha` = false); đổi trang không cần mã |
| Ô lọc | Năm quyết toán (`namQuyetToan`, ô chữ, mặc định năm trước), MST tổ chức (`mstToChuc`), ngày từ/đến để trống được (tích cả hai ô mới lọc) |
| Yêu cầu | `POST /tthc/tchs/pa/tracuu`, urlencoded, giá trị `form_search` ghi đè các ô lọc, kèm `page`, `size=10` và header CSRF; chạy trong trang `/tthc/tchs/pa` của WebView ngầm (`PortalHoSoSource.BuildFeedbackSearchScript`) |
| Đọc kết quả | Chung bộ đọc bảng bằng DOM, chế độ lỏng (`HoSoSources.IsUnverified`): dòng tổng có thì dùng để phân trang, thiếu thì coi một trang |
| Excel | Dùng chung; dòng thông tin ghi nguồn "Phản ánh quyết toán gửi CQT", kênh gửi và năm quyết toán |

## 2. Chưa kiểm chứng (cần tài khoản có phản ánh)

- Tên và số cột của bảng; cột "Thao tác" có bị bỏ đúng không.
- Đổi trang nhiều trang (yêu cầu có `page`/`size` như hồ sơ đã nộp nên có thể chạy, chưa thử).
- Danh sách năm quyết toán của cổng chỉ 2021–2025: app cho gõ năm tự do, cổng có thể từ chối năm khác.

## 3. Bảng tiến độ

| Mã | Việc | Trạng thái |
|---|---|---|
| TD05-01 | Nguồn `PhanAnh` (Domain/Application, `HoSoSources`) | ✅ |
| TD05-02 | `PortalHoSoSource` đọc `/tthc/tchs/pa/tracuu` | ✅ (test với runner giả) |
| TD05-03 | Màn hình: hai mục nguồn, ô năm quyết toán, MST tổ chức, ẩn CAPTCHA | ✅ |
| TD05-04 | Mô phỏng Debug (`MockHoSoSource.SearchFeedback`, dòng "(mẫu)") và xuất Excel | ✅ |
| TD05-05 | Kiểm chứng cột và đổi trang với dữ liệu thật | ⬜ cần tài khoản có phản ánh |
