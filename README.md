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
- Sao lưu/khôi phục dữ liệu cho một doanh nghiệp/MST trên mỗi máy.

## Quy trình đăng nhập

1. WebView mở trực tiếp trang đăng nhập của cổng Thuế.
2. Người dùng tự nhập tài khoản, mật khẩu và CAPTCHA.
3. Ứng dụng thu token/cookie của phiên đăng nhập thành công.
4. API client sử dụng phiên đó để tra cứu và tải dữ liệu.
5. Khi token hết hạn, tác vụ tạm dừng; sau khi đăng nhập lại, ứng dụng tiếp tục từ checkpoint.

Ứng dụng không tự giải CAPTCHA, không lưu mật khẩu và không ghi token vào database/log.

## Tài liệu

- [SRS đầy đủ](docs/requirements/SRS.md)
- [Wireframe HTML tương tác](docs/wireframes/index.html)
- [Danh mục wireframe PNG](docs/wireframes/README.md)
- [Chỉ mục tài liệu](docs/README.md)
- [Hướng dẫn ngữ cảnh dự án](CLAUDE.md)

## Quyết định thiết kế chính

- Một doanh nghiệp/MST trên mỗi máy.
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
├── requirements/     SRS và yêu cầu nghiệm thu
├── wireframes/       HTML tương tác và PNG
├── architecture/     Kiến trúc và luồng dữ liệu
├── decisions/        ADR
├── api-samples/      Mẫu API đã loại dữ liệu nhạy cảm
├── plans/            Kế hoạch triển khai
├── progress/         Ghi chú bàn giao giữa các phiên
└── runbooks/         Build, release và xử lý sự cố
```

## Trạng thái phát triển

Repository hiện ở giai đoạn đặc tả/thiết kế; solution và project source chưa được tạo.

Khi có solution, các lệnh dự kiến:

```powershell
msbuild HoaDonDienTu.sln /p:Configuration=Debug /restore
vstest.console.exe path\to\Tests.dll
```

Máy build cần Visual Studio/MSBuild và .NET Framework 4.5 targeting pack.
