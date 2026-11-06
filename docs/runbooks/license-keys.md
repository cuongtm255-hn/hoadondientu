# Runbook — Khóa bản quyền và cấp mã kích hoạt

Cơ chế: [ADR 0010](../decisions/0010-simple-offline-license.md), [DD-03](../detail-design/DD-03-LICENSING.md). Chỉ người phát hành làm các bước này; ứng dụng chỉ chứa khóa công khai.

## 1. Sinh cặp khóa (một lần cho sản phẩm)

```powershell
.\scripts\New-LicenseKeyPair.ps1
```

- Ghi `.local-secrets\HDDT-License.private.xml` (bí mật) và `.local-secrets\HDDT-License.public.xml`. Thư mục `.local-secrets/` đã nằm trong `.gitignore`; **không bao giờ commit** khóa bí mật.
- **Sao lưu khóa bí mật** ở nơi an toàn ngoài máy này (trình quản lý mật khẩu, két). Mất khóa thì không cấp được mã mới.
- Dán nội dung `HDDT-License.public.xml` vào `LicenseConstants.PublicKeyXml` (`src/HoaDonDienTu.Infrastructure/Licensing/MachineLicenseInfrastructure.cs`) rồi build lại.
- Script từ chối ghi đè khóa đang có nếu không có `-Force`. Đổi khóa làm **mọi mã đã cấp hết hiệu lực** và phải phát hành bản mới.

## 2. Cấp mã cho một khách

1. Khách mở ứng dụng → hộp thoại kích hoạt (hoặc Cài đặt → Bản quyền ứng dụng), bấm **Sao chép** ở mã máy và gửi cho bạn (dạng `XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX`).
2. Chạy:

```powershell
.\scripts\New-ActivationKey.ps1 -MachineCode 0123ABCD-4567EF01-89ABCDEF-02468ACE
```

3. Gửi nguyên **một dòng** `HDDT1|...|...` cho khách; khách dán vào ô Mã kích hoạt và bấm Kích hoạt.

Mã chỉ đúng với đúng máy đó, không hết hạn, không cần Internet.

## 3. Sự cố thường gặp

| Hiện tượng | Nguyên nhân | Xử lý |
|---|---|---|
| "Mã kích hoạt không đúng với máy này" | Khách gửi nhầm mã máy, hoặc máy khác | Lấy lại mã máy hiện tại và cấp lại |
| Đã kích hoạt nhưng lần mở sau lại hỏi mã | Cài lại Windows hoặc đổi ổ hệ thống nên mã máy đổi | Cấp mã mới cho mã máy mới |
| "Mã kích hoạt không hợp lệ hoặc đã bị thay đổi" | Dán thiếu/thừa ký tự, hoặc ký bằng khóa khác bản đang chạy | Dán lại đủ một dòng; kiểm khóa công khai trong bản đang dùng có khớp khóa bí mật |
| Khách muốn gia hạn dùng thử | Không hỗ trợ ở bản offline | Cấp mã kích hoạt |

## 4. Thử nhanh trên máy phát triển (Debug)

Debug lưu trạng thái ở `dev-license.bin` trong thư mục dữ liệu dev. Giả lập hết dùng thử bằng biến môi trường `HDDT_DEV_NOW` (UTC ISO) lớn hơn mốc chạy đầu 30 ngày, ví dụ `$env:HDDT_DEV_NOW = '2027-01-01T00:00:00Z'`, rồi chạy bản Debug để thấy hộp thoại. Không dùng mã kích hoạt thật hay cổng Thuế thật khi thử.
