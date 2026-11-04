# DD-06 — Chuẩn hóa, lưu XML, chống trùng và kho dữ liệu SQLite theo MST

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-HDDT-06 |
| Tên | Schema `company.db`, chuẩn hóa metadata, khóa chống trùng, ghi XML atomic, phiên bản hóa, cấu trúc thư mục |
| Phiên bản | 1.0 |
| Ngày | 01/11/2026 |
| Trạng thái | Thiết kế — chờ review; thư viện SQLite chờ gate G-08 |
| Feature | [FT-07](../requirements/FEATURE-LIST.md#ft-07--chuẩn-hóa-lưu-xml-chống-trùng-và-phiên-bản-hóa) |
| SRS | BR-008..010, 013, 018; FR-STORE-001..010; mục 9; NFR-REL-001, 003, 004; NFR-MNT-003; AC-04, AC-05 |
| Màn hình | Không có (hạ tầng dữ liệu); hiển thị ở SCR-05/06 |
| Phụ thuộc | [DD-00](DD-00-TECHNICAL-GATES.md) (G-02, G-08), [DD-01](DD-01-APP-SHELL-LOGIN.md) (`CompanyContext`, `StorageGuard`, atomic) |
| Phase triển khai | [Phase 3](../plans/PROJECT-PLAN.md#phase-3--tải-hóa-đơn-end-to-end) |

## 2. Mục đích và phạm vi

Định nghĩa cách một hóa đơn nhận từ cổng Thuế trở thành bản ghi metadata + file XML bền vững trong kho riêng của đúng MST. Đây là nền cho Kho hóa đơn, xuất dữ liệu, sao lưu và khôi phục.

Ngoài phạm vi: tạo PDF (DD-08), truy vấn giao diện (DD-07), đóng gói sao lưu (DD-11).

## 3. Cấu trúc thư mục

Theo SRS mục 9.4:

```text
<StorageRoot>\                      (companies.json, license.dat ở cấp cài đặt)
└── Companies\<MST>\
    ├── Data\company.db
    ├── <yyyy>\<MM>\{MuaVao|BanRa}\{XML|PDF}\
    ├── Exports\  Backups\  Logs\
```

- Database lưu đường dẫn **tương đối** so với thư mục `<MST>` để cả kho di chuyển được.
- Tên file: `<yyyyMMdd>_<MST-doi-tac>_<mau-so>_<ky-hieu>_<so-hoa-don>_<version>.xml`; ký tự không hợp lệ với Windows thay bằng `_`; thêm hậu tố chống va chạm; kiểm tra giới hạn độ dài đường dẫn.
- `InvoiceFileNamer` là lớp thuần, có unit test (NFR-MNT-001).

## 4. Schema SQLite (logic)

| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `SchemaInfo` | `Version`, `AppliedAt` | Một bản ghi hiện hành |
| `CompanySettings` | `TaxCode`, `DisplayName`, `Config` | Một bản ghi; MST khóa sau khi có dữ liệu (FR-SET-002) |
| `Invoice` | `InvoiceId`, `PortalInvoiceId`, `BusinessKey` (UNIQUE), `Direction`, 19 trường mục 9.1 SRS, `StatusRaw`, `CheckResultRaw`, `FirstSeenAt`, `LastSeenAt`, `CurrentVersionId`, `FileIntegrity` | Index: số hóa đơn, MST đối tác, ngày lập, direction |
| `InvoiceVersion` | `VersionId`, `InvoiceId`, `VersionNo`, `Sha256`, `CreatedAt`, `IsCurrent`, `SourceSessionId` | Nhiều phiên bản/hóa đơn |
| `FileAsset` | `FileId`, `VersionId`, `Kind` (XML/PDF), `RelativePath`, `SizeBytes`, `Sha256`, `DownloadedAt`, `Status`, `LastCheckedAt` | PDF ghi `SourceVersionId` + ngày tạo |
| `DownloadSession` / `DownloadSegment` / `DownloadEvent` | xem [DD-05](DD-05-DOWNLOAD-ENGINE.md) | |
| `ExportRecord` | `ExportId`, `Kind`, `Scope`, `Path`, `Manifest`, `CreatedAt` | Xem [DD-09](DD-09-EXPORT.md) |

- Bật transaction; WAL nếu provider hỗ trợ; `PRAGMA integrity_check` trong quy trình chẩn đoán/sao lưu (NFR-REL-003).
- Migration có version, chạy theo thứ tự, luôn sao lưu trước khi nâng cấp (NFR-MNT-003).
- Mọi truy cập đi qua `ICompanyUnitOfWorkFactory` của `CompanyContext`; không có truy cập database toàn cục (BR-018).

## 5. Chuẩn hóa và khóa chống trùng

```csharp
public sealed class NormalizedInvoice { /* 19 trường mục 9.1 SRS + Direction + PortalInvoiceId + raw status */ }

public interface IInvoiceNormalizer
{
    NormalizedInvoice Normalize(PortalInvoiceDto dto, InvoiceDirection direction);
}

public static class BusinessKey
{
    // Ưu tiên PortalInvoiceId; thiếu → Direction|SellerTaxCode|FormSymbol|InvoiceSymbol|InvoiceNumber|IssueDate
    public static string Build(NormalizedInvoice invoice);
}
```

- Mua vào và bán ra dùng chung một mô hình (FR-STORE-001); số hóa đơn, ký hiệu giữ dạng text để bảo toàn số 0 đầu.
- Trạng thái và kết quả kiểm tra lưu cả giá trị chuẩn hóa và giá trị nguồn; bảng ánh xạ có unit test.
- Mapping nằm trong Application/Infrastructure, độc lập với UI; thay đổi schema API được xử lý ở adapter (NFR-MNT-002).

### 5.1 Nguồn dữ liệu thật (đã đối chiếu mẫu)

- **Khóa nghiệp vụ:** cổng định danh hóa đơn bằng bộ **(MST người bán `nbmst`, ký hiệu mẫu số `khmshdon`, ký hiệu `khhdon`, số `shdon`)**; `id` (GUID) chỉ là mã nội bộ của cổng. `BusinessKey` nên dựa vào bộ này (+ `Direction`), `PortalInvoiceId` lưu thêm để đối chiếu. Số hóa đơn từ cổng là số nguyên (`40`) — lưu dạng text, định dạng lại khi cần.
- **Ngày lập:** `tdlap` là UTC của 0 giờ ngày lập theo giờ VN (`2026-10-30T17:00:00Z` = 31/10/2026); `PortalInvoiceDto.IssueDate` đã là ngày giờ VN, lưu dạng ngày.
- **Người mua cá nhân:** `nmmst` null và có `nmcmnd`; cột "MST người mua" của bảng kê lấy MST, nếu null thì lấy CCCD.
- **Trạng thái:** `tthai` 1–6 và `ttxly` 0–8 (bảng ánh xạ nhãn trong `PortalOptions`); lưu cả mã nguồn.
- **XML nguyên văn:** `invoice.xml` trong gói xuất XML là hóa đơn đã ký (XMLDSig của người bán và cơ quan thuế); lưu **byte nguyên văn**, băm SHA-256, không định dạng lại. Phần tử chính: `TTChung` (KHMSHDon, KHHDon, SHDon, NLap, DVTTe, TGia, HTTToan), `NDHDon/NBan|NMua`, `DSHHDVu/HHDVu*`, `TToan` (TgTCThue, TgTThue, TgTTTBSo, TgTTTBChu, THTTLTSuat), `MCCQT`, `DSCKS`.
- **Chi tiết JSON:** API chi tiết cho thêm `hdhhdvu[]` (hàng hóa), `thttltsuat[]`, `qrcode`; dùng cho SCR-06 khi chưa có XML hoặc để đối chiếu.

## 6. Quy trình lưu một hóa đơn

```text
UpsertAsync(dto, xmlStream)
  guard: CompanyContext khớp, MST phiên khớp (NFR-SEC-006)
  1. Normalize → BusinessKey
  2. Ghi XML vào file tạm cùng volume (…\XML\*.tmp)
  3. Kiểm tra XML đọc được (XmlReader, DtdProcessing.Prohibit) và tính SHA-256   (FR-STORE-003)
  4. Tìm Invoice theo BusinessKey:
        không có            → tạo Invoice + InvoiceVersion #1 (IsCurrent)
        có, hash trùng      → chỉ cập nhật LastSeenAt                           (FR-STORE-006, BR-009)
        có, hash khác       → tạo InvoiceVersion mới, đặt IsCurrent, giữ bản cũ (FR-STORE-007, BR-010)
  5. Đổi tên file tạm → file chính thức (atomic rename), rồi commit transaction  (FR-STORE-004)
  6. Lỗi ở bất kỳ bước nào: xóa file tạm, rollback, ghi DownloadEvent; không báo thành công (FR-STORE-010)
```

Thứ tự "đổi tên rồi commit" cùng quy tắc quét file mồ côi (file có trong thư mục nhưng không có `FileAsset`) bảo đảm mất điện không làm mất hóa đơn đã commit (NFR-REL-001).

## 7. Kiểm tra toàn vẹn

`IFileIntegrityChecker.CheckAsync(scope)` so sánh `FileAsset` với đĩa: thiếu, không đọc được XML, sai SHA-256. Kết quả ghi `FileIntegrity`/`LastCheckedAt` và hiển thị cảnh báo cụ thể ở Kho hóa đơn; không tự thay file (FR-STORE-009, 010). Hành động "tải lại XML" do người dùng khởi tạo qua DD-05.

## 8. Kiểm thử

### 8.1 Unit

- `BusinessKey` ưu tiên ID cổng; khóa dự phòng ổn định; phân biệt mua/bán.
- `InvoiceFileNamer`: ký tự cấm, hậu tố va chạm, độ dài tối đa.
- Ánh xạ trạng thái/kết quả kiểm tra; chuẩn hóa số 0 đầu.
- Quy tắc phiên bản hóa: trùng hash / khác hash / mới (AC-04, AC-05).

### 8.2 Integration

- SQLite: transaction, rollback, migration lên/xuống bằng backup, tìm kiếm 100.000 bản ghi (NFR-PERF-002).
- File: ghi atomic, file bị khóa, hết dung lượng, sai quyền, mất điện giả lập giữa bước 5.
- Hai MST hai database độc lập; ghi sai context bị từ chối.
- XML hỏng không tạo file chính thức; integrity checker phát hiện thiếu/sai hash.

## 9. Theo dõi tiến độ implement

Trạng thái: ⬜ Chưa bắt đầu · 🟡 Đang làm · ✅ Hoàn thành · ⛔ Bị chặn.

### 9.1 Công việc

| ID | Công việc | Lớp | Phụ thuộc | Trạng thái | PR/Commit | Ghi chú |
|---|---|---|---|---|---|---|
| T06-01 | Domain: `NormalizedInvoice`, `BusinessKey`, trạng thái/kết quả kiểm tra, `InvoiceFileNamer` | Domain | T01-02 | ✅ | | `NormalizedInvoice`, `InvoiceStatus`/`InvoiceCheckResult` (+ `InvoiceCodes`), `BusinessKey` (khóa bốn trường + chiều, bỏ số 0 đầu), `InvoiceFileNamer` (tên + thư mục + hậu tố + giới hạn đường dẫn) ở Domain; 31 test |
| T06-02 | Schema + `CompanyDatabaseInitializer` + `SchemaVersionReader` + migration framework | Infra | G-08 | ✅ | | `CompanyDatabaseInitializer` (tạo thư mục/file, nâng cấp theo `user_version`, mỗi migration một giao dịch, từ chối schema mới hơn, `integrity_check`), `SchemaVersionReader`, `SchemaMigration`/`CompanyDatabaseSchema` v1 (5 bảng, khóa duy nhất, một phiên bản hiện hành mỗi hóa đơn, cascade), `SqliteDatabase` (WAL, NORMAL, foreign_keys, busy_timeout). Tiền = số nguyên × 10.000, tỷ giá TEXT, thời điểm ISO 8601 UTC; phiên/bộ lọc lưu JSON. 14 test (`CompanyDatabaseInitializerTests`) |
| T06-03 | Repository `Invoice`/`InvoiceVersion`/`FileAsset` + unit-of-work factory | Infra | T06-02 | ✅ | | `CompanyDatabaseProvider` (`<kho>\Data\company.db`, khởi tạo lần đầu, một `SqliteDatabase` mỗi file), `SqliteInvoiceRepository` (thêm, thêm phiên bản, làm mới, liệt kê, cột tìm đã bỏ dấu), ghi tuần tự qua `WriteAsync` (giao dịch, đã commit trước khi trả về). Chưa có `FileAsset` PDF (T08-04) |
| T06-04 | `IInvoiceNormalizer` + mapping từ DTO cổng | Application/Infra | G-02 | ✅ | | `IInvoiceNormalizer`/`InvoiceNormalizer` từ `PortalInvoiceDto` (đã khớp mẫu thật): làm sạch text, giữ giá trị nguồn của mã |
| T06-05 | `InvoiceStorageService.UpsertAsync` (file tạm, hash, phiên bản hóa) | Application | T06-01, T06-03 | 🟡 | | `InvoiceStorageService : IInvoiceStore` đủ quy trình mục 6 (chuẩn hóa → kiểm XML `DtdProcessing.Prohibit` → SHA-256 → khóa chống trùng → file tạm → đổi tên atomic → metadata; trùng hash chỉ cập nhật LastSeen, khác hash tạo phiên bản mới giữ bản cũ; lỗi thì dọn file). `FileSystemInvoiceFileStore` ghi thật ra đĩa. Metadata mới ở `InMemoryInvoiceRepository` — chờ SQLite (T06-02/03, G-08) |
| T06-06 | `IFileIntegrityChecker` + quét file mồ côi | Application | T06-05 | ✅ | | 03/11/2026: `InvoiceIntegrityChecker` (chỉ đọc) so metadata với đĩa: thiếu file, đọc không được, sai SHA-256, không phải XML, file XML mồ côi, file `.tmp` sót; `FileSystemInvoiceFileScanner` bỏ qua `Data`/`Backups`/`Exports`; 12 + 4 test. 04/11/2026: đã nối vào nút Kiểm tra file của Kho hóa đơn (SCR-05) và đăng ký trong `CompositionRoot`, có hộp thoại báo cáo. Kết quả được ghi vào `company.db` (migration 3: `invoice.file_problem` = vấn đề đầu tiên, NULL là nguyên vẹn/chưa kiểm; `file_checked_utc`) chỉ khi kiểm xong cả kho; thêm phiên bản mới hoặc ghi lại file bị mất thì xóa kết quả cũ. Kho hóa đơn hiện trạng thái `XML lỗi` (`ArchiveFileState.Damaged`) cho lỗi nội dung, còn "thiếu file" luôn theo đĩa lúc này; bộ lọc Lỗi/thiếu file và số hóa đơn có vấn đề tính cả hai. Chân trang Kho hóa đơn ghi "Kiểm tra file lần cuối" (lấy `file_checked_utc` mới nhất của các hóa đơn đang lọc), dòng tóm tắt tách "thiếu file" và "lỗi nội dung". Tổng quan đếm "cần chú ý" qua cùng bộ lọc Lỗi/thiếu file nên tự gồm cả file lỗi nội dung; danh sách hóa đơn gần đây ở Tổng quan vẫn chỉ nhận ra file thiếu. Hộp thoại kết quả có nút "Xóa file tạm" (hỏi lại, chỉ xóa file `.tmp` mà bộ quét vẫn thấy trong kho; `FileSystemInvoiceFileScanner.DeleteTempFile` từ chối file không phải `.tmp`, đường dẫn ra ngoài kho và thư mục `Data`/`Backups`/`Exports`; file bị khóa vẫn còn trong báo cáo). 04/11/2026: không còn việc dở của T06-06 (file lạ vẫn chỉ báo, không tự xóa). |
| T06-07 | Tạo cấu trúc thư mục kho + kiểm tra quyền/dung lượng (`CompanyStorageValidator`) | Infra | T02-03 | ✅ | 2026-11-04 | Quyền ghi/dung lượng/MST đã có từ T02-03; thêm `StorageInspection.DatabaseProblem` (đọc nhanh `Data\company.db`: không phải SQLite/hỏng đầu file hoặc schema mới hơn ứng dụng) chặn thêm/liên kết lại kho và mở kho (`DatabaseInvalid`). Thư mục `Data`, `Data\company.db` vẫn tạo lười ở lần dùng đầu; integrity_check đầy đủ chạy lúc đó. 6 test mới |
| T06-08 | Unit test (mục 8.1) | Tests | T06-01..T06-05 | 🟡 | | 40 test: Domain (31) + `InvoiceStorageServiceTests` (23: mới/trùng/phiên bản, XML hỏng/DTD, đường dẫn dài, lỗi ghi, guard MST/kho). Bổ sung khi có repository SQLite |
| T06-09 | Integration test (mục 8.2) | Tests | T06-05..T06-07 | 🟡 | | 12 integration test (`InvoiceStorageTests`): kho file thật (atomic, không ghi đè, dọn tạm), XML thật của cổng lưu nguyên byte + SHA-256, engine tải trên cổng giả ghi file vào thư mục MST (một tháng, 120 hóa đơn nhiều trang, tải lại không trùng, đổi nội dung → v2, zip hỏng, một hóa đơn bị từ chối). Chưa có SQLite/hết dung lượng thật |

### 9.2 Nhật ký tiến độ

| Ngày | Nội dung | Người thực hiện |
|---|---|---|
| 01/11/2026 | Tạo tài liệu; chưa có code | Claude Code |

## 10. Lịch sử thay đổi tài liệu

| Phiên bản | Ngày | Thay đổi |
|---|---|---|
| 1.0 | 01/11/2026 | Bản đầu |
