# DD-DVC-02 — Danh mục thủ tục hành chính (TTHC)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-DVC-02 |
| Phiên bản | 0.1 (nháp) |
| Ngày | 18/11/2026 |
| Trạng thái | Nháp. Phần mô hình, bộ phân tích và giao diện làm được ngay bằng dữ liệu giả; phần nguồn dữ liệu thật bị chặn bởi G-DVC-03/06 và mẫu HTML (mục 9) |
| Feature | FT-DVC-02 ([FEATURE-LIST](../requirements/FEATURE-LIST.md)) |
| SRS | [SRS-DVC](../requirements/SRS-DVC.md): FR-DVC-TTHC-001..007, NFR-DVC-SEC-003, AC-DVC-03 |
| Khảo sát | [khao-sat-tthc.md](../api-samples/khao-sat-tthc.md) mục "0. Thủ tục hành chính" |
| Ảnh mockup | `DVC-02-danh-sach-tthc`, `DVC-02-danh-sach-tthc-tim-hoa-don`, `DVC-02-chi-tiet-tthc` ([wireframes](../wireframes/)) |
| Phụ thuộc | [DD-DVC-01](DD-DVC-01-LOGIN.md) (phiên), [DD-DVC-00](DD-DVC-00-GROUP-CHOOSER.md) (shell nhóm) |

## 2. Mục đích và quyết định

Cho người dùng tìm và đọc danh mục TTHC của cổng Dịch vụ công trong ứng dụng: lọc, phân trang, xem chi tiết. Chỉ đọc.

Quyết định đã chốt (SRS mục 18 và BR-DVC):
- Tra trực tiếp từ cổng, **không** lưu danh mục cục bộ (#5); kết quả chỉ ở bộ nhớ (BR-DVC-07).
- Không có nút "Nộp hồ sơ" (BR-DVC-02, FR-DVC-TTHC-006).
- Không có CAPTCHA ở mục này, nhưng vẫn gọi tuần tự, mỗi lần một yêu cầu (BR-DVC-05).
- HTML của cổng không được thực thi trong ứng dụng; chỉ trích xuất văn bản (NFR-DVC-SEC-003).

## 3. Kiến trúc

```
TthcScreen (WinForms)  →  TthcPresenter  →  ITthcSource  ──►  PortalTthcSource (Release: gửi form trong trang + TthcHtmlParser)
                                                          └─►  MockTthcSource   (Debug: bộ dữ liệu giả, nhãn MÔ PHỎNG)
```

| Thành phần | Tầng | Vai trò |
|---|---|---|
| `TthcQuery` | Domain | `Keyword`, `ProcedureType`, `Declaration`, `Level`, `TaxpayerType` (mỗi trường là mã lọc của cổng hoặc null), `PageNumber` (từ 1) |
| `TthcFilterOptions` | Domain | Danh sách (mã, nhãn) cho từng ô chọn, đọc từ chính trang cổng (FR-DVC-TTHC-001, không mã hóa cứng) |
| `TthcSummary` | Domain | `Code`, `Name`, `Level`, `Agency` (đúng các cột STT, Mã, Tên, Mức độ, Cơ quan) |
| `TthcPage` | Domain | `Items`, `TotalRecords`, `TotalProcedures`, `PageNumber`, `PageCount`, `PageSize` (10) |
| `TthcDetail` | Domain | Mã, Cấp thực hiện, Trình tự, Cách thức, Thành phần hồ sơ, Đối tượng, Cơ quan thực hiện, Địa chỉ tiếp nhận, Kết quả, Điều kiện, Căn cứ pháp lý: mỗi mục là văn bản thuần đã làm sạch |
| `ITthcSource` | Application | `Task<TthcFilterOptions> GetFilterOptionsAsync(ct)`, `Task<TthcPage> SearchAsync(TthcQuery, ct)`, `Task<TthcDetail> GetDetailAsync(string code, ct)` |
| `TthcService` | Application | Kiểm tra đầu vào (mã TTHC hợp lệ, số trang trong khoảng), ánh xạ lỗi, kiểm tra phiên khi cần, ghi nhật ký không chứa nội dung nhạy cảm |
| `TthcHtmlParser` | Infrastructure | HTML mảnh của cổng → `TthcPage` / `TthcDetail` / `TthcFilterOptions`; làm sạch thành văn bản (bỏ thẻ `script`, `style`, `iframe`, thuộc tính `on*`, URL `javascript:`; giữ xuống dòng và danh sách) |
| `MockTthcSource` | Infrastructure (`#if DEBUG`) | Bộ dữ liệu giả vài chục TTHC "(mẫu)", phân trang 10 dòng, lọc theo từ khóa |
| `PortalTthcSource` | Infrastructure (Release) | Gửi form `POST /tthc/thu-tuc-hanh-chinh` và `GET .../chi-tiet-tthc?maTTHC=` **trong trang** qua WebView DVC, nhận HTML, chuyển cho `TthcHtmlParser` |
| `TthcPresenter` / `TthcScreen` | WinForms | Bộ lọc, bảng kết quả, phân trang, khung chi tiết |

Thư viện phân tích HTML: **HtmlAgilityPack** (có nhóm `net45`, dùng được cho net452; kiểm tra khi thêm). Không dùng regex để phân tích HTML.

## 4. Luồng và quy tắc

### 4.1 Mở màn hình

1. Presenter gọi `GetFilterOptionsAsync` một lần, điền các ô Loại TTHC, Tờ khai, Mức độ, Loại người nộp thuế. Lỗi thì ô chọn để trống (vẫn tìm bằng từ khóa) và báo nhẹ.
2. Chưa tìm gì cho tới khi người dùng bấm Tìm (không tự gọi danh sách mặc định 147 bản ghi). Đề xuất tự tìm lần đầu khi mở màn hình là **không**, để giảm tải cổng; chốt khi review.

### 4.2 Tìm và phân trang

- Bấm Tìm: `PageNumber = 1`. Đổi trang hoặc nhập số trang rồi Đi: gọi `SearchAsync` với cùng bộ lọc đang áp dụng, trang mới. **Giữ nguyên bộ lọc đã dùng để tìm**, không lấy từ ô nhập đã bị sửa dở (tránh trang 2 của một bộ lọc khác).
- Không tự tải trước trang chưa xem (FR-DVC-TTHC-003).
- Mỗi lần chỉ một yêu cầu đang chạy; bấm tiếp trong lúc chạy thì bỏ qua hoặc hủy yêu cầu cũ có kiểm soát (không gửi song song).
- Số trang ngoài khoảng (nhỏ hơn 1, lớn hơn `PageCount`) bị chặn ở `TthcService` trước khi gọi cổng.
- Kết quả rỗng: "Không tìm thấy thủ tục phù hợp", giữ bộ lọc.

### 4.3 Chi tiết

- Chọn một dòng (hoặc bấm tên) mở chi tiết `GetDetailAsync(code)`. Mã phải khớp mẫu mã TTHC của cổng (chữ số và dấu chấm, ví dụ `1.010339`), nếu không thì từ chối, không đưa chuỗi tùy ý vào địa chỉ gọi (danh sách trắng, BR-DVC-02).
- Hiển thị theo thứ tự các mục ở mục 3, mỗi mục là văn bản thuần; nút Quay lại về danh sách giữ nguyên bộ lọc và trang.
- Có thể sao chép văn bản chi tiết. (Xuất Excel của danh sách thuộc FT-DVC-08.)

### 4.4 Phiên

- `FR-DVC-TTHC-007`: khi đã đăng nhập thì dùng phiên; khi chưa đăng nhập, thử bản công khai `/tthc/homelogin/tthc` **[Giả định]**. Nếu bản công khai không dùng được thì màn hình hiện "Cần đăng nhập" kèm nút Đăng nhập (mở `DvcLoginForm`).
- Trang trả về trang đăng nhập giữa chừng: `DvcSession.MarkExpired`, dừng, mời đăng nhập lại; không tự đăng nhập (FR-DVC-AUTH-007).

## 5. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Mất mạng/hết thời gian chờ | Báo lỗi, giữ bộ lọc và trang, cho thử lại thủ công |
| Cổng trả HTML không đúng mẫu (đổi giao diện) | Bộ phân tích báo `TthcParseException`; hiện "Cổng đã đổi giao diện, chức năng này tạm chưa dùng được" và ghi nhật ký loại lỗi (không ghi HTML) |
| Mã TTHC không tồn tại | Cổng trả trang trống/lỗi: "Không tìm thấy thủ tục" |
| Hết phiên | Mục 4.4 |
| Cổng trả HTML chứa script | Bộ phân tích bỏ script; không có đường thực thi (NFR-DVC-SEC-003) |

## 6. Bảo mật và quyền riêng tư

- Nội dung TTHC là thông tin công khai, nhưng vẫn không ghi HTML/văn bản chi tiết vào nhật ký; nhật ký chỉ ghi thao tác, số trang, số bản ghi.
- Từ khóa tìm kiếm có thể là tên doanh nghiệp của người dùng: không ghi vào nhật ký.
- Văn bản hiển thị trong điều khiển văn bản thuần (không dùng trình duyệt nhúng để vẽ HTML của cổng).
- Mẫu HTML đưa vào repo (fixture) là dữ liệu công khai của danh mục TTHC, đã bỏ mọi phần thuộc phiên (tên người dùng, token CSRF, cookie).

## 7. Kiểm thử

| Mã | Kiểm thử | Loại |
|---|---|---|
| T-DVC02-01 | Bộ phân tích đọc đúng bảng danh sách từ HTML mẫu: STT, Mã, Tên, Mức độ, Cơ quan; tổng bản ghi, tổng TTHC, số trang | Unit (fixture) |
| T-DVC02-02 | Bộ phân tích đọc đúng tùy chọn của 4 ô chọn | Unit (fixture) |
| T-DVC02-03 | Bộ phân tích đọc đủ 11 mục chi tiết; văn bản thuần, giữ xuống dòng và danh sách | Unit (fixture) |
| T-DVC02-04 | HTML chứa `script`, `on*`, `javascript:`, `iframe` ⇒ không có trong kết quả | Unit |
| T-DVC02-05 | HTML sai mẫu ⇒ `TthcParseException`, không trả dữ liệu rỗng im lặng | Unit |
| T-DVC02-06 | `TthcService`: số trang ngoài khoảng và mã TTHC sai mẫu bị chặn, nguồn không được gọi | Unit |
| T-DVC02-07 | Presenter: đổi trang dùng bộ lọc đã áp dụng, không dùng ô nhập đang sửa | Unit |
| T-DVC02-08 | Presenter: không gửi song song; kết quả rỗng; lỗi giữ bộ lọc | Unit |
| T-DVC02-09 | Nhật ký không chứa từ khóa, HTML hay văn bản chi tiết | Unit |
| T-DVC02-10 | Danh sách trắng địa chỉ: `PortalTthcSource` chỉ tạo hai loại yêu cầu (tìm, chi tiết) | Unit |
| T-DVC02-11 | Tay: tìm "hóa đơn", mở chi tiết, không thấy "Nộp hồ sơ" (AC-DVC-03) trên bản Debug mô phỏng | Thủ công |

Toàn bộ chạy bằng `MockTthcSource` và fixture; không chạm cổng thật (bản Release mới gọi cổng).

## 8. Giao diện

- Bố cục theo mockup: hàng bộ lọc (Tên/Mã, Loại TTHC, Tờ khai, Mức độ, Loại người nộp thuế) và nút Tìm; bảng kết quả với phân trang và ô nhập số trang + Đi; dòng tổng "Tổng số bản ghi / TTHC".
- Chi tiết mở trong khung bên phải hoặc trang con có nút Quay lại (chốt khi làm giao diện).
- Bản Debug: nhãn "MÔ PHỎNG" và bộ dữ liệu có chữ "(mẫu)".
- Mở từ cửa sổ nhóm DVC (thay cho phần giữ chỗ hiện tại).

## 9. Điều kiện chặn

| Mục | Cần | Ảnh hưởng |
|---|---|---|
| Mẫu HTML công khai | **Đã có** (TD02-06): fixture dựng lại từ DOM. Còn thiếu giá trị `hx-vals` của phân trang và token CSRF khi gửi form (G-DVC-03) | Bộ phân tích viết được; nguồn thật vẫn chờ G-DVC-03 |
| G-DVC-03 | **Đã xác nhận 18/11/2026** cho trang công khai: gửi `POST /tthc/homelogin/tthc` (trường `_csrf`, `maTTHC`, `maNghiepVu`, `maToKhai`, `mucDo`, `page` từ 1, `size` 10) kèm header `X-XSRF-TOKEN` (từ meta) và `accept: text/html-partial` ngay trong trang; xem [khảo sát](../api-samples/khao-sat-tthc.md) | Mở khóa `PortalTthcSource` |
| G-DVC-06 | Trang công khai cũng ghi khóa một-tab ở localStorage/sessionStorage của **hồ sơ trình duyệt**; WebView của app có hồ sơ riêng nên không đụng Chrome của người dùng. Chỉ cần giữ một trang DVC trong app; xác nhận lại khi chạy WebView thật | Còn lại: thử với WebView thật ở bản Release |
| Bản công khai | **Đã xác nhận 18/11/2026:** `/tthc/homelogin/tthc` tra cứu được khi chưa đăng nhập; chi tiết ở `/tthc/homelogin/chi-tiet-tthc?maTTHC=`. Nguồn thật không cần đăng nhập | Đóng FR-DVC-TTHC-007 |
| Giá trị ô chọn | Danh sách tùy chọn thật của 4 ô lọc | Đã dự kiến đọc từ trang, nên chỉ cần fixture |

Làm trước được (không bị chặn): mô hình Domain, `ITthcSource`, `TthcService`, `MockTthcSource`, presenter, giao diện. Bộ phân tích viết sau khi có fixture.

## 10. Tiến độ triển khai

| Task | Nội dung | Trạng thái |
|---|---|---|
| TD02-01 | Domain `TthcQuery`, `TthcSummary`, `TthcPage`, `TthcDetail`, `TthcFilterOptions`, `TthcParseException` | Xong 18/11/2026 |
| TD02-02 | `ITthcSource`, `TthcService` (danh sách trắng mã, chặn trang ngoài khoảng, chuẩn hóa lỗi) + T-DVC02-06, 09 | Xong 18/11/2026 |
| TD02-03 | `MockTthcSource` (Debug, 35 bản ghi "(mẫu)") | Xong 18/11/2026 |
| TD02-04 | `TthcSearchSession` (bộ điều khiển UI-độc lập) + T-DVC02-07, 08 (22 test đạt) | Xong 18/11/2026 |
| TD02-05 | `TthcForm` + `TthcDetailForm`, nút "Tra cứu thủ tục hành chính" ở cửa sổ nhóm DVC; Release không có mock nên nút bị khóa | Xong 18/11/2026, đã thử tay (người dùng xác nhận tra cứu mô phỏng OK) |
| TD02-06 | Lấy mẫu HTML công khai (danh sách + chi tiết) vào `tests/.../Fixtures/` | Xong 18/11/2026 (mẫu dựng lại từ DOM, đã làm sạch) |
| TD02-07 | `TthcHtmlParser` (HtmlAgilityPack 1.11.46, chỉ lấy văn bản; bỏ script/style/iframe/object) + T-DVC02-01..05 (6 test đạt, thêm fixture `tthc-filters.sample.html`). Bản công khai không có ô Loại người nộp thuế nên `TaxpayerTypes` rỗng | Xong 18/11/2026 |
| TD02-08 | `IDvcPageRunner` + `PortalTthcSource` (script dựng sẵn, giá trị đi vào dạng literal JSON, danh sách trắng 2 địa chỉ, tuần tự) + T-DVC02-10 (19 test đạt với runner giả); `CefDvcPageRunner` (WebView ngầm CefSharp trên luồng giao diện riêng, hồ sơ riêng trong bộ nhớ) nối vào `DvcServices` ở **Release** (Debug vẫn dùng nguồn giả) | Xong 18/11/2026; **đã thử Release với cổng thật** (người dùng xác nhận chạy OK 21/11/2026): danh sách 147 bản ghi/15 trang, đổi trang, chi tiết đều chạy (xem mục 11) |

## 11. Ghi chú kỹ thuật CefSharp (bài học 18/11/2026)

Lần thử Release đầu tiên bị treo giao diện ~15 giây rồi sập `libcef.dll` (APPCRASH, `FATAL hwnd_util.cc`). Đã tái hiện và khoanh vùng bằng trang cục bộ (không gọi cổng):

- **Nguyên nhân:** gọi `ChromiumWebBrowser.LoadUrl` ngay sau khi hiện cửa sổ chứa, lúc trình duyệt chưa tạo xong. Truyền địa chỉ ngay trong hàm tạo `ChromiumWebBrowser(address)` thì chạy ổn (~0,3 giây) trong mọi tình huống thử: cửa sổ ẩn ngoài màn hình, hộp thoại modal, khởi tạo Chromium muộn hay sớm.
- **Không phải nguyên nhân:** `Locale`, `PersistSessionCookies`, cờ tắt GPU, hộp thoại modal, vị trí cửa sổ chứa.
- **Cách làm hiện tại:** `CefDvcPageRunner` dựng WebView ẩn trên **luồng giao diện riêng** (STA, `Application.Run`), địa chỉ truyền trong hàm tạo, chỉ gọi `LoadUrl` khi đổi sang trang khác sau khi đã nạp xong một trang. Log chẩn đoán kỹ thuật ghi vào nhật ký ứng dụng (không có dữ liệu người dùng).
- **Cảnh báo cho tính năng DVC sau này (đăng nhập thật):** cùng quy tắc: không `LoadUrl` ngay sau `Show()`; kiểm thử WebView bằng trang cục bộ/`example.com`, không chạy spike vào cổng thật.

## 12. Xuất Excel danh mục TTHC (FT-DVC-08, FR-DVC-EXP-002)

- **Hai nút trên thanh phân trang:** "Xuất trang này" (trang đang xem) và "Xuất tất cả" (mọi trang của bộ lọc đã áp dụng). Cả hai bật khi có kết quả; chọn nơi lưu bằng hộp thoại, tên gợi ý `DanhMucTTHC-yyyyMMdd-HHmm.xlsx`.
- **Nội dung tệp:** dòng 1 tiêu đề; các dòng thông tin: phạm vi (trang x/N hoặc tất cả), **bộ lọc bằng nhãn** (không phải mã nội bộ), số bản ghi trong tệp, thời điểm tra cứu; một dòng trống; tiêu đề cột cố định khi cuộn: STT, Mã, Tên thủ tục, Mức độ, Cơ quan thực hiện. Mọi ô là chuỗi (giữ nguyên số 0 đầu), tự xuống dòng, ký tự điều khiển lạ được bỏ.
- **Tất cả các trang:** `TthcSearchSession.FetchAllPagesAsync` tải tuần tự theo **bộ lọc đã áp dụng** (không theo ô đang sửa), nghỉ 300 ms giữa các trang (BR-DVC-05), báo tiến độ "Đang tải trang n/N", không đổi trang đang hiển thị, hủy được bằng cách đóng màn hình. **Bất kỳ trang nào lỗi thì không ghi tệp** (không xuất dữ liệu dở dang).
- **Cài đặt:** `TableExcelWriter` (bộ ghi bảng .xlsx riêng của nhóm DVC, cùng cách làm ADR 0003 nhưng không phụ thuộc bộ ghi hóa đơn) + `TthcExcelExport` (cột, mô tả bộ lọc, tên tệp). Tệp chỉ được ghi khi người dùng chủ động xuất (BR-DVC-07); lỗi ghi (tệp đang mở, không có quyền) báo rõ, không làm hỏng màn hình.
- **Kiểm thử:** 14 test (gói zip đủ phần và cố định dòng tiêu đề, bố cục, số 0 đầu, ký tự đặc biệt, nhãn bộ lọc, danh sách rỗng, tiến độ, tải tuần tự, lỗi một trang, hủy).

| Task | Nội dung | Trạng thái |
|---|---|---|
| TD02-09 | `TableExcelWriter`, `TthcExcelExport`, `FetchAllPagesAsync`, hai nút xuất + test | Xong 21/11/2026, đã thử tay (người dùng xác nhận xuất Excel OK) |

**Thử Release 27/11/2026 (đang đăng nhập):** tìm TTHC chạy đúng và đổi trang hiện các dòng 31–40. Ô "Loại người nộp thuế" chỉ có "(Tất cả)" vì cổng không có danh sách cho ô này: màn hình **ẩn ô** khi danh sách rỗng.
