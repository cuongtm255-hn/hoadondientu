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
5. Khối chữ ký giống cổng (khung xanh, "Signature Valid", "Ký bởi …", "Ký ngày …", dấu tích xanh) **chỉ khi chữ ký thật sự hợp lệ**: `InvoiceSignatureVerifier` kiểm XML-DSig của người bán bằng `SignedXml` trên chính file XML (băm từng tham chiếu và kiểm chữ ký bằng khóa trong chứng thư đính kèm; đã kiểm trên file mẫu thật của cổng: hợp lệ, và sửa số hóa đơn hoặc tổng tiền thì thành không hợp lệ). Chữ ký sai in khung đỏ "Signature Invalid"; không kiểm được thì khung xám "Chưa kiểm tra được chữ ký số"; XML không có chữ ký thì không có khung. Chỉ nhận tham chiếu nội bộ và phép biến đổi chuẩn hóa/chữ ký bao (không XSLT, không địa chỉ ngoài). **Không** kiểm chuỗi chứng thư, thời hạn hay thu hồi, và trang ghi rõ điều đó ở chân trang.

## Hệ quả

- (+) Chạy offline, không gọi cổng, không JavaScript ngoài; kiểm thử được cả bố cục (ảnh trang) lẫn PDF.
- (+) Một bố cục cho xem trước, in và PDF.
- (+) Có hình nền hoa văn trống đồng như trang của cổng: nhúng `viewinvoice-bg.jpg` lấy từ gói xuất XML của cổng (tài sản của cổng, nhúng theo yêu cầu của người dùng; dấu tích xanh được vẽ vector thay vì dùng `sign-check.jpg` để không có nền trắng). Dấu gạch chấm dưới từng dòng thông tin và khung viền theo mẫu PDF in của cổng (`docs/api-samples`).
- (−) Hình nền làm tăng cỡ file PDF thêm khoảng 150 KB mỗi bản (một ảnh dùng chung cho mọi trang) và là tài sản của cổng: nếu cần bỏ, xóa tài nguyên nhúng thì bản in vẫn dựng bình thường.
- (−) Phụ thuộc phông Times New Roman của Windows (có sẵn trên mọi bản Windows mục tiêu); phông được nhúng vào PDF nên mở được ở máy khác.
- (−) Chưa hỗ trợ các biến thể hóa đơn lạ (hóa đơn có nhiều bảng, phụ lục, mẫu đặc thù): hiển thị phần chung và để trống phần không hiểu.

## Cập nhật 04/11/2026: gói xem hóa đơn (`invoice.html`)

Gói "Xuất xml" của cổng có `invoice.xml` + `invoice.html` + `details.js` + `sign-check.jpg` + `viewinvoice-bg.jpg`. `invoice.html` của cổng đã **dựng sẵn dữ liệu** (không đọc XML lúc mở; `details.js` chỉ vẽ mã QR từ `qrcodeContent` và rút chuỗi chứng thư thành CN), nên không thể chép nguyên cho hóa đơn khác. Ứng dụng giữ nguyên phần đầu (CSS, script) của file mẫu làm tài nguyên nhúng và dựng phần thân từ `InvoicePrintModel` (`InvoiceViewerPackage.Html`), cùng một mô hình với PDF. Test so chữ hiển thị với `invoice.html` mẫu của cổng: giống nhau (chỉ khác chuỗi chứng thư đã rút về CN). Hai chỗ khác cổng có chủ ý: nhãn chữ ký lấy theo kết quả kiểm thật ("Signature Valid" chỉ khi hợp lệ), và không in các thẻ lạ `<HHDVu>`, `<LTSuat>` mà cổng để lọt vào HTML. Ảnh nền trống đồng dùng chung cho PDF và HTML (một bản nhúng trong Application). Thứ tự dòng người mua ở PDF theo bản PDF in của cổng, còn HTML theo trang HTML của cổng; hai nguồn của cổng vốn khác nhau ở chỗ này.

## Chưa kiểm

- Đối chiếu bố cục với nhiều hóa đơn thật (bán hàng, GTGT nhiều thuế suất, ngoại tệ, hóa đơn từ máy tính tiền, hóa đơn điều chỉnh/thay thế). Mới kiểm bằng hóa đơn giả theo cấu trúc TT78 và cấu trúc của một file XML thật đã có trong `docs/api-samples/invoice.zip`.
- In ra máy in thật (đã kiểm phần dựng ảnh 300 dpi, chưa gửi tới máy in).
- Kiểm chữ ký trên nhiều chứng thư/thuật toán khác (mới kiểm RSA-SHA256 trên một file mẫu).
- Mở file PDF trong các trình đọc khác nhau (Adobe, Edge, Foxit); đã kiểm cấu trúc (có font nhúng, đúng số trang) nhưng không mở bằng trình đọc.
