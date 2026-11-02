# DD-04 — Chọn kỳ, bộ lọc và kế hoạch tải (SCR-03)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-04 |
| Tên | Chọn loại hóa đơn, kỳ (tháng/quý/năm/tùy chọn), bộ lọc, chia đoạn tháng và xem trước kế hoạch |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review |
| Feature | [FT-05](../requirements/FEATURE-LIST.md#ft-05--chọn-kỳ-và-lập-kế-hoạch-tải) |
| SRS | BR-002..BR-006, FR-SEARCH-001..010, AC-02, AC-03 |
| Màn hình | SCR-03 |
| Wireframe | [SCR-03](../wireframes/index.html?screen=SCR-03) · [PNG](../wireframes/png/SCR-03-download.png) |
| Phụ thuộc | [DD-01](DD-01-APP-SHELL-LOGIN.md) (shell, session), [DD-05](DD-05-DOWNLOAD-ENGINE.md) (tạo phiên tải) |
| Phase triển khai | [Phase 3](../plans/PROJECT-PLAN.md#phase-3--tải-hóa-đơn-end-to-end) |

## 2. Mục đích và phạm vi

Định nghĩa logic thuần (không phụ thuộc UI) chia kỳ thành các đoạn không vượt ranh giới tháng, cùng màn hình SCR-03 để người dùng cấu hình và xem trước. Cổng Thuế giới hạn một lần tra cứu trong một tháng, nên toàn bộ chia kỳ là trách nhiệm của `PeriodSplitter` (NFR-MNT-001).

Ngoài phạm vi: thực thi tải (DD-05), lưu dữ liệu (DD-06).

## 3. Mô hình miền

```csharp
public enum InvoiceDirection { Purchase, Sold }              // Mua vào, Bán ra
public enum PeriodKind { Month, Quarter, Year, Custom }

public sealed class PeriodSpec
{
    public PeriodKind Kind { get; private set; }
    public int Year { get; private set; }
    public int? Month { get; private set; }       // Kind = Month
    public int? Quarter { get; private set; }     // Kind = Quarter
    public DateTime? From { get; private set; }   // Kind = Custom (bao gồm)
    public DateTime? To { get; private set; }     // Kind = Custom (bao gồm)
}

public sealed class DateSegment                    // một đoạn trong 1 tháng
{
    public DateTime Start { get; private set; }
    public DateTime End { get; private set; }
    public int Year { get { return Start.Year; } }
    public int Month { get { return Start.Month; } }
}

public sealed class InvoiceSearchFilter          // theo form "Tra cứu hóa đơn" của cổng; rỗng = "Tất cả", không gửi
{
    public string CounterpartyTaxCode { get; set; } // bán ra: MST người mua (nmmst); mua vào: MST người bán (nbmst) ⚠
    public string BuyerIdNumber { get; set; }       // CCCD người mua (nmcmnd) — chỉ bán ra
    public string StatusCode { get; set; }          // tthai "1".."6"
    public string CheckResultCode { get; set; }     // ttxly "0".."8"
    public string InvoiceNumber { get; set; }       // shdon, tối đa 8 chữ số
    public string FormSymbol { get; set; }          // khmshdon "1".."9" (dropdown)
    public string InvoiceSymbol { get; set; }       // khhdon, ví dụ C26TBN
    public bool? ProxyInvoice { get; set; }         // unhiem==1 — chỉ gửi khi tích
}

public sealed class DownloadPlan
{
    public InvoiceDirection Direction { get; private set; }
    public PeriodSpec Period { get; private set; }
    public InvoiceSearchFilter Filter { get; private set; }
    public IList<DateSegment> Segments { get; private set; }
}
```

Các trường của `InvoiceSearchFilter` chỉ hiển thị nếu API đã xác minh (G-02) hỗ trợ (FR-SEARCH-007).

## 4. Thuật toán chia kỳ

```csharp
public interface IPeriodSplitter
{
    PeriodValidationResult Validate(PeriodSpec spec);
    IList<DateSegment> Split(PeriodSpec spec);   // tăng dần theo thời gian
}
```

Quy tắc:

1. Chuẩn hóa mọi kiểu kỳ về khoảng `[From, To]` (đều tính cả hai đầu — BR-002):
   - `Month`: ngày 1 → ngày cuối tháng (kể cả 29/02 năm nhuận — FR-SEARCH-005).
   - `Quarter`: Q1 01/01–31/03, Q2 01/04–30/06, Q3 01/07–30/09, Q4 01/10–31/12 (BR-004).
   - `Year`: 01/01–31/12.
   - `Custom`: giá trị người dùng nhập; từ chối nếu `To < From` (FR-SEARCH-004).
2. Chia theo ranh giới tháng: đoạn đầu từ `From` đến cuối tháng chứa `From`; các đoạn giữa đủ tháng; đoạn cuối từ ngày 1 đến `To`. Mỗi đoạn không vượt qua ranh giới tháng (BR-003, BR-005).
3. Chỉ dùng `DateTime.Date`; không phụ thuộc múi giờ.
4. Thứ tự đoạn tăng dần (BR-006).

Ví dụ AC-03: `15/01/2026–10/03/2026` → `15/01–31/01`, `01/02–28/02`, `01/03–10/03`.

Giới hạn hợp lý: khoảng tùy chọn tối đa 10 năm (cấu hình), vượt quá hiển thị cảnh báo thay vì chạy ngầm.

## 5. Màn hình SCR-03

### 5.1 Control

```text
DownloadConfigControl
├── rdoPurchase / rdoSold              Loại hóa đơn
├── cboPeriodKind                      Tháng | Quý | Năm | Tùy chọn
├── pnlPeriodInputs                    đổi theo kiểu kỳ (tháng+năm | quý+năm | năm | từ ngày–đến ngày)
├── grpFilters                         bộ lọc theo tham số của cổng (bảng 5.1a), theo chiều; nút Xóa bộ lọc
├── gridPlan                           kế hoạch: STT, từ ngày, đến ngày
├── lblStorageCheck                    trạng thái thư mục lưu có ghi được
└── btnStartDownload                   hành động chính
```

### 5.1a Bộ lọc theo tham số của cổng (đã đối chiếu `spec.xlsx` và traffic thật)

| Ô trên SCR-03 | Trường | Tham số cổng | Kiểu nhập / giá trị | Chiều |
|---|---|---|---|---|
| MST người mua / MST người bán (nhãn đổi theo chiều) | `CounterpartyTaxCode` | `nmmst` / `nbmst` ⚠ | text; 10 số, 13 số hoặc `10 số-3 số` | cả hai |
| CCCD người mua | `BuyerIdNumber` | `nmcmnd` | text; 12 số (9 số CMND cũ) | chỉ **bán ra** (ẩn khi mua vào, giá trị cũ bị bỏ khỏi kế hoạch) |
| Trạng thái hóa đơn | `StatusCode` | `tthai` | dropdown *không cho gõ*: Tất cả · 1 Hóa đơn mới · 2 thay thế · 3 điều chỉnh · 4 đã bị thay thế · 5 đã bị điều chỉnh · 6 đã bị hủy | cả hai |
| Kết quả kiểm tra | `CheckResultCode` | `ttxly` | dropdown: Tất cả · 0…8 (9 nhãn của cổng, `PortalOptions.CheckResults`) | cả hai |
| Ký hiệu mẫu số | `FormSymbol` | `khmshdon` | dropdown Tất cả, 1…9 | cả hai |
| Ký hiệu hóa đơn | `InvoiceSymbol` | `khhdon` | text chữ + số | cả hai |
| Số hóa đơn | `InvoiceNumber` | `shdon` | text số, ≤ 8 chữ số | cả hai |
| Hóa đơn ủy nhiệm | `ProxyInvoice` | `unhiem==1` | checkbox; không tích = không gửi | cả hai |

- Ngày lập (bắt buộc) là kỳ ở mục 1 của form, được chia theo tháng; mỗi đoạn gửi `tdlap=ge=dd/MM/yyyyT00:00:00;tdlap=le=dd/MM/yyyyT23:59:59`.
- `InvoiceFilterValidator` kiểm tra định dạng ngay khi nhập (thông báo ở thẻ bộ lọc, nút bắt đầu khóa kèm lý do "Bộ lọc có giá trị chưa hợp lệ…"); giá trị dropdown luôn là mã của cổng, "Tất cả" có mã rỗng.
- `IPortalCapabilities.SupportsFilter(direction, field)` quyết định trường nào hiện theo chiều (cấu hình `search.soldFilterFields` / `purchaseFilterFields`).

### 5.2 Hành vi

- Đổi kiểu kỳ/giá trị → tính lại kế hoạch ngay (FR-SEARCH-006); lỗi nhập hiển thị tại control, không bằng `MessageBox`.
- Bộ lọc không làm thay đổi phạm vi ngày (FR-SEARCH-008); `Xóa bộ lọc` đưa về mặc định (FR-SEARCH-009).
- `btnStartDownload` chỉ bật khi (FR-SEARCH-010): đã đăng nhập (`IPortalSessionStore.HasActiveSession`), kỳ hợp lệ, `StorageGuard` xác nhận thư mục ghi được, entitlement cho `NewDownload`. Lý do khóa hiển thị thành chữ cạnh nút.
- Bấm bắt đầu → `IDownloadOrchestrator.CreateSessionAsync(plan)` ([DD-05](DD-05-DOWNLOAD-ENGINE.md)) → điều hướng SCR-04.
- ServiceMode: header luôn hiển thị tên/MST đang hoạt động; xác nhận bắt đầu nêu tên và MST (NFR-USE-007).

### 5.3 Presenter

`DownloadConfigPresenter` nhận `IPeriodSplitter`, `IPortalSessionStore`, `IEntitlementService`, `IStorageGuard`, `IDownloadOrchestrator`; không tham chiếu control cụ thể (DD-01 mục 12.2).

## 6. Kiểm thử

### 6.1 Unit (bắt buộc — NFR-MNT-001)

- Một tháng; nhiều tháng; quý Q1–Q4; năm = 12 đoạn; năm nhuận 2024 và 2028 (29 ngày), năm thường 2026.
- Ngày đầu/cuối tháng; From = To; qua năm (15/12/2025–10/02/2026).
- `To < From` bị từ chối; thiếu giá trị theo từng kiểu kỳ bị từ chối.
- Thứ tự tăng dần; không đoạn nào vượt ranh giới tháng; hợp các đoạn = đúng khoảng ban đầu.
- Điều kiện bật nút bắt đầu (bảng thật/giả cho 4 điều kiện).

### 6.2 UI smoke

- Chọn năm 2026 mua vào → bảng 12 dòng (AC-02).
- Nhập 15/01/2026–10/03/2026 → 3 dòng (AC-03).

## 7. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 7.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T04-01 | Domain: `InvoiceDirection`, `PeriodKind`, `PeriodSpec`, `DateSegment`, `InvoiceSearchFilter`, `DownloadPlan` | Domain | T01-02 | ✅ | | `PeriodKind/PeriodSpec/DateSegment` ở Domain; `InvoiceSearchFilter` (tên trường theo form cổng: `CounterpartyTaxCode`, `BuyerIdNumber`, `StatusCode`, `CheckResultCode`...) và `DownloadPlan` ở Application vì dùng chung với client cổng |
| T04-02 | `PeriodSplitter` + `PeriodValidationResult` | Domain | T04-01 | ✅ | | Lỗi tại control, cảnh báo khoảng > 10 năm (cấu hình `MaxCustomYears`) |
| T04-03 | Unit test chia kỳ (mục 6.1) | Tests | T04-02 | ✅ | | 41 test: tháng/quý/năm, năm nhuận 2024/2028, biên tháng, qua năm, From = To, kỳ sai, bất biến (liền kề, tăng dần, hợp = khoảng gốc), không phụ thuộc culture; AC-03 |
| T04-04 | `DownloadConfigControl` + `DownloadConfigPresenter` (SCR-03) | WinForms | T04-02, T01-05 | ✅ | | Krypton, partial Designer, bám wireframe SCR-03; `DownloadConfigScreen` đăng ký qua `ScreenDescriptor`; 19 test presenter |
| T04-05 | Điều kiện bật nút bắt đầu + `IStorageGuard` | Application | T01-07 | ✅ | | `DownloadStartPolicy` (doanh nghiệp → quyền → đăng nhập đúng MST → kỳ → thư mục ghi được, lý do hiển thị cạnh nút) + `IStorageProbe`/`FileSystemStorageProbe`; kiểm tra lại ngay khi bấm bắt đầu |
| T04-06 | Bộ lọc theo trường API hỗ trợ | WinForms | G-02 | ✅ | | `IPortalCapabilities.SupportsFilter(direction, field)` ← `search.soldFilterFields/purchaseFilterFields`: chỉ hiện trường được hỗ trợ theo chiều (CCCD chỉ bán ra), nhãn MST đổi theo chiều; danh sách trường của **bán ra đã khớp traffic thật**, mua vào ⚠ |
| T04-07 | Nối `IDownloadOrchestrator.CreateSessionAsync` → SCR-04 | WinForms | T05-03 | ✅ | | `IDownloadSessionStarter.StartAsync` trả mã phiên: `OrchestratorSessionStarter` tạo phiên từ `DownloadPlan`, SCR-03 mở SCR-04 với mã phiên đó; Release dùng `UnavailableDownloadSessionStarter` tới khi có SQLite |
| T04-08 | UI smoke test (mục 6.2) | Tests | T04-04 | ✅ | | 6 test: năm 2026 → 12 dòng (AC-02), 15/01–10/03 → 3 dòng (AC-03), kỳ sai, chưa đăng nhập, bắt đầu → SCR-04, chiều hóa đơn loại trừ |
| T04-09 | Rà soát bộ lọc theo mô tả tham số của cổng: dropdown đúng mã/nhãn (`PortalOptions`), thêm CCCD người mua, `InvoiceFilterValidator`, thông báo lỗi trên form | Application/WinForms | G-02 | ✅ | | 25 test (validator, options, presenter); SCR-03 vẽ lại thẻ bộ lọc bằng dock; dropdown là `DropDownList`, không còn gõ tự do |

### 7.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 8. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
