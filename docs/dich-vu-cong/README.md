# Nhóm chức năng: Tra cứu Dịch vụ công thuế (DVC)

Nhóm chức năng thứ hai của phần mềm, **tách hẳn** nhóm "Tra cứu hóa đơn" (tài liệu ở [`docs/`](../README.md)). Lý do tách: cổng nguồn, cách đăng nhập, dạng dữ liệu và luồng thông tin đều khác.

| | Nhóm Hóa đơn (đã có) | Nhóm Dịch vụ công (mới) |
|---|---|---|
| Cổng | `hoadondientu.gdt.gov.vn` | `dichvucong.gdt.gov.vn/tthc/` |
| Giao tiếp | REST JSON, JWT | Ứng dụng htmx: form POST/GET trả mảnh HTML, phiên cookie |
| Đăng nhập | `POST /api/security-taxpayer/authenticate` (JWT) | `POST /tthc/loginLDAP` (cookie) |
| Dữ liệu | Hóa đơn mua/bán, theo tháng, lưu kho SQLite + XML | Thủ tục hành chính, hồ sơ nộp/lưu tạm, phản ánh, thông báo LPTB; chủ yếu tra cứu trực tiếp |
| Tài liệu | `docs/requirements`, `docs/detail-design`, `docs/decisions`... | **Thư mục này** |

## Chọn nhóm chức năng khi mở phần mềm

Sau khi qua kiểm tra bản quyền (và chọn doanh nghiệp), người dùng chọn một trong hai nhóm: **Tra cứu hóa đơn** hoặc **Tra cứu Dịch vụ công thuế nhà nước**. Chi tiết ở [FT-DVC-00](requirements/FEATURE-LIST.md). Hai nhóm không dùng chung phiên đăng nhập, kho dữ liệu hay hàng đợi tải.

## Nội dung thư mục

| Tài liệu | Nội dung |
|---|---|
| [requirements/SRS-DVC.md](requirements/SRS-DVC.md) | Đặc tả yêu cầu của nhóm (bản nháp 0.2, đã chốt 7 câu hỏi mở; còn các cổng kỹ thuật G-DVC) |
| [requirements/FEATURE-LIST.md](requirements/FEATURE-LIST.md) | Danh sách tính năng `FT-DVC-xx` |
| [detail-design/](detail-design/) | Thiết kế chi tiết `DD-DVC-xx` (nháp): [DD-DVC-00 chọn nhóm](detail-design/DD-DVC-00-GROUP-CHOOSER.md), [DD-DVC-01 đăng nhập](detail-design/DD-DVC-01-LOGIN.md) |
| [decisions/](decisions/) | ADR của nhóm, đánh số riêng `DVC-NNNN` |
| [api-samples/khao-sat-tthc.md](api-samples/khao-sat-tthc.md) | Khảo sát cổng ngày 16/11/2026: chức năng, request, luồng đăng nhập |
| [wireframes/](wireframes/) | Ảnh chụp giao diện cổng làm mockup: `DVC-xx-*.jpg` là bản **đã che tên doanh nghiệp và MST** (commit được); `*.raw.jpg` là bản gốc, git bỏ qua. Mã `xx` khớp `FT-DVC-xx` |

Chưa có: DD-DVC-02 trở đi, kế hoạch phase.

## Quy tắc áp dụng chung (từ CLAUDE.md)

- Kiểm thử chỉ bằng dữ liệu giả; chỉ gọi cổng thật ở bản Release hoặc khi người dùng tự đăng nhập lần đầu.
- Không giải CAPTCHA, không lưu/ghi nhật ký mật khẩu, token, cookie; chỉ commit mẫu đã làm sạch.
- Khi khảo sát cổng DVC bằng trình duyệt: cổng chỉ cho **một tab** mỗi tài khoản, không mở cổng ở hai tab.
