# Bàn giao 01/11/2026 — Nền tảng Phase 1 (DD-01 với development adapter)

## Đã làm

- Solution net45 (`src/HoaDonDienTu.sln`): Domain, Application, Infrastructure, WinForms (Krypton 5.500.2008) + 4 project test (206 test; 12 UI smoke cần desktop).
- DD-01 T01-01…T01-13, T01-15, T01-17: khung Ribbon + sidebar, startup theo chế độ (Standard/Service/ReadOnly/Recovery), `CompanyContext` + chuyển MST + `StorageGuard`, phiên cổng Thuế trong bộ nhớ + client theo generation, `PortalLoginForm` (trình duyệt giả), `NavigationService`, đóng app an toàn, `LogSanitizer`, `UiOperationRunner`, `AtomicJsonWriter`.
- Mọi licence/catalog/kho/xác thực/trình duyệt đăng nhập đang là adapter giả **chỉ biên dịch ở Debug**; Release báo chưa có cấu hình production.
- Chi tiết khác thiết kế: [DD-01 mục 21.4](../detail-design/DD-01-APP-SHELL-LOGIN.md).

## Cần quyết định / làm tiếp

1. ~~Standard tự mở form đăng nhập khi khởi động~~ — đã làm (quyết định 01/11/2026).
2. **T01-16 / Phase 1 spike (T00-xx):** chọn WebView (G-01), khảo sát API cổng Thuế (G-02), SQLite (G-08), thư viện Excel/PDF/ZIP (G-09) — cần Visual Studio, tài khoản cổng Thuế thật và ADR. Đây là bước chặn các DD-04…DD-09.
3. Phase 2: DD-02 (catalog + màn hình doanh nghiệp), DD-03 (licence) để thay adapter giả; viết runbook build.
4. Review tài liệu Phase 0 (P0-07, P0-08) vẫn chờ chủ sản phẩm.

## Lưu ý

- Chạy thử: `HDDT_DEV_MODE` (Standard/StandardFirstRun/Service/ReadOnly/Recovery), `HDDT_DEV_JOB=1`.
- Chưa có app manifest/DPI awareness, chưa xoay vòng/dọn log, chưa có `.gitattributes` (git cảnh báo LF/CRLF).
