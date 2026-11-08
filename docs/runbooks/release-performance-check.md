# Runbook — Đo hiệu năng bản Release trên máy chuẩn (NFR-PERF-001, 003)

Hai mục này chỉ đo đúng được trên **bản Release thật** (Chromium, đăng nhập thật, máy thật); kiểm thử tự động không thay được. Kết quả điền vào `docs/performance/P6-02-performance-reliability.md`.

**Quy tắc cổng Thuế:** chỉ bước "Đăng nhập" (mục 3) được chạm cổng thật, người dùng tự nhập tài khoản và CAPTCHA. Không gọi API tra cứu/tải trong lúc đo. Không ghi MST, tên, token vào biên bản.

## Chuẩn bị

1. Gói: `dist\HoaDonDienTu-1.0.0-win-x86.zip`. Kiểm SHA-256 với `SHA256SUMS.txt` / giá trị công bố.
2. Máy chuẩn: ghi lại CPU, RAM, ổ đĩa (SSD/HDD), Windows (nên có một máy cấu hình thấp nhất được hỗ trợ, ví dụ Windows 7/8 hoặc HDD).
3. Giải nén vào thư mục mới, ví dụ `C:\HDDT-perf`. Đã kích hoạt hoặc còn trong 30 ngày dùng thử (nếu hết hạn, cửa sổ kích hoạt sẽ chặn, và đó không phải số đo khởi động).

## NFR-PERF-003: mở ứng dụng đến màn hình đăng nhập/tổng quan ≤ 5 giây

Đo 2 kiểu, mỗi kiểu **5 lần**, ghi cả lần đầu:

| Kiểu | Cách làm |
|---|---|
| Nguội | Khởi động lại máy (hoặc vừa giải nén), mở app lần đầu |
| Ấm | Đóng hẳn app (kiểm Task Manager không còn `HoaDonDienTu.exe`, `CefSharp.BrowserSubprocess`), mở lại |

Cách bấm giờ: đồng hồ bấm giờ từ lúc nhấn đúp `HoaDonDienTu.exe` đến lúc cửa sổ chính hiện đầy đủ, nhận được thao tác (màn hình tổng quan hoặc nút đăng nhập bấm được). Có thể quay màn hình 60 fps và đếm khung để chính xác hơn.

Điền:

| Lần | Nguội (giây) | Ấm (giây) |
|---|---|---|
| 1 | | |
| 2 | | |
| 3 | | |
| 4 | | |
| 5 | | |
| Trung vị | | |

Đạt: trung vị ấm và nguội đều ≤ 5 s. Ghi riêng nếu lần đầu sau cài vượt (Chromium giải nén/khởi tạo cache).

Thêm: dung lượng RAM sau khi mở xong (Task Manager, tổng các tiến trình của app).

## NFR-PERF-001: UI không treo khi gọi mạng, tính hash, xuất file, tạo PDF

Thước đo: trong lúc tác vụ chạy, kéo cửa sổ, đổi tab, cuộn bảng; cửa sổ không "Not responding" và con trỏ không chuyển sang vòng xoay quá 1 giây.

| # | Tác vụ | Cách thử | Treo? (Có/Không) | Ghi chú |
|---|---|---|---|---|
| 1 | Đăng nhập (WebView) | Mở trang đăng nhập, kéo cửa sổ khi đang tải | | |
| 2 | Tải hóa đơn khoảng nhiều tháng | Chỉ làm sau khi UAT cổng thật cho phép (xem `portal-uat.md`); không thì ghi "Chưa thử" | | |
| 3 | Tính hash khi nhập kho nhiều file XML | Nhập thư mục XML mẫu lớn (≥ 1.000 file) | | |
| 4 | Xuất Excel danh sách lớn | Kho ≥ 10.000 hóa đơn, xuất cả kỳ | | |
| 5 | Xuất ZIP XML | Như trên, thử bấm Hủy giữa chừng (NFR-PERF-005) | | |
| 6 | In/tạo PDF nhiều hóa đơn | Chọn ≥ 50 hóa đơn | | |
| 7 | Chuyển doanh nghiệp khi có kho lớn | Dịch vụ: chuyển qua lại 2 kho | | |

Kho thử lớn: dùng bản Debug với dữ liệu giả sinh sẵn (có nhãn MOCK) nếu cần kho 10.000+ hóa đơn; **không** dùng dữ liệu cổng thật để làm kho thử.

## Báo cáo

Sau khi đo, cập nhật:
1. Bảng NFR-PERF-001/003 trong `docs/performance/P6-02-performance-reliability.md`: số đo, máy, ngày, kết luận Đạt/Không đạt.
2. Mỗi mục "Không đạt" thành một task, kèm cách tái hiện.
