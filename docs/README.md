# docs — project context for humans and Claude Code

Index of where context lives. Keep files short; link rather than duplicate.

## Tài liệu chính

- [SRS](requirements/SRS.md) — đặc tả yêu cầu
- [Feature list](requirements/FEATURE-LIST.md) — danh sách tính năng FT-00…FT-12
- [Chỉ mục Detail design](detail-design/README.md) — mỗi tính năng một tài liệu, có phần theo dõi tiến độ implement
  - [DD-01 — Khung chương trình, khởi động và đăng nhập](detail-design/DD-01-APP-SHELL-LOGIN.md)
- [**Kế hoạch và tiến độ dự án theo phase**](plans/PROJECT-PLAN.md) — đọc trước khi tiếp tục công việc
- [Wireframe HTML tương tác](wireframes/index.html) · [Danh mục wireframe PNG](wireframes/README.md)

SRS và wireframe mô tả cả `StandardMode` một MST và `ServiceMode` đa MST kích hoạt bằng licence theo máy. ServiceMode chỉ mở một MST tại một thời điểm và cách ly SQLite/thư mục theo từng doanh nghiệp.

## Thứ tự đọc khi tiếp tục công việc

1. [`plans/PROJECT-PLAN.md`](plans/PROJECT-PLAN.md) — phase hiện tại, task đang làm/bị chặn.
2. [`progress/`](progress/) — ghi chú bàn giao gần nhất.
3. Detail design của tính năng đang làm (mục *Theo dõi tiến độ implement*) và SRS tương ứng.

## Cấu trúc thư mục

| Folder | Put here |
|---|---|
| `requirements/` | SRS, feature list, acceptance criteria (source: portal "Tra cứu" behaviour + period-selection extension) |
| `detail-design/` | Detail design theo tính năng `DD-xx`, kèm bảng theo dõi tiến độ implement; chỉ mục + mẫu ở `README.md` |
| `wireframes/` | Interactive HTML wireframes and PNG exports, keyed by `SCR-xx` screen IDs |
| `architecture/` | Tổng quan kiến trúc/sơ đồ xuyên tính năng (chi tiết theo tính năng nằm ở `detail-design/`) |
| `decisions/` | ADRs, one file per decision (`NNNN-title.md`), e.g. WebView library choice (sinh ra từ các gate trong DD-00) |
| `api-samples/` | Captured portal API requests/responses (strip tokens and personal/tax data) |
| `plans/` | `PROJECT-PLAN.md` — kế hoạch và tiến độ chung theo phase, tham chiếu SRS/feature/DD |
| `progress/` | Status log / handoff notes between sessions (what's done, next, blockers) |
| `runbooks/` | Build, release, and troubleshooting procedures |
| `security/` | Security reviews (P6-03) and their findings |
| `performance/` | Performance measurements (P6-02) |

## Quy tắc đồng bộ tài liệu

- Đổi yêu cầu: sửa SRS → Feature list → Detail design → Plan (theo thứ tự).
- Đổi trạng thái task: sửa bảng trong DD, rồi đồng bộ bảng tổng hợp trong `PROJECT-PLAN.md`.
- Mã `SCR-xx`, `FR-/BR-/NFR-/AC-`, `FT-xx`, `DD-xx`, `T<nn>-<mm>`, `G-xx` phải nhất quán giữa các tài liệu.
