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

## Bổ sung 18/11/2026 (2): cơ chế gửi form và CSRF (G-DVC-03), trang công khai

Quan sát bằng Chrome (một tab, chưa đăng nhập) bằng cách ghi sự kiện htmx của chính trang (`htmx:configRequest`, `htmx:afterRequest`): chỉ ghi tên trường, đường dẫn, tên header; **không** ghi giá trị token.

| Mục | Kết quả |
|---|---|
| Tìm | `POST /tthc/homelogin/tthc`, `Content-Type` form urlencoded (htmx). Trường: `_csrf` (ẩn), `maTTHC` (ô tên/mã), `maNghiepVu`, `maToKhai`, `mucDo`. Không gửi `_domain`. Phản hồi 200 `text/html`, là mảnh HTML (bắt đầu bằng `div` có `style` min-height) nạp vào `#bangKetQuaTTHC` |
| Đổi trang | Cùng địa chỉ `POST /tthc/homelogin/tthc`, cùng các trường, thêm `page` (**bắt đầu từ 1**) và `size` (10). Liên kết phân trang khai báo: `hx-post` địa chỉ trên, `hx-target` `#bangKetQuaTTHC`, `hx-include` `#form_search`, `hx-vals` JSON gồm `page` và `size`. Nút `«` ở trang 1 mang `page` 0 (cần chặn, không gửi) |
| Header | htmx tự thêm `HX-Request`, `HX-Trigger`, `HX-Trigger-Name`, `HX-Target`, `HX-Current-URL`; script `htmx-csrf.js` (nghe `htmx:configRequest`) đặt thêm cho mọi yêu cầu **khác GET**: `accept: text/html-partial` và header CSRF. Tên header CSRF lấy từ thẻ `meta` `csrf-header` (giá trị **`X-XSRF-TOKEN`**), giá trị token lấy từ thẻ `meta` `csrf-token` (96 ký tự, **không** ghi vào repo) |
| Token khác | Form còn có trường ẩn `_csrf` (cùng cơ chế token) |
| Chi tiết | Điều hướng GET thường tới `/tthc/homelogin/chi-tiet-tthc?maTTHC=<mã>`; không cần token |
| Một tab | Ngay cả trang công khai cũng ghi `localStorage.APP_SINGLE_TAB_ACTIVE` và `sessionStorage.TAB_ID`. Hai thứ này thuộc hồ sơ trình duyệt: WebView của app có hồ sơ riêng (không dùng chung với Chrome của người dùng); trong app vẫn chỉ giữ **một** trang DVC |
| Cookie | Trang đặt cookie phiên (đọc được qua JS 8 cookie; tên và giá trị không ghi nhận) |

**Hệ quả thiết kế:** gửi trong trang bằng chính cơ chế của cổng là đủ, app không phải tự xử lý token: dựng `FormData` từ `#form_search` (đã có `_csrf`), thêm `page`/`size`, gọi `fetch` cùng gốc (same-origin) với header `accept: text/html-partial`, `X-XSRF-TOKEN` đọc từ meta ngay lúc gửi, `HX-Request: true`. Hoặc gọi `htmx.ajax` của trang. Không sao chép cookie/token ra ngoài WebView.

## Bổ sung 21/11/2026: luồng đăng nhập thật (G-DVC-02), đọc từ mã nguồn của trang

Bộ ghi request cài vào trang không bắt được gì vì trang chuyển hướng giữa các bước làm mất nó. Thay vào đó đọc **mã JavaScript công khai** của trang `/tthc/login` (hàm `submitLDAP`, `processChonDT`, `reloadCaptcha`) sau khi người dùng tự đăng nhập; **không** đọc, ghi hay gửi bất kỳ giá trị đăng nhập nào. Không ghi token/cookie vào tài liệu.

| Mục | Kết quả |
|---|---|
| Chọn loại tài khoản | Ba thẻ: Thuế điện tử, Định danh điện tử, Công chức thuế. Thuế điện tử → hộp chọn đối tượng: `processChonDT('CN' hoặc 'DN', …)`; giá trị được ghi vào ô ẩn `input[name=ldap]` |
| **`doiTuong`** | **`DN`** = Doanh nghiệp, **`CN`** = Cá nhân (đóng G-DVC-02). Với Cá nhân tên đăng nhập có thêm gợi ý "/CCCD" |
| Ảnh CAPTCHA | **`GET /tthc/login/getCaptcha?<thời gian>`** (sửa lại: trước đây ghi nhầm là `/tthc/getCaptcha`). Nút đổi mã đặt `src` của `#image-capt` tới địa chỉ này với tem thời gian mới |
| Gửi đăng nhập | jQuery `$.ajax` **`POST /tthc/loginLDAP`**, dạng **form urlencoded** (mặc định của jQuery, **không phải JSON**). Trường: `tenDN` (nguyên văn), **`matKhau` = base64 của mật khẩu UTF-8** (`btoa(unescape(encodeURIComponent(matKhau)))`, chỉ là mã hóa base64, không phải mã hóa bảo mật), `doiTuong` (`DN`/`CN`), `captcha` |
| CSRF của lần đăng nhập | **Không** gửi header CSRF cho `loginLDAP` (không có `ajaxSetup`/`beforeSend` cho jQuery); header `X-XSRF-TOKEN` chỉ dùng cho htmx và cho `POST /tthc/logout` (dạng JSON). Trang đăng nhập có `meta csrf-token`/`csrf-header` |
| Kiểm tra phía trang trước khi gửi | Thiếu tên/mật khẩu/CAPTCHA thì báo lỗi ngay (không gọi mạng); tên đăng nhập có khoảng trắng bị từ chối "Thông tin tên đăng nhập hoặc mật khẩu chưa đúng". Gửi xong nút đăng nhập bị khóa cho tới khi có phản hồi |
| Phản hồi | JSON có `status` (chuỗi): **`"200"`** thành công, trang chuyển tới `/tthc/home`; **`"201"`** thành công kèm `value` (chuyển tới `/tthc/` + `value`, hộp đánh giá dịch vụ ở trang chủ); **giá trị khác**: lỗi, thông báo lấy từ trường **`desc`**, trang tải lại CAPTCHA và xóa ô CAPTCHA. Lỗi HTTP/mạng: "Đã có lỗi hệ thống xảy ra!" |
| Sau đăng nhập | Tên tổ chức nằm ở `span.ten_dn` (trong `.user-name`), MST ở mục menu `.user-name.dropdown-item` |
| Đăng xuất | `POST /tthc/logout` (JSON, header `X-XSRF-TOKEN` lấy từ meta) |
| Cổng chặn nhiều tab | Cả trang đăng nhập hiện "Tài khoản hiện đang được sử dụng trên một tab khác" khi có tab khác của cùng hồ sơ Chrome (khóa `localStorage.APP_SINGLE_TAB_ACTIVE`); đóng tab kia là hết |

**Chưa biết (cần thử lỗi thật, chưa làm):** nội dung `desc` và giá trị `status` khi sai CAPTCHA/sai mật khẩu/khóa tài khoản; giá trị `value` của `"201"`; thời hạn phiên; tên cookie phiên; giới hạn thử sai.

**Hệ quả thiết kế:** app đăng nhập bằng cách đặt các ô `tenDN`, `matKhau`, `ldap`, `captcha` rồi chạy chính `submitLDAP()` của trang (hoặc dựng đúng yêu cầu trên bằng `fetch` cùng gốc, nhớ mã hóa `matKhau` base64 UTF-8), đọc `status`/`desc` từ phản hồi. Mật khẩu chỉ đi qua dạng literal JSON vào script, không ghi nhật ký.

## Bổ sung 21/11/2026 (2): phản hồi khi sai CAPTCHA

Người dùng tự nhập tên đăng nhập, mật khẩu đúng và **CAPTCHA cố ý sai**; bộ ghi trong trang (bọc `XMLHttpRequest` và `showNotify`) chỉ ghi phản hồi, không ghi giá trị đã gõ.

| Mục | Kết quả |
|---|---|
| HTTP | **200**, `application/json` (lỗi nghiệp vụ không dùng mã HTTP lỗi) |
| Thân phản hồi | `{"status":"999","desc":"Mã captcha không đúng, vui lòng thử lại."}` (chỉ hai khóa `status` và `desc`) |
| Hiển thị của trang | `showNotify('error', desc)`, tải lại ảnh CAPTCHA, xóa ô CAPTCHA, mở lại nút Đăng nhập |
| Tải lại trang | Không; hộp đăng nhập giữ nguyên, các ô tên đăng nhập và mật khẩu còn nguyên |

Hệ quả: `status` khác `"200"`/`"201"` là lỗi, riêng sai CAPTCHA là `"999"` kèm `desc` tiếng Việt. Chưa biết `status` của sai mật khẩu và khóa tài khoản (có thể cũng `"999"`, khác nhau ở `desc`); vì vậy **phân loại lỗi theo `desc`** (chứa "captcha" ⇒ sai CAPTCHA) và các trường hợp còn lại gộp "đăng nhập không thành công" kèm nguyên văn `desc` của cổng (cắt ngắn, không chứa dữ liệu người dùng).

## Bổ sung 21/11/2026 (3): tra cứu hồ sơ đã nộp, tab Dịch vụ công (đã đăng nhập, người dùng tự gõ CAPTCHA)

Quan sát bằng Chrome, một tab, ghi sự kiện htmx của trang (tên tham số, đường dẫn, mã trạng thái; không ghi giá trị CAPTCHA, token, mã hồ sơ, tên). Tài khoản thử có 2 hồ sơ trong 01/2026 và không có hồ sơ trong 11/2026, nên **chưa quan sát được phân trang nhiều trang**.

| Mục | Kết quả |
|---|---|
| Trang | `/tthc/tchs` (menu "Tra cứu hồ sơ" → "Tra cứu hồ sơ đã nộp"). Hai tab trong trang: "trên DVC" (form `form-search-advanced`) và "trên thuế điện tử" (form `form_search_tdt`, POST). Các mục khác trong menu: `/tthc/tchs/draft` (lưu tạm), `/tthc/tchs/pa` (phản ánh), `/tthc/tchs/ho-so-nha-dat` (đất đai), `/tthc/tchs/thong-bao-lptb` (LPTB), `/tthc/tra-cuu-thongbao-cqt` (thông báo của CQT) |
| Tìm (tab DVC) | **`GET /tthc/ho-so/search`** (htmx, `hx-target=#table-container`). Tham số: `maNghiepVu` (Loại TTHC), `maTTHC` (Tên TTHC), `maToKhai`, `maHoSo`, `tuNgay`, `denNgay` (**`dd/MM/yyyy`**), `scope_tdt1` (`SELF` tự kê khai, `PROXY` khai thay theo hợp đồng DLT), `mstUyQuyen_tdt1`, `captcha`. Header: các `HX-*` của htmx và `accept`. Mặc định "Từ ngày" là đầu tháng, "Đến ngày" là hôm nay |
| Phản hồi thành công | HTTP 200, `text/html` mảnh HTML. Không có kết quả: mảnh ngắn (~1,8 KB) không có bảng ("Không có dữ liệu"). Có kết quả: `#table-container > section.main-section` chứa dòng "Trang x/N - Tổng số bản ghi: n", `ul.pagination`, và bảng `table.table.table-bordered.table-hover` |
| **Sai CAPTCHA** | **HTTP 400**, thân văn bản ngắn (45 ký tự, chưa đọc được nội dung chính xác), không có bảng. Khác với đăng nhập (HTTP 200 + `status` 999). Phía trang htmx coi là `responseError` |
| Cột bảng (13) | STT; Thao tác (hai biểu tượng: xem, thư mục; gọi bằng `onclick`); Mã hồ sơ (dạng `999.999.99.A99-999999-99999999999999`); Tên TTHC (`mã - tên`); Tờ khai (`mã tờ khai - tên (kỳ)`); Kỳ tính thuế (vd `Q4/2025`); Loại tờ khai (vd "Chính thức"); Lần nộp bổ sung (có thể rỗng); Lần nộp (Tờ khai)/File số (DLVC); Cơ quan thuế tiếp nhận (`mã - tên cơ quan`); Ngày nộp (`dd/MM/yyyy HH:mm`); Trạng thái (vd "Đã chấp nhận"); Hồ sơ đính kèm (có thể rỗng) |
| Phân trang | `ul.pagination > li.page-item` (trang hiện tại thêm `active`) với `a.page-link` mang **`onclick="onChangePage(<trang>,10)"`**: **10 dòng mỗi trang**. Hàm của trang: lấy `FormData` của `#form-search-advanced` (gồm **cả ô CAPTCHA hiện có**), đặt `page` và `size`, rồi `htmx.ajax('GET', '/tthc/ho-so/search?<toàn bộ tham số>&page=N&size=10', {target:'#table-container', swap:'outerHTML'})` |
| Chưa biết | Nội dung thông báo lỗi 400; kết quả đổi trang có cần CAPTCHA mới không (trang cổng gửi lại giá trị trong ô CAPTCHA); giới hạn khoảng ngày; tab thuế điện tử (POST) chưa quan sát; cấu trúc dòng khi nhiều trang |

**Quyết định của chủ sản phẩm (21/11/2026): chốt đổi trang KHÔNG cần CAPTCHA mới**, tức dùng lại mã của lần tìm đã ra kết quả, giống hành vi của chính trang cổng. Đây là giả định chưa kiểm chứng (tài khoản thử không đủ dữ liệu để phân trang); xem BR-DVC-03 và DD-DVC-03 mục 4.3 về cách phòng ngừa nếu cổng từ chối.

## Bổ sung 21/11/2026 (4): tra cứu hồ sơ đã nộp, tab thuế điện tử (đã đăng nhập, người dùng tự gõ CAPTCHA)

Đọc mã nguồn công khai của trang `/tthc/tchs` (nút `#btnSearch_tdt`, `changePage_tdt`, `goToPage_tdt`) và ghi sự kiện htmx khi người dùng tìm. Tài khoản thử có 1 hồ sơ trong 01/2025 (không có trong 06/2025).

| Mục | Kết quả |
|---|---|
| Form | `form_search_tdt`, `method=post`, `action=/tthc/tchs/thuedientu`. Trường: `_csrf`, `page`, `size` (ẩn), `maToKhai_tdt` (ô chọn, 484 lựa chọn), `maGiaoDichTthc_tdt`, `tuNgay_tdt`, `denNgay_tdt` (**mặc định 01/06/2025 đến 30/06/2025**, `dd/MM/yyyy`), `scope_tdt2` (`SELF`, `PROXY`), `mstUyQuyen_tdt2`, `captcha`. Ảnh CAPTCHA `#image-capt-tdt`, đổi mã `refreshCaptcha('image-capt-tdt')`. Lưu ý của cổng: chỉ hỗ trợ tờ khai nộp **trước 01/07/2025** |
| Tìm | Bấm nút gọi `htmx.ajax('POST', <action của form>, {source:'#form_search_tdt', target:'#bangKetQuaTraCuu_tdt', values: htmx.values(form)})`. Yêu cầu thật: `POST /tthc/tchs/thuedientu`, `Content-Type` form urlencoded, header htmx + `accept` + **`X-XSRF-TOKEN`**; tham số gồm toàn bộ trường form kèm `btnSearch_tdt` (rỗng). Lần tìm đầu `page` và `size` rỗng. Kiểm tra phía trang: bắt buộc nhập CAPTCHA, kiểm tra loại tờ khai/ngày (`validLoaiTKhai_TDT`) |
| Kết quả | HTTP 200, mảnh HTML. **Rỗng:** ~0,9 KB, chỉ có khung phân trang (có chú thích HTML), không có bảng. **Có dữ liệu:** dòng "Trang x/ N — Tổng số bản ghi: n", ô nhảy trang `goToPage_tdt` (thuộc tính `max`), `.pagination`, bảng `table.table-bordered.table-hover` có `thead` và `tbody`, mỗi dòng đúng số ô bằng số tiêu đề (khác tab DVC) |
| Cột bảng (11) | STT; Mã giao dịch; Tờ khai/Phụ lục (`tên (kỳ)(mã)`); Kỳ tính thuế (vd `Q4/2024`); Loại tờ khai ("Chính thức"); Lần nộp; Lần bổ sung; Ngày nộp (`dd/MM/yyyy HH:mm:ss`); Nơi nộp; Tiến trình giải quyết hồ sơ (Trạng thái); Thao tác (biểu tượng) |
| Phân trang | Liên kết `a` có `onclick` và `data-page`, `data-size`; `changePage_tdt(el)` đặt `page`, `size` vào form rồi POST lại **cùng toàn bộ giá trị form, kể cả ô CAPTCHA**; `goToPage_tdt()` tương tự. Giống tab DVC |
| Lỗi CAPTCHA | Có bộ nghe `htmx:responseError` (lỗi HTTP) hiển thị nội dung phản hồi hoặc "Mã captcha không chính xác"; tương tự tab DVC (HTTP 400). **Chưa thử sai CAPTCHA ở tab này** |

Bổ sung cho tab DVC: sau **mỗi** yêu cầu trang gọi `refreshCaptcha('image-capt')` (ảnh mới, mã máy chủ đổi), nhưng đổi trang vẫn gửi chữ đang có trong ô. Nếu cổng chấp nhận được hay không vẫn chưa kiểm chứng (cần tài khoản nhiều hồ sơ).

