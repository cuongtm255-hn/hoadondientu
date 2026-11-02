# Kế hoạch và theo dõi tiến độ dự án

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | PLAN-HDDT-001 |
| Phiên bản | 1.0 |
| Cập nhật lần cuối | 01/11/2026 |
| Phase hiện tại | **Phase 0 — Yêu cầu và thiết kế** (đang chờ review để đóng) |

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
| 1 | Nền tảng và technical spike | Solution chạy build được; ADR chặn kiến trúc | FT-00 | 🟡 Solution xong; chưa làm spike/ADR (G-01, G-02, G-08, G-09) |
| 2 | Khung chương trình, doanh nghiệp, licence và đăng nhập | Chạy 3 chế độ bằng development adapter | FT-01, FT-02, FT-03, FT-04 | 🟡 DD-01 xong phần dev adapter; DD-02 xong phần lõi + UI; DD-03 chưa bắt đầu |
| 3 | Tải hóa đơn end-to-end | Chọn kỳ → tải → lưu XML trên cổng thật | FT-05, FT-06, FT-07, FT-02 (production) | ⬜ |
| 4 | Kho hóa đơn, chi tiết và trình xem | Tra cứu, xem XML/PDF | FT-08, FT-09 | ⬜ |
| 5 | Xuất dữ liệu, tổng quan, lịch sử, cài đặt và sao lưu | Hoàn thiện tính năng còn lại | FT-10, FT-11, FT-12 | ⬜ |
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
**Tiêu chí thoát:** solution build bằng MSBuild; ADR G-01, G-02, G-08, G-09 (tối thiểu) đã chốt; mẫu API đã làm sạch được commit; runbook build đã có.

| Task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T01-01 | Tạo solution/project/test project | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | ✅ |
| T01-17 | Cập nhật lệnh build/test thật vào `CLAUDE.md`/`README.md` + runbook | DD-01 | ✅ (runbook chưa viết) |
| T00-01 | Spike WebView trên net45 | [DD-00](../detail-design/DD-00-TECHNICAL-GATES.md#6-theo-dõi-tiến-độ) | ⬜ |
| T00-02 | Cách phát hiện login và lấy token/cookie | DD-00 | ⬜ |
| T00-03 | Ghi traffic API (xác minh phiên/MST, tra cứu, phân trang, tải XML) | DD-00 | 🟡 (đăng nhập, bán ra, chi tiết, xuất XML/Excel đã có; còn mua vào, `state`, 401/429 thật) |
| T00-04 | Giới hạn tốc độ / 429 | DD-00 | ⬜ |
| T00-05 | Chiến lược PDF | DD-00 | ⬜ |
| T00-06 | Đối chiếu mẫu Excel | DD-00 | 🟡 (đã có mẫu thật `DANH SÁCH HÓA ĐƠN.xlsx`, bố cục ghi ở DD-09; còn so sánh file ứng dụng xuất) |
| T00-07 | Chốt giao thức licence | DD-00 | ⬜ |
| T00-09 | Chọn SQLite provider net45 | DD-00 | ⬜ |
| T00-10 | Chọn thư viện Excel/PDF/ZIP net45 | DD-00 | ⬜ |

### Phase 2 — Khung chương trình, doanh nghiệp, licence và đăng nhập

**Mục tiêu:** chạy được Standard / Service / ReadOnly bằng development adapter; đăng nhập qua `PortalLoginForm` với cơ chế xác minh MST.
**Tiêu chí thoát:** Definition of Done của [DD-01 mục 21.2](../detail-design/DD-01-APP-SHELL-LOGIN.md#212-definition-of-done); AC-12, AC-14, AC-15 (phần chuyển MST), AC-16, AC-17 pass bằng development adapter.

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T01-02…T01-15 | Domain/Application nền, composition root, shell, startup, context/switch, login form, session, logging, test | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | ✅ (T01-14 một phần, chờ DD-02/03/06) |
| T02-01…T02-10 | Catalog, first-run, SCR-10/11, relink, nâng cấp Standard → Service | [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md#9-theo-dõi-tiến-độ-implement) | 🟡 (T02-08 chờ DD-03; T02-03/07/09 một phần) |
| T03-01…T03-08 | Entitlement, licence dev + production, SCR-12 | [DD-03](../detail-design/DD-03-LICENSING.md#9-theo-dõi-tiến-độ-implement) | 🟡 (T03-01..03, 06, 08 xong; T03-04/05/07 một phần, chờ G-06) |
| T06-02 (phần khởi tạo) | Khung `CompanyDatabaseInitializer` tối thiểu để DD-01/DD-02 mở kho | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md#91-công-việc) | ⬜ |
| T00-08 | Spike an toàn chuyển MST / crash recovery | DD-00 | ⬜ |

### Phase 3 — Tải hóa đơn end-to-end

**Mục tiêu:** từ SCR-03 chọn kỳ đến lưu XML đúng, chống trùng, tiếp tục được sau gián đoạn — trên cổng thật.
**Tiêu chí thoát:** AC-02, AC-03, AC-04, AC-05, AC-06, AC-07, AC-15 (tải) pass; UAT mục 16.3 SRS (phần đăng nhập, mua/bán, nhiều trang, đăng nhập lại).

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T01-18 | Ghi lại API đăng nhập thật: `PortalToken`, `PortalAuthCapture`, xác minh MST bằng JWT | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | ✅ |
| T01-16 | Thay development adapter bằng production login adapter | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md#21-theo-dõi-tiến-độ-implement) | 🟡 (Release chạy được; đăng nhập cổng Thuế chờ G-01, G-02) |
| T04-01…T04-09 | Chia kỳ, SCR-03, bộ lọc theo tham số thật của cổng | [DD-04](../detail-design/DD-04-PERIOD-DOWNLOAD-CONFIG.md#7-theo-dõi-tiến-độ-implement) | ✅ |
| T05-01…T05-11 | Orchestrator, checkpoint, SCR-04, API client | [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md#8-theo-dõi-tiến-độ-implement) | 🟡 (engine, client, SCR-04 và nối vào ứng dụng xong trên cổng giả; còn SQLite, khôi phục khi khởi động, UAT cổng thật) |
| T06-01…T06-09 | Chuẩn hóa, lưu XML, phiên bản hóa, toàn vẹn | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md#9-theo-dõi-tiến-độ-implement) | 🟡 (T06-01, 04 xong; T06-05/08/09 một phần; SQLite chờ G-08) |

### Phase 4 — Kho hóa đơn, chi tiết và trình xem

**Tiêu chí thoát:** NFR-PERF-002 đạt trên 100.000 hóa đơn; AC-10 pass; điều hướng SCR-05 ↔ SCR-06 ↔ SCR-07 giữ bộ lọc.

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T07-01…T07-09 | Kho hóa đơn SCR-05 | [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md#6-theo-dõi-tiến-độ-implement) | 🟡 6/9 |
| T08-01…T08-08 | Chi tiết, trình xem XML, PDF, in | [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md#7-theo-dõi-tiến-độ-implement) | 🟡 (SCR-06 và SCR-07 phần XML xong; PDF/in chờ G-03, G-09) |

### Phase 5 — Xuất dữ liệu, tổng quan, lịch sử, cài đặt và sao lưu

**Tiêu chí thoát:** AC-08, AC-09, AC-11 pass; Dashboard/History phản ánh đúng phiên dở dang (AC-07).

| Nhóm task | Mô tả | DD | Trạng thái |
|---|---|---|---|
| T09-01…T09-07 | Xuất Excel/ZIP | [DD-09](../detail-design/DD-09-EXPORT.md#7-theo-dõi-tiến-độ-implement) | 🟡 (T09-02 đối chiếu mẫu Excel xong; còn lại chờ DD-06/07, G-09) |
| T10-01…T10-07 | Tổng quan, lịch sử | [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md#6-theo-dõi-tiến-độ-implement) | 🟡 6/7 (SCR-02, SCR-08 xong; còn T10-05 chế độ chi tiết lịch sử trên SCR-04) |
| T11-01…T11-08 | Cài đặt, sao lưu/khôi phục | [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md#7-theo-dõi-tiến-độ-implement) | 🟡 (khung SCR-09 + lối vào Bản quyền; sao lưu chờ DD-06) |

### Phase 6 — Hardening, UAT và phát hành

**Tiêu chí thoát:** toàn bộ AC-01…AC-17 đạt; UAT mục 16.3 SRS hoàn tất; không còn lỗi mức chặn; gói cài đặt chạy trên máy sạch.

| ID | Công việc | Trạng thái |
|---|---|---|
| P6-01 | UAT trên cổng thật theo SRS mục 16.3 (đủ kịch bản) | ⬜ |
| P6-02 | Kiểm thử hiệu năng (NFR-PERF-001…007) và độ tin cậy (NFR-REL-001…007) | ⬜ |
| P6-03 | Rà soát bảo mật: token/log/WebView/XML viewer/licence (NFR-SEC-001…008) | ⬜ |
| P6-04 | Kiểm tra Release configuration không chứa development adapter/secret | ⬜ |
| P6-05 | Đóng gói/cài đặt (net45, WebView engine, SQLite native), ghi phiên bản app/schema/WebView (NFR-MNT-005) | ⬜ |
| P6-06 | Runbook: build, release, xử lý sự cố, hỗ trợ khách hàng | ⬜ |
| P6-07 | Hướng dẫn sử dụng và ghi chú phát hành | ⬜ |

## 4. Tổng hợp theo tính năng

Số task lấy từ bảng theo dõi trong từng DD (cập nhật khi đổi trạng thái).

| Tính năng | DD | Phase | Tổng task | ✅ | 🟡 | ⛔ | Ghi chú |
|---|---|---|---:|---:|---:|---:|---|
| FT-00 Cổng kỹ thuật | [DD-00](../detail-design/DD-00-TECHNICAL-GATES.md) | 1 | 10 | 0 | 0 | 0 | |
| FT-01/02 Khung + đăng nhập | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) | 1–3 | 18 | 16 | 2 | 0 | T01-14 một phần; T01-16 Release chạy được, đăng nhập WebView chờ gate G-01; hợp đồng đăng nhập thật đã có (T01-18) |
| FT-03 Doanh nghiệp | [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md) | 2 | 10 | 6 | 3 | 0 | T02-08 chờ DD-03; 486 test pass |
| FT-04 Licence | [DD-03](../detail-design/DD-03-LICENSING.md) | 2 | 8 | 5 | 3 | 0 | Lõi + SCR-12 + HTTP client + nạp cấu hình xong; khóa/hợp đồng thật chờ G-06 |
| FT-05 Chọn kỳ | [DD-04](../detail-design/DD-04-PERIOD-DOWNLOAD-CONFIG.md) | 3 | 9 | 9 | 0 | 0 | Bộ lọc bán ra khớp tham số thật, mua vào ⚠; đã nối SCR-03 → SCR-04 |
| FT-06 Hàng đợi tải | [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) | 3 | 11 | 7 | 3 | 1 | Chạy toàn tuyến trên cổng giả (SCR-03 → engine → kho file → SCR-04); còn SQLite (G-08), khôi phục lúc khởi động, T05-04 chờ G-05, UAT cổng thật |
| FT-07 Lưu trữ | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md) | 3 (T06-02 sớm ở 2) | 9 | 2 | 3 | 1 | Lõi lưu trữ + kho file thật xong; metadata trong bộ nhớ, chờ G-08 (SQLite) |
| FT-08 Kho hóa đơn | [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md) | 4 | 9 | 6 | 0 | 0 | SCR-05 chạy trên kho bộ nhớ; còn SQLite, chọn nhiều |
| FT-09 Chi tiết/viewer | [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md) | 4 | 8 | 3 | 3 | 1 | SCR-06 và XML SCR-07 xong; PDF/in chờ G-03, G-09 |
| FT-10 Xuất | [DD-09](../detail-design/DD-09-EXPORT.md) | 5 | 7 | 1 | 0 | 1 | Mẫu Excel đã đối chiếu (T09-02); T09-03 chờ G-09 |
| FT-11 Tổng quan/lịch sử | [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md) | 5 | 7 | 6 | 0 | 0 | SCR-02/SCR-08 chạy trên dữ liệu bộ nhớ; còn T10-05 |
| FT-12 Cài đặt/sao lưu | [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md) | 5 | 8 | 0 | 0 | 0 | |
| **Tổng** | | | **112** | **22** | **3** | **9** | DD-01 (dev adapter) + client cổng Thuế theo cấu hình/cổng giả + SCR-03 |

## 5. Phụ thuộc quan trọng

```text
Phase 1 (G-01, G-02, G-08, G-09)
   ├─> Phase 2: khung + login dev adapter   (không cần G-01/G-02 để dựng khung)
   └─> Phase 3: production login (T01-16), API client (T05-06), normalizer (T06-04)
Phase 3 ──> Phase 4 (cần dữ liệu thật trong kho) ──> Phase 5 (xuất cần Kho/Storage)
G-06 (licence) chặn T03-04/T03-05 nhưng không chặn Phase 2 nhờ DevelopmentLicenseService
G-03/G-04/G-09 chặn tương ứng T08-05, T09-02/03
```

## 6. Câu hỏi mở và rủi ro

| ID | Nội dung | Ảnh hưởng | Người xử lý | Trạng thái |
|---|---|---|---|---|
| R-01 | WebView nào chạy ổn với cổng Thuế trên net45 (WebBrowser/IE legacy hay CefSharp) | FT-02, đóng gói | — | Mở (G-01) |
| R-02 | Cổng Thuế thay đổi API/giới hạn tốc độ | FT-06, FT-07 | — | Mở (G-02, G-05) |
| R-03 | Giao thức licence và nhà cung cấp máy chủ licence | FT-04 | — | Mở (G-06) |
| R-04 | Cần tài khoản cổng Thuế thật để spike và UAT | Phase 1, 3, 6 | — | Mở |
| R-05 | Thư viện SQLite/Excel/PDF phù hợp net45 và giấy phép | FT-07, FT-09, FT-10 | — | Mở (G-08, G-09) |
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
