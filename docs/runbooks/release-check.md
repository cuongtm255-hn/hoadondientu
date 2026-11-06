# Runbook: kiểm tra bản Release trước khi phát hành (P6-04)

Theo [ADR 0005](../decisions/0005-mock-only-testing-policy.md), mã giả (development adapter, `Mock*`) chỉ biên dịch ở Debug và không được lọt vào bản Release; bản Release cũng không được chứa bí mật. Script `scripts/Check-Release.ps1` kiểm điều này trên thư mục đầu ra, chỉ đọc, không chạy ứng dụng, không gọi mạng.

## Cách chạy

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" src/HoaDonDienTu.WinForms/HoaDonDienTu.WinForms.csproj -restore -p:Configuration=Release -nologo -v:m
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Check-Release.ps1
```

Mặc định kiểm `src\HoaDonDienTu.WinForms\bin\Release`; đổi bằng `-ReleaseDir <thư mục>` (ví dụ thư mục sẽ đóng gói). Thoát mã 0 là đạt, mã 1 là có vi phạm (danh sách in ra).

## Script kiểm gì

1. **Kiểu giả**: không có kiểu nào trong `HoaDonDienTu.exe`, `.Application.dll`, `.Domain.dll`, `.Infrastructure.dll` thuộc namespace `Development` hoặc có tên chứa `Mock`, `Fake`, `Dummy`.
2. **Chuỗi cấm**: không có `[TEST]`, `DỮ LIỆU GIẢ`, `MOCK`, `mô phỏng`, `localhost` trong các assembly (dấu hiệu nhãn dữ liệu giả hoặc địa chỉ phát triển còn sót).
3. **Mẫu bí mật**: không có JWT, `Bearer <token>`, khóa riêng PEM, `password=<giá trị>` trong assembly và các file `.config/.json/.xml/.txt` đi kèm.
4. **File cấm** trong thư mục Release: `*.raw.*`, `*.db`, `companies.json`, `license.dat`, `company.identity.json`, `sample-api-*`, `*mock*`.

## Đã xác minh

- Ngày 06/11/2026: bản Release vừa build **đạt**. Cùng script chạy trên thư mục **Debug** báo **28 vi phạm** (các kiểu `Development*`, `Mock*` và nhãn dữ liệu giả), tức script phân biệt được hai cấu hình.

## Giới hạn

- Không chứng minh được mã Release không bao giờ gọi cổng thật ngoài ý muốn; đó là việc của UAT (P6-01) do người dùng thật chạy theo ngoại lệ ở ADR 0005.
- Chỉ quét assembly của ứng dụng, không quét CefSharp/Chromium và gói NuGet của bên thứ ba.
- Mẫu bí mật là heuristic; không thay cho rà soát P6-03.
- Nên chạy trước mỗi lần đóng gói (P6-05). Chưa gắn vào CI vì dự án chưa có CI.
