# Danh sách tính năng — nhóm Dịch vụ công (FT-DVC)

| Thuộc tính | Giá trị |
|---|---|
| Nhóm | Tra cứu Dịch vụ công thuế nhà nước ([README](../README.md)) |
| Ngày | 16/11/2026 |
| Trạng thái | **Đề xuất**, dựa trên [khảo sát](../api-samples/khao-sat-tthc.md); đã có [SRS-DVC](SRS-DVC.md) (nháp), chưa có DD |
| Tách khỏi | Nhóm Hóa đơn: [FEATURE-LIST FT-00…FT-12](../../requirements/FEATURE-LIST.md) |

Ưu tiên: **M** bắt buộc · **S** nên có · **C** có thể làm sau.

## 1. Tổng quan

| Mã | Tính năng | Cổng | CAPTCHA | Dữ liệu thấy khi khảo sát | Ưu tiên | Ảnh |
|---|---|---|---|---|:---:|---|
| FT-DVC-00 | Chọn nhóm chức năng khi khởi động | — (phần mềm) | — | — | M | — |
| FT-DVC-01 | Đăng nhập cổng Dịch vụ công | `POST /tthc/loginLDAP` | Có | — | M | — |
| FT-DVC-02 | Danh mục thủ tục hành chính + chi tiết | `POST /tthc/thu-tuc-hanh-chinh`, `GET .../chi-tiet-tthc?maTTHC=` | Không | **Có** (147 bản ghi) | M | DVC-02 |
| FT-DVC-03 | Hồ sơ đã nộp (trên DVC; trên thuế điện tử) | `GET /tthc/ho-so/search` (suy ra), `POST /tthc/tchs/thuedientu` | Có | Rỗng | M | DVC-03a/b |
| FT-DVC-04 | Hồ sơ lưu tạm | `POST /tthc/tchs/draft` | Không | Rỗng | S | DVC-04 |
| FT-DVC-05 | Phản ánh quyết toán gửi CQT | `POST /tthc/tchs/pa/tracuu` | Không | Rỗng | S | DVC-05 |
| FT-DVC-06 | TTHC đất đai | chưa quan sát | Không | Chưa thử | C | DVC-06 |
| FT-DVC-07 | Thông báo lệ phí trước bạ | `POST /tthc/tchs/thong-bao-lptb` | Không | Rỗng | S | DVC-07 |
| FT-DVC-08 | Xuất kết quả tra cứu (Excel) | app tự xuất từ bảng đã đọc; **đã làm cho danh mục TTHC** ([DD-DVC-02 mục 12](../detail-design/DD-DVC-02-TTHC-CATALOG.md)), các mục khác làm cùng bộ ghi khi có dữ liệu | — | — | C | — |
| FT-DVC-09 | Các mục "Tra cứu" trong Dịch vụ khác | `/tthc/dich-vu-khac` (liên kết `#`) | ? | Chưa khảo sát | C | DVC-08 |

## 2. Mô tả

### FT-DVC-00 — Chọn nhóm chức năng khi khởi động

- **Mô tả:** Sau kiểm tra bản quyền, hiển thị màn chọn **Tra cứu hóa đơn** hoặc **Tra cứu Dịch vụ công thuế nhà nước**. Màn luôn hiện mỗi lần mở, chọn sẵn nhóm dùng gần nhất; cho đổi nhóm không cần thoát phần mềm. Mỗi nhóm có shell, phiên đăng nhập, WebView và dữ liệu riêng.
- **Quy tắc (đã chốt, xem SRS-DVC mục 18):**
  - Hai nhóm **không dùng chung phiên**; đổi nhóm **giữ** phiên của nhóm kia.
  - Doanh nghiệp đang chọn (MST) dùng chung cho cả hai nhóm; tên đăng nhập DVC điền sẵn MST nhưng cho sửa (có thể có hậu tố).
  - Bản quyền: một mã kích hoạt cho cả hai nhóm, hết dùng thử khóa cả hai.
- **Tác động tài liệu nhóm Hóa đơn (chưa sửa, cần duyệt):** FT-01 (khung, điều hướng) và SRS mục khởi động/điều hướng sẽ phải thêm bước chọn nhóm.

### FT-DVC-01 — Đăng nhập cổng Dịch vụ công

- **Mô tả:** Đăng nhập tài khoản **Thuế điện tử – Doanh nghiệp** (đã quan sát). Request: `POST /tthc/loginLDAP` (XHR, JSON) với `tenDN`, `matKhau`, `doiTuong`, `captcha`; phản hồi có khóa `status`; sau đó trang chuyển về `/tthc/home`. Ảnh CAPTCHA: `GET /tthc/getCaptcha?<timestamp>`.
- **Hướng thiết kế:** form riêng của app + WebView ngầm gọi hàm đăng nhập của cổng, người dùng tự gõ mật khẩu và CAPTCHA (cùng nguyên tắc ADR 0011 của nhóm Hóa đơn, nhưng cài đặt riêng). Xem [ADR DVC-0001](../decisions/0001-htmx-webview-integration.md).
- **Giới hạn đã biết:** chỉ một tab hoạt động mỗi tài khoản (kiểm bằng `localStorage.APP_SINGLE_TAB_ACTIVE` và `sessionStorage.TAB_ID` ở phía trang); WebView phải tránh hai trang cùng hoạt động.
- **Chưa biết:** kiểu thân gửi đi, giá trị `doiTuong` (Cá nhân/Doanh nghiệp), CSRF, thông báo lỗi sai mật khẩu/CAPTCHA, thời hạn phiên, đăng nhập Cá nhân, VNeID.
- **Ngoài phạm vi:** tài khoản Công chức thuế.

### FT-DVC-02 — Danh mục thủ tục hành chính

- **Bộ lọc:** Tên/Mã TTHC, Loại TTHC, Tờ khai, Mức độ, Loại người nộp thuế.
- **Kết quả:** bảng STT, Mã, Tên thủ tục, Mức độ, Cơ quan thực hiện, Nộp hồ sơ; phân trang 10 dòng/trang (147 bản ghi mặc định). Chi tiết theo mã: Cấp thực hiện, Trình tự, Cách thức, Thành phần hồ sơ, Đối tượng, Cơ quan, Địa chỉ tiếp nhận, Kết quả, Điều kiện, Căn cứ pháp lý.
- **Ghi chú:** nút "Nộp hồ sơ" không thuộc phạm vi tra cứu.

### FT-DVC-03 — Hồ sơ đã nộp

- **Tab trên DVC:** Loại TTHC, Tên TTHC, Tờ khai, Mã hồ sơ, Từ/Đến ngày (mặc định đầu tháng đến hôm nay), MST ủy quyền, **CAPTCHA**.
- **Tab trên thuế điện tử:** Tờ khai, Mã giao dịch, Từ/Đến ngày (mặc định tháng 6/2025), MST ủy quyền, **CAPTCHA**. Chỉ hỗ trợ tờ khai nộp trước 01/07/2025.
- **Điểm khác với nhóm Hóa đơn:** mỗi lần tìm cần một CAPTCHA do người dùng gõ nên không thể chia kỳ và tra cứu hàng loạt như hóa đơn.

### FT-DVC-04 — Hồ sơ lưu tạm

- Tờ khai, Tên TTHC, Từ/Đến ngày (tùy chọn). Lưu ý của cổng: chỉ "Nộp hồ sơ" khi hồ sơ ở trạng thái "Đã kiểm tra thông tin kê khai".

### FT-DVC-05 — Phản ánh quyết toán gửi CQT

- Năm quyết toán (2021–2025), MST tổ chức trả thu nhập, ngày gửi từ/đến, nguồn dữ liệu (DVC hoặc Thuế điện tử); kết quả có "Trang x/N – Tổng số bản ghi".

### FT-DVC-06 — TTHC đất đai

- Mã hồ sơ (bắt buộc) kèm Mã số thuế **hoặc** Số giấy tờ. Chưa quan sát request.

### FT-DVC-07 — Thông báo lệ phí trước bạ

- Hình thức (của người nộp thuế / của người nộp thuế khác), MST (điền sẵn), Mã giao dịch điện tử, Trạng thái thông báo, Ngày hóa đơn từ/đến (bắt buộc, mặc định 30 ngày). Kết quả: Mã giao dịch điện tử, MST, Tên NNT, Loại tài sản, Số khung, Số máy, Số tiền nộp LPTB, Trạng thái.

### FT-DVC-08 — Xuất kết quả tra cứu

- Chưa xác định: cổng có nút xuất hay không; nếu không thì app tự xuất Excel từ bảng đã đọc.

### FT-DVC-09 — Dịch vụ khác (tra cứu)

- Lập/phê duyệt/tra cứu GNT, thông báo, đề nghị tra soát, bù trừ nộp thừa, giấy đề nghị xác nhận... Chưa khảo sát.

## 3. Phụ thuộc và thứ tự đề xuất

FT-DVC-00 → FT-DVC-01 → FT-DVC-02 (có dữ liệu thật, không CAPTCHA, nên làm trước; bản công khai `/tthc/homelogin/tthc` khi chưa đăng nhập chưa được kiểm tra) → FT-DVC-04/05/07 → FT-DVC-03 (cần CAPTCHA) → còn lại.
