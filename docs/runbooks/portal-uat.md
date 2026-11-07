# Runbook — UAT trên cổng Thuế thật (P6-01, T05-11)

Mục tiêu: kiểm bằng **bản Release thật**, do **người dùng thật** chạy, các kịch bản ở SRS mục 16.3. Đây là ngoại lệ duy nhất cho quy tắc "không gọi cổng thật" ([ADR 0005](../decisions/0005-mock-only-testing-policy.md)): người dùng tự nhập tài khoản và CAPTCHA, ứng dụng không tự động hóa đăng nhập, không lưu hay ghi log mật khẩu, token, cookie.

## Chuẩn bị

- Gói `dist\HoaDonDienTu-1.0.0-win-x86.zip` (đối chiếu SHA-256), giải nén vào một thư mục. Máy đã qua kích hoạt/dùng thử (xem [license-uat.md](license-uat.md)).
- Tài khoản cổng Thuế của một MST có hóa đơn **mua vào và bán ra**, trong ít nhất một tháng có **trên 50 hóa đơn** (để có nhiều trang) và một kỳ **không có** hóa đơn.
- Một file Excel xuất từ chính cổng cho cùng kỳ (để đối chiếu cột và số liệu).
- Chỉ dùng dữ liệu của chính bạn. **Không** chép token, cookie, mật khẩu hay hóa đơn thật vào repo hay ảnh chụp gửi đi.

## Kịch bản

| # | Kịch bản (SRS 16.3) | Thao tác | Kết quả mong đợi | Đạt? |
|---|---|---|---|---|
| 1 | Đăng nhập thủ công, lấy token | Mở app, bấm đăng nhập, nhập MST/mật khẩu/CAPTCHA trong cửa sổ trình duyệt nhúng | App báo đã đăng nhập; không ô nào được điền hộ | |
| 2 | Bán ra trong kỳ có dữ liệu | Tra cứu bán ra một tháng | Số hóa đơn bằng số trên cổng; tổng tiền khớp | |
| 3 | Mua vào trong kỳ có dữ liệu | Tra cứu mua vào cùng tháng | Số hóa đơn khớp cổng. *Chưa có traffic thật cho truy vấn mua vào (⚠ trong `docs/api-samples/README.md`)* | |
| 4 | Kỳ không có dữ liệu | Tra cứu kỳ rỗng | Thông báo rỗng, không lỗi | |
| 5 | Nhiều trang trong một tháng | Tra cứu tháng trên 50 hóa đơn | Đủ hóa đơn, không trùng, không sót. *Phân trang `state` chưa kiểm* | |
| 6 | Quý/năm/khoảng tự chọn | Tra cứu một quý, rồi từ–đến chéo hai tháng | Số hóa đơn bằng tổng từng tháng trên cổng | |
| 7 | Tải về kho | Tải một tháng về kho | Mỗi hóa đơn có `.xml` **và `.zip`** cạnh nhau; mở `.zip` thấy `invoice.xml`, trang HTML, `details.js`, ảnh | |
| 8 | Xuất Excel so với mẫu cổng | Xuất danh sách, mở cạnh file Excel của cổng | Cột, tiêu đề, kiểu số/ngày, số 0 đầu khớp | |
| 9 | XML ZIP và in | Xuất ZIP XML; giải nén; mở `invoice.html` bằng trình duyệt, in thử | Trang xem giống trên cổng, in được | |
| 10 | Đăng nhập lại giữa phiên | Trong lúc tải, để token hết hạn hoặc đăng xuất cổng ở nơi khác | App yêu cầu đăng nhập lại, đăng nhập xong **tiếp tục** đúng chỗ, không tải lại hóa đơn cũ. *401 thật chưa kiểm* | |
| 11 | Giới hạn tốc độ | Tải nhiều tháng liên tiếp | Nếu cổng trả 429 thì app chờ/giảm nhịp, không bỏ sót. *429 thật chưa kiểm* | |
| 12 | Đăng nhập đúng/sai MST sau khi chuyển doanh nghiệp | (ServiceMode) chuyển sang doanh nghiệp B rồi đăng nhập bằng MST của A | App từ chối/cảnh báo, không lưu dữ liệu A vào kho B | |
| 13 | Một phiên Thuế, một SQLite | Khi chuyển doanh nghiệp, quan sát Task Manager / thư mục dữ liệu | Chỉ một phiên đăng nhập và một file SQLite đang mở tại mọi thời điểm | |

## Điều cần quan sát thêm

- **Cấu trúc zip thật** của cổng (tên file trong zip, có đúng `invoice.xml`/`invoice.html`?). Mock chỉ mô phỏng; nếu khác, ghi lại tên entry (không ghi nội dung hóa đơn) để chỉnh `ZipEntryName`.
- Dung lượng kho tăng bao nhiêu khi có thêm file zip (ghi số hóa đơn và tổng dung lượng).
- Cookie/token không xuống đĩa: sau khi đăng nhập, tìm trong `%LOCALAPPDATA%\HoaDonDienTu` chuỗi `Bearer`/`token` (xem [P6-03](../security/P6-03-security-review.md)).

## Ghi kết quả

Ghi vào `docs/progress/` (ví dụ `YYYY-MM-DD-portal-uat.md`): ngày, phiên bản, hệ điều hành, bảng trên với cột Đạt?, lỗi và cách tái hiện. **Chỉ commit mẫu đã làm sạch** (bỏ MST, tên, số tiền thật, token). Sau đó cập nhật các mục ⚠ trong `docs/api-samples/README.md`, T05-11 trong [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) và P6-01 trong [PROJECT-PLAN](../plans/PROJECT-PLAN.md).
