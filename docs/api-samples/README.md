# API cổng Thuế `hoadondientu.gdt.gov.vn` — hợp đồng đã đối chiếu traffic thật

> **Nguồn:** bản ghi traffic thật của trang Tra cứu (tháng 11/2026), gồm đăng nhập, tra cứu hóa đơn **bán ra**, xem chi tiết, xuất Excel, xuất XML,
> cùng `spec.xlsx` (giao diện tìm kiếm), `DANH SÁCH HÓA ĐƠN.xlsx` (mẫu bảng kê) và `invoice.zip` (gói XML do cổng xuất).
> [`sample-api-web.md`](sample-api-web.md) là bản ghi traffic **đã thay mọi giá trị nhạy cảm bằng placeholder** (`<JWT>`, `<COOKIES…>`, `<PASSWORD>`, `<CAPTCHA>`, `<CAPTCHA_KEY>`). Bản ghi nguyên văn (có mật khẩu, token, cookie thật) phải đặt tên `*.raw.*` hoặc trong `raw/` — các đường dẫn này nằm trong `.gitignore`; sau khi làm sạch mới lưu thành tệp thường và chỉ commit khi không còn bí mật.
>
> **Đã xác nhận bằng mẫu thật:** đăng nhập, tra cứu bán ra và toàn bộ tham số lọc, chi tiết, xuất XML (zip), xuất Excel.
> **08/11/2026:** người dùng báo UAT cổng thật đạt toàn bộ kịch bản (mua vào, nhiều trang, 401/429), nhưng chưa nộp mẫu đã làm sạch. Các mục ⚠ dưới đây vẫn giữ, vì chưa có traffic để đối chiếu; bỏ dấu ⚠ khi có mẫu.
> **Chưa có mẫu thật (suy luận, đánh dấu ⚠):** hành vi bỏ `ttxly` của tra cứu **mua vào** (đã có mẫu URL mua vào), trang kế tiếp qua `state`, kích thước trang tối đa, giới hạn tốc độ. Xem mục 7.

## 1. Cách hoạt động

- Mọi thứ cổng-đặc-thù nằm trong **một file cấu hình** (`PortalApiConfig`, `schemaVersion` 2). Mặc định trong code đã là giá trị thật:
  [`portal-api.config.sample.json`](portal-api.config.sample.json) (có test bảo đảm file này luôn khớp mặc định). `verified` còn `false` cho tới khi hết phần ⚠.
- `PortalHttpApiClient` dựng request/đọc phản hồi theo cấu hình; `MockPortalHandler` (cổng giả trong tiến trình, chỉ Debug) mô phỏng **đúng hình dạng API thật**
  (JWT, RSQL, `datas/total/state`, zip) từ cùng cấu hình. Thứ tự nạp cấu hình: `%LOCALAPPDATA%\HoaDonDienTu\portal-api.json` → `portal-api.json` cạnh file chạy → mặc định.
  File sai cấu trúc hoặc `schemaVersion` cũ bị từ chối.
- Bản Debug chạy toàn bộ trên cổng giả. Bản Release gọi cổng thật nhưng **chưa có WebView** nên chưa đăng nhập được (T01-16).

## 2. Đăng nhập và phiên

Trang đăng nhập của cổng (WebView) gọi:

```
POST /api/security-taxpayer/authenticate
content-type: application/json
{"username":"<MST>","password":"…","cvalue":"<CAPTCHA>","ckey":"<mã CAPTCHA>"}
→ {"token":"<JWT>"}
```

- **Ứng dụng không tự giải CAPTCHA và không lưu hay ghi log `password`, `cvalue`, `ckey`.** Ở form đăng nhập riêng (ADR 0011) người dùng gõ mật khẩu và CAPTCHA, app chuyển chúng một chiều vào lời gọi trên; ở chế độ thủ công người dùng thao tác trên trang cổng. `PortalAuthCapture` chỉ đọc `token` từ phản hồi (tên đăng nhập lấy từ claim `sub`).
- **JWT** (HS512): claim `sub` = MST đăng nhập, `type` = 2, `exp`/`iat` là Unix time (sống khoảng 24 giờ). Ứng dụng chỉ **đọc** payload (`PortalToken`), không xác minh chữ ký; cổng kiểm tra lại ở mỗi lần gọi.
- **Xác minh MST không cần gọi mạng:** `sub` phải trùng MST của kho đang mở (FR-AUTH-011) và `exp` còn hiệu lực. Không có API hồ sơ.
- Giá trị lưu trong bộ nhớ cho các lần gọi sau (`PortalSessionMaterial`, không ghi đĩa, không log):

| Giá trị | Nguồn | Dùng để |
|---|---|---|
| `token` | phản hồi đăng nhập | header `Authorization: Bearer <token>` |
| cookie của host (`jwt`, `JSESSIONID`, cookie WAF `TS…`, `f5avraaaaaaaaaaaaaaaa_session_`…) | kho cookie WebView sau đăng nhập | header `Cookie` (`auth.sendCookies = true`) |
| `User-Agent` của WebView | WebView | cùng `User-Agent` khi gọi API (lớp WAF có thể gắn phiên với trình duyệt) |
| MST, `exp` | claim JWT | đối chiếu kho, báo hết phiên |

- Hết hạn: 401/403 → `AuthExpired` → phiên tải dừng ở `WaitingForLogin`, người dùng đăng nhập lại trong WebView rồi tiếp tục.

### Lấy CAPTCHA (trước khi đăng nhập)

```
GET /api/captcha            (không cần Authorization; mang cookie WAF của phiên WebView)
→ {"key":"<ckey: 24 ký tự hex>","content":"<svg …200x40…>…</svg>"}
```

- Mẫu thật 10/11/2026 (đã làm sạch, xem [`sample-api-web.md`](sample-api-web.md) mục 0): `content` là **SVG thuần** (không base64, không ảnh nhị phân), 2 đường nhiễu và mỗi ký tự là một `<path>` đường viền.
- `key` được gửi lại làm `ckey` trong `POST /api/security-taxpayer/authenticate`; `cvalue` là chữ người dùng đọc từ ảnh.
- `key` gắn với cookie/phiên của lần gọi: lấy CAPTCHA và đăng nhập phải cùng một WebView (cùng `RequestContext`).
- Header gửi kèm giống các API khác (`action;` rỗng, `end-point: /`, `request-id` GUID, `referer` gốc), không có `authorization`.
- ⚠ Chưa biết: thời gian sống của `key`, số lần sai tối đa trước khi cổng khóa hoặc đòi CAPTCHA mới, và cổng có đổi định dạng ảnh theo thời gian không.
- Từ 10/11/2026 ứng dụng **gọi API này** từ WebView ngầm và hiển thị ảnh trên form đăng nhập riêng; người dùng gõ mật khẩu và đọc CAPTCHA, app gửi `POST authenticate` trong cùng trang ([ADR 0011](../decisions/0011-own-login-form-hidden-webview.md)). Bản Debug dùng trình duyệt giả, chưa thử trên cổng thật.

### Header mọi API truy vấn đều gửi (từ traffic thật)

| Header | Giá trị |
|---|---|
| `accept` | `application/json, text/plain, */*` |
| `accept-language` | `vi` |
| `authorization` | `Bearer <token>` |
| `end-point` | `/tra-cuu/tra-cuu-hoa-don` |
| `referer` | `https://hoadondientu.gdt.gov.vn/tra-cuu/tra-cuu-hoa-don` |
| `action` | tên thao tác mã hóa phần trăm (như `encodeURIComponent`), ví dụ `T%C3%ACm%20ki%E1%BA%BFm%20(h%C3%B3a%20%C4%91%C6%A1n%20b%C3%A1n%20ra)` = "Tìm kiếm (hóa đơn bán ra)" |
| `request-id` | GUID mới mỗi lần gọi |
| `cookie`, `user-agent` | từ WebView (bảng trên) |

`action` theo thao tác: **Tìm kiếm** / **Xem hóa đơn** / **Xuất hóa đơn** (Excel) / **Xuất xml**, kèm `(hóa đơn bán ra)`; mua vào dùng `(hóa đơn mua vào)` (đã có mẫu thật 02/11/2026). Máy chủ có bắt buộc `action`/`end-point` hay không chưa rõ nên ứng dụng gửi đủ như trình duyệt.

## 3. Tra cứu hóa đơn

```
GET /api/query/invoices/sold?sort=tdlap:desc&size=15&search=<RSQL>[&state=<con trỏ>]
GET /api/query/invoices/purchase?…            (mẫu thật 02/11/2026, cùng định dạng `search` với bán ra)
```

`search` là biểu thức RSQL, các điều kiện nối bằng `;` (AND). Ví dụ thật (đủ mọi tham số):

```
tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nmmst==1112232234;tthai==6;ttxly==0;khmshdon==1;shdon==323;khhdon==121;unhiem==1;nmcmnd==036402421225
```

| Ô trên form (`spec.xlsx`) | Tham số | Bắt buộc | Giá trị |
|---|---|:---:|---|
| Ngày lập hóa đơn – Từ ngày / Đến ngày | `tdlap=ge=` / `tdlap=le=` | ✓ | `dd/MM/yyyy'T'HH:mm:ss`; từ 00:00:00, đến 23:59:59. Cổng giới hạn khoảng tìm kiếm (UI nói "theo từng tháng"; mẫu thật 02/10–01/11 = 30 ngày vẫn được chấp nhận) |
| MST người mua *(bán ra)* | `nmmst==` | | 10 hoặc 13 số |
| MST người bán *(mua vào)* | `nbmst==` | | 10 hoặc 13 số. URL mẫu và form mua vào đều là **MST người bán** (`nbmst`) |
| CCCD người mua *(cả bán ra và mua vào)* | `nmcmnd==` | | 12 số (9 số với CMND cũ). Form mua vào có ô "CCCD người mua" và URL mẫu mua vào gửi `nmcmnd` |
| Trạng thái hóa đơn (\*) | `tthai==` | | "Tất cả" = không gửi; **1** Hóa đơn mới · **2** thay thế · **3** điều chỉnh · **4** đã bị thay thế · **5** đã bị điều chỉnh · **6** đã bị hủy |
| Kết quả kiểm tra (\*) | `ttxly==` | | "Tất cả" = không gửi; **0** Cục Thuế đã nhận · **1** Đang tiến hành kiểm tra điều kiện cấp mã · **2** CQT từ chối hóa đơn theo từng lần phát sinh · **3** Hóa đơn đủ điều kiện cấp mã · **4** không đủ điều kiện cấp mã · **5** Đã cấp mã hóa đơn · **6** Cục Thuế đã nhận không mã · **7** Đã kiểm tra định kỳ HĐĐT không có mã · **8** Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền |
| Ký hiệu mẫu số hóa đơn | `khmshdon==` | | dropdown 1…9 |
| Ký hiệu hóa đơn | `khhdon==` | | chuỗi, ví dụ `C26TBN`, `121` |
| Số hóa đơn | `shdon==` | | số nguyên, ví dụ `323` (`shdon`, trùng trường phản hồi) |
| Hóa đơn ủy nhiệm | `unhiem==1` | | checkbox: **chỉ gửi khi tích**, bỏ tích thì không gửi gì |

**Khác biệt của tra cứu mua vào** (mẫu thật + ảnh `spec.xlsx`, 02/11/2026):

```
tdlap=ge=02/10/2026T00:00:00;tdlap=le=01/11/2026T23:59:59;nbmst==1234567890;tthai==1;ttxly==6;khmshdon==8;shdon==123;khhdon==321;unhiem==1;nmcmnd==999999999999
```

- Dropdown **Kết quả kiểm tra** của mua vào **không có "Tất cả"**, chỉ 3 giá trị: **5** Đã cấp mã hóa đơn · **6** Cục Thuế đã nhận không mã · **8** Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền (các giá trị 0–4, 7 chỉ có ở bán ra). Ô được đánh dấu (\*) bắt buộc trên form.
- Dropdown **Trạng thái hóa đơn** của mua vào vẫn có "Tất cả" (không gửi `tthai`); mặc định của form là "Hóa đơn mới".
- ⚠ Chưa biết: bỏ hẳn `ttxly` khi gọi API có được không (form bắt buộc chọn nên có thể máy chủ cũng bắt buộc), và ba mã 5/6/8 có phủ hết hóa đơn mua vào hay không. Cho tới khi xác nhận, ứng dụng coi `ttxly` là bắt buộc với mua vào: "Tất cả" = gọi lần lượt ba mã rồi gộp (khử trùng theo khóa chống trùng).
- Hai tab kết quả *Hóa đơn điện tử* / *Hóa đơn có mã khởi tạo từ máy tính tiền*: ⚠ nhiều khả năng là `ttxly` 5, 6 và 8; chưa có traffic phân biệt.

Chuỗi ghép từng ký tự giống URL mẫu (`=`, `;`, `:`, `/` giữ nguyên); thứ tự điều kiện như form. Giá trị chứa ký tự đặc biệt của RSQL được đặt trong dấu nháy kép để không chèn thêm điều kiện.
Mã trạng thái/kết quả kiểm tra và nhãn nằm ở `PortalOptions` (dùng chung cho dropdown, cột bảng kê và dữ liệu giả).

**Phản hồi** — `{"datas":[…], "total":3, "state":null, "time":133}`. Mua vào có đúng cùng hình dạng và cùng bộ trường (mẫu thật 02/11/2026: `total:3`, `state:null`, `ttxly:5`, `nbmst` là người bán, `nmmst` là MST đang đăng nhập, `nmcmnd:null`, `tdlap` là 0 giờ ngày lập theo UTC); test `RealPurchaseResponse_…`. `total` là tổng số bản ghi khớp; `state` là con trỏ trang kế (null = hết) ⚠ (mẫu thật chỉ có `state:null`, tên tham số gửi lại `state` là suy luận).
Trường của mỗi hóa đơn (rút gọn; bản thật có hơn 100 trường, đa số null):

| JSON | Kiểu | `PortalInvoiceDto` | Cột bảng kê |
|---|---|---|---|
| `id` | GUID | `PortalInvoiceId` | — |
| `khmshdon` | số | `FormSymbol` | B Ký hiệu mẫu số |
| `khhdon` | chuỗi | `InvoiceSymbol` | C Ký hiệu hóa đơn |
| `shdon` | **số** | `InvoiceNumber` (text) | D Số hóa đơn |
| `tdlap` | `2026-10-30T17:00:00Z` = **UTC của 0 giờ ngày lập giờ VN** | `IssueDate` (+7 giờ → 31/10/2026) | E Ngày lập |
| `nbmst`, `nbten` | chuỗi | `SellerTaxCode`, `SellerName` | F, G |
| `nmmst` (null nếu cá nhân), `nmcmnd`, `nmten`, `nmdchi` | chuỗi | `BuyerTaxCode`, `BuyerIdNumber`, `BuyerName`, `BuyerAddress` | H (MST, nếu null thì CCCD), I, J |
| `tgtcthue`, `tgtthue`, `ttcktmai`, `tgtphi`, `tgtttbso` | số | `TotalBeforeTax`, `TotalTax`, `TotalDiscount`, `TotalFee`, `TotalPayment` | K, L, M, N, O |
| `dvtte`, `tgia` | chuỗi, số | `Currency`, `ExchangeRate` | P, Q |
| `tthai`, `ttxly` | số | `StatusRaw`, `CheckResultRaw` | R, S (hiển thị bằng nhãn) |
| `unhiem` | 1/null | — | — |
| `nbmst+khhdon+shdon+khmshdon` | | **khóa dùng cho chi tiết và xuất XML** | |

⚠ Bốn trường định danh (`nbmst, khhdon, shdon, khmshdon`) là khóa duy nhất của hóa đơn; `id` không dùng để gọi API khác.

## 4. Chi tiết, in, xuất

- **Chi tiết:** `GET /api/query/invoices/detail?nbmst=…&khhdon=…&shdon=…&khmshdon=…` → JSON như một phần tử tra cứu cộng `hdhhdvu[]` (hàng hóa: `stt, ten, dvtinh, sluong, dgia, thtien, ltsuat, tsuat, tthue, ttkhac…`), `thttltsuat[]`, `qrcode`. Client trả JSON nguyên văn (`GetInvoiceDetailAsync`) cho DD-06/08.
- **In hóa đơn:** cổng không có API in; trang web gọi lại API chi tiết rồi render và gọi hộp thoại in của trình duyệt. Ứng dụng làm tương tự: lấy XML/chi tiết, render HTML (có thể dùng `invoice.html` + `details.js` trong gói xuất XML) rồi in ra PDF.
- **Xuất XML:** `GET /api/query/invoices/export-xml?nbmst=…&khhdon=…&shdon=…&khmshdon=…` → **file zip** gồm `invoice.xml` (hóa đơn đã ký), `invoice.html` + `details.js` (trình xem), `viewinvoice-bg.jpg`, `sign-check.jpg`. Engine tải chỉ lấy `invoice.xml` (đọc thẳng trong bộ nhớ, không giải nén ra đĩa).
  Cấu trúc `invoice.xml` (mẫu `invoice.zip`): `HDon/DLHDon[@Id=DuLieuKy]/{TTChung{PBan,THDon,KHMSHDon,KHHDon,SHDon,NLap,HDCTTChinh,DVTTe,TGia,HTTToan,MSTTCGP}, NDHDon{NBan{Ten,MST,DChi,DCTDTu,SDThoai}, NMua{Ten,MST,DChi}, DSHHDVu/HHDVu*{TChat,STT,THHDVu,DVTinh,SLuong,DGia,TSuat,ThTien,TTKhac}, TToan{THTTLTSuat/LTSuat*{TSuat,TThue,ThTien},TgTCThue,TgTThue,TgTTTBSo,TgTTTBChu}}}`, `MCCQT` (mã của cơ quan thuế), `DSCKS/{NBan,CQT}/Signature` (XMLDSig, X.509). Phải lưu **byte nguyên văn** để chữ ký còn kiểm tra được.
- **Xuất Excel của cổng:** `GET /api/query/invoices/export-excel?sort=tdlap:desc&search=<RSQL>` → file `.xlsx` theo mẫu bên dưới. Ứng dụng **tự xuất** từ dữ liệu đã lưu (cho phép khoảng thời gian nhiều tháng) nên không gọi API này.

## 5. Bảng kê Excel (`DANH SÁCH HÓA ĐƠN.xlsx`)

Một sheet; `A3:S3` gộp ô "DANH SÁCH HÓA ĐƠN"; `A4:S4` gộp ô "Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy"; dòng 6 là tiêu đề; dữ liệu từ dòng 7. 19 cột:

`STT · Ký hiệu mẫu số · Ký hiệu hóa đơn · Số hóa đơn · Ngày lập · MST người bán/MST người xuất hàng · Tên người bán/Tên người xuất hàng · MST người mua/MST người nhận hàng · Tên người mua/Tên người nhận hàng · Địa chỉ người mua · Tổng tiền chưa thuế · Tổng tiền thuế · Tổng tiền chiết khấu thương mại · Tổng tiền phí · Tổng tiền thanh toán · Đơn vị tiền tệ · Tỷ giá · Trạng thái hóa đơn · Kết quả kiểm tra hóa đơn`

- Ngày lập `dd/MM/yyyy` (giờ VN); số tiền là số; cột H lấy MST người mua, nếu là cá nhân thì lấy CCCD (mẫu `spec.xlsx` dòng 7); cột M/N để trống khi cổng không có giá trị; R, S là **nhãn** của `tthai`, `ttxly` (xem `PortalOptions`).
- Độ rộng cột theo mẫu: A≈8, B–D≈11,7, E≈19,5, F–J≈31,3, K–L≈11,7, M–O≈19,5, P–Q≈11,7, R≈19,5, S≈50,8.

## 6. Giao diện tìm kiếm (`spec.xlsx`)

Menu **Tra cứu** có hai tab: *Tra cứu hóa đơn điện tử bán ra* và *…mua vào*; mỗi tab còn hai nhóm kết quả (*Hóa đơn điện tử*, *Hóa đơn có mã khởi tạo từ máy tính tiền*). Form bán ra: MST người mua, CCCD người mua, Trạng thái hóa đơn (\*), Ngày lập hóa đơn (\*) từ–đến, Kết quả kiểm tra (\*), Ký hiệu mẫu số hóa đơn, Số hóa đơn, Ký hiệu hóa đơn, Hóa đơn ủy nhiệm; form mua vào giống nhưng ô đầu là MST người bán, Kết quả kiểm tra chỉ có 3 giá trị; nút *Tìm kiếm*, *Bỏ tìm kiếm*; kết quả phân trang (mặc định 15/trang) với các nút xuất Excel, in, xuất XML.
Yêu cầu của PM (sheet *Requirement*): đăng nhập tích hợp WebView gọi thẳng trang login của cổng; tìm theo tháng, **quý, năm hoặc khoảng ngày tự nhập**; xuất bảng kê theo mẫu; in ra PDF; xuất XML thành file zip. Ứng dụng làm đúng form này ở SCR-03 (chiều mua/bán, bộ lọc theo bảng mục 3).

## 7. Còn phải xác nhận khi có tài khoản/hóa đơn mua vào (T00-02)

| ⚠ | Cần ghi traffic |
|---|---|
| Tra cứu **mua vào**: bỏ `ttxly` được không | Đường dẫn, `nbmst`, `nmcmnd`, `action` đã xác nhận (02/11/2026). Còn: gọi không có `ttxly` có bị 400 hay trả thiếu; 5/6/8 có phủ hết; tab "có mã khởi tạo từ máy tính tiền" có dùng cùng endpoint không |
| Trang kế tiếp | tên tham số gửi lại `state`, khi nào `state` khác null, `size` tối đa |
| Hóa đơn có mã khởi tạo từ máy tính tiền | tab riêng; có thể là endpoint khác (`sco-…`); hiện chưa hỗ trợ |
| Kiểm tra header | máy chủ có kiểm tra `action`/`end-point`/`request-id` không (`action` mua vào đã biết: `Tìm kiếm (hóa đơn mua vào)`) |
| Giới hạn tốc độ, mã lỗi | 429/403 thật, `Retry-After`, nhịp gọi an toàn (G-05) |
| Lỗi hết phiên | mã HTTP và thân khi token hết hạn (hiện coi 401/403 là `AuthExpired`) |

Khi đã khớp: đặt `"verified": true`, cập nhật [ADR 0001](../decisions/0001-portal-api-contract.md) và đóng G-02.

## 8. Dữ liệu giả (chỉ Debug)

`MockInvoiceDataset` sinh dữ liệu xác định theo MST, **đúng tên trường và kiểu của cổng thật**, phủ 2025-01…2026-12 cho cả mua vào và bán ra: tháng nhiều trang (mua vào 2026-07: 120 hóa đơn),
tháng rỗng (bán ra 2026-03), tháng một hóa đơn (bán ra 2026-02), người mua cá nhân chỉ có CCCD, hóa đơn ủy nhiệm, đủ trạng thái 1–6 và kết quả kiểm tra, ngoại tệ.
`MockPortalHandler` xác thực JWT (`sub`, `exp`), phân tích RSQL, trả `datas/total/state`, chi tiết, và zip xuất XML; có thể bơm lỗi: `EnqueueFailure(status, retryAfter)`, `EnqueueNetworkError()`, `Latency`, `MaxPageSize`,
`RejectedRsqlFields`, `OmitJsonField`, `MalformedSearchResponse`, `MalformedExportResponse`. Token thử: `MockInvoiceDataset.TokenFor(mst)` / `ExpiredTokenFor(mst)`.

## Licence server (`license-api.config.sample.json`)

Hợp đồng nháp của `LicenseApiClient` (chưa xác minh với server thật, gate G-06): `POST {baseUrl}/activate|validate` nhận `{licenseKey, machineIdHash, appVersion}` và trả `{licenseType, machineIdHash, issuedAtUtc, expiresAtUtc|null, features[], signature}`; `POST {baseUrl}/deactivate` nhận `{licenseKey, machineIdHash}`. Từ chối nghiệp vụ là 4xx với `{"error": "key_invalid|bound_to_other_machine|expired|revoked"}`; 5xx, 429, timeout và mất mạng được coi là server tạm lỗi, không phải thu hồi.

Đặt bản đã điền thành `license-api.json` trong `%LOCALAPPDATA%\HoaDonDienTu\` hoặc cạnh file chạy. `baseUrl` phải là HTTPS và `publicKeyXml` (RSA XML) là khóa công khai dùng xác minh chữ ký. Thiếu hoặc sai file thì bản Release vẫn chạy chế độ chuẩn và kích hoạt báo chưa kết nối được máy chủ licence.
