# Runbook: build, phát hành và xử lý sự cố (P6-06)

Tài liệu cho người build/phát hành và người hỗ trợ khách hàng. Quy tắc nền: **không gọi cổng Thuế thật khi build, test hay demo** ([ADR 0005](../decisions/0005-mock-only-testing-policy.md)); cổng thật chỉ chạy ở bản Release do người dùng thật đăng nhập.

## 1. Yêu cầu máy build

- Visual Studio 2022 (MSBuild) có **.NET Framework 4.5.2 targeting pack**. `msbuild` không nằm trên PATH; dùng bản của VS (đường dẫn dưới đây theo Git Bash).
- Có mạng lần đầu để khôi phục NuGet (CefSharp 109.1.110, Krypton 5.500.2008, System.Data.SQLite.Core 1.0.118, PDFsharp-gdi, QRCoder). Chỉ có CLI `dotnet` thì **không** build được (thiếu reference assembly net452).
- Dự án nhắm `net452`, exe build **x86** (CefSharp không chạy AnyCPU).

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
VT="/c/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TestWindow/vstest.console.exe"
```

## 2. Build và test

```bash
"$MSB" src/HoaDonDienTu.sln -restore -nologo -v:m          # Debug, gồm cả test
"$VT" tests/HoaDonDienTu.Domain.Tests/bin/Debug/net452/HoaDonDienTu.Domain.Tests.dll
"$VT" tests/HoaDonDienTu.Application.Tests/bin/Debug/net452/HoaDonDienTu.Application.Tests.dll tests/HoaDonDienTu.Infrastructure.Tests/bin/Debug/net452/HoaDonDienTu.Infrastructure.Tests.dll
"$VT" tests/HoaDonDienTu.WinForms.Tests/bin/Debug/net452/HoaDonDienTu.WinForms.Tests.dll   # lâu (~8 phút do test tải dài)
```

- Lọc test: thêm `/TestCaseFilter:"FullyQualifiedName~TênLớp"`.
- Test dùng dữ liệu giả (cổng giả `MockPortalHandler`, thư mục tạm); bản Debug luôn có nhãn `[TEST] DỮ LIỆU GIẢ`. Test chỉ chạy ở Debug.
- Bản Debug có biến môi trường riêng để dev: `HDDT_DEV_MODE` (Standard, StandardFirstRun, Service, ReadOnly, Recovery) và `HDDT_DATA_DIR`. **Không có tác dụng ở Release.**

## 3. Quy trình phát hành

1. Commit sạch trên nhánh cần phát hành; toàn bộ test ở mục 2 phải qua.
2. **Xóa `src\HoaDonDienTu.WinForms\bin\Release` trước khi build.** Thư mục này không tự dọn: lần kiểm đầu tiên đã tìm thấy `CefSpike.exe` sót từ một lần chạy spike cũ trong đúng thư mục sẽ đóng gói.
3. Build Release:
   ```bash
   "$MSB" src/HoaDonDienTu.WinForms/HoaDonDienTu.WinForms.csproj -restore -p:Configuration=Release -nologo -v:m
   ```
   Release sao chép Chromium (CefSharp) và rút gọn `locales`; Debug thì không (`CefSharpBuildAction=NoAction`).
4. **Kiểm Release** (bắt buộc, [runbook](release-check.md)):
   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Check-Release.ps1
   ```
   Phải in "ĐẠT" và thoát mã 0 (không có mã giả, nhãn dữ liệu giả, mẫu bí mật, file cấm như `*Spike*`).
5. **Đóng gói** bằng một lệnh (thực hiện luôn các bước 2–4: dọn, build, kiểm Release, đối chiếu file bắt buộc):
   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package-Release.ps1
   ```
   Script tạo `dist\HoaDonDienTu-<phiên bản>-win-x86.zip` và file `.sha256`, trong gói có `release-info.json` (phiên bản ứng dụng, schema cơ sở dữ liệu, CefSharp, SQLite, thời điểm build, commit) và `SHA256SUMS.txt`; rồi giải nén thử, chạy lại Check-Release và đối chiếu SHA-256 từng file. Script cảnh báo nếu thư mục làm việc git còn thay đổi chưa commit: **không phát hành bản đó**.
6. Phiên bản ứng dụng đặt ở `src/HoaDonDienTu.WinForms/Properties/AssemblyInfo.cs` (hiện ở chân cửa sổ chính, Cài đặt → Thông tin ứng dụng và `release-info.json`); đổi trước khi đóng gói bản mới và viết ghi chú phát hành ở `docs/release/`.
7. Trước khi phát hành chạy thêm `dotnet list src/HoaDonDienTu.sln package --vulnerable --include-transitive` (cần mạng) và ghi kết quả vào ghi chú phát hành.

## 4. Nội dung bản Release

`HoaDonDienTu.exe` + `HoaDonDienTu.Application.dll`, `.Domain.dll`, `.Infrastructure.dll`; thư viện giao diện/PDF/QR (`Krypton.*`, `PdfSharp*`, `QRCoder`, `Newtonsoft.Json`); SQLite (`System.Data.SQLite.dll` và **cả hai** thư mục `x86\` và `x64\` chứa `SQLite.Interop.dll`); Chromium (`libcef.dll`, `CefSharp.*`, `*.pak`, `icudtl.dat`, `snapshot_blob.bin`, `v8_context_snapshot.bin`, `locales\`, `vk_swiftshader*`, `vulkan-1.dll`, `d3dcompiler_47.dll`, `libEGL.dll`, `libGLESv2.dll`). Không được có: file `.db`, `companies.json`, `license.dat`, `*.raw.*`, file spike/mock.

## 5. Dữ liệu và cấu hình trên máy người dùng

| Thứ | Vị trí |
|---|---|
| Nhật ký | `%LOCALAPPDATA%\HoaDonDienTu\logs\app-yyyyMMdd.log` (mỗi ngày một file, đã che token/cookie/mật khẩu/XML) |
| Danh mục doanh nghiệp | `%LOCALAPPDATA%\HoaDonDienTu\data\companies.json` (+ `.bak`) |
| Licence | `%ProgramData%\HoaDonDienTu\Runtime\hddt-license.bin` và `HKCU\Software\HoaDonDienTu\Runtime` (DPAPI, gắn máy; mốc dùng thử + mã kích hoạt) |
| Cấu hình API cổng Thuế | `portal-api.json`: bản trong `%LOCALAPPDATA%\HoaDonDienTu\` được ưu tiên, rồi bản cạnh `HoaDonDienTu.exe`. Thiếu thì dùng mặc định. `baseUrl` bắt buộc thuộc `gdt.gov.vn` (https), sai thì bị bỏ và dùng mặc định |
| Cấp mã kích hoạt | [license-keys.md](license-keys.md): khách gửi mã máy, người phát hành chạy `scripts\New-ActivationKey.ps1` |
| Kho của một doanh nghiệp | thư mục người dùng chọn (gợi ý `…\data\Companies\<MST>`): `company.identity.json`, `Data\company.db`, thư mục theo năm/tháng chứa XML, `Backups\`, `Exports\` |
| Bản sao trước nâng cấp schema | `Data\company.db.v<N>.bak` cạnh cơ sở dữ liệu (có thể xóa khi đã dùng ổn định) |

Token, cookie đăng nhập **không bao giờ** ghi xuống đĩa (xem [rà soát bảo mật](../security/P6-03-security-review.md)).

## 6. Xử lý sự cố

Luôn bắt đầu bằng file log của ngày xảy ra lỗi. Mỗi lỗi hiển thị cho người dùng có mã tham chiếu dạng `[correlationId]` cũng nằm trong log.

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| Ứng dụng không mở được | Lỗi khởi động (log có `Khởi động thất bại [mã]`) | Gửi file log; thử mở lại; nếu lỗi do kho thì xem hai dòng sau |
| Doanh nghiệp báo "thư mục kho không còn tồn tại" (`PathMissing`) | Thư mục đã bị di chuyển/xóa/ổ đĩa rời | Màn Doanh nghiệp → liên kết lại thư mục đúng. Không tạo kho mới trên cùng MST khi dữ liệu cũ còn |
| Báo "kho thuộc MST khác" hoặc "không phải kho hợp lệ" | Chọn nhầm thư mục, hoặc file `company.identity.json` hỏng/xóa | Chọn đúng thư mục; không sửa tay file định danh; nếu mất, khôi phục từ gói sao lưu |
| Cơ sở dữ liệu hỏng / không mở được | Tắt máy đột ngột, đĩa lỗi, schema mới hơn ứng dụng | Schema mới hơn: cập nhật ứng dụng. Hỏng: Cài đặt → Khôi phục từ gói sao lưu gần nhất (ứng dụng tự lưu điểm an toàn trước khi ghi); hoặc dùng bản `.bak` trước nâng cấp |
| Trình duyệt đăng nhập không hiện / không khởi tạo được Chromium | Thiếu file Chromium trong thư mục cài, hoặc chạy bản Debug | Kiểm tra gói đủ file ở mục 4; chạy bản Release x86; log ghi `Không khởi tạo được trình duyệt đăng nhập` |
| Đăng nhập xong nhưng "Tải hóa đơn" vẫn khóa | Phiên cổng Thuế chưa xác minh được MST, hoặc phiên hết hạn | Đăng nhập lại; MST đăng nhập phải khớp MST doanh nghiệp đang mở |
| Phiên tải chuyển sang "chờ đăng nhập lại" (`AUTH_EXPIRED`) | Phiên cổng Thuế hết hạn hoặc không còn khớp doanh nghiệp | Đăng nhập lại đúng MST rồi tiếp tục phiên tải |
| `portal-api.json không hợp lệ, dùng cấu hình mặc định` trong log | File cấu hình sai cấu trúc/schema cũ hoặc `baseUrl` ngoài `gdt.gov.vn` | Xóa file tùy chỉnh ở `%LOCALAPPDATA%\HoaDonDienTu\` để dùng mặc định, hoặc sửa theo mẫu |
| Hộp thoại "Đã hết 30 ngày dùng thử" / "Mã kích hoạt không còn hợp lệ" | Hết dùng thử, hoặc mã máy đổi (cài lại Windows, đổi ổ hệ thống) | Khách gửi mã máy hiện tại, cấp mã mới theo [license-keys.md](license-keys.md) |
| Kho hóa đơn hiện "Thiếu XML" / "XML lỗi" | File bị xóa, di chuyển hoặc sửa ngoài ứng dụng | Kho hóa đơn → Kiểm tra file → báo cáo toàn vẹn; "Xóa file tạm" chỉ dọn `.tmp` sót; tải lại kỳ đó hoặc khôi phục từ sao lưu |
| Xuất Excel/ZIP báo lỗi ghi | File đích đang mở bằng Excel, hoặc ổ đĩa đầy | Đóng file đang mở hoặc chọn nơi khác; file cũ vẫn nguyên, không để file tạm |
| Sao lưu/khôi phục/đổi thư mục kho bị từ chối | Đang có phiên tải chạy | Tạm dừng hoặc chờ phiên tải xong |
| Đổi thư mục kho lỗi giữa chừng | Ổ đích đầy/không ghi được, hoặc bị hủy | Ứng dụng vẫn dùng thư mục cũ và đã dọn phần chép dở; thư mục cũ luôn còn nguyên. Chọn thư mục trống khác |

## 7. Thu thập thông tin khi hỗ trợ

Xin: phiên bản ứng dụng (Cài đặt → Thông tin ứng dụng), file log của ngày lỗi, mã tham chiếu `[correlationId]` trên thông báo, mô tả thao tác, ảnh chụp màn hình (che thông tin nhạy cảm). **Không xin và không nhận** mật khẩu, mã CAPTCHA, token, cookie, nội dung file XML hóa đơn hay `license.dat`.

## 8. Giới hạn của runbook

- Gói là bản giải nén chạy ngay: chưa có trình cài đặt (phím tắt, gỡ cài đặt), chưa ký số file chạy, chưa có CI. Script đóng gói chạy thủ công.
- Chưa thử gói trên máy Windows sạch (chưa cài sẵn Visual C++ Redistributable) hay máy Windows 7/8.
- Bảng sự cố ở mục 6 dựa trên hành vi đã có trong mã và test; chưa qua UAT với người dùng thật (P6-01), nên có thể thiếu tình huống thực tế. Bổ sung khi có phản hồi.
