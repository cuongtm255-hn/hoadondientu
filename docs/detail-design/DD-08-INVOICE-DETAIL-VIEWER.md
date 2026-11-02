# DD-08 — Chi tiết hóa đơn, trình xem XML/PDF và in (SCR-06, SCR-07)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-08 |
| Tên | Chi tiết metadata/phiên bản, trình xem XML an toàn, tạo PDF phát sinh, in và lưu bản sao |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review; chiến lược PDF chờ gate G-03, G-09 |
| Feature | [FT-09](../requirements/FEATURE-LIST.md#ft-09--chi-tiết-hóa-đơn-trình-xem-xmlpdf-và-in) |
| SRS | FR-DETAIL-001..007, BR-008, BR-014, NFR-SEC-003, AC-10 |
| Màn hình | SCR-06, SCR-07 |
| Wireframe | [SCR-06](../wireframes/index.html?screen=SCR-06) · [SCR-07](../wireframes/index.html?screen=SCR-07) · [PNG 06](../wireframes/png/SCR-06-detail.png) · [PNG 07](../wireframes/png/SCR-07-viewer.png) |
| Phụ thuộc | [DD-00](DD-00-TECHNICAL-GATES.md) (G-03, G-09), [DD-06](DD-06-INVOICE-STORAGE.md), [DD-07](DD-07-INVOICE-ARCHIVE.md) |
| Phase triển khai | [Phase 4](../plans/PROJECT-PLAN.md#phase-4--kho-hóa-đơn-chi-tiết-và-trình-xem) |

## 2. Mục đích và phạm vi

Cho người dùng kiểm tra một hóa đơn: metadata, trạng thái file, lịch sử phiên bản, nguồn phiên tải; xem XML hiện hành chỉ đọc; xem/in/lưu PDF. XML là chứng từ gốc; PDF chỉ là bản trình bày phát sinh, có thể tạo lại (BR-014). Không sửa nội dung XML.

## 3. Chi tiết hóa đơn (SCR-06)

```csharp
public interface IInvoiceDetailService
{
    Task<InvoiceDetail> GetAsync(InvoiceKey key, CancellationToken ct);   // metadata, versions, file states, source session
}
```

- Hiển thị toàn bộ metadata (19 trường + ID cổng + thời điểm thấy đầu/cuối), trạng thái file (XML/PDF), nguồn phiên tải, danh sách `InvoiceVersion` với phiên bản hiện hành (FR-DETAIL-001, FR-STORE-008).
- Hành động: `Xem XML`, `Xem chứng từ (PDF)`, chọn phiên bản để xem, `Quay lại` Kho (giữ bộ lọc), `Tải lại XML` khi file lỗi (qua DD-05), `Xuất ZIP` cho một hóa đơn (DD-09).
- Cảnh báo cụ thể khi XML mất/lỗi/sai hash (FR-STORE-009).

### 3.1 Nguồn dữ liệu và in (đã đối chiếu cổng thật)

- API chi tiết: `GET /api/query/invoices/detail?nbmst=…&khhdon=…&shdon=…&khmshdon=…` → JSON có `hdhhdvu[]` (hàng hóa/dịch vụ), `thttltsuat[]`, `qrcode` (`IPortalApiClient.GetInvoiceDetailAsync`).
- **Cổng không có API in.** Nút In của cổng gọi lại API chi tiết, render hóa đơn trong trang rồi mở hộp thoại in của trình duyệt. Ứng dụng làm tương tự: dựng trang từ XML/JSON đã lưu và in ra PDF.
- Gói xuất XML của cổng kèm sẵn **trình xem**: `invoice.html` + `details.js` (+ `viewinvoice-bg.jpg`, `sign-check.jpg`) đọc `invoice.xml` để hiển thị mẫu hóa đơn có hình nền và dấu kiểm chữ ký. Đây là ứng viên cho `IPdfRenderer` (gate G-03/G-09): dùng lại đúng mẫu hiển thị của cổng thay vì tự thiết kế; cần kiểm tra giấy phép/độ an toàn của `details.js` (110 KB) trước khi nhúng, và chạy trong WebView không có quyền ra mạng (NFR-SEC-003).

## 4. Trình xem XML (SCR-07 — tab XML)

- Chế độ **cây** và chế độ **nội dung thô** (FR-DETAIL-003), chỉ đọc.
- Phân tích bằng `XmlReader` với `XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }`; không thực thi script, không resolve external entity, không tải tài nguyên ngoài (NFR-SEC-003).
- Không dùng control web để render XML thô; dùng `TreeView` và `TextBox`/`RichTextBox` chỉ đọc.
- File lớn: đọc streaming hoặc giới hạn kích thước hiển thị và báo rõ; không nạp XML hàng loạt vào bộ nhớ.

## 5. PDF phát sinh, in và lưu bản sao (SCR-07 — tab PDF)

```csharp
public interface IInvoicePdfService
{
    // Trả bản PDF đúng phiên bản XML; tạo mới nếu chưa có. Ghi FileAsset(Kind=PDF, SourceVersionId, SHA-256 nguồn)
    Task<PdfResult> GetOrCreateAsync(InvoiceKey key, VersionId version, CancellationToken ct);
}

public interface IPdfRenderer
{
    Task<Stream> RenderAsync(InvoiceXmlDocument xml, CancellationToken ct);   // adapter theo ADR G-03/G-09
}
```

- PDF chỉ tạo khi người dùng xem, in hoặc xuất PDF (BR-008); không tạo trong lúc tải.
- Metadata nội bộ PDF lưu ngày tạo và SHA-256/ID phiên bản XML nguồn (FR-DETAIL-005).
- Khi người dùng chuyển phiên bản XML trong lúc đang xem PDF của phiên bản khác: cảnh báo (FR-DETAIL-007).
- In qua hộp thoại in Windows; `Lưu bản sao` ra vị trí tự chọn; không ghi đè nếu chưa xác nhận (FR-DETAIL-006, FR-EXPORT-008).
- Kết xuất chạy nền, có trạng thái chờ và hủy; không treo UI (NFR-PERF-001).
- Chiến lược hiển thị (endpoint PDF của cổng hay render theo mẫu từ XML) chốt ở G-03; định dạng PDF hiển thị trong control chốt theo G-09. Trước khi có ADR chỉ phát triển tới interface và adapter giả.

## 6. Kiểm thử

### 6.1 Unit

- Chọn phiên bản hiện hành/cũ; phát hiện PDF thuộc phiên bản khác.
- Parser XML từ chối DTD/external entity; XML hỏng báo lỗi rõ.
- Tạo PDF chỉ khi chưa có PDF đúng phiên bản (cache).

### 6.2 Integration

- Hóa đơn mẫu tiếng Việt, số 0 đầu, ngoại tệ → PDF hiển thị đúng; XML gốc không bị sửa (AC-10).
- File XML bị xóa/sửa → cảnh báo và không tạo PDF sai.

### 6.3 UI smoke

- SCR-05 → SCR-06 → SCR-07 → quay lại SCR-05 giữ bộ lọc.

## 7. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 7.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T08-01 | `IInvoiceDetailService` + `InvoiceDetail` | Application | T06-03 | ✅ | | IInvoiceDetailService/InvoiceDetailService: metadata, phiên bản (mới nhất trước), trạng thái file từng phiên, phiên nguồn; OpenXmlAsync cảnh báo file mất/quá lớn/hỏng/sai SHA-256; IInvoiceFileReader đọc chỉ trong thư mục kho |
| T08-02 | `InvoiceDetailControl` + presenter (SCR-06) | WinForms | T08-01, T07-04 | ✅ | | InvoiceDetailControl + InvoiceDetailPresenter (SCR-06): thông tin, bên bán/mua, giá trị, thẻ file XML/PDF, lịch sử phiên bản, nguồn tải; Quay lại giữ bộ lọc SCR-05 |
| T08-03 | Trình xem XML an toàn (cây/thô) | WinForms | T06-05 | ✅ | | XmlTreeBuilder (DtdProcessing.Prohibit, XmlResolver=null, giới hạn độ sâu 64 và 50.000 nút) + TreeView và TextBox chỉ đọc; XML hỏng vẫn xem được nội dung thô |
| T08-04 | `IInvoicePdfService`, lưu `FileAsset` PDF theo phiên bản | Application | T06-03 | ⬜ | | Chờ G-03/G-09; chưa có IInvoicePdfService |
| T08-05 | `IPdfRenderer` thật theo ADR | Infra | G-03, G-09 | ⛔ | | Chờ ADR |
| T08-06 | `InvoiceViewerControl` (SCR-07): tab XML/PDF, in, lưu bản sao | WinForms | T08-03, T08-04 | 🟡 | | InvoiceViewerControl + InvoiceViewerPresenter (SCR-07): tab XML cây/thô, đổi phiên bản, Lưu bản sao XML nguyên byte; tab PDF và nút In chờ G-03/G-09 |
| T08-07 | Cảnh báo PDF khác phiên bản; chuyển phiên bản | WinForms | T08-06 | 🟡 | | Đổi phiên bản XML đã có; cảnh báo PDF khác phiên bản chờ có PDF |
| T08-08 | Unit + integration + UI smoke (mục 6) | Tests | T08-02..T08-06 | 🟡 | | 19 test service, 6 test đọc file, 24 test presenter, 1 test điều hướng từ SCR-05; còn integration hóa đơn mẫu và smoke 05→06→07 |

### 7.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 8. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
