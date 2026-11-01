# DD-11 — Cài đặt, sao lưu và khôi phục (SCR-09)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-11 |
| Tên | Thông tin kho, dung lượng, kiểm tra kết nối, sao lưu/khôi phục độc lập theo MST |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review |
| Feature | [FT-12](../requirements/FEATURE-LIST.md#ft-12--cài-đặt-sao-lưu-và-khôi-phục) |
| SRS | FR-SET-004..009, FR-LIC-001, NFR-SEC-004, NFR-REL-003, NFR-MNT-003, NFR-USE-007, AC-11 |
| Màn hình | SCR-09 (+ hộp thoại sao lưu/khôi phục) |
| Wireframe | [SCR-09](../wireframes/index.html?screen=SCR-09) · [PNG](../wireframes/png/SCR-09-settings.png) |
| Phụ thuộc | [DD-02](DD-02-COMPANY-MANAGEMENT.md), [DD-03](DD-03-LICENSING.md) (mục Bản quyền), [DD-06](DD-06-INVOICE-STORAGE.md) |
| Phase triển khai | [Phase 5](../plans/PROJECT-PLAN.md#phase-5--xuất-dữ-liệu-tổng-quan-lịch-sử-cài-đặt-và-sao-lưu) |

## 2. Mục đích và phạm vi

Quản lý cấu hình của doanh nghiệp đang hoạt động, theo dõi dung lượng và bảo vệ dữ liệu bằng sao lưu/khôi phục. Mỗi thư mục `<MST>` là một đơn vị sao lưu, khôi phục và bàn giao độc lập. Sao lưu và khôi phục của MST khác phải chuyển MST trước; không có sao lưu xuyên MST.

Việc khai báo/chọn doanh nghiệp thuộc DD-02; licence thuộc DD-03 (SCR-09 chỉ có lối vào `Bản quyền`).

## 3. Màn hình SCR-09

- Thông tin doanh nghiệp đang hoạt động (tên, MST khóa nếu kho đã có dữ liệu — FR-SET-002).
- Thư mục kho: kiểm tra quyền đọc/ghi và dung lượng trước khi lưu (FR-SET-003); `Mở thư mục kho`; `Kiểm tra kết nối` (cổng Thuế/licence) (FR-SET-009).
- Dung lượng: database, XML, PDF, bản xuất (FR-SET-004), tính nền.
- Sao lưu/khôi phục (mục 4, 5).
- Lối vào `Bản quyền` → SCR-12 (luôn hiển thị).
- `ReadOnlyServiceMode`: sao lưu cho phép; sửa cấu hình quan trọng bị khóa ở UI và service.

## 4. Sao lưu

```csharp
public interface IBackupService
{
    Task<BackupResult> CreateAsync(string targetFolder, IProgress<BackupProgress> p, CancellationToken ct);
    Task<BackupManifest> InspectAsync(string backupPath, CancellationToken ct);
    Task<RestoreResult> RestoreAsync(string backupPath, RestoreOptions options, IProgress<BackupProgress> p, CancellationToken ct);
}
```

Gói sao lưu: `company.db` (bản chụp nhất quán), XML, PDF đã tạo, `manifest.json` (MST, schema version, ngày tạo, danh sách file + SHA-256) (FR-SET-006).

Tạo gói:

1. Chỉ chạy khi không có transaction ghi/phiên tải đang chạy (hoặc yêu cầu tạm dừng) (FR-SET-005).
2. `PRAGMA integrity_check`; dùng backup API của SQLite hoặc checkpoint WAL rồi sao chép nhất quán (NFR-REL-003).
3. Ghi vào tệp tạm, tính hash, ghi manifest, đổi tên atomic.

## 5. Khôi phục

1. Đọc manifest; kiểm tra **MST khớp kho hiện hành**, schema tương thích, SHA-256 từng file **trước khi** thay đổi dữ liệu (FR-SET-007, NFR-SEC-004). Gói khác MST bị từ chối (AC-11).
2. Tạo điểm sao lưu an toàn của dữ liệu hiện hành hoặc yêu cầu vị trí (FR-SET-008).
3. Đóng `CompanyContext`, thay dữ liệu vào thư mục tạm cùng volume, hoán đổi, mở lại và chạy `integrity_check`; lỗi → phục hồi điểm an toàn.
4. Hộp thoại nêu rõ tên và MST bị tác động (NFR-USE-007).
5. Schema cũ hơn được migrate sau khi khôi phục (NFR-MNT-003); schema mới hơn ứng dụng bị từ chối.

## 6. Kiểm thử

### 6.1 Unit

- Manifest builder/validator; so khớp MST; phiên bản schema.
- Kiểm tra từ chối gói khác MST, hash sai, thiếu file, schema mới hơn.

### 6.2 Integration

- Sao lưu → khôi phục trên kho cùng MST: dữ liệu giống nhau (AC-11).
- Khôi phục lỗi giữa chừng → trở về điểm an toàn.
- Sao lưu khi có phiên tải đang chạy bị chặn/yêu cầu tạm dừng.
- Hết dung lượng, đường dẫn dài, file khóa.

### 6.3 UI smoke

- SCR-09 hiển thị dung lượng; mở thư mục; vào SCR-12.

## 7. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 7.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T11-01 | `SettingsControl` + presenter (SCR-09): thông tin kho, đổi thư mục, mở thư mục | WinForms | T02-03, T01-05 | ⬜ | | |
| T11-02 | `IStorageStatisticsService` (dung lượng DB/XML/PDF/Exports) | Application/Infra | T06-03 | ⬜ | | |
| T11-03 | Kiểm tra kết nối (cổng Thuế, licence) | Application | T01-09 | ⬜ | | |
| T11-04 | `BackupManifest` + `IBackupService.CreateAsync` | Application/Infra | T06-02 | ⬜ | | |
| T11-05 | `RestoreAsync` + điểm sao lưu an toàn + rollback | Application/Infra | T11-04, T01-07 | ⬜ | | |
| T11-06 | Hộp thoại sao lưu/khôi phục (tên + MST, tiến độ, hủy) | WinForms | T11-05 | ⬜ | | |
| T11-07 | Lối vào `Bản quyền` trong SCR-09 | WinForms | T03-06 | ⬜ | | |
| T11-08 | Unit + integration + UI smoke (mục 6) | Tests | T11-05 | ⬜ | | AC-11 |

### 7.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 8. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
