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
            if (string.IsNullOrWhiteSpace(html)) throw Fail("HTML rỗng.");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNode root = doc.DocumentNode;

            HtmlNode table = root.SelectSingleNode("//table");
            if (table == null)
            {
                // Không có bảng: cổng trả thông điệp "Không có dữ liệu" (mảnh ngắn). Mảnh dài mà không có bảng là cấu trúc lạ.
                string message = TthcHtmlParser.Text(root);
                if (message.Length == 0 || message.Length > MaxEmptyMessageLength) throw Fail("Không thấy bảng hồ sơ và cũng không phải thông điệp rỗng.");
                return new HoSoPage(null, null, 0, 1, 0, message);
            }

            // Dòng tiêu đề: dòng trong thead, hoặc dòng đầu tiên của bảng. Ô có thể là th hoặc td (cột STT hay dùng th).
            HtmlNode headerRow = table.SelectSingleNode(".//thead//tr") ?? table.SelectSingleNode(".//tr");
            var headers = new List<string>();
            if (headerRow != null)
            {
                foreach (HtmlNode cell in headerRow.SelectNodes("./th|./td") ?? new HtmlNodeCollection(null)) headers.Add(TthcHtmlParser.Text(cell));
            }
            if (headers.Count == 0) throw Fail("Bảng hồ sơ không có tiêu đề cột.");

            var keep = new List<int>();
            for (int i = 0; i < headers.Count; i++)
            {
                if (!SkippedColumns.Contains(headers[i], StringComparer.OrdinalIgnoreCase)) keep.Add(i);
            }
            IList<string> columns = keep.Select(i => headers[i]).ToList();

            var rows = new List<HoSoRow>();
            int rowNumber = 0;
            foreach (HtmlNode tr in table.SelectNodes(".//tr") ?? new HtmlNodeCollection(null))
            {
                if (tr == headerRow) continue;
                rowNumber++;
                HtmlNodeCollection cells = tr.SelectNodes("./td|./th");
                if (cells == null || cells.Count != headers.Count)
                {
                    throw Fail("Dòng hồ sơ " + rowNumber + " có " + (cells == null ? 0 : cells.Count) + " ô, tiêu đề có " + headers.Count + " cột.");
                }
                rows.Add(new HoSoRow(keep.Select(i => TthcHtmlParser.Text(cells[i])).ToList()));
            }

            string summary = TthcHtmlParser.Text(root);
            Match m = SummaryPattern.Match(summary);
            if (!m.Success) throw Fail("Không thấy dòng tổng số bản ghi (" + rows.Count + " dòng, " + headers.Count + " cột).");
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

        /// <summary>
        /// Mô tả CẤU TRÚC của HTML (thẻ, class, tên thuộc tính; chữ thay bằng "a", số thay bằng "9"), tối đa vài nghìn ký tự, để ghi vào thông báo lỗi
        /// khi cổng đổi giao diện. Không chứa dữ liệu thật nào.
        /// </summary>
        public static string DescribeStructure(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "(rỗng)";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var sb = new System.Text.StringBuilder();
            int budget = 90;
            Describe(doc.DocumentNode, 0, sb, ref budget);
            return sb.Length > 3500 ? sb.ToString(0, 3500) : sb.ToString();
        }

        private static void Describe(HtmlNode node, int depth, System.Text.StringBuilder sb, ref int budget)
        {
            foreach (HtmlNode child in node.ChildNodes)
            {
                if (budget <= 0 || depth > 9) return;
                if (child.NodeType == HtmlNodeType.Text)
                {
                    string text = Regex.Replace(HtmlEntity.DeEntitize(child.InnerText), @"\s+", " ").Trim();
                    if (text.Length > 0) sb.Append(new string(' ', depth)).Append("\"").Append(Shape(text)).Append("\"\n");
                    continue;
                }
                if (child.NodeType != HtmlNodeType.Element || child.Name == "script" || child.Name == "style") continue;
                budget--;
                string cls = child.GetAttributeValue("class", string.Empty);
                sb.Append(new string(' ', depth)).Append(child.Name);
                if (cls.Length > 0) sb.Append('.').Append(cls.Replace(' ', '.'));
                string[] attrs = child.Attributes.Select(a => a.Name).Where(n => n != "class" && n != "style").ToArray();
                if (attrs.Length > 0) sb.Append('[').Append(string.Join(",", attrs)).Append(']');
                sb.Append('\n');
                Describe(child, depth + 1, sb, ref budget);
            }
        }

        private static string Shape(string text)
        {
            string s = Regex.Replace(text, @"[A-Za-z\u00C0-\u1EF9]+", "a");
            s = Regex.Replace(s, @"\d+", "9");
            return s.Length > 40 ? s.Substring(0, 40) : s;
        }

        /// <summary>Lỗi do bộ phân tích tạo: lý do là chuẩn đoán an toàn (không chứa dữ liệu).</summary>
        private static HoSoParseException Fail(string reason)
        {
            return new HoSoParseException(reason, reason);
        }

        private static int ToInt(string digits)
        {
            int value;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value)) throw Fail("Số không hợp lệ.");
            return value;
        }
    }
}
