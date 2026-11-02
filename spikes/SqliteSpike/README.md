# Spike G-08 — SQLite provider cho .NET Framework 4.5

Mã thử nghiệm dùng một lần, **không nằm trong solution** (`src/HoaDonDienTu.sln`) và không được tham chiếu từ sản phẩm. Chỉ dùng dữ liệu giả; không gọi cổng Thuế.
Kết luận và quyết định nằm ở [ADR 0006](../../docs/decisions/0006-sqlite-provider.md).

## Chạy lại

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" spikes/SqliteSpike/SqliteSpike.csproj -restore -nologo -v:m
spikes/SqliteSpike/bin/Debug/net45/SqliteSpike.exe          # 64-bit (AnyCPU trên OS 64-bit)
```

Bản 32-bit: build ra thư mục riêng rồi ép cờ `32BITREQ` bằng `CorFlags.exe <exe> /32BITREQ+` (SDK .NET Framework 4.6 Tools) và chạy lại.
Chương trình tự tạo thư mục tạm, in kết quả từng mục và có chế độ con `crash-writer` để kiểm tra bị giết tiến trình giữa giao dịch.

## Kết quả (02/11/2026, một lần chạy trên máy phát triển Windows 11 x64; số đo chỉ mang tính tương đối)

Gói: `System.Data.SQLite.Core 1.0.118` → SQLite **3.42.0**; chạy được cả tiến trình **64-bit** và **32-bit** (ép `32BITREQ`), kết quả như nhau.

| # | Hạng mục | Kết quả |
|---|---|---|
| 1 | `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON`, `user_version`, `PRAGMA integrity_check` | Hoạt động; `integrity_check = ok` |
| 2 | Nạp 100.000 hóa đơn (13 cột, 3 chỉ mục) trong 1 giao dịch | ≈ 2,5–2,8 giây; file ≈ 32 MB |
| 2 | Trang 50 dòng sắp theo ngày lập giảm dần (chiều mua vào) | 0–1 ms, kể cả `OFFSET 50000` |
| 2 | `COUNT(*)` theo chiều / tìm theo MST (có chỉ mục) / tra khóa duy nhất | 0–1 ms |
| 2 | Tóm tắt `SUM` theo năm và chiều trên 75.000 dòng | ≈ 99 ms |
| 2 | Tìm nhanh `LIKE '%…%'` trên cột đã bỏ dấu (quét toàn bảng) | ≈ 23 ms |
| 2 | Đọc streaming 75.000 dòng bằng `DataReader` | ≈ 255 ms, bộ nhớ managed tăng ≈ 8 KB |
| 3 | Tiếng Việt | Lưu/đọc nguyên vẹn. `lower()`/`LIKE` **không** hạ/so khớp chữ có dấu (`LIKE 'công ty đông á'` khớp 0/3); `LIKE` không bỏ dấu ⇒ cần cột đã bỏ dấu do ứng dụng tạo |
| 4 | 1 người ghi (50 giao dịch × 200 dòng) + 1 người đọc liên tục (WAL) | Ghi ≈ 0,45–0,5 s; ≈ 760–900 lần đọc; lần đọc chậm nhất 1 ms; không lỗi khóa |
| 5 | `BackupDatabase` file 110.000 dòng | 87–342 ms; bản sao `integrity_check = ok`, số dòng bằng nguồn |
| 5 | `BackupDatabase` khi đang có người ghi | Bản sao nhất quán (`integrity_check = ok`) |
| 6 | Migration bằng `user_version` + `ALTER TABLE ADD COLUMN` trong giao dịch | Hoạt động |
| 7 | Giết tiến trình đang ghi (8 vòng × 2 kiến trúc) | Không lần nào hỏng; không còn giao dịch dở (mỗi lô 100 dòng hoặc đủ hoặc không có) |
| 8 | Đường dẫn có tiếng Việt và khoảng trắng | Hoạt động |
| 8 | Đường dẫn 240+ ký tự | `PathTooLongException` từ **.NET 4.5** (`Directory.CreateDirectory`), không phải từ SQLite; giới hạn 260 ký tự đã có ở `InvoiceFileNamer` và áp dụng cho cả file `company.db` |

Phân phối: `System.Data.SQLite.dll` ≈ 441 KB + `x86\SQLite.Interop.dll` ≈ 1,5 MB + `x64\SQLite.Interop.dll` ≈ 1,8 MB. Bản interop chỉ import `kernel32`, `advapi32`, `user32` (không thấy `VCRUNTIME`/`MSVCP` trong bảng import) nên có vẻ liên kết CRT tĩnh và không đòi Visual C++ Redistributable — **chưa kiểm trên máy sạch**.

## Các gói khác đã thử restore trên `net45`

| Gói | Kết quả |
|---|---|
| `Microsoft.Data.Sqlite.Core` 8.0.0 / 2.x | `NU1202`: không tương thích net45 |
| `Microsoft.Data.Sqlite` 3.1.32 | `NU1202`: không tương thích net45 |
| `sqlite-net-pcl` 1.8.116 | `NU1202`: không tương thích net45 |
| `SQLitePCLRaw.bundle_e_sqlite3` 2.1.6 | `NU1202`: không có bản cho net45 (tối thiểu net461/netstandard2.0) |

## Chưa kiểm

- Máy sạch (Windows 7/8.1/10 chưa cài gì), tương thích .NET 4.5 thật sự trên máy chỉ có runtime 4.5 (máy thử có .NET 4.8).
- Phần mềm diệt virus chặn/hay báo nhầm `SQLite.Interop.dll`.
- Thư mục mạng (UNC) và ổ đĩa ngoài; WAL không an toàn trên chia sẻ mạng.
- Mã hóa dữ liệu lúc nghỉ (System.Data.SQLite bản miễn phí không có).
- Tải nhiều hơn 100.000 dòng hoặc nhiều tiến trình cùng mở một DB.
