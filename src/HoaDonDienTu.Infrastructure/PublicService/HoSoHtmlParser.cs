using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HoaDonDienTu.Domain.PublicService;
using HtmlAgilityPack;

namespace HoaDonDienTu.Infrastructure.PublicService
{
    /// <summary>
    /// Đọc kết quả "tra cứu hồ sơ đã nộp" (tab Dịch vụ công, <c>GET /tthc/ho-so/search</c>) thành <see cref="HoSoPage"/> (DD-DVC-03).
    /// Cấu trúc đã khảo sát 21/11/2026: dòng "Trang x/N - Tổng số bản ghi: n", <c>ul.pagination</c>, bảng <c>table-bordered</c> 13 cột.
    /// Giữ các cột dữ liệu theo đúng thứ tự cổng, bỏ hai cột không phải dữ liệu là "STT" (màn hình tự đánh số) và "Thao tác" (biểu tượng).
    /// Chỉ lấy văn bản (không bao giờ chuyển tiếp thuộc tính, script...). Sai mẫu thì ném <see cref="HoSoParseException"/>, không trả rỗng im lặng.
    /// </summary>
    public static class HoSoHtmlParser
    {
        private static readonly Regex SummaryPattern = new Regex(
            @"Trang\s*(\d*)\s*/\s*(\d+).*?bản\s*ghi\s*:?\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly string[] SkippedColumns = { "STT", "Thao tác" };

        private const int MaxEmptyMessageLength = 300;

        public static HoSoPage ParseList(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) throw new HoSoParseException("HTML rỗng.");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNode root = doc.DocumentNode;

            HtmlNode table = root.SelectSingleNode("//table");
            if (table == null)
            {
                // Không có bảng: cổng trả thông điệp "Không có dữ liệu" (mảnh ngắn). Mảnh dài mà không có bảng là cấu trúc lạ.
                string message = TthcHtmlParser.Text(root);
                if (message.Length == 0 || message.Length > MaxEmptyMessageLength) throw new HoSoParseException("Không thấy bảng hồ sơ và cũng không phải thông điệp rỗng.");
                return new HoSoPage(null, null, 0, 1, 0, message);
            }

            var headers = new List<string>();
            foreach (HtmlNode th in table.SelectNodes(".//thead//th") ?? new HtmlNodeCollection(null)) headers.Add(TthcHtmlParser.Text(th));
            if (headers.Count == 0) throw new HoSoParseException("Bảng hồ sơ không có tiêu đề cột.");

            var keep = new List<int>();
            for (int i = 0; i < headers.Count; i++)
            {
                if (!SkippedColumns.Contains(headers[i], StringComparer.OrdinalIgnoreCase)) keep.Add(i);
            }
            IList<string> columns = keep.Select(i => headers[i]).ToList();

            var rows = new List<HoSoRow>();
            foreach (HtmlNode tr in table.SelectNodes(".//tbody/tr") ?? new HtmlNodeCollection(null))
            {
                HtmlNodeCollection cells = tr.SelectNodes("./td");
                if (cells == null || cells.Count != headers.Count) throw new HoSoParseException("Dòng hồ sơ không khớp số cột.");
                rows.Add(new HoSoRow(keep.Select(i => TthcHtmlParser.Text(cells[i])).ToList()));
            }

            string summary = TthcHtmlParser.Text(root);
            Match m = SummaryPattern.Match(summary);
            if (!m.Success) throw new HoSoParseException("Không thấy dòng tổng số bản ghi.");
            int pageCount = ToInt(m.Groups[2].Value);
            int total = ToInt(m.Groups[3].Value);

            // Số trang hiện tại: ô trong dòng tổng thường để trống, lấy từ mục .active của thanh phân trang.
            int page = 1;
            HtmlNode active = root.SelectSingleNode("//ul[contains(@class,'pagination')]//li[contains(@class,'active')]");
            int fromActive;
            if (active != null && int.TryParse(TthcHtmlParser.Text(active), NumberStyles.None, CultureInfo.InvariantCulture, out fromActive)) page = fromActive;
            else if (m.Groups[1].Value.Length > 0) page = ToInt(m.Groups[1].Value);

            return new HoSoPage(columns, rows, total, page, pageCount, null);
        }

        private static int ToInt(string digits)
        {
            int value;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value)) throw new HoSoParseException("Số không hợp lệ.");
            return value;
        }
    }
}
