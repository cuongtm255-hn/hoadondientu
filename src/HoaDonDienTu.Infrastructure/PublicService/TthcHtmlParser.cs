using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HoaDonDienTu.Domain.PublicService;
using HtmlAgilityPack;

namespace HoaDonDienTu.Infrastructure.PublicService
{
    /// <summary>
    /// Chuyển HTML của cổng Dịch vụ công (danh sách, chi tiết, ô lọc TTHC) thành mô hình Domain (DD-DVC-02).
    /// Chỉ lấy VĂN BẢN từ các nút chữ: không bao giờ chuyển tiếp thuộc tính (onclick, href javascript:...), thẻ script, style, iframe,
    /// nên không có đường thực thi mã của cổng (NFR-DVC-SEC-003). Sai mẫu thì ném <see cref="TthcParseException"/>, không trả rỗng im lặng.
    /// </summary>
    public static class TthcHtmlParser
    {
        private static readonly Regex SummaryPattern = new Regex(
            @"Trang\s*(\d+)\s*/\s*(\d+).*?bản\s*ghi\s*:?\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ProcedureCountPattern = new Regex(@"\(\s*(\d+)\s*TTHC\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> SkippedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "style", "iframe", "object", "embed", "noscript", "template", "svg", "head"
        };

        private static readonly HashSet<string> BlockTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "p", "div", "li", "ul", "ol", "tr", "table", "h1", "h2", "h3", "h4", "h5", "h6", "section", "article", "blockquote"
        };

        /// <summary>Đọc bảng danh sách và dòng tổng/số trang. Kết quả rỗng hợp lệ khi có bảng, không có dòng và không có dòng tổng.</summary>
        public static TthcPage ParseList(string html)
        {
            HtmlNode root = Load(html);
            HtmlNode table = root.SelectSingleNode("//table");
            if (table == null) throw new TthcParseException("Không thấy bảng danh sách TTHC.");

            var items = new List<TthcSummary>();
            foreach (HtmlNode row in table.SelectNodes(".//tbody/tr") ?? new HtmlNodeCollection(null))
            {
                HtmlNodeCollection cells = row.SelectNodes("./td");
                if (cells == null || cells.Count < 5) throw new TthcParseException("Dòng danh sách TTHC thiếu cột.");
                string code = Text(cells[1]);
                string name = Text(cells[2]);
                if (code.Length == 0 || name.Length == 0) throw new TthcParseException("Dòng danh sách TTHC thiếu mã hoặc tên.");
                items.Add(new TthcSummary(code, name, Text(cells[3]), Text(cells[4])));
            }

            string summaryText = FindSummaryText(root);
            if (summaryText == null)
            {
                if (items.Count == 0) return new TthcPage(items, 0, 0, 1, 0);
                throw new TthcParseException("Không thấy dòng tổng số bản ghi.");
            }

            Match m = SummaryPattern.Match(summaryText);
            if (!m.Success) throw new TthcParseException("Dòng tổng số bản ghi sai mẫu.");
            int page = ToInt(m.Groups[1].Value), pages = ToInt(m.Groups[2].Value), records = ToInt(m.Groups[3].Value);
            Match pm = ProcedureCountPattern.Match(summaryText);
            int procedures = pm.Success ? ToInt(pm.Groups[1].Value) : records;
            return new TthcPage(items, records, procedures, page, pages);
        }

        /// <summary>Đọc các ô chọn của bộ lọc. Ô không có trong trang (ví dụ Loại người nộp thuế ở bản công khai) cho danh sách rỗng.</summary>
        public static TthcFilterOptions ParseFilterOptions(string html)
        {
            HtmlNode root = Load(html);
            IList<TthcOption> types = ReadSelect(root, "maNghiepVu");
            IList<TthcOption> declarations = ReadSelect(root, "maToKhai");
            IList<TthcOption> levels = ReadSelect(root, "mucDo");
            if (types == null && declarations == null && levels == null) throw new TthcParseException("Không thấy ô lọc nào.");
            return new TthcFilterOptions(types, declarations, levels, null);
        }

        /// <summary>Đọc trang chi tiết: tên và các mục (nhãn + văn bản thuần) theo thứ tự của cổng. Bắt buộc có mã thủ tục.</summary>
        public static TthcDetail ParseDetail(string html)
        {
            HtmlNode root = Load(html);
            HtmlNode body = root.SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' card-body ')]") ?? root;
            HtmlNode title = body.SelectSingleNode(".//h4");
            string name = title == null ? string.Empty : Text(title);

            var sections = new List<KeyValuePair<string, string>>();
            string code = null;
            foreach (HtmlNode block in body.SelectNodes(".//div[contains(concat(' ', normalize-space(@class), ' '), ' mb-3 ')]") ?? new HtmlNodeCollection(null))
            {
                HtmlNode label = block.SelectSingleNode(".//label");
                HtmlNode value = block.SelectSingleNode("./div[@id]");
                if (label == null || value == null) continue;
                string key = Text(label).TrimEnd(':', ' ');
                string text = Text(value);
                if (value.Id == "maTTHC") code = text;
                sections.Add(new KeyValuePair<string, string>(key, text));
            }

            if (string.IsNullOrEmpty(code)) throw new TthcParseException("Trang chi tiết không có mã thủ tục.");
            return new TthcDetail(code, name, sections);
        }

        private static IList<TthcOption> ReadSelect(HtmlNode root, string name)
        {
            HtmlNode select = root.SelectSingleNode("//select[@name='" + name + "']");
            if (select == null) return null;
            var options = new List<TthcOption>();
            foreach (HtmlNode option in select.SelectNodes(".//option") ?? new HtmlNodeCollection(null))
            {
                string code = option.GetAttributeValue("value", string.Empty).Trim();
                if (code.Length == 0) continue; // dòng "Tất cả" do giao diện tự thêm
                options.Add(new TthcOption(code, Text(option)));
            }
            return options;
        }

        private static string FindSummaryText(HtmlNode root)
        {
            foreach (HtmlNode node in root.SelectNodes("//div[contains(@class,'text-center')]") ?? new HtmlNodeCollection(null))
            {
                string text = Text(node);
                if (SummaryPattern.IsMatch(text)) return text;
            }
            return null;
        }

        private static int ToInt(string digits)
        {
            int value;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value)) throw new TthcParseException("Số không hợp lệ.");
            return value;
        }

        private static HtmlNode Load(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) throw new TthcParseException("HTML rỗng.");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc.DocumentNode;
        }

        /// <summary>Văn bản thuần của một nút: bỏ thẻ nguy hiểm, giữ xuống dòng theo khối, gộp khoảng trắng.</summary>
        internal static string Text(HtmlNode node)
        {
            var sb = new StringBuilder();
            Walk(node, sb);
            var lines = new List<string>();
            bool lastBlank = true;
            foreach (string raw in sb.ToString().Replace("\r", string.Empty).Split('\n'))
            {
                string line = Regex.Replace(raw, @"[ \t ]+", " ").Trim();
                if (line.Length == 0)
                {
                    if (!lastBlank) lines.Add(string.Empty);
                    lastBlank = true;
                }
                else
                {
                    lines.Add(line);
                    lastBlank = false;
                }
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return string.Join(Environment.NewLine, lines);
        }

        private static void Walk(HtmlNode node, StringBuilder sb)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    sb.Append(Regex.Replace(HtmlEntity.DeEntitize(((HtmlTextNode)node).Text), @"\s+", " "));
                    return;
                case HtmlNodeType.Comment:
                    return;
            }

            string tag = node.Name;
            if (node.NodeType == HtmlNodeType.Element)
            {
                if (SkippedTags.Contains(tag)) return;
                if (string.Equals(tag, "br", StringComparison.OrdinalIgnoreCase)) { sb.Append('\n'); return; }
            }

            bool block = node.NodeType == HtmlNodeType.Element && BlockTags.Contains(tag);
            if (block) sb.Append('\n');
            foreach (HtmlNode child in node.ChildNodes) Walk(child, sb);
            if (block) sb.Append('\n');
            else if (node.NodeType == HtmlNodeType.Element && (string.Equals(tag, "td", StringComparison.OrdinalIgnoreCase) || string.Equals(tag, "th", StringComparison.OrdinalIgnoreCase))) sb.Append(' ');
        }
    }
}
