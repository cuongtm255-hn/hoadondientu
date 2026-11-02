# ADR 0001 — Hợp đồng API cổng Thuế dạng cấu hình + cổng giả

| | |
|---|---|
| Trạng thái | **Accepted một phần** — đã đối chiếu traffic thật cho đăng nhập, tra cứu bán ra và mua vào (mẫu URL), chi tiết, xuất XML/Excel (11/2026); phân trang `state` còn là suy luận, đóng hẳn G-02 khi có mẫu |
| Ngày | 01/11/2026 |
| Liên quan | [DD-00 G-02](../detail-design/DD-00-TECHNICAL-GATES.md), [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md), [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md), [api-samples](../api-samples/README.md) |

## Bối cảnh

Ban đầu endpoint, tham số, phân trang và cấu trúc phản hồi của cổng Thuế chưa được xác minh (chưa có tài khoản để ghi traffic), nhưng DD-04…DD-09 cần một
`IPortalInvoiceClient` để phát triển và kiểm thử. Không được giả định cấu trúc chưa kiểm chứng (CLAUDE.md, SRS mục 17).

## Quyết định

1. Tầng Application chỉ biết hợp đồng `IPortalInvoiceClient` + DTO ổn định (`InvoiceSearchRequest`, `InvoicePage`, `PortalInvoiceDto`, `InvoiceXmlPayload`) và `PortalApiException` phân loại lỗi.
2. Mọi chi tiết đặc thù của cổng nằm trong **`PortalApiConfig`** (JSON, có schema version, có kiểm tra hợp lệ, mặc định `verified: false`). Client HTTP không chứa hằng endpoint/tên tham số/đường dẫn JSON.
3. Có **cổng giả** (`MockPortalHandler` + `MockInvoiceDataset`) đọc cùng cấu hình; chỉ biên dịch ở Debug. Mọi test client chạy lại trên nhiều hình dạng cấu hình khác nhau (con trỏ/query, số trang/query, POST/body + base64).
4. Khi có tài khoản thật chỉ cần: ghi traffic → làm sạch → chỉnh `portal-api.json` → đặt `verified: true` → chạy lại test. Chỉ sửa code nếu cấu trúc thật vượt khả năng cấu hình (xem "Giới hạn" trong api-samples/README).

## Hệ quả

- (+) DD-05/06/07/09 có thể tiến hành với dữ liệu giả phủ các tình huống khó: nhiều trang, tháng rỗng, số 0 đầu, ngoại tệ, trường rỗng, lỗi 401/429/5xx, đổi cấu trúc.
- (+) Rủi ro "cổng khác giả định" bị khoanh vào một file cấu hình và một bộ test.
- (−) Hợp đồng có thể phải đổi khi thấy cấu trúc thật (đặc biệt biểu thức lọc ngày gộp, phân trang, các trường không có trong 19 trường SRS).
- (−) Cổng giả chỉ mô phỏng hành vi tự định nghĩa; không thay thế UAT trên cổng thật (T05-11).

## Cập nhật 11/2026 — đối chiếu traffic thật

Đã có bản ghi traffic thật ([api-samples/README.md](../api-samples/README.md)). Kết quả:

- **Quyết định 1–2 giữ nguyên.** Hợp đồng `IPortalInvoiceClient` không đổi hình dạng; cấu hình (`schemaVersion` 2) giờ mặc định là giá trị thật. Các điều chỉnh bắt buộc theo cổng thật:
  - Tra cứu dùng **một tham số `search` dạng RSQL** (`tdlap=ge=…;tdlap=le=…;tthai==1;…`) thay vì từng tham số riêng → thêm `search.rsql`, bộ trường lọc theo chiều (`soldFilterFields`/`purchaseFilterFields`), thêm trường lọc CCCD người mua.
  - Xem chi tiết và xuất XML dùng **khóa tổng hợp** `nbmst, khhdon, shdon, khmshdon` (không dùng `id`); xuất XML trả **file zip** chứa `invoice.xml` → `download.responseMode = Zip`.
  - `tdlap` là UTC của 0 giờ ngày lập theo giờ VN → đổi múi giờ (+7) khi đọc `IssueDate`.
  - Phản hồi là `{datas, total, state, time}`; header `action`, `request-id`, `end-point` được gửi như trình duyệt.
  - Không có API hồ sơ: **MST xác minh bằng claim `sub` của JWT** (`PortalToken`), `exp` báo hết phiên. Đăng nhập vẫn do WebView; `PortalAuthCapture` ghi lại token, cookie, User-Agent và không bao giờ đọc mật khẩu/CAPTCHA.
- **Quyết định 3 điều chỉnh.** Cổng giả mô phỏng **đúng hình dạng API thật** (JWT, RSQL, zip) thay vì nhiều "hình dạng" tự định nghĩa; các biến thể Page/POST-Body/Base64 đã bỏ khỏi code và test vì cổng thật không dùng.
- **Bổ sung 02/11/2026 (mẫu thật tra cứu mua vào):** đường dẫn `/api/query/invoices/purchase`, `action` `Tìm kiếm (hóa đơn mua vào)`, lọc người bán bằng `nbmst`, có cả `nmcmnd` (CCCD người mua); dropdown Kết quả kiểm tra của mua vào chỉ có `ttxly` 5/6/8, không có "Tất cả" và bắt buộc. Hệ quả: khi người dùng muốn mọi kết quả, ứng dụng tải từng mã 5, 6, 8 (T05-12) cho tới khi xác nhận bỏ `ttxly` được.
- **Còn phải xác nhận (⚠):** gọi mua vào không có `ttxly`, 5/6/8 có phủ hết hóa đơn mua vào, tên tham số/điều kiện của `state`, `size` tối đa, tab "hóa đơn có mã khởi tạo từ máy tính tiền", giới hạn tốc độ (G-05).
- **Bảo mật:** bản ghi nguyên văn chứa mật khẩu/token/cookie thật không được lưu trong repo (`.gitignore`); chỉ dùng bản đã làm sạch.
