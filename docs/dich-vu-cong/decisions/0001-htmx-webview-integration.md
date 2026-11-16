# ADR DVC-0001 — Nhóm Dịch vụ công: cổng `dichvucong.gdt.gov.vn` là ứng dụng htmx, tích hợp qua phiên WebView

> Thuộc nhóm chức năng **Dịch vụ công** ([README](../README.md)). ADR của nhóm Hóa đơn nằm ở [../../decisions/](../../decisions/README.md); hai nhóm không dùng chung số thứ tự.

- Ngày: 16/11/2026
- Trạng thái: Đề xuất (dựa trên khảo sát ngày 16/11/2026, chưa có mẫu phản hồi có dữ liệu; xem [khảo sát](../api-samples/khao-sat-tthc.md))

## Bối cảnh

Nhóm chức năng Dịch vụ công (DVC) tra cứu các mục "Thủ tục hành chính" và "Tra cứu hồ sơ" của cổng Dịch vụ công ngành Thuế. Khác cổng hóa đơn (`hoadondientu.gdt.gov.vn`, REST JSON + JWT), khảo sát cho thấy:

- Không có API JSON: form gửi `POST`/`GET` (htmx) và nhận mảnh HTML bảng kết quả; POST kèm CSRF (`htmx-csrf.js`).
- Phiên là cookie SSO; **chỉ một tab hoạt động mỗi tài khoản** (tab thứ hai bị báo "đang được sử dụng trên một tab khác").
- Tra cứu hồ sơ đã nộp (2 tab) bắt buộc CAPTCHA; lưu tạm, phản ánh, TTHC đất đai, thông báo LPTB không có CAPTCHA.
- Danh mục lọc (loại TTHC, tờ khai...) nằm sẵn trong HTML.

## Phương án

1. **Phiên WebView + đọc HTML (đề xuất).** Người dùng đăng nhập SSO trong WebView (CefSharp; xem ADR [0007](../../decisions/0007-webview-cefsharp.md) và [0011](../../decisions/0011-own-login-form-hidden-webview.md) của nhóm Hóa đơn; dùng lại thư viện, không dùng chung phiên), app điền form và gửi trong trang, rồi phân tích HTML trả về. Hợp với quy tắc "không giải CAPTCHA": mục có CAPTCHA, người dùng tự nhập.
2. `HttpClient` + cookie/CSRF chép từ WebView: dễ vỡ (CSRF đổi theo trang, kiểm một-tab) và tăng rủi ro lộ phiên. Không chọn.
3. Chỉ mở trang cổng trong WebView, không bóc dữ liệu: không xuất Excel/lưu được. Dự phòng nếu phương án 1 không khả thi.

## Quyết định (dự kiến, chờ chốt sau khi có mẫu có dữ liệu)

- Dùng phương án 1; bộ phân tích HTML tách khỏi UI, kiểm thử bằng mẫu HTML đã làm sạch/dữ liệu giả (ADR 0005).
- Một phiên duy nhất: app giữ **một** WebView TTHC, không mở tab/cửa sổ thứ hai cho cùng tài khoản; báo rõ khi cổng báo trùng phiên.
- Không lưu mật khẩu, CAPTCHA, CSRF, cookie; mẫu trong repo phải làm sạch.

## Hệ quả

- Hợp đồng dữ liệu là HTML, dễ vỡ khi cổng đổi giao diện: cần kiểm tra cấu trúc và báo lỗi rõ.
- Cần xác nhận trên cổng thật (người dùng thực hiện, Release hoặc ngoại lệ đăng nhập lần đầu theo CLAUDE.md): mẫu HTML có dữ liệu, phân trang, tải file, tên trường CSRF.
