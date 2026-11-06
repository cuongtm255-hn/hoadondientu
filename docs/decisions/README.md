# Quyết định kiến trúc (ADR)

Mỗi quyết định một file `NNNN-<tên-ngắn>.md` gồm bối cảnh, phương án, bằng chứng, quyết định, hệ quả. Quy trình ở [DD-00 mục 5](../detail-design/DD-00-TECHNICAL-GATES.md).

| ADR | Quyết định | Trạng thái | Gate |
|---|---|---|---|
| [0001](0001-portal-api-contract.md) | Hợp đồng API cổng Thuế dạng cấu hình + cổng giả | Accepted một phần (mua vào, `state` còn ⚠) | G-02 |
| [0002](0002-excel-export-fidelity.md) | Bảng kê Excel: độ khớp với mẫu của cổng | Accepted | G-04 |
| [0003](0003-xlsx-and-zip-generation.md) | Tạo .xlsx và .zip không dùng thư viện ngoài | Accepted một phần (PDF chưa chốt) | G-09 (Excel/ZIP) |
| [0004](0004-interim-in-memory-metadata.md) | Metadata và phiên tải trong bộ nhớ cho tới khi cài đặt SQLite | Superseded bởi 0006 | G-08 |
| [0005](0005-mock-only-testing-policy.md) | Chỉ kiểm thử trên dữ liệu giả; cổng thật chỉ ở bản Release | Accepted | — |
| [0006](0006-sqlite-provider.md) | SQLite provider cho net45: System.Data.SQLite.Core | Accepted (còn kiểm máy sạch) | G-08 |
| [0007](0007-webview-cefsharp.md) | WebView đăng nhập: CefSharp 109 trên net452 (nâng từ net45) | Accepted hướng chọn (chưa thử trên cổng thật) | G-01 |
| [0008](0008-company-switch-safety.md) | Thứ tự an toàn khi chuyển MST và phục hồi sau sự cố | Accepted một phần (chưa spike giết tiến trình) | G-07 |
| [0009](0009-pdf-from-xml-pdfsharp.md) | PDF và in: tự dựng từ XML bằng PDFsharp-gdi 1.50 | Accepted (chưa đối chiếu hóa đơn thật nhiều dạng) | G-03, G-09 (PDF) |
| [0010](0010-simple-offline-license.md) | Bản quyền đơn giản: dùng thử 30 ngày + mã kích hoạt RSA offline theo máy | Accepted | G-06 |

Gate **chưa có ADR**: G-05 (giới hạn tốc độ), cần đo trên cổng thật (UAT). G-06 (licence) đã đóng bằng ADR 0010.
