# ADR 0009 — PDF và in hóa đơn: tự dựng từ XML bằng PDFsharp-gdi 1.50 (G-03, G-09 phần PDF)

| | |
|---|---|
| Trạng thái | **Accepted** — đã cài đặt và kiểm trên hóa đơn giả; chưa đối chiếu với hóa đơn thật nhiều dạng (xem "Chưa kiểm") |
| Ngày | 03/11/2026 |
| Gate | G-03 PDF strategy và G-09 (phần PDF) ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md)) |
| Liên quan | [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md) mục 3.1 và 5, [ADR 0003](0003-xlsx-and-zip-generation.md), [ADR 0005](0005-mock-only-testing-policy.md) |

## Bối cảnh

Hóa đơn gốc là file XML; PDF chỉ là bản thể hiện để xem và in (BR-008). Cổng Thuế **không có API in/PDF**: nút In của cổng gọi lại API chi tiết rồi dựng trang trong trình duyệt
(DD-08 mục 3.1). Gói XML xuất của cổng kèm trình xem `invoice.html` + `details.js` (110 KB) nhưng cần WebView, kiểm giấy phép/độ an toàn của `details.js` và nhúng JavaScript
của bên thứ ba. Ứng dụng nhắm net452 (ADR 0007), chạy được cả khi không có mạng.

## Phương án

| Phương án | Đánh giá |
|---|---|
| Lấy PDF/endpoint in của cổng | Không có endpoint như vậy theo traffic thật; sẽ phải gọi cổng cho mỗi lần xem, vi phạm ý định "đọc dữ liệu đã lưu" và làm tăng tải lên cổng |
| Nhúng `invoice.html`/`details.js` của cổng trong WebView | Dùng đúng mẫu của cổng nhưng kéo JavaScript chưa kiểm vào ứng dụng, cần WebView thứ hai (Chromium đã dành cho đăng nhập) và xuất PDF vẫn phải làm riêng |
| **Tự dựng từ XML bằng PDFsharp** | Không phụ thuộc mạng/WebView/JS ngoài; một bố cục dùng chung cho PDF, xem trước và in; kiểm thử tự động được. Phải tự thiết kế mẫu (đơn giản hơn mẫu có hình nền của cổng) |
| QuestPDF, iText 7, PDFsharp 6.x | Không có bản net452 hoặc giấy phép AGPL/thương mại; PDFsharp 6.x cần .NET Standard |

Đã kiểm gói trên NuGet: `PDFsharp-gdi` **1.50.5147** (MIT) là bản GDI+ có `XGraphics.FromGraphics`, lib `net20` nên dùng được trên net452 (gói `PdfSharp` cùng số hiệu là bản
"core" không có `FromGraphics` và không vẽ được lên `Graphics`).

## Quyết định

1. Dựng bản thể hiện từ chính file XML đã lưu: `InvoicePrintModelParser` đọc lược đồ TT78 (`HDon/DLHDon`, so khớp theo tên cục bộ, không DTD, không resolve tài nguyên ngoài) thành
   `InvoicePrintModel`; trường thiếu để trống, không tính lại số liệu.
2. `PdfSharpInvoiceRenderer` (Infrastructure) dàn trang A4 thành danh sách lệnh vẽ theo trang rồi phát ra **PDF** (Times New Roman nhúng Unicode), **ảnh PNG** (xem trước ~96 dpi, in ~300 dpi) —
   cùng một bố cục nên ba đầu ra luôn giống nhau. Application chỉ biết `IInvoiceDocumentRenderer`/`IInvoiceDocument`.
   **Bố cục theo trang xem hóa đơn của cổng** (đối chiếu ảnh chụp và HTML mẫu do người dùng cung cấp, 03/11/2026): khung viền đôi màu nâu, mã QR (từ `DLQRCode` trong XML, vẽ bằng QRCoder 1.6.0, MIT) ở góc trên trái, khối "Mẫu số / Ký hiệu / Số" bên phải, tiêu đề đầy đủ theo ký hiệu mẫu số (1 → HOÁ ĐƠN GIÁ TRỊ GIA TĂNG...), ngày, MCCQT, danh sách thông tin người bán/người mua (giữ cả các dòng không có giá trị như cổng), bảng 10 cột, bảng thuế suất và bảng tổng hợp, khối chữ ký, dòng "Cần kiểm tra, đối chiếu khi lập, nhận hóa đơn".
3. **Không dựng khi không tin được XML**: file mất, quá lớn (>5 MB), không phải hóa đơn, hoặc **sai SHA-256** → tab PDF nêu lý do, In/Lưu PDF bị khóa (tránh in sai chứng từ, AC-10).
4. PDF chỉ được ghi ra đĩa khi người dùng bấm **Lưu PDF**; in qua hộp thoại in của Windows (`PrintDocument`). Chưa lưu PDF vào kho và chưa có bảng `FileAsset` cho PDF: dựng lại mỗi lần
   xem (vài chục mili giây); sẽ thêm bộ nhớ đệm khi đo thấy cần.
5. Khối chữ ký chỉ nói "XML có chữ ký số của người bán, ký bởi …, ký ngày …" và **không** in "Signature Valid"/dấu tích xanh như cổng, vì ứng dụng **không** kiểm chữ ký số; trang ghi rõ bản thể hiện dựng từ XML và XML mới là chứng từ gốc.

## Hệ quả

- (+) Chạy offline, không gọi cổng, không JavaScript ngoài; kiểm thử được cả bố cục (ảnh trang) lẫn PDF.
- (+) Một bố cục cho xem trước, in và PDF.
- (−) Chưa có hình nền hoa văn trống đồng và dấu kiểm chữ ký xanh của cổng (hình nền là tài sản của cổng, dấu kiểm sẽ ngụ ý đã xác minh chữ ký); các phần còn lại giống bố cục của cổng.
- (−) Phụ thuộc phông Times New Roman của Windows (có sẵn trên mọi bản Windows mục tiêu); phông được nhúng vào PDF nên mở được ở máy khác.
- (−) Chưa hỗ trợ các biến thể hóa đơn lạ (hóa đơn có nhiều bảng, phụ lục, mẫu đặc thù): hiển thị phần chung và để trống phần không hiểu.

## Chưa kiểm

- Đối chiếu bố cục với nhiều hóa đơn thật (bán hàng, GTGT nhiều thuế suất, ngoại tệ, hóa đơn từ máy tính tiền, hóa đơn điều chỉnh/thay thế). Mới kiểm bằng hóa đơn giả theo cấu trúc TT78 và cấu trúc của một file XML thật đã có trong `docs/api-samples/invoice.zip`.
- In ra máy in thật (đã kiểm phần dựng ảnh 300 dpi, chưa gửi tới máy in).
- Mở file PDF trong các trình đọc khác nhau (Adobe, Edge, Foxit); đã kiểm cấu trúc (có font nhúng, đúng số trang) nhưng không mở bằng trình đọc.
