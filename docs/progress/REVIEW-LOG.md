# Nhật ký review mã

Mỗi lần review ghi lại commit cuối đã được xét. **Mốc hiện tại: `78cc0f5`** — toàn bộ `f53c7d1..78cc0f5` đã được review; các commit sau đó (`958efb9`, `0d064a7`, `5c186f0`, `ac27b4e`, `e808167`) là bản sửa do chính người viết tạo, chỉ được kiểm bằng test, nên lần sau review từ `78cc0f5`. Lần sau review phần thay đổi từ mốc này:
`git diff <mốc>..HEAD` (bỏ `*.Designer.cs`), ưu tiên mã nguồn trong `src/`.

| Ngày | Phạm vi | Mốc (commit cuối đã review) | Kết quả |
|---|---|---|---|
| 02/11/2026 | `e91145c..f53c7d1` | `f53c7d1` | Các phát hiện đã sửa trong chính `f53c7d1` |
| 03/11/2026 | `f53c7d1..78cc0f5` | `78cc0f5` | 10 phát hiện; đã sửa 1, 2, 3, 4, 5, 8, 9, 10 (7 là báo nhầm); còn lại chưa sửa: xem mục dưới |
| 03/11/2026 (bổ sung) | Phần WinForms/Application còn lại của `f53c7d1..78cc0f5` | `78cc0f5` | 8 phát hiện; đã sửa 3, 4, 5; còn lại chưa sửa: xem mục "Bổ sung" |

## Review 03/11/2026 (`f53c7d1..78cc0f5`)

Đã đọc kỹ: `BackupService`, `SqliteDatabase`, `CompanyDatabaseInitializer`, `SqliteInvoiceRepository`, `SqliteDownloadRepository`,
`DownloadOrchestrator.RecoverInterruptedAsync`. Chỉ lướt: `InvoiceExportService`, `InvoiceExcelWriter`. Phần WinForms đã review ở mục "Bổ sung" bên dưới.

1. ✅ Đã sửa. Khôi phục không kiểm lại SHA-256 của byte đọc ở lượt ghi (`BackupService`, vòng ghi trong `RestoreAsync`).
2. ✅ Đã sửa (`File.Replace`). `Replace` xóa file đích rồi mới `Move`: tắt máy giữa hai bước mất file; nên dùng `File.Replace`.
3. ✅ Đã sửa (gói định dạng 2 có company.db). Gói sao lưu không chứa `company.db`: khôi phục lên máy mới có XML nhưng Kho hóa đơn trống (đã biết, chờ formatVersion 2).
4. ✅ Đã sửa (giữ chỗ phiên trong `_active`). `RecoverInterruptedAsync` có thể đua với một lượt chạy vừa bắt đầu giữa lúc đọc và ghi trạng thái.
5. ✅ Đã sửa (tên duy nhất). Tên điểm sao lưu an toàn chỉ chính xác đến giây; `CreateCore` ghi đè file cùng tên.
6. Truy vấn Kho gọi `File.Exists` cho mọi hóa đơn khớp (đã biết, chưa đo trên đĩa thật).
7. ❌ Báo nhầm: `Csv()` đã thêm `'` trước ô bắt đầu bằng `= + - @`. Manifest CSV khi xuất không vô hiệu hóa ô bắt đầu bằng `= + - @` (dữ liệu từ cổng Thuế).
8. ✅ Đã sửa (Unspecified = UTC). `SqliteValues.Time` coi `DateTime` kiểu Unspecified là giờ local rồi đổi sang UTC.
9. ✅ Đã sửa (chỉ UNIQUE trên khóa nghiệp vụ). `AddAsync` coi mọi lỗi ràng buộc là "khóa nghiệp vụ đã tồn tại".
10. ✅ Đã sửa (`company.db.v<N>.bak` trước khi nâng). Migration chạy thẳng trên `company.db` mà không sao chép file trước.

## Bổ sung 03/11/2026: presenter/control WinForms và dịch vụ đi kèm

Đã đọc: `DashboardService`, `InvoiceDetailService`, `XmlTreeBuilder`, `StorageStatisticsService`, `DashboardPresenter`, `InvoiceDetailPresenter`,
`InvoiceViewerPresenter`/`InvoiceViewerControl`, `ArchivePresenter` (luồng làm mới, xuất, kiểm file), `SettingsPresenter` (sao lưu/khôi phục);
lướt các control còn lại chỉ để xem marshal sang UI thread (đều có `OnUi`).

1. `DashboardService.GetAsync` nạp mọi hóa đơn kèm mọi phiên bản (`ListAsync`) và quét `File.Exists` hai lần mỗi lần mở Tổng quan; vi phạm tinh thần T07-02 ở 100.000 hóa đơn.
2. `InvoiceViewerControl.ShowResult` gọi `TreeView.ExpandAll()` trên tới 50.000 nút ở luồng UI: treo giao diện với XML lớn.
3. ✅ Đã sửa (`TooLarge` + `ReadBytesAsync`). XML lớn hơn 5 MB: thông báo bảo "hãy lưu bản sao" nhưng `Bytes` là null nên nút Lưu bản sao bị khóa.
4. ✅ Đã sửa (hộp xác nhận nêu rõ cơ sở dữ liệu bị thay). Hộp xác nhận khôi phục không nói cơ sở dữ liệu (chỉ mục, lịch sử tải) sẽ bị thay: hóa đơn tải sau lúc sao lưu biến mất khỏi Kho (điểm an toàn vẫn giữ được).
5. ✅ Đã sửa (mỗi chiều một mục). Việc cần xử lý "XML thiếu" cộng cả hai chiều nhưng bấm vào chỉ lọc một chiều (mua vào nếu có).
6. "Tổng thanh toán năm" cộng cả mua vào, bán ra và cộng lẫn ngoại tệ như một con số VND.
7. `StorageStatistics` bỏ qua thư mục `Data` (company.db và các bản `.v<N>.bak`): dung lượng cơ sở dữ liệu không hiện.
8. Tổng quan chỉ xét 500 phiên mới nhất (`PageSize = 500`) cho phiên đang chạy, phiên lỗi và lần đồng bộ cuối.
