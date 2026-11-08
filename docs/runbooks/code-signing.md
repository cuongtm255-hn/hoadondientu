# Runbook — Ký số bản phát hành (Authenticode)

Mục đích: file `.exe`/`.dll` và trình cài đặt có chữ ký của nhà phát hành, để Windows SmartScreen và antivirus không coi là "Unknown publisher", và để người dùng biết file chưa bị sửa sau khi phát hành.

## Cần có

- **Chứng chỉ ký mã (Code Signing)** mua từ một CA. Loại OV (cá nhân/tổ chức) ký được; loại EV có uy tín SmartScreen ngay nhưng đắt hơn. Từ 2023 CA chỉ cấp chứng chỉ trên token USB/HSM (khóa riêng không xuất ra được), nên thường dùng `-Thumbprint` thay vì file `.pfx`.
- Windows SDK có `signtool.exe` (thành phần *Signing Tools for Desktop Apps*). Script tự tìm trong `C:\Program Files (x86)\Windows Kits\10\bin`.
- **Không bao giờ** đưa `.pfx`, mật khẩu, PIN token vào repo hay log. `*.pfx`/`*.p12` nên nằm ngoài repo (hoặc trong `.local-secrets/`).

## Ký

Ký riêng file (ví dụ trình cài đặt tự làm):

```powershell
.\scripts\Sign-Files.ps1 -Path D:\build\HoaDonDienTu-Setup.exe -Thumbprint <thumbprint>
```

Chứng chỉ ở dạng file (mật khẩu lấy từ biến `HDDT_SIGN_PFX_PASSWORD` hoặc script hỏi trên cửa sổ, không hiện ký tự):

```powershell
.\scripts\Sign-Files.ps1 -Path D:\build\HoaDonDienTu-Setup.exe -PfxPath D:\keys\cdsoft.pfx
```

Ký khi đóng gói (ký các assembly `HoaDonDienTu*.exe/dll` trước khi tính SHA-256 và nén):

```powershell
.\scripts\Package-Release.ps1 -Sign -Thumbprint <thumbprint>
```

Script ký SHA-256 và đóng dấu thời gian (`http://timestamp.digicert.com`, đổi bằng `-TimestampUrl`) để chữ ký còn hiệu lực sau khi chứng chỉ hết hạn, rồi chạy `signtool verify /pa` trên từng file.

## Thứ tự khi phát hành

1. `Package-Release.ps1 -Sign ...` → ra gói zip có assembly đã ký.
2. Dựng trình cài đặt từ thư mục đã ký (trong gói), rồi **ký chính trình cài đặt** bằng `Sign-Files.ps1`.
3. Ký xong mới tính SHA-256 để công bố.

## Phạm vi ký

Chỉ ký file của chính ứng dụng (`HoaDonDienTu.exe`, `HoaDonDienTu.*.dll`, trình cài đặt `*Setup*.exe`/`*.msi`). Không ký lại file của bên thứ ba (Chromium/CefSharp, SQLite, Krypton...): ký lại làm đổi danh tính nhà phát hành của chúng.

## Thử luồng khi chưa có chứng chỉ

Đã thử bằng chứng chỉ tự ký tạm (`New-SelfSignedCertificate -Type CodeSigningCert`, xóa ngay sau đó) với `-NoTimestamp -SkipVerifySignature`: 4 assembly của app được ký, `Get-AuthenticodeSignature` thấy đúng người ký, trạng thái `UnknownError` vì gốc không được tin cậy. **Chữ ký tự ký không dùng để phát hành.** Chưa thử với chứng chỉ CA thật, nên `signtool verify /pa` và dấu thời gian chưa được kiểm trong thực tế.

## Sự cố thường gặp

| Hiện tượng | Xử lý |
|---|---|
| `Không tìm thấy signtool.exe` | Cài Windows SDK hoặc truyền `-SignToolPath` |
| `SignTool Error: No certificates were found` | Sai thumbprint hoặc chứng chỉ ở kho khác; token USB phải cắm và đã nhập PIN |
| Lỗi dấu thời gian | Thử máy chủ khác qua `-TimestampUrl` (ví dụ `http://timestamp.sectigo.com`) |
| Windows 7 không tin chữ ký SHA-256 | Cần Windows 7 SP1 có bản cập nhật hỗ trợ SHA-2 (KB4474419) |
