# DD-00 — Cổng kỹ thuật (technical gates) và ADR

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-00 |
| Tên | Technical spike, bằng chứng và ADR phải hoàn thành trước khi code phụ thuộc |
| Phiên bản | 1.0 (tách từ DD-HDDT-APP-SHELL-LOGIN-001 mục 19 và SRS mục 17) |
| Ngày | 01/11/2026 |
| Trạng thái | Chưa thực hiện |
| Feature | [FT-00](../requirements/FEATURE-LIST.md#ft-00--cổng-kỹ-thuật-và-adr) |
| SRS | Mục 6.4, 11, 17 |
| Phụ thuộc | Không; là điều kiện cho DD-01 (production adapter), DD-03, DD-05, DD-08, DD-09 |
| Phase triển khai | [Phase 1](../plans/PROJECT-PLAN.md#phase-1--nền-tảng-và-technical-spike) |

## 2. Mục đích

Các quyết định dưới đây không đổi yêu cầu nghiệp vụ nhưng quyết định kiến trúc triển khai. Mỗi gate phải có bằng chứng thử nghiệm và ADR trong `docs/decisions/` (`NNNN-title.md`) trước khi code phụ thuộc vào lựa chọn đó. Mẫu API lưu trong `docs/api-samples/` sau khi loại token, cookie, MST thật, CCCD, tên, địa chỉ và nội dung hóa đơn.

## 3. WebView technical gate (G-01)

Detail Design không chọn WebView cụ thể. Trước implementation production phải:

1. Thử WebBrowser/CefSharp hoặc lựa chọn phù hợp khác trên máy đích.
2. Xác nhận tương thích .NET Framework 4.5.
3. Render được cổng Thuế thật.
4. Nhập CAPTCHA thủ công được.
5. Phát hiện và lấy được session hợp lệ.
6. Xóa session giữa hai MST.
7. Chặn/kiểm soát điều hướng ngoài domain.
8. Ghi quyết định tại `docs/decisions/`.

Không đưa API WebView cụ thể ra ngoài Infrastructure adapter.

## 4. Các gate còn lại

| Gate | Nội dung cần chứng minh | Đầu ra | Chặn |
|---|---|---|---|
| G-02 API discovery | Endpoint, method, header, pagination/cursor, giới hạn ngày, page size, error schema, cách xác minh phiên/MST, cách tải XML/PDF | `docs/api-samples/*` đã làm sạch + ADR | DD-01 (production auth), DD-05, DD-06 |
|  | **Trạng thái 11/2026: 🟡 đóng một phần.** Đã có mẫu thật cho đăng nhập (JWT, MST ở `sub`), tra cứu **bán ra** (RSQL, mọi tham số), chi tiết, xuất XML (zip), xuất Excel; xem [api-samples](../api-samples/README.md). Còn thiếu: tra cứu mua vào, `state` phân trang, `size` tối đa, mã lỗi hết phiên | | |
| G-03 PDF strategy | Dùng PDF/print endpoint của cổng hay render XML theo mẫu; nhất quán với XML | ADR | DD-08 |
| G-04 Excel fidelity | So sánh file xuất với workbook mẫu: cột, kiểu dữ liệu, tiêu đề, phạm vi ngày | Báo cáo đối chiếu + ADR | DD-09 |
|  | **Trạng thái 11/2026: 🟡 đã có mẫu** `DANH SÁCH HÓA ĐƠN.xlsx` và `spec.xlsx`; bố cục 19 cột, tiêu đề, ô gộp, độ rộng cột đã ghi ở [api-samples mục 5](../api-samples/README.md). Còn lại: so sánh file do ứng dụng xuất với mẫu khi có `ExcelInvoiceWriter` | | |
| G-05 Rate limit | Nhịp gọi an toàn; hành vi 429/giới hạn tương đương | ADR + cấu hình mặc định | DD-05 |
| G-06 Licence protocol | API, thuật toán chữ ký, mã máy, mã hóa cục bộ, chu kỳ xác thực, hủy/reset, chính sách lệch đồng hồ | ADR + contract server | DD-03 (T03-04, T03-05) |
| G-07 Company switch safety | Chứng minh thứ tự đóng tác vụ/token/SQLite và phục hồi sau crash | Báo cáo spike + ADR | DD-01 mục 9, DD-02 |
| G-08 SQLite provider | Chọn thư viện SQLite tương thích net45 (x86/x64, phân phối native), WAL, backup API | ADR | DD-06 |
| G-09 PDF/Excel library | Chọn thư viện tạo xlsx/pdf/zip tương thích net45 và giấy phép | ADR | DD-08, DD-09 |

G-08 và G-09 bổ sung so với SRS mục 17 vì chúng quyết định ràng buộc `net45` của phần lưu trữ và xuất.

## 5. Quy trình ADR

1. Tạo `docs/decisions/NNNN-<ten-ngan>.md` gồm: bối cảnh, phương án đã thử, bằng chứng, quyết định, hệ quả.
2. Cập nhật trạng thái gate bên dưới và liên kết ADR.
3. Cập nhật “Ghi chú” của task bị chặn ở các DD liên quan và [PROJECT-PLAN](../plans/PROJECT-PLAN.md).

## 6. Theo dõi tiến độ

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 6.1 Công việc

| ID | Gate | Công việc | Phụ thuộc | Trạng thái | ADR/Bằng chứng | Ghi chú |
|---|---|---|---|---|---|---|
| T00-01 | G-01 | Spike WebBrowser/CefSharp/lựa chọn khác trên máy đích (net45) | — | ⬜ | | Cần tài khoản cổng Thuế thật |
| T00-02 | G-01 | Xác định cách phát hiện login và lấy token/cookie | T00-01 | ⬜ | | |
| T00-03 | G-02 | Ghi traffic: xác minh phiên/MST, tra cứu mua vào/bán ra, phân trang, tải XML | T00-02 | 🟡 | | Đã ghi traffic thật cho đăng nhập, tra cứu bán ra, chi tiết, xuất XML/Excel (11/2026) và cập nhật cấu hình/cổng giả/mã nguồn theo đó. **Còn:** tra cứu mua vào, phân trang `state`, mã lỗi hết phiên. Bản ghi nguyên văn chứa mật khẩu/token thật nên chỉ giữ bản đã làm sạch |
| T00-04 | G-05 | Đo giới hạn tốc độ / hành vi 429 | T00-03 | ⬜ | | |
| T00-05 | G-03 | Quyết định chiến lược PDF | T00-03 | ⬜ | | |
| T00-06 | G-04 | Đối chiếu Excel với workbook mẫu `DANH SÁCH HÓA ĐƠN.xlsx` | — | 🟡 | | Bố cục, ô gộp, 19 cột, kiểu ô khớp mẫu; test đọc lại từng ô và đã mở thử bằng Excel thật. Còn viết báo cáo đối chiếu/ADR G-04 |
| T00-07 | G-06 | Chốt giao thức licence với nhà cung cấp | — | ⬜ | | |
| T00-08 | G-07 | Spike chuyển MST/crash recovery | DD-01 T01-07 | ⬜ | | |
| T00-09 | G-08 | Đánh giá SQLite provider net45 | — | ⬜ | | |
| T00-10 | G-09 | Đánh giá thư viện Excel/PDF/ZIP net45 | — | 🟡 | | Excel: bộ ghi xlsx tự viết bằng System.IO.Compression; ZIP: System.IO.Compression. Còn chọn thư viện PDF (G-03/G-09) và SQLite (G-08), chưa có ADR |

### 6.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa chạy spike nào | Claude Code |

## 7. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Tách từ DD-01 v1.0 (mục 19) và SRS mục 17; thêm G-08, G-09 |
