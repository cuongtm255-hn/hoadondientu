# DD-10 — Tổng quan và lịch sử tải (SCR-02, SCR-08)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-10 |
| Tên | Dashboard trạng thái kho/phiên và danh sách lịch sử phiên tải |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review |
| Feature | [FT-11](../requirements/FEATURE-LIST.md#ft-11--tổng-quan-và-lịch-sử-tải) |
| SRS | FR-DASH-001..003, FR-HIST-001..005, FR-JOB-010..011, AC-07 |
| Màn hình | SCR-02, SCR-08 |
| Wireframe | [SCR-02](../wireframes/index.html?screen=SCR-02) · [SCR-08](../wireframes/index.html?screen=SCR-08) · [PNG 02](../wireframes/png/SCR-02-dashboard.png) · [PNG 08](../wireframes/png/SCR-08-history.png) |
| Phụ thuộc | [DD-05](DD-05-DOWNLOAD-ENGINE.md), [DD-06](DD-06-INVOICE-STORAGE.md), [DD-07](DD-07-INVOICE-ARCHIVE.md), [DD-01](DD-01-APP-SHELL-LOGIN.md) |
| Phase triển khai | [Phase 5](../plans/PROJECT-PLAN.md#phase-5--xuất-dữ-liệu-tổng-quan-lịch-sử-cài-đặt-và-sao-lưu) |

## 2. Mục đích và phạm vi

Điểm vào sau đăng nhập (SCR-02) và nơi tra cứu phiên đã chạy (SCR-08). Chỉ đọc dữ liệu của MST đang hoạt động; không tổng hợp xuyên MST (FR-COMP-014). Số liệu danh mục nhiều MST nằm ở SCR-10 ([DD-02](DD-02-COMPANY-MANAGEMENT.md)).

## 3. Tổng quan (SCR-02)

```csharp
public interface IDashboardService
{
    Task<DashboardSnapshot> GetAsync(CancellationToken ct);
}

public sealed class DashboardSnapshot
{
    public PortalSessionSnapshot PortalSession { get; }     // không chứa token
    public long TotalInvoices { get; }
    public DateTime? LastSyncAt { get; }
    public IList<DownloadSessionSummary> ActiveOrPending { get; }   // đang chạy / chờ đăng nhập / chờ tiếp tục
    public IList<AttentionItem> Attention { get; }                   // phiên lỗi, file lỗi/thiếu
    public AppMode Mode { get; }
}
```

- Thẻ/chỉ báo: trạng thái đăng nhập, tổng hóa đơn đã lưu, lần đồng bộ gần nhất (FR-DASH-001), phiên cần xử lý (FR-DASH-002).
- Chọn thẻ mở đúng màn hình đã lọc: phiên → SCR-04, file lỗi → SCR-05 với bộ lọc lỗi, đăng nhập → SCR-01 (FR-DASH-003).
- Hành động nhanh: `Tải hóa đơn` (ẩn/khóa theo entitlement), `Tiếp tục` phiên dở (FR-JOB-010).
- Tải số liệu nền, hiển thị khung tải; không treo UI. Trong `ReadOnlyServiceMode` hiển thị nhãn `Chỉ đọc – cần gia hạn licence`, khóa hành động tải/đăng nhập.

## 4. Lịch sử (SCR-08)

```csharp
public interface IDownloadHistoryService
{
    Task<HistoryPage> QueryAsync(HistoryQuery query, CancellationToken ct);   // lọc loại, trạng thái, khoảng thời gian
    Task<SessionDetail> GetAsync(SessionId id, CancellationToken ct);          // lỗi theo đoạn + hành động đề xuất
}
```

- Cột: thời điểm, loại, phạm vi, trạng thái, số hóa đơn mới/cập nhật/trùng/lỗi, người dùng Windows khởi tạo (FR-HIST-001).
- Mở phiên → SCR-04 chế độ chi tiết (FR-HIST-002); lỗi theo tháng + hành động đề xuất (FR-HIST-003); `Mở Kho hóa đơn` với bộ lọc của phiên (FR-HIST-004).
- Phiên chưa hoàn tất có hành động `Tiếp tục` (FR-JOB-010, AC-07).
- Không xóa tự động lịch sử (FR-HIST-005, BR-013).
- `Tạo phiên tải mới` → SCR-03.

## 5. Kiểm thử

### 5.1 Unit

- Dựng `DashboardSnapshot` từ dữ liệu giả; trạng thái đăng nhập không chứa token.
- Lọc/sắp xếp lịch sử; ánh xạ phiên → `ArchiveQuery` khởi tạo.
- Nhãn và khóa hành động theo `AppMode`.

### 5.2 Integration / UI smoke

- Đóng ứng dụng giữa phiên → mở lại hiện phiên chưa hoàn tất với `Tiếp tục` (AC-07).
- Điều hướng từ thẻ đến đúng màn hình đã lọc.

## 6. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 6.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T10-01 | `IDashboardService`, `DashboardSnapshot`, truy vấn số liệu | Application/Infra | T05-02, T06-03 | ✅ | | IDashboardService + DashboardSnapshot (DashboardService): số liệu kho, phiên đang chạy, việc cần xử lý, hóa đơn mới tải; không chứa token (trạng thái đăng nhập do thanh đầu trang hiển thị) |
| T10-02 | `DashboardControl` + presenter (SCR-02) | WinForms | T10-01, T01-05 | ✅ | | DashboardControl + DashboardPresenter (SCR-02): 4 thẻ, phiên đang chạy, việc cần xử lý, hóa đơn mới tải, khóa nút tải theo quyền (ReadOnlyService) |
| T10-03 | `IDownloadHistoryService`, `HistoryQuery` | Application/Infra | T05-02 | ✅ | | IDownloadHistoryService + HistoryQuery; IDownloadRepository.GetSessionsAsync; mã phiên DL-yyyy-MMdd-NNN suy ra từ ngày tạo |
| T10-04 | `DownloadHistoryControl` + presenter (SCR-08) | WinForms | T10-03 | ✅ | | HistoryControl + HistoryPresenter (SCR-08): lọc loại/trạng thái/ngày/tìm nhanh, thẻ tóm tắt, phân trang, mở phiên sang SCR-04, Tạo phiên mới |
| T10-05 | Tái sử dụng SCR-04 ở chế độ chi tiết lịch sử | WinForms | T05-08 | ✅ | | 04/11/2026: Lịch sử mở phiên bằng chính SCR-04 (lỗi theo tháng, Thử lại các lỗi, Tiếp tục); nút Mở Kho hóa đơn của phiên xong mở kho đúng chiều và đúng năm của kỳ tải (kỳ nằm gọn trong một năm; kỳ trải qua nhiều năm thì xem mọi năm; năm không có hóa đơn thì mọi năm). Bộ lọc theo từng ngày của kỳ chưa có vì SCR-05 chỉ lọc theo năm |
| T10-06 | Điều hướng có bộ lọc sang SCR-05 | WinForms | T07-04 | ✅ | | ArchiveNavigationRequest: thẻ file thiếu mở SCR-05 đúng chiều với bộ lọc lỗi/thiếu file, mọi năm; thẻ phiên lỗi mở SCR-04 |
| T10-07 | Unit + UI smoke test (mục 5) | Tests | T10-02, T10-04 | ✅ | | 14 test service lịch sử, 17 test presenter lịch sử, 12 test service Tổng quan, 17 test presenter Tổng quan, 3 smoke |

### 6.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 7. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
