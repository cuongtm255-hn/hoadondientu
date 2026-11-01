# DD-09 — Xuất danh sách Excel và ZIP XML

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-09 |
| Tên | Xuất Excel theo mẫu cổng Thuế, ZIP XML kèm manifest, xử lý file lỗi, ghi đè và hủy |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review; thư viện/mẫu Excel chờ gate G-04, G-09 |
| Feature | [FT-10](../requirements/FEATURE-LIST.md#ft-10--xuất-excel-và-zip-xml) |
| SRS | FR-EXPORT-001..008, FR-ARCH-008, mục 9.1, NFR-PERF-005, AC-08, AC-09 |
| Màn hình | Hộp thoại/hành động trong SCR-05, SCR-06, SCR-07 |
| Wireframe | [SCR-05](../wireframes/index.html?screen=SCR-05) |
| Phụ thuộc | [DD-00](DD-00-TECHNICAL-GATES.md) (G-04, G-09), [DD-06](DD-06-INVOICE-STORAGE.md), [DD-07](DD-07-INVOICE-ARCHIVE.md) |
| Phase triển khai | [Phase 5](../plans/PROJECT-PLAN.md#phase-5--xuất-dữ-liệu-tổng-quan-lịch-sử-cài-đặt-và-sao-lưu) |

## 2. Mục đích và phạm vi

Tạo file Excel danh sách hóa đơn và ZIP chứa XML hiện hành từ tập kết quả hoặc các dòng đã chọn. Hoạt động ở `ReadOnlyServiceMode`. Xuất PDF từng hóa đơn thuộc DD-08.

## 3. Xuất Excel

```csharp
public interface IInvoiceExportService
{
    Task<ExportResult> ExportExcelAsync(ExportScope scope, string targetPath, IProgress<ExportProgress> p, CancellationToken ct);
    Task<ExportResult> ExportZipAsync(ExportScope scope, string targetPath, ZipFailurePolicy onMissing, IProgress<ExportProgress> p, CancellationToken ct);
}

public sealed class ExportScope      // tập kết quả hiện tại (ArchiveQuery) hoặc danh sách InvoiceKey đã chọn
{
    public ArchiveQuery Query { get; }
    public IList<InvoiceKey> SelectedKeys { get; }
    public string DescriptionForHeader { get; }   // phạm vi ngày ghi trong file
}
```

- Bố cục: tiêu đề, phạm vi ngày, đúng **19 cột** theo thứ tự mục 9.1 SRS (FR-EXPORT-001, AC-08); so với `DANH SÁCH HÓA ĐƠN.xlsx` (gate G-04).
- Kiểu dữ liệu: số tiền là numeric; ngày là kiểu ngày sắp xếp được; ký hiệu mẫu số/ký hiệu/số hóa đơn là text để giữ số 0 đầu (FR-EXPORT-002).
- Ghi rõ phạm vi (toàn bộ kết quả hay các dòng chọn) trong file (FR-EXPORT-003).
- STT là số thứ tự hiển thị trong file xuất (BR-015).
- Dữ liệu được đọc theo lô từ repository (stream), không nạp toàn bộ vào bộ nhớ.

## 4. Xuất ZIP XML

- ZIP chứa XML của **phiên bản hiện hành** của các hóa đơn được chọn (FR-EXPORT-004).
- Kèm `manifest.json`/`manifest.csv`: khóa hóa đơn, tên file, SHA-256, trạng thái thành công/lỗi (FR-EXPORT-005).
- Nếu có XML thiếu/lỗi: hỏi người dùng `Xuất phần hợp lệ` hay `Hủy`; manifest ghi rõ các dòng bị bỏ qua (FR-EXPORT-006).
- Có thể kiểm tra lại hash của từng file khi đóng gói để bảo đảm không đóng gói file hỏng.
- `ZipFailurePolicy`: `Ask` (mặc định), `SkipAndRecord`, `Abort`.

## 5. Quy tắc chung

- Tên file: `<loại>_<phạm-vi>_<yyyyMMdd-HHmmss>.<xlsx|zip>` (FR-EXPORT-007).
- Không ghi đè file đã tồn tại nếu chưa có xác nhận (FR-EXPORT-008); ghi tạm rồi đổi tên atomic.
- Chạy nền, có tiến độ và nút hủy an toàn; hủy xóa file tạm (NFR-PERF-005, NFR-PERF-001).
- Ghi `ExportRecord` (phạm vi, đường dẫn, manifest, thời điểm) trong `company.db`; thư mục mặc định `Exports\` của MST.
- Hộp thoại xuất nêu tên và MST doanh nghiệp (NFR-USE-007).
- Entitlement `ReadExport` được kiểm tra ở service.

## 6. Kiểm thử

### 6.1 Unit

- `ExportFileNamer`; manifest builder; ánh xạ cột 19 trường → ô.
- Chính sách `ZipFailurePolicy` với XML thiếu/lỗi.

### 6.2 Integration

- Excel với dữ liệu tiếng Việt, số 0 đầu, ngoại tệ; mở lại bằng thư viện đọc để kiểm tra kiểu ô.
- ZIP: giải nén, đối chiếu SHA-256 với manifest (AC-09).
- Ghi đè cần xác nhận; hủy giữa chừng không để lại file dở; file bị khóa/hết dung lượng báo lỗi rõ.
- Tập 100.000 dòng không tăng bộ nhớ tuyến tính.

## 7. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 7.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T09-01 | `ExportScope`, `ExportFileNamer`, `ExportRecord` | Application | T07-01 | ⬜ | | |
| T09-02 | Đối chiếu mẫu Excel của cổng (cột, kiểu, tiêu đề) | Docs/Test | G-04 | ⛔ | | Gate G-04 |
| T09-03 | `ExcelInvoiceWriter` (stream, 19 cột) | Infra | G-09, T09-02 | ⛔ | | Chờ chọn thư viện |
| T09-04 | `ZipInvoiceWriter` + manifest + chính sách file lỗi | Infra | T06-05 | ⬜ | | |
| T09-05 | `InvoiceExportService` (tiến độ, hủy, atomic, không ghi đè) | Application | T09-03, T09-04 | ⬜ | | |
| T09-06 | Hộp thoại xuất + tích hợp SCR-05/06/07 | WinForms | T09-05, T07-04 | ⬜ | | |
| T09-07 | Unit + integration test (mục 6) | Tests | T09-05 | ⬜ | | AC-08, AC-09 |

### 7.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 8. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
