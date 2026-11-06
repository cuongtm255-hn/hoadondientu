# P6-03 — Rà soát bảo mật (đợt 1, 06/11/2026)

Phạm vi: đọc mã và test các bề mặt NFR-SEC-001…008 (token, log, WebView, trình xem XML, licence, sao lưu/khôi phục, ghi kho). Không chạy trên cổng thật, không pen-test, không quét thư viện bên thứ ba. Kết quả: **1 phát hiện đã sửa**, các mục còn lại ở mức chấp nhận được, một số điểm cần theo dõi.

## Kết quả theo yêu cầu

| Yêu cầu | Kết luận | Căn cứ |
|---|---|---|
| NFR-SEC-001 token chỉ trong bộ nhớ | **Đạt** | Phiên cổng nằm ở `InMemoryPortalSessionStore`; không có chỗ nào ghi token/cookie xuống đĩa hay registry. CefSharp khởi tạo không đặt `CachePath`, `PersistSessionCookies = false`, `LogSeverity.Disable`: cookie và storage của trình duyệt đăng nhập chỉ ở bộ nhớ, mỗi lần đăng nhập một `RequestContext` riêng. Bộ lọc phản hồi chỉ đọc phản hồi của `POST …/authenticate`, không đọc thân request (mật khẩu, CAPTCHA). `PortalSessionMaterial.ToString` không in token. |
| NFR-SEC-002 log che dữ liệu nhạy cảm | **Đạt, có lưu ý** | Mọi log đi qua `FileApplicationLogger` → `LogSanitizer` (Authorization, Cookie, Bearer, JWT, mật khẩu, CAPTCHA, khóa licence, CCCD 12 số, nội dung XML) và mỗi bản ghi một dòng (không chèn được dòng log giả). Rà 60 lệnh log: không có lệnh nào ghi token, cookie hay XML. Lưu ý: đường dẫn kho chứa MST (thư mục đặt theo MST) nên có thể xuất hiện trong thông báo lỗi `IOException`; yêu cầu không bắt buộc che MST nhưng `LogSanitizer.MaskTaxCode` đã có sẵn nếu muốn che. |
| NFR-SEC-003 trình xem XML không script/entity/tài nguyên ngoài | **Đạt** | Mọi chỗ đọc XML (`InvoiceStorage`, `InvoiceDetail`, `InvoicePrint`, `InvoiceSignatureVerifier`) dùng `DtdProcessing.Prohibit` và `XmlResolver = null`. Trình xem hiển thị cây và văn bản thô, không có `WebBrowser`/HTML. Kiểm chữ ký chỉ chấp nhận tham chiếu cục bộ (`#id`) và phép biến đổi enveloped/C14N, nên không có đường để XML buộc ứng dụng tải tài nguyên ngoài. Bộ ghi Excel dùng chuỗi nội tuyến (không thành công thức), `manifest.csv` có chống công thức. |
| NFR-SEC-004 sao lưu/khôi phục kiểm MST và toàn vẹn | **Đạt** | Kiểm manifest, MST, SHA-256 từng file và đường dẫn trong gói (chặn tuyệt đối, `..`, đoạn rỗng, ký tự lạ) **trước khi ghi**; có test cho `../ngoai.xml`, `a/../../ngoai.xml`, gói MST khác, gói hỏng. |
| NFR-SEC-005 theo quyền file của Windows | **Đạt** | Không có `icacls`/đổi ACL; thư mục và file tạo bằng quyền mặc định. |
| NFR-SEC-006 xác minh MST khi ghi | **Đạt** | `StorageGuard` + kiểm MST ở khôi phục; đổi thư mục kho chỉ chép kho của chính doanh nghiệp đang mở và từ chối thư mục trùng/lồng kho khác. |
| NFR-SEC-007 `license.dat` mã hóa gắn máy + kiểm chữ ký | **Đạt** | `FileLicenseStore` dùng DPAPI `LocalMachine` với entropy riêng; chữ ký máy chủ vẫn được kiểm sau khi giải mã. Lưu ý thiết kế: phạm vi `LocalMachine` nghĩa là mọi tài khoản Windows trên cùng máy đọc được file này; đúng yêu cầu "gắn với máy", nhưng không gắn với người dùng. |
| NFR-SEC-008 server licence không nhận MST/dữ liệu hóa đơn | **Đạt** | `LicenseApiClient` chỉ gửi đúng ba trường được phép (BR-025). |

## Phát hiện và xử lý

### F-1 (Trung bình, **đã sửa**): `baseUrl` của `portal-api.json` không bị ghim vào cổng Thuế

- **Mô tả:** cấu hình được đọc từ `%LOCALAPPDATA%\HoaDonDienTu\portal-api.json` (ghi được không cần quyền quản trị, và được ưu tiên hơn file cạnh ứng dụng). Bộ kiểm chỉ yêu cầu `https`. Địa chỉ gốc này còn quyết định máy chủ mà trình duyệt đăng nhập được phép điều hướng tới (`PortalLoginTraffic`) và nơi gửi Bearer token, nên một file bị thay (phần mềm độc hại, hoặc người dùng bị dẫn dụ "chép file này vào") có thể đưa trang đăng nhập giả và token sang máy chủ lạ.
- **Sửa:** `PortalHostPolicy` chỉ chấp nhận `https`, cổng mặc định, không kèm tài khoản, host là `gdt.gov.vn` hoặc subdomain; `CompositionRoot.LoadPortalApiConfig` từ chối cấu hình khác và ứng dụng dùng cấu hình mặc định kèm log cảnh báo. Có 22 test (host giả như `gdt.gov.vn.evil.com`, `evilgdt.gov.vn`, `…@evil.com`, cổng khác, `http`).
- **Hệ quả:** không thể trỏ ứng dụng sang máy chủ khác qua cấu hình, kể cả khi dev muốn thử; phát triển vẫn dùng cổng giả qua handler thay vì đổi `baseUrl`.

## Điểm cần theo dõi (chưa sửa)

1. **`license-api.json` `baseUrl`:** chưa ghim máy chủ vì chưa biết tên miền licence thật (chờ G-06). Khi có, nên áp chính sách tương tự. Mức độ thấp hơn F-1 vì chỉ lộ khóa licence và ba trường BR-025, không lộ token cổng Thuế.
2. **Đường dẫn chứa MST trong log:** xem NFR-SEC-002 ở trên.
3. **`LocalMachine` của DPAPI:** xem NFR-SEC-007 ở trên.
4. **Chưa kiểm:** hành vi thật của Chromium (cookie có thật sự không xuống đĩa khi chạy Release, thư mục tạm của CEF), chưa kiểm trên cổng thật; thư viện bên thứ ba (CefSharp, SQLite, PDFsharp, QRCoder) chưa quét lỗ hổng đã biết; chưa fuzz XML/gói sao lưu. Nên làm khi có bản Release chạy với người dùng thật (P6-01).

## Cách kiểm lại

- `scripts/Check-Release.ps1` ([runbook](../runbooks/release-check.md)) cho bản Release không chứa mã giả hay mẫu bí mật.
- Test: `PortalHostPolicyTests`, `LogSanitizer*`, `BackupServiceTests` (đường dẫn trong gói), `InvoiceExportServiceTests` (manifest chống công thức), test chữ ký XML.
