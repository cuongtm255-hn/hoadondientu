# DD-DVC-00 — Màn chọn nhóm chức năng khi khởi động

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Mã tài liệu | DD-DVC-00 |
| Phiên bản | 0.1 (nháp) |
| Ngày | 16/11/2026 |
| Trạng thái | Nháp, chờ duyệt cùng SRS-DVC 1.0 |
| Feature | FT-DVC-00 ([FEATURE-LIST](../requirements/FEATURE-LIST.md)) |
| SRS | [SRS-DVC](../requirements/SRS-DVC.md): FR-DVC-NAV-001..005, BR-DVC-01, AC-DVC-01 |
| Phụ thuộc | DD-03 (`ActivationGate`), DD-01 (`AppApplicationContext`, `MainShellForm`), DD-02 (danh mục MST) |
| Tác động nhóm Hóa đơn | Chỉ chạm khung khởi động (`Program`/`AppApplicationContext`); không đổi logic hóa đơn |

## 2. Quyết định đã chốt (SRS-DVC mục 18)

1. Một mã kích hoạt cho cả hai nhóm; `ActivationGate` chạy **trước** màn chọn, không đổi.
2. Màn chọn **luôn hiện** mỗi lần mở; thẻ nhóm gần nhất được chọn sẵn.
3. Đổi nhóm giữ phiên và WebView của nhóm kia.

## 3. Luồng khởi động

```
Program.Main
  → CompositionRoot.Create
  → ActivationGate.Allow        (hết dùng thử: thoát, như hiện nay)
  → AppApplicationContext
      → xác định chế độ + doanh nghiệp (DD-01/DD-02, như hiện nay)
      → GroupChooserForm         (MỚI) → AppGroup.Invoice | AppGroup.PublicService | huỷ = thoát
      → mở shell nhóm đã chọn
```

- Doanh nghiệp (MST) được xác định **trước** màn chọn nhóm, vì cả hai nhóm dùng chung MST đang chọn (FT-DVC-00).
- Huỷ/đóng màn chọn ⇒ thoát ứng dụng, không mở shell nào.

## 4. Thiết kế

### 4.1 Kiểu và dịch vụ (đặt ở Application/WinForms, không phụ thuộc UI trừ form)

| Thành phần | Tầng | Vai trò |
|---|---|---|
| `AppGroup` (enum: `Invoice`, `PublicService`) | Domain | Định danh nhóm |
| `IGroupPreferenceStore` | Application | `AppGroup? GetLastGroup()`, `void SetLastGroup(AppGroup)`; lưu trong cài đặt người dùng (cùng nơi `Settings`), không lưu bí mật |
| `GroupNavigator` | Application | Giữ hai shell/host nhóm; `Show(AppGroup)`; `SwitchTo(AppGroup)`; tạo nhóm lười (lazy) lần đầu được chọn |
| `GroupChooserForm` | WinForms | Hai thẻ lớn (Krypton), thẻ gần nhất chọn sẵn, Enter = vào, Esc = thoát |
| `IGroupShell`, `IGroupShellFactory` | Application | Hợp đồng chung của shell (không phụ thuộc WinForms): `Show()`, `Hide()`, `CanLeave(out reason)`, `StopRunningTasks()`, `Dispose()` |

### 4.2 Quy tắc

- **Lazy:** nhóm chưa được chọn không nạp WebView/dịch vụ của nó (FR-DVC-NAV-002).
- **Đổi nhóm** (lệnh "Đổi nhóm chức năng" trên ribbon của mỗi shell):
  1. Gọi `CanLeave` của shell hiện tại; nếu có tác vụ đang chạy (tải hóa đơn, tra cứu) hỏi xác nhận dừng (FR-DVC-NAV-003).
  2. Ẩn shell hiện tại **không dispose**, giữ WebView/phiên.
  3. Mở lại `GroupChooserForm` dạng hộp thoại; chọn nhóm nào thì `Show` shell nhóm đó (tạo nếu chưa có).
- **Thoát ứng dụng:** dispose cả hai shell theo thứ tự; xoá kết quả DVC trong bộ nhớ (BR-DVC-07).
- **Một tab DVC:** WebView DVC chỉ tồn tại trong shell DVC; không bao giờ có hai trang DVC cùng hoạt động (BR-DVC-06). Giữ WebView ẩn khi ở nhóm Hóa đơn là cho phép vì vẫn là một trang.
- **Chế độ ReadOnlyServiceMode (DD-01):** áp dụng như cũ cho nhóm Hóa đơn; nhóm DVC ở chế độ này chỉ xem phần không cần đăng nhập, hoặc bị khoá giống hóa đơn (chốt ở review DD-01).

### 4.3 Tiêu đề và nhãn

- Tiêu đề cửa sổ ghi tên nhóm: "Hóa đơn điện tử — Tra cứu hóa đơn" / "Hóa đơn điện tử — Dịch vụ công thuế".
- Bản Debug/mock: nhãn "MÔ PHỎNG" ở thanh tiêu đề và trên màn chọn (CLAUDE.md).

## 5. Lưu cài đặt

- Khoá `LastGroup` trong tệp cài đặt người dùng hiện có (DD-11); giá trị chưa đặt hoặc không hợp lệ ⇒ mặc định `Invoice`.

## 6. Kiểm thử

| Mã | Kiểm thử | Loại |
|---|---|---|
| T-DVC00-01 | Chưa có `LastGroup` ⇒ thẻ Hóa đơn được chọn sẵn | Unit (`GroupNavigator`/presenter, tách khỏi form) |
| T-DVC00-02 | Có `LastGroup = PublicService` ⇒ thẻ DVC chọn sẵn; chọn xong lưu lại | Unit |
| T-DVC00-03 | Hết dùng thử ⇒ không hiện màn chọn (gate chặn trước) | Unit/tích hợp mức composition |
| T-DVC00-04 | Chưa chọn DVC ⇒ không tạo shell/WebView DVC (lazy) | Unit |
| T-DVC00-05 | Đổi nhóm giữ nguyên shell kia (cùng instance), không dispose | Unit |
| T-DVC00-06 | Đổi nhóm khi có tác vụ chạy ⇒ hỏi xác nhận; từ chối ⇒ ở lại | Unit |
| T-DVC00-07 | Huỷ màn chọn lúc khởi động ⇒ thoát, không mở shell | Unit |

Kiểm thử chỉ dùng shell giả; không chạm cổng thật.

## 7. Tiến độ triển khai

| Task | Nội dung | Trạng thái |
|---|---|---|
| TD00-01 | `AppGroup`, `IGroupPreferenceStore` (giao diện xong; cài đặt `LastGroup` vào DD-11 làm ở TD00-03) | Xong 16/11/2026 |
| TD00-02 | `GroupNavigator` + `IGroupShell` + test T-DVC00-01,02,04..07 (9 test đạt; T-DVC00-03 do `ActivationGateTests` phủ) | Xong 16/11/2026 |
| TD00-03 | `GroupChooserForm`, `FileGroupPreferenceStore` (`last-group.txt`, có test), nối vào `Program.Main` sau `ActivationGate`; nhóm DVC hiện `PublicServiceStubForm` giữ chỗ. Lệch DD: chooser hiện **trước** khi chọn MST (MST hiện được chọn trong shell Hóa đơn), nên DD mục 3 cần xét lại khi làm shell DVC. Chưa nối `GroupNavigator` vào shell thật | Xong một phần 16/11/2026 |
| TD00-04 | Lệnh "Đổi nhóm" trên ribbon shell Hóa đơn; stub shell DVC | Chưa làm |
| TD00-05 | Cập nhật SRS/FT-01 nhóm Hóa đơn thêm bước chọn nhóm (cần duyệt) | Chưa làm |
