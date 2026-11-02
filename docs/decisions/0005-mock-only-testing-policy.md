# ADR 0005 — Chỉ kiểm thử trên dữ liệu giả; gọi cổng Thuế thật chỉ ở bản Release

| | |
|---|---|
| Trạng thái | **Accepted** |
| Ngày | 02/11/2026 (chủ sản phẩm yêu cầu; đã ghi trong `CLAUDE.md`) |
| Liên quan | [CLAUDE.md](../../CLAUDE.md), [ADR 0001](0001-portal-api-contract.md), [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) |

## Bối cảnh

Ứng dụng đọc dữ liệu hóa đơn điện tử thật của doanh nghiệp từ cổng Thuế và đăng nhập có CAPTCHA, phiên có token. Gọi cổng thật khi phát triển hoặc chạy test có nguy cơ làm
lộ thông tin đăng nhập hoặc dữ liệu thuế, vượt giới hạn tốc độ, làm khóa tài khoản và cho kết quả không lặp lại được. Trong lịch sử dự án đã có một lần bản ghi traffic chứa mật khẩu/token bị commit và phải viết lại lịch sử git.

## Quyết định

1. **Phát triển, test, spike và demo chỉ dùng dữ liệu giả** (`MockPortalHandler`, `MockInvoiceDataset`, các adapter phát triển). API thật chỉ chạy trong bản **Release** do người dùng thật đăng nhập.
2. **Dữ liệu giả phải hiện rõ là giả**: bản Debug luôn có nhãn `[TEST] DỮ LIỆU GIẢ` trên tiêu đề và dải thông báo trên cùng; dữ liệu mẫu ghi chú "mẫu"/"(mô phỏng)"; test gắn tên/fixture nêu rõ dữ liệu giả.
3. **Mã giả chỉ biên dịch ở Debug** (`#if DEBUG`) và không với tới được từ Release.
4. **Ngoại lệ duy nhất**: thử tính năng đăng nhập lần đầu — người dùng tự nhập tài khoản và CAPTCHA, không tự động hóa, không gọi API hóa đơn/tra cứu/tải, không lưu hay ghi log bí mật.
5. Không đưa traffic, token, mật khẩu, cookie thật vào repo. Bản ghi nguyên văn đặt tên `*.raw.*` (bị `.gitignore`); chỉ commit bản đã làm sạch (như `sample-api-web.md`); quét diff trước mỗi commit.

## Hệ quả

- (+) Test lặp lại được, không rủi ro tài khoản thật, không lộ bí mật qua repo.
- (−) Chưa có bằng chứng chạy trên cổng thật cho tra cứu mua vào, phân trang `state`, 401/429 (G-02, G-05) cho tới khi có UAT (T05-11, P6-01), chỉ có thể thực hiện theo ngoại lệ hoặc ở bản Release do người dùng chạy.
- (−) Cổng giả chỉ mô phỏng hành vi đã biết; thay đổi phía cổng thật không bị test phát hiện.
- Kiểm tra bắt buộc trong P6-04: bản Release không chứa mã giả hay bí mật.
