# ADR 0008 — An toàn khi chuyển doanh nghiệp (MST): thứ tự thao tác và phục hồi (G-07)

| | |
|---|---|
| Trạng thái | **Accepted một phần** — thứ tự và phục hồi lỗi mở kho đã có test; chưa có spike giết tiến trình giữa lúc chuyển (xem "Chưa kiểm") |
| Ngày | 03/11/2026 |
| Gate | G-07 Company switch safety ([DD-00](../detail-design/DD-00-TECHNICAL-GATES.md)) |
| Liên quan | [DD-01](../detail-design/DD-01-APP-SHELL-LOGIN.md) mục 9, [DD-02](../detail-design/DD-02-COMPANY-MANAGEMENT.md), [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) (T05-07, T05-09), [ADR 0006](0006-sqlite-provider.md) |

## Bối cảnh

Mỗi MST có kho và `company.db` riêng; phiên cổng Thuế gắn với một MST. Chuyển MST sai thứ tự có thể để một phiên tải ghi dữ liệu của MST này vào kho MST kia,
để token của MST này dùng cho MST kia, hoặc mở hai `company.db` cùng lúc. G-07 yêu cầu chứng minh thứ tự đóng tác vụ, token và SQLite, và việc phục hồi sau sự cố.

## Quyết định (đã cài đặt trong `CompanySwitchService`)

Thứ tự bắt buộc, trong một cổng `SemaphoreSlim` nên chỉ một lần chuyển chạy tại một lúc:

1. Mục tiêu trùng MST đang mở → không làm gì (`Unchanged`).
2. Có phiên tải đang chạy → bắt buộc người dùng chọn chờ, tạm dừng (ghi checkpoint) hoặc hủy; không chọn thì `Blocked`, chưa đổi gì.
3. Xóa phiên cổng Thuế (`IPortalSessionStore.Clear`) **trước** khi rời MST hiện tại.
4. Đóng `CompanyContext` hiện tại (đóng trước khi mở kho mới; chỉ một `company.db` được dùng tại một lúc).
5. Mở kho đích; `CompanyContext` chỉ được gán sau khi mở thành công (kiểm thư mục, `company.db`, schema, `integrity_check`).
6. Mở đích lỗi → mở lại MST trước (`RestoredPrevious`); cả hai lỗi → `RecoveryRequired`, shell vào màn hình khôi phục và dữ liệu giữ nguyên.

Mọi repository lấy `company.db` qua `CompanyDatabaseProvider.Current()` theo `CompanyContext` hiện tại, nên một phiên tải đang chạy của MST cũ không thể ghi sang
MST mới: engine kiểm `session.CompanyId == CompanyContext.CompanyId` mỗi lần chạy và từ chối khi lệch (`StorageGuardException`).

Sau sự cố (tắt tiến trình hoặc mất điện giữa lúc tải): phiên còn ghi "đang chạy" được `InterruptedSessionRecovery` đưa về tạm dừng mỗi khi mở một doanh nghiệp,
checkpoint ghi sau từng hóa đơn nên Tiếp tục chạy từ đúng chỗ, khóa chống trùng ngăn bản ghi trùng.

## Bằng chứng

- `CompanySwitchServiceTests`: thứ tự đóng cũ-trước-mở-mới và xóa phiên trước; chặn khi đang tải mà chưa chọn cách xử lý; tạm dừng/hủy được gọi trước khi chuyển;
  đích mở lỗi → khôi phục MST trước; cả hai lỗi → `RecoveryRequired`; chuyển về chính MST hiện tại không có tác dụng phụ; chế độ Standard không chuyển giữa các MST.
- `DownloadOrchestratorTests`: guard MST/context, hai phiên cùng doanh nghiệp không chạy song song, phiên bị ngắt chạy tiếp từ checkpoint không tải lại hóa đơn đã xong,
  `RecoverInterruptedAsync` (gồm trường hợp đua với lượt Tiếp tục).
- `SqliteRepositoryTests`/`CompanyDatabaseInitializerTests`: mở lại giữa chừng giữ nguyên dữ liệu; migration lỗi quay về phiên bản trước; spike SQLite cho thấy giết
  tiến trình giữa giao dịch không làm hỏng cơ sở dữ liệu ([ADR 0006](0006-sqlite-provider.md)).

## Chưa kiểm

- **Cập nhật 08/11/2026:** `SwitchKilledProcessTests` giết một tiến trình thật đang giữ giao dịch ghi dở trên `company.db` rồi mở lại và chuyển qua lại: cả hai kho nguyên vẹn, giao dịch dở bị bỏ. Chuyển MST không ghi trạng thái "kho đang hoạt động" nào xuống đĩa nên không có điểm dở dang. Phần dưới là ghi chú cũ, còn đúng cho điểm tắt giữa các bước trong bộ nhớ.
- Chưa có spike giết tiến trình thật **đúng lúc đang chuyển MST** (giữa bước 3-5) rồi mở lại; các test hiện dùng đối tượng giả cho kho/phiên. Rủi ro đã giảm vì mỗi bước
  hoặc là bộ nhớ (xóa phiên) hoặc là giao dịch SQLite độc lập, nên tắt ở bất kỳ điểm nào đều để lại hai kho nguyên vẹn; vẫn nên kiểm một lần khi có bản chạy thật (UAT).
- Chưa kiểm chuyển MST khi WebView đăng nhập đang mở (phụ thuộc [ADR 0007](0007-webview-cefsharp.md): phải xóa cache/cookie của `RequestContext` ở bước 3).
