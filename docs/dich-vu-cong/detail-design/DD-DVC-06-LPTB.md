# DD-DVC-06 — Thông báo lệ phí trước bạ (FT-DVC-07)

**Trạng thái:** bản đọc chung đã làm (27/11/2026), **chưa kiểm chứng với dữ liệu thật** vì tài khoản thử không có thông báo LPTB. Khảo sát: [khao-sat-tthc.md, bổ sung 27/11/2026 (LPTB)](../api-samples/khao-sat-tthc.md).

## 1. Thiết kế

Nguồn thứ năm (`HoSoSource.Lptb`) của màn "Tra cứu hồ sơ đã nộp" ([DD-DVC-03](DD-DVC-03-SUBMITTED-FILES.md)), cùng cách làm [DD-DVC-04](DD-DVC-04-DRAFTS.md) và [DD-DVC-05](DD-DVC-05-FEEDBACK.md). **Chỉ đọc thông báo của chính người đăng nhập**: không có nộp tiền, OTP, ký số, chọn nhiều hồ sơ.

| Việc | Cách xử lý |
|---|---|
| CAPTCHA | Không có |
| Ô lọc | Mã hồ sơ, mã trạng thái (01–06, trống là tất cả), MST người bán, từ/đến ngày (mặc định 30 ngày gần nhất, luôn gửi). Số hóa đơn và ký hiệu hóa đơn có trong `HoSoQuery` nhưng chưa có ô trên màn hình |
| Yêu cầu | Trang không dùng htmx: `POST /tthc/tchs/thong-bao-lptb` **thân JSON** (`PortalHoSoSource.BuildLptbSearchScript`), header CSRF và `X-Requested-With`. `loaiTraCuu` cố định `01`; `mst` lấy từ ô có sẵn của trang (MST người đăng nhập), người dùng không nhập |
| Không làm | Kiểu `02` (người nộp thuế khác, từng qua OTP), nút nộp tiền, OTP, ký số |
| Đọc kết quả | Bộ đọc bảng bằng DOM, chế độ lỏng: bảng có tiêu đề nhưng không dòng (hoặc chỉ một dòng một ô "Không có dữ liệu") là kết quả rỗng; bỏ cột STT và Thao tác (cột "Chọn" vẫn hiện vì chưa biết nội dung) |
| Phân trang | Chỉ trang đầu: không thấy dòng tổng để biết số trang (thân JSON có `pageNumber`/`pageSize`, chưa thử) |

## 2. Chưa kiểm chứng (cần tài khoản có thông báo LPTB)

- Dòng dữ liệu thật: số ô so với 14 tiêu đề, cột "Chọn" và "Hướng dẫn" chứa gì.
- Có dòng tổng/phân trang khi có nhiều dòng không; yêu cầu đổi trang (`pageNumber`) chưa viết.
- Cổng có bắt `mst` đúng MST đăng nhập không (app gửi đúng giá trị trang điền sẵn).

## 3. Bảng tiến độ

| Mã | Việc | Trạng thái |
|---|---|---|
| TD06-01 | Nguồn `Lptb` trong Domain/Application | ✅ |
| TD06-02 | `PortalHoSoSource` gửi JSON tới `/tthc/tchs/thong-bao-lptb` (lỏng) | ✅ (test với runner giả) |
| TD06-03 | Màn hình: mục nguồn, ô trạng thái, MST người bán, ẩn CAPTCHA, cảnh báo chưa kiểm chứng | ✅ |
| TD06-04 | Mô phỏng Debug (`MockHoSoSource.SearchLptb`, dòng "(mẫu)") và xuất Excel | ✅ |
| TD06-05 | Kiểm chứng dòng dữ liệu và đổi trang với dữ liệu thật; ô số/ký hiệu hóa đơn | ⬜ cần tài khoản có thông báo LPTB |
