# ADR 0007 — WebView đăng nhập: CefSharp 109 trên net452 (G-01)

| | |
|---|---|
| Trạng thái | **Accepted (hướng chọn)** — chưa chứng minh trên cổng thật (xem "Chưa kiểm"); người dùng chọn phương án này ngày 03/11/2026 |
| Ngày | 03/11/2026 |
| Gate | G-01 WebView ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md) mục 3) |
| Liên quan | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) (T01-16), [CLAUDE.md](../../CLAUDE.md) (mục Authentication, Constraints), [ADR 0005](0005-mock-only-testing-policy.md), [ADR 0006](0006-sqlite-provider.md) |

## Bối cảnh

Cổng Thuế có CAPTCHA nên ứng dụng không tự động đăng nhập: người dùng đăng nhập tay trong một WebView nhúng, ứng dụng lấy phiên hợp lệ rồi gọi API trực tiếp
(CLAUDE.md, "Authentication design"). Dự án đang nhắm **net45**. G-01 yêu cầu chọn WebView tương thích trước khi viết adapter đăng nhập thật; đến nay mới có
adapter giả cho Debug, nên bản Release chưa đăng nhập được (T01-16).

## Phương án và bằng chứng

Kiểm trên NuGet (03/11/2026, đọc metadata gói, không cài):

| Phương án | Kết quả |
|---|---|
| `System.Windows.Forms.WebBrowser` (IE) | Có sẵn trên net45, không thêm thư viện. Dùng engine IE: SPA hiện đại của cổng khó chạy và IE đã bị Microsoft ngừng; chưa thử trên cổng thật |
| `CefSharp.WinForms` 67 → 114 | Chỉ có nhóm phụ thuộc **.NET Framework 4.5.2** (từ 95.7.141 thêm 4.6.2); **không có net45** |
| `CefSharp.WinForms` 115 trở lên | Chỉ net462 trở lên, và Chromium 110+ không còn hỗ trợ Windows 7/8 |
| `Microsoft.Web.WebView2` 1.0.x | Thư viện chỉ có `lib\net462`; cần WebView2 Runtime (Windows 10/11 trở lên) |
| **`CefSharp.WinForms` 109.1.110** | Phụ thuộc `CefSharp.Common` 109.1.110 → `cef.redist.x86`/`x64` 109.1.11; nhóm net452. Chromium 109 là bản cuối còn chạy trên Windows 7/8/8.1. Gói runtime nặng khoảng 97 MB (x86) và 104 MB (x64) |

## Quyết định

1. Dùng **CefSharp.WinForms 109.1.110** (ghim đúng phiên bản; nâng cấp phải thử lại đăng nhập trên cổng thật) làm WebView đăng nhập.
2. **Nâng target framework của toàn bộ solution từ net45 lên net452** (đã làm ngày 03/11/2026: build sạch, 1178 test qua). net452 là bản nâng tại chỗ của net45 (cùng CLR 4), các thư viện net45 đang dùng
   (Krypton 5.500.2008, System.Data.SQLite.Core 1.0.118, Newtonsoft.Json) vẫn tham chiếu được; Windows 7 SP1 trở lên vẫn chạy được.
3. Mọi API CefSharp nằm sau một cổng trong Infrastructure/WinForms (`IPortalLoginBrowser` đã có trong thiết kế); Application và Domain không biết CefSharp.
4. Chỉ phát hành x86 và x64 riêng (CefSharp không dùng AnyCPU tự nhiên); chọn nền tảng theo máy khi đóng gói.
5. Mỗi MST dùng một `RequestContext`/thư mục cache riêng và xóa khi chuyển MST hoặc đăng xuất (không để cookie của MST này lọt sang MST khác); điều hướng ra ngoài
   `hoadondientu.gdt.gov.vn` bị chặn bằng handler điều hướng/cửa sổ mới.
6. Phiên lấy được từ WebView chỉ giữ trong bộ nhớ (như `IPortalSessionStore` hiện có), không ghi đĩa hay nhật ký.

## Hệ quả

- (+) Chromium hiện đại chạy được SPA của cổng; hỗ trợ Windows 7/8/10/11; kiểm soát được cookie, điều hướng và request ở mức mã nguồn.
- (+) Việc nâng net45 → net452 gần như chỉ đổi dòng `TargetFramework`/`TargetFrameworkVersion` và gói tham chiếu (đã có test để bắt hồi quy).
- (−) Bộ cài tăng khoảng 100–150 MB, kèm nhiều file native; cần cập nhật quy trình đóng gói (P6-05) và kiểm antivirus.
- (−) Chromium 109 không còn nhận bản vá bảo mật; chỉ nên mở đúng domain cổng Thuế (quyết định 5) và không dùng làm trình duyệt chung.
- (−) Test host hiện chạy x86 và CefSharp cần x86/x64 rõ ràng: test UI cho phần đăng nhập thật phải chạy trên cổng giả hoặc trình duyệt giả, không khởi tạo Chromium.
- (−) Nếu sau này cần Chromium mới, phải bỏ hỗ trợ Windows 7/8 và nâng lên net462+ (khi đó cân nhắc WebView2).

## Chưa kiểm

Các mục 3-7 của G-01 phải được chứng minh bằng một lần người dùng đăng nhập tay trên cổng thật (ngoại lệ duy nhất của [ADR 0005](0005-mock-only-testing-policy.md)):
render cổng, nhập CAPTCHA, phát hiện đăng nhập và lấy phiên, xóa phiên giữa hai MST, chặn điều hướng ngoài domain. Cho tới lúc đó T00-01 và T00-02 vẫn mở.
Chưa thử cài `cef.redist` trên máy sạch và chưa đo dung lượng bộ cài thực tế.
