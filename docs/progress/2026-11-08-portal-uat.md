# Biên bản UAT trên cổng Thuế thật (P6-01, T05-11)

Kịch bản: [portal-uat.md](../runbooks/portal-uat.md). **Mẫu để điền**; chưa có kết quả nào. Chỉ ghi số liệu đã làm sạch: không MST, tên, số tiền hay số hóa đơn thật, không token/cookie/mật khẩu.

## Thông tin lần thử

| Mục | Giá trị |
|---|---|
| Ngày | |
| Người thử | |
| Gói (tên file, SHA-256 đầu 8 ký tự) | |
| Hệ điều hành | |
| Số hóa đơn bán ra / mua vào trong tháng thử (chỉ số lượng) | |

## Kết quả

Đạt: ✅ · Không đạt: ❌ · Chưa thử: ⬜. Ghi chú ngắn gọn, mô tả lỗi và cách tái hiện nếu ❌.

| # | Kịch bản | Kết quả | Ghi chú |
|---|---|---|---|
| 1 | Đăng nhập thủ công, lấy token | ⬜ | |
| 2 | Bán ra trong kỳ có dữ liệu | ⬜ | |
| 3 | Mua vào trong kỳ có dữ liệu | ⬜ | |
| 4 | Kỳ không có dữ liệu | ⬜ | |
| 5 | Nhiều trang trong một tháng | ⬜ | |
| 6 | Quý / năm / khoảng tự chọn | ⬜ | |
| 7 | Tải về kho (có `.xml` và `.zip` cạnh nhau) | ⬜ | |
| 8 | Xuất Excel so với mẫu cổng | ⬜ | |
| 9 | XML ZIP và in | ⬜ | |
| 10 | Đăng nhập lại giữa phiên và tiếp tục | ⬜ | |
| 11 | Giới hạn tốc độ (429) | ⬜ | |
| 12 | Đăng nhập đúng/sai MST sau khi chuyển doanh nghiệp | ⬜ | |
| 13 | Một phiên Thuế, một SQLite | ⬜ | |

## Quan sát thêm

| Mục | Quan sát |
|---|---|
| Tên các file trong zip của cổng (chỉ tên, không nội dung) | |
| Dung lượng kho: số hóa đơn → tổng dung lượng có/không có zip | |
| Tìm `Bearer`/`token` trong `%LOCALAPPDATA%\HoaDonDienTu` | |
| 429 hoặc 401 thật: có gặp không, app xử lý ra sao | |

## Sau khi điền

1. Cập nhật các mục ⚠ trong `docs/api-samples/README.md` bằng mẫu đã làm sạch (kiểu `sample-api-web.md`).
2. Cập nhật T05-11 trong [DD-05](../detail-design/DD-05-DOWNLOAD-ENGINE.md) và P6-01 trong [PROJECT-PLAN](../plans/PROJECT-PLAN.md).
3. Mỗi ❌ thành một task hoặc lỗi cần sửa.
