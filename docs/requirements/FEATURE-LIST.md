# Danh sách tính năng (Feature List)

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | FL-HDDT-001 |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Nguồn | [SRS-HDDT-001 v1.1](SRS.md), [wireframe](../wireframes/README.md) |
| Dùng cho | [Detail design](../detail-design/README.md) · [Kế hoạch dự án](../plans/PROJECT-PLAN.md) |

Mỗi tính năng (`FT-xx`) có đúng một tài liệu detail design chính (`DD-xx`) chứa phần **Theo dõi tiến độ implement**. Tiến độ tổng hợp theo phase nằm ở [PROJECT-PLAN](../plans/PROJECT-PLAN.md).

Ưu tiên: **M** = bắt buộc cho phiên bản 1 · **S** = nên có trong phiên bản 1.

## 1. Tổng quan

| Mã | Tính năng | Màn hình | Ưu tiên | Phase | Detail design |
|---|---|---|:---:|:---:|---|
| FT-00 | Cổng kỹ thuật và ADR | — | M | 1 | [DD-00](../detail-design/DD-00-TECHNICAL-GATES.md) |
| FT-01 | Khung chương trình, khởi động và điều hướng | Khung SCR-02…12 | M | 2 | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) |
| FT-02 | Đăng nhập cổng Thuế và quản lý phiên | SCR-01 | M | 2 (dev adapter), 3 (production) | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) |
| FT-03 | Quản lý doanh nghiệp và chuyển MST | SCR-10, SCR-11 | M | 2 | [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md) |
| FT-04 | Licence và quyền tính năng | SCR-12 | M | 2 | [DD-03](../detail-design/DD-03-LICENSING.md) |
| FT-05 | Chọn kỳ và lập kế hoạch tải | SCR-03 | M | 3 | [DD-04](../detail-design/DD-04-PERIOD-DOWNLOAD-CONFIG.md) |
| FT-06 | Hàng đợi tải, phân trang và checkpoint | SCR-04 | M | 3 | [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) |
| FT-07 | Chuẩn hóa, lưu XML, chống trùng và phiên bản hóa | — | M | 3 | [DD-06](../detail-design/DD-06-INVOICE-STORAGE.md) |
| FT-08 | Kho hóa đơn | SCR-05 | M | 4 | [DD-07](../detail-design/DD-07-INVOICE-ARCHIVE.md) |
| FT-09 | Chi tiết hóa đơn, trình xem XML/PDF và in | SCR-06, SCR-07 | M | 4 | [DD-08](../detail-design/DD-08-INVOICE-DETAIL-VIEWER.md) |
| FT-10 | Xuất Excel và ZIP XML | SCR-05, 06, 07 | M | 5 | [DD-09](../detail-design/DD-09-EXPORT.md) |
| FT-11 | Tổng quan và lịch sử tải | SCR-02, SCR-08 | M | 5 | [DD-10](../detail-design/DD-10-DASHBOARD-HISTORY.md) |
| FT-12 | Cài đặt, sao lưu và khôi phục | SCR-09 | M | 5 | [DD-11](../detail-design/DD-11-SETTINGS-BACKUP.md) |

## 2. Mô tả chi tiết

### FT-00 — Cổng kỹ thuật và ADR

- **Mô tả:** Technical spike và quyết định chặn kiến trúc: WebView, API discovery, PDF, Excel fidelity, rate limit, licence protocol, an toàn chuyển MST, SQLite provider, thư viện Excel/PDF/ZIP trên net45.
- **SRS:** mục 6.4, 11, 17.
- **Phụ thuộc:** không. **Chặn:** production adapter của FT-02, FT-04, FT-06, FT-09, FT-10.
- **Nghiệm thu:** mỗi gate có ADR trong `docs/decisions/` và mẫu đã làm sạch trong `docs/api-samples/`.

### FT-01 — Khung chương trình, khởi động và điều hướng

- **Mô tả:** Solution/project, composition root, `MainShellForm` (thanh điều hướng trái, header, status strip), xác định `Standard`/`Service`/`ReadOnlyService`, route khởi động, đóng ứng dụng, xử lý lỗi/logging chung.
- **SRS:** BR-001, BR-016, FR-COMP-001, FR-COMP-006, FR-COMP-009, mục 10.1, NFR-USE-001..003, 006..007, NFR-PERF-001, 003, NFR-MNT-001, 006.
- **Phụ thuộc:** FT-04 (interface entitlement), FT-03 (catalog).
- **Nghiệm thu:** AC-12; Service luôn mở SCR-10 khi khởi động.

### FT-02 — Đăng nhập cổng Thuế và quản lý phiên

- **Mô tả:** WebView đăng nhập thủ công, thu token/cookie, xác minh phiên và MST khớp, `PortalSession` trong bộ nhớ, đăng nhập lại khi hết hạn, đăng xuất xóa phiên.
- **SRS:** FR-AUTH-001..012, BR-012, BR-020, NFR-SEC-001..002, 006.
- **Phụ thuộc:** FT-00 (G-01, G-02) cho production; FT-01, FT-03.
- **Nghiệm thu:** AC-01, AC-06 (phần đăng nhập lại), AC-14 (bước đăng nhập sai MST).

### FT-03 — Quản lý doanh nghiệp và chuyển MST

- **Mô tả:** Khai báo doanh nghiệp StandardMode lần đầu; danh mục `companies.json`; thêm/sửa/ngừng quản lý/liên kết lại; chuyển MST an toàn; mỗi MST một thư mục và SQLite.
- **SRS:** BR-016..023, FR-SET-001..003, FR-COMP-001..014, NFR-REL-006..007, NFR-PERF-006..007.
- **Phụ thuộc:** FT-01, FT-04; schema từ FT-07.
- **Nghiệm thu:** AC-12, AC-13 (đăng ký kho đầu tiên), AC-14, AC-15, AC-17.

### FT-04 — Licence và quyền tính năng

- **Mô tả:** Kích hoạt/xác thực/hủy kích hoạt theo máy; entitlement có chữ ký; ngoại tuyến tối đa 30 ngày; `ReadOnlyServiceMode`; kiểm quyền ở application service.
- **SRS:** BR-022, BR-024..025, FR-LIC-001..012, NFR-SEC-007..008, NFR-MNT-006.
- **Phụ thuộc:** FT-00 (G-06).
- **Nghiệm thu:** AC-12, AC-13, AC-16.

### FT-05 — Chọn kỳ và lập kế hoạch tải

- **Mô tả:** Chọn mua vào/bán ra, kiểu kỳ (tháng/quý/năm/tùy chọn), bộ lọc, chia kỳ thành đoạn tháng, xem trước kế hoạch, điều kiện bật nút bắt đầu.
- **SRS:** FR-SEARCH-001..010, BR-002..006.
- **Phụ thuộc:** FT-01; kích hoạt tải cần FT-02, FT-06.
- **Nghiệm thu:** AC-02 (kế hoạch 12 đoạn), AC-03.

### FT-06 — Hàng đợi tải, phân trang và checkpoint

- **Mô tả:** `DownloadSession`/`DownloadSegment`, thực thi tuần tự, phân trang theo API, checkpoint, tạm dừng/tiếp tục/hủy, thử lại, chờ đăng nhập khi hết token, màn hình tiến độ.
- **SRS:** FR-JOB-001..014, BR-006..007, 011..012, NFR-REL-002, 005, NFR-MNT-004, mục 13.
- **Phụ thuộc:** FT-00 (G-02, G-05), FT-02, FT-05, FT-07.
- **Nghiệm thu:** AC-02, AC-04, AC-06, AC-07, AC-15.

### FT-07 — Chuẩn hóa, lưu XML, chống trùng và phiên bản hóa

- **Mô tả:** Chuẩn hóa metadata mua/bán, khóa chống trùng, tải XML qua file tạm + SHA-256, phiên bản hóa XML, SQLite theo MST, cấu trúc thư mục, quy tắc tên file, migration.
- **SRS:** BR-008..010, 013, 018; FR-STORE-001..010; mục 9; NFR-REL-001, 003, 004, NFR-MNT-003.
- **Phụ thuộc:** FT-00 (G-02, G-08).
- **Nghiệm thu:** AC-04, AC-05.

### FT-08 — Kho hóa đơn

- **Mô tả:** Tra cứu dữ liệu đã lưu theo mua/bán, bộ lọc, tìm nhanh, phân trang/sắp xếp/chọn nhiều, ẩn/hiện cột, tóm tắt tổng tiền, chỉ báo file lỗi, giữ bộ lọc khi quay lại.
- **SRS:** FR-ARCH-001..010, BR-015, NFR-PERF-002, 004, NFR-USE-004.
- **Phụ thuộc:** FT-07.
- **Nghiệm thu:** tìm theo số/MST trên 100.000 bản ghi ≤ 2 giây p95.

### FT-09 — Chi tiết hóa đơn, trình xem XML/PDF và in

- **Mô tả:** Chi tiết metadata + phiên bản, xem XML dạng cây/thô an toàn, tạo PDF từ XML hiện hành, in, lưu bản sao PDF.
- **SRS:** FR-DETAIL-001..007, BR-008, 014, NFR-SEC-003.
- **Phụ thuộc:** FT-07, FT-08; FT-00 (G-03, G-09).
- **Nghiệm thu:** AC-10.

### FT-10 — Xuất Excel và ZIP XML

- **Mô tả:** Xuất danh sách Excel đúng 19 cột theo mẫu cổng; ZIP XML kèm manifest SHA-256; xử lý file lỗi; không ghi đè khi chưa xác nhận.
- **SRS:** FR-EXPORT-001..008, FR-ARCH-008, mục 9.1, NFR-PERF-005.
- **Phụ thuộc:** FT-07, FT-08; FT-00 (G-04, G-09).
- **Nghiệm thu:** AC-08, AC-09.

### FT-11 — Tổng quan và lịch sử tải

- **Mô tả:** Dashboard trạng thái đăng nhập/kho/phiên cần xử lý; danh sách lịch sử phiên, lỗi theo đoạn, mở lại phiên và kho theo bộ lọc phiên.
- **SRS:** FR-DASH-001..003, FR-HIST-001..005, FR-JOB-010..011.
- **Phụ thuộc:** FT-06, FT-07, FT-08.
- **Nghiệm thu:** AC-07 (phiên chưa hoàn tất hiện khi mở lại).

### FT-12 — Cài đặt, sao lưu và khôi phục

- **Mô tả:** Thông tin kho/dung lượng, kiểm tra kết nối, mở thư mục kho, sao lưu có manifest, khôi phục có kiểm tra MST/schema/hash và điểm sao lưu an toàn.
- **SRS:** FR-SET-004..009, NFR-SEC-004, NFR-REL-003, NFR-MNT-003.
- **Phụ thuộc:** FT-03, FT-07.
- **Nghiệm thu:** AC-11.

## 3. Ma trận phủ yêu cầu SRS → tính năng

| Nhóm SRS | Tính năng |
|---|---|
| 8.1 AUTH (FR-AUTH) | FT-02 (FR-AUTH-007..008 phối hợp FT-06) |
| 8.2 SEARCH (FR-SEARCH) | FT-05 |
| 8.3 JOB (FR-JOB) | FT-06; FR-JOB-010..011 → FT-11 |
| 8.4 STORE (FR-STORE) | FT-07 |
| 8.5 ARCHIVE (FR-ARCH) | FT-08; FR-ARCH-008 → FT-10 |
| 8.6 DETAIL (FR-DETAIL) | FT-09 |
| 8.7 EXPORT (FR-EXPORT) | FT-10 |
| 8.8 DASH/HIST | FT-11 |
| 8.9 SET (FR-SET) | FR-SET-001..003 → FT-03; FR-SET-004..009 → FT-12 |
| 8.10 COMP (FR-COMP) | FT-03 (FR-COMP-006, 009 hiển thị → FT-01) |
| 8.11 LIC (FR-LIC) | FT-04 |
| BR-001..025 | Theo bảng ở từng tính năng |
| NFR-SEC / REL / PERF / USE / MNT | Phân bổ ở từng tính năng; kiểm tra tổng hợp ở Phase 6 |

## 4. Ma trận nghiệm thu AC → tính năng

| AC | Tính năng liên quan |
|---|---|
| AC-01 | FT-02, FT-01 |
| AC-02 | FT-05, FT-06, FT-07 |
| AC-03 | FT-05 |
| AC-04, AC-05 | FT-06, FT-07 |
| AC-06, AC-07 | FT-02, FT-06, FT-11 |
| AC-08, AC-09 | FT-10 |
| AC-10 | FT-09 |
| AC-11 | FT-12 |
| AC-12, AC-13, AC-16 | FT-04, FT-03, FT-01 |
| AC-14, AC-15, AC-17 | FT-03, FT-02, FT-06 |

## 5. Ngoài phạm vi

Giữ nguyên SRS mục 3.2: tự động đăng nhập/CAPTCHA, tải nền đa MST, báo cáo xuyên MST, đồng bộ đám mây, sửa XML, lập/ký hóa đơn, kê khai thuế, lập lịch tải tự động, mã hóa kho dữ liệu riêng.
