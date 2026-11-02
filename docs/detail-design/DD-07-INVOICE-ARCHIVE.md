# DD-07 — Kho hóa đơn (SCR-05)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-07 |
| Tên | Truy vấn, lọc, tìm kiếm, phân trang, chọn nhiều và thao tác hàng loạt trên dữ liệu đã lưu |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review |
| Feature | [FT-08](../requirements/FEATURE-LIST.md#ft-08--kho-hóa-đơn) |
| SRS | FR-ARCH-001..010, BR-015, NFR-PERF-002, 004, NFR-USE-004, NFR-USE-002 |
| Màn hình | SCR-05 |
| Wireframe | [SCR-05](../wireframes/index.html?screen=SCR-05) · [PNG](../wireframes/png/SCR-05-archive.png) |
| Phụ thuộc | [DD-06](DD-06-INVOICE-STORAGE.md), [DD-01](DD-01-APP-SHELL-LOGIN.md) (navigation), [DD-09](DD-09-EXPORT.md) (hành động xuất) |
| Phase triển khai | [Phase 4](../plans/PROJECT-PLAN.md#phase-4--kho-hóa-đơn-chi-tiết-và-trình-xem) |

## 2. Mục đích và phạm vi

Màn hình trung tâm tra cứu dữ liệu đã lưu cục bộ của **MST đang hoạt động**. Không có chế độ xem trộn nhiều MST (FR-COMP-014). Hoạt động ở cả `ReadOnlyServiceMode` (xem, tìm, xuất).

## 3. Truy vấn

```csharp
public sealed class ArchiveQuery
{
    public InvoiceDirection Direction { get; set; }
    public DateTime? IssueFrom { get; set; }
    public DateTime? IssueTo { get; set; }
    public string Counterparty { get; set; }       // MST hoặc tên
    public string InvoiceNumber { get; set; }
    public string FormSymbol { get; set; }
    public string InvoiceSymbol { get; set; }
    public string Status { get; set; }
    public string CheckResult { get; set; }
    public FileIntegrityFilter FileFilter { get; set; }   // Tất cả | Lỗi/thiếu file
    public string QuickSearch { get; set; }               // số HĐ / MST / tên đối tác
    public ArchiveSort Sort { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}

public interface IInvoiceArchiveService
{
    Task<ArchivePage> QueryAsync(ArchiveQuery query, CancellationToken ct);       // rows + tổng + tóm tắt
    Task<ArchiveSummary> SummarizeAsync(ArchiveQuery query, CancellationToken ct); // số HĐ, chưa thuế, thuế, thanh toán
    Task<IList<InvoiceKey>> ResolveSelectionAsync(ArchiveQuery query, CancellationToken ct); // "chọn tất cả kết quả"
}
```

- Phân trang và sắp xếp thực hiện ở SQL; không nạp XML vào bộ nhớ (NFR-PERF-004).
- Mục tiêu: tìm theo số/MST và mở trang đầu ≤ 2 giây p95 trên 100.000 hóa đơn (NFR-PERF-002) → index theo số hóa đơn, MST đối tác, ngày lập, direction; tìm nhanh dùng tiền tố/`LIKE` có index trước, mở rộng sau khi đo.
- STT là số thứ tự hiển thị theo trang/sắp xếp, không phải khóa (BR-015).

## 4. Giao diện

> **Bộ lọc kho nên khớp form tra cứu của cổng** ([DD-04 mục 5.1a](DD-04-PERIOD-DOWNLOAD-CONFIG.md)): MST đối tác, CCCD người mua, trạng thái hóa đơn (`tthai` 1–6), kết quả kiểm tra (`ttxly` 0–8), ký hiệu mẫu số, ký hiệu hóa đơn, số hóa đơn, hóa đơn ủy nhiệm, khoảng ngày lập. Nhãn trạng thái/kết quả lấy từ `PortalOptions`.
> Cột lưới: STT, ký hiệu mẫu số, ký hiệu, số, ngày lập, MST/tên người mua (hoặc bán), tổng tiền chưa thuế, trạng thái — như bảng kết quả của cổng (`spec.xlsx`).

- Tab/bộ chọn `Mua vào` | `Bán ra` (FR-ARCH-001); thanh lọc đầy đủ + ô tìm nhanh.
- `DataGridView` virtual-mode hoặc phân trang; cột theo mẫu Excel mục 9.1 SRS; người dùng ẩn/hiện cột nhưng xuất Excel luôn theo mẫu chuẩn (FR-ARCH-005).
- Chỉ báo cảnh báo file lỗi/thiếu bằng biểu tượng + chữ; bộ lọc riêng cho file lỗi (FR-ARCH-009).
- Thanh tóm tắt tập kết quả hiện tại (FR-ARCH-006).
- Hành động: `Xem chi tiết` / nhấp đúp → SCR-06 (FR-ARCH-007); `Xuất Excel`, `Xuất ZIP XML`, `Kiểm tra file` (FR-ARCH-008); sao chép số hóa đơn/MST/đường dẫn file (FR-ARCH-010).
- Phím tắt: Enter mở chi tiết, Ctrl+F tìm nhanh, Ctrl+A chọn trang (NFR-USE-002).
- Bộ lọc, sắp xếp, trang, dòng chọn được giữ trong phiên khi xem chi tiết rồi quay lại (`ArchiveViewState` lưu trong navigation parameter — NFR-USE-004).
- Mở từ SCR-04/SCR-08/SCR-02 nhận `ArchiveQuery` khởi tạo (loại + phạm vi của phiên).
- Khi `NewDownload` không được phép, SCR-05 vẫn đầy đủ chức năng đọc/xuất.

## 5. Kiểm thử

### 5.1 Unit

- Dựng điều kiện SQL từ `ArchiveQuery` (parameterized, chống injection).
- Giữ/khôi phục `ArchiveViewState`; "chọn tất cả kết quả" vs "chọn trang".
- Tính tóm tắt trên decimal không mất độ chính xác.

### 5.2 Integration

- Dataset 100.000 hóa đơn: tìm theo số/MST, sắp xếp, phân trang đạt NFR-PERF-002.
- File thiếu/sai hash hiện đúng cảnh báo và lọc được.

### 5.3 UI smoke

- Mở từ phiên tải với bộ lọc đúng; quay lại từ SCR-06 giữ bộ lọc.

## 6. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 6.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T07-01 | `ArchiveQuery`, `ArchivePage`, `ArchiveSummary`, `ArchiveViewState` | Application | T06-01 | ✅ | ArchiveQuery/ArchivePage/ArchiveSummary; trạng thái view lưu bằng ArchiveViewStateStore | |
| T07-02 | `InvoiceArchiveRepository` (SQL lọc/sắp xếp/phân trang, index) | Infra | T06-03 | ⬜ | chờ SQLite (G-08); hiện lọc trong bộ nhớ | |
| T07-03 | `InvoiceArchiveService` (query, summary, resolve selection) | Application | T07-02 | ✅ | InvoiceArchiveService (lọc, sắp xếp, phân trang, tóm tắt) trên IInvoiceRepository | |
| T07-04 | `InvoiceArchiveControl` + presenter (SCR-05): lưới, lọc, tìm nhanh, tóm tắt | WinForms | T07-03, T01-05 | ✅ | ArchiveControl + ArchivePresenter; lưới, lọc, tìm nhanh, thẻ tóm tắt | |
| T07-05 | Chọn nhiều, ẩn/hiện cột, sao chép, phím tắt | WinForms | T07-04 | ⬜ | chọn nhiều/cột/phím tắt chưa làm | |
| T07-06 | Chỉ báo + bộ lọc file lỗi, hành động `Kiểm tra file` | WinForms | T06-06 | ✅ | cột + bộ lọc file lỗi, nút Kiểm tra file | |
| T07-07 | Giữ trạng thái khi vào/ra SCR-06 | WinForms | T07-04 | ✅ | giữ lọc/sắp xếp/trang theo doanh nghiệp; nhấp đôi một dòng mở SCR-06 và quay lại thấy đúng bộ lọc | |
| T07-08 | Test hiệu năng 100.000 bản ghi | Tests | T07-02 | ⬜ | chờ SQLite | NFR-PERF-002 |
| T07-09 | Unit + UI smoke test (mục 5.1, 5.3) | Tests | T07-04 | ✅ | 20 test service, 21 test presenter, 1 smoke test | |

### 6.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 7. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
