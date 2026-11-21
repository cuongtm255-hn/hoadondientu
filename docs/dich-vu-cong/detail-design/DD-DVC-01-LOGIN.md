# DD-DVC-01 — Đăng nhập cổng Dịch vụ công

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-DVC-01 |
| Phiên bản | 0.1 (nháp) |
| Ngày | 16/11/2026 |
| Trạng thái | Nháp. **Bị chặn một phần bởi G-DVC-02 và G-DVC-06** (xem mục 8) |
| Feature | FT-DVC-01 |
| SRS | [SRS-DVC](../requirements/SRS-DVC.md): FR-DVC-AUTH-001..009, BR-DVC-03/04/06, NFR-DVC-SEC-001, AC-DVC-02/07/10 |
| ADR | [DVC-0001](../decisions/0001-htmx-webview-integration.md) (đề xuất); tham chiếu ADR 0007 (CefSharp), 0011 (form đăng nhập riêng) của nhóm Hóa đơn |
| Phụ thuộc | DD-DVC-00 (shell nhóm), DD-02 (MST đang chọn) |

## 2. Quyết định đã chốt

- Chỉ **Thuế điện tử – Doanh nghiệp** ở v1 (SRS mục 18 #4).
- Tên đăng nhập điền sẵn MST đang chọn, cho sửa; chỉ nhớ tên đăng nhập, không nhớ mật khẩu (#7).
- Mật khẩu và CAPTCHA do người dùng gõ; app không lưu, không ghi nhật ký (BR-DVC-03/04).
- Phiên DVC tách hẳn phiên Hóa đơn: WebView, thư mục cache/cookie riêng (BR-DVC-01).

## 3. Luồng

```
DvcLoginForm mở
  → DvcWebSession.EnsureReady()      nạp /tthc/login trong WebView ngầm, chọn "Thuế điện tử" → "Doanh nghiệp"
  → GetCaptchaImage()                GET /tthc/getCaptcha?<ts> chạy TRONG trang → byte ảnh hiển thị trên form
  → người dùng gõ tên đăng nhập, mật khẩu, CAPTCHA → bấm Đăng nhập
  → SubmitLogin(tenDN, matKhau, captcha)   POST /tthc/loginLDAP chạy TRONG trang (kèm doiTuong)
  → đọc JSON `status` + điều hướng /tthc/home
  → thành công: DvcSession.State = LoggedIn, hiện tên tổ chức; lỗi: xoá mật khẩu + CAPTCHA, tải CAPTCHA mới
```

- Gọi `loginLDAP` bằng cách chạy chính hàm đăng nhập của cổng trong trang (hoặc `fetch` cùng origin kèm CSRF của trang), để không phải sao chép cookie/CSRF ra ngoài WebView (ADR DVC-0001).
- Không bao giờ đưa mật khẩu qua chuỗi JS ghép tay không escape: truyền bằng tham số đã mã hoá JSON (`JSON.stringify`) và xoá biến ngay sau khi gửi.

## 4. Thiết kế

| Thành phần | Tầng | Vai trò |
|---|---|---|
| `IDvcLoginBrowser` | Application | `Task<byte[]> GetCaptchaAsync()`, `Task<DvcLoginResult> SubmitAsync(DvcCredentials, string captcha)`, `Task LogoutAsync()`, `bool IsLoggedIn` |
| `DvcCredentials` | Domain | `UserName`, `Password` (chỉ trong bộ nhớ, xoá sau dùng); `ToString()` không lộ giá trị |
| `DvcLoginResult` | Domain | `Success`, `FailureReason` (enum: `WrongPassword`, `WrongCaptcha`, `Locked`, `AnotherTab`, `Network`, `Unknown`), `OrganizationName` |
| `DvcSession` | Application | Trạng thái phiên (NotLoggedIn / LoggedIn / Expired / TakenByOtherTab), sự kiện thay đổi |
| `DvcLoginService` | Application | Điều phối form ↔ `IDvcLoginBrowser`; ánh xạ lỗi; không giữ mật khẩu |
| `CefDvcLoginBrowser` | Infrastructure (Release) | Cài đặt bằng CefSharp, profile riêng `dvc` |
| `MockDvcLoginBrowser` | Infrastructure (`#if DEBUG`) | Trả CAPTCHA giả, đăng nhập giả; nhãn "MÔ PHỎNG" |
| `DvcLoginForm` | WinForms | Form đăng nhập: tên đăng nhập, mật khẩu (ẩn/hiện), ảnh CAPTCHA + nút đổi mã, Đăng nhập, "Đăng nhập trên trang cổng" |
| `DvcUserNameStore` | Infrastructure | Nhớ tên đăng nhập theo MST trong cài đặt người dùng (không có mật khẩu) |

- Phát hiện hết phiên / bị đá tab khác (FR-DVC-AUTH-007): `DvcSession` chuyển `Expired`/`TakenByOtherTab` khi trang trả về trang đăng nhập hoặc thông báo "đang được sử dụng trên một tab khác"; các tác vụ dừng, mời đăng nhập lại, không tự đăng nhập.
- Đăng xuất (AUTH-008): gọi lệnh đăng xuất của cổng nếu xác định được, rồi xoá cookie của profile `dvc`.
- Phương án dự phòng (AUTH-006): nút hiển thị WebView thường; app chỉ quan sát chuyển trang `/tthc/home` để chuyển trạng thái `LoggedIn`.

## 5. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Sai mật khẩu / CAPTCHA | Thông báo rõ, xoá mật khẩu + CAPTCHA, tải mã mới, giữ tên đăng nhập |
| Tài khoản bị khóa | Thông báo, không thử lại tự động |
| Lấy CAPTCHA thất bại | Gợi ý đăng nhập dự phòng |
| Cổng đổi tên trường / `doiTuong` | Gợi ý đăng nhập dự phòng, ghi nhật ký **không** có giá trị nhập |
| Mất mạng | Báo lỗi, cho thử lại thủ công |

## 6. Bảo mật

- Không ghi nhật ký tên đăng nhập kèm mật khẩu, CAPTCHA, cookie, token, thân request; nhật ký chỉ ghi kết quả (`Success`/`FailureReason`).
- `DvcCredentials.Password` dùng `SecureString` hoặc mảng ký tự xoá sau gửi nếu khả thi trên net452; tối thiểu: không giữ trong trường của form sau khi gửi.
- Không có đường dẫn mã tự động giải CAPTCHA (BR-DVC-03).

## 7. Kiểm thử

| Mã | Kiểm thử | Loại |
|---|---|---|
| T-DVC01-01 | Thành công: `DvcSession` = LoggedIn, tên tổ chức hiện | Unit (mock browser) |
| T-DVC01-02 | Sai CAPTCHA/mật khẩu: xoá ô nhạy cảm, tải CAPTCHA mới, giữ tên đăng nhập | Unit |
| T-DVC01-03 | Nhật ký không chứa mật khẩu/CAPTCHA (quét log của mọi kịch bản) | Unit |
| T-DVC01-04 | Tên đăng nhập điền sẵn MST đang chọn, sửa được, nhớ theo MST; không bao giờ lưu mật khẩu | Unit |
| T-DVC01-05 | Trang trả "tab khác" ⇒ `TakenByOtherTab`, tác vụ dừng, không tự đăng nhập lại | Unit |
| T-DVC01-06 | Đăng xuất xoá phiên, trạng thái NotLoggedIn | Unit |
| T-DVC01-07 | Bản Release không tham chiếu `MockDvcLoginBrowser` | Kiểm tra cấu trúc (như ADR 0011) |
| T-DVC01-08 | Tay: đăng nhập thật lần đầu (ngoại lệ duy nhất CLAUDE.md), người dùng tự nhập mọi thứ | Thủ công |

## 8. Điều kiện chặn (cổng kỹ thuật)

| Cổng | Cần xác nhận | Ảnh hưởng |
|---|---|---|
| G-DVC-02 | **Đã xác nhận 21/11/2026**: `POST /tthc/loginLDAP` form urlencoded; `tenDN` nguyên văn, `matKhau` = base64(UTF-8 mật khẩu), `doiTuong` = `DN`, `captcha`; không cần header CSRF; phản hồi `status` "200"/"201" là thành công, khác là lỗi (`desc`). CAPTCHA `GET /tthc/login/getCaptcha?<tem thời gian>`. Còn lại: nội dung lỗi thật (sai CAPTCHA, sai mật khẩu, khóa) | `CefDvcLoginBrowser` làm được; ánh xạ `DvcLoginFailure` theo `desc` cần mẫu lỗi thật |
| G-DVC-06 | WebView nhúng không bị coi là tab thứ hai | Cách giữ WebView ngầm khi đổi nhóm |

Có thể làm trước (không bị chặn): kiểu Domain/Application, `DvcLoginService`, mock browser, form, test.

## 9. Tiến độ triển khai

| Task | Nội dung | Trạng thái |
|---|---|---|
| TD01-01 | Domain `DvcCredentials`/`DvcLoginResult`/`DvcLoginFailure`, `DvcSession` + test | Xong 16/11/2026 |
| TD01-02 | `IDvcLoginBrowser`, `DvcLoginService` + T-DVC01-01..06 (trình duyệt giả trong test; `MockDvcLoginBrowser` Debug làm cùng `DvcLoginForm`) | Xong 16/11/2026 |
| TD01-03 | `IDvcUserNameStore` + `FileDvcUserNameStore` + test | Xong 16/11/2026 |
| TD01-04 | `DvcLoginForm` (code-only), `MockDvcLoginBrowser` (`#if DEBUG`, gắn nhãn MÔ PHỎNG), `DvcLoginMessages`; cửa sổ nhóm DVC có nút Đăng nhập + trạng thái phiên. Release build không chứa mock, nút Đăng nhập bị khóa | Xong 16/11/2026, đã thử tay (người dùng xác nhận đăng nhập mô phỏng OK) |
| TD01-05 | `CefDvcLoginBrowser` (Release): chạy trong trang `/tthc/login` qua `IDvcPageRunner`/luồng WebView riêng: lấy ảnh CAPTCHA, đặt ô `tenDN`/`matKhau`/`ldap`/`captcha` rồi chạy `submitLDAP()` hoặc `fetch` form urlencoded với `matKhau` base64 UTF-8; đọc `status`/`desc`; ánh xạ lỗi (sai CAPTCHA/mật khẩu/khóa) cần mẫu lỗi thật. Quy tắc WebView: địa chỉ truyền trong hàm tạo, không `LoadUrl` ngay sau `Show()` (DD-DVC-02 mục 11) | Chưa làm; **hết chặn G-DVC-02**, còn G-DVC-06 (thử với WebView thật) và mẫu lỗi |
