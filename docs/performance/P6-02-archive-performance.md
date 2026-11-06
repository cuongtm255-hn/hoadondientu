# P6-02 — Hiệu năng kho 100.000 hóa đơn (đợt 1, 06/11/2026)

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

## Giới hạn và điểm cần theo dõi

1. **Lần truy cập đầu khi bộ nhớ đệm hệ điều hành còn nguội (cả mở trang đầu lẫn chuyển kho, đo được 3,2–3,3 s):** trong 20 lần đo của lượt chạy sau tối ưu có một mẫu chậm 3,3 giây (không ghi lại là lần thứ mấy, nhiều khả năng là lần đầu khi bộ nhớ đệm hệ điều hành còn nguội). p95 của 20 mẫu không phản ánh mẫu này. Người dùng mở kho lần đầu sau khi khởi động máy, nhất là ổ cứng cơ, có thể gặp độ trễ đó; chưa đo trên cấu hình triển khai.
2. **Chưa đo kho lớn thật trên một chiều:** số liệu một chiều 100.000 là ngoại suy tuyến tính từ 50.000.
3. **Tìm nhanh bằng `LIKE '%...%'`** quét toàn bảng (0,12–0,18 s ở 100.000); chấp nhận được, nếu kho lớn hơn nhiều thì cần chỉ mục toàn văn.
4. **Tóm tắt vẫn tính trên toàn bộ kết quả lọc** (tổng tiền, số lỗi) mỗi lần tra cứu; một cách giảm thêm là tính nền, chưa cần ở quy mô này.
5. **Chưa đo:** NFR-PERF-003 (mở ứng dụng ≤ 5 s), NFR-PERF-006 (danh mục từ cache), NFR-PERF-001 (UI không treo, cần chạy giao diện), cùng các NFR-REL ngoài REL-006. Nên đo khi có bản Release chạy với kho thật.
