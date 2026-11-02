# Quyết định kiến trúc (ADR)

Mỗi quyết định một file `NNNN-<tên-ngắn>.md` gồm bối cảnh, phương án, bằng chứng, quyết định, hệ quả. Quy trình ở [DD-00 mục 5](../detail-design/DD-00-TECHNICAL-GATES.md).

| ADR | Quyết định | Trạng thái | Gate |
|---|---|---|---|
| [0001](0001-portal-api-contract.md) | Hợp đồng API cổng Thuế dạng cấu hình + cổng giả | Accepted một phần (mua vào, `state` còn ⚠) | G-02 |
| [0002](0002-excel-export-fidelity.md) | Bảng kê Excel: độ khớp với mẫu của cổng | Accepted | G-04 |
| [0003](0003-xlsx-and-zip-generation.md) | Tạo .xlsx và .zip không dùng thư viện ngoài | Accepted một phần (PDF chưa chốt) | G-09 (Excel/ZIP) |
| [0004](0004-interim-in-memory-metadata.md) | Metadata và phiên tải trong bộ nhớ cho tới khi cài đặt SQLite | Accepted (tạm thời) | G-08 |
| [0005](0005-mock-only-testing-policy.md) | Chỉ kiểm thử trên dữ liệu giả; cổng thật chỉ ở bản Release | Accepted | — |
| [0006](0006-sqlite-provider.md) | SQLite provider cho net45: System.Data.SQLite.Core | Accepted (còn kiểm máy sạch) | G-08 |

Các gate **chưa có ADR**: G-01 (WebView), G-03 (PDF), G-05 (giới hạn tốc độ), G-06 (giao thức licence), G-07 (an toàn chuyển MST), G-09 phần PDF.
