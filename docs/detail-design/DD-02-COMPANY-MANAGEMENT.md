# DD-02 — Quản lý doanh nghiệp, danh mục MST và khai báo kho

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-02 |
| Tên | Khai báo doanh nghiệp, danh mục `companies.json`, thêm/sửa/ngừng quản lý, liên kết lại kho |
| Phiên bản | 1.0 (tách từ DD-HDDT-APP-SHELL-LOGIN-001 v1.0, mục 10, 11, 13) |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế đã phê duyệt — chờ review tài liệu |
| Feature | [FT-03](../requirements/FEATURE-LIST.md#ft-03--quản-lý-doanh-nghiệp-và-chuyển-mst) |
| SRS | BR-001, BR-016..BR-023, FR-SET-001..003, FR-COMP-001..014, NFR-REL-006..007, NFR-PERF-006..007, AC-12..AC-15, AC-17 |
| Màn hình | SCR-10 (Quản lý doanh nghiệp), SCR-11 (Thêm/sửa doanh nghiệp), form khai báo lần đầu (StandardMode) |
| Wireframe | [SCR-10](../wireframes/index.html?screen=SCR-10) · [SCR-11](../wireframes/index.html?screen=SCR-11) |
| Phụ thuộc | [DD-01](DD-01-APP-SHELL-LOGIN.md) (`CompanyContext`, `CompanySwitchService`, shell), [DD-03](DD-03-LICENSING.md) (entitlement), [DD-06](DD-06-INVOICE-STORAGE.md) (schema `company.db`) |
| Phase triển khai | [Phase 2](../plans/PROJECT-PLAN.md#phase-2--khung-chương-trình-doanh-nghiệp-licence-và-đăng-nhập) |

## 2. Mục đích và phạm vi

### 2.1 Mục đích

Mô tả giao diện và quy trình khai báo, liệt kê, thêm, sửa, ngừng quản lý và liên kết lại doanh nghiệp (MST), dựa trên `companies.json` làm danh mục định tuyến. Cơ chế `CompanyContext`, `CompanySwitchService` và `StorageGuard` được định nghĩa ở [DD-01 mục 9](DD-01-APP-SHELL-LOGIN.md#9-companycontext-và-chuyển-mst); tài liệu này chỉ dùng lại.

### 2.2 Trong phạm vi

- Khai báo doanh nghiệp duy nhất của StandardMode lần chạy đầu (FR-SET-001..003).
- `CompanyListControl` (SCR-10): danh sách, tìm kiếm, “Làm việc”, mở thư mục, sao lưu (lối vào), ngừng quản lý.
- `CompanyEditControl` (SCR-11): thêm/sửa hồ sơ, ghi hai pha.
- `ICompanyCatalog`, `JsonCompanyCatalog`, `CompanyStorageValidator`, cache tổng quan.
- Liên kết lại thư mục kho bị mất và đăng ký lại hồ sơ đã ngừng quản lý (FR-COMP-012, AC-17).
- Nâng cấp kho StandardMode thành doanh nghiệp đầu tiên khi bật licence (FR-COMP-013, AC-13).

### 2.3 Ngoài phạm vi

- Báo cáo/tìm kiếm/xuất dữ liệu tổng hợp xuyên MST (FR-COMP-014).
- Xóa vật lý thư mục dữ liệu doanh nghiệp (quy trình riêng ngoài SCR-10).
- Cơ chế sao lưu/khôi phục chi tiết — xem [DD-11](DD-11-SETTINGS-BACKUP.md).

## 3. Khai báo doanh nghiệp StandardMode lần đầu

### 3.1 Giao diện

`CompanySetupControl` hiển thị trong `MainShellForm`; menu nghiệp vụ bị ẩn.

Trường bắt buộc:

- MST.
- Tên hiển thị.
- Thư mục kho.

### 3.2 Quy trình

```text
Nhập dữ liệu
  → chuẩn hóa MST
  → kiểm tra thư mục/quyền ghi
  → kiểm tra database hiện hữu
  → tạo cấu trúc/database tạm
  → xác minh schema/MST
  → commit cấu hình
  → tạo CompanyContext
  → mở PortalLoginForm
```

Nếu commit thất bại, không đánh dấu hồ sơ hoàn tất và không ghi đè dữ liệu đã tồn tại.

## 4. Danh sách doanh nghiệp ServiceMode

### 4.1 `CompanyListControl`

Danh sách chỉ đọc `companies.json` và cache tổng quan:

```csharp
public sealed class CompanyCatalogItem
{
    public CompanyId Id { get; private set; }
    public TaxCode TaxCode { get; private set; }
    public string DisplayName { get; private set; }
    public string StoragePath { get; private set; }
    public DateTime? LastOpenedAt { get; private set; }
    public DateTime? LastDownloadedAt { get; private set; }
    public long CachedInvoiceCount { get; private set; }
    public long CachedStorageBytes { get; private set; }
    public CompanyCatalogStatus Status { get; private set; }
}
```

Không mở SQLite của nhiều MST để dựng danh sách.

### 4.2 Chọn MST

```text
Chọn “Làm việc”
  → kiểm tra entitlement
  → đánh giá tác vụ của context hiện tại
  → chờ/pause/cancel theo lựa chọn người dùng
  → xóa PortalSession
  → đóng SQLite/file handle cũ
  → xác minh kho mới
  → mở CompanyContext mới
  → cập nhật header
  → mở PortalLoginForm
```

Trong ReadOnlyServiceMode, bước cuối là Dashboard/Kho hóa đơn; không mở form đăng nhập.

## 5. Thêm/sửa doanh nghiệp ServiceMode

`CompanyEditControl` ghi theo hai pha:

```text
Pha 1
  → validate input
  → chuẩn bị thư mục/database tạm
  → xác minh schema/MST/quyền

Pha 2
  → ghi companies.json.tmp
  → đọc và xác minh lại
  → replace atomic companies.json
  → đổi tài nguyên tạm thành chính thức
```

MST đã có dữ liệu không được sửa. Muốn quản lý MST khác phải tạo hồ sơ/kho khác.

“Ngừng quản lý” chỉ bỏ liên kết catalog, không xóa thư mục.

## 6. Định dạng `companies.json` và quy tắc cache

```json
{
  "schemaVersion": 1,
  "companies": [
    {
      "id": "7d3e…",
      "taxCode": "0100000000",
      "displayName": "Công ty ABC",
      "storagePath": "Companies\\0100000000",
      "lastOpenedAt": "2026-11-01T09:00:00Z",
      "lastDownloadedAt": "2026-10-31T16:20:00Z",
      "cachedInvoiceCount": 1250,
      "cachedStorageBytes": 734003200,
      "status": "Active"
    }
  ]
}
```

- Chỉ chứa thông tin định tuyến và số liệu tổng quan lưu đệm (BR-021). Không chứa hóa đơn, token, licence key.
- Cache được cập nhật khi đóng/chuyển kho, không mở SQLite của nhiều MST để dựng danh sách (NFR-PERF-006).
- Mọi ghi dùng `AtomicJsonWriter` (DD-01 mục 16) và giữ `companies.json.bak`.
- `companies.json` mất/hỏng: không tạo kho rỗng; cho phép quét thư mục `Companies\` và liên kết lại (NFR-REL-007).
- `CompanyCatalogStatus`: `Active`, `PathMissing`, `SchemaMismatch`, `Unlinked`.

## 7. Các luồng bổ sung

### 7.1 Nâng cấp StandardMode → ServiceMode (FR-COMP-013)

1. Sau khi kích hoạt licence thành công ([DD-03](DD-03-LICENSING.md)), `CompanyCatalog` đăng ký hồ sơ StandardMode hiện hữu làm doanh nghiệp đầu tiên.
2. Không di chuyển, không sao chép, không tải lại dữ liệu; chỉ ghi thêm `companies.json` nếu chưa có.
3. Điều hướng tới SCR-10 để người dùng chủ động chọn MST.

### 7.2 Ngừng quản lý và liên kết lại (FR-COMP-010..012, AC-17)

- Ngừng quản lý: xác nhận hộp thoại nêu tên + MST; chỉ gỡ bản ghi khỏi catalog; không xóa thư mục.
- Liên kết lại: chọn thư mục → `CompanyStorageValidator` kiểm tra `company.db` tồn tại, MST khớp, schema tương thích, toàn vẹn → thêm lại vào catalog.
- Trùng MST trong catalog: không tạo kho mới, mở hồ sơ đã tồn tại hoặc cho sửa đường dẫn.

### 7.3 ReadOnlyServiceMode

Cho phép “Làm việc”, mở thư mục, sao lưu; khóa “Thêm doanh nghiệp”, sửa cấu hình quan trọng; nhãn `Chỉ đọc – cần gia hạn licence`. Kiểm tra entitlement ở application service, không chỉ ẩn control.

## 8. Kiểm thử

### 8.1 Unit

- Chuẩn hóa/validate MST; trùng MST bị từ chối.
- Quy trình hai pha: lỗi pha 2 không để hồ sơ nửa vời.
- Catalog đọc/ghi atomic, phục hồi từ `.bak`, phát hiện catalog hỏng.
- Entitlement: Standard từ chối thêm/chuyển MST khi gọi ngoài UI (AC-12).
- Sửa MST của kho đã có dữ liệu bị từ chối.

### 8.2 Integration

- Hai MST, hai SQLite độc lập; danh sách không mở nhiều SQLite.
- Nâng cấp kho Standard thành hồ sơ đầu tiên không di chuyển dữ liệu.
- Đường dẫn kho mất → trạng thái `PathMissing`, relink thành công khi MST/schema khớp.
- Ngừng quản lý rồi liên kết lại giữ nguyên dữ liệu.

### 8.3 UI smoke

- Service khởi động tại SCR-10; chọn MST mở đăng nhập đúng tên/MST.
- Standard không hiển thị lối vào SCR-10/11.

## 9. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 9.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T02-01 | `CompanyCatalogItem`, `CompanyCatalogStatus`, `ICompanyCatalog` | Application | T01-03 | ✅ | | Application/Companies/CompanyCatalogItem.cs |
| T02-02 | `JsonCompanyCatalog` + `AtomicJsonWriter` tích hợp, `.bak` recovery | Infra | T01-11 | ✅ | | JsonCompanyCatalog, AtomicJsonWriter, recovery .bak/.corrupt |
| T02-03 | `CompanyStorageValidator` (quyền ghi, dung lượng, MST, schema) | Infra | T02-01 | 🟡 | | FileSystemCompanyStorage + CompanyManagementService.ValidateAsync; chưa có schema DB (chờ DD-06) |
| T02-04 | `CompanySetupControl` + presenter (StandardMode first-run) | WinForms | T01-05, T02-03 | ✅ | | First-run dùng lại CompanyEditControl (mode FirstRun), không tách CompanySetupControl |
| T02-05 | `CompanyListControl` + presenter (SCR-10) + cache tổng quan | WinForms | T02-02, T01-07 | ✅ | | CompanyListControl/Presenter; cache tổng quan hiển thị từ catalog |
| T02-06 | `CompanyEditControl` + presenter (SCR-11), ghi hai pha | WinForms | T02-03 | ✅ | | CompanyEditControl/Presenter, tạo hai pha + rollback |
| T02-07 | Ngừng quản lý, relink, quét kho (recovery) | App/WinForms | T02-03 | ✅ | 06/11/2026 | Unlink/relink; khôi phục danh mục: `CompanyRecoveryService` quét thư mục dữ liệu (và thư mục con trực tiếp) tìm kho có file định danh hợp lệ bằng đọc-chỉ (`ICompanyStorageScanner`, không ghi file thử vào thư mục lạ), đánh dấu kho đã có trong danh mục, trùng MST hoặc định danh hỏng; `RebuildAsync` đăng ký các kho được chọn, danh mục hỏng được đổi tên thành `companies.json.corrupt-<thời gian>` để giữ lại (`ICompanyCatalogRepair`). Màn hình "Cần khôi phục" thật thay placeholder: quét tự động, chọn thư mục khác, tick kho, đăng ký, khởi động lại. Tên hiển thị đặt tạm theo MST. 17 test + 9 test presenter/control |
| T02-08 | Nâng cấp Standard → Service đăng ký kho đầu tiên | Application | T03-05 | ⬜ | | Chờ DD-03 |
| T02-09 | Unit + integration test (mục 8.1–8.2) | Tests | T02-02..T02-08 | 🟡 | | Unit/integration/presenter test xong, gồm quét kho khôi phục (06/11/2026); thiếu phần nâng cấp Standard→Service (T02-08, chờ DD-03) |
| T02-10 | UI smoke test (mục 8.3) | Tests | T02-05 | ✅ | | 8 UI smoke test trong ShellUiSmokeTests |

### 9.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tách tài liệu từ DD-01; chưa có code | Claude Code |

## 10. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Tách từ DD-01 v1.0 (mục 10, 11, 13); bổ sung định dạng `companies.json`, luồng nâng cấp/relink, kiểm thử, theo dõi tiến độ |
