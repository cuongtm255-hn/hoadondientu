# ADR 0004 — Metadata hóa đơn và phiên tải lưu trong bộ nhớ cho tới khi chọn SQLite (G-08 tạm thời)

| | |
|---|---|
| Trạng thái | **Superseded bởi [ADR 0006](0006-sqlite-provider.md)** (03/11/2026): repository SQLite đã được cài đặt và nối vào `CompositionRoot` (T05-02, T06-03); các lớp trong bộ nhớ chỉ còn dùng cho test |
| Ngày | 02/11/2026 |
| Gate | G-08 SQLite provider ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md)) |
| Liên quan | [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md), [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md), [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md), [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md) |

## Bối cảnh

DD-06 thiết kế metadata và phiên tải trong SQLite (`company.db`), nhưng chưa chọn được provider tương thích net45 (x86/x64, phân phối thư viện native, WAL, backup API — gate G-08).
Để không chặn các màn hình và engine phía trên, cần một cách lưu tạm mà không làm sai hợp đồng với phần còn lại.

## Quyết định

1. Mọi truy cập dữ liệu đi qua **cổng giao diện** `IInvoiceRepository` và `IDownloadRepository`; lớp trên (engine tải, lưu trữ, kho hóa đơn, lịch sử, tổng quan, chi tiết, xuất) chỉ biết hai giao diện này.
2. Cài đặt tạm là `InMemoryInvoiceRepository` (bọc theo doanh nghiệp bằng `CompanyScopedInvoiceRepository`) và `InMemoryDownloadRepository`; đọc luôn trả **bản sao** để mô phỏng lưu bền vững.
3. **File XML vẫn được ghi thật** vào thư mục kho của MST (file tạm → đổi tên atomic, `FileSystemInvoiceFileStore`); chỉ metadata và phiên tải nằm trong bộ nhớ.
4. **Bản Release không cho tải thật** (`UnavailableDownloadSessionStarter`): nếu cho tải khi metadata mất sau khi tắt ứng dụng thì có file mà không có chỉ mục, và phiên dở dang không khôi phục được.
5. Khi chọn SQLite: viết ADR mới, cài đặt hai giao diện trên bằng SQLite, đổi ở `CompositionRoot`, thêm migration; không sửa lớp trên.

## Hệ quả

- (+) Toàn bộ luồng tải → lưu → tra cứu → chi tiết → xuất chạy được trên dữ liệu giả và có test; thay SQLite không đụng nghiệp vụ.
- (−) **Sau khi tắt ứng dụng, Kho hóa đơn, lịch sử và tổng quan trống** dù file XML vẫn còn trên đĩa; phiên dở dang không khôi phục được (T05-09 chưa làm).
- (−) Lọc, sắp xếp, phân trang, đếm đều làm trong bộ nhớ và sao chép toàn bộ danh sách mỗi truy vấn; chưa đạt NFR-PERF-002 (100.000 hóa đơn). Chưa có `ExportRecord` (T09-01), `FileAsset` PDF (T08-04).
- (−) Khôi phục chỉ mục từ thư mục kho (quét file, T06-06) cần có thể làm được độc lập với SQLite nếu muốn bản Release dùng được sớm; chưa quyết định.
- (−) Gói sao lưu (DD-11) chỉ chứa file XML/PDF và `manifest.json`; không có bản chụp database. Khôi phục đưa file trở lại thư mục kho nhưng chỉ mục Kho hóa đơn chưa tự dựng lại.
