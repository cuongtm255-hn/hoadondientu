# ADR 0003 — Tạo file .xlsx và .zip không dùng thư viện ngoài (G-09, phần Excel/ZIP)

| | |
|---|---|
| Trạng thái | **Accepted một phần** — chốt cho Excel và ZIP; **PDF chưa chốt** (G-03, G-09 còn mở) |
| Ngày | 02/11/2026 |
| Gate | G-09 PDF/Excel library ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md)) |
| Liên quan | [DD-09](../detail-design/DD-09-EXPORT.md), [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md), [ADR 0002](0002-excel-export-fidelity.md) |

## Bối cảnh

Mục tiêu là .NET Framework 4.5 (CLAUDE.md): nhiều thư viện hiện đại không có bản net45, và mỗi phụ thuộc thêm làm tăng rủi ro giấy phép, đóng gói và bảo trì
(R-05). Việc xuất cần: bảng kê `.xlsx` 19 cột có kiểu ô đúng, ghi từng dòng (không nạp cả bảng vào bộ nhớ, NFR-PERF-005) và gói `.zip` chứa XML nguyên byte kèm manifest.

## Phương án

| Phương án | Nhận xét (đánh giá trên giấy; **chưa thử** trong dự án này) |
|---|---|
| Open XML SDK | Cần bản tương thích net45 và kéo theo nhiều gói; kích thước và phụ thuộc lớn so với nhu cầu một sheet |
| ClosedXML / EPPlus / NPOI | Tính năng thừa (công thức, biểu đồ); mỗi thư viện có ràng buộc giấy phép hoặc phiên bản net45 riêng cần kiểm lại; một số mô hình bộ nhớ nạp cả sheet |
| Xuất CSV | Không giữ được kiểu ô, số 0 đầu và không khớp mẫu của cổng |
| **Tự dựng SpreadsheetML bằng `System.IO.Compression`** | Xlsx là gói zip các phần XML; nhu cầu chỉ một sheet, vài kiểu ô, ô gộp và độ rộng cột. `System.IO.Compression` có sẵn từ net45 |

## Quyết định

1. **Excel**: `InvoiceExcelWriter` tự viết gói OPC (`[Content_Types].xml`, quan hệ, `workbook.xml`, `styles.xml`, một sheet) bằng `ZipArchive`; chữ ghi dạng `inlineStr` (giống mẫu của cổng), số/ngày là số có định dạng, ghi từng dòng ra luồng.
2. **ZIP**: dùng thẳng `System.IO.Compression.ZipArchive`; không cần thư viện thứ ba.
3. Hai bộ ghi đặt ở lớp Application (chỉ phụ thuộc thư viện chuẩn), không phải Infrastructure như DD-09 ban đầu; không có adapter để thay thế nên không cần cổng giao diện.
4. **Không có quyết định nào ở đây cho PDF và SQLite.** PDF (G-03, G-09) và SQLite (G-08) vẫn mở; T00-10 chỉ đóng phần Excel/ZIP.

## Bằng chứng

- Test tự động đọc lại từng ô, kiểm tra các phần XML hợp lệ, quan hệ trỏ đúng phần có thật, thoát ký tự `& < > "` và bỏ ký tự điều khiển không hợp lệ.
- File sinh ra mở được bằng Microsoft Excel (COM) và bằng `System.IO.Packaging` (xem [ADR 0002](0002-excel-export-fidelity.md)).
- Một lỗi hiển thị thật đã được bắt và sửa nhờ mở bằng Excel: định dạng `#,##0.##` hiện "30,000,000." (dấu chấm thừa) → dùng `#,##0` cho số nguyên và `#,##0.00` cho số có phần lẻ.

## Hệ quả

- (+) Không thêm phụ thuộc, không vướng giấy phép, không cần đóng gói DLL; bộ nhớ không tăng theo số dòng.
- (−) Tự bảo trì một phần định dạng OOXML: nếu cần công thức, nhiều sheet, đồ thị hay mật khẩu thì phải mở rộng hoặc đổi sang thư viện (khi đó viết ADR mới thay thế).
- (−) Mới kiểm với Excel trên một máy; chưa thử LibreOffice, Excel cũ (2010) hoặc Excel trên Mac. Cần thêm vào kiểm thử phát hành (P6).
- (−) Chưa có test tập 100.000 dòng và file bị khóa/hết dung lượng (T09-07).
