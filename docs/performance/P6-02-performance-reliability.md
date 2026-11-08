# P6-02 — Hiệu năng và độ tin cậy (đợt 1 và 2, 06/11/2026)

Phạm vi đợt này: **NFR-PERF-002** (tìm theo số/MST và mở trang đầu không quá 2 giây ở percentile 95 với kho 100.000 hóa đơn) và **NFR-PERF-007** / **NFR-REL-006** (chuyển kho, mục cuối). Các yêu cầu PERF/REL khác chưa đo.

## Cách đo

Test `ArchiveScaleTests` (Infrastructure.Tests, nhóm `Performance`, dữ liệu GIẢ, không gọi cổng Thuế) dựng kho thật: SQLite `company.db` + 100.000 file XML thật trên đĩa, 50.000 hóa đơn mỗi chiều (mua vào/bán ra), 5.000 đối tác, mọi file còn đủ. Mỗi phép đo chạy 20 lần với `InvoiceArchiveService` mới (bộ nhớ đệm tồn tại file trống, tức đúng lúc mở màn hình lần đầu) rồi lấy p95. Mặc định test bị bỏ qua vì dựng dữ liệu mất vài chục phút:

```bash
export HDDT_PERF=1 HDDT_PERF_N=100000 HDDT_PERF_DIR='C:\Users\<tên>\AppData\Local\Temp\hddt-perf-100k'   # HDDT_PERF_DIR giữ dữ liệu cho lần chạy sau
"$VT" tests/HoaDonDienTu.Infrastructure.Tests/bin/Debug/net452/HoaDonDienTu.Infrastructure.Tests.dll /TestCaseFilter:"FullyQualifiedName~ArchiveScaleTests" --logger:"console;verbosity=detailed"
```

Máy đo là máy phát triển, **không phải cấu hình triển khai** chuẩn; số tuyệt đối chỉ để so sánh trước/sau.

## Kết quả (kho 100.000, p95 của 20 lần)

| Thao tác | Trước tối ưu | Sau tối ưu | Ngưỡng |
|---|---|---|---|
| Mở trang đầu, một chiều (50.000 hóa đơn), không lọc | 1,32 s | **0,50 s** | 2 s |
| Tìm theo số hóa đơn | 0,008 s | 0,008 s | 2 s |
| Tìm theo MST đối tác | 0,072 s | 0,070 s | 2 s |
| Tìm nhanh theo tên (LIKE trên `search_text`) | 0,134 s | 0,180 s | 2 s |
| Mở lại khi đã có bộ nhớ đệm tồn tại file | 0,24 s | 0,29 s | 2 s |

Tất cả đạt NFR-PERF-002. Hai dòng cuối chênh nhẹ giữa hai lần chạy là nhiễu đo (không đổi mã ở các đường đó).

## Nút thắt tìm thấy và sửa

Mỗi lần tra cứu, `InvoiceArchiveService` lấy **mọi** hóa đơn khớp bộ lọc rồi gọi `File.Exists` từng file để tính tình trạng file và tóm tắt (số lỗi/thiếu), kể cả khi chỉ hiển thị 50 dòng. Với 50.000 file mỗi chiều mất khoảng 1,1 s; ngoại suy tuyến tính, một kho **100.000 hóa đơn cùng một chiều** sẽ mất khoảng 2,6 s, vượt ngưỡng.

Sửa: thêm `IInvoiceDirectoryLister` (tùy chọn, `FileSystemInvoiceFileStore` triển khai). Khi một thư mục có từ 200 hóa đơn khớp trở lên, ứng dụng **quét thư mục một lần** và đối chiếu tên file thay vì hỏi từng file; thư mục ít file vẫn hỏi từng file, quét không được thì quay về cách cũ, kết quả vẫn vào bộ nhớ đệm 10 giây như trước. Ngữ nghĩa "thiếu file luôn theo đĩa lúc này" và nút Kiểm tra file (bỏ qua bộ nhớ đệm) giữ nguyên. Có 5 test mới cho dịch vụ và 1 test cho bộ liệt kê. Ngoại suy sau sửa: 100.000 cùng một chiều khoảng 1,0 s.

## Chuyển kho (NFR-PERF-007, NFR-REL-006)

Test `SwitchScaleTests` dùng chính bộ dữ liệu 100.000 hóa đơn làm một kho và một kho nhỏ làm kho kia, với các thành phần thật (`CompanySwitchService`, `CompanyContextManager`, `CompanyStorageOpener`, `JsonCompanyCatalog`, SQLite). Đo cả phần chuyển context lẫn lần tra cứu đầu tiên sau đó (lúc đó cơ sở dữ liệu của kho đích mới thật sự được mở và kiểm toàn vẹn). Cần `HDDT_PERF=1` và `HDDT_PERF_DIR` đã dựng sẵn.

| Phép đo (12 lần chuyển) | Kết quả | Ngưỡng |
|---|---|---|
| Chuyển context | 0,004–0,018 s | |
| Chuyển sang kho nhỏ + tra cứu đầu | 0,010–0,016 s | 5 s |
| Chuyển sang kho 100.000 + tra cứu đầu (bộ nhớ đệm hệ điều hành nóng) | 0,26–0,30 s | 5 s |
| **Lần đầu, bộ nhớ đệm hệ điều hành nguội** | **3,15 s** | 5 s |

Đạt NFR-PERF-007, nhưng lần đầu nguội dùng gần 2/3 ngưỡng trên máy phát triển; ổ cứng cơ hoặc máy yếu hơn có thể vượt 5 s. NFR-REL-006 phần "không có hai kho cùng hoạt động qua nhiều lần chuyển" cũng được kiểm với kho thật (test `OnlyOneStoreContextIsOpenAtATime…`); phần "đóng tiến trình giữa lúc chuyển" chưa kiểm ở mức tiến trình thật.

## Đợt 2: các yêu cầu còn lại

| Yêu cầu | Kết quả | Căn cứ |
|---|---|---|
| NFR-PERF-001 UI không treo khi tính hash, xuất file | **Đạt ở mức dịch vụ** | `UiResponsivenessTests`: chạy thao tác từ một luồng giao diện giả (vòng lặp thông điệp một luồng có SynchronizationContext) và đo độ trễ của thông điệp 10 ms. Excel 20.000 dòng: đứng hình tối đa 4 ms; ZIP 300 hóa đơn kèm băm SHA-256: 0–3 ms; sao lưu 500 file: 11 ms; khôi phục 500 file: 100 ms (ngưỡng 250 ms). Bài đối chứng cho thấy phép đo phát hiện được luồng bị chặn 600 ms |
| NFR-PERF-003 mở ứng dụng ≤ 5 s | **Đạt (Debug, dữ liệu giả)** | Smoke: từ dựng CompositionRoot tới màn Tổng quan 1,35 s, gồm lần chạy đầu (JIT). Chưa đo bản Release trên máy chuẩn |
| NFR-PERF-006 danh mục từ cache | **Đạt** | 200 doanh nghiệp liệt kê trong 8 ms; không có file `company.db` nào được mở hay tạo; số liệu tổng quan (số hóa đơn, dung lượng) lấy từ danh mục |
| NFR-REL-001 không mất hóa đơn đã commit | **Đạt** | Chép các file cơ sở dữ liệu đúng lúc kết nối còn mở (mô phỏng dừng đột ngột/mất điện) rồi mở bản chép: đủ 150/150 hóa đơn, kiểm toàn vẹn đạt; một lần ghi thất bại giữa chừng không để lại dòng dở dang |
| NFR-REL-002 tiếp tục không tạo bản ghi trùng | **Đạt** | Ngắt do đĩa đầy ở file thứ 6 rồi tiếp tục: đủ hóa đơn, mỗi hóa đơn một phiên bản (T06-09); các test checkpoint của engine |
| NFR-REL-003 transaction và integrity | **Đạt** | Mọi ghi đi qua transaction; `IsIntact` chạy khi mở và khi sao lưu/khôi phục |
| NFR-REL-004 SHA-256 và trạng thái kiểm tra | **Đạt** | SHA-256 từng phiên bản, `file_problem` và `file_checked_utc` lưu theo hóa đơn, hiện ở Kho hóa đơn |
| NFR-REL-005 không báo hoàn tất khi còn đoạn lỗi | **Đạt** | Export hỏng của cổng giả → phiên `CompletedWithErrors`, không lưu gì |
| NFR-REL-006 chuyển kho an toàn | **Đạt (đã kiểm bằng tiến trình thật)** | Qua nhiều lần chuyển chỉ có một kho mở (kho thật). `SwitchKilledProcessTests`: một tiến trình PowerShell thật giữ giao dịch ghi dở trên `company.db` của kho A (rồi kho B) bị giết bằng TerminateProcess; bài đối chứng xác nhận tiến trình con đang giữ khóa ghi. Mở lại: cả hai kho qua `integrity_check`, giao dịch dở bị bỏ, chuyển A→B→A mỗi lần chỉ một context. "Lần mở sau chọn kho hợp lệ cuối cùng": ứng dụng **không lưu** kho đang hoạt động nên không có trạng thái nào có thể dở dang; bản Dịch vụ luôn vào màn chọn doanh nghiệp, bản Standard mở doanh nghiệp duy nhất. Chưa kiểm giết giữa lúc ghi catalog `companies.json` (chuyển kho không ghi file này) |
| NFR-REL-007 danh mục hỏng không tạo kho rỗng | **Đạt** | Danh mục hỏng không bị ghi đè; màn hình khôi phục quét thư mục và đăng ký lại kho, giữ bản hỏng (T02-07) |

### Gia cố tìm được khi đo PERF-001

Khi chạy thử với kho trả kết quả ngay (không có điểm bất đồng bộ thật), cả Excel, ZIP và sao lưu chạy trọn trên luồng giao diện (đứng hình 0,7–2,4 s). Bản chạy thật không bị vì repository SQLite chạy truy vấn bằng `Task.Run` nên các tiếp nối rời luồng giao diện, nhưng sự an toàn đó phụ thuộc vào chi tiết của repository. Đã gia cố: `InvoiceExportService` giờ ghi Excel và đóng gói ZIP (đọc file, băm, nén) trong `Task.Run`, không còn phụ thuộc. `BackupService` vẫn dựa vào việc chụp/thay cơ sở dữ liệu là bất đồng bộ thật (đúng với `SqliteCompanyDatabaseBackup`); nếu thay cách cài đặt thì cần chạy lại `UiResponsivenessTests`.

## Giới hạn và điểm cần theo dõi

1. **Lần truy cập đầu khi bộ nhớ đệm hệ điều hành còn nguội (cả mở trang đầu lẫn chuyển kho, đo được 3,2–3,3 s):** trong 20 lần đo của lượt chạy sau tối ưu có một mẫu chậm 3,3 giây (không ghi lại là lần thứ mấy, nhiều khả năng là lần đầu khi bộ nhớ đệm hệ điều hành còn nguội). p95 của 20 mẫu không phản ánh mẫu này. Người dùng mở kho lần đầu sau khi khởi động máy, nhất là ổ cứng cơ, có thể gặp độ trễ đó; chưa đo trên cấu hình triển khai.
2. **Chưa đo kho lớn thật trên một chiều:** số liệu một chiều 100.000 là ngoại suy tuyến tính từ 50.000.
3. **Tìm nhanh bằng `LIKE '%...%'`** quét toàn bảng (0,12–0,18 s ở 100.000); chấp nhận được, nếu kho lớn hơn nhiều thì cần chỉ mục toàn văn.
4. **Tóm tắt vẫn tính trên toàn bộ kết quả lọc** (tổng tiền, số lỗi) mỗi lần tra cứu; một cách giảm thêm là tính nền, chưa cần ở quy mô này.
5. **Chưa đo:** NFR-PERF-001 ở mức giao diện (vẽ lưới, tab PDF; mới đo dịch vụ), mở ứng dụng bản Release trên máy chuẩn, và hai ý còn thiếu của NFR-REL-006. Nên đo khi có bản Release chạy với kho thật.
