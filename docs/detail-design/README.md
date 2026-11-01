# Detail design

Thư mục này chứa các tài liệu thiết kế chi tiết, **mỗi tính năng trong [Feature list](../requirements/FEATURE-LIST.md) có một tài liệu**. Mỗi tài liệu có phần *Theo dõi tiến độ implement* để lập trình viên cập nhật; tiến độ tổng hợp theo phase nằm ở [PROJECT-PLAN](../plans/PROJECT-PLAN.md).

## Chỉ mục

| Mã | Tài liệu | Tính năng | Màn hình | Phase |
|---|---|---|---|:---:|
| DD-00 | [Cổng kỹ thuật và ADR](DD-00-TECHNICAL-GATES.md) | FT-00 | — | 1 |
| DD-01 | [Khung chương trình, khởi động và đăng nhập](DD-01-APP-SHELL-LOGIN.md) | FT-01, FT-02 | SCR-01 + khung | 2–3 |
| DD-02 | [Quản lý doanh nghiệp](DD-02-COMPANY-MANAGEMENT.md) | FT-03 | SCR-10, 11 | 2 |
| DD-03 | [Licence và quyền tính năng](DD-03-LICENSING.md) | FT-04 | SCR-12 | 2 |
| DD-04 | [Chọn kỳ và kế hoạch tải](DD-04-PERIOD-DOWNLOAD-CONFIG.md) | FT-05 | SCR-03 | 3 |
| DD-05 | [Hàng đợi tải và checkpoint](DD-05-DOWNLOAD-ENGINE.md) | FT-06 | SCR-04 | 3 |
| DD-06 | [Chuẩn hóa, lưu XML, SQLite](DD-06-INVOICE-STORAGE.md) | FT-07 | — | 3 |
| DD-07 | [Kho hóa đơn](DD-07-INVOICE-ARCHIVE.md) | FT-08 | SCR-05 | 4 |
| DD-08 | [Chi tiết hóa đơn và trình xem](DD-08-INVOICE-DETAIL-VIEWER.md) | FT-09 | SCR-06, 07 | 4 |
| DD-09 | [Xuất Excel/ZIP](DD-09-EXPORT.md) | FT-10 | — | 5 |
| DD-10 | [Tổng quan và lịch sử](DD-10-DASHBOARD-HISTORY.md) | FT-11 | SCR-02, 08 | 5 |
| DD-11 | [Cài đặt, sao lưu, khôi phục](DD-11-SETTINGS-BACKUP.md) | FT-12 | SCR-09 | 5 |

## Quy ước đặt tên

`DD-<nn>-<TEN-VIET-HOA-NGAN>.md`. Mã `DD-HDDT-<nn>` ghi trong bảng thông tin tài liệu. Task có ID `T<nn>-<mm>` (ví dụ `T05-03` là task thứ 3 của DD-05). Mã gate kỹ thuật `G-xx` định nghĩa trong [DD-00](DD-00-TECHNICAL-GATES.md).

## Mẫu cấu trúc tài liệu

Mọi detail design theo cùng khung (có thể thêm mục nội dung ở giữa):

1. **Thông tin tài liệu** — bảng: mã, tên, phiên bản, ngày, trạng thái, Feature, SRS, màn hình, wireframe, phụ thuộc, phase.
2. **Mục đích và phạm vi** — trong/ngoài phạm vi, chỉ sang DD khác nếu liên quan.
3. **Thiết kế** — mô hình, interface, luồng, màn hình (các mục nội dung).
4. **Bảo mật / lỗi** (nếu có).
5. **Kiểm thử** — unit / integration / UI smoke / UAT.
6. **Theo dõi tiến độ implement** — bảng công việc, DoD, nhật ký.
7. **Lịch sử thay đổi tài liệu**.

### Bảng theo dõi tiến độ

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn (ghi lý do/gate ở *Ghi chú*).

## Quy tắc cập nhật

1. Khi bắt đầu/hoàn thành task: sửa trạng thái trong DD, điền PR/commit, thêm dòng vào *Nhật ký tiến độ*.
2. Đồng bộ bảng tổng hợp mục 4 của [PROJECT-PLAN](../plans/PROJECT-PLAN.md) (số task ✅/🟡/⛔) và trạng thái phase.
3. Thay đổi thiết kế: tăng phiên bản, ghi *Lịch sử thay đổi tài liệu*; nếu đổi yêu cầu nghiệp vụ, cập nhật [SRS](../requirements/SRS.md) và [FEATURE-LIST](../requirements/FEATURE-LIST.md) trước.
4. Quyết định kiến trúc quan trọng ghi ADR trong [`docs/decisions/`](../decisions/) và liên kết từ DD.
5. Giữ nhất quán mã `SCR-xx`, `FR-/BR-/NFR-/AC-` với SRS.
