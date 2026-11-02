using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Exports
{
    /// <summary>
    /// Ghi bảng kê hóa đơn ra .xlsx theo bố cục mẫu của cổng (DANH SÁCH HÓA ĐƠN.xlsx): A3:S3 tiêu đề, A4:S4 khoảng ngày, dòng 6 là tiêu đề cột,
    /// dữ liệu từ dòng 7, 19 cột. Tự dựng SpreadsheetML (một file zip các phần XML) nên không cần thư viện ngoài tương thích net45.
    /// Ghi từng dòng thẳng ra luồng, không giữ cả bảng trong bộ nhớ.
    /// </summary>
    public static class InvoiceExcelWriter
    {
        public const string Title = "DANH SÁCH HÓA ĐƠN";
        public const int HeaderRow = 6;
        public const int FirstDataRow = 7;

        private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Chỉ số kiểu ô trong styles.xml bên dưới.
        private const int StyleTitle = 1;
        private const int StyleSubtitle = 2;
        private const int StyleHeader = 3;
        private const int StyleText = 4;
        private const int StyleMoney = 5;
        private const int StyleDate = 6;
        private const int StyleRate = 7;
        private const int StyleIndex = 8;
        private const int StyleMoneyDecimal = 9;

        public sealed class Column
        {
            public Column(string title, double width)
            {
                Title = title;
                Width = width;
            }

            public string Title { get; private set; }
            public double Width { get; private set; }
        }

        /// <summary>19 cột và độ rộng đúng mẫu của cổng.</summary>
        public static readonly IList<Column> Columns = new List<Column>
        {
            new Column("STT", 7.8125),
            new Column("Ký hiệu mẫu số", 11.71875),
            new Column("Ký hiệu hóa đơn", 11.71875),
            new Column("Số hóa đơn", 11.71875),
            new Column("Ngày lập", 19.53125),
            new Column("MST người bán/MST người xuất hàng", 31.25),
            new Column("Tên người bán/Tên người xuất hàng", 31.25),
            new Column("MST người mua/MST người nhận hàng", 31.25),
            new Column("Tên người mua/Tên người nhận hàng", 31.25),
            new Column("Địa chỉ người mua", 31.25),
            new Column("Tổng tiền chưa thuế", 11.71875),
            new Column("Tổng tiền thuế", 11.71875),
            new Column("Tổng tiền chiết khấu thương mại", 19.53125),
            new Column("Tổng tiền phí", 19.53125),
            new Column("Tổng tiền thanh toán", 19.53125),
            new Column("Đơn vị tiền tệ", 11.71875),
            new Column("Tỷ giá", 11.71875),
            new Column("Trạng thái hóa đơn", 19.53125),
            new Column("Kết quả kiểm tra hóa đơn", 50.78125)
        }.AsReadOnly();

        /// <param name="scopeText">Phạm vi xuất (toàn bộ kết quả hay các dòng đã chọn), ghi ở dòng 2 (FR-EXPORT-003).</param>
        /// <param name="rangeText">Nội dung dòng 4, ví dụ "Từ ngày 01/01/2026 đến ngày 31/12/2026".</param>
        /// <param name="invoices">Hóa đơn theo đúng thứ tự cần ghi; STT đánh lại từ 1 (BR-015).</param>
        /// <param name="onRow">Gọi sau mỗi dòng đã ghi (số dòng dữ liệu) để báo tiến độ và kiểm tra hủy.</param>
        public static void Write(Stream output, string scopeText, string rangeText, IEnumerable<NormalizedInvoice> invoices, Action<int> onRow)
        {
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
                    WriteSheet(w, scopeText, rangeText, invoices, onRow);
                }
            }
        }

        // ---- Trang tính ----

        private static void WriteSheet(TextWriter w, string scopeText, string rangeText, IEnumerable<NormalizedInvoice> invoices, Action<int> onRow)
        {
            var last = ColumnName(Columns.Count);
            w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            w.Write("<worksheet xmlns=\"" + Main + "\">");
            w.Write("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"" + (HeaderRow) + "\" topLeftCell=\"A" + FirstDataRow + "\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            w.Write("<sheetFormatPr defaultRowHeight=\"15\"/>");
            w.Write("<cols>");
            for (var i = 0; i < Columns.Count; i++)
            {
                w.Write("<col min=\"" + (i + 1) + "\" max=\"" + (i + 1) + "\" width=\"" + Columns[i].Width.ToString("0.#####", CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
            }

            w.Write("</cols><sheetData>");

            Row(w, 2, TextCell("A2", scopeText, StyleSubtitle));
            Row(w, 3, TextCell("A3", Title, StyleTitle));
            Row(w, 4, TextCell("A4", rangeText, StyleSubtitle));

            w.Write("<row r=\"" + HeaderRow + "\" ht=\"32\" customHeight=\"1\">");
            for (var i = 0; i < Columns.Count; i++) w.Write(TextCell(ColumnName(i + 1) + HeaderRow, Columns[i].Title, StyleHeader));
            w.Write("</row>");

            var n = 0;
            foreach (var invoice in invoices)
            {
                n++;
                var r = FirstDataRow + n - 1;
                w.Write("<row r=\"" + r + "\">");
                w.Write(NumberCell("A" + r, n, StyleIndex));
                w.Write(TextCell("B" + r, invoice.FormSymbol, StyleText));
                w.Write(TextCell("C" + r, invoice.InvoiceSymbol, StyleText));
                w.Write(TextCell("D" + r, invoice.InvoiceNumber, StyleText));
                w.Write(DateCell("E" + r, invoice.IssueDate, StyleDate));
                w.Write(TextCell("F" + r, invoice.SellerTaxCode, StyleText));
                w.Write(TextCell("G" + r, invoice.SellerName, StyleText));
                w.Write(TextCell("H" + r, invoice.BuyerIdentifier, StyleText));
                w.Write(TextCell("I" + r, invoice.BuyerName, StyleText));
                w.Write(TextCell("J" + r, invoice.BuyerAddress, StyleText));
                w.Write(MoneyCell("K" + r, invoice.TotalBeforeTax));
                w.Write(MoneyCell("L" + r, invoice.TotalTax));
                w.Write(MoneyCell("M" + r, invoice.TotalDiscount));
                w.Write(MoneyCell("N" + r, invoice.TotalFee));
                w.Write(MoneyCell("O" + r, invoice.TotalPayment));
                w.Write(TextCell("P" + r, invoice.Currency, StyleText));
                w.Write(NumberCell("Q" + r, invoice.ExchangeRate, StyleRate));
                w.Write(TextCell("R" + r, PortalOptions.StatusLabel(invoice.StatusRaw), StyleText));
                w.Write(TextCell("S" + r, PortalOptions.CheckResultLabel(invoice.CheckResultRaw), StyleText));
                w.Write("</row>");
                if (onRow != null) onRow(n);
            }

            w.Write("</sheetData><mergeCells count=\"3\">");
            w.Write("<mergeCell ref=\"A2:" + last + "2\"/><mergeCell ref=\"A3:" + last + "3\"/><mergeCell ref=\"A4:" + last + "4\"/>");
            w.Write("</mergeCells></worksheet>");
        }

        private static void Row(TextWriter w, int number, string cell)
        {
            w.Write("<row r=\"" + number + "\">" + cell + "</row>");
        }

        // ---- Ô ----

        /// <summary>Ô chữ ở dạng inlineStr: giữ nguyên số 0 đầu và không phụ thuộc bảng chuỗi dùng chung.</summary>
        private static string TextCell(string reference, string value, int style)
        {
            if (string.IsNullOrEmpty(value)) return "<c r=\"" + reference + "\" s=\"" + style + "\"/>";
            var preserve = value.Length != value.Trim().Length ? " xml:space=\"preserve\"" : string.Empty;
            return "<c r=\"" + reference + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t" + preserve + ">" + Escape(value) + "</t></is></c>";
        }

        /// <summary>Số nguyên hiện #,##0; số có phần lẻ (ngoại tệ) hiện #,##0.00 để không làm tròn khi nhìn. Giá trị trong ô luôn là số gốc.</summary>
        private static string MoneyCell(string reference, decimal? value)
        {
            var integral = value.HasValue && value.Value == decimal.Truncate(value.Value);
            return NumberCell(reference, value, integral ? StyleMoney : StyleMoneyDecimal);
        }

        private static string NumberCell(string reference, decimal? value, int style)
        {
            if (!value.HasValue) return "<c r=\"" + reference + "\" s=\"" + style + "\"/>";
            return "<c r=\"" + reference + "\" s=\"" + style + "\"><v>" + value.Value.ToString(CultureInfo.InvariantCulture) + "</v></c>";
        }

        private static string NumberCell(string reference, int value, int style)
        {
            return "<c r=\"" + reference + "\" s=\"" + style + "\"><v>" + value.ToString(CultureInfo.InvariantCulture) + "</v></c>";
        }

        /// <summary>Ngày lưu dạng số ngày của Excel để sắp xếp và lọc được; hiển thị dd/MM/yyyy.</summary>
        private static string DateCell(string reference, DateTime value, int style)
        {
            var serial = (value.Date - new DateTime(1899, 12, 30)).TotalDays;
            return "<c r=\"" + reference + "\" s=\"" + style + "\"><v>" + serial.ToString("0", CultureInfo.InvariantCulture) + "</v></c>";
        }

        public static string ColumnName(int index)
        {
            var name = string.Empty;
            while (index > 0)
            {
                var rem = (index - 1) % 26;
                name = (char)('A' + rem) + name;
                index = (index - 1) / 26;
            }

            return name;
        }

        /// <summary>Thoát ký tự XML và bỏ ký tự điều khiển không hợp lệ (nội dung từ cổng có thể chứa chúng).</summary>
        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
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

        // ---- Các phần cố định của gói ----

        private static void AddPart(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
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

        // Kiểu ô: 0 mặc định · 1 tiêu đề · 2 phụ đề · 3 tiêu đề cột · 4 chữ · 5 tiền nguyên · 6 ngày · 7 tỷ giá · 8 STT · 9 tiền có phần lẻ.
        private const string Styles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"" + Main + "\">" +
            "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/></numFmts>" +
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
            "<cellXfs count=\"10\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>" +
            "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +
            "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>" +
            "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }
}
