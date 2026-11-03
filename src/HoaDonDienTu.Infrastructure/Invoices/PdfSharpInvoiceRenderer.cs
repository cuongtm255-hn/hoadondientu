using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HoaDonDienTu.Application.Invoices;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;

namespace HoaDonDienTu.Infrastructure.Invoices
{
    /// <summary>
    /// Dàn trang bản thể hiện hóa đơn (A4) theo bố cục trang xem hóa đơn của cổng Thuế (khung viền đôi, mã QR, tiêu đề giữa trang, danh sách thông tin người bán/mua,
    /// bảng hàng hóa, hai bảng tổng hợp, khối chữ ký) bằng PDFsharp 1.50 (MIT, ADR 0009). Một danh sách lệnh vẽ theo trang được phát ra PDF, ảnh xem trước và máy in nên ba
    /// nơi luôn giống nhau. Chữ Times New Roman nhúng Unicode (đủ dấu tiếng Việt). Chỉ vẽ những gì XML có; không tự tính lại số liệu và không khẳng định chữ ký hợp lệ.
    /// </summary>
    public sealed class PdfSharpInvoiceRenderer : IInvoiceDocumentRenderer
    {
        public IInvoiceDocument Build(InvoicePrintModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            return new Document(InvoiceLayout.Build(model), model);
        }

        // ---- Lệnh vẽ ----

        private enum Align
        {
            Left,
            Center,
            Right
        }

        private abstract class Op
        {
        }

        private sealed class TextOp : Op
        {
            public double X;
            public double Y;
            public double Width;
            public string Text;
            public double Size;
            public XFontStyle Style;
            public Align Align;
            public XColor Color = XColors.Black;
        }

        private sealed class LineOp : Op
        {
            public double X1;
            public double Y1;
            public double X2;
            public double Y2;
            public double Thickness = 0.6;
            public XColor Color = XColors.Black;
            public bool Dotted;
        }

        private sealed class PolygonOp : Op
        {
            public XPoint[] Points;
            public XColor Fill;
        }

        private sealed class RectOp : Op
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
            public XColor Fill;
            public bool Filled;
            public bool Border = true;
            public XColor BorderColor = XColors.Black;
            public double BorderThickness = 0.6;
        }

        private sealed class Document : IInvoiceDocument
        {
            private const double PageWidth = 595.28;
            private const double PageHeight = 841.89;
            private readonly IList<IList<Op>> _pages;
            private readonly InvoicePrintModel _model;

            public Document(IList<IList<Op>> pages, InvoicePrintModel model)
            {
                _pages = pages;
                _model = model;
            }

            public int PageCount
            {
                get { return _pages.Count; }
            }

            public byte[] ToPdf()
            {
                var background = LoadBackground();
                var pdf = new PdfDocument();
                pdf.Info.Title = "Hóa đơn " + (_model.InvoiceNumber ?? string.Empty);
                pdf.Info.Creator = "Hóa đơn điện tử (bản thể hiện tạo từ XML)";
                foreach (var ops in _pages)
                {
                    var page = pdf.AddPage();
                    page.Width = XUnit.FromPoint(PageWidth);
                    page.Height = XUnit.FromPoint(PageHeight);
                    using (var graphics = XGraphics.FromPdfPage(page)) Draw(graphics, ops, true, background);
                }

                using (var stream = new MemoryStream())
                {
                    pdf.Save(stream, false);
                    return stream.ToArray();
                }
            }

            public byte[] RenderPagePng(int pageIndex, int dpi)
            {
                if (pageIndex < 0 || pageIndex >= _pages.Count) throw new ArgumentOutOfRangeException("pageIndex");
                if (dpi < 24 || dpi > 600) throw new ArgumentOutOfRangeException("dpi");

                var background = LoadBackground();
                var width = (int)Math.Ceiling(PageWidth * dpi / 72.0);
                var height = (int)Math.Ceiling(PageHeight * dpi / 72.0);
                using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
                {
                    bitmap.SetResolution(dpi, dpi);
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.White);
                        g.PageUnit = GraphicsUnit.Point;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        using (var graphics = XGraphics.FromGraphics(g, new XSize(PageWidth, PageHeight), XGraphicsUnit.Point)) Draw(graphics, _pages[pageIndex], false, background);
                    }

                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        return stream.ToArray();
                    }
                }
            }

            /// <summary>Hoa văn trống đồng làm nền, đúng ảnh nền của trang xem hóa đơn (tài sản của cổng Thuế, nhúng theo yêu cầu của người dùng); null nếu không đọc được.</summary>
            private static XImage LoadBackground()
            {
                try
                {
                    using (var resource = typeof(PdfSharpInvoiceRenderer).Assembly.GetManifestResourceStream("HoaDonDienTu.Invoices.viewinvoice-bg.jpg"))
                    {
                        if (resource == null) return null;
                        var memory = new MemoryStream();
                        resource.CopyTo(memory);
                        memory.Position = 0;
                        return XImage.FromStream(memory);
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }

            private static void Draw(XGraphics graphics, IList<Op> ops, bool forPdf, XImage background)
            {
                var options = forPdf ? new XPdfFontOptions(PdfFontEncoding.Unicode) : null;

                // Ảnh nền vuông phủ hết chiều cao trang, căn giữa theo chiều ngang (phần thừa hai bên bị cắt).
                if (background != null) graphics.DrawImage(background, (PageWidth - PageHeight) / 2, 0, PageHeight, PageHeight);

                foreach (var op in ops)
                {
                    var text = op as TextOp;
                    if (text != null)
                    {
                        var font = new XFont(InvoiceLayout.FontName, text.Size, text.Style, options);
                        var format = new XStringFormat
                        {
                            LineAlignment = XLineAlignment.Near,
                            Alignment = text.Align == Align.Left ? XStringAlignment.Near : text.Align == Align.Center ? XStringAlignment.Center : XStringAlignment.Far
                        };
                        graphics.DrawString(text.Text, font, new XSolidBrush(text.Color), new XRect(text.X, text.Y, text.Width, text.Size * 1.6), format);
                        continue;
                    }

                    var line = op as LineOp;
                    if (line != null)
                    {
                        var pen = new XPen(line.Color, line.Thickness);
                        if (line.Dotted) pen.DashStyle = XDashStyle.Dot;
                        graphics.DrawLine(pen, line.X1, line.Y1, line.X2, line.Y2);
                        continue;
                    }

                    var polygon = op as PolygonOp;
                    if (polygon != null)
                    {
                        graphics.DrawPolygon(new XSolidBrush(polygon.Fill), polygon.Points, XFillMode.Winding);
                        continue;
                    }

                    var rect = (RectOp)op;
                    if (rect.Filled) graphics.DrawRectangle(new XSolidBrush(rect.Fill), rect.X, rect.Y, rect.Width, rect.Height);
                    if (rect.Border) graphics.DrawRectangle(new XPen(rect.BorderColor, rect.BorderThickness), rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        // ---- Dàn trang ----

        private static class InvoiceLayout
        {
            public const string FontName = "Times New Roman";

            private const double PageWidth = 595.28;
            private const double PageHeight = 841.89;
            private const double FrameInset = 10;
            private const double Margin = 42;
            private const double ContentWidth = PageWidth - 2 * Margin;
            private const double BottomLimit = PageHeight - 46;
            private const double BodySize = 9.5;
            private const double CellSize = 9.5;
            private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
            private static readonly XColor Brown = XColor.FromArgb(166, 114, 50);
            private static readonly XColor Gray = XColor.FromArgb(90, 90, 90);

            // Cột bảng hàng hóa (tổng = ContentWidth).
            private static readonly double[] ColumnWidths = { 24, 50, 50, 110, 32, 34, 58, 40, 36, 0 };
            private static readonly string[] ColumnTitles =
            {
                "STT", "Tính chất", "Loại hàng hoá đặc trưng", "Tên hàng hóa, dịch vụ", "Đơn vị tính", "Số lượng", "Đơn giá", "Chiết khấu", "Thuế suất", "Thành tiền chưa có thuế GTGT"
            };

            public static IList<IList<Op>> Build(InvoicePrintModel m)
            {
                using (var bitmap = new Bitmap(1, 1))
                using (var gdi = Graphics.FromImage(bitmap))
                using (var measure = XGraphics.FromGraphics(gdi, new XSize(PageWidth, PageHeight), XGraphicsUnit.Point))
                {
                    var layout = new Builder(measure, m);
                    layout.Run();
                    return layout.Pages;
                }
            }

            private static string TitleOf(InvoicePrintModel m)
            {
                // Tên loại hóa đơn theo ký hiệu mẫu số (TT78): cổng thể hiện tên đầy đủ chứ không phải tên viết tắt trong XML.
                switch (m.FormSymbol)
                {
                    case "1": return "HOÁ ĐƠN GIÁ TRỊ GIA TĂNG";
                    case "2": return "HOÁ ĐƠN BÁN HÀNG";
                    case "3": return "HOÁ ĐƠN BÁN TÀI SẢN CÔNG";
                    case "4": return "HOÁ ĐƠN BÁN HÀNG DỰ TRỮ QUỐC GIA";
                    case "5": return "TEM, VÉ, THẺ ĐIỆN TỬ LÀ HOÁ ĐƠN";
                    case "6": return "PHIẾU XUẤT KHO KIÊM VẬN CHUYỂN NỘI BỘ";
                    default: return string.IsNullOrEmpty(m.Title) ? "HOÁ ĐƠN" : m.Title.ToUpper(Vi);
                }
            }

            private static string KindOf(string code)
            {
                switch (code)
                {
                    case "1": return "Hàng hóa, dịch vụ";
                    case "2": return "Khuyến mại";
                    case "3": return "Chiết khấu thương mại";
                    case "4": return "Ghi chú, diễn giải";
                    default: return code ?? string.Empty;
                }
            }

            private sealed class Builder
            {
                private readonly XGraphics _measure;
                private readonly InvoicePrintModel _m;
                private readonly double[] _columns;
                private IList<Op> _page;
                private double _y;

                public Builder(XGraphics measure, InvoicePrintModel model)
                {
                    _measure = measure;
                    _m = model;
                    _columns = (double[])ColumnWidths.Clone();
                    _columns[_columns.Length - 1] = ContentWidth - _columns.Take(_columns.Length - 1).Sum();
                    Pages = new List<IList<Op>>();
                }

                public IList<IList<Op>> Pages { get; private set; }

                public void Run()
                {
                    NewPage();
                    Header();
                    Parties();
                    LinesTable();
                    Totals();
                    Signatures();
                    NumberPages();
                }

                // ---- Nền tảng ----

                private void NewPage()
                {
                    _page = new List<Op>();
                    Pages.Add(_page);
                    _y = Margin - 10;

                    // Khung viền đôi của trang xem hóa đơn.
                    _page.Add(new RectOp { X = FrameInset, Y = FrameInset, Width = PageWidth - 2 * FrameInset, Height = PageHeight - 2 * FrameInset, BorderColor = Brown, BorderThickness = 1.0 });
                    _page.Add(new RectOp { X = FrameInset + 3.5, Y = FrameInset + 3.5, Width = PageWidth - 2 * FrameInset - 7, Height = PageHeight - 2 * FrameInset - 7, BorderColor = Brown, BorderThickness = 0.4 });
                }

                private double Width(string text, double size, XFontStyle style)
                {
                    return _measure.MeasureString(text, new XFont(FontName, size, style)).Width;
                }

                /// <summary>Ngắt dòng theo từ để vừa bề rộng; từ dài hơn bề rộng được cắt theo ký tự.</summary>
                private IList<string> Wrap(string text, double width, double size, XFontStyle style)
                {
                    var lines = new List<string>();
                    if (string.IsNullOrEmpty(text)) return lines;
                    foreach (var paragraph in text.Replace("\r", string.Empty).Split('\n'))
                    {
                        var current = new StringBuilder();
                        foreach (var word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var candidate = current.Length == 0 ? word : current + " " + word;
                            if (Width(candidate, size, style) <= width)
                            {
                                current.Clear();
                                current.Append(candidate);
                                continue;
                            }

                            if (current.Length > 0) lines.Add(current.ToString());
                            current.Clear();
                            var rest = word;
                            while (Width(rest, size, style) > width && rest.Length > 1)
                            {
                                var cut = rest.Length - 1;
                                while (cut > 1 && Width(rest.Substring(0, cut), size, style) > width) cut--;
                                lines.Add(rest.Substring(0, cut));
                                rest = rest.Substring(cut);
                            }

                            current.Append(rest);
                        }

                        lines.Add(current.ToString());
                    }

                    return lines;
                }

                private static double LineStep(double size)
                {
                    return size * 1.22;
                }

                private double TextHeight(string text, double width, double size, XFontStyle style)
                {
                    return Math.Max(1, Wrap(text, width, size, style).Count) * LineStep(size);
                }

                private double Text(double x, double y, double width, string text, double size, XFontStyle style, Align align, XColor? color = null)
                {
                    var lines = Wrap(text, width, size, style);
                    var step = LineStep(size);
                    for (var i = 0; i < lines.Count; i++)
                    {
                        _page.Add(new TextOp { X = x, Y = y + i * step, Width = width, Text = lines[i], Size = size, Style = style, Align = align, Color = color ?? XColors.Black });
                    }

                    return Math.Max(1, lines.Count) * step;
                }

                private void Rule()
                {
                    _page.Add(new LineOp { X1 = Margin, Y1 = _y, X2 = Margin + ContentWidth, Y2 = _y, Color = Brown, Thickness = 0.8 });
                    _y += 6;
                }

                // ---- Phần đầu ----

                private void Header()
                {
                    const double qrSize = 60;
                    var top = _y;
                    if (!string.IsNullOrEmpty(_m.QrPayload)) DrawQr(Margin, top, qrSize);

                    var right = Margin + ContentWidth - 150;
                    var line = top + 4;
                    foreach (var text in new[] { "Mẫu số " + (_m.FormSymbol ?? string.Empty), "Ký hiệu: " + (_m.InvoiceSymbol ?? string.Empty), "Số: " + (_m.InvoiceNumber ?? string.Empty) })
                    {
                        Text(right, line, 150, text, BodySize, XFontStyle.Bold, Align.Left);
                        line += LineStep(BodySize) + 1;
                    }

                    _y = top + qrSize + 6;
                    _y += Text(Margin, _y, ContentWidth, TitleOf(_m), 19, XFontStyle.Bold, Align.Center) + 4;
                    if (_m.IssueDate.HasValue)
                    {
                        var d = _m.IssueDate.Value;
                        _y += Text(Margin, _y, ContentWidth, "Ngày " + d.Day.ToString("00") + " tháng " + d.Month.ToString("00") + " năm " + d.Year, 11, XFontStyle.Regular, Align.Center) + 4;
                    }

                    if (!string.IsNullOrEmpty(_m.AuthorityCode)) _y += Text(Margin, _y, ContentWidth, "MCCQT: " + _m.AuthorityCode, 11, XFontStyle.Regular, Align.Center);
                    _y += 6;
                    Rule();
                }

                private void DrawQr(double x, double y, double size)
                {
                    try
                    {
                        using (var generator = new QRCodeGenerator())
                        using (var data = generator.CreateQrCode(_m.QrPayload, QRCodeGenerator.ECCLevel.M))
                        {
                            var matrix = data.ModuleMatrix;
                            var count = matrix.Count;
                            var cell = size / count;
                            for (var row = 0; row < count; row++)
                            {
                                var column = 0;
                                while (column < count)
                                {
                                    if (!matrix[row][column])
                                    {
                                        column++;
                                        continue;
                                    }

                                    var start = column;
                                    while (column < count && matrix[row][column]) column++;
                                    _page.Add(new RectOp { X = x + start * cell, Y = y + row * cell, Width = (column - start) * cell + 0.15, Height = cell + 0.15, Filled = true, Fill = XColors.Black, Border = false });
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Nội dung QR quá dài hoặc không mã hóa được: bỏ ô QR thay vì làm hỏng cả bản in.
                    }
                }

                // ---- Người bán / người mua ----

                private void Row(string label, string value)
                {
                    var text = label + " " + (value ?? string.Empty);
                    if (_y + TextHeight(text, ContentWidth, BodySize, XFontStyle.Regular) > BottomLimit) NewPage();
                    var height = Text(Margin, _y, ContentWidth, text.TrimEnd(), BodySize, XFontStyle.Regular, Align.Left);
                    DottedRule(Margin, Margin + ContentWidth, _y + height + 0.5);
                    _y += height + 1.5;
                }

                private void DottedRule(double x1, double x2, double y)
                {
                    _page.Add(new LineOp { X1 = x1, Y1 = y, X2 = x2, Y2 = y, Color = XColor.FromArgb(150, 150, 150), Thickness = 0.5, Dotted = true });
                }

                private static string Account(InvoicePrintParty p)
                {
                    if (string.IsNullOrEmpty(p.BankAccount)) return string.Empty;
                    return string.IsNullOrEmpty(p.BankName) ? p.BankAccount : p.BankAccount + " - " + p.BankName;
                }

                private void Parties()
                {
                    var s = _m.Seller;
                    Row("Tên người bán:", s.Name);
                    Row("Mã số thuế:", s.TaxCode);
                    Row("Mã cửa hàng:", s.StoreCode);
                    Row("Tên cửa hàng:", s.StoreName);
                    Row("Địa chỉ:", s.Address);
                    Row("Điện thoại:", s.Phone);
                    Row("Số tài khoản:", Account(s));
                    _y += 2;
                    Rule();

                    var b = _m.Buyer;
                    Row("Tên người mua:", b.Name);
                    Row("Họ tên người mua hàng:", b.ContactName);
                    Row("Mã số thuế:", b.TaxCode);
                    Row("Mã ĐVCQHVNSNN:", b.BudgetUnitCode);
                    Row("CCCD người mua:", b.IdNumber);
                    Row("Số hộ chiếu:", b.PassportNumber);
                    Row("Địa chỉ:", b.Address);
                    Row("Điện thoại:", b.Phone);
                    Row("Số tài khoản:", Account(b));
                    Row("Hình thức thanh toán:", _m.PaymentMethod);
                    Row("Đơn vị tiền tệ:" , _m.Currency + (!string.IsNullOrEmpty(_m.Currency) && _m.Currency != "VND" && _m.ExchangeRate.HasValue ? " (tỷ giá " + Number(_m.ExchangeRate) + ")" : string.Empty));
                    if (_y + LineStep(BodySize) > BottomLimit) NewPage();
                    Text(Margin, _y, 250, "Số bảng kê: " + (_m.ListNumber ?? string.Empty), BodySize, XFontStyle.Regular, Align.Left);
                    var listHeight = Text(Margin + 250, _y, ContentWidth - 250, "Ngày bảng kê: " + (_m.ListDate ?? string.Empty), BodySize, XFontStyle.Regular, Align.Left);
                    DottedRule(Margin, Margin + 240, _y + listHeight + 0.5);
                    DottedRule(Margin + 250, Margin + ContentWidth, _y + listHeight + 0.5);
                    _y += listHeight + 8;
                }

                // ---- Bảng ----

                private double Cell(double x, double y, double width, double height, string text, XFontStyle style, Align align, bool border = true)
                {
                    if (border) _page.Add(new RectOp { X = x, Y = y, Width = width, Height = height });
                    if (!string.IsNullOrEmpty(text)) Text(x + 3, y + 3, width - 6, text, CellSize, style, align);
                    return height;
                }

                private double RowHeight(IList<double> widths, IList<string> cells, XFontStyle style)
                {
                    var tallest = 1.0;
                    for (var i = 0; i < widths.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(cells[i])) tallest = Math.Max(tallest, TextHeight(cells[i], widths[i] - 6, CellSize, style));
                    }

                    return tallest + 6;
                }

                private void TableHeader()
                {
                    var height = RowHeight(_columns, ColumnTitles, XFontStyle.Bold);
                    var x = Margin;
                    for (var i = 0; i < _columns.Length; i++)
                    {
                        Cell(x, _y, _columns[i], height, ColumnTitles[i], XFontStyle.Bold, Align.Center);
                        x += _columns[i];
                    }

                    _y += height;
                }

                private void LinesTable()
                {
                    var tableTop = _y;
                    TableHeader();
                    foreach (var line in _m.Lines)
                    {
                        var isNote = line.Kind == "4";
                        var cells = new[]
                        {
                            line.Number, KindOf(line.Kind), null, line.Name, isNote ? null : line.Unit, isNote ? null : Number(line.Quantity),
                            isNote ? null : Number(line.UnitPrice), isNote ? null : Number(line.DiscountAmount), isNote ? null : line.TaxRate, isNote ? null : Number(line.Amount)
                        };
                        var height = RowHeight(_columns, cells, XFontStyle.Regular);
                        if (_y + height > BottomLimit - 20)
                        {
                            NewPage();
                            TableHeader();
                        }

                        var x = Margin;
                        for (var i = 0; i < _columns.Length; i++)
                        {
                            var align = i == 1 || i == 2 || i == 3 || i == 4 ? Align.Left : Align.Center;
                            Cell(x, _y, _columns[i], height, cells[i], XFontStyle.Regular, align);
                            x += _columns[i];
                        }

                        _y += height;
                    }

                    if (_m.Lines.Count == 0)
                    {
                        Cell(Margin, _y, ContentWidth, 24, "(XML không có dòng hàng hóa, dịch vụ)", XFontStyle.Italic, Align.Center);
                        _y += 24;
                    }

                    _y += 8;
                }

                private void Totals()
                {
                    // Bảng thuế suất bên trái, bảng tổng hợp bên phải (như trang xem của cổng).
                    var leftWidths = new[] { 52.0, 78.0, 70.0 };
                    var rightLabelWidth = 190.0;
                    var rightWidth = ContentWidth - 200 - 10;
                    var valueWidth = rightWidth - rightLabelWidth;

                    var rightRows = new List<string[]>
                    {
                        new[] { "Tổng tiền chưa thuế\n(Tổng cộng thành tiền chưa có thuế)", Money(_m.TotalBeforeTax) },
                        new[] { "Tổng tiền thuế (Tổng cộng tiền thuế)", Money(_m.TotalTax) },
                        new[] { "Tổng tiền phí", Money(_m.TotalFee) },
                        new[] { "Tổng tiền chiết khấu thương mại", Money(_m.TotalDiscount) },
                        new[] { "Tổng tiền thanh toán bằng số", Money(_m.TotalPayment) },
                        new[] { "Tổng tiền thanh toán bằng chữ", _m.TotalInWords }
                    };
                    var rightHeights = rightRows.Select(r => RowHeight(new[] { rightLabelWidth, valueWidth }, r, XFontStyle.Regular)).ToList();
                    var taxRows = _m.TaxLines.Select(t => new[] { t.Rate, Number(t.TaxableAmount), Number(t.TaxAmount) }).ToList();
                    var leftHeader = new[] { "Thuế suất", "Tổng tiền chưa thuế", "Tổng tiền thuế" };
                    var leftHeaderHeight = RowHeight(leftWidths, leftHeader, XFontStyle.Bold);
                    var leftHeights = taxRows.Select(r => RowHeight(leftWidths, r, XFontStyle.Regular)).ToList();

                    var need = Math.Max(rightHeights.Sum(), leftHeaderHeight + leftHeights.Sum());
                    if (_y + need > BottomLimit) NewPage();

                    var top = _y;
                    var x = Margin;
                    for (var i = 0; i < leftWidths.Length; i++)
                    {
                        Cell(x, top, leftWidths[i], leftHeaderHeight, leftHeader[i], XFontStyle.Bold, Align.Center);
                        x += leftWidths[i];
                    }

                    var y = top + leftHeaderHeight;
                    for (var r = 0; r < taxRows.Count; r++)
                    {
                        x = Margin;
                        for (var i = 0; i < leftWidths.Length; i++)
                        {
                            Cell(x, y, leftWidths[i], leftHeights[r], taxRows[r][i], XFontStyle.Regular, Align.Center);
                            x += leftWidths[i];
                        }

                        y += leftHeights[r];
                    }

                    var rightX = Margin + 200 + 10;
                    y = top;
                    for (var r = 0; r < rightRows.Count; r++)
                    {
                        Cell(rightX, y, rightLabelWidth, rightHeights[r], rightRows[r][0], XFontStyle.Regular, Align.Center);
                        Cell(rightX + rightLabelWidth, y, valueWidth, rightHeights[r], rightRows[r][1], XFontStyle.Regular, Align.Center);
                        y += rightHeights[r];
                    }

                    _y = top + need + 6;
                }

                // ---- Chữ ký ----

                private void Signatures()
                {
                    if (_y + 118 > BottomLimit) NewPage();
                    Rule();
                    var half = ContentWidth / 2;
                    var top = _y;
                    Text(Margin, top, half, "NGƯỜI MUA HÀNG", 13, XFontStyle.Regular, Align.Center);
                    Text(Margin, top + 17, half, "(Chữ ký số (nếu có))", 10, XFontStyle.Italic, Align.Center);
                    Text(Margin + half, top, half, "NGƯỜI BÁN HÀNG", 13, XFontStyle.Regular, Align.Center);
                    Text(Margin + half, top + 17, half, "(Chữ ký điện tử, chữ ký số)", 10, XFontStyle.Italic, Align.Center);

                    var boxTop = top + 36;
                    double boxHeight = 0;
                    var status = _m.SellerSignatureStatus;
                    var hasSigner = !string.IsNullOrEmpty(_m.SellerSignedAtText) || _m.SellerSignedAt.HasValue;
                    if (status != InvoiceSignatureStatus.None || hasSigner)
                    {
                        // Trạng thái do ứng dụng kiểm trên chính file XML (InvoiceSignatureVerifier): "Signature Valid" và dấu tích chỉ khi chữ ký toàn vẹn thật.
                        var signed = _m.SellerSignedAtText ?? (_m.SellerSignedAt.HasValue ? _m.SellerSignedAt.Value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty);
                        var signer = _m.SellerSignerName ?? _m.Seller.Name ?? string.Empty;
                        XColor color;
                        var lines = new List<string>();
                        switch (status)
                        {
                            case InvoiceSignatureStatus.Valid:
                                color = XColor.FromArgb(33, 181, 15);
                                lines.Add("Signature Valid");
                                break;
                            case InvoiceSignatureStatus.Invalid:
                                color = XColor.FromArgb(200, 30, 30);
                                lines.Add("Signature Invalid");
                                lines.Add("XML đã bị sửa sau khi ký hoặc chữ ký sai");
                                break;
                            default:
                                color = Gray;
                                lines.Add("Chưa kiểm tra được chữ ký số");
                                break;
                        }

                        lines.Add("Ký bởi " + signer);
                        if (!string.IsNullOrEmpty(signed)) lines.Add("Ký ngày: " + signed);

                        var boxWidth = half - 40;
                        var boxX = Margin + half + 20;
                        var used = 0.0;
                        var heights = lines.Select(l => TextHeight(l, boxWidth - 12, 10.5, XFontStyle.Regular)).ToList();
                        boxHeight = heights.Sum() + 12;
                        _page.Add(new RectOp { X = boxX, Y = boxTop, Width = boxWidth, Height = boxHeight, BorderColor = color, BorderThickness = 1.2 });
                        for (var i = 0; i < lines.Count; i++)
                        {
                            Text(boxX + 6, boxTop + 6 + used, boxWidth - 12, lines[i], 10.5, XFontStyle.Regular, Align.Left, color);
                            used += heights[i];
                        }

                        if (status == InvoiceSignatureStatus.Valid) AddCheckMark(boxX + boxWidth - 78, boxTop + boxHeight - 36, 52, 46, color);
                    }

                    _y = boxTop + Math.Max(boxHeight, 36) + 10;
                    _y += Text(Margin, _y, ContentWidth, "(Cần kiểm tra, đối chiếu khi lập, nhận hóa đơn)", 10, XFontStyle.Italic, Align.Center) + 4;
                    Text(
                        Margin, _y, ContentWidth,
                        "Bản thể hiện này do ứng dụng dựng từ file XML đã lưu; file XML mới là chứng từ gốc. Trạng thái chữ ký do ứng dụng kiểm trên file XML (toàn vẹn và chữ ký, chưa kiểm chuỗi chứng thư số).",
                        7.5, XFontStyle.Italic, Align.Center, Gray);
                }

                /// <summary>Dấu tích xanh (đa giác gần giống dấu tích của cổng), vẽ vector để in nét ở mọi độ phân giải và không có nền trắng.</summary>
                private void AddCheckMark(double x, double y, double width, double height, XColor color)
                {
                    var unit = new[]
                    {
                        new[] { 0.00, 0.58 }, new[] { 0.28, 0.74 }, new[] { 0.45, 0.52 }, new[] { 0.65, 0.28 }, new[] { 0.85, 0.10 }, new[] { 1.00, 0.00 },
                        new[] { 0.80, 0.22 }, new[] { 0.60, 0.50 }, new[] { 0.45, 0.78 }, new[] { 0.33, 1.00 }
                    };
                    _page.Add(new PolygonOp { Points = unit.Select(p => new XPoint(x + p[0] * width, y + p[1] * height)).ToArray(), Fill = color });
                }

                private void NumberPages()
                {
                    for (var i = 0; i < Pages.Count; i++)
                    {
                        Pages[i].Add(new TextOp { X = Margin, Y = PageHeight - 29, Width = ContentWidth, Text = "Trang " + (i + 1) + "/" + Pages.Count, Size = 8, Style = XFontStyle.Regular, Align = Align.Center, Color = Gray });
                    }
                }

                private static string Money(decimal? value)
                {
                    return value.HasValue ? Number(value) : null;
                }

                private static string Number(decimal? value)
                {
                    return value.HasValue ? value.Value.ToString("#,##0.####", Vi) : null;
                }
            }
        }
    }
}
