# Wireframe

Wireframe được bàn giao ở hai dạng:

- [HTML tương tác](index.html): dùng để chuyển màn hình và trao đổi kỹ thuật.
- `png/`: ảnh tĩnh 1440 × 900, một file cho mỗi màn hình.

## Cách mở HTML

Mở `index.html` bằng Chrome hoặc Edge. Thanh trên cùng cho phép chuyển giữa các màn hình.

Có thể mở trực tiếp một màn hình bằng query string:

```text
index.html?screen=SCR-05
```

Thêm `capture=1` để ẩn thanh chuyển màn hình khi chụp ảnh:

```text
index.html?screen=SCR-05&capture=1
```

## Danh sách màn hình

| Mã | Màn hình | HTML | PNG |
|---|---|---|---|
| SCR-01 | Đăng nhập | [Mở](index.html?screen=SCR-01) | [Ảnh](png/SCR-01-login.png) |
| SCR-02 | Tổng quan | [Mở](index.html?screen=SCR-02) | [Ảnh](png/SCR-02-dashboard.png) |
| SCR-03 | Tải hóa đơn | [Mở](index.html?screen=SCR-03) | [Ảnh](png/SCR-03-download.png) |
| SCR-04 | Tiến độ/chi tiết phiên tải | [Mở](index.html?screen=SCR-04) | [Ảnh](png/SCR-04-progress.png) |
| SCR-05 | Kho hóa đơn | [Mở](index.html?screen=SCR-05) | [Ảnh](png/SCR-05-archive.png) |
| SCR-06 | Chi tiết hóa đơn | [Mở](index.html?screen=SCR-06) | [Ảnh](png/SCR-06-detail.png) |
| SCR-07 | Xem chứng từ XML/PDF | [Mở](index.html?screen=SCR-07) | [Ảnh](png/SCR-07-viewer.png) |
| SCR-08 | Lịch sử tải | [Mở](index.html?screen=SCR-08) | [Ảnh](png/SCR-08-history.png) |
| SCR-09 | Cài đặt | [Mở](index.html?screen=SCR-09) | [Ảnh](png/SCR-09-settings.png) |
| SCR-10 | Quản lý doanh nghiệp | [Mở](index.html?screen=SCR-10) | [Ảnh](png/SCR-10-companies.png) |
| SCR-11 | Thêm/sửa doanh nghiệp | [Mở](index.html?screen=SCR-11) | [Ảnh](png/SCR-11-company-edit.png) |
| SCR-12 | Kích hoạt/quản lý licence | [Mở](index.html?screen=SCR-12) | [Ảnh](png/SCR-12-license.png) |

## Luồng tham chiếu

```text
SCR-01 Đăng nhập
  └─> SCR-02 Tổng quan
        ├─> SCR-03 Tải hóa đơn ─> SCR-04 Tiến độ ─> SCR-05 Kho hóa đơn
        ├─> SCR-05 Kho hóa đơn ─> SCR-06 Chi tiết ─> SCR-07 Xem chứng từ
        ├─> SCR-08 Lịch sử ─> SCR-04 hoặc SCR-05
        └─> SCR-09 Cài đặt

SCR-04 --token hết hạn--> SCR-01 --đăng nhập lại--> SCR-04/checkpoint

StandardMode: SCR-09 --> SCR-12 Kích hoạt licence
                              └─> SCR-10 Quản lý doanh nghiệp
                                      ├─> SCR-11 Thêm/sửa doanh nghiệp --> SCR-01 Đăng nhập
                                      └─> SCR-02 Tổng quan của MST được chọn

SCR-02..09 --bộ chọn MST--> SCR-10 --chuyển MST an toàn--> SCR-02
```

## Quy ước

- Wireframe mô tả cấu trúc, nội dung, trạng thái và điều hướng; không phải thiết kế UI hoàn thiện.
- Dữ liệu doanh nghiệp/hóa đơn trong wireframe là dữ liệu minh họa.
- Thanh điều hướng bên trái là phương án đã được chọn.
- XML được lưu tự động; PDF xuất hiện sau khi người dùng xem hoặc in.
- SCR-02 đến SCR-11 mô tả ServiceMode với bộ chọn MST; SCR-12 mô tả điểm kích hoạt luôn thấy trong StandardMode.
- Mỗi MST chỉ dùng một thư mục và SQLite riêng; wireframe không có màn hình trộn hóa đơn nhiều doanh nghiệp.
- Chuyển MST yêu cầu dừng an toàn tác vụ và đóng phiên/database hiện tại trước khi mở kho mới.
- Mã `SCR-xx` phải được giữ nhất quán giữa SRS, ticket triển khai, test case và UI automation.

## Cập nhật ảnh PNG

`index.html` là nguồn duy nhất. Sau khi sửa HTML, xuất lại PNG bằng Chrome/Edge headless ở kích thước 1440 × 900 với URL `?screen=SCR-xx&capture=1`. Không chỉnh sửa PNG thủ công vì sẽ làm hai định dạng không đồng bộ.
