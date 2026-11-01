# ADR 0001 — Hợp đồng API cổng Thuế dạng cấu hình + cổng giả

| | |
|---|---|
| Trạng thái | **Draft (tạm thời)** — chưa đối chiếu với cổng thật; chuyển `Accepted` khi G-02 xong |
| Ngày | 01/11/2026 |
| Liên quan | [DD-00 G-02](../detail-design/DD-00-TECHNICAL-GATES.md), [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md), [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md), [api-samples](../api-samples/README.md) |

## Bối cảnh

Endpoint, tham số, phân trang và cấu trúc phản hồi của cổng Thuế chưa được xác minh (chưa có tài khoản để ghi traffic), nhưng DD-04…DD-09 cần một
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
