# Ghi chú phát hành — Hóa đơn điện tử 1.0.0

Bản dựng: 06/11/2026 · Gói: `HoaDonDienTu-1.0.0-win-x86.zip` (103 MB, giải nén 210 MB) · Lược đồ cơ sở dữ liệu: 4 · Trình duyệt nhúng: CefSharp 109.1.110 (Chromium 109) · SQLite 1.0.118 · .NET Framework 4.5.2, x86.

> **Đọc trước khi dùng thật:** bản này **chưa qua thử nghiệm với cổng Thuế thật** (đăng nhập, tra cứu mua vào, phân trang `state`, giới hạn tốc độ 429, hết phiên 401) và **chưa có máy chủ bản quyền thật**. Mọi kiểm thử cho tới nay chạy trên cổng giả và dữ liệu mô phỏng theo quy định của dự án. Hãy thử với một kỳ nhỏ và đối chiếu số lượng với cổng Thuế.

## Tính năng

- Tải hóa đơn **mua vào / bán ra** theo **tháng, quý, năm hoặc khoảng ngày tùy chọn**, tự chia thành từng tháng, tạm dừng/tiếp tục/hủy, thử lại đoạn lỗi, không tạo hóa đơn trùng, lưu XML nguyên văn và giữ phiên bản cũ khi nội dung đổi.
- Đăng nhập cổng Thuế bằng trình duyệt nhúng, **người dùng tự nhập tài khoản và CAPTCHA**; không lưu mật khẩu hay mã phiên xuống đĩa.
- **Kho hóa đơn:** tra cứu, lọc, sắp xếp, tìm nhanh không phân biệt dấu; **xuất Excel** theo mẫu cổng Thuế và **xuất ZIP XML** (kèm manifest SHA-256); kiểm tra toàn vẹn file.
- **Chi tiết và xem chứng từ:** XML dạng cây/thô, PDF dựng từ XML, in, lưu bản sao nguyên văn.
- **Sao lưu và khôi phục** có kiểm SHA-256 và rollback; **đổi thư mục kho** an toàn (chép, kiểm, giữ thư mục cũ); **khôi phục danh sách doanh nghiệp** khi file danh mục hỏng.
- Nhiều doanh nghiệp (bản quyền dịch vụ), chế độ chỉ đọc khi hết hạn, Tổng quan, Lịch sử tải, lịch sử xuất.

## Chất lượng đã kiểm

- Kho 100.000 hóa đơn: mở trang đầu 0,5 s, tìm theo số 8 ms, chuyển kho 0,3 s (3,15 s lần đầu khi bộ nhớ đệm nguội); mở ứng dụng 1,4 s ([báo cáo](../performance/P6-02-performance-reliability.md)).
- Mất điện/đóng đột ngột không mất hóa đơn đã lưu; đĩa đầy giữa chừng tạm dừng có điểm dừng và tiếp tục không trùng.
- Rà soát bảo mật, quét gói NuGet, kiểm thử XML độc hại và gói sao lưu hỏng ([báo cáo](../security/P6-03-security-review.md)); bản phát hành được kiểm không chứa mã giả hay bí mật (`scripts/Check-Release.ps1`).

## Hạn chế đã biết

1. **Chưa thử trên cổng Thuế thật** (xem trên). Cấu hình API nằm trong `portal-api.json`; địa chỉ bắt buộc thuộc `gdt.gov.vn`.
2. **Bản quyền:** chưa kèm `license-api.json` (chờ máy chủ bản quyền), nên ứng dụng chạy **chế độ chuẩn** (một doanh nghiệp). Nhiều doanh nghiệp cần cấu hình và bản quyền dịch vụ.
3. **Trình duyệt nhúng Chromium 109 đã hết vòng đời** và bị báo 4 lỗ hổng đã biết; được giữ vì là bản cuối hỗ trợ Windows 7/8. Rủi ro và biện pháp giảm thiểu: [P6-03 mục F-2](../security/P6-03-security-review.md). Chỉ dùng để đăng nhập cổng Thuế.
4. **Chưa có trình cài đặt:** gói là bản giải nén chạy ngay, không có phím tắt hay mục gỡ cài đặt (gỡ bằng cách xóa thư mục chương trình; dữ liệu ở thư mục kho và `%LOCALAPPDATA%\HoaDonDienTu` được giữ). Chưa ký số file chạy nên Windows SmartScreen có thể cảnh báo.
5. Mục **Nhật ký và chẩn đoán** trong Cài đặt chưa có thao tác.
6. Tên hiển thị của doanh nghiệp khôi phục từ thư mục được đặt tạm theo MST.
7. Chưa kiểm trên máy Windows sạch (chưa cài sẵn Visual C++ 2019 Redistributable x86); chưa đo hiệu năng bản Release trên cấu hình chuẩn.

## Kiểm gói trước khi cài

Mở PowerShell tại thư mục chứa gói:

```powershell
(Get-FileHash .\HoaDonDienTu-1.0.0-win-x86.zip -Algorithm SHA256).Hash.ToLower()
```

So kết quả với file `HoaDonDienTu-1.0.0-win-x86.zip.sha256` đi kèm. Sau khi giải nén, `SHA256SUMS.txt` liệt kê mã băm từng file và `release-info.json` ghi phiên bản, lược đồ, phiên bản trình duyệt/SQLite và commit.

## Nâng cấp

Đóng ứng dụng, giải nén đè bản mới. Cơ sở dữ liệu của kho tự nâng lược đồ khi mở và tạo bản sao `company.db.v<N>.bak` trước khi nâng; ứng dụng không mở được kho có lược đồ mới hơn bản đang chạy.
