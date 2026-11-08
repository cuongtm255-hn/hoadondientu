# DD-03 — Bản quyền: dùng thử 30 ngày và mã kích hoạt theo máy

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-03 |
| Tên | Dùng thử 30 ngày, mã kích hoạt ký số theo máy (kiểm offline), cổng vào ứng dụng, SCR-12 |
| Phiên bản | 2.0 (thay thế thiết kế kích hoạt trực tuyến v1.0, xem [ADR 0010](../decisions/0010-simple-offline-license.md)) |
| Ngày | 07/11/2026 |
| Trạng thái | Đã triển khai, có test và đã thử bản thật (người dùng báo 08/11/2026) |
| Feature | [FT-04](../requirements/FEATURE-LIST.md#ft-04--licence-và-quyền-tính-năng) |
| Màn hình | SCR-12 (Bản quyền); hộp thoại kích hoạt lúc khởi động; mục `Bản quyền` trong SCR-09 |
| Tham khảo | Cơ chế của dự án `cdsoft-ezacc` (mã máy + mã kích hoạt RSA, trạng thái dùng thử lưu DPAPI) |
| Vận hành | [Cấp mã kích hoạt](../runbooks/license-keys.md) |

## 2. Cơ chế

1. **Lần chạy đầu** ghi mốc `FirstRunUtc`. Từ đó có **30 ngày dùng thử** đầy đủ chức năng (kể cả nhiều doanh nghiệp). Hết ngày thứ 30 (theo ngày lịch UTC) thì hết dùng thử.
2. **Hết dùng thử mà chưa kích hoạt**: ngay lúc khởi động hiện hộp thoại **Kích hoạt bản quyền** (mã máy có nút Sao chép, ô dán mã kích hoạt). Kích hoạt không thành công hoặc đóng hộp thoại thì **thoát ứng dụng**, không vào được màn hình chính. Dữ liệu trong kho không bị xóa.
3. **Kích hoạt**: người dùng gửi *mã máy* cho đơn vị phát hành, nhận lại *mã kích hoạt* `EZACC1|payload|chữ ký` rồi dán vào. Mã do API `POST /platform/tenants/{id}/machines` của máy chủ cdsoft-sale-management cấp (quản trị ký bằng khóa bí mật của máy chủ). Ứng dụng kiểm **offline**: chữ ký RSA-SHA256 bằng khóa công khai nhúng sẵn, sản phẩm `CDSOFT_EZACC`, đúng mã máy, loại `PerMachine`. Ứng dụng không gọi máy chủ và không gửi dữ liệu nào đi.
4. Đã kích hoạt thì không hết hạn. Mã đã lưu được **kiểm lại ở mỗi lần khởi động**; nếu không còn khớp máy (ví dụ cài lại Windows) thì tính lại theo đồng hồ dùng thử và hộp thoại báo rõ lý do.

## 3. Mã máy

`SHA-256("product=HDDT|machineGuid=<MachineGuid>|systemDrive=<serial ổ hệ thống>")`, 16 byte đầu, dạng `XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX`. Cố ý **không** dùng tên máy, phiên bản Windows hay CPU để đổi tên máy hoặc nâng cấp không làm mất mã. Đọc `MachineGuid` bằng `RegistryView.Registry64` vì tiến trình x86.

## 4. Lưu trạng thái và chống gian lận

- `LicenseRecord`: `FirstRunUtc`, `LastSeenUtc`, mã kích hoạt, `ActivatedAtUtc`.
- Lưu mã hóa DPAPI (phạm vi máy) vào **hai nơi**: `%ProgramData%\HoaDonDienTu\Runtime\hddt-license.bin` và `HKCU\Software\HoaDonDienTu\Runtime`. Khi đọc: mốc chạy đầu **sớm nhất**, mốc đã thấy **muộn nhất**, mã kích hoạt nơi nào có. Xóa một nơi không làm mới thời gian dùng thử.
- `LastSeenUtc` (ghi tối đa mỗi giờ) chặn chỉnh lùi đồng hồ: ngày tính là `max(đồng hồ, LastSeenUtc)`. Mốc chạy đầu nằm ở tương lai bị coi là bất thường và đặt lại về hiện tại.
- **Giới hạn đã biết** của cơ chế offline: xóa cả file lẫn khóa registry thì thời gian dùng thử bắt đầu lại. Chấp nhận (cùng mức với dự án tham khảo); cần chặn tuyệt đối thì phải có máy chủ.
- Không lưu hay ghi log mã kích hoạt đầy đủ: giao diện chỉ hiện `••••` + 4 ký tự cuối.
- Khóa bí mật nằm ngoài kho mã (`.local-secrets/`, đã `.gitignore`). Ứng dụng chỉ có khóa công khai nên không tự sinh được mã.

## 5. Thành phần

| Lớp | Thành phần |
|---|---|
| Application | `MachineLicenseService` (`Evaluate`, `ActivateAsync`, `DeactivateAsync`, `CurrentInfo`, `InfoChanged`), `LicenseSnapshot`, cổng `ILicenseStateStore`, `IMachineCodeProvider`, `IActivationKeyVerifier`, `IClock` |
| Infrastructure | `LicenseConstants` (sản phẩm, tiền tố, khóa công khai), `MachineCodeProvider`, `RsaActivationKeyVerifier`, `FileRegistryLicenseStateStore` |
| WinForms | `ActivationGate` + `ActivationForm` (chặn ở `Program.Main`), SCR-12 `LicenseControl`/`LicensePresenter`, `ShellModeMapper.WithLicense` |
| Công cụ | `scripts/New-LicenseKeyPair.ps1`, `scripts/New-ActivationKey.ps1` |

`Trial` và `Valid` đều cho chế độ `Service` (đầy đủ chức năng); `Expired` bị chặn ở cổng nên chế độ chỉ đọc theo licence không còn xảy ra ở bản chạy thật. `StartupCoordinator`: chế độ Service mà chưa có doanh nghiệp nào thì vào thẳng màn tạo doanh nghiệp đầu tiên.

## 6. Debug và kiểm thử

- Debug lưu trạng thái ở `dev-license.bin` trong thư mục dữ liệu dev, không đụng ProgramData hay registry. `HDDT_DEV_NOW` (ngày giờ UTC ISO) giả lập thời điểm hiện tại để thử hết dùng thử.
- `HDDT_DEV_MODE` vẫn chọn chế độ khởi động giả lập (Standard, Service, ReadOnly...) cho các màn hình khác.
- Test dùng đồng hồ, kho và bộ kiểm mã giả; test chữ ký dùng cặp khóa RSA tạm sinh trong test, không dùng khóa thật.

## 7. Kiểm thử

| Nhóm | Nội dung |
|---|---|
| Application (`MachineLicenseServiceTests`) | Dùng thử 30 ngày và mốc hết hạn, chỉnh lùi đồng hồ, mốc tương lai, khởi động lại, kích hoạt hợp lệ/sai/máy khác/rỗng, mã bị cắt khoảng trắng, mã đã lưu hết khớp máy, hủy kích hoạt, sự kiện, kho không ghi được |
| Infrastructure (`MachineLicenseInfrastructureTests`) | Mã máy ổn định và đổi theo đầu vào, chữ ký đúng/sai khóa/sửa payload/sản phẩm khác/loại không hỗ trợ, mã sai định dạng không ném lỗi, khóa công khai hỏng, kho DPAPI + registry, gộp hai bản, file hỏng |
| WinForms | `ActivationGateTests` (vào thẳng khi còn dùng thử, chặn khi hết, kích hoạt mở lại, hộp thoại nói dối không qua được), `LicensePresenter.Build`, tóm tắt ở Cài đặt, 2 UI smoke SCR-12 |

## 8. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

| ID | Công việc | Lớp | Trạng thái | Ghi chú |
|---|---|---|---|---|
| T03-01 | Domain: `LicenseStatus` (+`Trial`), `AppMode`, `FeatureMatrix` | Domain | ✅ | |
| T03-02 | `EntitlementService` nhận `Trial`/`Valid` → Service | Application | ✅ | |
| T03-03 | `DevelopmentLicenseService` (Debug) | Infra | ✅ | giữ để thử các chế độ khởi động |
| T03-04 | Mã máy, kiểm chữ ký RSA, kho trạng thái DPAPI + registry | Infra | ✅ | thay `MachineIdentityProvider`/`FileLicenseStore`/`RsaLicenseSignatureVerifier` cũ |
| T03-05 | `MachineLicenseService` (dùng thử, kích hoạt, chống lùi đồng hồ) | Application | ✅ | thay `LicenseService` + `LicenseApiClient` (đã xóa) |
| T03-06 | SCR-12 viết lại + hộp thoại kích hoạt + cổng ở `Program.Main` | WinForms | ✅ | |
| T03-07 | Unit + integration test | Tests | ✅ | xem mục 7 |
| T03-08 | UI smoke SCR-12 | Tests | ✅ | |
| T03-09 | Công cụ sinh khóa/cấp mã + runbook | Scripts, docs | ✅ | [license-keys.md](../runbooks/license-keys.md) |
| T03-10 | Thử bản cài thật: hết dùng thử → hộp thoại → dán mã → vào | Thủ công | ✅ | người dùng báo đã thử xong ngày 08/11/2026 theo [license-uat.md](../runbooks/license-uat.md); chưa có biên bản chi tiết từng bước |

### Nhật ký tiến độ

| Ngày | Nội dung |
|---|---|
| 01/11/2026 | Thiết kế v1.0 (kích hoạt trực tuyến, ngoại tuyến 30 ngày, ReadOnly khi hết hạn) |
| 07/11/2026 | Thiết kế lại theo yêu cầu: mã kích hoạt offline, dùng thử 30 ngày, chặn vào ứng dụng khi hết (v2.0); xóa mã licence trực tuyến; ADR 0010 |
| 08/11/2026 | Khớp máy chủ cấp mã cdsoft-sale-management: tiền tố `EZACC1`, `product=CDSOFT_EZACC`, khóa công khai của máy chủ; công thức mã máy giữ nguyên (chuỗi băm vẫn dùng `HDDT`, không đổi theo sản phẩm cấp mã). Chưa kiểm với khóa thật |

## 9. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Tách và mở rộng từ DD-01 v1.0 (mục 18): kích hoạt trực tuyến, entitlement, ReadOnly |
| 2.0 | 07/11/2026 | Viết lại: dùng thử 30 ngày + mã kích hoạt offline theo máy, bỏ máy chủ licence và chế độ chỉ đọc theo licence |
