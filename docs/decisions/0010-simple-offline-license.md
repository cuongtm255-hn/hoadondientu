# ADR 0010 — Bản quyền đơn giản: dùng thử 30 ngày + mã kích hoạt offline theo máy

- Ngày: 07/11/2026
- Trạng thái: Chấp nhận (thay thế hướng kích hoạt trực tuyến của DD-03 v1.0 và gate G-06)

## Bối cảnh

DD-03 v1.0 thiết kế kích hoạt trực tuyến với máy chủ licence, ngoại tuyến 30 ngày, chế độ chỉ đọc khi hết hạn. Máy chủ licence chưa có và là rủi ro R-03. Yêu cầu của chủ sản phẩm: chỉ cần nhập mã kích hoạt, mặc định dùng thử 30 ngày, hết 30 ngày thì hiện cửa sổ kích hoạt, không kích hoạt thành công thì không vào được ứng dụng. Tham khảo cơ chế của `cdsoft-ezacc`.

## Quyết định

- **Offline hoàn toàn**: mã máy → đơn vị phát hành ký mã kích hoạt bằng khóa RSA bí mật → ứng dụng kiểm bằng khóa công khai nhúng. Không máy chủ, không Internet, không gửi dữ liệu.
- **Dùng thử 30 ngày** đầy đủ chức năng từ lần chạy đầu; hết thì chặn ở `Program.Main` bằng hộp thoại kích hoạt.
- Trạng thái dùng thử lưu DPAPI (phạm vi máy) ở file ProgramData và registry HKCU; lấy mốc sớm nhất, chống chỉnh lùi đồng hồ bằng mốc đã thấy muộn nhất.
- Mã máy chỉ dựa trên `MachineGuid` và serial ổ hệ thống để đổi tên máy/nâng cấp Windows không làm mất mã.
- Bỏ: `LicenseService`, `LicenseApiClient`, `license-api.json`, `license.dat`, chế độ ReadOnly do licence, gate G-06.

## Hệ quả

- Xóa cả file lẫn khóa registry làm dùng thử bắt đầu lại (giới hạn của cơ chế offline, chấp nhận).
- Mất khóa bí mật là không cấp được mã mới; đổi khóa làm mọi mã cũ vô hiệu. Phải sao lưu `.local-secrets\HDDT-License.private.xml` ngoài kho mã.
- Cài lại Windows đổi `MachineGuid` nên cần cấp mã mới cho mã máy mới.
- `AppMode.ReadOnlyService` và `LicenseStatus.GracePeriod/Revoked` còn trong mã nhưng không phát sinh ở bản chạy thật.
