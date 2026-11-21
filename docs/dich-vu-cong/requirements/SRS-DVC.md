# Đặc tả yêu cầu phần mềm — Nhóm Dịch vụ công (SRS-DVC)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Tên nhóm chức năng | Tra cứu Dịch vụ công thuế nhà nước (DVC) |
| Mã tài liệu | SRS-DVC-001 |
| Phiên bản | 0.2 (nháp; đã chốt 7 câu hỏi mở ở mục 18) |
| Ngày | 16/11/2026 |
| Trạng thái | **Nháp**, chưa baseline. Dựa trên [khảo sát](../api-samples/khao-sat-tthc.md) và [ADR DVC-0001](../decisions/0001-htmx-webview-integration.md) (đề xuất) |
| Nền tảng | Cùng ứng dụng Windows Forms, C#, .NET Framework 4.5.2 với nhóm Hóa đơn |
| Tách khỏi | [SRS-HDDT-001](../../requirements/SRS.md) của nhóm Hóa đơn: hai tài liệu độc lập, chỉ giao nhau ở khung ứng dụng và bản quyền |

Quy ước nhãn: **[Đã thấy]** điều quan sát được ở cổng ngày 16/11/2026; **[Giả định]** điều suy ra hoặc chưa kiểm chứng, phải xác nhận ở cổng kỹ thuật (mục 17).

## 2. Mục đích

Cho phép người dùng (kế toán thuế) tra cứu trong phần mềm các thông tin của cổng Dịch vụ công ngành Thuế `dichvucong.gdt.gov.vn/tthc/`: danh mục thủ tục hành chính, hồ sơ đã nộp, hồ sơ lưu tạm, phản ánh quyết toán, thông báo lệ phí trước bạ và tra cứu TTHC đất đai; xem kết quả trong ứng dụng và xuất ra Excel.

Khác nhóm Hóa đơn: không có kho dữ liệu dài hạn và không có hàng đợi tải hàng loạt. Dữ liệu chủ yếu được tra cứu tại thời điểm cần và hiển thị ngay.

## 3. Phạm vi

### 3.1 Trong phạm vi phiên bản 1 của nhóm DVC

- Màn chọn nhóm chức năng khi khởi động: **Tra cứu hóa đơn** hoặc **Tra cứu Dịch vụ công thuế** (FR-DVC-NAV).
- Đăng nhập cổng DVC bằng tài khoản **Thuế điện tử – Doanh nghiệp** (FR-DVC-AUTH).
- Tra cứu và xem chi tiết danh mục thủ tục hành chính (FR-DVC-TTHC).
- Tra cứu hồ sơ đã nộp (tab "trên DVC", tab "trên thuế điện tử"), hồ sơ lưu tạm, phản ánh, thông báo LPTB, TTHC đất đai (FR-DVC-HS, FR-DVC-DRAFT, FR-DVC-PA, FR-DVC-LPTB, FR-DVC-DD).
- Hiển thị kết quả dạng bảng có phân trang, sắp xếp cục bộ, xuất Excel (FR-DVC-EXP).
- Chạy hoàn toàn bằng dữ liệu giả ở bản Debug, có nhãn MÔ PHỎNG (BR-DVC-08).

### 3.2 Ngoài phạm vi phiên bản 1

- **Mọi thao tác ghi**: nộp hồ sơ, sửa/xóa hồ sơ lưu tạm, gửi phản ánh, đăng ký dịch vụ. Nút "Nộp hồ sơ" của cổng không được gọi.
- Giải CAPTCHA, lưu hoặc nhớ mật khẩu, tự động hóa tra cứu hàng loạt cần CAPTCHA.
- Tài khoản Cá nhân, VNeID, Công chức thuế (chưa quan sát luồng đăng nhập; có thể bổ sung sau).
- Nhóm "Dịch vụ khác" (Lập/Phê duyệt GNT, tra soát, bù trừ...), tin tức, đánh giá chất lượng dịch vụ, trợ giúp, đăng ký dịch vụ.
- Lưu trữ lâu dài kết quả tra cứu và đồng bộ với kho hóa đơn.
- Dùng chung phiên đăng nhập, kho dữ liệu hay hàng đợi với nhóm Hóa đơn.

## 4. Tài liệu và nguồn yêu cầu

| Tài liệu | Vai trò |
|---|---|
| [khao-sat-tthc.md](../api-samples/khao-sat-tthc.md) | Nguồn chính: chức năng, request, luồng đăng nhập, ảnh giao diện |
| [FEATURE-LIST.md](FEATURE-LIST.md) | Danh sách `FT-DVC-xx` |
| [ADR DVC-0001](../decisions/0001-htmx-webview-integration.md) | Cách tích hợp: phiên WebView + đọc HTML |
| [SRS nhóm Hóa đơn](../../requirements/SRS.md), [ADR 0007](../../decisions/0007-webview-cefsharp.md), [0011](../../decisions/0011-own-login-form-hidden-webview.md), [0003](../../decisions/0003-xlsx-and-zip-generation.md), [0010](../../decisions/0010-simple-offline-license.md) | Thư viện và nguyên tắc dùng lại (WebView, xuất Excel, bản quyền); không dùng chung dữ liệu |
| `CLAUDE.md` | Quy tắc kiểm thử mock-only, không ghi bí mật |

## 5. Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| DVC | Cổng Dịch vụ công ngành Thuế, `dichvucong.gdt.gov.vn/tthc/` |
| TTHC | Thủ tục hành chính; mã dạng `1.010339` |
| Nhóm chức năng | Một trong hai phần của phần mềm: Hóa đơn, Dịch vụ công |
| Mảnh HTML | Phản hồi htmx của cổng: đoạn HTML (bảng kết quả) thay vì JSON |
| LPTB | Lệ phí trước bạ |
| QTT | Quyết toán thuế |
| GNT | Giấy nộp tiền |
| Phiên DVC | Cookie đăng nhập cổng DVC giữ trong WebView |

## 6. Tổng quan sản phẩm

### 6.1 Kiến trúc logic (đề xuất)

```
Màn chọn nhóm ─► Shell DVC (WinForms) ─► Dịch vụ tra cứu DVC (tham số, kiểm hợp lệ, phân trang)
                                          └► Cổng DVC (giao diện trừu tượng)
                                              ├ Thật: WebView ngầm ─ điền form/POST trong trang ─ đọc mảnh HTML ─ bộ phân tích HTML
                                              └ Giả (Debug): HTML mẫu có nhãn MÔ PHỎNG
```

- Bộ phân tích HTML tách khỏi giao diện, kiểm thử bằng mẫu HTML đã làm sạch.
- Không dùng `HttpClient` với cookie sao chép từ WebView (ADR DVC-0001, phương án 2).

### 6.2 Người dùng

Cùng nhóm Hóa đơn: kế toán thuế doanh nghiệp, công ty dịch vụ kế toán. Mặc định một doanh nghiệp (MST) đang chọn; tài khoản đăng nhập DVC không bắt buộc trùng MST (**[Đã thấy]** tên đăng nhập có dạng MST kèm hậu tố).

### 6.3 Môi trường vận hành

Giống nhóm Hóa đơn: Windows 7 trở lên, .NET Framework 4.5.2, CefSharp 109 (x86), TLS 1.2, cần Internet và truy cập được `dichvucong.gdt.gov.vn`.

### 6.4 Ràng buộc

- **[Đã thấy]** Cổng chỉ cho **một tab hoạt động** mỗi tài khoản, kiểm bằng `localStorage.APP_SINGLE_TAB_ACTIVE` và `sessionStorage.TAB_ID` ở phía trang. Đăng nhập hoặc mở cổng ở nơi khác sẽ đá phiên đang dùng.
- **[Đã thấy]** Phản hồi là HTML; giao diện cổng đổi thì bộ phân tích có thể hỏng.
- **[Đã thấy]** Tra cứu hồ sơ đã nộp (cả hai tab) bắt buộc CAPTCHA mỗi lần tìm.
- **[Đã thấy]** Các mục lưu tạm, phản ánh, LPTB, danh mục TTHC không đòi CAPTCHA ở thời điểm khảo sát.
- Công cụ ngoài (extension khảo sát) không đọc được tên trường CSRF và nội dung request; phải xác nhận ở cổng G-DVC-03.

## 7. Quy tắc nghiệp vụ

| Mã | Quy tắc |
|---|---|
| BR-DVC-01 | Hai nhóm chức năng độc lập: không chia sẻ phiên đăng nhập, WebView, kho dữ liệu, hàng đợi. Đổi nhóm không tự đăng xuất nhóm kia **[Giả định, cần chốt]**. |
| BR-DVC-02 | **Chỉ đọc**: ứng dụng không gửi bất kỳ yêu cầu làm thay đổi dữ liệu trên cổng (nộp, sửa, xóa, gửi). Danh sách lệnh/địa chỉ được phép là danh sách trắng cố định trong mã. |
| BR-DVC-03 | CAPTCHA luôn do người dùng đọc và gõ; ứng dụng không giải, không lưu, không tái dùng mã. Mỗi lần tìm mục có CAPTCHA dùng một mã mới. |
| BR-DVC-04 | Mật khẩu chỉ đi từ ô nhập tới lời gọi đăng nhập trong WebView; không lưu, không ghi nhật ký, không vào tham số dòng lệnh hay URL (cùng nguyên tắc ADR 0011). |
| BR-DVC-05 | Mọi yêu cầu tới cổng thực hiện **tuần tự**, một yêu cầu tại một thời điểm; không tự động thử lại các yêu cầu có CAPTCHA. |
| BR-DVC-06 | Ứng dụng chỉ giữ **một** trang DVC hoạt động trong WebView; không mở cổng DVC ở cửa sổ/tab thứ hai. |
| BR-DVC-07 | Kết quả tra cứu mặc định chỉ giữ trong bộ nhớ khi màn hình mở và bị xóa khi đóng nhóm hoặc đăng xuất; chỉ ghi ra đĩa khi người dùng chủ động xuất (FR-DVC-EXP). |
| BR-DVC-08 | Bản Debug/test chỉ chạy dữ liệu giả, hiện nhãn "TEST/MÔ PHỎNG", mã giả không thể gọi từ Release; cổng thật chỉ ở Release hoặc khi người dùng tự đăng nhập lần đầu để kiểm tra (theo `CLAUDE.md`). |
| BR-DVC-09 | Không trộn dữ liệu và tài liệu: mã `FT-DVC-`, `FR-DVC-`, `BR-DVC-`, `AC-DVC-`, `G-DVC-` thuộc riêng nhóm này. |
| BR-DVC-10 | Giá trị người dùng nhập (mã hồ sơ, MST, số giấy tờ) được xem là dữ liệu nhạy cảm: không ghi nhật ký, không đưa vào URL. |

## 8. Yêu cầu chức năng

### 8.1 Chọn nhóm chức năng (FR-DVC-NAV) — FT-DVC-00

- FR-DVC-NAV-001: Sau kiểm tra bản quyền và khi doanh nghiệp đã được xác định, hiển thị màn chọn nhóm với hai thẻ: **Tra cứu hóa đơn** và **Tra cứu Dịch vụ công thuế nhà nước**.
- FR-DVC-NAV-002: Lựa chọn mở shell của nhóm tương ứng; nhóm kia không được nạp tài nguyên của nó (WebView, dịch vụ) cho tới khi chọn.
- FR-DVC-NAV-003: Có lệnh "Đổi nhóm chức năng" ở shell mỗi nhóm; khi đổi, các tác vụ đang chạy của nhóm hiện tại dừng theo xác nhận của người dùng, nhưng **phiên đăng nhập và WebView của nhóm đó được giữ** để quay lại không phải đăng nhập lại (quyết định mục 18 #3). Kết quả tra cứu trong bộ nhớ cũng được giữ cho tới khi đăng xuất hoặc thoát ứng dụng.
- FR-DVC-NAV-004: Màn chọn nhóm **hiện mỗi lần mở ứng dụng**, thẻ nhóm dùng gần nhất được chọn sẵn (Enter để vào). Nhớ nhóm gần nhất trong cài đặt người dùng.
- FR-DVC-NAV-005: Một mã kích hoạt mở cả hai nhóm; hết dùng thử mà chưa kích hoạt thì khóa cả hai nhóm qua `ActivationGate` hiện có (ADR 0010), không có kiểm tra quyền riêng từng nhóm.

### 8.2 Đăng nhập cổng DVC (FR-DVC-AUTH) — FT-DVC-01

- FR-DVC-AUTH-001: Form đăng nhập riêng của app (chỉ loại Thuế điện tử – Doanh nghiệp ở v1): tên đăng nhập (điền sẵn MST doanh nghiệp đang chọn, cho sửa; app chỉ nhớ tên đăng nhập, không nhớ mật khẩu), mật khẩu (che ký tự, có nút hiện/ẩn), ảnh CAPTCHA, nút đổi mã, nút Đăng nhập. Mật khẩu và CAPTCHA do người dùng tự gõ.
- FR-DVC-AUTH-002: App nạp trang cổng trong WebView ngầm, lấy ảnh CAPTCHA (`GET /tthc/login/getCaptcha?<tem thời gian>`) và hiển thị trên form riêng **[Giả định]**; nếu lấy ảnh thất bại, đề nghị dùng đăng nhập trực tiếp trên trang cổng.
- FR-DVC-AUTH-003: Khi bấm Đăng nhập, app gọi `POST /tthc/loginLDAP` **trong trang**, dạng **form urlencoded**, 4 trường `tenDN`, `matKhau` (**base64 của mật khẩu UTF-8**), `doiTuong` (`DN` cho Doanh nghiệp), `captcha` **[Đã xác nhận 21/11/2026]**.
- FR-DVC-AUTH-004: Thành công khi phản hồi JSON có `status` = `"200"` (về `/tthc/home`) hoặc `"201"` (kèm `value`); giá trị khác là lỗi, thông báo ở `desc`; sai CAPTCHA là `"999"` kèm `desc` "Mã captcha không đúng, vui lòng thử lại." **[Đã xác nhận 21/11/2026; `desc` của sai mật khẩu/khóa chưa thấy]**. Hiển thị tên tổ chức do cổng trả để người dùng đối chiếu.
- FR-DVC-AUTH-005: Thất bại (sai mật khẩu/CAPTCHA, khóa tài khoản, lỗi mạng): hiển thị thông báo rõ, xóa ô mật khẩu và CAPTCHA, tải CAPTCHA mới.
- FR-DVC-AUTH-006: Phương án dự phòng: nút "Đăng nhập trên trang cổng" hiển thị WebView thường (chọn Thuế điện tử → Doanh nghiệp) để người dùng tự thao tác.
- FR-DVC-AUTH-007: Phát hiện hết phiên hoặc bị đá do tab khác: khi trang trả về trang đăng nhập hoặc thông báo "đang được sử dụng trên một tab khác" thì dừng tác vụ, báo người dùng và mời đăng nhập lại; không tự đăng nhập lại.
- FR-DVC-AUTH-008: Đăng xuất: gọi lệnh đăng xuất của cổng (nếu có) rồi xóa cookie/phiên của WebView DVC.
- FR-DVC-AUTH-009: Phiên DVC không dùng chung với phiên cổng Hóa đơn (BR-DVC-01).

### 8.3 Danh mục thủ tục hành chính (FR-DVC-TTHC) — FT-DVC-02

- FR-DVC-TTHC-001: Bộ lọc: Tên/Mã TTHC (văn bản), Loại TTHC, Tờ khai, Mức độ, Loại người nộp thuế **[Đã thấy]**. Danh sách giá trị của các ô chọn lấy từ chính trang cổng, không mã hóa cứng.
- FR-DVC-TTHC-002: Tìm kiếm gửi `POST /tthc/thu-tuc-hanh-chinh`; hiển thị bảng: STT, Mã, Tên thủ tục, Mức độ, Cơ quan thực hiện; tổng số bản ghi/TTHC và số trang **[Đã thấy]**.
- FR-DVC-TTHC-003: Phân trang theo cổng (10 dòng mỗi trang **[Đã thấy]**), có nhảy tới trang; không tự tải trước các trang chưa xem.
- FR-DVC-TTHC-004: Xem chi tiết một TTHC (`GET /tthc/thu-tuc-hanh-chinh/chi-tiet-tthc?maTTHC=<mã>`): Mã, Cấp thực hiện, Trình tự thực hiện, Cách thức thực hiện, Thành phần hồ sơ, Đối tượng, Cơ quan thực hiện, Địa chỉ tiếp nhận, Kết quả, Điều kiện, Căn cứ pháp lý **[Đã thấy]**.
- FR-DVC-TTHC-005: Văn bản chi tiết hiển thị an toàn: HTML của cổng được chuyển thành văn bản/định dạng tối thiểu, không thực thi script, không nạp tài nguyên ngoài.
- FR-DVC-TTHC-006: Không hiển thị và không gọi nút "Nộp hồ sơ" (BR-DVC-02).
- FR-DVC-TTHC-007: Có thể chạy khi đã đăng nhập; khi chưa đăng nhập, thử bản công khai `/tthc/homelogin/tthc` **[Giả định]**; nếu không có thì yêu cầu đăng nhập.

### 8.4 Hồ sơ đã nộp (FR-DVC-HS) — FT-DVC-03

- FR-DVC-HS-001: Hai tab giống cổng: "Tra cứu hồ sơ đã nộp trên DVC" và "trên thuế điện tử".
- FR-DVC-HS-002 (tab DVC): bộ lọc Loại TTHC, Tên TTHC, Tờ khai, Mã hồ sơ, Từ/Đến ngày (mặc định đầu tháng → hôm nay), MST ủy quyền (nếu có), CAPTCHA; gửi `GET /tthc/ho-so/search` **[Giả định: suy từ `hx-get` của form]**.
- FR-DVC-HS-003 (tab thuế điện tử): Tờ khai, Mã giao dịch, Từ/Đến ngày (mặc định 01–30/06/2025), MST ủy quyền, CAPTCHA; gửi `POST /tthc/tchs/thuedientu`. Hiển thị cảnh báo của cổng: chỉ hỗ trợ tờ khai nộp trước 01/07/2025.
- FR-DVC-HS-004: Mỗi lần tìm yêu cầu một CAPTCHA mới do người dùng gõ (BR-DVC-03); sai CAPTCHA thì tải mã mới và giữ nguyên các bộ lọc khác.
- FR-DVC-HS-005: Kết quả rỗng hiển thị đúng thông điệp của cổng: tab DVC "Không có dữ liệu" (Tổng số bản ghi: 0); tab thuế điện tử "Không có dữ liệu thỏa mãn điều kiện tìm kiếm!" **[Đã thấy]**.
- FR-DVC-HS-006: Cột của bảng kết quả có dữ liệu chưa biết **[G-DVC-04]**; thiết kế bảng sau khi có mẫu thật.

### 8.5 Hồ sơ lưu tạm (FR-DVC-DRAFT) — FT-DVC-04

- FR-DVC-DRAFT-001: Bộ lọc Tờ khai, Tên TTHC, Từ ngày, Đến ngày (đều tùy chọn); gửi `POST /tthc/tchs/draft` **[Đã thấy]**.
- FR-DVC-DRAFT-002: Hiển thị lưu ý của cổng: chỉ "Nộp hồ sơ" khi hồ sơ ở trạng thái "Đã kiểm tra thông tin kê khai"; ứng dụng chỉ hiển thị, không thực hiện nộp.
- FR-DVC-DRAFT-003: Kết quả rỗng: "Không có dữ liệu".

### 8.6 Phản ánh (FR-DVC-PA) — FT-DVC-05

- FR-DVC-PA-001: Bộ lọc: Năm quyết toán (danh sách do cổng cung cấp, hiện 2021–2025), MST tổ chức trả thu nhập, Ngày gửi phản ánh từ/đến, Nguồn dữ liệu ("trên DVC" hoặc "trên Thuế điện tử"); gửi `POST /tthc/tchs/pa/tracuu` **[Đã thấy]**.
- FR-DVC-PA-002: Hiển thị "Trang x/N – Tổng số bản ghi" và bảng kết quả; cột chưa biết khi có dữ liệu **[G-DVC-04]**.

### 8.7 Thông báo lệ phí trước bạ (FR-DVC-LPTB) — FT-DVC-07

- FR-DVC-LPTB-001: Hình thức tra cứu: "của người nộp thuế" hoặc "của người nộp thuế khác"; MST điền sẵn và khóa ở chế độ thứ nhất; chế độ thứ hai cần xác định thêm trường **[G-DVC-04]**.
- FR-DVC-LPTB-002: Bộ lọc: Mã giao dịch điện tử, Trạng thái thông báo (danh sách do cổng), Ngày hóa đơn từ/đến (**bắt buộc**, mặc định 30 ngày); gửi `POST /tthc/tchs/thong-bao-lptb` **[Đã thấy]**.
- FR-DVC-LPTB-003: Cột kết quả: STT, Chọn, Mã giao dịch điện tử, Mã số thuế, Tên người nộp thuế, Loại tài sản, Số khung, Số máy, Số tiền nộp LPTB, Trạng thái thông báo **[Đã thấy]**. Cột "Chọn" không dùng để thao tác ghi.
- FR-DVC-LPTB-004: Hiển thị ghi chú của cổng: chỉ áp dụng ô tô, xe máy chưa qua sử dụng (xe mới).

### 8.8 TTHC đất đai (FR-DVC-DD) — FT-DVC-06

- FR-DVC-DD-001: Trường: Mã hồ sơ (bắt buộc), Mã số thuế, Số giấy tờ; **phải nhập ít nhất một trong hai** MST/Số giấy tờ, nếu thiếu hiện "Yêu cầu nhập thông tin Mã số thuế hoặc Số giấy tờ!" **[Đã thấy, kiểm ở phía trình duyệt]**.
- FR-DVC-DD-002: Request và dạng kết quả chưa quan sát; yêu cầu bổ sung sau G-DVC-04. Ưu tiên C.

### 8.9 Hiển thị và xuất kết quả (FR-DVC-EXP) — FT-DVC-08

- FR-DVC-EXP-001: Bảng kết quả hỗ trợ phân trang theo cổng, sắp xếp cục bộ theo cột đã tải, sao chép ô/dòng.
- FR-DVC-EXP-002: Xuất kết quả **đang hiển thị** (các trang đã tải) ra `.xlsx` bằng bộ tạo Excel đã có (ADR 0003): sheet có dòng tiêu đề cột đúng thứ tự trên màn hình, kèm dòng ghi bộ lọc và thời điểm tra cứu. Tuỳ chọn "xuất tất cả các trang" chạy tuần tự (BR-DVC-05), không áp dụng cho mục có CAPTCHA mỗi trang.
- FR-DVC-EXP-003: Không ghi đè file có sẵn khi chưa xác nhận.
- FR-DVC-EXP-004: Nếu cổng có nút xuất riêng thì ưu tiên đối chiếu bố cục **[G-DVC-05]**.

### 8.10 Dịch vụ khác (FR-DVC-OTH) — FT-DVC-09, ưu tiên C

- Các mục "Tra cứu" trong "Dịch vụ khác" chỉ bổ sung sau khi khảo sát riêng; chưa có yêu cầu chi tiết.

## 9. Dữ liệu

- Không có cơ sở dữ liệu riêng của nhóm DVC trong phiên bản 1 (BR-DVC-07).
- Cài đặt riêng nhóm DVC (lưu cùng tệp cấu hình ứng dụng, tách khóa): nhóm dùng gần nhất, kích thước cột, thư mục xuất gần nhất.
- Mô hình bộ nhớ: `DvcQuery` (loại tra cứu + tham số), `DvcPage<T>` (dòng, tổng, trang), `ProcedureSummary`, `ProcedureDetail`; kiểu dòng kết quả cho từng mục xác định sau G-DVC-04.
- Không có tệp XML hay PDF cần lưu.

## 10. Màn hình và điều hướng

| Mã (đề xuất) | Màn hình | Ảnh mockup |
|---|---|---|
| SCR-DVC-00 | Chọn nhóm chức năng | chưa có |
| SCR-DVC-01 | Đăng nhập cổng DVC | chưa có (khảo sát: form `modalLdap`) |
| SCR-DVC-02 | Danh mục TTHC + chi tiết | `DVC-02-*.jpg` |
| SCR-DVC-03 | Hồ sơ đã nộp (2 tab) | `DVC-03a/b-*.jpg` |
| SCR-DVC-04 | Hồ sơ lưu tạm | `DVC-04-luu-tam.jpg` |
| SCR-DVC-05 | Phản ánh | `DVC-05-phan-anh.jpg` |
| SCR-DVC-06 | TTHC đất đai | `DVC-06-*.jpg` |
| SCR-DVC-07 | Thông báo LPTB | `DVC-07-thong-bao-lptb.jpg` |

Ảnh nằm ở [`../wireframes/`](../wireframes/) (bản đã che tên/MST). Ảnh chỉ để tham chiếu bố cục của cổng; giao diện ứng dụng theo khung Krypton của nhóm Hóa đơn.

Yêu cầu chung: dùng cùng khung, phông, trạng thái (đang tải/lỗi/trống) như nhóm Hóa đơn; trạng thái đăng nhập DVC hiển thị rõ ở thanh trạng thái; bản mô phỏng có nhãn "MÔ PHỎNG".

## 11. Giao diện bên ngoài

### 11.1 Cổng DVC

Bảng địa chỉ **[Đã thấy]** hoặc **[Giả định]** (xem [khảo sát](../api-samples/khao-sat-tthc.md)):

| Mục | Địa chỉ | Phương thức | Ghi chú |
|---|---|---|---|
| Đăng nhập | `/tthc/loginLDAP` | POST (XHR, JSON) | `tenDN`, `matKhau`, `doiTuong`, `captcha`; phản hồi `status` |
| CAPTCHA | `/tthc/getCaptcha?<t>` | GET | ảnh, gắn phiên bằng cookie |
| Danh mục TTHC | `/tthc/thu-tuc-hanh-chinh` | POST | mảnh HTML |
| Chi tiết TTHC | `/tthc/thu-tuc-hanh-chinh/chi-tiet-tthc?maTTHC=` | GET | trang HTML |
| Hồ sơ đã nộp (DVC) | `/tthc/ho-so/search` | GET | **[Giả định]** |
| Hồ sơ đã nộp (thuế điện tử) | `/tthc/tchs/thuedientu` | POST | **[Giả định]** theo `action` của form |
| Hồ sơ lưu tạm | `/tthc/tchs/draft` | POST | |
| Phản ánh | `/tthc/tchs/pa/tracuu` | POST | |
| LPTB | `/tthc/tchs/thong-bao-lptb` | POST | |
| Đất đai | `/tthc/tchs/ho-so-nha-dat` | ? | chưa quan sát |

CSRF: `htmx-csrf.js` được nạp, nên các POST có thể cần token **[G-DVC-03]**; khi gửi trong trang bằng chính cơ chế htmx của cổng thì không phải tự xử lý token.

### 11.2 WebView

Dùng CefSharp theo ADR 0007: một WebView ngầm cho nhóm DVC, hoạt động độc lập với WebView nhóm Hóa đơn. Một trang hoạt động tại một thời điểm (BR-DVC-06). Phiên (cookie) nằm trong profile riêng của WebView DVC, bị xóa khi đăng xuất.

### 11.3 File và hệ điều hành

Chỉ ghi file khi xuất Excel (FR-DVC-EXP), vào thư mục người dùng chọn.

### 11.4 Licence

Nhóm DVC dùng chung cơ chế bản quyền (ADR 0010): một mã kích hoạt cho cả hai nhóm, hết dùng thử khóa cả hai (mục 18 #1). Không đổi DD-03.

## 12. Yêu cầu phi chức năng

### 12.1 Bảo mật

- NFR-DVC-SEC-001: Không lưu mật khẩu, CAPTCHA, cookie phiên, CSRF vào đĩa, nhật ký hay thông báo lỗi.
- NFR-DVC-SEC-002: Không ghi giá trị người dùng nhập (mã hồ sơ, MST, số giấy tờ, kết quả) vào nhật ký; nhật ký chỉ ghi loại tra cứu, mã trạng thái, thời lượng.
- NFR-DVC-SEC-003: HTML của cổng không được thực thi trong ứng dụng; chỉ trích xuất văn bản qua bộ phân tích (FR-DVC-TTHC-005).
- NFR-DVC-SEC-004: Danh sách trắng địa chỉ/phương thức được phép gọi (BR-DVC-02); kiểm thử bắt buộc.
- NFR-DVC-SEC-005: Mẫu HTML/ảnh trong repo phải làm sạch tên, MST, mã hồ sơ, cookie.

### 12.2 Hiệu năng

- NFR-DVC-PERF-001: Giao diện không đứng khi chờ cổng; tra cứu có nút Hủy và hết thời gian chờ có thể cấu hình (mặc định 30 giây **[đề xuất]**).
- NFR-DVC-PERF-002: Phân tích một trang kết quả (≤ 100 dòng) dưới 200 ms trên máy phổ thông.

### 12.3 Độ tin cậy

- NFR-DVC-REL-001: Khi cấu trúc HTML không như kỳ vọng, hiển thị lỗi rõ "Cổng DVC đã đổi giao diện, vui lòng cập nhật phần mềm" và không hiển thị dữ liệu sai.
- NFR-DVC-REL-002: Mất mạng/hết phiên không làm mất bộ lọc người dùng đã nhập.

### 12.4 Khả dụng

- NFR-DVC-USE-001: Cùng bố cục và thao tác cơ bản như nhóm Hóa đơn (bộ lọc trên, bảng dưới, thanh trạng thái).
- NFR-DVC-USE-002: Tiếng Việt cho mọi thông báo; nhãn trường khớp tên trên cổng để người dùng đối chiếu.

### 12.5 Bảo trì và kiểm thử

- NFR-DVC-MNT-001: Bộ phân tích HTML có bộ mẫu HTML (đã làm sạch) cho từng mục và kiểm thử đơn vị.
- NFR-DVC-MNT-002: Địa chỉ và tên trường cổng nằm trong một tệp cấu hình/hằng số duy nhất để sửa nhanh khi cổng đổi.

## 13. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Sai CAPTCHA/mật khẩu | Báo lỗi, xóa mật khẩu/CAPTCHA, tải CAPTCHA mới |
| Bị đá do tab khác / hết phiên | Dừng tra cứu, báo "phiên DVC không còn hiệu lực", mời đăng nhập lại; không tự đăng nhập |
| Mất mạng/hết thời gian chờ | Báo lỗi, giữ bộ lọc, cho phép thử lại thủ công |
| Cổng trả lỗi hoặc bảo trì | Hiển thị thông điệp của cổng nếu có; không thử lại tự động |
| HTML không đọc được | Lỗi cấu trúc (NFR-DVC-REL-001), gợi ý báo lỗi kèm mã mục tra cứu, không kèm dữ liệu |
| Cổng đổi `doiTuong`/trường đăng nhập | Gợi ý dùng đăng nhập dự phòng (FR-DVC-AUTH-006) |

## 14. Tiêu chí nghiệm thu

- **AC-DVC-01 — Chọn nhóm.** Mở app có bản quyền hợp lệ ⇒ thấy hai thẻ; chọn Hóa đơn mở đúng shell hóa đơn như trước; chọn DVC mở shell DVC; "Đổi nhóm" quay về màn chọn.
- **AC-DVC-02 — Đăng nhập.** Với cổng giả: nhập đúng ⇒ vào được và hiện tên tổ chức; nhập sai CAPTCHA ⇒ báo lỗi, ô mật khẩu/CAPTCHA bị xóa, CAPTCHA mới. Ghi nhật ký không chứa mật khẩu.
- **AC-DVC-03 — Danh mục TTHC.** Tìm "hóa đơn" trả bảng có phân trang; mở chi tiết một mã thấy đủ các mục; không có điều khiển "Nộp hồ sơ".
- **AC-DVC-04 — Hồ sơ đã nộp.** Tab DVC và tab thuế điện tử yêu cầu CAPTCHA mới mỗi lần; sai CAPTCHA giữ bộ lọc; kết quả rỗng hiện đúng thông điệp.
- **AC-DVC-05 — Lưu tạm, Phản ánh, LPTB.** Gửi bộ lọc mặc định thành công; LPTB đòi ngày hóa đơn; kết quả trống hiển thị "Không có dữ liệu".
- **AC-DVC-06 — Đất đai.** Thiếu cả MST và số giấy tờ ⇒ thông báo yêu cầu nhập; không gửi request.
- **AC-DVC-07 — Hết phiên/tab khác.** Mô phỏng phản hồi "đang được sử dụng trên một tab khác" ⇒ dừng, báo, không tự đăng nhập.
- **AC-DVC-08 — Xuất Excel.** Xuất kết quả đang hiển thị ⇒ `.xlsx` mở được, cột đúng thứ tự, có dòng bộ lọc; không ghi đè khi chưa xác nhận.
- **AC-DVC-09 — Chỉ đọc.** Kiểm thử chứng minh mọi yêu cầu đi ra thuộc danh sách trắng và không chứa lệnh ghi.
- **AC-DVC-10 — Cách ly.** Đăng xuất DVC không ảnh hưởng phiên Hóa đơn và ngược lại; hai WebView không dùng chung cookie.
- **AC-DVC-11 — Giao diện cổng đổi.** Với HTML sai cấu trúc ⇒ thông báo NFR-DVC-REL-001, không có dữ liệu sai.

## 15. Ma trận truy vết

| Tính năng | Yêu cầu | Nghiệm thu |
|---|---|---|
| FT-DVC-00 | FR-DVC-NAV-001..005, BR-DVC-01 | AC-DVC-01 |
| FT-DVC-01 | FR-DVC-AUTH-001..009, BR-DVC-03..04, NFR-DVC-SEC-001 | AC-DVC-02, 07, 10 |
| FT-DVC-02 | FR-DVC-TTHC-001..007 | AC-DVC-03 |
| FT-DVC-03 | FR-DVC-HS-001..006, BR-DVC-03, 05 | AC-DVC-04 |
| FT-DVC-04 | FR-DVC-DRAFT-001..003 | AC-DVC-05 |
| FT-DVC-05 | FR-DVC-PA-001..002 | AC-DVC-05 |
| FT-DVC-06 | FR-DVC-DD-001..002 | AC-DVC-06 |
| FT-DVC-07 | FR-DVC-LPTB-001..004 | AC-DVC-05 |
| FT-DVC-08 | FR-DVC-EXP-001..004 | AC-DVC-08 |
| Xuyên suốt | BR-DVC-02, NFR-DVC-SEC-002..005, REL-001 | AC-DVC-09, 11 |

## 16. Kiểm thử bắt buộc

- **Unit:** bộ phân tích HTML từng mục (bảng, tổng bản ghi, thông điệp rỗng, cảnh báo); dựng tham số form; danh sách trắng địa chỉ; kiểm hợp lệ đầu vào (đất đai, LPTB ngày bắt buộc).
- **Integration (cổng giả):** `MockDvcPortalHandler` mô phỏng login/captcha/từng mục, bao gồm sai CAPTCHA, hết phiên, "tab khác", HTML đổi cấu trúc; nhãn MÔ PHỎNG; chỉ biên dịch trong `#if DEBUG`.
- **Cách ly:** test chứng minh hai nhóm không dùng chung phiên/cookie/cấu hình.
- **UAT trên cổng thật (người dùng tự thao tác, sau Release):** đăng nhập, mỗi mục tra cứu một lần, thu mẫu HTML có dữ liệu đã làm sạch để hoàn thiện bộ phân tích. Tuân thủ `CLAUDE.md`: không tự động hóa, không lưu bí mật.

## 17. Các cổng kỹ thuật (G-DVC) trước khi triển khai

| Cổng | Nội dung | Đầu ra |
|---|---|---|
| G-DVC-01 | Xác nhận gọi `loginLDAP` được từ trang trong WebView ngầm (giá trị `status`, thông báo lỗi, thời hạn phiên) | ADR/ghi chú, mẫu đã làm sạch |
| G-DVC-02 | Giá trị `doiTuong` (Doanh nghiệp/Cá nhân), kiểu thân gửi (JSON/form) | **Đã xác nhận 21/11/2026**: `DN`/`CN`, form urlencoded, `matKhau` base64 UTF-8, phản hồi `status` "200"/"201", lỗi ở `desc` (xem khảo sát). Còn lại: nội dung lỗi thật, thời hạn phiên |
| G-DVC-03 | Cơ chế CSRF và header/cookie bắt buộc của htmx | **Đã xác nhận 18/11/2026 cho trang công khai TTHC** (header `X-XSRF-TOKEN` từ meta, `accept: text/html-partial`, trường `_csrf`); xem khảo sát mục (2). Còn lại cho các mục sau đăng nhập |
| G-DVC-04 | Mẫu HTML **có dữ liệu** cho hồ sơ đã nộp, lưu tạm, phản ánh, LPTB (hai chế độ), đất đai; cột bảng; phân trang | thiết kế bảng, bộ phân tích |
| G-DVC-05 | Cổng có nút xuất Excel/PDF không; giới hạn khoảng ngày; giới hạn tần suất/CAPTCHA sai | cập nhật FR-DVC-EXP, BR-DVC-05 |
| G-DVC-06 | Kiểm tra một tab và cách WebView nhúng tránh bị coi là tab thứ hai | cập nhật BR-DVC-06 |

## 18. Quyết định của chủ sản phẩm (đã trả lời 16/11/2026)

| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | Entitlement/licence | **Một mã kích hoạt cho cả hai nhóm**; hết dùng thử khóa cả hai (không đổi ADR 0010, DD-03) |
| 2 | Màn chọn nhóm | **Luôn hiện mỗi lần mở**, mặc định chọn sẵn nhóm dùng gần nhất (FR-DVC-NAV-004) |
| 3 | Đổi nhóm khi đang chạy | **Giữ phiên của nhóm kia**; mỗi nhóm có WebView riêng |
| 4 | Kiểu đăng nhập | **Chỉ Thuế điện tử – Doanh nghiệp** ở v1; Cá nhân và VNeID ngoài phạm vi |
| 5 | Lưu cục bộ danh mục TTHC | **Không**, tra trực tiếp; kết quả chỉ giữ trong bộ nhớ |
| 6 | Dịch vụ khác (FT-DVC-09) | **Ngoài phạm vi v1**, khảo sát sau |
| 7 | Tên đăng nhập DVC | **Điền sẵn MST doanh nghiệp đang chọn nhưng cho sửa**; chỉ nhớ tên đăng nhập, không nhớ mật khẩu |

Đã đối chiếu và cập nhật FR-DVC-NAV-003..005, FR-DVC-AUTH-001, mục 11.4 và FEATURE-LIST FT-DVC-00 theo bảng này.

## 19. Phê duyệt thiết kế

Chưa phê duyệt. Chủ sản phẩm đã trả lời mục 18; sau khi hoàn tất G-DVC-01..04, nâng phiên bản lên 1.0, tạo `DD-DVC-xx` cho từng tính năng và bảng tiến độ riêng của nhóm.
