# ADR 0006 — SQLite provider cho .NET Framework 4.5: System.Data.SQLite.Core (G-08)

| | |
|---|---|
| Trạng thái | **Accepted** — có điều kiện cần kiểm trên máy sạch (xem "Chưa kiểm") |
| Ngày | 02/11/2026 |
| Gate | G-08 SQLite provider ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md)) |
| Thay thế một phần | [ADR 0004](0004-interim-in-memory-metadata.md): chốt provider; việc cài đặt repository bằng SQLite vẫn còn |
| Liên quan | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md), [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md), [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md), [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md), [spike](../../spikes/SqliteSpike/README.md) |

## Bối cảnh

Metadata hóa đơn, phiên tải, checkpoint và lịch sử đang nằm trong bộ nhớ (ADR 0004), nên sau khi tắt ứng dụng mọi chỉ mục mất, bản Release chưa tải được,
phiên dở dang không khôi phục được và chưa đạt NFR-PERF-002 (100.000 hóa đơn). DD-06 thiết kế `company.db` bằng SQLite nhưng gate G-08 yêu cầu chứng minh một provider chạy được trên
**net45**, x86/x64, có WAL và backup API, rồi mới được chọn.

## Phương án và bằng chứng

Thử trực tiếp trong [spike](../../spikes/SqliteSpike/README.md) (dữ liệu giả, 02/11/2026):

| Phương án | Kết quả |
|---|---|
| **System.Data.SQLite.Core 1.0.118** (SQLite 3.42.0) | Restore/build trên net45 thành công; chạy được 64-bit và 32-bit; WAL, `integrity_check`, `BackupDatabase`, `user_version`, giao dịch hoạt động |
| `Microsoft.Data.Sqlite(.Core)` 3.1.32 / 8.0.0 | `NU1202` không tương thích net45 |
| `sqlite-net-pcl` 1.8.116 | `NU1202` không tương thích net45 |
| `SQLitePCLRaw.bundle_e_sqlite3` 2.1.6 | `NU1202` không có bản net45 |
| LiteDB và các CSDL nhúng khác | Không thử: thiết kế dữ liệu của dự án là quan hệ + SQL (lọc, sắp xếp, đếm, tóm tắt) và cần backup/`integrity_check` chuẩn |

Với System.Data.SQLite.Core (kết quả chính):

- 100.000 hóa đơn nạp trong ≈ 2,5–2,8 s; trang 50 dòng sắp theo ngày lập (kể cả `OFFSET 50000`), `COUNT`, tìm theo MST, tra khóa duy nhất đều ≤ 1 ms; tóm tắt `SUM` theo năm ≈ 99 ms; tìm nhanh `LIKE` quét toàn bảng ≈ 23 ms → **đạt NFR-PERF-002 với dư địa lớn**.
- Một người ghi + người đọc liên tục dưới WAL: không lỗi khóa, lần đọc chậm nhất 1 ms.
- `BackupDatabase` cho bản sao nhất quán kể cả khi đang có người ghi (NFR-REL-003).
- Giết tiến trình đang ghi 16 lần (8 vòng × 2 kiến trúc): không lần nào hỏng, không có giao dịch dở (NFR-REL-001/003).
- Gói phân phối nhỏ: DLL ≈ 441 KB + interop 1,5 MB (x86) / 1,8 MB (x64); bản interop không import thư viện VC++ runtime.
- Giấy phép: SQLite và System.Data.SQLite thuộc phạm vi công cộng (public domain) — không ràng buộc phân phối.

## Quyết định

1. Dùng **`System.Data.SQLite.Core` 1.0.118** (ghim đúng phiên bản; nâng cấp phải chạy lại spike) làm provider cho `company.db`. Chạy được cả x86 và x64; **phải phân phối cả hai thư mục `x86\` và `x64\`** cùng `System.Data.SQLite.dll`.
2. Cấu hình kết nối bắt buộc: `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON`, `busy_timeout` ≈ 5 giây; mở `PRAGMA integrity_check` khi mở kho và sau khôi phục.
3. Quy ước truy cập: **một kết nối ghi tuần tự** (khóa `SemaphoreSlim`, khớp `InvoiceStorageService._gate`) và mỗi truy vấn đọc dùng kết nối riêng; mọi ghi nhiều dòng nằm trong giao dịch; đọc kết quả lớn bằng `DataReader` (không nạp cả danh sách).
4. Schema do `CompanyDatabaseInitializer` tạo và nâng cấp bằng **`PRAGMA user_version` + migration trong giao dịch** (đã chạy thử `ALTER TABLE ADD COLUMN`). Schema mới hơn ứng dụng bị từ chối.
5. **Tiếng Việt**: SQLite không hạ chữ hoa/bỏ dấu cho chữ có dấu. Lưu thêm cột tìm kiếm đã bỏ dấu và chữ thường do ứng dụng tạo (dùng đúng `InvoiceArchiveService.Fold`), tìm bằng `LIKE` trên cột đó; sắp xếp theo tên dùng cột đã bỏ dấu.
6. **Tiền và số thập phân**: không dùng `REAL`; lưu số nguyên theo đơn vị nhỏ nhất hoặc `TEXT` chính xác — chi tiết chốt trong DD-06 khi thiết kế schema.
7. **Sao lưu**: dùng `BackupDatabase` ra file tạm, `integrity_check` bản sao rồi đổi tên atomic; đưa `company.db` vào gói sao lưu (DD-11, tăng `formatVersion` lên 2, giữ đọc được gói định dạng 1 không có database).
8. Cài đặt `IInvoiceRepository` và `IDownloadRepository` bằng SQLite thay cho bản trong bộ nhớ ở `CompositionRoot` (ADR 0004 mục 5); bản Release chỉ bật tải thật sau khi việc này xong.
9. Đường dẫn `company.db` chịu giới hạn 260 ký tự của .NET 4.5 như các file khác; kho nằm trên đĩa cục bộ, không hỗ trợ thư mục mạng (WAL không an toàn trên chia sẻ mạng).

## Hệ quả

- (+) Gỡ chặn T06-02 (schema + initializer), T06-03 (repository), T07-02 (truy vấn SQL), T07-08 (hiệu năng 100.000), T05-02 (lưu phiên), T05-09 (khôi phục phiên khi khởi động) và phần database của sao lưu.
- (+) Giữ nguyên tầng trên nhờ hai giao diện repository (ADR 0004).
- (−) Phụ thuộc thêm một gói native (interop) theo kiến trúc; cần đóng gói đúng và kiểm trên máy sạch (P6-05).
- (−) Không có mã hóa dữ liệu lúc nghỉ ở bản miễn phí; XML và PDF cũng đang không mã hóa — nếu thành yêu cầu thì là quyết định riêng.
- (−) Bản System.Data.SQLite.Core 1.0.118 ghim SQLite 3.42.0; theo dõi bản vá bảo mật của SQLite và dự án System.Data.SQLite.

## Chưa kiểm (cần làm trước khi phát hành, ghi ở P6)

- Máy sạch và máy chỉ có runtime .NET 4.5 (máy thử có .NET 4.8): xác nhận interop nạp được và không cần Visual C++ Redistributable.
- Phần mềm diệt virus chặn hoặc báo nhầm `SQLite.Interop.dll`.
- Ổ đĩa ngoài, ổ mạng ánh xạ, đường dẫn gần giới hạn 260 ký tự.
- Nhiều tiến trình ứng dụng cùng mở một `company.db`; tải trên 100.000 dòng và máy cấu hình thấp (số đo hiện chỉ từ một máy phát triển).
