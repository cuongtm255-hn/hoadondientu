# 02/11/2026 — Đối chiếu tra cứu hóa đơn mua vào với mẫu thật

Nguồn: `docs/api-samples/sample-api-web.md` (mục 2 bán ra, mục 4 mua vào) và ảnh form mua vào trong `docs/api-samples/spec.xlsx`.
Mục tiêu: sửa tài liệu và tính năng cho đúng hai loại tra cứu. Phần tài liệu đã sửa trong lượt này; phần mã là kế hoạch bên dưới.

## 1. Khác biệt giữa mua vào và bán ra

| | Bán ra (`/api/query/invoices/sold`) | Mua vào (`/api/query/invoices/purchase`) |
|---|---|---|
| Đối tác | MST người mua `nmmst` | MST **người bán** `nbmst` (URL mẫu + nhãn form; bản ghi tay 4.2 ghi nhầm "người mua/nmmst") |
| CCCD người mua `nmcmnd` | có | **có** (trước đây giả định không có) |
| Trạng thái `tthai` | Tất cả, 1…6 | Tất cả, 1…6 (mặc định form: Hóa đơn mới) |
| Kết quả kiểm tra `ttxly` | Tất cả, 0…8 | **chỉ 5, 6, 8; không có Tất cả; bắt buộc (\*)** |
| `action` | `Tìm kiếm (hóa đơn bán ra)` | `Tìm kiếm (hóa đơn mua vào)` (đã xác nhận) |
| Còn lại (`tdlap`, `khmshdon`, `shdon`, `khhdon`, `unhiem`) | giống nhau | giống nhau |

Mục 2 (bán ra) trong file mới chỉ đổi tiêu đề; tham số không đổi. Các chỗ ghi tay sai: mục 2.8/4.8 viết `hdon==` (thật là `shdon==`), mục 4.2 viết `nmmst`, mục "4" bị trùng số với "4. API In hóa đơn".

## 2. Việc đã làm trong lượt này

- `sample-api-web.md`: thay token JWT và cookie thật ở mục 4 bằng chỗ giữ chỗ như các mục khác (file này được commit; token là thật, hết hạn 02/11/2026 15:26 UTC).
- `api-samples/README.md`: mục 3 thêm khác biệt mua vào, bỏ ⚠ cho đường dẫn/`nbmst`/`nmcmnd`/`action`; mục 7 chỉ còn phần chưa biết.
- ADR 0001, DD-04 (mục 5.1a, T04-10), DD-05 (T05-12), `PROJECT-PLAN.md`.

## 3. Hạng mục cần sửa (chưa làm, theo thứ tự)

| # | Hạng mục | Nơi sửa | Task |
|---|---|---|---|
| 1 | Cấu hình: `PurchaseFilterFields` thêm `BuyerIdNumber → nmcmnd`; ghi chú `Notes`, `portal-api.config.sample.json` (có test khớp mặc định); thêm cờ `search.requireCheckResult` cho mua vào | `PortalApiConfig*`, `portal-api.config.sample.json` | T04-10 |
| 2 | `PortalOptions.CheckResultsFor(direction)`: bán ra 10 mục, mua vào 3 mục (5/6/8) không có Tất cả; `InvoiceFilterValidator.Validate(filter, direction)` báo mua vào chỉ nhận 5/6/8 | `PortalOptions`, `InvoiceFilterValidator` | T04-10 |
| 3 | SCR-03: hiện CCCD người mua ở cả hai chiều; dropdown Kết quả kiểm tra đổi theo chiều (mua vào thêm lựa chọn "Cả ba loại" làm mặc định); nhãn thẻ bộ lọc | `DownloadConfigControl/Presenter` | T04-10 |
| 4 | Engine: mua vào không chọn kết quả kiểm tra thì mỗi tháng chạy ba lượt `ttxly` 5, 6, 8; đoạn tải có thêm khóa lượt, checkpoint từng lượt, khử trùng theo khóa chống trùng, tiến độ hiển thị đúng số đoạn | `DownloadPlan`, `DownloadSegment`, `DownloadOrchestrator`, `DownloadProgress` | T05-12 |
| 5 | Schema SQLite: thêm cột kết quả kiểm tra vào `download_segment` và khóa duy nhất `(session_id, segment_index)` vẫn đủ; sửa trực tiếp migration v1 vì chưa phát hành | `CompanyDatabaseSchema` | T05-12, trước T06-03 |
| 6 | Cổng giả: mua vào bắt buộc `ttxly` (thiếu thì 400 giả lập), dữ liệu mua vào chỉ có `ttxly` 5/6/8, có `nmcmnd`; ghi rõ MOCK như hiện nay | `MockPortalHandler`, `MockInvoiceDataset` | T05-12 |
| 7 | Test: request builder (mua vào có `nmcmnd`, `ttxly`), validator theo chiều, kế hoạch ba lượt, resume giữa lượt, khử trùng, UI SCR-03 đổi dropdown theo chiều | tests | T04-10, T05-12 |
| 8 | Kho hóa đơn (SCR-05) lọc dữ liệu đã lưu nên giữ đủ 9 nhãn; chỉ kiểm tra không có chỗ nào dùng nhầm danh sách mua vào | `ArchivePresenter` | rà soát |

## 4. Câu hỏi mở (cần traffic thật hoặc người có tài khoản mua vào)

1. Gọi mua vào không có `ttxly` có được không? Nếu được, bỏ ba lượt và dùng Tất cả (đóng T05-12 rất gọn). Cho tới đó coi là bắt buộc.
2. Ba mã 5/6/8 có phủ mọi hóa đơn mua vào không; hai tab *Hóa đơn điện tử* và *Hóa đơn có mã khởi tạo từ máy tính tiền* có dùng cùng endpoint không (nhiều khả năng tab sau là `ttxly==8`).
3. Mua vào có `state`/phân trang giống bán ra không (vẫn ⚠ từ trước).

Việc phải làm ngoài mã: token trong lần ghi traffic này lộ trong file làm việc; nếu chưa đăng xuất thì nên đăng xuất khỏi cổng để vô hiệu phiên.
