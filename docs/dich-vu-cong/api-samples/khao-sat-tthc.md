# Khảo sát cổng Dịch vụ công thuế (TTHC) — nhóm Dịch vụ công

Ngày khảo sát: 2026-11-16. Nguồn: <https://dichvucong.gdt.gov.vn/tthc/> (menu **THỦ TỤC HÀNH CHÍNH** và **TRA CỨU HỒ SƠ**), duyệt bằng Claude in Chrome, chỉ đọc.
Đối chiếu số trong bảng bên dưới với mã tính năng: 0 = FT-DVC-02, 1a/1b = FT-DVC-03, 2 = FT-DVC-04, 3 = FT-DVC-05, 4 = FT-DVC-06, 5 = FT-DVC-07; đăng nhập = FT-DVC-01 ([danh sách](../requirements/FEATURE-LIST.md)).
Không lưu token/cookie/CSRF. Chưa kiểm tra nội dung phản hồi có dữ liệu thật (tài khoản thử không có hồ sơ).

## Kiến trúc chung (khác cổng hóa đơn)

- Cổng **không có REST/JSON API**. Đây là ứng dụng server-rendered dùng **htmx**: form `POST`/`GET` trả về **mảnh HTML** (bảng kết quả), không phải JSON.
- Có `htmx-csrf.js` ⇒ mọi POST cần CSRF token lấy từ trang. Phiên là cookie đăng nhập SSO (khác JWT của cổng hóa đơn).
- **Chỉ cho một tab hoạt động mỗi tài khoản**: mở tab thứ hai thì tab kia báo "Tài khoản hiện đang được sử dụng trên một tab khác". Đã gặp hai lần khi khảo sát.
- Các ô chọn (loại TTHC, tờ khai, tên TTHC) được render sẵn vào HTML (148 TTHC, 478 tờ khai, 23 loại nghiệp vụ), không gọi API riêng khi tải trang.
- CAPTCHA ảnh qua `GET /tthc/getCaptcha?<timestamp>`.
- Hệ quả: cách tích hợp phù hợp là phiên WebView (CefSharp) đã đăng nhập + gọi form/HTML-scrape trong phiên đó, không dùng được `HttpClient` + token như cổng hóa đơn. Cần ADR riêng.

## Danh sách chức năng

| # | Menu | Đường dẫn | Bộ lọc | CAPTCHA | Gửi đi | Trạng thái khảo sát |
|---|------|-----------|--------|---------|--------|---------------------|
| 1a | Tra cứu hồ sơ đã nộp – tab "đã nộp trên DVC" | `/tthc/tchs` | Loại TTHC (`maNghiepVu`), Tên TTHC (`maTTHC`), Tờ khai (`maToKhai`), Mã hồ sơ (`maHoSo`), Từ ngày/Đến ngày (`tuNgay`,`denNgay`, mặc định đầu tháng→hôm nay), radio phạm vi + `mstUyQuyen_tdt1` (MST ủy quyền), `captcha` | **Có** | `GET /tthc/ho-so/search` (htmx, đích `#table-container`) | Đã chạy (người dùng tự nhập CAPTCHA), "Trang 1/0 - Tổng số bản ghi: 0", "Không có dữ liệu". Request chưa bắt được (công cụ theo dõi không ghi nhận); `GET /tthc/ho-so/search` là suy ra từ thuộc tính `hx-get` của form |
| 1b | Tra cứu hồ sơ đã nộp – tab "đã nộp trên thuế điện tử" | `/tthc/tchs` | Tờ khai (`maToKhai_tdt`), Mã giao dịch (`maGiaoDichTthc_tdt`), Từ/Đến ngày, radio phạm vi + `mstUyQuyen_tdt2`, `captcha` | **Có** | `POST /tthc/tchs/thuedientu` | Đã chạy (người dùng tự nhập CAPTCHA, 01/06–30/06/2025, không chọn tờ khai): thông báo đỏ "Không có dữ liệu thỏa mãn điều kiện tìm kiếm!" (khác mục 1a: không có dòng "Tổng số bản ghi"). Request chưa bắt được; `POST /tthc/tchs/thuedientu` lấy từ `action` của form |
| 2 | Tra cứu hồ sơ lưu tạm | `/tthc/tchs/draft` | Tờ khai, Tên TTHC, Từ ngày, Đến ngày (đều tùy chọn) | Không | `POST /tthc/tchs/draft` | Đã chạy, kết quả "Không có dữ liệu" |
| 3 | Tra cứu phản ánh (QTT gửi đến CQT) | `/tthc/tchs/pa` | Năm quyết toán (2021–2025), MST tổ chức trả thu nhập, Ngày gửi phản ánh từ/đến, Nguồn dữ liệu ("Phản ánh đã gửi trên DVC" / "...trên Thuế điện tử") | Không | `POST /tthc/tchs/pa/tracuu` | Đã chạy, "Tổng số bản ghi: 0", có phân trang "Trang x/N" |
| 4 | Tra cứu TTHC đất đai | `/tthc/tchs/ho-so-nha-dat` | Mã hồ sơ (**bắt buộc**) và phải kèm Mã số thuế **hoặc** Số giấy tờ | Không | Chưa quan sát (kiểm tra ở phía trình duyệt, chưa gửi) | Chỉ thấy thông báo "Yêu cầu nhập thông tin Mã số thuế hoặc Số giấy tờ!"; cần mã hồ sơ thật |
| 5 | Tra cứu thông báo LPTB (lệ phí trước bạ) | `/tthc/tchs/thong-bao-lptb` | Hình thức (của NNT / của NNT khác), MST (điền sẵn, khóa), Mã giao dịch điện tử, Trạng thái thông báo, Ngày hóa đơn từ/đến (**bắt buộc**, mặc định 30 ngày) | Không | `POST /tthc/tchs/thong-bao-lptb` | Đã chạy, "Không có dữ liệu" |

Cột kết quả mục 5: STT, Chọn, Mã giao dịch điện tử, Mã số thuế, Tên người nộp thuế, Loại tài sản, Số khung, Số máy, Số tiền nộp LPTB, Trạng thái thông báo.
Mục 2 hiển thị lưu ý: chỉ "Nộp hồ sơ" được khi hồ sơ ở trạng thái TTHC đã kiểm tra thông tin kê khai.

## Màn "Thủ tục hành chính" (cũng nằm trong vùng khoanh đỏ)

Đây là mục duy nhất **có dữ liệu thật** và không cần đăng nhập để có nghĩa (là danh mục công khai), nên là chức năng dễ tích hợp nhất.

| Thành phần | Chi tiết |
|---|---|
| Đường dẫn | `/tthc/thu-tuc-hanh-chinh` |
| Bộ lọc | Tên/Mã TTHC (văn bản tự do), Loại TTHC, Tờ khai, Mức độ, Loại người nộp thuế; không CAPTCHA |
| Tìm kiếm / phân trang | `POST /tthc/thu-tuc-hanh-chinh` (htmx, trả mảnh HTML; phân trang cũng POST cùng địa chỉ, quan sát thấy 2 POST mỗi lần tìm). Mặc định 147 bản ghi (137 TTHC) / 15 trang, 10 dòng mỗi trang; có ô nhập số trang và nút Đi |
| Cột kết quả | STT, Mã (vd `1.010339`), Tên thủ tục (link), Mức độ (vd "Toàn trình"), Cơ quan thực hiện, Nộp hồ sơ (biểu tượng) |
| Ví dụ | Tìm "hóa đơn" ⇒ "Trang 1/1, tổng 6 (137 TTHC)" |
| Chi tiết | `GET /tthc/thu-tuc-hanh-chinh/chi-tiet-tthc?maTTHC=<mã>` (trang HTML đầy đủ). Trường: Mã thủ tục, Cấp thực hiện, Trình tự thực hiện, Cách thức thực hiện, Thành phần hồ sơ, Đối tượng thực hiện, Cơ quan thực hiện, Địa chỉ tiếp nhận hồ sơ, Kết quả thực hiện, Điều kiện thực hiện, Căn cứ pháp lý; nút "Quay lại" |
| Nộp hồ sơ | Nút trên mỗi dòng dẫn tới nộp hồ sơ (không khảo sát: thao tác ghi, ngoài phạm vi tra cứu) |

Danh sách "Loại TTHC" gồm (cắt bớt): Đại lý thuế, Báo cáo/thông báo của NNT, Đăng ký của NNT, Đăng ký thuế, Đề nghị của NNT (T-VAN), Gia hạn, Hồ sơ khai thuế, Hồ sơ quyết toán thuế, Hóa đơn/biên lai, Hoàn thuế, Khai khác, Miễn giảm...

## Đăng nhập (khảo sát một phần)

Toàn bộ khảo sát trên dùng phiên **đã đăng nhập sẵn** của người dùng; chưa quan sát luồng đăng nhập thật vì không được đăng xuất tài khoản của họ. Điều đã thấy từ DOM của `/tthc/home` khi đang đăng nhập:

- Nút "Đăng nhập" (`hx-on:click=submitCBT()`) và hộp thoại chọn **Đối tượng đăng nhập**: `modalChonDoiTuong` (Cá nhân `processChonDT('CN', …)` / Doanh nghiệp `processChonDT('DN', …)`), `modalChonDoiTuongVNEID` (VNeID, `redirectDtVneid('CN'|'DN')`).
- Hộp thoại `modalCanBoThueLogin` (cán bộ thuế): chọn miền (`_domain`: han/mb/mn/hcm/vp.tct.vn), `tenDNCbt`, `matKhauCbt`, `captchaCbt` + ảnh CAPTCHA (`btnReloadCaptcha`). Không dành cho người nộp thuế, ngoài phạm vi.
- Đăng nhập người nộp thuế là **SSO chuyển hướng** (script `ssocommon.js`); trang đích SSO, trường nhập, CAPTCHA/OTP, cookie phiên và cách phát hiện hết phiên **chưa quan sát**.
- Một tài khoản chỉ một tab (xem "Kiến trúc chung"): đăng nhập ở nơi khác sẽ đá phiên hiện tại.
- Công cụ khảo sát chặn đọc mã chứa cookie/query nên không đọc được `processChonDT`, `redirectDtVneid`, `submitCBT`.

### Luồng đăng nhập đã quan sát (16/11/2026, người dùng tự nhập thông tin)

1. Chưa đăng nhập thì mọi trang chuyển về `/tthc/login`; menu chuyển sang `/tthc/homelogin/...` (bản công khai: TTHC, Dịch vụ khác, Đánh giá, Tin tức, Giới thiệu, Trợ giúp). Nút "Đăng ký" và "Đăng nhập" ở góc phải.
2. `/tthc/login` có 3 thẻ: **Tài khoản Thuế điện tử**, Tài khoản Định danh điện tử (VNeID), Tài khoản Công chức thuế (có lời nhắc về Nghị định 69/2024/NĐ-CP).
3. Chọn Thuế điện tử ⇒ hộp chọn **Cá nhân / Doanh nghiệp** (`processChonDT('CN'|'DN')`).
4. Chọn Doanh nghiệp ⇒ hộp `modalLdap` ngay trên trang: `tenDN`, `matKhau` (có nút hiện/ẩn), `captcha` + ảnh (`reloadCaptcha()`), nút `submitLDAP()`. **Có CAPTCHA, không thấy OTP** ở lần đăng nhập này.
5. Sau khi đăng nhập thành công về `/tthc/home` (điều hướng thường, không chuyển qua máy chủ SSO ngoài mà trình duyệt ghi nhận); tên công ty hiện ở góc phải.
6. Phiên giữ bằng cookie (không đọc); `localStorage` chỉ có khóa `APP_SINGLE_TAB_ACTIVE`, `sessionStorage` có `TAB_ID`: **giới hạn một tab do chính trang kiểm bằng hai khóa này**, không phải do máy chủ. Không thấy token trong storage.
7. **Yêu cầu đăng nhập đã bắt được** (lần thử thứ hai, người dùng nhập sẵn tên/mật khẩu/CAPTCHA, app bấm nút): `POST https://dichvucong.gdt.gov.vn/tthc/loginLDAP` bằng XHR, trả `200 application/json`.
   - Thân gửi đi gồm đúng 4 trường: `tenDN`, `matKhau`, `doiTuong`, `captcha` (tên trường lấy từ bộ ghi; **không ghi giá trị**; kiểu mã hóa thân chưa xác định: JSON hay form).
   - Phản hồi JSON có khóa cấp một `status` (giá trị chưa xác định); sau đó trang tự chuyển bằng `GET /tthc/home` (client điều hướng, không phải 3xx).
   - Chưa biết: giá trị `doiTuong` cho Cá nhân/Doanh nghiệp (`CN`/`DN` theo tên hàm `processChonDT`, chưa kiểm chứng), có header CSRF hay không, tên cookie phiên, ảnh CAPTCHA lấy từ `GET /tthc/getCaptcha?<timestamp>` (đã thấy ở trang tra cứu) và gắn với phiên qua cookie.
8. Chưa quan sát: thông báo khi sai mật khẩu/CAPTCHA, thời hạn phiên, đăng nhập Cá nhân/VNeID.

Hệ quả cho ADR DVC-0001: tương tự ADR 0011 (form riêng + WebView ngầm gọi `submitLDAP` với CAPTCHA do người dùng đọc), nhưng phải xác nhận địa chỉ đăng nhập và cơ chế cookie trước; kiểm tra một tab nằm ở phía trình duyệt nên WebView nhúng sẽ phải tránh làm hai trang cùng hoạt động.

## Ngoài phạm vi (đã thấy)

- `/tthc/tra-cuu-thongbao-cqt` — "Tra cứu thông báo của CQT" (menu người dùng).
- `/tthc/dich-vu-khac` — "Dịch vụ khác" (Tổ chức: tab Nộp thuế, Hồ sơ khai thuế khác, Hoàn thuế, Tra cứu, Chuẩn hóa dữ liệu MST cá nhân): Lập/Phê duyệt/Tra cứu GNT, Tra cứu thông báo, Tra cứu hồ sơ đề nghị tra soát, Tra cứu đề nghị bù trừ khoản nộp thừa, Tra cứu giấy đề nghị xác nhận... Liên kết là `#` (mở bằng script), chưa khảo sát. Nhiều mục "Tra cứu" có thể đáng thêm vào nhóm DVC sau (FT-DVC-09).
- `/tthc/danh-gia`, `/tthc/tintuc`, `/tthc/trogiup/views`, `/tthc/dang-ky-dich-vu`.

Ghi chú giao diện: tab 1b có dòng đỏ "Chức năng chỉ hỗ trợ đối với tra cứu tờ khai nộp trước ngày 01/07/2025", khoảng ngày mặc định 01/06/2025–30/06/2025. Trang 1a mặc định từ đầu tháng đến hôm nay. Mục 2 có lưu ý "Nộp hồ sơ" chỉ được khi hồ sơ ở trạng thái "Đã kiểm tra thông tin kê khai".

## Ảnh giao diện làm mockup

Ảnh chụp ngày 16/11/2026 nằm ở [`../wireframes/`](../wireframes/) (`DVC-02` danh mục + chi tiết TTHC, `DVC-03a/b` hồ sơ đã nộp, `DVC-04`…`DVC-07`, `DVC-08` Dịch vụ khác). Bản gốc `*.raw.jpg` còn **tên doanh nghiệp và MST thật** (góc phải trên, và ô MST ở `DVC-07`) nên git bỏ qua. Bản `*.jpg` cùng tên đã tô xám các vùng đó, dùng để commit. Chưa có ảnh trang kết quả có dữ liệu. Mã CAPTCHA hiển thị trong ảnh là mã dùng một lần đã hết hiệu lực.

## Việc còn thiếu

1. Kết quả khi **có dữ liệu** (cột bảng, phân trang, chi tiết, tải file) của mục 1–3: tài khoản thử không có hồ sơ.
2. Mục 1a/1b cần người dùng nhập CAPTCHA; mục 4 cần mã hồ sơ thật kèm MST/số giấy tờ.
3. Tên/giá trị các trường CSRF và cách htmx đính kèm: công cụ khảo sát chặn đọc `htmx-csrf.js` (có dữ liệu cookie), cần mở thủ công.
4. Cần ADR: tích hợp qua phiên WebView + scrape HTML, và xử lý giới hạn một tab.

## Bổ sung 18/11/2026: bản công khai của danh mục TTHC (khảo sát đọc, chưa đăng nhập)

Quan sát bằng Chrome, một tab, chỉ đọc, không đăng nhập.

| Mục | Kết quả |
|---|---|
| Có dùng được khi chưa đăng nhập? | **Có.** `/tthc/thu-tuc-hanh-chinh` chuyển về `/tthc/homelogin`; trang công khai `/tthc/homelogin/tthc` có đủ bộ lọc, bảng, phân trang (147 bản ghi, 137 TTHC, 15 trang). Đóng [Giả định] FR-DVC-TTHC-007 |
| Chi tiết công khai | `GET /tthc/homelogin/chi-tiet-tthc?maTTHC=<mã>` (mở bằng cách bấm tên thủ tục). Đường dẫn tự suy `/tthc/homelogin/tthc/chi-tiet-tthc` bị tường lửa trả "Request Rejected" (nên chỉ dùng đường dẫn lấy từ chính trang) |
| Form tìm | `form_search` (POST), có trường ẩn CSRF (`_csrf`), các ô `maNghiepVu` (Loại TTHC, 23 lựa chọn), `maToKhai` (Tờ khai, nhiều lựa chọn có mã số), `mucDo` (`01` Toàn trình, `02` Cung cấp thông tin, `03` DVCTT một phần), `_domain` (danh sách miền, ngoài phạm vi), ô tên/mã `maTTHC`, `goToPage` |
| Vùng kết quả | `#bangKetQuaTTHC`: thanh phân trang `ul.pagination` với các `a.page-link` mang thuộc tính htmx (`hx-post`, `hx-target`, `hx-include`, `hx-vals`; chưa đọc được giá trị), ô `#goToPage` + nút Đi, dòng "Trang x/N — Tổng số bản ghi: n (m TTHC)", bảng `table.table-bordered` cột STT, Mã, Tên thủ tục (liên kết), Mức độ, Cơ quan thực hiện, Nộp hồ sơ |
| Trang chi tiết | `div.card-body` gồm `h4` tên và 11 khối `div.mb-3` có `label` và nội dung với `id`: `maTTHC`, `capThuchien`, `thuTuThucHien`, `cachThuchien`, `thanhPhanHoSo`, `doiTuongThucHien`, `cqThucHien`, `dchiTiepnhanHso`, `ketQuaThucHien`, `dieuKienThucHien`, `canCuPhapLy`; nội dung dài lồng `p > span > span > span`, có `strong`/`em` |
| Mẫu làm sạch | `tests/HoaDonDienTu.Infrastructure.Tests/Fixtures/tthc-list.sample.html`, `tthc-detail.sample.html` (dựng lại từ cấu trúc DOM, đã bỏ giá trị hx-*, href nhạy cảm, token, script; nội dung rút gọn) |

Chưa biết: giá trị `hx-vals` của phân trang (tên tham số số trang), giá trị tên trường tìm kiếm khi gửi form (công cụ chặn đọc chuỗi giống query), tên và giá trị token CSRF. Cần ở G-DVC-03.
