# P6-03 — Rà soát bảo mật (đợt 1 và 2, 06/11/2026)

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

## Đợt 2 (06/11/2026): thư viện bên thứ ba, XML độc hại, gói sao lưu hỏng

### Thư viện bên thứ ba

`dotnet list package --vulnerable --include-transitive` (nguồn nuget.org, chỉ tra cứu): `Domain`, `Application`, `Infrastructure` và các dự án test không có gói nào bị báo; chỉ **CefSharp 109.1.110** bị báo (SQLite, PDFsharp, QRCoder, Krypton, Newtonsoft không có cảnh báo).

| Advisory | Mức | Nội dung | Vá ở | Liên quan tới ứng dụng |
|---|---|---|---|---|
| GHSA-j646-gj5p-p45g | Nghiêm trọng | Tràn bộ nhớ heap trong WebP | 116.0.230 | Cần vẽ một ảnh WebP độc hại; trình duyệt chỉ mở trang đăng nhập của cổng Thuế |
| GHSA-4c29-gfrp-g6x9 | Cao | Tràn heap trong mã hóa VP8 của libvpx | 117.2.20 | Mã hóa video/WebRTC: ứng dụng không dùng |
| GHSA-f87w-3j5w-v58p | Cao | Handle sai trong Mojo trên Windows | 134.3.90 | Cần tiến trình kết xuất đã bị chiếm trước |
| GHSA-85jm-cwp2-mvpv | Trung bình | `FolderSchemeHandlerFactory` không chặn đúng ranh giới thư mục | 148.0.90 | Ứng dụng không đăng ký scheme handler nào (đã tìm trong mã) |

**F-2 (cao, rủi ro được chấp nhận có điều kiện, chưa xử lý được):** Chromium 109 đã hết vòng đời, nên sẽ còn thêm lỗ hổng chưa vá. [ADR 0007](../decisions/0007-webview-cefsharp.md) chọn 109 vì là bản cuối chạy trên Windows 7/8/8.1 và net452. Các biện pháp giảm thiểu đang có: trình duyệt chỉ dùng để đăng nhập, điều hướng bị chặn ngoài tên miền cổng Thuế (`PortalLoginTraffic`, nay còn bị ghim `*.gdt.gov.vn`), không mở cửa sổ mới, không menu chuột phải, không bộ nhớ đệm hay cookie xuống đĩa (theo cấu hình; chưa kiểm hành vi thật của Chromium). Phần chưa chặn: ảnh và tài nguyên con tải từ máy chủ khác trong trang đăng nhập (chưa chặn vì không thể kiểm trang thật khi chưa có UAT, chặn mù có thể làm hỏng đăng nhập). **Đề xuất:** khi bỏ hỗ trợ Windows 7/8 thì nâng CefSharp (từ 134.3.90 trở lên đóng cả bốn advisory, cần net462 và Windows 10); trước đó theo dõi bản vá và cân nhắc chặn tài nguyên con ngoài tên miền cổng Thuế sau khi có UAT.

### XML độc hại và gói sao lưu hỏng

`InputHardeningTests` (16 test): đưa vào mọi bộ đọc XML của hóa đơn (`XmlTreeBuilder`, `InvoiceXmlValidator`, `InvoicePrintModelParser`, `InvoiceSignatureVerifier`) các tài liệu độc hại có chủ đích (billion laughs, thực thể ngoài `file:///`, DTD ngoài qua http, XInclude, lồng sâu 100.000 cấp, văn bản 5 MB, 20.000 thuộc tính, ký tự null, thẻ lệch, rỗng) và 300 biến dạng ngẫu nhiên (hạt giống cố định) của một hóa đơn hợp lệ; cùng 200 biến dạng của một gói sao lưu thật. Yêu cầu: từ chối bằng đúng loại lỗi đã định, không treo (giới hạn 10 s), không đọc tài nguyên ngoài, gói hỏng không bao giờ làm thay đổi kho.

Kết quả: tất cả qua sau khi sửa ba điểm:

1. **F-3 (trung bình, đã sửa) — `InspectAsync` ném `ArgumentException` với gói có tên mục chứa ký tự không hợp lệ.** .NET Framework đọc danh mục zip chậm nên lỗi lọt qua các `catch` hiện có, màn Khôi phục báo lỗi chung thay vì "gói không dùng được". Nay trả về kết quả "gói hỏng" kèm lý do; bắt thêm `NotSupportedException`, `UnauthorizedAccessException`, `IOException`. Bài ngẫu nhiên có hạt giống cố định tự tái hiện đúng lỗi này (gói số 6).
2. **F-4 (trung bình, đã sửa) — `InspectAsync` chạy đồng bộ.** Việc mở gói và băm SHA-256 mọi file chạy trên luồng gọi (luồng giao diện của màn Khôi phục): gói vài GB sẽ làm treo cửa sổ. Nay chạy bằng `Task.Run`.
3. **F-5 (thấp, phòng ngừa) — XML lồng quá sâu.** Chuẩn hóa C14N của chữ ký số và một số bộ duyệt cây đệ quy theo độ sâu; tràn ngăn xếp không bắt được và làm chết cả ứng dụng. `XmlTreeBuilder` đã có giới hạn 64 cấp; `InvoiceSignatureVerifier` và `InvoicePrintModelParser` chưa có. Thêm `XmlSafety.EnsureShallow` (giới hạn 128 cấp, đọc theo luồng) chạy trước: kiểm chữ ký trả về "không kiểm được", in/PDF từ chối với lỗi rõ ràng. Tôi chưa chứng minh được một lần chết thật khi chưa có chốt chặn (không chạy thử để tránh làm chết tiến trình test); đây là biện pháp phòng ngừa theo hành vi đã biết của C14N.

## Điểm cần theo dõi (chưa sửa)

1. **`license-api.json` `baseUrl`:** chưa ghim máy chủ vì chưa biết tên miền licence thật (chờ G-06). Khi có, nên áp chính sách tương tự. Mức độ thấp hơn F-1 vì chỉ lộ khóa licence và ba trường BR-025, không lộ token cổng Thuế.
2. **Đường dẫn chứa MST trong log:** xem NFR-SEC-002 ở trên.
3. **`LocalMachine` của DPAPI:** xem NFR-SEC-007 ở trên.
4. **CefSharp 109 hết vòng đời:** xem F-2 ở Đợt 2.
5. **Chưa kiểm:** hành vi thật của Chromium (cookie có thật sự không xuống đĩa khi chạy Release, thư mục tạm của CEF) và hành vi trên cổng thật; chưa quét lỗ hổng gói NuGet không có trong cơ sở dữ liệu advisory của nuget.org; chưa fuzz XML dạng tệp rất lớn (hàng trăm MB) hay tệp zip bom (nén tỷ lệ cực lớn). Nên làm khi có bản Release chạy với người dùng thật (P6-01).

## Cách kiểm lại

- `scripts/Check-Release.ps1` ([runbook](../runbooks/release-check.md)) cho bản Release không chứa mã giả hay mẫu bí mật.
- Test: `PortalHostPolicyTests`, `LogSanitizer*`, `BackupServiceTests` (đường dẫn trong gói), `InvoiceExportServiceTests` (manifest chống công thức), test chữ ký XML, `InputHardeningTests` (XML độc hại, biến dạng ngẫu nhiên, gói hỏng).
- Gói NuGet: `dotnet list src/HoaDonDienTu.sln package --vulnerable --include-transitive` (cần mạng); chạy trước mỗi lần phát hành.
