# DD-DVC-03 — Tra cứu hồ sơ đã nộp

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-DVC-03 |
| Phiên bản | 0.1 (nháp) |
| Ngày | 21/11/2026 |
| Trạng thái | Nháp. Phần mô hình, dịch vụ CAPTCHA, bộ điều khiển và giao diện làm được ngay bằng dữ liệu giả; **bộ phân tích HTML và nguồn thật bị chặn** (cần đăng nhập thật và mẫu HTML có dữ liệu, G-DVC-02, G-DVC-04) |
| Feature | FT-DVC-03 ([FEATURE-LIST](../requirements/FEATURE-LIST.md)) |
| SRS | [SRS-DVC](../requirements/SRS-DVC.md): FR-DVC-HS-001..006, BR-DVC-03/05/07, AC-DVC-04 |
| Khảo sát | [khao-sat-tthc.md](../api-samples/khao-sat-tthc.md) mục 1a (trên DVC), 1b (trên thuế điện tử) |
| Ảnh mockup | `DVC-03a-da-nop-dvc`, `DVC-03a-da-nop-dvc-ket-qua-trong`, `DVC-03b-da-nop-thue-dien-tu`, `DVC-03b-da-nop-thue-dien-tu-ket-qua-trong` ([wireframes](../wireframes/)) |
| Phụ thuộc | [DD-DVC-01](DD-DVC-01-LOGIN.md) (phiên đăng nhập), [DD-DVC-02](DD-DVC-02-TTHC-CATALOG.md) (WebView ngầm `IDvcPageRunner`, mẫu thiết kế nguồn thật) |

## 2. Mục đích và khác biệt với danh mục TTHC

Cho người dùng đã đăng nhập tra cứu hồ sơ đã nộp theo hai nguồn: **trên Dịch vụ công** và **trên thuế điện tử** (tờ khai nộp trước 01/07/2025). Chỉ đọc.

Khác với [DD-DVC-02](DD-DVC-02-TTHC-CATALOG.md):

| | DD-DVC-02 (danh mục TTHC) | DD-DVC-03 (hồ sơ đã nộp) |
|---|---|---|
| Đăng nhập | Không cần | **Bắt buộc** |
| CAPTCHA | Không | **Mỗi lần tìm một mã mới do người dùng gõ** (BR-DVC-03) |
| Dữ liệu | Công khai | **Riêng tư** của doanh nghiệp (mã hồ sơ, MST, trạng thái) |
| Khối lượng | Duyệt nhiều trang | Mỗi trang kết quả cũng có thể cần CAPTCHA mới **[Giả định, G-DVC-05]** nên không tải hàng loạt |

Quyết định đã chốt (SRS):
- Không giải CAPTCHA, không lưu hay tái dùng mã, **không tự thử lại** yêu cầu có CAPTCHA (BR-DVC-03, 05).
- Kết quả chỉ ở bộ nhớ; chỉ ghi ra đĩa khi người dùng chủ động xuất (BR-DVC-07, FR-DVC-EXP).
- Dữ liệu hồ sơ là nhạy cảm: không ghi vào nhật ký, không gắn vào báo lỗi.

## 3. Kiến trúc

```
HoSoForm (WinForms) → HoSoSearchSession → HoSoService → IHoSoSource ─► PortalHoSoSource (Release: form trong trang + CAPTCHA + HoSoHtmlParser)
                                              │                     └► MockHoSoSource   (Debug: dữ liệu giả, CAPTCHA giả)
                                              └► DvcSession (phải LoggedIn)
```

| Thành phần | Tầng | Vai trò |
|---|---|---|
| `HoSoSource` (enum `Dvc`, `ThueDienTu`) | Domain | Hai tab nguồn |
| `HoSoQuery` | Domain | Chung: `Source`, `FromDate`, `ToDate`, `AuthorizedTaxCode`; tab DVC: `ProcedureType`, `ProcedureName`, `Declaration`, `FileCode`; tab thuế điện tử: `Declaration`, `TransactionCode`; `PageNumber`; **`CaptchaText`** (chỉ sống trong bộ nhớ, xóa ngay sau gửi) |
| `HoSoRow` / `HoSoPage` | Domain | Dòng và trang kết quả. **Cột chưa biết** (chưa có mẫu có dữ liệu): mô hình dự kiến giữ danh sách cặp cột/giá trị văn bản (`Cells`) để không đoán tên cột; chốt cột cụ thể sau G-DVC-04 |
| `IHoSoSource` | Application | `Task<byte[]> GetCaptchaAsync(HoSoSource, ct)`, `Task<HoSoPage> SearchAsync(HoSoQuery, ct)` |
| `HoSoService` | Application | Yêu cầu `DvcSession.IsLoggedIn`; kiểm tra bộ lọc (khoảng ngày hợp lệ, CAPTCHA không rỗng, số trang); ánh xạ lỗi; không ghi nhật ký nội dung |
| `HoSoSearchSession` | Application | Bộ điều khiển UI-độc lập: giữ bộ lọc đã áp dụng, một yêu cầu tại một thời điểm, quản lý vòng đời CAPTCHA (mỗi lần tìm xóa mã cũ, tải mã mới khi sai) |
| `HoSoHtmlParser` | Infrastructure | HTML mảnh của cổng → `HoSoPage`, nhận diện thông điệp "Không có dữ liệu" và lỗi CAPTCHA; chỉ lấy văn bản (cùng quy tắc `TthcHtmlParser`) |
| `MockHoSoSource` | Infrastructure/WinForms (`#if DEBUG`) | Dữ liệu giả "(mẫu)", CAPTCHA giả tự vẽ, mô phỏng đúng/sai CAPTCHA |
| `PortalHoSoSource` | Infrastructure (Release) | Gửi trong trang qua `IDvcPageRunner` (như `PortalTthcSource`); lấy ảnh CAPTCHA `GET /tthc/getCaptcha?<tem thời gian>` |
| `HoSoForm` | WinForms | Hai tab, bộ lọc, ô CAPTCHA + ảnh + Đổi mã, bảng kết quả |

## 4. Luồng và quy tắc

### 4.1 Điều kiện vào

- Chỉ mở được khi `DvcSession.IsLoggedIn`; nếu chưa, hiện "Cần đăng nhập" và nút mở `DvcLoginForm`.
- Hết phiên giữa chừng (trang trả về trang đăng nhập): `DvcSession.MarkExpired`, dừng, mời đăng nhập lại; **không tự đăng nhập, không tự thử lại** (FR-DVC-AUTH-007).

### 4.2 Tìm

1. Người dùng nhập bộ lọc và **gõ CAPTCHA đang hiện** (ảnh lấy khi mở tab hoặc bấm Đổi mã).
2. Bấm Tìm: `HoSoService` kiểm tra đầu vào (CAPTCHA không rỗng, `FromDate <= ToDate`, khoảng ngày trong giới hạn **[G-DVC-05]**), gửi **một** yêu cầu.
3. Sau mỗi lần gửi (thành công hay không) mã CAPTCHA đã dùng **bị xóa** khỏi ô và bộ nhớ; ảnh CAPTCHA được tải lại cho lần sau.
4. **Sai CAPTCHA:** thông báo "Mã xác nhận không đúng", giữ nguyên các bộ lọc khác, tải mã mới, **không** gửi lại tự động (FR-DVC-HS-004).
5. Kết quả rỗng: hiện đúng thông điệp của cổng ("Không có dữ liệu" / "Không có dữ liệu thỏa mãn điều kiện tìm kiếm!"), giữ bộ lọc (FR-DVC-HS-005).

### 4.3 Phân trang

- **Quyết định đã chốt (21/11/2026): đổi trang KHÔNG cần CAPTCHA mới.** Trang cổng gọi `onChangePage(trang, 10)` gửi lại toàn bộ giá trị form kể cả ô CAPTCHA; app làm giống: đổi trang dùng lại đúng mã của lần tìm đã ra kết quả này. Mã chỉ ở bộ nhớ của `HoSoSearchSession`, bị xóa khi: bấm Tìm mới (giữ lại chỉ nếu lần tìm mới thành công), đổi tab nguồn, lỗi (kể cả cổng từ chối), hết phiên, đóng màn hình.
- **Giả định chưa kiểm chứng:** tài khoản thử chỉ có một trang kết quả nên chưa thấy cổng có chấp nhận hay không. **Phòng ngừa:** nếu cổng từ chối khi đổi trang (HTTP 400, sai CAPTCHA) thì app báo "Cổng cần mã xác nhận mới: nhập mã trong ảnh rồi bấm lại", xóa mã đã giữ, không tự gửi lại; người dùng gõ mã mới rồi bấm đổi trang, mã mới thay thế và được giữ cho các trang sau. Nhờ vậy giả định sai không làm hỏng chức năng, chỉ thêm một bước gõ mã.
- Đổi trang dùng bộ lọc đã áp dụng (không dùng ô đang sửa dở), không tự tải trước trang chưa xem, 10 dòng mỗi trang.
- **Xuất tất cả các trang** (FT-DVC-08) khả thi theo giả định trên: tải tuần tự có nghỉ giữa các trang (BR-DVC-05); dừng ngay và không ghi tệp dở dang nếu cổng từ chối. Làm sau khi có nguồn thật.

### 4.4 Cảnh báo tab thuế điện tử

- Luôn hiện cảnh báo của cổng: chỉ hỗ trợ tờ khai nộp trước 01/07/2025; mặc định khoảng ngày theo cổng (FR-DVC-HS-003).

### 4.5 Một tab, một yêu cầu

- Chỉ một yêu cầu tại một thời điểm (BR-DVC-05); WebView chỉ giữ một trang DVC (BR-DVC-06). Chuyển giữa hai tab nguồn chỉ đổi form trong cùng trang hoặc nạp trang tương ứng, không mở trang thứ hai.

## 5. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Chưa đăng nhập / hết phiên | Mục 4.1 |
| Sai CAPTCHA | Mục 4.2 bước 4 |
| Đầu vào sai (ngày, CAPTCHA rỗng) | Báo tại ô tương ứng, không gọi cổng |
| Mất mạng, hết thời gian chờ | Báo lỗi, giữ bộ lọc, xóa CAPTCHA, cho thử lại thủ công (mã mới) |
| Cổng đổi giao diện (không đọc được mẫu) | `HoSoParseException` → "Cổng đã đổi giao diện, chức năng này tạm chưa dùng được"; ghi nhật ký loại lỗi, không ghi HTML |
| Cổng giới hạn tần suất/khóa tạm | Báo thông điệp của cổng, không thử lại tự động (G-DVC-05) |

## 6. Bảo mật và quyền riêng tư

- **CAPTCHA:** không ghi nhật ký, không lưu đĩa, không đưa vào báo lỗi; chỉ trong biến của lần gửi.
- **Dữ liệu hồ sơ** (mã hồ sơ, MST, tên người nộp, trạng thái): không ghi nhật ký; nhật ký chỉ ghi thao tác ("Tìm hồ sơ DVC: n bản ghi", loại lỗi).
- **MST ủy quyền** và các ô lọc là dữ liệu nhạy cảm: không ghi nhật ký; không lưu tạm giữa các lần mở màn hình.
- **Xuất Excel** (FT-DVC-08) là đường duy nhất đưa kết quả ra đĩa, do người dùng chủ động.
- HTML của cổng không được thực thi; chỉ trích văn bản (NFR-DVC-SEC-003).
- Script gửi chạy trong trang bằng cơ chế CSRF của cổng, giá trị người dùng đi vào dạng literal JSON (giống `PortalTthcSource`).

## 7. Kiểm thử

| Mã | Kiểm thử | Loại |
|---|---|---|
| T-DVC03-01 | Chưa đăng nhập ⇒ `HoSoService` từ chối, không gọi nguồn | Unit |
| T-DVC03-02 | CAPTCHA rỗng, ngày đảo ngược ⇒ chặn trước khi gọi nguồn | Unit |
| T-DVC03-03 | Sau mỗi lần bấm Tìm (thành công, sai CAPTCHA, lỗi mạng) mã trong ô bị xóa, ảnh được tải lại, **không** gửi lại tự động; đổi trang dùng lại mã của lần tìm, quên mã khi tìm mới lỗi, cổng từ chối, đổi tab, đóng | Unit |
| T-DVC03-04 | Sai CAPTCHA giữ nguyên bộ lọc, trả lỗi `WrongCaptcha` | Unit |
| T-DVC03-05 | Kết quả rỗng trả trang rỗng hợp lệ kèm thông điệp, không phải lỗi | Unit |
| T-DVC03-06 | Hết phiên ⇒ `DvcSession` = Expired, dừng, không tự đăng nhập | Unit |
| T-DVC03-07 | Không gửi song song; thao tác thứ hai khi bận bị bỏ qua | Unit |
| T-DVC03-08 | Nhật ký không chứa CAPTCHA, MST ủy quyền, mã hồ sơ, nội dung dòng | Unit |
| T-DVC03-09 | Bộ phân tích đọc bảng, tổng/số trang, thông điệp rỗng, lỗi CAPTCHA từ HTML mẫu; bỏ script/`on*`/`javascript:` | Unit (fixture, **sau G-DVC-04**) |
| T-DVC03-10 | `PortalHoSoSource` chỉ tạo các loại yêu cầu trong danh sách trắng; giá trị người dùng là literal JSON | Unit (runner giả, **sau G-DVC-02/04**) |
| T-DVC03-11 | Tay: tìm bằng CAPTCHA mô phỏng đúng và sai trên bản Debug (AC-DVC-04) | Thủ công |

Toàn bộ chạy bằng nguồn giả và mẫu HTML đã làm sạch; không chạm cổng thật.

## 8. Giao diện

- Bố cục theo mockup `DVC-03a`, `DVC-03b`: hai tab; hàng bộ lọc; ô CAPTCHA kèm ảnh và nút Đổi mã; nút Tìm; bảng kết quả; dòng tổng/trang; thông điệp rỗng.
- Bản Debug: nhãn "MÔ PHỎNG" và dữ liệu "(mẫu)".
- Mở từ cửa sổ nhóm DVC (nút "Hồ sơ đã nộp", chỉ bật khi đã đăng nhập).

## 9. Điều kiện chặn

| Mục | Cần | Ảnh hưởng |
|---|---|---|
| G-DVC-02 | Đăng nhập thật (`TD01-05`) để có phiên | Không có phiên thì không gọi được cổng thật |
| G-DVC-04 | HTML **có dữ liệu** của hai tab: cột bảng, phân trang, định dạng ngày, thông điệp lỗi CAPTCHA, địa chỉ và tên trường thật của form (`GET /tthc/ho-so/search` mới là suy đoán) | Bộ phân tích, cột `HoSoRow`, `PortalHoSoSource` |
| G-DVC-05 | Giới hạn khoảng ngày, CAPTCHA mỗi trang hay mỗi lần tìm, giới hạn tần suất, khóa khi sai CAPTCHA nhiều lần | Mục 4.3, 5 |
| G-DVC-06 | WebView nhúng sau đăng nhập không bị coi là tab thứ hai (khóa một-tab theo tài khoản) | Cách giữ phiên đăng nhập trong WebView ngầm |

Làm trước được (không bị chặn): mô hình Domain, `IHoSoSource`, `HoSoService`, `HoSoSearchSession`, `MockHoSoSource`, `HoSoForm`, test T-DVC03-01..08, 11. Bộ phân tích và nguồn thật làm sau khi bạn tự đăng nhập một lần để lấy mẫu và đo giới hạn.

**Lưu ý khảo sát:** khảo sát lấy mẫu có dữ liệu cần tài khoản có hồ sơ thật; ảnh chụp màn hình có thể chứa thông tin doanh nghiệp, nên mẫu đưa vào repo phải **làm sạch** (bỏ MST, tên, mã hồ sơ, giá trị phiên) và ảnh gốc để dạng `*.raw.*` (git bỏ qua).

## 10. Tiến độ triển khai

| Task | Nội dung | Trạng thái |
|---|---|---|
| TD03-01 | Domain `HoSoSource`, `HoSoQuery` (có `WithoutCaptcha`), `HoSoRow`, `HoSoPage`, `HoSoParseException` | Xong 21/11/2026 |
| TD03-02 | `IHoSoSource`, `HoSoService` (phải đăng nhập, chặn CAPTCHA rỗng/ngày đảo/trang sai, chuẩn hóa lỗi, nhật ký sạch) + T-DVC03-01, 02, 08 | Xong 21/11/2026 |
| TD03-03 | `HoSoSearchSession` (vòng đời CAPTCHA: bỏ mã sau mỗi lần gửi, tải ảnh mới, không tự gửi lại; tuần tự; giữ bộ lọc đã áp dụng) + T-DVC03-03..07 (13 test đạt) | Xong 21/11/2026 |
| TD03-04 | `MockHoSoSource` (Debug; CAPTCHA tự vẽ, **dùng một lần**, hai nguồn, 23 bản ghi "(mẫu)", thông điệp rỗng đúng như cổng) | Xong 21/11/2026 |
| TD03-05 | `HoSoForm` + nút "Hồ sơ đã nộp" ở cửa sổ nhóm DVC (chỉ bật khi đã đăng nhập; Release chưa có nguồn nên khóa) | Xong 21/11/2026, cần thử tay |
| TD03-06 | Lấy mẫu HTML có dữ liệu + đo giới hạn (người dùng tự đăng nhập; G-DVC-02, 04, 05) | Chưa làm, cần người dùng |
| TD03-07 | `HoSoHtmlParser` + T-DVC03-09 | Chưa làm, sau TD03-06 |
| TD03-08 | `PortalHoSoSource` (Release) + T-DVC03-10 | Chưa làm, sau TD03-06 và TD01-05 |
