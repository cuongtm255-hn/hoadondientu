using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Portal
{
    /// <summary>
    /// Đọc phản hồi JSON theo đường dẫn trong cấu hình. Thiếu cấu trúc đã cấu hình ném
    /// <see cref="PortalErrorKind.SchemaChanged"/>; thông báo lỗi không chứa nội dung phản hồi.
    /// </summary>
    public static class PortalResponseParser
    {
        public static JToken ParseJson(string body)
        {
            try
            {
                var token = JToken.Parse(body);
                if (token == null) throw Schema("Phản hồi rỗng.");
                return token;
            }
            catch (JsonException)
            {
                throw Schema("Phản hồi không phải JSON hợp lệ.");
            }
        }

        public static InvoicePage ParseSearch(PortalApiConfig config, string body)
        {
            var response = config.Search.Response;
            var root = ParseJson(body);

            var items = root.SelectToken(response.ItemsPath) as JArray;
            if (items == null) throw Schema("Không thấy danh sách hóa đơn tại '" + response.ItemsPath + "'.");

            var dtos = new List<PortalInvoiceDto>();
            foreach (var item in items) dtos.Add(MapInvoice(response, item));

            int? total = null;
            if (!string.IsNullOrEmpty(response.TotalPath))
            {
                var totalToken = root.SelectToken(response.TotalPath);
                if (totalToken != null && totalToken.Type != JTokenType.Null) total = ToInt(totalToken, response.TotalPath);
            }

            string next = null;
            if (!string.IsNullOrEmpty(response.NextCursorPath))
            {
                var token = root.SelectToken(response.NextCursorPath);
                if (token != null && token.Type != JTokenType.Null)
                {
                    var text = ToText(token);
                    if (!string.IsNullOrEmpty(text)) next = text;
                }
            }

            return new InvoicePage(dtos, total, next);
        }

        /// <summary>
        /// Lấy nội dung XML hóa đơn từ file zip của API xuất XML. Cổng đóng gói <c>invoice.xml</c> cùng trang xem (HTML, JS, ảnh);
        /// chỉ đọc đúng entry XML, không giải nén ra đĩa nên tên entry không thể thoát khỏi thư mục đích.
        /// </summary>
        public static byte[] ExtractXml(DownloadConfig download, byte[] zipBytes)
        {
            try
            {
                using (var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = null;
                    foreach (var candidate in archive.Entries)
                    {
                        if (string.Equals(candidate.Name, download.ZipEntryName, StringComparison.OrdinalIgnoreCase)) { entry = candidate; break; }
                    }

                    if (entry == null) throw Schema("File zip không có '" + download.ZipEntryName + "'.");
                    using (var stream = entry.Open())
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        return buffer.ToArray();
                    }
                }
            }
            catch (InvalidDataException)
            {
                throw Schema("Phản hồi xuất XML không phải file zip hợp lệ.");
            }
        }

        private static PortalInvoiceDto MapInvoice(SearchResponseConfig response, JToken item)
        {
            var f = response.Fields;
            var dto = new PortalInvoiceDto
            {
                PortalInvoiceId = Text(item, f, "PortalInvoiceId", true),
                FormSymbol = Text(item, f, "FormSymbol", true),
                InvoiceSymbol = Text(item, f, "InvoiceSymbol", true),
                InvoiceNumber = Text(item, f, "InvoiceNumber", true),
                SellerTaxCode = Text(item, f, "SellerTaxCode", true),
                SellerName = Text(item, f, "SellerName", false),
                BuyerTaxCode = Text(item, f, "BuyerTaxCode", false),
                BuyerIdNumber = Text(item, f, "BuyerIdNumber", false),
                BuyerName = Text(item, f, "BuyerName", false),
                BuyerAddress = Text(item, f, "BuyerAddress", false),
                TotalBeforeTax = Number(item, f, "TotalBeforeTax"),
                TotalTax = Number(item, f, "TotalTax"),
                TotalDiscount = Number(item, f, "TotalDiscount"),
                TotalFee = Number(item, f, "TotalFee"),
                TotalPayment = Number(item, f, "TotalPayment"),
                Currency = Text(item, f, "Currency", false),
                ExchangeRate = Number(item, f, "ExchangeRate"),
                StatusRaw = Text(item, f, "StatusRaw", false),
                CheckResultRaw = Text(item, f, "CheckResultRaw", false)
            };

            dto.IssueDate = Date(item, f, response);
            return dto;
        }

        private static string Text(JToken item, Dictionary<string, string> fields, string field, bool required)
        {
            string path;
            JToken token = null;
            if (fields.TryGetValue(field, out path) && !string.IsNullOrEmpty(path)) token = item.SelectToken(path);
            var text = token == null || token.Type == JTokenType.Null ? null : ToText(token);
            if (required && string.IsNullOrEmpty(text)) throw Schema("Hóa đơn thiếu trường bắt buộc '" + field + "'.");
            return text;
        }

        private static decimal? Number(JToken item, Dictionary<string, string> fields, string field)
        {
            string path;
            if (!fields.TryGetValue(field, out path) || string.IsNullOrEmpty(path)) return null;
            var token = item.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null) return null;

            decimal value;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return token.Value<decimal>();
            }

            var text = ToText(token);
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return value;
            throw Schema("Trường số '" + field + "' không hợp lệ.");
        }

        /// <summary>
        /// Ngày lập của cổng là thời điểm UTC (ví dụ <c>2026-10-30T17:00:00Z</c> là 31/10/2026 giờ Việt Nam); đổi sang múi giờ cấu hình
        /// rồi lấy phần ngày, nếu không hóa đơn lập 0 giờ sẽ lệch về ngày hôm trước.
        /// </summary>
        private static DateTime Date(JToken item, Dictionary<string, string> fields, SearchResponseConfig response)
        {
            string path;
            if (!fields.TryGetValue("IssueDate", out path)) throw Schema("Thiếu ánh xạ IssueDate.");
            var token = item.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null) throw Schema("Hóa đơn thiếu trường bắt buộc 'IssueDate'.");

            // JToken.Value<string>() của JTokenType.Date có thể đổi định dạng; đọc nguyên văn khi là chuỗi.
            var text = token.Type == JTokenType.Date
                ? token.Value<DateTime>().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                : ToText(token);

            DateTime parsed;
            var isUtc = text != null && text.EndsWith("Z", StringComparison.Ordinal);
            var styles = isUtc ? DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal : DateTimeStyles.None;
            if (DateTime.TryParseExact(text, response.DateFormats.ToArray(), CultureInfo.InvariantCulture, styles, out parsed))
            {
                return (isUtc ? parsed.AddHours(response.TimeZoneOffsetHours) : parsed).Date;
            }

            throw Schema("Ngày lập không khớp định dạng đã cấu hình.");
        }

        private static string ToText(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.String: return token.Value<string>();
                case JTokenType.Integer: return token.Value<long>().ToString(CultureInfo.InvariantCulture);
                case JTokenType.Float: return token.Value<decimal>().ToString(CultureInfo.InvariantCulture);
                case JTokenType.Boolean: return token.Value<bool>() ? "true" : "false";
                default: return null;
            }
        }

        private static int ToInt(JToken token, string path)
        {
            int value;
            if (token.Type == JTokenType.Integer) return token.Value<int>();
            if (int.TryParse(ToText(token), NumberStyles.None, CultureInfo.InvariantCulture, out value)) return value;
            throw Schema("Trường tổng '" + path + "' không phải số.");
        }

        private static PortalApiException Schema(string message)
        {
            return new PortalApiException(PortalErrorKind.SchemaChanged, message, null, null);
        }
    }
}
