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

namespace HoaDonDienTu.Infrastructure.Invoices
{
    /// <summary>
    /// Dàn trang bản thể hiện hóa đơn (A4) bằng PDFsharp 1.50 (MIT, ADR 0009): một danh sách lệnh vẽ theo trang được phát ra PDF, ảnh xem trước và máy in,
    /// nên ba nơi luôn giống nhau. Chữ Arial nhúng dạng Unicode (có đủ dấu tiếng Việt). Chỉ vẽ những gì XML có; không tự tính lại số liệu.
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
            public double Thickness = 0.5;
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
                var pdf = new PdfDocument();
                pdf.Info.Title = "Hóa đơn " + (_model.InvoiceNumber ?? string.Empty);
                pdf.Info.Creator = "Hóa đơn điện tử (bản thể hiện tạo từ XML)";
                foreach (var ops in _pages)
                {
                    var page = pdf.AddPage();
                    page.Width = XUnit.FromPoint(PageWidth);
                    page.Height = XUnit.FromPoint(PageHeight);
                    using (var graphics = XGraphics.FromPdfPage(page)) Draw(graphics, ops, true);
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
                        using (var graphics = XGraphics.FromGraphics(g, new XSize(PageWidth, PageHeight), XGraphicsUnit.Point)) Draw(graphics, _pages[pageIndex], false);
                    }

                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, ImageFormat.Png);
                        return stream.ToArray();
                    }
                }
            }

            private static void Draw(XGraphics graphics, IList<Op> ops, bool forPdf)
            {
                var options = forPdf ? new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.Always) : null;
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
                        graphics.DrawLine(new XPen(XColors.Black, line.Thickness), line.X1, line.Y1, line.X2, line.Y2);
                        continue;
                    }

                    var rect = (RectOp)op;
                    if (rect.Filled) graphics.DrawRectangle(new XSolidBrush(rect.Fill), rect.X, rect.Y, rect.Width, rect.Height);
                    if (rect.Border) graphics.DrawRectangle(new XPen(XColors.Black, 0.5), rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        // ---- Dàn trang ----

        private static class InvoiceLayout
        {
            public const string FontName = "Arial";

            private const double PageWidth = 595.28;
            private const double PageHeight = 841.89;
            private const double Margin = 36;
            private const double ContentWidth = PageWidth - 2 * Margin;
            private const double BottomLimit = PageHeight - 46;
            private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

            // Cột bảng hàng hóa (tổng = ContentWidth).
            private static readonly double[] ColumnWidths = { 28, 174, 36, 46, 76, 54, 101.28 };
            private static readonly string[] ColumnTitles = { "STT", "Tên hàng hóa, dịch vụ", "ĐVT", "Số lượng", "Đơn giá", "Thuế suất", "Thành tiền" };

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

            private sealed class Builder
            {
                private readonly XGraphics _measure;
                private readonly InvoicePrintModel _m;
                private IList<Op> _page;
                private double _y;

                public Builder(XGraphics measure, InvoicePrintModel model)
                {
                    _measure = measure;
                    _m = model;
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
                    SignaturesAndNote();
                    NumberPages();
                }

                private void NewPage()
                {
                    _page = new List<Op>();
                    Pages.Add(_page);
                    _y = Margin;
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

                private double Text(double x, double y, double width, string text, double size, XFontStyle style, Align align)
                {
                    var lines = Wrap(text, width, size, style);
                    var step = size * 1.35;
                    for (var i = 0; i < lines.Count; i++)
                    {
                        _page.Add(new TextOp { X = x, Y = y + i * step, Width = width, Text = lines[i], Size = size, Style = style, Align = align });
                    }

                    return Math.Max(1, lines.Count) * step;
                }

                private void Header()
                {
                    var title = string.IsNullOrEmpty(_m.Title) ? "HÓA ĐƠN" : _m.Title.ToUpper(Vi);
                    _y += Text(Margin, _y, ContentWidth, title, 15, XFontStyle.Bold, Align.Center);
                    _y += Text(Margin, _y, ContentWidth, "(Bản thể hiện của hóa đơn điện tử)", 8.5, XFontStyle.Italic, Align.Center);
                    if (_m.IssueDate.HasValue)
                    {
                        var d = _m.IssueDate.Value;
                        _y += Text(Margin, _y + 2, ContentWidth, "Ngày " + d.Day.ToString("00") + " tháng " + d.Month.ToString("00") + " năm " + d.Year, 9.5, XFontStyle.Italic, Align.Center);
                    }

                    _y += 6;
                    var symbol = (_m.FormSymbol ?? string.Empty) + (_m.InvoiceSymbol ?? string.Empty);
                    var left = Margin;
                    Text(left, _y, ContentWidth, "Mẫu số / Ký hiệu: " + symbol, 10, XFontStyle.Regular, Align.Right);
                    _y += 14;
                    Text(left, _y, ContentWidth, "Số: " + (_m.InvoiceNumber ?? string.Empty), 11, XFontStyle.Bold, Align.Right);
                    _y += 20;
                    Rule();
                }

                private void Rule()
                {
                    _page.Add(new LineOp { X1 = Margin, Y1 = _y, X2 = Margin + ContentWidth, Y2 = _y });
                    _y += 6;
                }

                private void LabelRow(string label, string value, bool bold = false)
                {
                    if (string.IsNullOrEmpty(value)) return;
                    const double labelWidth = 118;
                    var height = Math.Max(
                        Text(Margin, _y, labelWidth, label, 9.5, XFontStyle.Regular, Align.Left),
                        Text(Margin + labelWidth, _y, ContentWidth - labelWidth, value, 9.5, bold ? XFontStyle.Bold : XFontStyle.Regular, Align.Left));
                    _y += height + 1;
                }

                private void Parties()
                {
                    LabelRow("Đơn vị bán hàng:", _m.Seller.Name, true);
                    LabelRow("Mã số thuế:", _m.Seller.TaxCode);
                    LabelRow("Địa chỉ:", _m.Seller.Address);
                    LabelRow("Điện thoại:", _m.Seller.Phone);
                    LabelRow("Email:", _m.Seller.Email);
                    LabelRow("Số tài khoản:", Join(_m.Seller.BankAccount, _m.Seller.BankName));
                    _y += 3;
                    Rule();
                    LabelRow("Họ tên người mua hàng:", _m.Buyer.ContactName);
                    LabelRow("Tên đơn vị:", _m.Buyer.Name, true);
                    LabelRow("Mã số thuế:", _m.Buyer.TaxCode);
                    LabelRow("CCCD:", _m.Buyer.IdNumber);
                    LabelRow("Địa chỉ:", _m.Buyer.Address);
                    LabelRow("Hình thức thanh toán:", _m.PaymentMethod);
                    if (!string.IsNullOrEmpty(_m.Currency) && _m.Currency != "VND")
                    {
                        LabelRow("Đồng tiền / tỷ giá:", _m.Currency + (_m.ExchangeRate.HasValue ? " / " + Number(_m.ExchangeRate) : string.Empty));
                    }

                    _y += 4;
                }

                private static string Join(string a, string b)
                {
                    if (string.IsNullOrEmpty(a)) return b;
                    return string.IsNullOrEmpty(b) ? a : a + " - " + b;
                }

                private void TableHeader()
                {
                    const double height = 20;
                    var x = Margin;
                    for (var i = 0; i < ColumnWidths.Length; i++)
                    {
                        _page.Add(new RectOp { X = x, Y = _y, Width = ColumnWidths[i], Height = height, Filled = true, Fill = XColor.FromArgb(235, 235, 235) });
                        Text(x + 2, _y + 5, ColumnWidths[i] - 4, ColumnTitles[i], 8.5, XFontStyle.Bold, Align.Center);
                        x += ColumnWidths[i];
                    }

                    _y += height;
                }

                private void LinesTable()
                {
                    TableHeader();
                    foreach (var line in _m.Lines)
                    {
                        var isNote = line.Kind == "4";
                        var name = line.Name ?? string.Empty;
                        var nameLines = Wrap(name, ColumnWidths[1] - 6, 9, XFontStyle.Regular).Count;
                        var rowHeight = Math.Max(1, nameLines) * 9 * 1.35 + 8;
                        if (_y + rowHeight > BottomLimit - 30)
                        {
                            NewPage();
                            TableHeader();
                        }

                        var cells = isNote
                            ? new[] { line.Number, name, null, null, null, null, null }
                            : new[] { line.Number, name, line.Unit, Number(line.Quantity), Number(line.UnitPrice), line.TaxRate, Number(line.Amount) };
                        var x = Margin;
                        for (var i = 0; i < ColumnWidths.Length; i++)
                        {
                            _page.Add(new RectOp { X = x, Y = _y, Width = ColumnWidths[i], Height = rowHeight });
                            if (!string.IsNullOrEmpty(cells[i]))
                            {
                                var align = i == 1 || i == 2 ? Align.Left : i == 0 || i == 5 ? Align.Center : Align.Right;
                                Text(x + 3, _y + 4, ColumnWidths[i] - 6, cells[i], 9, i == 1 && isNote ? XFontStyle.Italic : XFontStyle.Regular, align);
                            }

                            x += ColumnWidths[i];
                        }

                        _y += rowHeight;
                    }

                    if (_m.Lines.Count == 0)
                    {
                        _page.Add(new RectOp { X = Margin, Y = _y, Width = ContentWidth, Height = 24 });
                        Text(Margin, _y + 6, ContentWidth, "(XML không có dòng hàng hóa, dịch vụ)", 9, XFontStyle.Italic, Align.Center);
                        _y += 24;
                    }

                    _y += 8;
                }

                private void TotalRow(string label, string value, bool bold)
                {
                    if (string.IsNullOrEmpty(value)) return;
                    if (_y + 16 > BottomLimit) NewPage();
                    var style = bold ? XFontStyle.Bold : XFontStyle.Regular;
                    Text(Margin, _y, ContentWidth - 110, label, 9.5, style, Align.Right);
                    Text(Margin + ContentWidth - 105, _y, 105, value, 9.5, style, Align.Right);
                    _y += 14;
                }

                private void Totals()
                {
                    var currency = string.IsNullOrEmpty(_m.Currency) ? string.Empty : " " + _m.Currency;
                    TotalRow("Cộng tiền hàng (chưa thuế):", Money(_m.TotalBeforeTax), false);
                    foreach (var tax in _m.TaxLines)
                    {
                        TotalRow("Thuế GTGT (" + (tax.Rate ?? "?") + ")" + (tax.TaxableAmount.HasValue ? " trên " + Number(tax.TaxableAmount) : string.Empty) + ":", Money(tax.TaxAmount), false);
                    }

                    TotalRow("Tổng tiền thuế:", Money(_m.TotalTax), false);
                    TotalRow("Tổng tiền chiết khấu thương mại:", Money(_m.TotalDiscount), false);
                    TotalRow("Tổng tiền thanh toán" + currency + ":", Money(_m.TotalPayment), true);
                    if (!string.IsNullOrEmpty(_m.TotalInWords))
                    {
                        if (_y + 30 > BottomLimit) NewPage();
                        _y += Text(Margin, _y + 2, ContentWidth, "Số tiền viết bằng chữ: " + _m.TotalInWords, 9.5, XFontStyle.Italic, Align.Left) + 4;
                    }

                    _y += 6;
                }

                private void SignaturesAndNote()
                {
                    if (_y + 70 > BottomLimit) NewPage();
                    if (_m.SellerSignedAt.HasValue)
                    {
                        _y += Text(Margin, _y, ContentWidth, "Người bán đã ký số ngày " + _m.SellerSignedAt.Value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture), 9, XFontStyle.Regular, Align.Left);
                    }

                    if (!string.IsNullOrEmpty(_m.AuthorityCode))
                    {
                        _y += Text(Margin, _y, ContentWidth, "Mã của cơ quan thuế: " + _m.AuthorityCode, 9, XFontStyle.Bold, Align.Left);
                    }

                    if (_m.AuthoritySignedAt.HasValue)
                    {
                        _y += Text(Margin, _y, ContentWidth, "Cơ quan thuế ký ngày " + _m.AuthoritySignedAt.Value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture), 9, XFontStyle.Regular, Align.Left);
                    }

                    _y += 6;
                    Text(
                        Margin, _y, ContentWidth,
                        "Bản thể hiện này do ứng dụng dựng từ file XML đã lưu để xem và in; file XML mới là chứng từ gốc. Chữ ký số chỉ kiểm được trên file XML.",
                        8, XFontStyle.Italic, Align.Left);
                }

                private void NumberPages()
                {
                    for (var i = 0; i < Pages.Count; i++)
                    {
                        Pages[i].Add(new TextOp { X = Margin, Y = PageHeight - 30, Width = ContentWidth, Text = "Trang " + (i + 1) + "/" + Pages.Count, Size = 8, Style = XFontStyle.Regular, Align = Align.Center });
                    }
                }

                private string Money(decimal? value)
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
