using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>Một file trong gói xem hóa đơn.</summary>
    public sealed class ViewerFile
    {
        public ViewerFile(string name, byte[] content)
        {
            Name = name;
            Content = content;
        }

        public string Name { get; private set; }
        public byte[] Content { get; private set; }
    }

    /// <summary>
    /// Gói file giống gói "Xuất xml" của cổng Thuế: <c>invoice.xml</c> (nguyên byte) cùng trang xem <c>invoice.html</c>, <c>details.js</c>, <c>sign-check.jpg</c> và
    /// <c>viewinvoice-bg.jpg</c>. <c>invoice.html</c> được dựng lại từ XML trên đúng khung CSS của trang xem của cổng (cổng dựng sẵn dữ liệu vào HTML, không đọc XML lúc mở);
    /// script và ảnh là tài sản của cổng, nhúng nguyên bản. Trạng thái chữ ký trong HTML do ứng dụng kiểm trên XML chứ không mặc định là hợp lệ.
    /// </summary>
    public static class InvoiceViewerPackage
    {
        public const string XmlName = "invoice.xml";
        public const string HtmlName = "invoice.html";
        public const string ScriptName = "details.js";
        public const string CheckImageName = "sign-check.jpg";
        public const string BackgroundName = "viewinvoice-bg.jpg";

        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>Năm file của gói. <paramref name="html"/> null khi XML không đọc được thành hóa đơn: khi đó gói chỉ có <c>invoice.xml</c>.</summary>
        public static IList<ViewerFile> Files(byte[] xml)
        {
            var files = new List<ViewerFile> { new ViewerFile(XmlName, xml) };
            InvoicePrintModel model;
            try
            {
                model = InvoicePrintModelParser.Parse(xml);
            }
            catch (InvoicePrintException)
            {
                return files;
            }

            files.Add(new ViewerFile(HtmlName, new UTF8Encoding(false).GetBytes(Html(model))));
            files.Add(new ViewerFile(ScriptName, Resource("details.js")));
            files.Add(new ViewerFile(CheckImageName, Resource("sign-check.jpg")));
            files.Add(new ViewerFile(BackgroundName, Resource("viewinvoice-bg.jpg")));
            return files;
        }

        /// <summary>Ảnh nền hoa văn trống đồng của trang xem hóa đơn; null nếu tài nguyên không có.</summary>
        public static byte[] Background()
        {
            return TryResource("viewinvoice-bg.jpg");
        }

        // ---- Tên theo mẫu số ----

        /// <summary>Tên loại hóa đơn theo ký hiệu mẫu số (TT78): cổng thể hiện tên đầy đủ chứ không phải tên viết tắt trong XML.</summary>
        public static string TitleOf(InvoicePrintModel m)
        {
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

        public static string KindOf(string code)
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

        // ---- invoice.html ----

        /// <summary>Trang HTML tự chứa dữ liệu, cùng cấu trúc và lớp CSS với <c>invoice.html</c> của cổng.</summary>
        public static string Html(InvoicePrintModel m)
        {
            var head = Encoding.UTF8.GetString(Resource("invoice.head.html")).TrimStart('﻿');
            var b = new StringBuilder(head.Length + 8192);
            b.Append(head);
            if (!head.EndsWith("\n", StringComparison.Ordinal)) b.Append('\n');
            b.Append("<body>\n<div class=\"main-page\">\n<div class=\"heading-content\">\n<div class=\"top-content\">\n");
            b.Append("<div style=\"width: 80px; min-height: 20px\">\n<div id=\"qrcodeTable\"></div>\n</div>\n");
            b.Append("<div class=\"code-content\">\n");
            b.Append("<b>Mẫu số: ").Append(E(m.FormSymbol)).Append("</b>\n<br>\n");
            b.Append("<b>Ký hiệu: ").Append(E(m.InvoiceSymbol)).Append("</b>\n<br>\n");
            b.Append("<b>Số: ").Append(E(m.InvoiceNumber)).Append("</b>\n");
            b.Append("</div>\n</div>\n");
            b.Append("<div class=\"title-heading\">\n<h2 class=\"main-title\">").Append(E(TitleOf(m))).Append("</h2>\n<div class=\"day\">\n");
            if (m.IssueDate.HasValue)
            {
                var d = m.IssueDate.Value;
                b.Append("<p class=\"day\">Ngày ").Append(d.Day.ToString("00", CultureInfo.InvariantCulture)).Append(" tháng ").Append(d.Month.ToString("00", CultureInfo.InvariantCulture))
                    .Append(" năm ").Append(d.Year.ToString(CultureInfo.InvariantCulture)).Append("</p>\n");
            }

            if (!string.IsNullOrEmpty(m.AuthorityCode)) b.Append("<p class=\"day\">MCCQT: ").Append(E(m.AuthorityCode)).Append("</p>\n");
            b.Append("</div>\n</div>\n</div>\n<div class=\"vip-divide\"></div>\n<div class=\"content-info\">\n<ul class=\"list-fill-out\">\n");

            var s = m.Seller;
            Item(b, "Tên người bán:", s.Name);
            Item(b, "Mã số thuế:", s.TaxCode);
            Item(b, "Mã cửa hàng:", s.StoreCode);
            Item(b, "Tên cửa hàng:", s.StoreName);
            Item(b, "Địa chỉ:", s.Address);
            Item(b, "Điện thoại:", s.Phone);
            Item(b, "Số tài khoản:", Account(s));
            b.Append("<li>\n<div class=\"vip-divide\" style=\"margin: 5px 0;\"></div>\n</li>\n");

            var u = m.Buyer;
            Item(b, "Tên người mua:", u.Name);
            Item(b, "Họ tên người mua:", u.ContactName);
            Item(b, "Mã số thuế:", u.TaxCode);
            Item(b, "Mã ĐVCQHVNSNN:", u.BudgetUnitCode);
            Item(b, "CCCD người mua:", u.IdNumber);
            Item(b, "Số hộ chiếu:", u.PassportNumber);
            Item(b, "Địa chỉ:", u.Address);
            Item(b, "Số tài khoản:", Account(u));
            Item(b, "Hình thức thanh toán:", m.PaymentMethod);
            b.Append("<li class=\"flex-li\">\n");
            Cell(b, "Số bảng kê:", m.ListNumber, " style=\"width: 50%\"");
            Cell(b, "Ngày bảng kê:", m.ListDate, " style=\"width: 50%\"");
            b.Append("</li>\n</ul>\n");

            Lines(b, m);
            Totals(b, m);
            b.Append("</div>\n<div class=\"vip-divide\"></div>\n");
            Signature(b, m);
            b.Append("</div>\n<input type=\"hidden\" id=\"qrcodeContent\" value=\"").Append(E(m.QrPayload)).Append("\">\n</body>\n</html>\n");
            return b.ToString();
        }

        private static void Item(StringBuilder b, string label, string value)
        {
            b.Append("<li>\n");
            Cell(b, label, value, string.Empty);
            b.Append("</li>\n");
        }

        private static void Cell(StringBuilder b, string label, string value, string attributes)
        {
            b.Append("<div class=\"data-item\"").Append(attributes).Append(">\n<div class=\"di-label\">\n<span>").Append(E(label)).Append("</span>\n</div>\n");
            b.Append("<div class=\"di-value\">\n<div>").Append(E(value)).Append("</div>\n</div>\n</div>\n");
        }

        private static string Account(InvoicePrintParty p)
        {
            if (string.IsNullOrEmpty(p.BankAccount)) return string.Empty;
            return string.IsNullOrEmpty(p.BankName) ? p.BankAccount : p.BankAccount + " - " + p.BankName;
        }

        private static void Lines(StringBuilder b, InvoicePrintModel m)
        {
            b.Append("<table class=\"res-tb\">\n<thead style=\"text-align: center;\">\n<tr>\n");
            b.Append("<th class=\"tb-stt\">STT</th><th class=\"tb-stt\">Tính chất</th><th class=\"tb-stt\">Loại hàng hoá đặc trưng</th><th class=\"tb-thh\">Tên hàng hóa, dịch vụ</th>");
            b.Append("<th class=\"tb-dvt\">Đơn vị tính</th><th class=\"tb-sl\">Số lượng</th><th class=\"tb-dg\">Đơn giá</th><th class=\"tb-dg\">Chiết khấu</th>");
            b.Append("<th class=\"tb-ts\">Thuế suất</th><th class=\"tb-ttct\">Thành tiền chưa có thuế GTGT</th>\n</tr>\n</thead>\n<tbody>\n");
            foreach (var line in m.Lines)
            {
                var note = line.Kind == "4";
                b.Append("<tr>\n");
                b.Append("<td class=\"tx-center\">").Append(E(line.Number)).Append("</td>");
                b.Append("<td class=\"tx-left\"><span>").Append(E(KindOf(line.Kind))).Append("</span></td>");
                b.Append("<td class=\"tx-left\" style=\"max-width: 200px;word-wrap: break-word;\"></td>");
                b.Append("<td class=\"tx-left\">").Append(E(line.Name)).Append("</td>");
                b.Append("<td class=\"tx-left\">").Append(E(note ? null : line.Unit)).Append("</td>");
                b.Append("<td class=\"tx-center\">").Append(E(note ? null : Number(line.Quantity))).Append("</td>");
                b.Append("<td class=\"tx-center\">").Append(E(note ? null : Number(line.UnitPrice))).Append("</td>");
                b.Append("<td class=\"tx-center\">").Append(E(note ? null : Number(line.DiscountAmount))).Append("</td>");
                b.Append("<td class=\"tx-center\">").Append(E(note ? null : line.TaxRate)).Append("</td>");
                b.Append("<td class=\"tx-center\">").Append(E(note ? null : Number(line.Amount))).Append("</td>\n</tr>\n");
            }

            b.Append("</tbody>\n</table>\n");
        }

        private static void Totals(StringBuilder b, InvoicePrintModel m)
        {
            b.Append("<div class=\"table-horizontal-wrapper\">\n<div style=\"margin-right: 10;\">\n<table class=\"res-tb\">\n<thead style=\"text-align: center\">\n<tr>\n");
            b.Append("<th>Thuế suất</th><th>Tổng tiền chưa thuế</th><th>Tổng tiền thuế</th>\n</tr>\n</thead>\n<tbody>\n");
            foreach (var t in m.TaxLines)
            {
                b.Append("<tr>\n<td class=\"tx-center\">").Append(E(t.Rate)).Append("</td><td class=\"tx-center\">").Append(E(Number(t.TaxableAmount)))
                    .Append("</td><td class=\"tx-center\">").Append(E(Number(t.TaxAmount))).Append("</td>\n</tr>\n");
            }

            b.Append("</tbody>\n</table>\n</div>\n<div style=\"flex: 1\">\n<table class=\"res-tb\">\n<tbody>\n");
            Total(b, "Tổng tiền chưa thuế<br>(Tổng cộng thành tiền chưa có thuế)", E(Number(m.TotalBeforeTax)));
            Total(b, "Tổng tiền thuế (Tổng cộng tiền thuế)", E(Number(m.TotalTax)));
            Total(b, "Tổng tiền phí", E(Number(m.TotalFee)));
            Total(b, "Tổng tiền chiết khấu thương mại", E(Number(m.TotalDiscount)));
            Total(b, "Tổng tiền thanh toán bằng số", E(Number(m.TotalPayment)));
            Total(b, "Tổng tiền thanh toán bằng chữ", E(m.TotalInWords));
            b.Append("</tbody>\n</table>\n</div>\n</div>\n");
        }

        private static void Total(StringBuilder b, string labelHtml, string valueHtml)
        {
            b.Append("<tr>\n<td class=\"tx-center\">").Append(labelHtml).Append("</td><td class=\"tx-center\" style=\"min-width: 200px; max-width: 300px\">").Append(valueHtml).Append("</td>\n</tr>\n");
        }

        private static void Signature(StringBuilder b, InvoicePrintModel m)
        {
            b.Append("<div class=\"ft-sign\">\n<div class=\"sign-dx\">\n<h3>\n<p>NGƯỜI MUA HÀNG</p>\n<p>\n<i>(Chữ ký số (nếu có))</i>\n</p>\n</h3>\n");
            b.Append("<h3>\n<p>NGƯỜI BÁN HÀNG</p>\n<p>\n<i>(Chữ ký điện tử, chữ ký số)</i>\n</p>\n");

            var status = m.SellerSignatureStatus;
            var signedAt = m.SellerSignedAtText ?? (m.SellerSignedAt.HasValue ? m.SellerSignedAt.Value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) : null);
            if (status != InvoiceSignatureStatus.None || !string.IsNullOrEmpty(signedAt))
            {
                // Nhãn lấy theo kết quả kiểm thật trên XML: "Signature Valid" chỉ khi chữ ký toàn vẹn; details.js đổi chuỗi chứng thư thành CN khi mở trang.
                string label;
                switch (status)
                {
                    case InvoiceSignatureStatus.Valid: label = "Signature Valid"; break;
                    case InvoiceSignatureStatus.Invalid: label = "Signature Invalid"; break;
                    default: label = "Chưa kiểm tra được chữ ký số"; break;
                }

                b.Append("<div class=\"sign-box\">\n<span>").Append(E(label)).Append("</span><span class=\"span-sign-box\">Ký bởi&nbsp;</span><span id=\"cks\" class=\"span-sign-box\">CN=")
                    .Append(E(m.SellerSignerName ?? m.Seller.Name)).Append("</span><span></span><span class=\"span-sign-box\">Ký ngày:&nbsp;</span><span class=\"span-sign-box\">")
                    .Append(E(signedAt)).Append("</span>\n</div>\n");
            }

            b.Append("</h3>\n</div>\n<div class=\"fd-end\">\n<p>\n<i>(Cần kiểm tra, đối chiếu khi lập, nhận hóa đơn)</i>\n</p>\n</div>\n</div>\n");
        }

        private static string Number(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("#,##0.####", Vi) : null;
        }

        private static string E(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        // ---- Tài nguyên nhúng ----

        private static byte[] Resource(string name)
        {
            var bytes = TryResource(name);
            if (bytes == null) throw new InvalidOperationException("Thiếu tài nguyên nhúng của trang xem hóa đơn: " + name);
            return bytes;
        }

        private static byte[] TryResource(string name)
        {
            using (var stream = typeof(InvoiceViewerPackage).Assembly.GetManifestResourceStream("HoaDonDienTu.Viewer." + name))
            {
                if (stream == null) return null;
                var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }
    }
}
