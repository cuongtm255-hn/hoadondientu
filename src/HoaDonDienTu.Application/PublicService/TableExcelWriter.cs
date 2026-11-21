using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Ghi một bảng văn bản ra .xlsx (nhóm Dịch vụ công, FT-DVC-08). Cùng cách làm với bộ ghi của nhóm Hóa đơn (ADR 0003): tự dựng SpreadsheetML
    /// trong một gói zip nên không cần thư viện ngoài tương thích net45, nhưng là bản riêng để hai nhóm không phụ thuộc nhau.
    /// Bố cục: dòng 1 tiêu đề, các dòng thông tin, một dòng trống, dòng tiêu đề cột (cố định khi cuộn), rồi dữ liệu. Mọi ô là chuỗi (giữ nguyên số 0 đầu).
    /// </summary>
    public static class TableExcelWriter
    {
        private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Chỉ số kiểu ô trong styles.xml bên dưới.
        private const int StyleTitle = 1;
        private const int StyleInfo = 2;
        private const int StyleHeader = 3;
        private const int StyleText = 4;
        private const int StyleIndex = 5;

        public sealed class Column
        {
            public Column(string title, double width)
            {
                Title = title ?? string.Empty;
                Width = width;
            }

            public string Title { get; private set; }

            public double Width { get; private set; }
        }

        /// <param name="title">Tiêu đề bảng, dòng 1 (gộp ô).</param>
        /// <param name="infoLines">Các dòng thông tin (bộ lọc, thời điểm tra cứu, số bản ghi), mỗi dòng một hàng.</param>
        /// <param name="columns">Cột dữ liệu; cột đầu "STT" do hàm tự thêm và đánh số từ 1.</param>
        /// <param name="rows">Mỗi dòng là danh sách ô theo thứ tự <paramref name="columns"/>.</param>
        /// <param name="onRow">Gọi sau mỗi dòng đã ghi (số dòng dữ liệu) để báo tiến độ và kiểm tra hủy.</param>
        public static void Write(Stream output, string title, IList<string> infoLines, IList<Column> columns, IEnumerable<IList<string>> rows, Action<int> onRow)
        {
            if (output == null) throw new ArgumentNullException("output");
            if (columns == null || columns.Count == 0) throw new ArgumentException("Cần ít nhất một cột.", "columns");

            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                AddPart(zip, "[Content_Types].xml", ContentTypes);
                AddPart(zip, "_rels/.rels", RootRels);
                AddPart(zip, "xl/workbook.xml", Workbook);
                AddPart(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
                AddPart(zip, "xl/styles.xml", Styles);

                var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
                using (var stream = sheet.Open())
                using (var w = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    WriteSheet(w, title, infoLines ?? new string[0], columns, rows ?? new IList<string>[0], onRow);
                }
            }
        }

        private static void WriteSheet(TextWriter w, string title, IList<string> infoLines, IList<Column> columns, IEnumerable<IList<string>> rows, Action<int> onRow)
        {
            int columnCount = columns.Count + 1; // + STT
            string last = ColumnName(columnCount);
            int infoStart = 2;
            int headerRow = infoStart + infoLines.Count + 1; // một dòng trống trước tiêu đề cột
            int firstData = headerRow + 1;

            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            w.Write("<worksheet xmlns=\"" + Main + "\">");
            w.Write("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"" + headerRow + "\" topLeftCell=\"A" + firstData + "\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            w.Write("<sheetFormatPr defaultRowHeight=\"15\"/><cols>");
            w.Write("<col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/>");
            for (int i = 0; i < columns.Count; i++)
            {
                w.Write("<col min=\"" + (i + 2) + "\" max=\"" + (i + 2) + "\" width=\"" + columns[i].Width.ToString("0.#####", CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
            }
            w.Write("</cols><sheetData>");

            w.Write("<row r=\"1\" ht=\"24\" customHeight=\"1\">" + TextCell("A1", title, StyleTitle) + "</row>");
            for (int i = 0; i < infoLines.Count; i++)
            {
                int r = infoStart + i;
                w.Write("<row r=\"" + r + "\">" + TextCell("A" + r, infoLines[i], StyleInfo) + "</row>");
            }

            w.Write("<row r=\"" + headerRow + "\" ht=\"30\" customHeight=\"1\">");
            w.Write(TextCell("A" + headerRow, "STT", StyleHeader));
            for (int i = 0; i < columns.Count; i++) w.Write(TextCell(ColumnName(i + 2) + headerRow, columns[i].Title, StyleHeader));
            w.Write("</row>");

            int n = 0;
            foreach (IList<string> row in rows)
            {
                n++;
                int r = firstData + n - 1;
                w.Write("<row r=\"" + r + "\">");
                w.Write("<c r=\"A" + r + "\" s=\"" + StyleIndex + "\"><v>" + n.ToString(CultureInfo.InvariantCulture) + "</v></c>");
                for (int c = 0; c < columns.Count; c++)
                {
                    string value = row != null && c < row.Count ? row[c] : null;
                    w.Write(TextCell(ColumnName(c + 2) + r, value, StyleText));
                }
                w.Write("</row>");
                if (onRow != null) onRow(n);
            }

            w.Write("</sheetData><mergeCells count=\"" + (1 + infoLines.Count) + "\">");
            w.Write("<mergeCell ref=\"A1:" + last + "1\"/>");
            for (int i = 0; i < infoLines.Count; i++)
            {
                int r = infoStart + i;
                w.Write("<mergeCell ref=\"A" + r + ":" + last + r + "\"/>");
            }
            w.Write("</mergeCells></worksheet>");
        }

        /// <summary>Ô chữ dạng inlineStr: giữ nguyên số 0 đầu (mã TTHC) và không phụ thuộc bảng chuỗi dùng chung.</summary>
        private static string TextCell(string reference, string value, int style)
        {
            if (string.IsNullOrEmpty(value)) return "<c r=\"" + reference + "\" s=\"" + style + "\"/>";
            string preserve = value.Length != value.Trim().Length || value.Contains("\n") ? " xml:space=\"preserve\"" : string.Empty;
            return "<c r=\"" + reference + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t" + preserve + ">" + Escape(value) + "</t></is></c>";
        }

        public static string ColumnName(int index)
        {
            string name = string.Empty;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                name = (char)('A' + rem) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        /// <summary>Thoát ký tự XML và bỏ ký tự điều khiển không hợp lệ (nội dung từ cổng có thể chứa chúng).</summary>
        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                if (!XmlConvert.IsXmlChar(c) && !char.IsSurrogate(c)) continue;
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static void AddPart(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (var w = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                w.Write(content);
            }
        }

        private const string ContentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private const string RootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"" + RelNs + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string Workbook =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"" + Main + "\" xmlns:r=\"" + RelNs + "\">" +
            "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

        private const string WorkbookRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"" + RelNs + "/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"" + RelNs + "/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        // Kiểu ô: 0 mặc định · 1 tiêu đề · 2 dòng thông tin · 3 tiêu đề cột · 4 chữ có viền, tự xuống dòng · 5 STT.
        private const string Styles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"" + Main + "\">" +
            "<fonts count=\"3\">" +
            "<font><sz val=\"11\"/><name val=\"Times New Roman\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Times New Roman\"/></font>" +
            "<font><b/><sz val=\"14\"/><name val=\"Times New Roman\"/></font>" +
            "</fonts>" +
            "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDCE6F1\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right><top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"6\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"top\"/></xf>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }
}
