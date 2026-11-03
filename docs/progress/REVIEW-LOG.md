# Nhật ký review mã

Mỗi lần review ghi lại commit cuối đã được xét. Lần sau review phần thay đổi từ mốc này:
`git diff <mốc>..HEAD` (bỏ `*.Designer.cs`), ưu tiên mã nguồn trong `src/`.

| Ngày | Phạm vi | Mốc (commit cuối đã review) | Kết quả |
|---|---|---|---|
| 02/11/2026 | `e91145c..f53c7d1` | `f53c7d1` | Các phát hiện đã sửa trong chính `f53c7d1` |
| 03/11/2026 | `f53c7d1..78cc0f5` | `78cc0f5` | 10 phát hiện; đã sửa 1, 2, 4, 5, 8, 9 (7 là báo nhầm); còn lại chưa sửa: xem mục dưới |

## Review 03/11/2026 (`f53c7d1..78cc0f5`)

Đã đọc kỹ: `BackupService`, `SqliteDatabase`, `CompanyDatabaseInitializer`, `SqliteInvoiceRepository`, `SqliteDownloadRepository`,
`DownloadOrchestrator.RecoverInterruptedAsync`. Chỉ lướt: `InvoiceExportService`, `InvoiceExcelWriter`. **Chưa review**: các presenter/control
WinForms (Dashboard, Detail, Archive, Settings), `Dashboard.cs`, `InvoiceDetail.cs`, `StorageStatistics.cs`.

1. ✅ Đã sửa. Khôi phục không kiểm lại SHA-256 của byte đọc ở lượt ghi (`BackupService`, vòng ghi trong `RestoreAsync`).
2. ✅ Đã sửa (`File.Replace`). `Replace` xóa file đích rồi mới `Move`: tắt máy giữa hai bước mất file; nên dùng `File.Replace`.
3. Gói sao lưu không chứa `company.db`: khôi phục lên máy mới có XML nhưng Kho hóa đơn trống (đã biết, chờ formatVersion 2).
4. ✅ Đã sửa (giữ chỗ phiên trong `_active`). `RecoverInterruptedAsync` có thể đua với một lượt chạy vừa bắt đầu giữa lúc đọc và ghi trạng thái.
5. ✅ Đã sửa (tên duy nhất). Tên điểm sao lưu an toàn chỉ chính xác đến giây; `CreateCore` ghi đè file cùng tên.
6. Truy vấn Kho gọi `File.Exists` cho mọi hóa đơn khớp (đã biết, chưa đo trên đĩa thật).
7. ❌ Báo nhầm: `Csv()` đã thêm `'` trước ô bắt đầu bằng `= + - @`. Manifest CSV khi xuất không vô hiệu hóa ô bắt đầu bằng `= + - @` (dữ liệu từ cổng Thuế).
8. ✅ Đã sửa (Unspecified = UTC). `SqliteValues.Time` coi `DateTime` kiểu Unspecified là giờ local rồi đổi sang UTC.
9. ✅ Đã sửa (chỉ UNIQUE trên khóa nghiệp vụ). `AddAsync` coi mọi lỗi ràng buộc là "khóa nghiệp vụ đã tồn tại".
10. Migration chạy thẳng trên `company.db` mà không sao chép file trước.
