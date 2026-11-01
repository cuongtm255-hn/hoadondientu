using System;
using System.Collections.Generic;
using System.Globalization;
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

        public static InvoicePage ParseSearch(PortalApiConfig config, string body, string requestCursor)
        {
            var response = config.Search.Response;
            var paging = config.Search.Pagination;
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
            if (paging.Mode == "Page")
            {
                // Không biết cổng cắt kích thước trang bao nhiêu nên không suy ra từ số lượng yêu cầu:
                // dùng cờ hasMore nếu có, nếu không thì đọc tiếp tới khi gặp trang rỗng.
                var more = items.Count > 0;
                if (!string.IsNullOrEmpty(response.HasMorePath))
                {
                    var flag = root.SelectToken(response.HasMorePath);
                    more = items.Count > 0 && flag != null && flag.Type == JTokenType.Boolean && flag.Value<bool>();
                }

                if (more)
                {
                    var current = paging.StartPage;
                    if (!string.IsNullOrEmpty(requestCursor)) int.TryParse(requestCursor, NumberStyles.None, CultureInfo.InvariantCulture, out current);
                    next = (current + 1).ToString(CultureInfo.InvariantCulture);
                }
            }
            else if (!string.IsNullOrEmpty(response.NextCursorPath))
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

        public static string ParseProfileTaxCode(PortalApiConfig config, string body)
        {
            var token = ParseJson(body).SelectToken(config.Profile.TaxCodePath);
            var text = token == null ? null : ToText(token);
            if (string.IsNullOrWhiteSpace(text)) throw Schema("Không thấy MST tại '" + config.Profile.TaxCodePath + "'.");
            return text.Trim();
        }

        public static byte[] ParseBase64Content(PortalApiConfig config, string body)
        {
            var token = ParseJson(body).SelectToken(config.Download.ContentPath);
            var text = token == null ? null : ToText(token);
            if (string.IsNullOrEmpty(text)) throw Schema("Không thấy nội dung XML tại '" + config.Download.ContentPath + "'.");
            try
            {
                return Convert.FromBase64String(text);
            }
            catch (FormatException)
            {
                throw Schema("Nội dung XML không phải base64 hợp lệ.");
            }
        }

        private static PortalInvoiceDto MapInvoice(SearchResponseConfig response, JToken item)
        {
            var f = response.Fields;
            var dto = new PortalInvoiceDto
            {
                PortalInvoiceId = Text(item, f, "PortalInvoiceId", true),
                FormSymbol = Text(item, f, "FormSymbol", false),
                InvoiceSymbol = Text(item, f, "InvoiceSymbol", false),
                InvoiceNumber = Text(item, f, "InvoiceNumber", true),
                SellerTaxCode = Text(item, f, "SellerTaxCode", false),
                SellerName = Text(item, f, "SellerName", false),
                BuyerTaxCode = Text(item, f, "BuyerTaxCode", false),
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

            dto.IssueDate = Date(item, f, response.DateFormats);
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

        private static DateTime Date(JToken item, Dictionary<string, string> fields, List<string> formats)
        {
            string path;
            if (!fields.TryGetValue("IssueDate", out path)) throw Schema("Thiếu ánh xạ IssueDate.");
            var token = item.SelectToken(path);
            if (token == null || token.Type == JTokenType.Null) throw Schema("Hóa đơn thiếu trường bắt buộc 'IssueDate'.");

            // JToken.Value<string>() của JTokenType.Date có thể đổi định dạng; đọc nguyên văn khi là chuỗi.
            var text = token.Type == JTokenType.Date ? token.Value<DateTime>().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : ToText(token);
            DateTime parsed;
            if (DateTime.TryParseExact(text, formats.ToArray(), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed.Date;
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
