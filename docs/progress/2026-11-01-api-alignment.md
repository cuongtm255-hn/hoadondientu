# Ghi chú bàn giao — đối chiếu API cổng Thuế thật (01/11/2026)

## Việc đã làm

Đối chiếu code và tài liệu với traffic thật trong `docs/api-samples` (`sample-api-web.md` — đã làm sạch, `spec.xlsx`, `DANH SÁCH HÓA ĐƠN.xlsx`, `invoice.zip`). Hợp đồng đã làm sạch: [api-samples/README.md](../api-samples/README.md).

| Nhóm | Thay đổi |
|---|---|
| Bộ lọc | `InvoiceSearchFilter` theo form cổng (thêm `BuyerIdNumber`/CCCD); `PortalOptions` (mã + nhãn `tthai`, `ttxly`, `khmshdon`); `InvoiceFilterValidator`; `IPortalCapabilities.SupportsFilter(direction, field)` |
| Cấu hình | `PortalApiConfig` schema 2: `auth.authenticatePath/tokenResponsePath`, `search.rsql`, `soldFilterFields`/`purchaseFilterFields`, `search.response.timeZoneOffsetHours`, `detail`, `download.responseMode=Zip/zipEntryName`, `exportExcel`, `http.requestIdHeader/actionHeader`, `endpoint.action`; bỏ Profile, Page, POST/Body, Base64 |
| Client | `PortalRequestBuilder` dựng `search=<RSQL>` y hệt URL mẫu; parser đọc `datas/total/state`, đổi ngày UTC→giờ VN, `ExtractXml` từ zip; `GetInvoiceDetailAsync`; header `action`, `request-id`, `end-point`, `Cookie`, `User-Agent` của WebView |
| Xác thực | `PortalToken` (JWT `sub/exp/iat`), `PortalAuthCapture` (token + cookie + UA, không đọc mật khẩu/CAPTCHA), `PortalAuthenticationService` xác minh bằng JWT (không gọi mạng), `PortalSessionMaterial.UserAgent/Token` |
| Cổng giả | `MockPortalHandler`/`MockInvoiceDataset` mô phỏng đúng hình dạng thật: JWT, RSQL, `datas/total/state`, chi tiết, zip xuất XML, XML đúng cấu trúc `invoice.xml`, người mua cá nhân (CCCD), ủy nhiệm, đủ trạng thái |
| UI SCR-03 | Thẻ bộ lọc: MST (nhãn theo chiều), CCCD (chỉ bán ra), trạng thái/kết quả kiểm tra/ký hiệu mẫu số là dropdown đúng mã, ký hiệu, số, ủy nhiệm, thông báo lỗi định dạng |
| Test | Viết lại bộ `Portal*` (request khớp URL mẫu từng ký tự, parser với phản hồi thật, client+mock, auth, config); thêm test presenter/validator |
| Tài liệu | ADR 0001, DD-00/01/04/05/06/07/08/09, PROJECT-PLAN, api-samples |

## Còn mở (⚠ cần traffic thật)

1. Tra cứu **mua vào**: đường dẫn `/api/query/invoices/purchase`, trường `nbmst`, có `nmcmnd` không, chuỗi `action`.
2. Phân trang: tham số gửi lại `state`, `size` tối đa (mặc định 50 là giả định).
3. Tab "hóa đơn có mã khởi tạo từ máy tính tiền" (endpoint khác?) — chưa hỗ trợ.
4. Mã lỗi/thân khi hết phiên, 429 thật (G-05).
5. WebView thật để ghi lại cuộc gọi đăng nhập (T01-16, G-01).

## Cần lưu ý

- `sample-api-web.md` từng chứa **mật khẩu, token và cookie thật** và bị commit nhầm ở `be254a2`. Lịch sử đã được viết lại (force-push) để loại file; tệp hiện tại đã thay mọi giá trị nhạy cảm bằng placeholder. Bản nguyên văn phải đặt tên `*.raw.*` (git-ignored). Mật khẩu tài khoản cổng Thuế đó cần được đổi.
- File `portal-api.json` cũ (schema 1) trong `%LOCALAPPDATA%` không còn hợp lệ; ứng dụng ghi log và dùng cấu hình mặc định.
