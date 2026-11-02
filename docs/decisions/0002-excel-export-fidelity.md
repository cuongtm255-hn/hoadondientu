# ADR 0002 — Bảng kê Excel: độ khớp với mẫu của cổng (G-04)

| | |
|---|---|
| Trạng thái | **Accepted** |
| Ngày | 02/11/2026 |
| Gate | G-04 Excel fidelity ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md)) |
| Liên quan | [DD-09](../detail-design/DD-09-EXPORT.md), [ADR 0003](0003-xlsx-and-zip-generation.md), [api-samples mục 5](../api-samples/README.md) |

## Bối cảnh

Ứng dụng xuất bảng kê hóa đơn từ dữ liệu đã lưu và phải "mirror" chức năng xuất Excel của cổng (CLAUDE.md), nhưng được phép khác ở chỗ cần thiết:
xuất cả khoảng nhiều tháng, ghi phạm vi xuất (FR-EXPORT-003) và giữ kiểu dữ liệu sắp xếp được (FR-EXPORT-002). Gate G-04 đòi một báo cáo đối chiếu
file do ứng dụng xuất với workbook mẫu thật `docs/api-samples/DANH SÁCH HÓA ĐƠN.xlsx` (cột, kiểu dữ liệu, tiêu đề, phạm vi ngày) và một quyết định rõ về chỗ khác biệt.

## Bằng chứng

Đối chiếu ngày 02/11/2026 giữa mẫu của cổng và file do `InvoiceExcelWriter` sinh ra (dữ liệu giả, 3 hóa đơn):

| Hạng mục | Mẫu của cổng | File ứng dụng | Kết quả |
|---|---|---|---|
| Số sheet | 1 | 1 | Khớp |
| Tiêu đề `A3:S3` | "DANH SÁCH HÓA ĐƠN" (gộp ô) | giống | Khớp |
| Khoảng ngày `A4:S4` | "Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy" (gộp ô) | giống; khoảng lấy từ bộ lọc, hoặc từ ngày lập nhỏ nhất/lớn nhất của hóa đơn xuất | Khớp |
| Dòng tiêu đề | dòng 6, 19 cột | dòng 6, 19 cột; **chuỗi tiêu đề giống từng ký tự** (kiểm bằng script) | Khớp |
| Dữ liệu | từ dòng 7 | từ dòng 7 | Khớp |
| Độ rộng 19 cột | 7,8125 · 11,71875 · 19,53125 · 31,25 · 50,78125 theo cột | **bằng nhau từng cột** | Khớp |
| Ký hiệu mẫu số, ký hiệu, số hóa đơn, MST | chuỗi (giữ số 0 đầu) | chuỗi (`inlineStr`) | Khớp |
| STT | số | số, đánh lại từ 1 | Khớp |
| Tiền (K–O) | số; ô chiết khấu/phí để trống khi không có | số; để trống khi không có | Khớp |
| Cột H | MST người mua, cá nhân thì CCCD | giống | Khớp |
| Cột R, S | nhãn của `tthai`, `ttxly` | nhãn lấy từ `PortalOptions` | Khớp |
| **Ngày lập (E)** | **chuỗi** "30/10/2026" | **số ngày của Excel**, định dạng `dd/mm/yyyy` | Khác có chủ đích |
| **Tỷ giá (Q)** | **chuỗi** "1.0" | **số** | Khác có chủ đích |
| Dòng 2 | trống | phạm vi xuất (gộp `A2:S2`) | Thêm có chủ đích |
| Định dạng hiển thị | font Times New Roman 11, viền một số ô | Times New Roman 11, viền mọi ô dữ liệu, tiêu đề cột in đậm có nền, định dạng số `#,##0` / `#,##0.00` | Khác nhỏ |
| Cố định hàng | không | cố định đến dòng tiêu đề | Thêm nhỏ |

File sinh ra đã được **mở bằng Microsoft Excel** (qua COM) và đọc lại đúng tiêu đề, ngày (`01/11/2026`), số hóa đơn có số 0 đầu (`0000121`), tiền (`30,000,000`)
và tên có ký tự `&` và tiếng Việt. Cấu trúc gói OPC cũng được `System.IO.Packaging` mở được. Các kiểm tra tương đương nằm trong test tự động (`InvoiceExportServiceTests`).

## Quyết định

1. **Giữ nguyên bố cục của cổng**: vị trí tiêu đề/khoảng ngày/tiêu đề cột/dữ liệu, 19 cột đúng thứ tự, chuỗi tiêu đề và độ rộng cột. Đây là phần người dùng đối chiếu hoặc nhập vào hệ thống khác.
2. **Ngày lập và tỷ giá ghi dạng số**, không bắt chước kiểu chuỗi của cổng. Người dùng xem vẫn thấy `dd/MM/yyyy` nhưng sắp xếp và lọc theo ngày được trong Excel (FR-EXPORT-002). Cột ký hiệu, số hóa đơn, MST giữ dạng chuỗi để không mất số 0 đầu.
3. **Thêm dòng 2 ghi phạm vi xuất** (toàn bộ kết quả đang lọc hay các dòng đã chọn, kèm số hóa đơn) vì FR-EXPORT-003; dòng này trống ở mẫu nên không làm lệch vị trí các ô khác.
4. Định dạng số/viền/cố định hàng được phép khác mẫu vì không ảnh hưởng dữ liệu.

## Hệ quả

- (+) Khớp mẫu ở mọi chỗ ảnh hưởng đến cách đọc dữ liệu; chỗ khác đều có lý do và được ghi lại ở đây.
- (−) Người dùng nhập file vào công cụ cần đúng kiểu chuỗi cho ngày/tỷ giá sẽ phải đổi định dạng ô. Nếu có yêu cầu như vậy, thêm tùy chọn "ngày dạng chuỗi" thay vì đổi mặc định.
- (−) Mới đối chiếu với **một** mẫu bán ra gồm 3 hóa đơn. Hóa đơn mua vào, ngoại tệ và hóa đơn có chiết khấu/phí chưa có mẫu thật; cần đối chiếu lại khi có (T05-11).

## Cách kiểm tra lại

1. Sinh file bằng `InvoiceExcelWriter` (hoặc nút Xuất Excel ở Kho hóa đơn, bản Debug dữ liệu giả).
2. So tiêu đề, độ rộng cột, ô gộp với mẫu bằng script đọc XML (zip) như ở phần Bằng chứng; mở bằng Excel kiểm tra ngày, số 0 đầu, tiền.
3. Chạy `InvoiceExportServiceTests` (đọc lại từng ô từ file xuất).
