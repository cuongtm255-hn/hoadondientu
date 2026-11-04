# DD-09 — Xuất danh sách Excel và ZIP XML

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-09 |
| Tên | Xuất Excel theo mẫu cổng Thuế, ZIP XML kèm manifest, xử lý file lỗi, ghi đè và hủy |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review; gate G-04 đóng ([ADR 0002](../decisions/0002-excel-export-fidelity.md)), Excel/ZIP chốt ([ADR 0003](../decisions/0003-xlsx-and-zip-generation.md)) |
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

- Bố cục (mẫu thật `DANH SÁCH HÓA ĐƠN.xlsx`, [api-samples mục 5](../api-samples/README.md)): một sheet; `A3:S3` gộp ô **"DANH SÁCH HÓA ĐƠN"**; `A4:S4` gộp ô **"Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy"**; dòng 6 tiêu đề; dữ liệu từ dòng 7; đúng **19 cột** A–S (FR-EXPORT-001, AC-08):
  `STT · Ký hiệu mẫu số · Ký hiệu hóa đơn · Số hóa đơn · Ngày lập · MST người bán/MST người xuất hàng · Tên người bán/Tên người xuất hàng · MST người mua/MST người nhận hàng · Tên người mua/Tên người nhận hàng · Địa chỉ người mua · Tổng tiền chưa thuế · Tổng tiền thuế · Tổng tiền chiết khấu thương mại · Tổng tiền phí · Tổng tiền thanh toán · Đơn vị tiền tệ · Tỷ giá · Trạng thái hóa đơn · Kết quả kiểm tra hóa đơn`.
- Nguồn từng cột: `khmshdon, khhdon, shdon, tdlap (dd/MM/yyyy, giờ VN), nbmst, nbten, nmmst (cá nhân: nmcmnd), nmten, nmdchi, tgtcthue, tgtthue, ttcktmai, tgtphi, tgtttbso, dvtte, tgia`; hai cột cuối là **nhãn** của `tthai` ("Hóa đơn mới"…) và `ttxly` ("Đã cấp mã hóa đơn"…) từ `PortalOptions`. Cột M/N để trống khi không có giá trị. Độ rộng cột theo mẫu (A≈8, B–D≈11,7, E≈19,5, F–J≈31,3, K–L≈11,7, M–O≈19,5, P–Q≈11,7, R≈19,5, S≈50,8).
- Khác với cổng: ứng dụng xuất cả khoảng nhiều tháng (kỳ tháng/quý/năm/tùy chọn) từ dữ liệu đã lưu, không bị giới hạn một tháng như API `export-excel` của cổng.
- Kiểu dữ liệu: số tiền là numeric; ngày là kiểu ngày sắp xếp được; ký hiệu mẫu số/ký hiệu/số hóa đơn là text để giữ số 0 đầu (FR-EXPORT-002).
- Ghi rõ phạm vi (toàn bộ kết quả hay các dòng chọn) trong file (FR-EXPORT-003).
- STT là số thứ tự hiển thị trong file xuất (BR-015).
- Dữ liệu được đọc theo lô từ repository (stream), không nạp toàn bộ vào bộ nhớ.

## 4. Xuất ZIP XML

- ZIP chứa XML của **phiên bản hiện hành** của các hóa đơn được chọn (FR-EXPORT-004). Cổng xuất mỗi hóa đơn thành một zip gồm `invoice.xml` + trình xem (`invoice.html`, `details.js`, `sign-check.jpg`, `viewinvoice-bg.jpg`). Từ 04/11/2026 (yêu cầu của người dùng) ứng dụng cũng đóng gói đủ năm file này: **một hóa đơn** thì zip chứa đúng năm file như gói của cổng, không thư mục, không manifest; **nhiều hóa đơn** thì mỗi hóa đơn một thư mục (tên theo file XML trong kho) chứa năm file, kèm `manifest.csv` ở gốc. `invoice.xml` luôn là byte nguyên văn (giữ chữ ký số); `invoice.html` do ứng dụng dựng từ XML trên đúng CSS của cổng (`InvoiceViewerPackage`), `details.js` và hai ảnh là tài sản của cổng nhúng nguyên bản. XML không đọc được thành hóa đơn thì chỉ có `invoice.xml`.
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
| T09-01 | `ExportScope`, `ExportFileNamer`, `ExportRecord` | Application | T07-01 | ✅ | | ExportScope, ExportRequest/Result, ExportFileNamer xong; 04/11/2026: `ExportRecord` + `IExportHistoryRepository` (bộ nhớ và `SqliteExportHistoryRepository`, bảng `export_record`, migration 4): mỗi lần xuất xong ghi loại, nơi lưu, phạm vi, số hóa đơn đã xuất/bỏ qua (tối đa 50 dòng lý do), giờ UTC; xuất bị hủy, lỗi hay chờ quyết định không ghi; lỗi khi ghi lịch sử không làm hỏng lần xuất đã xong. Chưa có màn hình xem lịch sử xuất |
| T09-02 | Đối chiếu mẫu Excel của cổng (cột, kiểu, tiêu đề) | Docs/Test | G-04 | ✅ | | Đã đối chiếu mẫu: bố cục, ô gộp, 19 cột, kiểu dữ liệu, độ rộng cột ghi ở mục 3 và api-samples mục 5. So sánh file do ứng dụng xuất với mẫu thực hiện cùng T09-03 |
| T09-03 | `ExcelInvoiceWriter` (stream, 19 cột) | Infra | G-09, T09-02 | ✅ | [ADR 0002](../decisions/0002-excel-export-fidelity.md), [ADR 0003](../decisions/0003-xlsx-and-zip-generation.md) | InvoiceExcelWriter tự dựng SpreadsheetML bằng System.IO.Compression (không cần thư viện ngoài, giải G-09 phần Excel): ghi từng dòng ra luồng, 19 cột đúng mẫu cổng, số 0 đầu giữ dạng chữ, tiền/ngày dạng số. Đã mở thử bằng Excel thật: hiển thị đúng. Đặt ở Application thay vì Infra |
| T09-04 | `ZipInvoiceWriter` + manifest + chính sách file lỗi | Infra | T06-05 | ✅ | | 04/11/2026: gói năm file như cổng (một hóa đơn: phẳng, không manifest; nhiều hóa đơn: thư mục theo hóa đơn + manifest.csv). Đóng gói trong InvoiceExportService: chỉ phiên bản hiện hành, nguyên byte, kiểm SHA-256 từng file, manifest.csv (UTF-8 BOM, chống công thức), chính sách Ask/SkipAndRecord/Abort |
| T09-05 | `InvoiceExportService` (tiến độ, hủy, atomic, không ghi đè) | Application | T09-03, T09-04 | ✅ | | InvoiceExportService: tiến độ từng hóa đơn, hủy xóa file tạm, ghi tạm rồi đổi tên, không ghi đè khi chưa xác nhận, kiểm quyền ReadExport; ghi ExportRecord sau khi xuất xong |
| T09-06 | Hộp thoại xuất + tích hợp SCR-05/06/07 | WinForms | T09-05, T07-04 | 🟡 | | SCR-05: nút Xuất Excel / Xuất ZIP XML; có dòng chọn thì chỉ xuất các dòng đó, không thì toàn bộ kết quả đang lọc; hộp thoại lưu nêu tên và MST; hỏi xuất phần hợp lệ khi có file lỗi. Chưa có xuất ZIP một hóa đơn ở SCR-06/07 |
| T09-07 | Unit + integration test (mục 6) | Tests | T09-05 | 🟡 | | 30 test xuất (Excel đọc lại được từng ô, ZIP đối chiếu SHA-256 với manifest, ghi đè, hủy, quyền) + 8 test UI; còn test tập 100.000 dòng và file bị khóa/hết dung lượng |

### 7.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 8. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
