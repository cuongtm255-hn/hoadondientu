# DD-05 — Hàng đợi tải, phân trang, checkpoint và màn hình tiến độ (SCR-04)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-05 |
| Tên | `DownloadSession`/`DownloadSegment`, orchestrator, portal invoice client, retry, token hết hạn, SCR-04 |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review; payload API chờ gate G-02, G-05 |
| Feature | [FT-06](../requirements/FEATURE-LIST.md#ft-06--hàng-đợi-tải-phân-trang-và-checkpoint) |
| SRS | FR-JOB-001..014, FR-AUTH-007..008, BR-006..007, BR-011..012, NFR-REL-002, NFR-REL-005, NFR-MNT-004, mục 9.3, 11.1, 13, AC-02, AC-04, AC-06, AC-07, AC-15 |
| Màn hình | SCR-04 |
| Wireframe | [SCR-04](../wireframes/index.html?screen=SCR-04) · [PNG](../wireframes/png/SCR-04-progress.png) |
| Phụ thuộc | [DD-00](DD-00-TECHNICAL-GATES.md) (G-02, G-05), [DD-01](DD-01-APP-SHELL-LOGIN.md) (session, context, login), [DD-04](DD-04-PERIOD-DOWNLOAD-CONFIG.md), [DD-06](DD-06-INVOICE-STORAGE.md) |
| Phase triển khai | [Phase 3](../plans/PROJECT-PLAN.md#phase-3--tải-hóa-đơn-end-to-end) |

## 2. Mục đích và phạm vi

Thực thi `DownloadPlan` tuần tự theo từng đoạn tháng, tự lặp trang/cursor/batch đến hết dữ liệu, ghi checkpoint, và cho phép tạm dừng, tiếp tục, hủy, thử lại. Không chạy song song nhiều tháng (FR-JOB-002) và không chạy cho MST không hoạt động (FR-JOB-014).

Ngoài phạm vi: chuẩn hóa và ghi file (DD-06); chọn kỳ (DD-04).

## 3. Mô hình và trạng thái

### 3.1 State machine phiên (SRS 9.3)

```text
Draft → Running → Completed
              ├→ CompletedWithErrors
              ├→ PausedByUser → Running
              ├→ WaitingForLogin → Running
              ├→ Cancelled
              └→ Failed        (chỉ khi không thể tiếp tục / dữ liệu-cấu hình không hợp lệ)
```

Lỗi mạng còn khả năng thử lại và hết token **không** chuyển sang `Failed` (mục 13 SRS).

### 3.2 State machine đoạn

```text
Pending → Running → Completed
                ├→ Failed (có thể Retry)
                ├→ Paused
                └→ Skipped(Cancelled)
```

Một đoạn chỉ `Completed` khi API báo hết trang/cursor **và** mọi hóa đơn hợp lệ đã ghi nhận (BR-007).

### 3.3 Thực thể

| Thực thể | Trường chính |
|---|---|
| `DownloadSession` | `SessionId`, `CompanyId`, `TaxCode`, `StorageRoot` (gắn tại thời điểm tạo — FR-JOB-013), `Direction`, `PeriodSpec`, `Filter`, `State`, `StartedAt`, `FinishedAt`, `StartedByWindowsUser` |
| `DownloadSegment` | `SegmentId`, `SessionId`, `Start`, `End`, `State`, `Cursor`/`PageIndex`, `ReceivedCount`, `XmlSavedCount`, `ErrorCount`, `RetryCount` |
| `DownloadEvent` | `EventId`, `SessionId`, `SegmentId?`, `Time`, `Level`, `Code`, `Message` (đã che dữ liệu nhạy cảm), `CorrelationId` |

## 4. Interface

```csharp
public interface IDownloadOrchestrator
{
    Task<SessionId> CreateSessionAsync(DownloadPlan plan, CancellationToken ct);
    Task RunAsync(SessionId id, IProgress<DownloadProgress> progress, CancellationToken ct);
    Task PauseAsync(SessionId id, CancellationToken ct);
    Task ResumeAsync(SessionId id, CancellationToken ct);
    Task CancelAsync(SessionId id, CancellationToken ct);
    Task RetrySegmentAsync(SegmentId id, CancellationToken ct);
    Task<IList<DownloadSessionSummary>> GetUnfinishedAsync(CancellationToken ct);
}

// Adapter cổng Thuế — tầng Application chỉ biết các DTO này. Hóa đơn được định danh bằng khóa tổng hợp
// (nbmst, khhdon, shdon, khmshdon); xuất XML trả file zip, adapter chỉ trả invoice.xml (api-samples mục 4)
public interface IPortalInvoiceClient
{
    Task<InvoicePage> SearchAsync(InvoiceSearchRequest request, CancellationToken ct); // direction, DateSegment, filter, cursor/page
    Task<InvoiceXmlPayload> DownloadXmlAsync(PortalInvoiceRef invoice, CancellationToken ct);
}

public interface IRetryPolicy { /* tối đa 3 lần, thời gian chờ tăng dần (FR-JOB-008) */ }
public interface IRateLimiter { Task WaitAsync(CancellationToken ct); }   // cấu hình ở Infrastructure (FR-JOB-012)
```

Phân loại lỗi từ `IPortalInvoiceClient`: `AuthExpired` (401/403), `Transient` (timeout, 5xx), `RateLimited` (429), `SchemaChanged`, `FilterRejected`, `Fatal`.

## 5. Luồng thực thi

```text
RunAsync(session)
  guard: Command/Session.CompanyId == CompanyContext.CompanyId, PortalSession.TaxCode == TaxCode, entitlement NewDownload
  foreach segment (tăng dần, bỏ qua Completed):
      segment.Running
      loop:
          rateLimiter.WaitAsync
          page = client.SearchAsync(segment, cursor)     // retry theo IRetryPolicy
          foreach invoice in page:
              storage.UpsertAsync(invoice, xml)           // DD-06: file tạm → hash → commit
          checkpoint(segment: cursor, counts)             // sau mỗi trang & mỗi hóa đơn thành công (FR-JOB-006)
          break khi total/next-cursor/kích thước trang báo hết (FR-JOB-004)
      segment.Completed
  session = có segment Failed ? CompletedWithErrors : Completed
```

Quy tắc:

- Số lượt gọi không hard-code theo tháng; dựa vào `total`/`nextCursor`/page size đã xác minh (FR-JOB-004).
- Idempotency: tiếp tục từ checkpoint không tạo bản ghi trùng (NFR-REL-002, nhờ khóa chống trùng DD-06).
- **Hết token** (`AuthExpired`): đặt session `WaitingForLogin`, giữ checkpoint, gọi `IPortalLoginCoordinator.LoginAsync` với `ReturnScreen = DownloadProgress`; đăng nhập xong cho phép `Resume` (FR-AUTH-007/008, BR-012).
- **Lỗi tạm thời**: thử lại tối đa 3 lần, chờ tăng dần; hết lượt → segment `Failed`, hiển thị `Thử lại ngay` (FR-JOB-008).
- **Schema thay đổi**: dừng đoạn, lưu thông tin kỹ thuật đã làm sạch, không ghi metadata sai (mục 11.1 SRS).
- **Hết dung lượng / không có quyền ghi**: `PausedByUser`-tương đương trạng thái chờ xử lý, không mất checkpoint.
- **Hủy**: dừng an toàn sau hóa đơn hiện tại; dữ liệu đã lưu giữ nguyên (BR-011).
- **Đóng ứng dụng / chuyển MST**: dùng `PauseAndSwitch`/`CancelAndSwitch` của [DD-01 mục 9](DD-01-APP-SHELL-LOGIN.md#9-companycontext-và-chuyển-mst); checkpoint ghi trước khi đóng SQLite (AC-15).
- Mở lại ứng dụng: `GetUnfinishedAsync` trả các phiên chưa hoàn tất với hành động `Tiếp tục` (FR-JOB-010).

## 6. Màn hình SCR-04 `DownloadProgressControl`

- Phần đầu: loại, kỳ, thời điểm bắt đầu, trạng thái phiên; nút `Tạm dừng` / `Tiếp tục` / `Hủy phiên`.
- Bảng đoạn: tháng, trạng thái (chữ + biểu tượng, không chỉ màu), trang/batch hiện tại, số hóa đơn đã nhận, số XML đã lưu, số lỗi, hành động `Thử lại` cho đoạn lỗi (FR-JOB-005, 009).
- Khi hoàn tất: nút `Mở Kho hóa đơn` với bộ lọc cùng loại và phạm vi (FR-JOB-011).
- Chế độ chi tiết lịch sử (từ SCR-08): cùng control, chỉ đọc nếu phiên đã kết thúc.
- Cập nhật tiến độ qua `IProgress<DownloadProgress>` (marshal về UI thread bởi `UiOperationRunner`); không treo UI (NFR-PERF-001).

## 7. Kiểm thử

### 7.1 Unit

- State machine session/segment: chuyển hợp lệ và không hợp lệ.
- Orchestrator với `IPortalInvoiceClient` giả: nhiều trang; kỳ rỗng; trang cuối; chạy tuần tự đúng thứ tự.
- Checkpoint và resume không tạo bản ghi trùng; không tải lại đoạn `Completed` (AC-06).
- Retry 3 lần với backoff; 429; lỗi `SchemaChanged` dừng đoạn.
- `AuthExpired` → `WaitingForLogin` → resume sau đăng nhập.
- Context/MST không khớp → từ chối (FR-JOB-013, 014).
- `Completed` vs `CompletedWithErrors` (NFR-REL-005).

### 7.2 Integration

- Với mẫu request/response đã làm sạch từ G-02: parse, pagination, 401/403, timeout, 5xx.
- Mất điện/đóng tiến trình giữa trang → mở lại thấy phiên chưa hoàn tất, dữ liệu đã commit vẫn truy vấn được (AC-07).

### 7.3 UAT trên cổng thật

- Mua vào/bán ra có dữ liệu; kỳ không dữ liệu; nhiều trang trong một tháng; đăng nhập lại giữa phiên.

## 8. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 8.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T05-01 | Domain: `DownloadSession`, `DownloadSegment`, state machine, `DownloadEvent` | Domain | T01-02 | ✅ | | Enum trạng thái + `DownloadStateMachine` (Domain); `DownloadSession`/`DownloadSegment`/`DownloadEvent` ở Application vì giữ `InvoiceSearchFilter` |
| T05-02 | Repository interface + SQLite bảng session/segment/event | Application/Infra | T06-02 | ✅ | | `SqliteDownloadRepository` (phiên, đoạn có `check_result`, sự kiện; `ON CONFLICT DO UPDATE` để không cascade xóa đoạn); `DownloadSession.Restore`, `DownloadSegment.RestoreState`, `DownloadEvent.Restore`; 22 test `SqliteRepositoryTests` gồm mở lại giữa chừng và luồng tải trên cổng giả |
| T05-03 | `DownloadOrchestrator` (tuần tự, checkpoint, pause/resume/cancel) với client giả | Application | T05-01, T05-02 | ✅ | | `DownloadOrchestrator`: tuần tự theo đoạn, cursor/trang, checkpoint sau từng hóa đơn, pause/cancel/retry đoạn, chạy lại từ checkpoint không trùng |
| T05-04 | `IRetryPolicy`, `IRateLimiter` (cấu hình hạ tầng) | Infra | T05-03 | 🟡 | | `IRetryPolicy` + `DefaultRetryPolicy` (3 lần, gấp đôi mỗi lần, theo Retry-After khi 429) và `IRateLimiter`/`MinIntervalRateLimiter`; giá trị mặc định chờ G-05 |
| T05-05 | Xử lý `AuthExpired` → `WaitingForLogin` → login lại → resume | Application | T01-08, T05-03 | ✅ | | Hết token hoặc chưa đăng nhập → `WaitingForLogin`; SCR-04 báo rõ, nút Tiếp tục mở đăng nhập (`LoginReason.TokenExpired`) rồi chạy tiếp từ checkpoint, không tải lại phần đã xong (test AC-06 trên cổng giả trả 401) |
| T05-06 | `PortalInvoiceClient` thật (search, tải XML, phân loại lỗi) | Infra | G-02, T01-09 | ✅ | | `PortalHttpApiClient` đã **khớp traffic thật** cho đăng nhập/bán ra/chi tiết/xuất XML: tra cứu RSQL (`search=tdlap=ge=…;…`), `datas/total/state`, ngày UTC→giờ VN, khóa tổng hợp, zip → `invoice.xml`, header `action/request-id/end-point/cookie/user-agent`, `GetInvoiceDetailAsync`; cổng giả mô phỏng đúng hình dạng đó. ⚠ còn: mua vào, `state`, `size` tối đa (xem [api-samples](../api-samples/README.md)) |
| T05-07 | Tích hợp chuyển MST/đóng ứng dụng (pause + checkpoint) | Application | T01-07, T05-03 | ✅ | | `DownloadJobService : IActiveJobService` đã nối vào `ShutdownCoordinator` và chuyển MST trong Debug (thay `DevelopmentActiveJobService`, trừ khi đặt `HDDT_DEV_JOB=1`); rời SCR-04 hoặc đóng màn hình hủy token → engine ghi checkpoint, phiên ở trạng thái tạm dừng |
| T05-08 | `DownloadProgressControl` + presenter (SCR-04) | WinForms | T05-03 | ✅ | | `DownloadProgressControl` + `DownloadProgressPresenter` + `DownloadProgressScreen` (SCR-04): 4 thẻ số liệu, tiến độ tổng, bảng theo tháng, Tạm dừng/Tiếp tục/Hủy phiên, Thử lại đoạn/các lỗi; engine báo tiến độ từ luồng nền nên view chuyển về UI thread. SCR-03 chỉ tạo phiên (`IDownloadSessionStarter` trả mã phiên) rồi mở SCR-04 chạy; 14 test presenter + 2 smoke test chạy toàn tuyến trên cổng giả |
| T05-09 | Khôi phục phiên chưa hoàn tất khi khởi động | Application | T05-02 | 🟡 | | `GetUnfinishedAsync` + phiên bị ngắt chạy tiếp được; SCR-04 mở phiên chưa xong mới nhất khi không có mã phiên. Chưa gọi lúc khởi động ứng dụng và chưa có nơi liệt kê phiên (SCR-08) |
| T05-10 | Unit test (mục 7.1) | Tests | T05-03..T05-05 | ✅ | | 76 test (state machine, nhiều trang, retry/backoff, 429, schema, auth, pause/resume, cancel, kho đầy, guard, tuần tự) |
| T05-11 | Integration test + UAT cổng thật (mục 7.2–7.3) | Tests | T05-06 | ⛔ | | Integration + UAT cổng thật (mục 7.2–7.3): mẫu bán ra đã có để làm test (RealSamples); còn cần traffic mua vào, nhiều trang và 401/429 thật |
| T05-12 | Tra cứu mua vào bắt buộc `ttxly`: khi bộ lọc không chọn kết quả kiểm tra thì mỗi tháng tải lần lượt ba mã 5, 6, 8 (đoạn tải thêm chiều "kết quả kiểm tra", checkpoint theo từng lượt), gộp và khử trùng theo khóa chống trùng; cấu hình `search.requireCheckResult` cho mua vào; cổng giả mô phỏng bắt buộc `ttxly`; thêm cột tương ứng vào `download_segment` trước T06-03 | Application/Infra | T04-10, T05-03 | ✅ | | `DownloadSegment.CheckResultCode`; `CreateSessionAsync` mở rộng mua vào không chọn kết quả thành 3 đoạn/tháng (5, 6, 8), search dùng mã của đoạn, tiến độ ghi nhãn kết quả; cột `check_result` trong `download_segment`; cổng giả bắt buộc `ttxly` và dữ liệu mua vào chỉ 5/6/8. Không làm cờ cấu hình `requireCheckResult` (quy tắc nằm ở `PortalOptions`, đổi một chỗ khi xác nhận bỏ được `ttxly`). Chưa kiểm trên cổng thật |

### 8.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 9. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
