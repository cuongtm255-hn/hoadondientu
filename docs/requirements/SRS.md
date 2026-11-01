# Đặc tả yêu cầu phần mềm (SRS)

## 1. Thông tin tài liệu

| Thuộc tính | Giá trị |
|---|---|
| Tên sản phẩm | Ứng dụng tải và quản lý hóa đơn điện tử |
| Mã tài liệu | SRS-HDDT-001 |
| Phiên bản | 1.1 |
| Ngày | 01/11/2026 |
| Trạng thái | Baseline thiết kế đã được xác nhận |
| Nền tảng | Windows Forms, C#, .NET Framework 4.5 |
| Đối tượng sử dụng | Kế toán thuế doanh nghiệp; công ty dịch vụ kế toán quản lý nhiều khách hàng |

## 2. Mục đích

Tài liệu này đặc tả ứng dụng Windows hỗ trợ đăng nhập vào Cổng Hóa đơn điện tử của Tổng cục Thuế, tra cứu và tải hóa đơn mua vào/bán ra, mở rộng giới hạn tìm kiếm từng tháng của cổng thành các kỳ tháng, quý, năm và khoảng ngày tùy chọn.

Ứng dụng đồng thời cung cấp kho hóa đơn cục bộ để kế toán tìm kiếm, kiểm tra, lưu XML, tạo PDF khi cần, xuất danh sách Excel, xuất ZIP XML và theo dõi lịch sử tải.

## 3. Phạm vi

### 3.1 Trong phạm vi phiên bản 1

- Chế độ chuẩn quản lý một doanh nghiệp/MST trên mỗi máy.
- Chế độ dịch vụ được mở khóa bằng licence theo máy, không giới hạn số MST đã khai báo nhưng chỉ một MST hoạt động tại một thời điểm.
- Mỗi MST trong chế độ dịch vụ có thư mục và SQLite riêng; không trộn dữ liệu giữa các doanh nghiệp.
- Người dùng đăng nhập thủ công trong WebView và tự xử lý CAPTCHA.
- Tra cứu hóa đơn điện tử mua vào và bán ra.
- Chọn kỳ theo tháng, quý, năm hoặc ngày bắt đầu/ngày kết thúc.
- Tự chia kỳ thành các đoạn không vượt quá một tháng.
- Tự phân trang/lặp batch cho đến khi lấy hết dữ liệu của từng tháng.
- Tải và lưu XML tự động.
- Tạo PDF khi người dùng xem hoặc in.
- Kho hóa đơn có lọc, tìm kiếm, chi tiết và thao tác hàng loạt.
- Xuất danh sách Excel theo bố cục mẫu của cổng Thuế.
- Xuất ZIP chứa XML và in/xuất PDF.
- Theo dõi tiến độ, tiếp tục sau gián đoạn và thử lại lỗi.
- Sao lưu và khôi phục độc lập dữ liệu của đúng doanh nghiệp đang hoạt động.
- Kích hoạt licence trực tuyến, kiểm tra quyền cục bộ và gia hạn xác thực khi có Internet.

### 3.2 Ngoài phạm vi phiên bản 1

- Tự động nhập tài khoản, mật khẩu hoặc giải CAPTCHA.
- Tải nền, đăng nhập hoặc xử lý đồng thời nhiều MST.
- Báo cáo, tìm kiếm hoặc xuất dữ liệu tổng hợp xuyên nhiều MST.
- Đồng bộ đám mây hoặc chia sẻ dữ liệu thời gian thực giữa nhiều máy.
- Chỉnh sửa nội dung XML hóa đơn.
- Lập, ký, phát hành, thay thế hoặc điều chỉnh hóa đơn.
- Kê khai thuế, nộp tờ khai hoặc đối chiếu sổ kế toán.
- Lập lịch tải tự động khi không có người đăng nhập.
- Mã hóa kho dữ liệu riêng của ứng dụng; phiên bản 1 dựa vào phân quyền Windows và chính sách bảo mật của doanh nghiệp.

## 4. Tài liệu và nguồn yêu cầu

- `CLAUDE.md`: mục tiêu dự án, kiến trúc đăng nhập, giới hạn .NET Framework 4.5 và phân lớp dự kiến.
- Workbook yêu cầu `DANH SÁCH HÓA ĐƠN.xlsx`: hành vi tra cứu/xuất/in/XML, ảnh tham chiếu màn hình cổng Thuế và mẫu danh sách Excel.
- Cổng Hóa đơn điện tử: <https://hoadondientu.gdt.gov.vn/>.
- Bộ wireframe: [HTML tương tác](../wireframes/index.html) và [danh mục PNG](../wireframes/README.md).

Các endpoint, header, payload, giới hạn tốc độ và cơ chế token của cổng Thuế chưa được coi là cố định. Nhóm triển khai phải xác minh bằng network traffic hợp lệ trên môi trường thật và lưu mẫu đã loại dữ liệu nhạy cảm trong `docs/api-samples/`.

## 5. Thuật ngữ

| Thuật ngữ | Ý nghĩa |
|---|---|
| Cổng Thuế | Website Hóa đơn điện tử của Tổng cục Thuế |
| Hóa đơn mua vào | Hóa đơn doanh nghiệp là bên mua/nhận hàng |
| Hóa đơn bán ra | Hóa đơn doanh nghiệp là bên bán/xuất hàng |
| Phiên tải | Một yêu cầu tải do người dùng khởi tạo cho một phạm vi và bộ lọc |
| Đoạn tải | Một phần của phiên tải, giới hạn trong một tháng |
| Checkpoint | Vị trí trang/cursor và số liệu đã hoàn thành để tiếp tục sau gián đoạn |
| Metadata | Dữ liệu mô tả hóa đơn được lập chỉ mục trong database |
| XML gốc | File XML lấy từ cổng Thuế, được lưu và kiểm tra mã băm |
| PDF phát sinh | Bản trình bày được tạo khi xem/in; có thể tạo lại từ dữ liệu hợp lệ |
| Kho hóa đơn | Màn hình tra cứu dữ liệu đã lưu cục bộ |
| Chế độ chuẩn (`StandardMode`) | Chế độ mặc định, chỉ quản lý một MST và không hiển thị chức năng đa doanh nghiệp |
| Chế độ dịch vụ (`ServiceMode`) | Chế độ được licence mở khóa để quản lý danh mục nhiều MST trên cùng máy |
| Chế độ dịch vụ chỉ đọc (`ReadOnlyServiceMode`) | Trạng thái licence không còn hiệu lực; cho xem/xuất/sao lưu dữ liệu cũ nhưng không thêm MST hoặc tải mới |
| MST đang hoạt động | Doanh nghiệp duy nhất có database, phiên đăng nhập và tác vụ được mở tại một thời điểm |
| Danh mục doanh nghiệp | Tệp định tuyến tối thiểu chứa MST, tên hiển thị, đường dẫn kho và số liệu tổng quan lưu đệm; không chứa hóa đơn |

## 6. Tổng quan sản phẩm

### 6.1 Kiến trúc logic

```text
WinForms UI
├── WebView đăng nhập thủ công
├── Tổng quan / Tải / Kho / Lịch sử / Cài đặt
├── Kích hoạt licence / Quản lý doanh nghiệp khi được cấp quyền
└── Trình xem XML/PDF
        │
Application Services
├── Licence và feature entitlement
├── Danh mục doanh nghiệp / chuyển CompanyContext
├── Quản lý phiên xác thực
├── Chia kỳ theo tháng
├── Hàng đợi tải và checkpoint
├── Chuẩn hóa/chống trùng/phiên bản hóa
├── Xuất Excel/ZIP và tạo PDF
└── Sao lưu/khôi phục
        │
Infrastructure
├── Portal API Client dùng token
├── Licence API Client và bộ kiểm tra chữ ký
├── Company catalog cục bộ
├── SQLite repository riêng theo MST
├── File archive riêng theo MST
└── Logging đã che dữ liệu nhạy cảm
```

UI không được tự thực hiện logic chia kỳ, phân trang hoặc ghi file. Các service phải độc lập với WinForms để có thể unit test.

### 6.2 Người dùng

| Vai trò | Mục tiêu |
|---|---|
| Kế toán thuế | Tải đủ hóa đơn theo kỳ, kiểm tra, tìm kiếm, xuất và lưu hồ sơ |
| Kế toán dịch vụ | Quản lý danh mục khách hàng và lần lượt xử lý từng MST mà không trộn dữ liệu |
| Quản trị máy/IT | Cài đặt, cấp quyền thư mục, sao lưu, hỗ trợ kết nối và cập nhật cấu hình |

Phiên bản 1 không có phân quyền nội bộ theo vai trò. Quyền truy cập phụ thuộc tài khoản Windows và tài khoản cổng Thuế.

### 6.3 Môi trường vận hành

- Windows có .NET Framework 4.5 và TLS 1.2.
- Máy có quyền ghi vào thư mục kho được cấu hình.
- Có kết nối HTTPS đến cổng Thuế.
- Có kết nối HTTPS đến máy chủ licence khi kích hoạt và xác thực định kỳ; được phép ngoại tuyến trong thời gian ân hạn.
- Có trình điều khiển WebView tương thích với cổng Thuế và .NET Framework 4.5.
- Có máy in Windows khi sử dụng chức năng in.

### 6.4 Ràng buộc

- Target framework là `net45`.
- Không dùng API runtime hiện đại không hỗ trợ .NET Framework 4.5.
- WebView chỉ phục vụ đăng nhập; truy xuất dữ liệu sử dụng API client trực tiếp.
- `ServicePointManager.SecurityProtocol` phải bật TLS 1.2 trước kết nối.
- Không giả định endpoint/payload khi chưa có mẫu traffic đã xác minh.
- Công nghệ WebView phải được quyết định bằng ADR sau một technical spike trên cổng Thuế thật.
- Licence chỉ mở quyền tính năng; kiểm tra quyền phải tồn tại ở cả UI và application service.
- Máy chủ licence không được nhận MST khách hàng, hóa đơn, token Thuế hoặc đường dẫn kho dữ liệu.

## 7. Quy tắc nghiệp vụ

| Mã | Quy tắc |
|---|---|
| BR-001 | Không có licence dịch vụ, mỗi cài đặt chỉ quản lý một MST và không hiển thị chức năng đa doanh nghiệp. |
| BR-002 | Ngày bắt đầu và kết thúc đều được tính vào phạm vi tra cứu. |
| BR-003 | Mỗi đoạn tải không được vượt qua ranh giới tháng. |
| BR-004 | Quý dùng quý dương lịch: Q1 01/01–31/03, Q2 01/04–30/06, Q3 01/07–30/09, Q4 01/10–31/12. |
| BR-005 | Năm được chia thành 12 đoạn tháng; khoảng tùy chọn được chia theo ranh giới tháng thực tế. |
| BR-006 | Các đoạn tải mặc định chạy tuần tự theo thời gian tăng dần. |
| BR-007 | Một đoạn chỉ hoàn tất khi API báo hết trang/cursor và mọi hóa đơn hợp lệ đã được ghi nhận. |
| BR-008 | XML được tải tự động; PDF chỉ tạo khi người dùng xem, in hoặc xuất PDF. |
| BR-009 | Tải lại cùng hóa đơn không tạo bản ghi trùng. |
| BR-010 | XML có nội dung khác mã băm được giữ thành phiên bản mới; phiên bản cũ không bị xóa. |
| BR-011 | Hủy phiên tải không xóa dữ liệu đã lưu thành công. |
| BR-012 | Token hết hạn làm phiên tải chuyển sang trạng thái chờ đăng nhập, không chuyển thành thất bại ngay. |
| BR-013 | Không tự động xóa hóa đơn, XML hoặc phiên bản lịch sử. |
| BR-014 | PDF không được coi là chứng từ gốc thay thế XML. |
| BR-015 | STT trong danh sách/Excel là số thứ tự hiển thị, không phải khóa dữ liệu. |
| BR-016 | Licence dịch vụ được cấp theo máy và không giới hạn số MST đã khai báo trên máy đó. |
| BR-017 | Chỉ một MST được hoạt động tại một thời điểm; không giữ nhiều phiên đăng nhập hoặc tải nền cho MST khác. |
| BR-018 | Mỗi MST phải có một thư mục và một SQLite riêng; hóa đơn không được lưu trong database dùng chung. |
| BR-019 | Khi chuyển MST, tác vụ hiện tại phải dừng an toàn, token bị xóa khỏi bộ nhớ và database cũ phải đóng trước khi mở kho mới. |
| BR-020 | MST trả về từ phiên cổng Thuế phải khớp MST đang hoạt động trước khi cho phép tải hoặc ghi dữ liệu. |
| BR-021 | `companies.json` chỉ giữ thông tin định tuyến và số liệu tổng quan lưu đệm, không chứa hóa đơn hay dữ liệu xác thực. |
| BR-022 | Hết licence không được làm mất quyền xem, in, xuất hoặc sao lưu dữ liệu đã lưu; các thao tác thêm MST và tải mới bị khóa. |
| BR-023 | Ngừng quản lý một doanh nghiệp chỉ loại hồ sơ khỏi danh mục, không tự động xóa thư mục dữ liệu. |
| BR-024 | Licence phải được xác thực trực tuyến khi kích hoạt và gắn với mã định danh máy đã băm. |
| BR-025 | Không gửi danh sách/số lượng MST hoặc dữ liệu nghiệp vụ tới máy chủ licence. |

## 8. Yêu cầu chức năng

### 8.1 Đăng nhập và phiên xác thực

| Mã | Yêu cầu |
|---|---|
| FR-AUTH-001 | Ứng dụng phải mở trang đăng nhập cổng Thuế trong WebView nhúng. |
| FR-AUTH-002 | Người dùng phải tự nhập tài khoản, mật khẩu, CAPTCHA và xác nhận đăng nhập. |
| FR-AUTH-003 | Ứng dụng không được tự điền, lưu hoặc ghi log mật khẩu/CAPTCHA. |
| FR-AUTH-004 | Sau đăng nhập thành công, ứng dụng phải thu token/cookie cần thiết từ request, storage hoặc cookie của WebView theo cơ chế đã xác minh. |
| FR-AUTH-005 | API client phải tái sử dụng phiên xác thực cho các request tra cứu/tải. |
| FR-AUTH-006 | Thanh trạng thái phải hiển thị `Chưa đăng nhập`, `Đã đăng nhập`, `Sắp hết hạn` nếu xác định được, hoặc `Cần đăng nhập lại`. |
| FR-AUTH-007 | Khi token hết hạn, ứng dụng phải tạm dừng các đoạn đang chạy, mở đăng nhập dạng lớp phủ và giữ checkpoint. |
| FR-AUTH-008 | Sau khi đăng nhập lại, ứng dụng phải quay về phiên tải trước và cho phép tiếp tục. |
| FR-AUTH-009 | Đăng xuất phải xóa token trong bộ nhớ, cookie phiên và dữ liệu WebView liên quan. |
| FR-AUTH-010 | Khi đóng/mở lại ứng dụng, người dùng phải đăng nhập lại; token không được lưu bền vững. |
| FR-AUTH-011 | Sau đăng nhập, ứng dụng phải đối chiếu MST của phiên cổng Thuế với MST đang hoạt động; nếu không khớp phải hủy phiên và không ghi dữ liệu. |
| FR-AUTH-012 | Chuyển MST phải xóa token/cookie của MST cũ trước khi mở màn hình đăng nhập cho MST mới. |

### 8.2 Chọn loại và phạm vi hóa đơn

| Mã | Yêu cầu |
|---|---|
| FR-SEARCH-001 | Người dùng phải chọn `Hóa đơn mua vào` hoặc `Hóa đơn bán ra`. |
| FR-SEARCH-002 | Người dùng phải chọn kiểu kỳ: tháng, quý, năm hoặc tùy chọn. |
| FR-SEARCH-003 | Tháng yêu cầu chọn tháng/năm; quý yêu cầu quý/năm; năm yêu cầu năm; tùy chọn yêu cầu từ ngày/đến ngày. |
| FR-SEARCH-004 | Ứng dụng phải từ chối ngày kết thúc nhỏ hơn ngày bắt đầu. |
| FR-SEARCH-005 | Ứng dụng phải hỗ trợ năm nhuận và số ngày thực tế của từng tháng. |
| FR-SEARCH-006 | Trước khi tải, ứng dụng phải hiển thị kế hoạch các đoạn tháng với ngày đầu/cuối của từng đoạn. |
| FR-SEARCH-007 | Bộ lọc phải phản ánh các trường có trên cổng Thuế khi API hỗ trợ: MST/CCCD đối tác, trạng thái hóa đơn, kết quả kiểm tra, số hóa đơn, hóa đơn ủy nhiệm, ký hiệu mẫu số và ký hiệu hóa đơn. |
| FR-SEARCH-008 | Bộ lọc không được làm thay đổi phạm vi ngày đã chọn ngoài ý muốn. |
| FR-SEARCH-009 | Người dùng phải có thể xóa bộ lọc và trở về giá trị mặc định. |
| FR-SEARCH-010 | Nút bắt đầu chỉ khả dụng khi đã đăng nhập, kỳ hợp lệ và thư mục lưu trữ có thể ghi. |

### 8.3 Hàng đợi tải, phân trang và checkpoint

| Mã | Yêu cầu |
|---|---|
| FR-JOB-001 | Mỗi lần bắt đầu phải tạo một `DownloadSession` và một `DownloadSegment` cho mỗi đoạn tháng. |
| FR-JOB-002 | Các đoạn mặc định chạy tuần tự; phiên bản 1 không chạy song song nhiều tháng. |
| FR-JOB-003 | Trong từng đoạn, ứng dụng phải tự gọi các trang/cursor/batch kế tiếp cho đến khi API xác nhận hết dữ liệu. |
| FR-JOB-004 | Số lượt gọi không được tính cứng theo tháng; phải dựa trên total, next cursor hoặc kích thước trang thực tế đã xác minh. |
| FR-JOB-005 | Màn hình tiến độ phải hiển thị tháng, trạng thái, trang/batch hiện tại, số hóa đơn đã nhận, số XML đã lưu và số lỗi. |
| FR-JOB-006 | Ứng dụng phải lưu checkpoint sau mỗi trang/batch và sau mỗi hóa đơn đã ghi thành công. |
| FR-JOB-007 | Người dùng phải có thể tạm dừng, tiếp tục hoặc hủy an toàn. |
| FR-JOB-008 | Lỗi mạng tạm thời phải được thử lại tối đa 3 lần với thời gian chờ tăng dần. |
| FR-JOB-009 | Người dùng phải có thể thử lại một đoạn lỗi mà không tải lại các đoạn đã hoàn tất. |
| FR-JOB-010 | Khi mở lại ứng dụng, các phiên chưa hoàn tất phải xuất hiện trong Tổng quan/Lịch sử với hành động `Tiếp tục`. |
| FR-JOB-011 | Hoàn tất phiên phải cung cấp hành động mở Kho hóa đơn với bộ lọc cùng loại và phạm vi. |
| FR-JOB-012 | Thời gian chờ và giới hạn tốc độ phải cấu hình được ở lớp hạ tầng, không đặt trong UI. |
| FR-JOB-013 | Tác vụ tải phải được gắn với MST và đường dẫn kho tại thời điểm tạo; không được tiếp tục khi `CompanyContext` không còn khớp. |
| FR-JOB-014 | Ứng dụng không được khởi chạy hoặc duy trì tác vụ tải cho MST không hoạt động. |

### 8.4 Chuẩn hóa, lưu XML và chống trùng

| Mã | Yêu cầu |
|---|---|
| FR-STORE-001 | Mỗi hóa đơn phải được chuẩn hóa thành metadata thống nhất giữa mua vào và bán ra. |
| FR-STORE-002 | Ứng dụng phải ưu tiên ID ổn định từ cổng Thuế; khi thiếu, dùng khóa nghiệp vụ gồm chiều hóa đơn, MST bên bán, ký hiệu mẫu số, ký hiệu hóa đơn, số hóa đơn và ngày lập. |
| FR-STORE-003 | Trước khi ghi file, ứng dụng phải tải vào file tạm, kiểm tra XML có thể đọc và tính SHA-256. |
| FR-STORE-004 | File tạm chỉ được đổi thành file chính thức sau khi kiểm tra thành công. |
| FR-STORE-005 | Database phải lưu đường dẫn tương đối, kích thước, SHA-256, thời điểm tải và trạng thái file. |
| FR-STORE-006 | Nếu khóa hóa đơn và SHA-256 đều trùng, ứng dụng chỉ cập nhật lần nhìn thấy cuối, không tạo phiên bản mới. |
| FR-STORE-007 | Nếu khóa hóa đơn trùng nhưng SHA-256 khác, ứng dụng phải tạo `InvoiceVersion` mới và giữ phiên bản trước. |
| FR-STORE-008 | Ứng dụng phải đánh dấu phiên bản hiện hành và hiển thị lịch sử phiên bản trong chi tiết hóa đơn. |
| FR-STORE-009 | Khi XML mất, không đọc được hoặc sai mã băm, Kho hóa đơn phải hiển thị cảnh báo cụ thể. |
| FR-STORE-010 | Hệ thống không được âm thầm thay XML lỗi bằng một file khác hoặc coi thiếu XML là thành công. |

### 8.5 Kho hóa đơn

| Mã | Yêu cầu |
|---|---|
| FR-ARCH-001 | Kho hóa đơn phải có tab/bộ lọc mua vào và bán ra. |
| FR-ARCH-002 | Người dùng phải lọc theo ngày lập, tháng/quý/năm, MST/tên đối tác, số hóa đơn, ký hiệu mẫu số, ký hiệu, trạng thái và kết quả kiểm tra. |
| FR-ARCH-003 | Người dùng phải tìm kiếm nhanh theo số hóa đơn, MST hoặc tên đối tác. |
| FR-ARCH-004 | Bảng phải hỗ trợ phân trang, sắp xếp, chọn một hoặc nhiều dòng và giữ bộ lọc khi quay lại từ chi tiết. |
| FR-ARCH-005 | Bảng phải cung cấp các cột từ mẫu Excel tại mục 9.1; người dùng có thể ẩn/hiện cột nhưng xuất Excel vẫn theo mẫu chuẩn. |
| FR-ARCH-006 | Thanh tóm tắt phải hiển thị số hóa đơn, tổng chưa thuế, tổng thuế và tổng thanh toán của tập kết quả hiện tại. |
| FR-ARCH-007 | Nhấp đúp hoặc `Xem chi tiết` phải mở SCR-06. |
| FR-ARCH-008 | Thao tác hàng loạt gồm xuất danh sách Excel, xuất ZIP XML và kiểm tra tính toàn vẹn file. |
| FR-ARCH-009 | Hóa đơn có file lỗi/thiếu phải có chỉ báo dễ nhận biết và bộ lọc riêng. |
| FR-ARCH-010 | Người dùng phải có thể sao chép số hóa đơn, MST và đường dẫn file mà không sửa dữ liệu. |

### 8.6 Chi tiết hóa đơn và trình xem

| Mã | Yêu cầu |
|---|---|
| FR-DETAIL-001 | Chi tiết phải hiển thị toàn bộ metadata, trạng thái file, nguồn phiên tải và lịch sử phiên bản. |
| FR-DETAIL-002 | Người dùng phải mở được XML hiện hành ở chế độ chỉ đọc. |
| FR-DETAIL-003 | Trình xem XML phải có chế độ cây và chế độ nội dung thô, đồng thời không thực thi script nhúng. |
| FR-DETAIL-004 | Khi chọn PDF, ứng dụng phải dùng bản đã tạo đúng phiên bản XML hoặc tạo mới nếu chưa có. |
| FR-DETAIL-005 | PDF phải hiển thị ngày tạo và SHA-256/ID phiên bản XML nguồn trong metadata nội bộ. |
| FR-DETAIL-006 | Người dùng phải có thể in qua hộp thoại in Windows và lưu bản sao PDF đến vị trí tự chọn. |
| FR-DETAIL-007 | Khi chuyển phiên bản XML, ứng dụng phải cảnh báo nếu PDF đang xem thuộc phiên bản khác. |

### 8.7 Xuất dữ liệu

| Mã | Yêu cầu |
|---|---|
| FR-EXPORT-001 | Xuất danh sách phải tạo file Excel theo tiêu đề, phạm vi ngày và thứ tự cột của mẫu cổng Thuế. |
| FR-EXPORT-002 | Số tiền phải là kiểu số; ngày phải có thể sắp xếp; mã số/ký hiệu/số hóa đơn phải bảo toàn số 0 đầu. |
| FR-EXPORT-003 | Excel phải chứa đúng tập kết quả hoặc các dòng người dùng đã chọn và ghi rõ phạm vi. |
| FR-EXPORT-004 | Xuất XML phải tạo ZIP chứa XML hiện hành của các hóa đơn được chọn. |
| FR-EXPORT-005 | ZIP phải kèm manifest gồm khóa hóa đơn, tên file, SHA-256 và trạng thái thành công/lỗi. |
| FR-EXPORT-006 | Nếu một XML thiếu/lỗi, ứng dụng phải hỏi người dùng xuất phần hợp lệ hay hủy; manifest phải ghi rõ dòng bị bỏ qua. |
| FR-EXPORT-007 | Tên file xuất phải chứa loại hóa đơn, phạm vi và thời điểm tạo. |
| FR-EXPORT-008 | Ứng dụng không được ghi đè file xuất đã tồn tại nếu chưa có xác nhận. |

### 8.8 Tổng quan và lịch sử

| Mã | Yêu cầu |
|---|---|
| FR-DASH-001 | Tổng quan phải hiển thị trạng thái đăng nhập, tổng hóa đơn đã lưu và lần đồng bộ gần nhất. |
| FR-DASH-002 | Tổng quan phải hiển thị phiên đang chạy/chờ đăng nhập/chờ tiếp tục và lỗi cần xử lý. |
| FR-DASH-003 | Các thẻ/chỉ báo phải mở đúng màn hình đã lọc khi người dùng chọn. |
| FR-HIST-001 | Lịch sử phải liệt kê thời điểm, loại, phạm vi, trạng thái, số hóa đơn mới/cập nhật/trùng/lỗi và người dùng Windows khởi tạo. |
| FR-HIST-002 | Mở một phiên phải dùng SCR-04 ở chế độ chi tiết. |
| FR-HIST-003 | Người dùng phải xem được lỗi theo đoạn/tháng và hành động đề xuất. |
| FR-HIST-004 | Người dùng phải mở Kho hóa đơn với bộ lọc của phiên. |
| FR-HIST-005 | Lịch sử không được xóa tự động trong phiên bản 1. |

### 8.9 Cài đặt, sao lưu và khôi phục

| Mã | Yêu cầu |
|---|---|
| FR-SET-001 | Lần chạy đầu phải yêu cầu nhập MST, tên doanh nghiệp và chọn thư mục kho; hồ sơ này trở thành doanh nghiệp mặc định. |
| FR-SET-002 | Sau khi kho có dữ liệu, MST của kho phải bị khóa; trong ServiceMode muốn dùng MST khác phải tạo hồ sơ và kho riêng. |
| FR-SET-003 | Ứng dụng phải kiểm tra quyền đọc/ghi và dung lượng trước khi lưu cấu hình thư mục. |
| FR-SET-004 | Cài đặt phải hiển thị dung lượng database, XML, PDF và bản xuất. |
| FR-SET-005 | Người dùng phải có thể tạo gói sao lưu nhất quán khi không có transaction ghi đang hoạt động. |
| FR-SET-006 | Gói sao lưu phải chứa database, XML, PDF đã tạo, manifest và phiên bản schema. |
| FR-SET-007 | Khôi phục phải kiểm tra MST, manifest, schema và mã băm trước khi thay đổi dữ liệu hiện hành. |
| FR-SET-008 | Trước khôi phục, ứng dụng phải tạo điểm sao lưu an toàn của dữ liệu hiện hành hoặc yêu cầu người dùng cung cấp vị trí. |
| FR-SET-009 | Cài đặt phải có hành động kiểm tra kết nối và mở thư mục kho. |

### 8.10 Quản lý doanh nghiệp và chuyển MST

| Mã | Yêu cầu |
|---|---|
| FR-COMP-001 | Chức năng quản lý nhiều doanh nghiệp chỉ được hiển thị khi entitlement là `ServiceMode` hoặc `ReadOnlyServiceMode`. |
| FR-COMP-002 | ServiceMode phải cung cấp SCR-10 để tìm và liệt kê doanh nghiệp theo tên hoặc MST. |
| FR-COMP-003 | Danh sách phải hiển thị tên, MST, kỳ tải gần nhất, thời điểm cập nhật, tổng số hóa đơn, dung lượng và trạng thái được lưu đệm. |
| FR-COMP-004 | Người dùng phải có thể thêm hồ sơ tại SCR-11 với MST, tên hiển thị và đường dẫn kho; MST không được trùng trong danh mục. |
| FR-COMP-005 | Mỗi hồ sơ phải trỏ tới một thư mục riêng chứa `company.db`; tạo hồ sơ không được mở đồng thời database của hồ sơ khác. |
| FR-COMP-006 | Header của SCR-02 đến SCR-09 trong ServiceMode phải hiển thị bộ chọn doanh nghiệp và MST đang hoạt động. |
| FR-COMP-007 | Chuyển MST phải kiểm tra tác vụ hiện tại; nếu đang chạy phải cho chọn tiếp tục chờ, tạm dừng hoặc hủy trước khi chuyển. |
| FR-COMP-008 | `CompanySwitchService` phải đóng tác vụ, xóa token, đóng SQLite cũ rồi mới tạo `CompanyContext` và mở SQLite mới. |
| FR-COMP-009 | Khi khởi động ServiceMode, ứng dụng phải mở SCR-10 để người dùng chủ động chọn MST; không tự mở MST được sử dụng gần nhất. |
| FR-COMP-010 | Người dùng phải có thể mở thư mục, sao lưu, sửa tên hiển thị/đường dẫn và ngừng quản lý từng doanh nghiệp. |
| FR-COMP-011 | Ngừng quản lý phải yêu cầu xác nhận và không xóa thư mục; xóa dữ liệu vật lý là quy trình riêng ngoài màn hình danh mục. |
| FR-COMP-012 | Nếu đường dẫn kho bị mất, ứng dụng phải cho xác định lại thư mục và xác minh MST/schema trước khi liên kết. |
| FR-COMP-013 | Khi nâng cấp từ StandardMode, kho hiện hữu phải được đăng ký làm doanh nghiệp đầu tiên mà không tải lại hoặc di chuyển dữ liệu nếu không cần. |
| FR-COMP-014 | Phiên bản này không được cung cấp danh sách hóa đơn, báo cáo hoặc export tổng hợp xuyên MST. |

### 8.11 Licence và quyền tính năng

| Mã | Yêu cầu |
|---|---|
| FR-LIC-001 | SCR-09 phải luôn có mục `Bản quyền`; StandardMode chỉ hiển thị thông tin ứng dụng và ô nhập licence key. |
| FR-LIC-002 | Ứng dụng phải gửi licence key, mã máy đã băm và phiên bản ứng dụng qua HTTPS để kích hoạt. |
| FR-LIC-003 | Phản hồi phải là entitlement có chữ ký gồm loại licence, mã máy, ngày phát hành, ngày hết hạn nếu có và tập tính năng. |
| FR-LIC-004 | Ứng dụng phải kiểm tra chữ ký và ràng buộc mã máy cục bộ mỗi lần khởi động. |
| FR-LIC-005 | Khi có Internet, ứng dụng phải xác thực lại với máy chủ; khi máy chủ tạm thời không khả dụng, được dùng ngoại tuyến tối đa 30 ngày kể từ lần xác thực thành công gần nhất. |
| FR-LIC-006 | Licence hợp lệ phải bật ServiceMode ngay mà không cần cài lại ứng dụng. |
| FR-LIC-007 | Licence hết hạn, bị thu hồi hoặc quá thời gian ngoại tuyến phải chuyển sang ReadOnlyServiceMode. |
| FR-LIC-008 | ReadOnlyServiceMode phải cho chuyển giữa MST đã có để xem, tìm, in, xuất và sao lưu; phải khóa thêm MST, đăng nhập Thuế, tải mới và sửa cấu hình nghiệp vụ quan trọng. |
| FR-LIC-009 | Người dùng phải có thể hủy kích hoạt trực tuyến để chuyển licence sang máy khác; trường hợp mất máy do nhà cung cấp reset. |
| FR-LIC-010 | Licence key đầy đủ không được ghi log; entitlement cục bộ phải được mã hóa theo máy và xác minh chữ ký. |
| FR-LIC-011 | Thay đổi ngày giờ hệ thống bất thường phải yêu cầu xác thực trực tuyến trước thao tác có quyền ServiceMode. |
| FR-LIC-012 | Việc kiểm tra entitlement phải được thực hiện ở application service, không chỉ bằng ẩn/hiện control giao diện. |

## 9. Dữ liệu

### 9.1 Trường hóa đơn và thứ tự xuất Excel

| STT | Trường | Kiểu/ghi chú |
|---:|---|---|
| 1 | STT | Số thứ tự hiển thị |
| 2 | Ký hiệu mẫu số | Text |
| 3 | Ký hiệu hóa đơn | Text |
| 4 | Số hóa đơn | Text để bảo toàn số 0 |
| 5 | Ngày lập | Date |
| 6 | MST người bán/MST người xuất hàng | Text |
| 7 | Tên người bán/Tên người xuất hàng | Text |
| 8 | MST người mua/MST người nhận hàng | Text |
| 9 | Tên người mua/Tên người nhận hàng | Text |
| 10 | Địa chỉ người mua | Text |
| 11 | Tổng tiền chưa thuế | Decimal |
| 12 | Tổng tiền thuế | Decimal |
| 13 | Tổng tiền chiết khấu thương mại | Decimal, có thể trống |
| 14 | Tổng tiền phí | Decimal, có thể trống |
| 15 | Tổng tiền thanh toán | Decimal |
| 16 | Đơn vị tiền tệ | Text/ISO currency khi có |
| 17 | Tỷ giá | Decimal, có thể trống |
| 18 | Trạng thái hóa đơn | Text chuẩn hóa kèm giá trị nguồn |
| 19 | Kết quả kiểm tra hóa đơn | Text chuẩn hóa kèm giá trị nguồn |

Ngoài các trường trên, database lưu ID cổng Thuế, chiều hóa đơn, thời điểm nhìn thấy đầu/cuối, dữ liệu trạng thái nguồn, khóa chống trùng và cờ toàn vẹn file.

### 9.2 Mô hình dữ liệu logic

| Bảng/thực thể | Mục đích | Khóa/liên kết chính |
|---|---|---|
| `CompanySettings` | MST, tên và cấu hình của kho hiện tại | Một bản ghi trong `company.db` của MST đó |
| `Invoice` | Metadata hiện hành và khóa tìm kiếm | `InvoiceId`, `PortalInvoiceId`, `BusinessKey` |
| `InvoiceVersion` | Các phiên bản XML của hóa đơn | Nhiều phiên bản thuộc một `Invoice` |
| `FileAsset` | XML/PDF, đường dẫn, hash, kích thước, trạng thái | Thuộc `InvoiceVersion` |
| `DownloadSession` | Yêu cầu tải cấp cao | Loại, kỳ, bộ lọc, trạng thái |
| `DownloadSegment` | Đoạn tải theo tháng | Thuộc `DownloadSession`; ngày đầu/cuối, cursor |
| `DownloadEvent` | Tiến độ, cảnh báo và lỗi đã che dữ liệu nhạy cảm | Thuộc session/segment |
| `ExportRecord` | File Excel/ZIP/PDF đã tạo | Phạm vi, đường dẫn, manifest, thời điểm |
| `SchemaInfo` | Phiên bản database và migration | Một bản ghi hiện hành |

Ngoài SQLite nghiệp vụ, ứng dụng có `companies.json` ở cấp cài đặt để định tuyến tới các kho. Tệp này chỉ gồm ID hồ sơ, MST, tên hiển thị, đường dẫn, cờ hoạt động gần nhất và số liệu tổng quan lưu đệm. `license.dat` lưu entitlement đã mã hóa theo máy và có chữ ký; hai tệp này không chứa hóa đơn hoặc phiên đăng nhập Thuế.

### 9.3 Trạng thái phiên tải

```text
Nháp → Đang chạy → Hoàn tất
              ├→ Hoàn tất có lỗi
              ├→ Tạm dừng bởi người dùng → Đang chạy
              ├→ Chờ đăng nhập → Đang chạy
              ├→ Đã hủy
              └→ Thất bại
```

`Thất bại` chỉ dùng khi không thể tiếp tục hoặc dữ liệu/cấu hình không hợp lệ. Lỗi mạng còn khả năng thử lại và hết token không được chuyển thẳng sang `Thất bại`.

### 9.4 Cấu trúc thư mục

```text
Documents\HoaDonDienTu\
├── companies.json
├── license.dat
└── Companies\
    └── <MST>\
        ├── Data\
        │   └── company.db
        ├── 2026\
        │   └── 04\
        │       ├── MuaVao\
        │       │   ├── XML\
        │       │   └── PDF\
        │       └── BanRa\
        │           ├── XML\
        │           └── PDF\
        ├── Exports\
        ├── Backups\
        └── Logs\
```

Mẫu tên file:

```text
<yyyyMMdd>_<MST-doi-tac>_<mau-so>_<ky-hieu>_<so-hoa-don>_<version>.xml
```

- Ký tự không hợp lệ với Windows phải được thay bằng `_`.
- Tên file phải có hậu tố chống va chạm khi các trường nghiệp vụ trùng.
- Database lưu đường dẫn tương đối để có thể di chuyển cả kho.
- File log không được chứa token hoặc toàn bộ XML.
- Mỗi thư mục `<MST>` là một đơn vị sao lưu, khôi phục và bàn giao độc lập.
- Danh mục không được dùng như database hóa đơn chung; số liệu tổng quan chỉ là cache cập nhật khi đóng/chuyển kho.

## 10. Đặc tả màn hình và điều hướng

Ứng dụng dùng thanh điều hướng bên trái sau đăng nhập. Header luôn hiển thị tên/MST doanh nghiệp, trạng thái đăng nhập và tác vụ đang chạy. Trong ServiceMode, tên/MST là bộ chọn mở SCR-10; trong StandardMode đây là nhãn tĩnh.

| Mã | Màn hình | Mục tiêu | Điều hướng vào | Điều hướng ra | Wireframe |
|---|---|---|---|---|---|
| SCR-01 | Đăng nhập | Đăng nhập thủ công, lấy token và xác minh đúng MST hoạt động | Khởi động; token hết hạn; sau khi chuyển MST | SCR-02; quay lại SCR-04 khi đăng nhập lại | [HTML](../wireframes/index.html?screen=SCR-01) · [PNG](../wireframes/png/SCR-01-login.png) |
| SCR-02 | Tổng quan | Trạng thái và công việc cần xử lý | Sau đăng nhập; menu | SCR-03/04/05/08/09 | [HTML](../wireframes/index.html?screen=SCR-02) · [PNG](../wireframes/png/SCR-02-dashboard.png) |
| SCR-03 | Tải hóa đơn | Cấu hình và bắt đầu phiên tải | Menu; hành động nhanh | SCR-04 | [HTML](../wireframes/index.html?screen=SCR-03) · [PNG](../wireframes/png/SCR-03-download.png) |
| SCR-04 | Tiến độ/chi tiết phiên | Theo dõi, dừng, tiếp tục, thử lại | SCR-03/02/08 | SCR-01 khi hết token; SCR-05 khi xem kết quả | [HTML](../wireframes/index.html?screen=SCR-04) · [PNG](../wireframes/png/SCR-04-progress.png) |
| SCR-05 | Kho hóa đơn | Tra cứu và thao tác hàng loạt | Menu; SCR-02/04/08 | SCR-06; xuất Excel/ZIP | [HTML](../wireframes/index.html?screen=SCR-05) · [PNG](../wireframes/png/SCR-05-archive.png) |
| SCR-06 | Chi tiết hóa đơn | Xem metadata, file và phiên bản | SCR-05 | SCR-05; SCR-07 | [HTML](../wireframes/index.html?screen=SCR-06) · [PNG](../wireframes/png/SCR-06-detail.png) |
| SCR-07 | Xem chứng từ | Xem XML/PDF và in | SCR-06 | SCR-06 | [HTML](../wireframes/index.html?screen=SCR-07) · [PNG](../wireframes/png/SCR-07-viewer.png) |
| SCR-08 | Lịch sử tải | Tìm phiên và xử lý lỗi | Menu; SCR-02 | SCR-04; SCR-05 | [HTML](../wireframes/index.html?screen=SCR-08) · [PNG](../wireframes/png/SCR-08-history.png) |
| SCR-09 | Cài đặt | Doanh nghiệp hoạt động, kho, sao lưu và lối vào bản quyền | Menu | SCR-02/12; hộp thoại sao lưu/khôi phục | [HTML](../wireframes/index.html?screen=SCR-09) · [PNG](../wireframes/png/SCR-09-settings.png) |
| SCR-10 | Quản lý doanh nghiệp | Danh mục, tìm kiếm và chọn MST hoạt động | Bộ chọn MST; sau kích hoạt licence | SCR-02/11; sao lưu/mở thư mục/ngừng quản lý | [HTML](../wireframes/index.html?screen=SCR-10) · [PNG](../wireframes/png/SCR-10-companies.png) |
| SCR-11 | Thêm/sửa doanh nghiệp | Khai báo MST và kho độc lập | SCR-10 | SCR-10; SCR-01 sau khi tạo và kích hoạt | [HTML](../wireframes/index.html?screen=SCR-11) · [PNG](../wireframes/png/SCR-11-company-edit.png) |
| SCR-12 | Bản quyền | Kích hoạt, xem trạng thái, xác thực và hủy kích hoạt | SCR-09 | SCR-09; SCR-10 sau kích hoạt | [HTML](../wireframes/index.html?screen=SCR-12) · [PNG](../wireframes/png/SCR-12-license.png) |

### 10.1 Yêu cầu chung cho màn hình

- Thao tác chính đặt ở góc phải phía dưới hoặc đầu khu vực nội dung và dùng nhãn động từ rõ ràng.
- Trạng thái không chỉ biểu đạt bằng màu; phải có chữ/biểu tượng.
- Bảng hỗ trợ bàn phím, chọn nhiều và giữ tiêu đề khi cuộn.
- Lỗi phải nêu đối tượng, nguyên nhân có thể hiểu được và hành động tiếp theo.
- Không hiển thị token, cookie hoặc dữ liệu xác thực kỹ thuật.
- Khi có tác vụ chạy, đóng ứng dụng phải hỏi `Tiếp tục chạy`, `Tạm dừng và thoát` hoặc `Hủy` theo khả năng kỹ thuật đã xác minh.
- Trong ServiceMode, tên và MST đang hoạt động phải luôn hiển thị tại các màn hình tải, xuất, khôi phục và thao tác dữ liệu.
- ReadOnlyServiceMode phải có nhãn `Chỉ đọc – cần gia hạn licence` và vô hiệu hóa hành động bị khóa bằng cả UI lẫn application service.

## 11. Giao diện bên ngoài

### 11.1 Cổng Thuế/API

- Giao tiếp HTTPS dùng TLS 1.2.
- Request phải mang token/cookie đúng cơ chế đã xác minh.
- API client phải tách biệt endpoint, header, serialization và mapping khỏi UI.
- Thay đổi schema không nhận diện được phải dừng đoạn tải với lỗi `Cổng Thuế đã thay đổi cấu trúc dữ liệu`, lưu thông tin kỹ thuật đã che dữ liệu nhạy cảm và không ghi metadata sai.
- Mẫu request/response dùng cho test phải loại token, cookie, MST thật, CCCD, tên, địa chỉ và nội dung hóa đơn thật.

### 11.2 WebView

- Chỉ mở domain cổng Thuế và tài nguyên cần thiết cho quy trình đăng nhập.
- Điều hướng ra domain ngoài phải bị chặn hoặc yêu cầu xác nhận.
- Không chèn script để tự giải CAPTCHA hoặc tự gửi thông tin đăng nhập.
- Control được chọn phải tải được cổng thật, cho phép quan sát token/cookie hợp lệ và tương thích `net45`.

### 11.3 File và hệ điều hành

- Sử dụng hộp thoại chuẩn Windows để chọn thư mục, lưu file và in.
- Ghi file phải atomic khi hệ thống file hỗ trợ: file tạm cùng volume rồi rename.
- Thông báo rõ khi đường dẫn quá dài, file bị khóa, không đủ quyền hoặc hết dung lượng.

### 11.4 Máy chủ licence

- Giao tiếp HTTPS và pin/kiểm tra chứng thư theo chính sách phát hành được duyệt.
- Request kích hoạt chỉ gồm licence key, mã máy đã băm và phiên bản ứng dụng; không gồm MST hoặc dữ liệu cổng Thuế.
- Response phải có chữ ký bất đối xứng để ứng dụng xác minh ngoại tuyến bằng public key nhúng.
- API phải hỗ trợ kích hoạt, xác thực trạng thái và hủy kích hoạt thiết bị.
- Timeout/lỗi 5xx không được đồng nghĩa licence bị thu hồi; ứng dụng áp dụng thời gian ngoại tuyến còn lại.

## 12. Yêu cầu phi chức năng

### 12.1 Bảo mật

| Mã | Yêu cầu |
|---|---|
| NFR-SEC-001 | Token chỉ tồn tại trong bộ nhớ của phiên chạy và không được ghi bền vững. |
| NFR-SEC-002 | Log phải che Authorization, cookie, CAPTCHA, mật khẩu, CCCD và dữ liệu XML. |
| NFR-SEC-003 | Trình xem XML không được thực thi script, external entity hoặc tài nguyên ngoài không được phép. |
| NFR-SEC-004 | Khôi phục/sao lưu phải kiểm tra đúng MST và tính toàn vẹn manifest. |
| NFR-SEC-005 | Ứng dụng phải tuân theo quyền truy cập file của Windows, không tự mở rộng quyền. |
| NFR-SEC-006 | Mọi thao tác ghi phải xác minh `CompanyContext.MST` khớp hồ sơ kho và MST phiên Thuế. |
| NFR-SEC-007 | `license.dat` phải được mã hóa bằng cơ chế gắn với máy và vẫn phải xác minh chữ ký máy chủ sau khi giải mã. |
| NFR-SEC-008 | Máy chủ licence và log kích hoạt không được nhận MST, số lượng khách hàng hoặc dữ liệu hóa đơn. |

### 12.2 Hiệu năng và khả năng mở rộng

| Mã | Yêu cầu mục tiêu |
|---|---|
| NFR-PERF-001 | UI không được treo trong lúc gọi mạng, tính hash, xuất file hoặc tạo PDF. |
| NFR-PERF-002 | Với kho 100.000 hóa đơn trên máy đáp ứng cấu hình triển khai, tìm theo số/MST và mở trang đầu không quá 2 giây ở percentile 95. |
| NFR-PERF-003 | Mở ứng dụng đến màn hình đăng nhập/tổng quan không quá 5 giây trong điều kiện thông thường. |
| NFR-PERF-004 | Bảng chỉ tải trang dữ liệu cần hiển thị; không nạp toàn bộ XML vào bộ nhớ. |
| NFR-PERF-005 | Xuất ZIP/Excel phải hiển thị tiến độ và cho phép hủy an toàn. |
| NFR-PERF-006 | SCR-10 phải hiển thị danh mục từ cache mà không mở đồng thời SQLite của nhiều MST. |
| NFR-PERF-007 | Chuyển giữa hai kho hợp lệ không có tác vụ chạy phải hoàn tất trong 5 giây ở điều kiện thông thường. |

### 12.3 Độ tin cậy

| Mã | Yêu cầu |
|---|---|
| NFR-REL-001 | Mất điện/đóng tiến trình không được làm mất các hóa đơn đã commit thành công. |
| NFR-REL-002 | Checkpoint phải cho phép tiếp tục mà không tạo bản ghi trùng. |
| NFR-REL-003 | Database phải bật cơ chế transaction và kiểm tra integrity trong quy trình chẩn đoán/sao lưu. |
| NFR-REL-004 | File XML phải có SHA-256 và trạng thái kiểm tra gần nhất. |
| NFR-REL-005 | Ứng dụng không được báo phiên hoàn tất khi còn đoạn chưa hoàn tất mà không ghi `Hoàn tất có lỗi`. |
| NFR-REL-006 | Lỗi hoặc đóng tiến trình trong lúc chuyển MST không được để hai database cùng hoạt động; lần mở sau chọn kho hợp lệ cuối cùng. |
| NFR-REL-007 | Mất/corrupt `companies.json` không được tạo kho rỗng đè lên dữ liệu; phải cho quét/liên kết lại các kho hiện hữu. |

### 12.4 Khả dụng

| Mã | Yêu cầu |
|---|---|
| NFR-USE-001 | Ngôn ngữ giao diện là tiếng Việt, thuật ngữ nhất quán với cổng Thuế và nghiệp vụ kế toán. |
| NFR-USE-002 | Các thao tác thường xuyên phải thực hiện được bằng bàn phím. |
| NFR-USE-003 | Mỗi màn hình chỉ có một hành động chính nổi bật. |
| NFR-USE-004 | Bộ lọc Kho hóa đơn phải được giữ trong phiên làm việc khi xem chi tiết rồi quay lại. |
| NFR-USE-005 | Tiến độ và lỗi phải đọc được mà không phụ thuộc riêng vào màu sắc. |
| NFR-USE-006 | StandardMode không được hiển thị bộ chọn MST hoặc màn hình quản lý doanh nghiệp; mục `Bản quyền` vẫn truy cập được. |
| NFR-USE-007 | Mọi hộp thoại chuyển, sao lưu hoặc khôi phục phải nêu rõ tên và MST bị tác động. |

### 12.5 Bảo trì và kiểm thử

| Mã | Yêu cầu |
|---|---|
| NFR-MNT-001 | Chia kỳ, state machine, chống trùng và quy tắc tên file phải độc lập UI và có unit test. |
| NFR-MNT-002 | API client phải cho phép thay endpoint/mapping có kiểm soát khi cổng thay đổi. |
| NFR-MNT-003 | Database schema phải có version và migration có thể rollback bằng backup. |
| NFR-MNT-004 | Log phải có correlation ID theo session/segment nhưng không chứa token. |
| NFR-MNT-005 | Build/release phải ghi rõ phiên bản ứng dụng, schema và WebView engine. |
| NFR-MNT-006 | `LicenseService`, `CompanyCatalog`, `CompanySwitchService`, `CompanyContext` và `StorageGuard` phải tách khỏi WinForms và có test tự động. |

## 13. Xử lý lỗi

| Nhóm | Hành vi | Thông điệp/hành động |
|---|---|---|
| Token hết hạn | Tạm dừng hàng đợi | `Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục từ tháng ...` |
| Mạng tạm thời | Thử lại tối đa 3 lần | Hiển thị lần thử và nút `Thử lại ngay` khi hết lượt |
| Cổng thay đổi schema | Dừng đoạn, giữ checkpoint | `Không nhận diện được dữ liệu từ cổng Thuế. Cần cập nhật phần mềm.` |
| API từ chối bộ lọc | Dừng đoạn liên quan | Hiển thị tháng, bộ lọc và mã phản hồi đã làm sạch |
| XML lỗi | Không commit file chính thức | Đánh dấu hóa đơn/đoạn lỗi và cho thử tải XML lại |
| Hết dung lượng | Tạm dừng trước lần ghi tiếp | Hiển thị đường dẫn và dung lượng cần giải phóng ước tính |
| Không có quyền ghi | Tạm dừng | Cho chọn thư mục khác hoặc mở Cài đặt |
| File bị sửa/mất | Không xóa metadata | Cảnh báo trong Kho và cho kiểm tra/tải lại |
| Database lỗi | Dừng thao tác ghi | Đề nghị sao lưu file hiện trạng và chạy chẩn đoán/khôi phục |
| Licence gắn máy khác | Không kích hoạt | Hướng dẫn hủy kích hoạt máy cũ hoặc yêu cầu nhà cung cấp reset |
| Máy chủ licence tạm lỗi | Dùng thời gian ngoại tuyến còn lại | Hiển thị lần xác thực cuối và ngày cần kết nối lại |
| Licence hết hạn/thu hồi | Chuyển chỉ đọc | Cho xem/in/xuất/sao lưu; hướng dẫn gia hạn hoặc kích hoạt lại |
| MST đăng nhập không khớp | Xóa phiên, không ghi dữ liệu | Hiển thị MST đang chọn và yêu cầu đăng nhập đúng doanh nghiệp |
| Trùng MST trong danh mục | Không tạo kho mới | Mở hồ sơ đã tồn tại hoặc cho sửa đường dẫn |
| Mất đường dẫn kho | Không tạo database thay thế | Cho xác định lại thư mục và kiểm tra MST/schema |
| SQLite kho khác đang mở | Hủy chuyển MST | Đóng an toàn kho cũ hoặc khởi động lại sau khi phục hồi trạng thái |

Mọi lỗi người dùng nhìn thấy phải có: đối tượng bị ảnh hưởng, thời điểm, bước thất bại, dữ liệu đã được giữ hay chưa và hành động tiếp theo.

## 14. Tiêu chí nghiệm thu

### AC-01 — Đăng nhập

1. Mở ứng dụng chưa có token.
2. SCR-01 tải trang đăng nhập cổng Thuế.
3. Người dùng tự nhập thông tin và CAPTCHA.
4. Sau đăng nhập, SCR-02 hiển thị đúng MST cấu hình và trạng thái `Đã đăng nhập`.
5. Không tìm thấy mật khẩu, CAPTCHA hoặc token trong database/log.

### AC-02 — Tải theo năm

1. Chọn hóa đơn mua vào, năm 2026.
2. Ứng dụng hiển thị 12 đoạn từ tháng 01 đến tháng 12.
3. Bắt đầu tải; mỗi đoạn tự phân trang đến hết.
4. Tiến độ thể hiện riêng từng tháng.
5. XML được lưu tự động; không tạo PDF nếu chưa xem/in.
6. Phiên kết thúc `Hoàn tất` hoặc `Hoàn tất có lỗi` đúng thực tế.

### AC-03 — Khoảng tùy chọn qua ranh giới tháng

Với 15/01/2026–10/03/2026, kế hoạch phải gồm đúng:

- 15/01/2026–31/01/2026
- 01/02/2026–28/02/2026
- 01/03/2026–10/03/2026

### AC-04 — Tải lại và chống trùng

1. Tải hoàn tất một kỳ.
2. Tải lại cùng kỳ và bộ lọc.
3. Số hóa đơn không tăng nếu dữ liệu nguồn không đổi.
4. Phiên lịch sử ghi nhận số bản trùng/đã có.

### AC-05 — XML thay đổi

Khi cùng khóa hóa đơn trả về XML khác hash, hệ thống giữ cả hai phiên bản, đánh dấu phiên bản hiện hành và không ghi đè lịch sử.

### AC-06 — Hết token giữa phiên

1. Token hết hạn khi đang tải một tháng.
2. Phiên chuyển `Chờ đăng nhập`, checkpoint được giữ.
3. Sau đăng nhập lại, người dùng tiếp tục từ checkpoint.
4. Không tạo bản ghi trùng và không tải lại các tháng đã hoàn tất.

### AC-07 — Mất mạng/đóng ứng dụng

Sau khi mạng gián đoạn hoặc ứng dụng bị đóng giữa phiên, mở lại phải hiển thị phiên chưa hoàn tất và cho tiếp tục; dữ liệu đã commit vẫn tìm được và có XML hợp lệ.

### AC-08 — Xuất Excel

File xuất có tiêu đề/phạm vi, đúng 19 cột theo mục 9.1, số tiền là numeric, ngày có thể sắp xếp và mã/số hóa đơn bảo toàn định dạng.

### AC-09 — Xuất ZIP XML

ZIP chứa XML hiện hành của các hóa đơn được chọn và manifest có SHA-256. File thiếu/lỗi được thông báo và ghi trong manifest theo lựa chọn của người dùng.

### AC-10 — Xem/in PDF

Mở một hóa đơn chưa có PDF sẽ tạo PDF từ phiên bản XML hiện hành; in dùng hộp thoại Windows. XML gốc không bị sửa.

### AC-11 — Sao lưu/khôi phục

Gói sao lưu có manifest hợp lệ và khôi phục được trên kho cùng MST; gói khác MST bị từ chối trước khi thay đổi dữ liệu.

### AC-12 — Ẩn tính năng khi chưa có licence

1. Khởi động với StandardMode.
2. SCR-02 đến SCR-09 không hiển thị bộ chọn MST hoặc lối vào SCR-10/11.
3. `Cài đặt > Bản quyền` vẫn mở SCR-12 và cho nhập key.
4. Các application service từ chối lệnh thêm/chuyển MST nếu gọi ngoài UI.

### AC-13 — Kích hoạt ServiceMode

1. Nhập key hợp lệ trên SCR-12 khi có Internet.
2. Server nhận key, mã máy đã băm và phiên bản ứng dụng; không nhận MST/dữ liệu hóa đơn.
3. Ứng dụng xác minh chữ ký, lưu `license.dat` và bật bộ chọn MST/SCR-10 mà không cài lại.
4. Kho StandardMode hiện hữu xuất hiện như doanh nghiệp đầu tiên và dữ liệu không bị di chuyển hoặc tải lại ngoài nhu cầu migration.

### AC-14 — Thêm và chuyển MST

1. Tạo hai hồ sơ MST A và B với hai thư mục/SQLite khác nhau.
2. Đăng nhập và tải một hóa đơn cho A.
3. Chuyển sang B; token A và kết nối database A bị đóng trước khi mở B.
4. Đăng nhập nhầm tài khoản A khi B đang hoạt động bị từ chối, không có file/bản ghi nào được ghi vào kho B.
5. Quay lại A vẫn thấy đúng hóa đơn và lịch sử của A; B không hiển thị dữ liệu của A.

### AC-15 — Chuyển MST khi có tác vụ

Khi A đang tải, yêu cầu chuyển sang B phải buộc người dùng chờ, tạm dừng hoặc hủy. B chỉ được kích hoạt sau khi checkpoint A được ghi, tác vụ dừng và database A đóng an toàn.

### AC-16 — Licence mất hiệu lực

1. Licence hết hạn, bị thu hồi hoặc quá 30 ngày không xác thực.
2. Ứng dụng chuyển ReadOnlyServiceMode và vẫn cho mở từng MST đã có để xem, tìm, in, xuất và sao lưu.
3. Thêm MST, đăng nhập Thuế, tải mới và sửa cấu hình nghiệp vụ bị từ chối ở cả UI và application service.
4. Kích hoạt/gia hạn thành công khôi phục ServiceMode mà không chuyển đổi dữ liệu.

### AC-17 — Ngừng quản lý và khôi phục liên kết

Ngừng quản lý một MST loại hồ sơ khỏi `companies.json` nhưng giữ nguyên thư mục. Người dùng có thể liên kết lại đúng thư mục sau khi ứng dụng xác minh MST, schema và tính toàn vẹn.

## 15. Ma trận truy vết màn hình–chức năng

| Nhóm yêu cầu | SCR-01 | SCR-02 | SCR-03 | SCR-04 | SCR-05 | SCR-06 | SCR-07 | SCR-08 | SCR-09 | SCR-10 | SCR-11 | SCR-12 |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| AUTH | X | X |  | X |  |  |  |  | X |  | X |  |
| SEARCH |  |  | X | X |  |  |  |  |  |  |  |  |
| JOB |  | X | X | X |  |  |  | X |  | X |  |  |
| STORE |  |  |  | X | X | X | X |  | X | X | X |  |
| ARCHIVE |  | X |  | X | X | X |  | X |  |  |  |  |
| DETAIL/PDF |  |  |  |  | X | X | X |  |  |  |  |  |
| EXPORT |  |  |  |  | X | X | X |  |  | X |  |  |
| HISTORY |  | X |  | X | X |  |  | X |  | X |  |  |
| SETTINGS/BACKUP |  |  |  |  |  |  |  |  | X | X | X | X |
| COMPANY | X | X | X | X | X | X | X | X | X | X | X |  |
| LICENCE |  |  |  |  |  |  |  |  | X | X | X | X |

## 16. Kiểm thử bắt buộc

### 16.1 Unit test

- Chia kỳ: một tháng, nhiều tháng, quý, năm, năm nhuận, ngày đầu/cuối tháng.
- State machine của session/segment.
- Checkpoint và idempotency khi thử lại.
- Khóa chống trùng và phiên bản hóa theo hash.
- Chuẩn hóa trường mua vào/bán ra.
- Quy tắc tên file và ký tự Windows không hợp lệ.
- Mapping trạng thái/kết quả kiểm tra.
- Manifest ZIP/backup.
- Chuyển trạng thái entitlement Standard/Service/ReadOnlyService.
- Trình tự đóng/mở `CompanyContext` và từ chối context/MST không khớp.
- Đọc/ghi atomic `companies.json`, phát hiện trùng MST và cache tổng quan.
- Kiểm tra chữ ký licence, mã máy, hạn dùng, thời gian ngoại tuyến và lệch đồng hồ.

### 16.2 Integration test

- API client với mẫu request/response đã làm sạch.
- SQLite transaction, migration và tìm kiếm 100.000 bản ghi.
- Ghi file atomic, file bị khóa, hết dung lượng và sai quyền.
- Excel/ZIP/PDF với dữ liệu tiếng Việt, số 0 đầu và ngoại tệ.
- Token hết hạn, 401/403, timeout, lỗi 5xx và schema thay đổi.
- Kích hoạt/xác thực/hủy kích hoạt licence với server giả lập và lỗi mạng.
- Hai kho MST độc lập, chuyển qua lại, database/file bị khóa và đường dẫn kho bị mất.
- Nâng cấp kho StandardMode thành hồ sơ đầu tiên trong ServiceMode.

### 16.3 UAT trên cổng thật

- Đăng nhập thủ công và lấy token.
- Mua vào/bán ra trong kỳ có dữ liệu.
- Kỳ không có dữ liệu.
- Nhiều trang trong một tháng.
- Xuất Excel so với mẫu cổng.
- XML ZIP và PDF/in.
- Đăng nhập lại giữa phiên và tiếp tục.
- Đăng nhập đúng/sai MST sau khi chuyển doanh nghiệp.
- Xác minh chỉ có một phiên Thuế và một SQLite hoạt động tại mọi thời điểm.

## 17. Các cổng kỹ thuật trước khi triển khai

Các mục dưới đây không làm thay đổi yêu cầu nghiệp vụ nhưng phải được hoàn thành trước khi chốt kiến trúc triển khai:

1. **WebView ADR:** thử nghiệm tối thiểu WebBrowser/CefSharp hoặc lựa chọn phù hợp khác trên máy đích, kiểm tra render cổng Thuế, đăng nhập, CAPTCHA, cookie/token và khả năng phân phối với `net45`.
2. **API discovery:** ghi lại endpoint, method, header, pagination, giới hạn ngày, page size, error schema và cách tải XML/PDF; loại dữ liệu nhạy cảm trước khi commit mẫu.
3. **PDF strategy:** xác định dùng PDF/print endpoint của cổng hay render XML theo mẫu đã xác minh; kết quả phải nhất quán với dữ liệu XML.
4. **Excel fidelity:** so sánh file xuất với workbook mẫu về cột, kiểu dữ liệu, tiêu đề và phạm vi ngày.
5. **Rate limit:** xác định nhịp gọi an toàn và hành vi khi nhận 429/giới hạn tương đương.
6. **Licence protocol:** chốt API, thuật toán chữ ký, tạo mã máy, mã hóa cục bộ, chu kỳ xác thực, hủy/reset thiết bị và chính sách lệch đồng hồ.
7. **Company switch safety:** technical spike chứng minh đóng tác vụ/token/SQLite theo đúng thứ tự và phục hồi an toàn sau crash.

Mỗi cổng phải có bằng chứng thử nghiệm và quyết định được lưu trong `docs/decisions/` trước khi code phụ thuộc vào lựa chọn đó.

## 18. Phê duyệt thiết kế

Các quyết định đã được xác nhận:

- Một ứng dụng có hai chế độ: StandardMode một MST và ServiceMode đa MST được licence mở khóa.
- Licence theo máy, kích hoạt trực tuyến, không giới hạn số MST và không gửi danh sách MST lên máy chủ licence.
- Chỉ một MST hoạt động tại một thời điểm; không tải nền hoặc giữ đồng thời nhiều phiên đăng nhập.
- Mỗi MST có thư mục và SQLite riêng; `companies.json` chỉ là danh mục định tuyến/cache.
- Tính năng đa MST được ẩn trong StandardMode; mục kích hoạt licence vẫn luôn hiện.
- Licence mất hiệu lực chuyển sang chỉ đọc, vẫn cho xem/in/xuất/sao lưu dữ liệu cũ.
- Ứng dụng nghiệp vụ kế toán với Kho hóa đơn làm trung tâm.
- Thanh điều hướng bên trái.
- XML lưu tự động; PDF tạo khi xem/in.
- SQLite kết hợp kho file có cấu trúc.
- Tải tuần tự theo tháng, có checkpoint và tiếp tục.
- Wireframe được bàn giao ở cả HTML và PNG.
