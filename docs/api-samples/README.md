# Mẫu API cổng Thuế (BẢN NHÁP / DỮ LIỆU GIẢ)

> **Cảnh báo:** mọi nội dung trong thư mục này là **hợp đồng tự định nghĩa** và **dữ liệu bịa** do cổng giả (`MockPortalHandler`) sinh ra.
> **Chưa có file nào được lấy từ traffic thật của cổng Thuế** (G-02 chưa làm). Không dùng làm tài liệu API thật.
> Khi có tài khoản thật: ghi traffic, **làm sạch dữ liệu nhạy cảm**, rồi sửa `portal-api.json` cho khớp (xem mục 4).

## 1. Cách hoạt động

- Mọi thứ cổng-đặc-thù nằm trong **một file cấu hình** (`PortalApiConfig`): base URL, endpoint, cách gửi tham số (query/body), phân trang
  (con trỏ/số trang), bộ lọc, đường dẫn JSON của phản hồi, ánh xạ trường, cách tải XML, ánh xạ lỗi, nhịp gọi.
- `PortalHttpApiClient` dựng request và đọc phản hồi **chỉ dựa trên cấu hình**; `MockPortalHandler` (cổng giả, chạy trong tiến trình, không mở socket)
  đọc **cùng cấu hình** để dựng phản hồi. Đổi cấu hình thì cả hai đổi theo → thử được nhiều "hình dạng API" mà không sửa code.
- Cấu hình nháp mặc định: [`portal-api.config.sample.json`](portal-api.config.sample.json) (`verified: false`). Thứ tự nạp: `%LOCALAPPDATA%\HoaDonDienTu\portal-api.json`
  → `portal-api.json` cạnh file chạy → mặc định trong code. File sai cấu trúc (khóa lạ, thiếu trường bắt buộc, `baseUrl` không https...) bị từ chối khi khởi động.
- Bản Debug dùng cổng giả cho **mọi** lời gọi cổng Thuế (đăng nhập giả → API hồ sơ → đối chiếu MST). Bản Release không có cổng giả.

## 2. Hợp đồng ở tầng Application

`IPortalInvoiceClient` (`HoaDonDienTu.Application.Portal`):

| Phương thức | Vào | Ra |
|---|---|---|
| `SearchAsync` | `InvoiceSearchRequest` (chiều mua/bán, từ–đến ngày gồm cả hai đầu, bộ lọc, `Cursor`, `PageSize`) | `InvoicePage` (`Items`, `TotalCount?`, `NextCursor`; rỗng = hết) |
| `DownloadXmlAsync` | `PortalInvoiceRef` | `InvoiceXmlPayload` (byte[] + tên file gợi ý) |

Lỗi: `PortalApiException` với `Kind` = `AuthExpired` (401/403) · `Transient` (timeout/mạng/5xx) · `RateLimited` (429 + `RetryAfter`) ·
`SchemaChanged` (phản hồi không khớp cấu hình) · `FilterRejected` (400/422) · `Fatal`. Thông báo lỗi chỉ có loại lỗi + mã HTTP, **không** có token/thân phản hồi.

`PortalInvoiceDto` = 19 trường của SRS mục 9.1 ở dạng thô (số hóa đơn là text để giữ số 0 đầu; trạng thái/kết quả kiểm tra giữ giá trị nguồn).

## 3. Mẫu (cổng giả, cấu hình mặc định)

Xác thực: `Authorization: Bearer mock-token-<MST>` (token chứa `expired` → 401).

**`GET /api/v1/profile`**
```json
{"data":{"taxCode":"0101234567","name":"Công ty mẫu 0101234567"}}
```

**`GET /api/v1/invoices/sold?sort=issueDate%3Aasc&from=2026-02-01&to=2026-02-28&size=50`**
```json
{
  "data": {
    "items": [
      {
        "id": "sold-0101234567-202602-0001",
        "formSymbol": "1",
        "invoiceSymbol": "C26TAA",
        "number": "0000011",
        "issueDate": "2026-02-08",
        "seller": {
          "taxCode": "0101234567",
          "name": "Công ty mẫu 0101234567"
        },
        "buyer": {
          "taxCode": "0100100004",
          "name": "Công ty CP Delta",
          "address": "Số 11 đường Mẫu, quận 3, Hà Nội"
        },
        "amounts": {
          "beforeTax": 22300000.0,
          "tax": 2230000.0,
          "payment": 24530000.0
        },
        "currency": "VND",
        "status": "1",
        "checkResult": "0"
      }
    ],
    "total": 1,
    "nextCursor": null
  }
}
```

**`GET /api/v1/invoices/purchase?...&from=2026-07-01&to=2026-07-31&size=2`** (cắt còn 2 phần tử; `nextCursor` là con trỏ mờ, client không được tự suy)
```json
{
  "data": {
    "items": [
      {
        "id": "pur-0101234567-202607-0031",
        "formSymbol": "1",
        "invoiceSymbol": "C26TAA",
        "number": "0000079",
        "issueDate": "2026-07-01",
        "seller": {
          "taxCode": "0100100003",
          "name": "Công ty TNHH Gamma"
        },
        "buyer": {
          "taxCode": "0101234567",
          "name": "Công ty mẫu 0101234567",
          "address": "Số 41 đường Mẫu, quận 8, Hà Nội"
        },
        "amounts": {
          "beforeTax": 17900000.0,
          "tax": 1790000.0,
          "payment": 19690000.0
        },
        "currency": "VND",
        "status": "1",
        "checkResult": "0"
      },
      {
        "id": "pur-0101234567-202607-0062",
        "formSymbol": "1",
        "invoiceSymbol": "C26TAA",
        "number": "0000110",
        "issueDate": "2026-07-01",
        "seller": {
          "taxCode": "0100100004",
          "name": "Công ty CP Delta"
        },
        "buyer": {
          "taxCode": "0101234567",
          "name": "Công ty mẫu 0101234567",
          "address": "Số 72 đường Mẫu, quận 8, Hà Nội"
        },
        "amounts": {
          "beforeTax": 12900000.0,
          "tax": 1290000.0,
          "payment": 14190000.0
        },
        "currency": "VND",
        "status": "1",
        "checkResult": "0"
      }
    ],
    "total": 120,
    "nextCursor": "bzoy"
  }
}
```

**`GET /api/v1/invoices/{id}/xml`** → `application/xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- DỮ LIỆU GIẢ - không phải hóa đơn thật -->
<HDon>
  <DLHDon>
    <TTChung>
      <PBan>2.0.0</PBan>
      <THDon>Hóa đơn giá trị gia tăng</THDon>
      <KHMSHDon>1</KHMSHDon>
      <KHHDon>C26TAA</KHHDon>
      <SHDon>0000011</SHDon>
      <NLap>2026-02-08</NLap>
      <DVTTe>VND</DVTTe>
      <TGia>1</TGia>
    </TTChung>
    <NDHDon>
      <NBan>
        <Ten>Công ty mẫu 0101234567</Ten>
        <MST>0101234567</MST>
      </NBan>
      <NMua>
        <Ten>Công ty CP Delta</Ten>
        <MST>0100100004</MST>
        <DChi>Số 11 đường Mẫu, quận 3, Hà Nội</DChi>
      </NMua>
      <TToan>
        <TgTCThue>22300000</TgTCThue>
        <TgTThue>2230000</TgTThue>
        <TgTTTBSo>24530000</TgTTTBSo>
      </TToan>
    </NDHDon>
  </DLHDon>
</HDon>
```

**Lỗi** (client chỉ dùng mã HTTP, không đọc thân): `401 {"error":{"code":"unauthorized"}}`, `429` + `Retry-After: 7`, `400` (bộ lọc không hỗ trợ / khoảng ngày > 31 ngày / con trỏ sai), `404`, `5xx`.

## 4. Khi có tài khoản thật: việc cần làm (T00-02, T00-03)

1. Đăng nhập thủ công trong WebView, ghi traffic (DevTools/Fiddler) cho: hồ sơ/MST, tra cứu mua vào, tra cứu bán ra (có nhiều trang), tải XML, một lỗi 401 và (nếu gặp) 429.
2. **Làm sạch** trước khi lưu vào thư mục này: bỏ token/cookie/header xác thực, MST/tên/địa chỉ thật, số hóa đơn thật.
3. Đối chiếu từng mục cấu hình:

| Mục cấu hình | Cần xác nhận từ traffic thật |
|---|---|
| `baseUrl`, `*.path`, `*.method` | Host (có thể kèm cổng), đường dẫn, GET hay POST |
| `auth.headerName/scheme/sendCookies/forwardHeaders` | Token nằm ở header nào/cookie nào; có header CSRF không |
| `search.parameterLocation/dateFormat/fromParam/toParam` | Tham số ngày nằm ở query hay body; định dạng ngày; **một biểu thức lọc hay hai tham số** (nếu là một biểu thức, cần mở rộng `PortalRequestBuilder`) |
| `search.pagination.*` | Cursor hay số trang, tên tham số, kích thước trang tối đa cổng chấp nhận, trang đầu là 0 hay 1 |
| `search.filterParameters` | Trường lọc nào cổng hỗ trợ (bỏ khóa nào không hỗ trợ) |
| `search.response.*` | Đường dẫn tới danh sách, tổng, con trỏ kế/cờ còn trang; định dạng ngày; đường dẫn từng trường (thay `fields`) |
| `download.*` | Tham số nào cần để tải XML (ID hay bộ MST+ký hiệu+số), vị trí tham số, trả XML thô hay JSON base64 |
| `errors.statusKinds`, `rateLimit` | Mã lỗi thật cho token hết hạn/giới hạn tốc độ; nhịp gọi an toàn |

4. Đặt `"verified": true` chỉ khi mọi mục trên đã khớp; chạy lại bộ test `Portal*` (chúng dùng cổng giả nên vẫn phải xanh với cấu hình mới, hoặc cập nhật mock nếu cấu trúc thật vượt khả năng cấu hình).
5. Cập nhật [ADR 0001](../decisions/0001-portal-api-contract.md) sang `Accepted`, ghi mẫu đã làm sạch vào thư mục này và đóng G-02.

**Giới hạn đã biết của thiết kế cấu hình** (cần mở rộng code nếu cổng thật yêu cầu): đường dẫn JSON của mock chỉ hỗ trợ dạng chấm (a.b.c); mỗi tham số là một cặp tên/giá trị
(chưa hỗ trợ biểu thức lọc gộp kiểu `ngay=ge=...;ngay=le=...`); phân trang Page không suy `hasMore` từ tổng (đọc tới khi gặp trang rỗng nếu không có cờ); tải XML hỗ trợ XML thô hoặc JSON base64.

## 5. Dữ liệu giả

`MockInvoiceDataset` sinh dữ liệu xác định theo MST (cùng MST → cùng dữ liệu), phủ 2025-01…2026-12, cả mua vào và bán ra:
tháng nhiều trang (mua vào 2026-07: 120 hóa đơn), tháng rỗng (bán ra 2026-03), tháng một hóa đơn (bán ra 2026-02), số hóa đơn có số 0 đầu,
ngoại tệ (USD + tỷ giá), trường tùy chọn để trống (chiết khấu/phí), MST đơn vị phụ thuộc (`...-001`), nhiều trạng thái/kết quả kiểm tra.
Cổng giả có thể bơm lỗi: `EnqueueFailure(status, retryAfter)`, `EnqueueNetworkError()`, `Latency`, `MaxPageSize`, `OmitDtoField`, `MalformedSearchResponse`, `UnsupportedFilterFields`.

## Licence server (`license-api.config.sample.json`)

Hợp đồng nháp của `LicenseApiClient` (chưa xác minh với server thật, gate G-06): `POST {baseUrl}/activate|validate` nhận `{licenseKey, machineIdHash, appVersion}` và trả `{licenseType, machineIdHash, issuedAtUtc, expiresAtUtc|null, features[], signature}`; `POST {baseUrl}/deactivate` nhận `{licenseKey, machineIdHash}`. Từ chối nghiệp vụ là 4xx với `{"error": "key_invalid|bound_to_other_machine|expired|revoked"}`; 5xx, 429, timeout và mất mạng được coi là server tạm lỗi, không phải thu hồi.

Đặt bản đã điền thành `license-api.json` trong `%LOCALAPPDATA%\HoaDonDienTu\` hoặc cạnh file chạy. `baseUrl` phải là HTTPS và `publicKeyXml` (RSA XML) là khóa công khai dùng xác minh chữ ký. Thiếu hoặc sai file thì bản Release vẫn chạy chế độ chuẩn và kích hoạt báo chưa kết nối được máy chủ licence.
