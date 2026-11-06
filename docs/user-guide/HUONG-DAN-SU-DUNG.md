# Hướng dẫn sử dụng — Hóa đơn điện tử

Ứng dụng giúp tra cứu, tải, lưu và xuất hóa đơn điện tử của doanh nghiệp từ cổng Thuế (hoadondientu.gdt.gov.vn). Cổng Thuế chỉ cho tra cứu một tháng mỗi lần; ứng dụng cho chọn **tháng, quý, năm hoặc khoảng ngày bất kỳ** rồi tự chia thành từng tháng để tải.

> **Lưu ý về phiên bản này:** chức năng tải hóa đơn từ cổng Thuế thật chưa được kiểm thử trên cổng thật (chỉ trên dữ liệu mô phỏng). Hãy thử với một kỳ nhỏ trước, đối chiếu số lượng với cổng Thuế, và xem [ghi chú phát hành](../release/RELEASE-NOTES-1.0.0.md).

## 1. Cài đặt và mở ứng dụng

1. Giải nén file `HoaDonDienTu-<phiên bản>-win-x86.zip` vào một thư mục, ví dụ `C:\HoaDonDienTu`. Không cần quyền quản trị.
2. Chạy `HoaDonDienTu.exe`.
3. Cập nhật lên bản mới: đóng ứng dụng rồi giải nén đè bản mới. **Dữ liệu của bạn nằm ngoài thư mục chương trình** (nhật ký và cấu hình ở `%LOCALAPPDATA%\HoaDonDienTu`, kho hóa đơn ở thư mục bạn chọn) nên không bị mất.

Yêu cầu: Windows 7 trở lên, .NET Framework 4.5.2, khoảng 210 MB ổ đĩa cho chương trình cộng chỗ cho kho hóa đơn. Theo tài liệu của trình duyệt nhúng, máy có thể cần Microsoft Visual C++ 2019 Redistributable (x86) nếu chưa có.

## 2. Lần chạy đầu: khai báo doanh nghiệp

Lần đầu ứng dụng mở màn hình **Khai báo doanh nghiệp**:

- **Mã số thuế** (10 hoặc 13 số): dùng để kiểm tra bạn đăng nhập đúng doanh nghiệp.
- **Tên hiển thị:** đặt tùy ý, đổi được sau.
- **Thư mục kho:** nơi lưu toàn bộ dữ liệu của doanh nghiệp này (XML hóa đơn, cơ sở dữ liệu, bản xuất, bản sao lưu). Chọn thư mục trống hoặc kho cũ của đúng MST này; nên đặt ở ổ có nhiều chỗ trống.

Ứng dụng kiểm tra quyền ghi, dung lượng và MST trước khi lưu. **Không có mật khẩu nào được lưu ở bước này.**

## 3. Đăng nhập cổng Thuế

Cổng Thuế có mã xác nhận (CAPTCHA) nên ứng dụng **không tự đăng nhập**:

1. Bấm **Đăng nhập** (nút ở góc phải phía trên, hoặc thẻ *Trang chủ* → nhóm *Cổng Thuế*).
2. Cửa sổ đăng nhập mở trang của cổng Thuế. **Bạn tự nhập tài khoản, mật khẩu và mã xác nhận.**
3. Đăng nhập xong cửa sổ tự đóng. Ứng dụng kiểm tra phiên đúng **MST của doanh nghiệp đang mở**; nếu khác sẽ báo và không cho tải.

Ứng dụng chỉ giữ phiên đăng nhập trong bộ nhớ khi đang chạy; **không ghi mật khẩu, mã xác nhận hay mã phiên xuống ổ đĩa** và không lưu vào nhật ký. Khi phiên hết hạn, bấm **Đăng nhập** lại. Nút **Tải hóa đơn** chỉ mở khi đã đăng nhập.

## 4. Tải hóa đơn

Vào **Tải hóa đơn**:

1. **Loại hóa đơn và kỳ tải:** chọn *Hóa đơn mua vào* hoặc *Hóa đơn bán ra*; *Kiểu kỳ* là **Tháng**, **Quý**, **Năm** hoặc **Tùy chọn** (từ ngày – đến ngày). Phần của kỳ nằm trong tương lai sẽ không tải.
2. **Bộ lọc nâng cao** (có thể bỏ trống): MST/CCCD người mua, số hóa đơn, trạng thái, kết quả kiểm tra, ký hiệu mẫu số, ký hiệu hóa đơn, hóa đơn ủy nhiệm. **Xóa bộ lọc** để bỏ hết.
3. **Kế hoạch tải:** bảng cho thấy kỳ được chia thành các đoạn theo tháng.
4. Bấm **Bắt đầu tải.**

Màn **Tiến độ tải** cho thấy tiến độ, số hóa đơn nhận được, số XML đã lưu và thời gian. Bạn có thể **Tạm dừng**, **Tiếp tục** hoặc **Hủy phiên**. Tạm dừng, đóng ứng dụng hay mất mạng đều ghi lại điểm dừng; lần sau tiếp tục từ chỗ đã dừng **mà không tạo hóa đơn trùng**. Đoạn nào lỗi thì phiên báo *Hoàn tất có lỗi* và có nút **Thử lại các lỗi**. Khi xong, **Mở Kho hóa đơn** hoặc **Tải kỳ khác**.

Hóa đơn đã tải được lưu nguyên văn dưới dạng XML. Tải lại cùng kỳ không tạo bản trùng; nếu cổng Thuế trả nội dung khác thì ứng dụng lưu thành **phiên bản mới** và giữ cả bản cũ.

Ứng dụng tự giãn khoảng cách giữa các lần gọi cổng Thuế để tránh bị chặn; một kỳ lớn (cả năm) sẽ mất thời gian.

## 5. Kho hóa đơn

**Kho hóa đơn** hiển thị hóa đơn đã tải của doanh nghiệp đang mở.

- **Mua vào / Bán ra:** hai nút cạnh tiêu đề.
- **Ô tìm nhanh:** gõ số hóa đơn, MST/CCCD hoặc tên đối tác; không phân biệt hoa thường và dấu tiếng Việt.
- **Bộ lọc:** năm, trạng thái, kết quả kiểm tra, tình trạng file; **Xóa lọc** để bỏ hết.
- **Thẻ tổng hợp:** số hóa đơn, tổng chưa thuế, tổng thuế, tổng thanh toán của kết quả đang lọc.
- Bấm tiêu đề cột để sắp xếp; chọn nhiều dòng bằng Ctrl/Shift; bấm đúp một dòng để mở chi tiết.

**Xuất Excel** tạo bảng kê theo mẫu của cổng Thuế; **Xuất ZIP XML** tạo gói chứa file XML (kèm `manifest.csv` có mã SHA-256 khi xuất nhiều hóa đơn). Nếu bạn đã chọn dòng thì chỉ xuất các dòng đó, không thì xuất toàn bộ kết quả đang lọc. Hộp thoại lưu nêu rõ tên và MST của doanh nghiệp. Khi không có dòng nào, các nút xuất và **Kiểm tra file** bị khóa. Mỗi lần xuất xong được ghi vào lịch sử xuất của doanh nghiệp.

**Kiểm tra file** kiểm lại file XML trên đĩa: thiếu file, file bị sửa (sai SHA-256), không phải XML, file lạ không thuộc hóa đơn nào, file tạm sót. Kết quả hiện trong báo cáo; **Xóa file tạm** chỉ xóa các file `.tmp` sót trong kho. Hóa đơn có vấn đề được đánh dấu *Thiếu XML* hoặc *XML lỗi* ở cột File và ở Tổng quan.

## 6. Chi tiết hóa đơn và xem chứng từ

Bấm đúp một hóa đơn để mở **Chi tiết hóa đơn**: thông tin hóa đơn, bên bán/bên mua, giá trị, file chứng từ, lịch sử các phiên bản XML và nguồn tải. Từ đây:

- **Xem chứng từ:** mở trình xem với ba thẻ **XML dạng cây**, **XML thô** và **PDF**; đổi phiên bản XML bằng ô *Phiên bản XML*. Trình xem chỉ đọc, không thực thi script hay tải tài nguyên ngoài.
- **Lưu bản sao** (XML nguyên văn, từng byte), **Lưu PDF**, **In**. PDF là bản thể hiện dựng từ XML; **XML mới là chứng từ gốc**.
- **Xuất ZIP XML:** gói chỉ chứa hóa đơn này.

## 7. Lịch sử tải và Tổng quan

- **Lịch sử tải:** danh sách các phiên tải, lọc theo loại, trạng thái, ngày; mở lại tiến độ hoặc **Tạo phiên tải mới**.
- **Tổng quan:** số hóa đơn đã lưu, tổng thanh toán năm, lần đồng bộ gần nhất, các việc **cần xử lý** (phiên lỗi, hóa đơn thiếu/lỗi file) và hóa đơn mới tải.

## 8. Cài đặt, sao lưu và khôi phục

Mục **Lưu trữ và sao lưu** trong **Cài đặt**:

- **Mở thư mục kho**, **Tính lại dung lượng** (XML, PDF, cơ sở dữ liệu, file xuất, bản sao lưu, chỗ trống trên ổ).
- **Tạo bản sao lưu:** gói zip gồm bản chụp cơ sở dữ liệu, mọi file XML/PDF của doanh nghiệp và `manifest.json` (SHA-256 từng file). Có nút **Hủy**; hủy thì không tạo gói nào.
- **Khôi phục từ gói...:** chỉ nhận gói đúng MST. Ứng dụng kiểm toàn bộ gói **trước khi ghi**, tự lưu một *điểm sao lưu an toàn* và quay về trạng thái cũ nếu có lỗi hoặc bạn hủy. Không xóa file nào ngoài gói.
- **Đổi thư mục kho...:** chép toàn bộ kho sang thư mục trống mới, kiểm SHA-256 từng file rồi mới chuyển sang dùng. **Thư mục cũ luôn được giữ nguyên**; bạn tự xóa khi đã yên tâm. Không làm được khi đang có phiên tải chạy.

Hãy sao lưu định kỳ và lưu bản sao lưu ở ổ khác. Mục **Kết nối cổng Thuế** có nút **Kiểm tra kết nối** (chỉ kiểm máy chủ có trả lời, không đăng nhập). **Thông tin ứng dụng** cho biết phiên bản và chế độ làm việc.

## 9. Nhiều doanh nghiệp và bản quyền

Chế độ **chuẩn** quản lý một doanh nghiệp. Với bản quyền dịch vụ, màn **Doanh nghiệp** cho thêm, đổi tên, **liên kết lại** thư mục kho khi bạn đã di chuyển nó, và chuyển giữa các MST (mỗi MST một kho riêng, dữ liệu không lẫn nhau). Khi bản quyền hết hạn ứng dụng chuyển sang **chỉ đọc**: vẫn xem, in, xuất dữ liệu cũ nhưng không tải mới. Kích hoạt bản quyền ở **Cài đặt → Bản quyền ứng dụng**; chỉ khóa bản quyền, mã máy đã băm và phiên bản ứng dụng được gửi đi, **không gửi MST, danh sách khách hàng hay hóa đơn**.

## 10. Khi danh sách doanh nghiệp bị hỏng

Nếu file danh sách doanh nghiệp bị mất hoặc hỏng, ứng dụng hiện màn **Cần khôi phục** và **không mở kho nào** để tránh tạo kho rỗng đè lên dữ liệu. Dữ liệu trong các thư mục kho vẫn nguyên. Ứng dụng tự quét thư mục dữ liệu mặc định; bấm **Chọn thư mục khác...** nếu kho nằm nơi khác, đánh dấu các kho cần dùng rồi **Đăng ký các kho đã chọn**, sau đó **Khởi động lại ứng dụng**. File danh sách hỏng được giữ lại (đổi tên `companies.json.corrupt-...`). Tên hiển thị được đặt tạm theo MST; đổi lại ở màn Doanh nghiệp.

## 11. Gặp sự cố

- Mỗi thông báo lỗi có mã tham chiếu; nhật ký nằm ở `%LOCALAPPDATA%\HoaDonDienTu\logs` (đã che mật khẩu, mã phiên, cookie và nội dung XML).
- Khi cần hỗ trợ, gửi: phiên bản ứng dụng, file nhật ký của ngày lỗi, mã tham chiếu và mô tả thao tác. **Đừng gửi** mật khẩu, mã xác nhận, mã phiên hay nội dung hóa đơn.
- Bảng nguyên nhân và cách xử lý thường gặp: [runbook](../runbooks/build-release-support.md) mục 6.
