# Kế hoạch và theo dõi tiến độ dự án

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | PLAN-HDDT-001 |
| Phiên bản | 1.0 |
| Cập nhật lần cuối | 02/11/2026 |
| Phase hiện tại | **Phase 3–5 đang triển khai trên dữ liệu giả** (Phase 0 chờ review để đóng; Phase 1 còn chờ ADR G-01, G-02 đầy đủ, G-03, G-05, G-06, G-07 và PDF của G-09) |

Đây là file theo dõi tiến độ **chung**. Chi tiết từng công việc nằm ở mục **Theo dõi tiến độ implement** trong từng detail design (`T<DD>-<nn>`). Khi đổi trạng thái một task, cập nhật DD tương ứng trước, sau đó đồng bộ bảng tổng hợp ở mục 3 và 4 tại đây.

Ký hiệu: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

## 1. Tài liệu tham chiếu

| Loại | Tài liệu |
|---|---|
| Yêu cầu | [SRS](../requirements/SRS.md) · [Feature list](../requirements/FEATURE-LIST.md) |
| Wireframe | [HTML tương tác](../wireframes/index.html) · [Danh mục PNG](../wireframes/README.md) |
| Detail design | [Chỉ mục](../detail-design/README.md): [DD-00](../detail-design/DD-00-TECHNICAL-GATES.md) · [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) · [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md) · [DD-03](../detail-design/DD-03-LICENSING.md) · [DD-04](../detail-design/DD-04-PERIOD-DOWNLOAD-CONFIG.md) · [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) · [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md) · [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md) · [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md) · [DD-09](../detail-design/DD-09-EXPORT.md) · [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md) · [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md) |
| Quyết định | [`docs/decisions/`](../decisions/) (ADR do DD-00 sinh ra) |
| Mẫu API | [`docs/api-samples/`](../api-samples/) |
| Bàn giao phiên | [`docs/progress/`](../progress/) |
| Runbook | [`docs/runbooks/`](../runbooks/) |

## 2. Tổng quan các phase

| Phase | Tên | Mục tiêu | Tính năng | Trạng thái |
|---|---|---|---|---|
| 0 | Yêu cầu và thiết kế | SRS, wireframe, feature list, detail design, kế hoạch | Tất cả (tài liệu) | 🟡 Đang review |
| 1 | Nền tảng và technical spike | Solution chạy build được; ADR chặn kiến trúc | FT-00 | 🟡 Solution xong; ADR 0001–0006 đã có (G-02 một phần, G-04 và G-08 đóng, G-09 Excel/ZIP); chưa có ADR cho G-01, G-03, G-05, G-06, G-07 và PDF |
| 2 | Khung chương trình, doanh nghiệp, licence và đăng nhập | Chạy 3 chế độ bằng development adapter | FT-01, FT-02, FT-03, FT-04 | 🟡 DD-01 16/18 task xong; DD-02 6/10; DD-03 5/8 (khóa/hợp đồng licence thật chờ G-06) |
| 3 | Tải hóa đơn end-to-end | Chọn kỳ → tải → lưu XML trên cổng thật | FT-05, FT-06, FT-07, FT-02 (production) | 🟡 DD-04 xong; DD-05 7/11 và DD-06 9/9 chạy trên cổng giả với SQLite thật ([ADR 0006](../decisions/0006-sqlite-provider.md)); còn đăng nhập WebView thật (G-01), UAT cổng thật |
| 4 | Kho hóa đơn, chi tiết và trình xem | Tra cứu, xem XML/PDF | FT-08, FT-09 | ✅ DD-07 9/9; DD-08 8/8 (PDF dựng từ XML, ADR 0009) |
| 5 | Xuất dữ liệu, tổng quan, lịch sử, cài đặt và sao lưu | Hoàn thiện tính năng còn lại | FT-10, FT-11, FT-12 | ✅ DD-09 7/7; DD-10 7/7; DD-11 7/8 (sao lưu/khôi phục có nút hủy, đổi thư mục kho, dung lượng, kiểm tra kết nối; còn thử kết nối cổng thật ở bản Release) |
| 6 | Hardening, UAT và phát hành | Nghiệm thu AC-01…AC-17, đóng gói | Xuyên suốt | ⬜ |

Mỗi phase kết thúc khi đạt đủ **tiêu chí thoát**; không bắt đầu code phụ thuộc vào gate chưa có ADR.

## 3. Chi tiết từng phase

### Phase 0 — Yêu cầu và thiết kế

**Tiêu chí thoát:** tài liệu được review, không còn câu hỏi nghiệp vụ mở chặn Phase 1.

| ID | Công việc | Tài liệu | Trạng thái |
|---|---|---|---|
| P0-01 | Đặc tả yêu cầu phần mềm | [SRS](../requirements/SRS.md) | ✅ |
| P0-02 | Wireframe HTML + PNG (SCR-01…12) | [wireframes](../wireframes/README.md) | ✅ |
| P0-03 | Chuẩn hóa detail design khung chương trình và login | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) | ✅ |
| P0-04 | Feature list | [FEATURE-LIST](../requirements/FEATURE-LIST.md) | ✅ |
| P0-05 | Detail design cho từng tính năng (DD-00, DD-02…DD-11) | [detail-design](../detail-design/README.md) | ✅ (bản nháp) |
| P0-06 | Kế hoạch và theo dõi tiến độ | tài liệu này | ✅ |
| P0-07 | Review SRS + feature list + detail design với chủ sản phẩm | — | ⬜ |
| P0-08 | Chốt các câu hỏi mở (xem mục 6) | — | ⬜ |

### Phase 1 — Nền tảng và technical spike

**Mục tiêu:** dựng được solution và loại bỏ rủi ro công nghệ trước khi viết nghiệp vụ.
**Tiêu chí thoát:** solution build bằng MSBuild; ADR G-01, G-02, G-08, G-09 (tối thiểu) đã chốt (G-08 đã chốt ở ADR 0006); mẫu API đã làm sạch được commit; runbook build đã có.

| Task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T01-01 | Tạo solution/project/test project | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | ✅ |
| T01-17 | Cập nhật lệnh build/test thật vào `CLAUDE.md`/`README.md` + runbook | DD-01 | ✅ (runbook chưa viết) |
| T00-01 | Spike WebView trên net45 | [DD-00](../detail-design/DD-00-TECHNICAL-GATES.md#6-theo-dõi-tiến-độ) | ⬜ |
| T00-02 | Cách phát hiện login và lấy token/cookie | DD-00 | ⬜ |
| T00-03 | Ghi traffic API (xác minh phiên/MST, tra cứu, phân trang, tải XML) | DD-00 | 🟡 (đăng nhập, bán ra, chi tiết, xuất XML/Excel đã có; còn mua vào, `state`, 401/429 thật) |
| T00-04 | Giới hạn tốc độ / 429 | DD-00 | ⬜ |
| T00-05 | Chiến lược PDF | DD-00 | ⬜ |
| T00-06 | Đối chiếu mẫu Excel | DD-00 | ✅ ([ADR 0002](../decisions/0002-excel-export-fidelity.md)) |
| T00-07 | Chốt giao thức licence | DD-00 | ⬜ |
| T00-09 | Chọn SQLite provider net45 | DD-00 | ✅ ([ADR 0006](../decisions/0006-sqlite-provider.md), [spike](../../spikes/SqliteSpike/README.md)) |
| T00-10 | Chọn thư viện Excel/PDF/ZIP net45 | DD-00 | 🟡 (Excel/ZIP chốt ở [ADR 0003](../decisions/0003-xlsx-and-zip-generation.md); còn PDF) |

### Phase 2 — Khung chương trình, doanh nghiệp, licence và đăng nhập

**Mục tiêu:** chạy được Standard / Service / ReadOnly bằng development adapter; đăng nhập qua `PortalLoginForm` với cơ chế xác minh MST.
**Tiêu chí thoát:** Definition of Done của [DD-01 mục 21.2](../detail-design/DD-01-APP-SHELL-LOGIN.md#212-definition-of-done); AC-12, AC-14, AC-15 (phần chuyển MST), AC-16, AC-17 pass bằng development adapter.

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T01-02…T01-15 | Domain/Application nền, composition root, shell, startup, context/switch, login form, session, logging, test | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | ✅ (T01-14 một phần, chờ DD-02/03/06) |
| T02-01…T02-10 | Catalog, first-run, SCR-10/11, relink, nâng cấp Standard → Service | [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md#9-theo-dõi-tiến-độ-implement) | 🟡 (T02-07 xong 06/11; T02-08 chờ DD-03; T02-03/09 một phần) |
| T03-01…T03-08 | Entitlement, licence dev + production, SCR-12 | [DD-03](../detail-design/DD-03-LICENSING.md#9-theo-dõi-tiến-độ-implement) | 🟡 (T03-01..03, 06, 08 xong; T03-04/05/07 một phần, chờ G-06) |
| T06-02 (phần khởi tạo) | Khung `CompanyDatabaseInitializer` tối thiểu để DD-01/DD-02 mở kho | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md#91-công-việc) | ✅ (`CompanyDatabaseInitializer` + schema v1 + migration, 14 test) |
| T00-08 | Spike an toàn chuyển MST / crash recovery | DD-00 | ⬜ |

### Phase 3 — Tải hóa đơn end-to-end

**Mục tiêu:** từ SCR-03 chọn kỳ đến lưu XML đúng, chống trùng, tiếp tục được sau gián đoạn — trên cổng thật.
**Tiêu chí thoát:** AC-02, AC-03, AC-04, AC-05, AC-06, AC-07, AC-15 (tải) pass; UAT mục 16.3 SRS (phần đăng nhập, mua/bán, nhiều trang, đăng nhập lại).

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T01-18 | Ghi lại API đăng nhập thật: `PortalToken`, `PortalAuthCapture`, xác minh MST bằng JWT | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | ✅ |
| T01-16 | Thay development adapter bằng production login adapter | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | 🟡 (Release chạy được; đăng nhập cổng Thuế chờ G-01, G-02) |
| T04-01…T04-10 | Chia kỳ, SCR-03, bộ lọc theo tham số thật của cổng | [DD-04](../detail-design/DD-04-PERIOD-DOWNLOAD-CONFIG.md#7-theo-dõi-tiến-độ-implement) | ✅ (T04-10: bộ lọc mua vào theo mẫu thật 02/11/2026) |
| T05-01…T05-12 | Orchestrator, checkpoint, SCR-04, API client | [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md#8-theo-dõi-tiến-độ-implement) | 🟡 (engine, client, SCR-04 và nối vào ứng dụng xong trên cổng giả; còn SQLite, khôi phục khi khởi động, UAT cổng thật; T05-12 mua vào bắt buộc `ttxly` xong trên cổng giả) |
| T06-01…T06-09 | Chuẩn hóa, lưu XML, phiên bản hóa, toàn vẹn | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md#9-theo-dõi-tiến-độ-implement) | ✅ 9/9 (T06-09 đã mô phỏng đĩa đầy bằng luồng giả) |

### Phase 4 — Kho hóa đơn, chi tiết và trình xem

**Tiêu chí thoát:** NFR-PERF-002 đạt trên 100.000 hóa đơn; AC-10 pass; điều hướng SCR-05 ↔ SCR-06 ↔ SCR-07 giữ bộ lọc.

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T07-01…T07-09 | Kho hóa đơn SCR-05 | [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md#6-theo-dõi-tiến-độ-implement) | ✅ 9/9 |
| T08-01…T08-08 | Chi tiết, trình xem XML, PDF, in | [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md#7-theo-dõi-tiến-độ-implement) | ✅ 8/8 (06/11/2026): PDF dựng từ XML theo [ADR 0009](../decisions/0009-pdf-from-xml-pdfsharp.md), không lưu PDF vào kho |

### Phase 5 — Xuất dữ liệu, tổng quan, lịch sử, cài đặt và sao lưu

**Tiêu chí thoát:** AC-08, AC-09, AC-11 pass; Dashboard/History phản ánh đúng phiên dở dang (AC-07).

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T09-01…T09-07 | Xuất Excel/ZIP | [DD-09](../detail-design/DD-09-EXPORT.md#7-theo-dõi-tiến-độ-implement) | 7 ✅ (T09-01..07) |
| T10-01…T10-07 | Tổng quan, lịch sử | [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md#6-theo-dõi-tiến-độ-implement) | ✅ 7/7 |
| T11-01…T11-08 | Cài đặt, sao lưu/khôi phục | [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md#7-theo-dõi-tiến-độ-implement) | 🟡 7 ✅ (T11-01, 02, 04, 05, 06, 07, 08) · 1 🟡 (T11-03 chưa thử cổng thật) |

### Phase 6 — Hardening, UAT và phát hành

**Tiêu chí thoát:** toàn bộ AC-01…AC-17 đạt; UAT mục 16.3 SRS hoàn tất; không còn lỗi mức chặn; gói cài đặt chạy trên máy sạch.

| ID | Công việc | Trạng thái |
|---|---|---|
| P6-01 | UAT trên cổng thật theo SRS mục 16.3 (đủ kịch bản) | ⬜ |
| P6-02 | Kiểm thử hiệu năng (NFR-PERF-001…007) và độ tin cậy (NFR-REL-001…007) | 🟡 Đợt 1 ([báo cáo](../performance/P6-02-archive-performance.md)): NFR-PERF-002 đạt trên kho 100.000 (mở trang đầu 1,32 s → 0,50 s sau khi sửa nút thắt kiểm tra file); PERF-007 đạt (chuyển sang kho 100.000 + tra cứu đầu 0,3 s nóng, 3,15 s nguội); còn PERF-001/003/006 và REL ngoài REL-006 |
| P6-03 | Rà soát bảo mật: token/log/WebView/XML viewer/licence (NFR-SEC-001…008) | 🟡 Đợt 1 xong ([báo cáo](../security/P6-03-security-review.md)): 8/8 yêu cầu đạt ở mức đọc mã và test, 1 phát hiện đã sửa (ghim baseUrl vào gdt.gov.vn); còn kiểm Chromium thật, thư viện bên thứ ba, ghim máy chủ licence (chờ G-06) |
| P6-04 | Kiểm tra Release configuration không chứa development adapter/secret | ✅ `scripts/Check-Release.ps1` ([runbook](../runbooks/release-check.md)): Release đạt, Debug bị báo 28 vi phạm; chạy lại trước mỗi lần đóng gói |
| P6-05 | Đóng gói/cài đặt (net45, WebView engine, SQLite native), ghi phiên bản app/schema/WebView (NFR-MNT-005) | ⬜ |
| P6-06 | Runbook: build, release, xử lý sự cố, hỗ trợ khách hàng | ✅ [build-release-support](../runbooks/build-release-support.md) (chưa qua UAT nên bảng sự cố có thể thiếu tình huống thực tế) |
| P6-07 | Hướng dẫn sử dụng và ghi chú phát hành | ⬜ |

## 4. Tổng hợp theo tính năng

Số task lấy từ bảng theo dõi trong từng DD (cập nhật khi đổi trạng thái).

| Tính năng | DD | Phase | Tổng task | ✅ | 🟡 | ⛔ | Ghi chú |
|---|---|---|---:|---:|---:|---:|---|
| FT-00 Cổng kỹ thuật | [DD-00](../detail-design/DD-00-TECHNICAL-GATES.md) | 1 | 10 | 2 | 2 | 0 | ADR: [0001](../decisions/0001-portal-api-contract.md) (G-02, một phần), [0002](../decisions/0002-excel-export-fidelity.md) (G-04), [0003](../decisions/0003-xlsx-and-zip-generation.md) (G-09 Excel/ZIP), [0004](../decisions/0004-interim-in-memory-metadata.md) (tạm), [0005](../decisions/0005-mock-only-testing-policy.md), [0006](../decisions/0006-sqlite-provider.md) (G-08); chưa có ADR cho G-01, G-03, G-05, G-06, G-07 ||
| FT-01/02 Khung + đăng nhập | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) | 1–3 | 18 | 16 | 2 | 0 | T01-14 một phần; T01-16 Release chạy được, đăng nhập WebView chờ gate G-01; hợp đồng đăng nhập thật đã có (T01-18) |
| FT-03 Doanh nghiệp | [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md) | 2 | 10 | 6 | 3 | 0 | T02-08 chờ DD-03; 486 test pass |
| FT-04 Licence | [DD-03](../detail-design/DD-03-LICENSING.md) | 2 | 8 | 5 | 3 | 0 | Lõi + SCR-12 + HTTP client + nạp cấu hình xong; khóa/hợp đồng thật chờ G-06 |
| FT-05 Chọn kỳ | [DD-04](../detail-design/DD-04-PERIOD-DOWNLOAD-CONFIG.md) | 3 | 10 | 10 | 0 | 0 | Bộ lọc bán ra và mua vào khớp mẫu thật (mua vào: `ttxly` 5/6/8, có CCCD); đã nối SCR-03 → SCR-04 |
| FT-06 Hàng đợi tải | [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) | 3 | 12 | 10 | 1 | 1 | Chạy toàn tuyến trên cổng giả (SCR-03 → engine → kho file → SCR-04) với SQLite; phiên bị ngắt được đưa về tạm dừng lúc mở doanh nghiệp; còn T05-04 chờ G-05, UAT cổng thật |
| FT-07 Lưu trữ | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md) | 3 (T06-02 sớm ở 2) | 9 | 9 | 0 | 0 | Lõi lưu trữ, kho file thật, schema và repository SQLite xong (metadata bền vững); T06-06 có bộ kiểm tra toàn vẹn, quét file mồ côi, hộp thoại báo cáo ở nút Kiểm tra file và nút xóa file tạm sót; T06-07 chặn kho có company.db hỏng hoặc schema mới hơn |
| FT-08 Kho hóa đơn | [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md) | 4 | 9 | 9 | 0 | 0 | SCR-05 truy vấn bằng SQL (lọc, sắp xếp, nạp một trang); 100.000 hóa đơn mở trang đầu ≈ 0,5 s; còn đo `File.Exists` thật trên kho lớn |
| 03/11/2026 | T05-09: phiên tải bị ngắt giữa chừng (tắt ứng dụng, mất điện) được đưa về tạm dừng mỗi khi mở doanh nghiệp (`RecoverInterruptedAsync` + `InterruptedSessionRecovery`), checkpoint giữ nguyên, Tiếp tục chạy từ đó. Còn: `company.db` trong gói sao lưu, đo `File.Exists` thật trên kho lớn, thử kill/mở lại ứng dụng thật (UAT) |
| 03/11/2026 | Sao lưu/khôi phục có `company.db` (gói định dạng 2, bản chụp bằng backup API, khôi phục nâng schema + gắn lại mã doanh nghiệp + hoán đổi atomic + rollback cả cơ sở dữ liệu); sửa các phát hiện review 1, 2, 4, 5, 8, 9. Còn: đo `File.Exists` thật trên kho lớn, UAT |
| 03/11/2026 | Nâng schema `company.db` giữ lại bản sao trước khi nâng (`company.db.v<N>.bak`, chụp bằng backup API); 2 test mới. Còn: đo `File.Exists` thật trên kho lớn, UAT |
| 03/11/2026 | ADR 0007 (G-01): WebView đăng nhập là CefSharp 109.1.110, nâng target framework net45 → net452 khi viết adapter thật (T01-16); ADR 0008 (G-07): thứ tự an toàn khi chuyển MST. G-03, G-05, G-06 vẫn chưa có ADR vì cần bằng chứng từ cổng thật/nhà cung cấp licence |
| 03/11/2026 | Nâng toàn bộ solution từ net45 lên net452 theo ADR 0007 (chỉ đổi target framework, đường dẫn build thành `bin\Debug\net452`); build Debug/Release sạch, 1178 test qua. CefSharp chưa thêm, chờ adapter đăng nhập thật (T01-16) |
| 03/11/2026 | T01-16: adapter đăng nhập thật `CefPortalLoginBrowser` (CefSharp 109.1.110) nối vào bản Release; app build x86; `PortalLoginTraffic` (giới hạn điều hướng, nhận phản hồi đăng nhập) 36 test; spike Chromium khởi động được. Còn: thử đăng nhập tay trên cổng thật (UAT); `locales` đã rút còn `vi`/`en-US` khi build Release (237 MB) |
| 03/11/2026 | Thử đăng nhập thật thành công (người dùng tự nhập). Sửa theo phản hồi: nút Đăng nhập trên ribbon chuyển thành "Đăng nhập lại" khi đã đăng nhập; màn tiến độ tải có cột "Kết quả kiểm tra" bằng tiếng Việt (bỏ "ttxly 5/6/8"), cột Tháng dạng "Tháng 11/2026", nút "Mở Kho hóa đơn" và "Tải kỳ khác" khi phiên xong, thông báo riêng khi cổng trả về 0 hóa đơn |
| 03/11/2026 | Trình duyệt đăng nhập tự đóng thông báo của cổng, bấm "Đăng nhập" để mở sẵn form, che phần trang chủ phía sau và đặt con trỏ vào ô tên đăng nhập (`PortalLoginPageScript`, chỉ thao tác giao diện, không đọc ô nhập). Kiểm bằng trang giả trong `spikes/CefSpike assist`; chưa thử trên cổng thật. Form đăng nhập là hộp thoại của trang chủ nên trang vẫn phải tải |
| 03/11/2026 | Giãn nhịp mọi lời gọi sang cổng Thuế: tải XML từng hóa đơn trước đây không bị giãn nhịp (chỉ tra cứu), nay mọi lời gọi và lần thử lại đều chờ; Release tối thiểu 3 giây giữa hai lời gọi. Mua vào gọi 3 lần tra cứu mỗi tháng (ttxly 5, 6, 8) cộng 1 lần tải XML mỗi hóa đơn |
| 03/11/2026 | PDF và in hóa đơn (T08-04/06, G-03, ADR 0009): tự dựng từ XML bằng PDFsharp-gdi 1.50.5147; tab PDF ở SCR-07 có xem trước, In, Lưu PDF; không dựng khi XML mất/sai SHA-256. Còn: đối chiếu hóa đơn thật nhiều dạng, in thử máy in thật, lưu PDF vào kho (`FileAsset`) |
| 03/11/2026 | Sửa theo phản hồi khi thử Release: kế hoạch tải không còn liệt kê/tra cứu tháng chưa tới (`TodayClampedSplitter`: bỏ đoạn sau hôm nay, cắt tháng hiện tại tới hôm nay, báo khi kỳ hoàn toàn ở tương lai); bỏ nhãn "Đã đăng nhập" thừa; nút "Lưu bản sao" không còn bị cắt chữ (mở rộng vùng nút); màn Chi tiết bỏ các nút Mở/Tạo/Xem thừa (đã có "Xem chứng từ" và ô chọn phiên bản ở màn Xem); sửa lỗi đua làm màn tiến độ đôi khi hiện "đang chạy" sau khi phiên đã xong |
| 03/11/2026 | Bản thể hiện PDF dựng lại theo đúng bố cục trang xem hóa đơn của cổng (theo ảnh và HTML mẫu của người dùng): khung viền đôi, mã QR từ `DLQRCode`, tiêu đề đầy đủ theo mẫu số, danh sách thông tin bên bán/mua, bảng 10 cột, hai bảng tổng hợp, khối chữ ký (không in "Signature Valid" vì chưa kiểm chữ ký), phông Times New Roman. Còn: hình nền trống đồng, đối chiếu thêm hóa đơn thật nhiều dạng |
| 03/11/2026 | PDF theo mẫu PDF in của cổng: hình nền trống đồng nhúng, dấu gạch chấm dưới từng dòng, khối chữ ký xanh có "Signature Valid" và dấu tích **chỉ khi chữ ký thật sự hợp lệ**: `InvoiceSignatureVerifier` (XML-DSig bằng `SignedXml`, RSA-SHA256) kiểm trên file XML mẫu thật của cổng (hợp lệ; sửa số hóa đơn/tổng tiền thì không hợp lệ), chữ ký sai in khung đỏ. Chưa kiểm chuỗi chứng thư số |
| 03/11/2026 | Đo kiểm tra file của Kho hóa đơn: 100.000 file XML trong cây thư mục năm/tháng/khách hàng, `File.Exists` tuần tự mất khoảng 2,0 giây khi file có và 2,0 giây khi file mất (đo bằng PowerShell trên SSD, bộ nhớ đệm hệ thống đã nóng). Bộ nhớ đệm của `InvoiceArchive` (10 giây, tối đa 200.000 mục) nên các lần lọc/phân trang liên tiếp không phải kiểm lại. Chưa đo ổ đĩa quay hoặc ổ mạng, lần đầu mở kho với bộ nhớ đệm nguội |
| 04/11/2026 | Xuất ZIP XML theo gói của cổng: `invoice.xml` + `invoice.html` + `details.js` + `sign-check.jpg` + `viewinvoice-bg.jpg` (một hóa đơn: phẳng như cổng; nhiều hóa đơn: một thư mục mỗi hóa đơn + manifest.csv). `invoice.html` dựng từ XML trên CSS của cổng, chữ hiển thị trùng `invoice.html` mẫu (có test); PDF sửa thứ tự dòng người mua theo PDF in của cổng, "Tổng tiền phí" in 0 khi không có phí |
| 04/11/2026 | Nút "Kiểm tra file" của Kho hóa đơn chạy thêm kiểm tra sâu (SHA-256, XML đọc được, file lạ, file tạm sót) và mở hộp thoại liệt kê từng file có vấn đề với hóa đơn và phiên bản; chỉ báo, không tự sửa hay xóa. 5 test presenter, 1 test form |
| FT-09 Chi tiết/viewer | [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md) | 4 | 8 | 8 | 0 | 0 | SCR-06, SCR-07 (XML, PDF, in, lưu bản sao) xong; smoke 05→06→07 trên kho thật |
| FT-10 Xuất | [DD-09](../detail-design/DD-09-EXPORT.md) | 7 | 7 | 7 | 0 | 0 | Excel (bộ ghi xlsx tự viết, đã mở thử bằng Excel) và ZIP chạy từ SCR-05; hỗ trợ xuất theo dòng chọn; lịch sử xuất lưu trong company.db (T09-01); SCR-06 có nút Xuất ZIP XML một hóa đơn (T09-06); có test 100.000 hóa đơn, file khóa (T09-07) |
| FT-11 Tổng quan/lịch sử | [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md) | 5 | 7 | 7 | 0 | 0 | SCR-02/SCR-08 chạy; T10-05 xong (mở kho đúng năm của phiên) |
| FT-12 Cài đặt/sao lưu | [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md) | 5 | 8 | 7 | 1 | 0 | Sao lưu/khôi phục XML/PDF (có điểm an toàn + rollback), dung lượng, kiểm tra kết nối; gói chưa có database (ADR 0004) |
| **Tổng** | | | **116** | **79** | **24** | **2** | Còn 11 task chưa bắt đầu; số liệu đếm tự động từ các bảng DD ngày 02/11/2026 |

## 5. Phụ thuộc quan trọng

```text
Phase 1 (G-01, G-02, G-08, G-09)
   ├─> Phase 2: khung + login dev adapter   (không cần G-01/G-02 để dựng khung)
   └─> Phase 3: production login (T01-16), API client (T05-06), normalizer (T06-04)
Phase 3 ──> Phase 4 (cần dữ liệu thật trong kho) ──> Phase 5 (xuất cần Kho/Storage)
G-06 (licence) chặn T03-04/T03-05 nhưng không chặn Phase 2 nhờ DevelopmentLicenseService
G-03/G-09 (PDF) chặn T08-04/05; Excel và ZIP (T09-03/04) không cần thư viện ngoài nên đã làm xong
```

## 6. Câu hỏi mở và rủi ro

| ID | Nội dung | Ảnh hưởng | Người xử lý | Trạng thái |
|---|---|---|---|---|
| R-01 | WebView nào chạy ổn với cổng Thuế trên net45 (WebBrowser/IE legacy hay CefSharp) | FT-02, đóng gói | — | Mở (G-01) |
| R-02 | Cổng Thuế thay đổi API/giới hạn tốc độ | FT-06, FT-07 | — | Mở (G-02, G-05) |
| R-03 | Giao thức licence và nhà cung cấp máy chủ licence | FT-04 | — | Mở (G-06) |
| R-04 | Cần tài khoản cổng Thuế thật để spike và UAT | Phase 1, 3, 6 | — | Mở |
| R-05 | Thư viện SQLite/Excel/PDF phù hợp net45 và giấy phép | FT-07, FT-09, FT-10 | — | Mở (G-09 PDF); SQLite chốt ([ADR 0006](../decisions/0006-sqlite-provider.md)); Excel/ZIP đã chốt ([ADR 0003](../decisions/0003-xlsx-and-zip-generation.md)) |
| R-06 | Máy build cần Visual Studio + net45 targeting pack (môi trường dev hiện chỉ có `dotnet` CLI) | Phase 1 | — | Mở |

## 7. Nhật ký thay đổi kế hoạch

| Ngày | Thay đổi |
|---|---|
| 01/11/2026 | Tạo kế hoạch; hoàn thành tài liệu Phase 0 (bản nháp chờ review) |
| 01/11/2026 | Hoàn thành DD-04 (chia kỳ + SCR-03): 7/8 task, T04-07 chờ DD-05 |
| 01/11/2026 | Thêm hợp đồng API cổng Thuế dạng cấu hình + cổng giả ([ADR 0001](../decisions/0001-portal-api-contract.md), [api-samples](../api-samples/README.md)): gỡ chặn T04-06, T06-04; T05-06 🟡 |
| 01/11/2026 | Đồng bộ sau khi implement DD-01 (T01-01…T01-15, T01-17): 15/17 task xong; xem [ghi chú bàn giao](../progress/2026-11-01-phase1-foundation.md) |
| 01/11/2026 | **Đối chiếu API cổng Thuế thật** (đăng nhập, tra cứu bán ra, chi tiết, xuất XML zip, xuất Excel; `spec.xlsx`, mẫu bảng kê, `invoice.zip`): sửa cấu hình/client/cổng giả theo hình dạng thật (RSQL `search`, `datas/total/state`, khóa tổng hợp, zip, JWT), bỏ các biến thể giả định, thêm `PortalToken`/`PortalAuthCapture`, bộ lọc SCR-03 theo đúng tham số (thêm CCCD, dropdown mã thật, validator). G-02/G-04 đóng một phần; còn mua vào, `state`, 401/429 thật. Cập nhật DD-00/01/04/05/06/07/08/09, [ADR 0001](../decisions/0001-portal-api-contract.md), [api-samples](../api-samples/README.md) |
| 02/11/2026 | DD-06: lõi lưu trữ (domain, normalizer, `InvoiceStorageService`, kho file atomic) + tích hợp engine tải trên cổng giả; metadata còn trong bộ nhớ chờ SQLite |
| 02/11/2026 | DD-05: SCR-04 Tiến độ tải + nối engine/kho file vào ứng dụng (Debug, cổng giả); SCR-03 chỉ tạo phiên rồi mở SCR-04. Release vẫn tắt tải vì metadata chưa bền vững (chờ SQLite) |
| 02/11/2026 | DD-07: SCR-05 Kho hóa đơn (lọc, sắp xếp, phân trang, kiểm tra file, nhớ trạng thái), sau đó chọn nhiều dòng, ẩn/hiện cột, phím tắt (T07-05) |
| 02/11/2026 | DD-10: SCR-08 Lịch sử tải và SCR-02 Tổng quan (điều hướng có bộ lọc sang SCR-05/SCR-04) |
| 02/11/2026 | Review `e91145c..HEAD` và sửa: kiểm tra file không báo thành công khi chưa truy vấn, tải lại cùng hash làm mới metadata và ghi lại file thiếu, bỏ `CancelAll` khi rời màn hình, thay đổi bộ lọc khi đang tải không bị mất, đọc phân đoạn một lần cho lịch sử |
| 02/11/2026 | DD-08: SCR-06 Chi tiết hóa đơn và SCR-07 Xem chứng từ (XML cây/thô, đổi phiên bản, lưu bản sao); PDF/in chờ G-03, G-09 |
| 02/11/2026 | DD-09: xuất Excel (bộ ghi xlsx tự viết, đã mở thử bằng Excel) và ZIP XML kèm manifest từ SCR-05, hỗ trợ xuất theo dòng chọn; còn ExportRecord và xuất từ SCR-06/07 |
| 02/11/2026 | Đồng bộ lại toàn bộ trạng thái task trong file này theo bảng từng DD (đếm tự động): 114 task, 66 ✅, 24 🟡, 3 ⛔, 21 ⬜ (sau khi bổ sung ADR: 67 ✅, 23 🟡); sửa tổng số task trước đó sai (112) |
| 02/11/2026 | Bổ sung ADR 0002 (độ khớp Excel, đóng G-04), 0003 (xlsx/zip tự dựng, G-09 phần Excel/ZIP), 0004 (metadata trong bộ nhớ, tạm cho G-08), 0005 (chính sách chỉ kiểm thử trên dữ liệu giả); thêm chỉ mục `docs/decisions/README.md`; T00-06 ✅, T00-10 🟡 |
| 02/11/2026 | DD-11: SCR-09 có thống kê dung lượng, sao lưu/khôi phục (manifest + SHA-256, điểm an toàn, rollback, chặn gói khác MST) và kiểm tra kết nối (Debug mô phỏng); gói chưa chứa database vì chưa có SQLite |
| 02/11/2026 | Spike G-08 ([spikes/SqliteSpike](../../spikes/SqliteSpike/README.md)) và ADR 0006: chọn System.Data.SQLite.Core 1.0.118 cho net45 (x86/x64, WAL, backup API, 100.000 hóa đơn, chịu bị giết giữa giao dịch); T00-09 ✅, gỡ chặn T06-02/03, T07-02/08, T05-02/09; còn kiểm máy sạch |
| 02/11/2026 | T06-02: `CompanyDatabaseInitializer`, schema v1 và khung migration cho `company.db` (System.Data.SQLite.Core 1.0.118 trong Infrastructure, 14 test); chưa nối vào `CompositionRoot`, T06-03 (repository) là bước kế |
| 02/11/2026 | Mẫu thật tra cứu mua vào + ảnh form (`docs/api-samples`): `ttxly` mua vào chỉ 5/6/8, không có Tất cả và bắt buộc; có `nmcmnd`; `nbmst` là MST người bán. Cập nhật api-samples/ADR 0001/DD-04/DD-05, thêm T04-10, T05-12 ([kế hoạch sửa](../progress/2026-11-02-purchase-search-alignment.md)); đã thay token/cookie thật trong `sample-api-web.md` bằng chỗ giữ chỗ |
| 02/11/2026 | T04-10 và T05-12: tra cứu mua vào theo mẫu thật (CCCD ở cả hai chiều, `ttxly` chỉ 5/6/8, "Tất cả" = tải ba lượt mỗi tháng), cổng giả bắt buộc `ttxly`; 1084 test qua, Release sạch. Còn xác nhận trên cổng thật việc bỏ `ttxly` |
| 03/11/2026 | T06-03 và T05-02: repository SQLite cho hóa đơn và phiên tải nối vào `CompositionRoot` (Debug và Release); Release bật engine tải (đăng nhập thật vẫn chờ G-01); ADR 0004 hết hiệu lực. Còn: truy vấn SQL cho Kho hóa đơn (T07-02), khôi phục phiên lúc khởi động (T05-09), `company.db` trong gói sao lưu |
| 03/11/2026 | T07-02 và T07-08: Kho hóa đơn lọc/sắp xếp/đếm bằng SQL (migration 2: `number_key`, `counterparty_sort`, `current_path`, `version_count`), chỉ nạp đầy đủ một trang; 100.000 hóa đơn giả mở trang đầu ≈ 0,5 s. Còn: khôi phục phiên lúc khởi động (T05-09), `company.db` trong gói sao lưu, đo `File.Exists` thật trên kho lớn |
