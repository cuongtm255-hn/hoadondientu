# Hóa đơn điện tử

Ứng dụng Windows Forms hỗ trợ kế toán tải và quản lý hóa đơn điện tử từ [Cổng Hóa đơn điện tử của Tổng cục Thuế](https://hoadondientu.gdt.gov.vn/).

## Mục tiêu

Cổng Thuế giới hạn một lần tra cứu trong phạm vi một tháng. Ứng dụng mở rộng quy trình này để người dùng chọn:

- Tháng.
- Quý.
- Năm.
- Khoảng ngày tùy chọn.

Ứng dụng tự chia phạm vi thành các đoạn theo tháng, tự phân trang đến khi lấy hết dữ liệu và lưu checkpoint để tiếp tục khi bị gián đoạn.

## Chức năng dự kiến

- Đăng nhập thủ công trong WebView; người dùng tự nhập CAPTCHA.
- Tải hóa đơn mua vào và bán ra.
- Theo dõi tiến độ từng tháng, tạm dừng, tiếp tục và thử lại lỗi.
- Tự động lưu XML; PDF chỉ tạo khi xem hoặc in.
- Kho hóa đơn có tìm kiếm, lọc, xem chi tiết và kiểm tra tính toàn vẹn.
- Xuất danh sách Excel theo mẫu cổng Thuế.
- Xuất ZIP XML có manifest và mã băm.
- Lịch sử phiên tải, chống trùng và giữ phiên bản XML thay đổi.
- Chế độ chuẩn quản lý một doanh nghiệp/MST trên mỗi máy.
- Chế độ dịch vụ được mở bằng licence theo máy, cho phép khai báo không giới hạn MST nhưng chỉ làm việc với một MST tại một thời điểm.
- Mỗi MST có SQLite và thư mục XML/PDF cache riêng để sao lưu, khôi phục hoặc bàn giao độc lập.

## Quy trình đăng nhập

1. WebView mở trực tiếp trang đăng nhập của cổng Thuế.
2. Người dùng tự nhập tài khoản, mật khẩu và CAPTCHA.
3. Ứng dụng thu token/cookie của phiên đăng nhập thành công.
4. API client sử dụng phiên đó để tra cứu và tải dữ liệu.
5. Khi token hết hạn, tác vụ tạm dừng; sau khi đăng nhập lại, ứng dụng tiếp tục từ checkpoint.

Ứng dụng không tự giải CAPTCHA, không lưu mật khẩu và không ghi token vào database/log.

## Tài liệu

- [SRS đầy đủ](docs/requirements/SRS.md)
- [Danh sách tính năng](docs/requirements/FEATURE-LIST.md)
- [Detail design theo tính năng](docs/detail-design/README.md), gồm [Khung chương trình và đăng nhập](docs/detail-design/DD-01-APP-SHELL-LOGIN.md)
- [Kế hoạch và tiến độ theo phase](docs/plans/PROJECT-PLAN.md)
- [Wireframe HTML tương tác](docs/wireframes/index.html)
- [Danh mục wireframe PNG](docs/wireframes/README.md)
- [Chỉ mục tài liệu](docs/README.md)
- [Hướng dẫn ngữ cảnh dự án](CLAUDE.md)

## Quyết định thiết kế chính

- Một ứng dụng có hai chế độ: chế độ chuẩn một MST và chế độ dịch vụ đa MST được licence mở khóa.
- Licence theo máy, kích hoạt trực tuyến, không giới hạn số MST và không gửi MST/dữ liệu hóa đơn tới máy chủ licence.
- Chỉ một MST hoạt động tại một thời điểm; chuyển MST sẽ đóng token, tác vụ và SQLite hiện tại trước khi mở kho mới.
- Mỗi MST có thư mục và SQLite riêng; không có database hóa đơn dùng chung.
- Khi licence dịch vụ mất hiệu lực, dữ liệu cũ vẫn được xem, in, xuất và sao lưu ở chế độ chỉ đọc.
- Giao diện ứng dụng nghiệp vụ kế toán với thanh điều hướng bên trái.
- Kho hóa đơn là trung tâm tra cứu và xử lý.
- SQLite lưu metadata/checkpoint; file XML/PDF lưu trong kho có cấu trúc theo năm, tháng và loại hóa đơn.
- XML lưu tự động; PDF là bản phát sinh theo nhu cầu.
- Tải tuần tự theo tháng để phù hợp giới hạn của cổng Thuế.

## Công nghệ và ràng buộc

- C#, Windows Forms.
- .NET Framework 4.5 (`net45`).
- TLS 1.2 phải được bật rõ ràng.
- WebView control phải được chọn sau technical spike trên cổng Thuế thật và ghi bằng ADR.
- Endpoint/payload API phải được xác minh từ network traffic hợp lệ; không giả định cấu trúc chưa kiểm chứng.

## Cấu trúc repository

```text
src/                  Mã nguồn ứng dụng
tests/                Unit/integration tests
docs/
├── requirements/     SRS, feature list và yêu cầu nghiệm thu
├── detail-design/    Detail design theo tính năng, kèm theo dõi tiến độ implement
├── wireframes/       HTML tương tác và PNG
├── architecture/     Tổng quan kiến trúc xuyên tính năng
├── decisions/        ADR
├── api-samples/      Mẫu API đã loại dữ liệu nhạy cảm
├── plans/            Kế hoạch và tiến độ dự án theo phase
├── progress/         Ghi chú bàn giao giữa các phiên
└── runbooks/         Build, release và xử lý sự cố
```

## Trạng thái phát triển

Repository đang ở **Phase 1 — Nền tảng** (tài liệu Phase 0 chờ review). Solution `src/HoaDonDienTu.sln` đã có khung chương trình (Krypton Ribbon), khởi động theo chế độ, đăng nhập cổng Thuế (trình duyệt giả ở Debug), chuyển MST và đóng ứng dụng an toàn; các màn hình nghiệp vụ vẫn là placeholder và WebView/API cổng Thuế thật chưa được chọn. Xem [kế hoạch theo phase](docs/plans/PROJECT-PLAN.md).

Build và test (PowerShell; `msbuild`/`vstest` của Visual Studio 2022 không có trong PATH):

```powershell
$vs = "C:\Program Files\Microsoft Visual Studio\2022\Community"
& "$vs\MSBuild\Current\Bin\MSBuild.exe" src\HoaDonDienTu.sln -restore -v:m
& "$vs\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" (Get-ChildItem tests\*\bin\Debug\net45\*.Tests.dll).FullName
```

Test UI (`Category=UiSmoke`) mở cửa sổ thật nên cần desktop tương tác; trên máy không có desktop thêm `/TestCaseFilter:"TestCategory!=UiSmoke"` khi chạy `vstest`.

Chạy thử bản Debug: `src\HoaDonDienTu.WinForms\bin\Debug\HoaDonDienTu.exe`. Đặt biến môi trường `HDDT_DEV_MODE` = `Standard` (mặc định), `StandardFirstRun`, `Service`, `ReadOnly` hoặc `Recovery` để thử từng chế độ với dữ liệu giả; `HDDT_DEV_JOB=1` giả lập tác vụ tải đang chạy. Các adapter giả chỉ được biên dịch ở cấu hình Debug.

Máy build cần Visual Studio/MSBuild và .NET Framework 4.5 targeting pack. UI dùng Krypton Toolkit/Ribbon `5.500.2008` (bản cuối có build net45).
