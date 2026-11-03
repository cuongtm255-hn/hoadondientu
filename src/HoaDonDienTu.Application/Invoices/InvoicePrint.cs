using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>Một dòng hàng hóa/dịch vụ của bản thể hiện hóa đơn.</summary>
    public sealed class InvoicePrintLine
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? DiscountAmount { get; set; }

        /// <summary>Thuế suất nguyên văn của cổng ("8%", "KCT"...).</summary>
        public string TaxRate { get; set; }

        public decimal? Amount { get; set; }

        /// <summary>Tính chất dòng (TT78): 1 hàng hóa/dịch vụ, 2 khuyến mại, 3 chiết khấu, 4 ghi chú/diễn giải.</summary>
        public string Kind { get; set; }
    }

    public sealed class InvoicePrintTaxLine
    {
        public string Rate { get; set; }
        public decimal? TaxableAmount { get; set; }
        public decimal? TaxAmount { get; set; }
    }

    public sealed class InvoicePrintParty
    {
        public string Name { get; set; }
        public string TaxCode { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string BankAccount { get; set; }
        public string BankName { get; set; }

        /// <summary>Họ tên người mua/người đại diện (chỉ có ở người mua).</summary>
        public string ContactName { get; set; }

        /// <summary>CCCD của người mua không có MST.</summary>
        public string IdNumber { get; set; }

        /// <summary>Mã cửa hàng (người bán).</summary>
        public string StoreCode { get; set; }

        /// <summary>Tên cửa hàng (người bán).</summary>
        public string StoreName { get; set; }

        /// <summary>Số hộ chiếu (người mua).</summary>
        public string PassportNumber { get; set; }

        /// <summary>Mã đơn vị có quan hệ với ngân sách nhà nước (người mua).</summary>
        public string BudgetUnitCode { get; set; }
    }

    /// <summary>
    /// Dữ liệu đủ để dựng bản thể hiện (PDF/in) của một hóa đơn, đọc từ XML đã lưu theo lược đồ hóa đơn điện tử TT78 (<c>HDon/DLHDon</c>).
    /// Chỉ chứa thứ XML có; không bổ sung hay tính lại số liệu. XML vẫn là chứng từ gốc (BR-008).
    /// </summary>
    public sealed class InvoicePrintModel
    {
        public InvoicePrintModel()
        {
            Seller = new InvoicePrintParty();
            Buyer = new InvoicePrintParty();
            Lines = new List<InvoicePrintLine>();
            TaxLines = new List<InvoicePrintTaxLine>();
        }

        public string SchemaVersion { get; set; }

        /// <summary>Tên loại hóa đơn nguyên văn ("Hóa đơn GTGT", "Hóa đơn bán hàng"...).</summary>
        public string Title { get; set; }

        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime? IssueDate { get; set; }
        public string Currency { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string PaymentMethod { get; set; }

        /// <summary>Số và ngày bảng kê kèm hóa đơn (nguyên văn).</summary>
        public string ListNumber { get; set; }

        public string ListDate { get; set; }

        /// <summary>Nội dung mã QR của hóa đơn (<c>DLQRCode</c>); null khi XML không có.</summary>
        public string QrPayload { get; set; }

        public InvoicePrintParty Seller { get; private set; }
        public InvoicePrintParty Buyer { get; private set; }
        public IList<InvoicePrintLine> Lines { get; private set; }
        public IList<InvoicePrintTaxLine> TaxLines { get; private set; }

        public decimal? TotalBeforeTax { get; set; }
        public decimal? TotalTax { get; set; }
        public decimal? TotalDiscount { get; set; }

        /// <summary>Tổng tiền phí (từ <c>TgTPhi</c>, hoặc cộng các khoản phí của <c>DSLPhi</c>).</summary>
        public decimal? TotalFee { get; set; }

        public decimal? TotalPayment { get; set; }
        public string TotalInWords { get; set; }

        /// <summary>Mã của cơ quan thuế (hóa đơn có mã); null với hóa đơn không mã.</summary>
        public string AuthorityCode { get; set; }

        public DateTime? SellerSignedAt { get; set; }
        public DateTime? AuthoritySignedAt { get; set; }

        /// <summary>Thời điểm người bán ký đúng như trong XML (ví dụ <c>2026-04-08T20:56:58</c>).</summary>
        public string SellerSignedAtText { get; set; }

        /// <summary>Kết quả kiểm chữ ký số của người bán trên file XML (<see cref="InvoiceSignatureVerifier"/>).</summary>
        public InvoiceSignatureStatus SellerSignatureStatus { get; set; }

        /// <summary>Tên người ký lấy từ chứng thư số của người bán (CN của <c>X509SubjectName</c>); null khi không có.</summary>
        public string SellerSignerName { get; set; }
    }

    /// <summary>XML không đọc được hoặc không phải hóa đơn điện tử theo lược đồ đã biết.</summary>
    public sealed class InvoicePrintException : Exception
    {
        public InvoicePrintException(string message)
            : base(message)
        {
        }

        public InvoicePrintException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Đọc XML hóa đơn thành <see cref="InvoicePrintModel"/>. Đọc bằng <see cref="XmlReader"/> không xử lý DTD và không resolve tài nguyên ngoài
    /// (NFR-SEC-003), so khớp theo tên cục bộ nên không phụ thuộc không gian tên; trường thiếu để trống thay vì đoán.
    /// </summary>
    public static class InvoicePrintModelParser
    {
        public static InvoicePrintModel Parse(byte[] xml)
        {
            if (xml == null || xml.Length == 0) throw new InvoicePrintException("File XML rỗng.");

            XDocument document;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(new MemoryStream(xml), settings))
                {
                    document = XDocument.Load(reader);
                }
            }
            catch (XmlException ex)
            {
                throw new InvoicePrintException("XML không đọc được (file hỏng hoặc không đúng định dạng).", ex);
            }

            var root = document.Root;
            var data = root == null ? null : Child(root, "DLHDon");
            if (data == null) throw new InvoicePrintException("XML không phải hóa đơn điện tử (không có phần DLHDon).");

            var general = Child(data, "TTChung");
            var content = Child(data, "NDHDon");
            var model = new InvoicePrintModel
            {
                SchemaVersion = Text(general, "PBan"),
                Title = Text(general, "THDon"),
                FormSymbol = Text(general, "KHMSHDon"),
                InvoiceSymbol = Text(general, "KHHDon"),
                InvoiceNumber = Text(general, "SHDon"),
                IssueDate = Date(Text(general, "NLap")),
                Currency = Text(general, "DVTTe"),
                ExchangeRate = Number(Text(general, "TGia")),
                PaymentMethod = Text(general, "HTTToan"),
                ListNumber = Text(general, "SBKe"),
                ListDate = Text(general, "NBKe"),
                AuthorityCode = Text(root, "MCCQT"),
                QrPayload = Text(root, "DLQRCode") ?? Text(data, "DLQRCode")
            };

            model.SellerSignatureStatus = InvoiceSignatureVerifier.VerifySeller(xml);
            ReadParty(Child(content, "NBan"), model.Seller);
            ReadParty(Child(content, "NMua"), model.Buyer);

            foreach (var item in Children(Child(content, "DSHHDVu"), "HHDVu"))
            {
                model.Lines.Add(
                    new InvoicePrintLine
                    {
                        Number = Text(item, "STT"),
                        Name = Text(item, "THHDVu"),
                        Unit = Text(item, "DVTinh"),
                        Quantity = Number(Text(item, "SLuong")),
                        UnitPrice = Number(Text(item, "DGia")),
                        DiscountAmount = Number(Text(item, "STCKhau")),
                        TaxRate = Text(item, "TSuat"),
                        Amount = Number(Text(item, "ThTien")),
                        Kind = Text(item, "TChat")
                    });
            }

            var total = Child(content, "TToan");
            foreach (var tax in Children(Child(total, "THTTLTSuat"), "LTSuat"))
            {
                model.TaxLines.Add(new InvoicePrintTaxLine { Rate = Text(tax, "TSuat"), TaxableAmount = Number(Text(tax, "ThTien")), TaxAmount = Number(Text(tax, "TThue")) });
            }

            model.TotalBeforeTax = Number(Text(total, "TgTCThue"));
            model.TotalTax = Number(Text(total, "TgTThue"));
            model.TotalDiscount = Number(Text(total, "TTCKTMai"));
            model.TotalFee = Number(Text(total, "TgTPhi")) ?? SumFees(total);
            model.TotalPayment = Number(Text(total, "TgTTTBSo"));
            model.TotalInWords = Text(total, "TgTTTBChu");

            var signatures = Child(root, "DSCKS");
            var sellerSignature = Child(signatures, "NBan");
            model.SellerSignedAt = SigningTime(sellerSignature);
            model.SellerSignedAtText = SigningTimeText(sellerSignature);
            model.SellerSignerName = CommonName(sellerSignature);
            model.AuthoritySignedAt = SigningTime(Child(signatures, "CQT"));
            return model;
        }

        private static void ReadParty(XElement element, InvoicePrintParty party)
        {
            if (element == null) return;
            party.Name = Text(element, "Ten");
            party.TaxCode = Text(element, "MST");
            party.Address = Text(element, "DChi");
            party.Phone = Text(element, "SDThoai");
            party.Email = Text(element, "DCTDTu");
            party.BankAccount = Text(element, "STKNHang");
            party.BankName = Text(element, "TNHang");
            party.ContactName = Text(element, "HVTNMHang");
            party.IdNumber = Text(element, "CCCDan");
            party.StoreCode = Text(element, "MCHang");
            party.StoreName = Text(element, "TCHang");
            party.PassportNumber = Text(element, "SHChieu");
            party.BudgetUnitCode = Text(element, "MDVQHNSach");
        }

        private static decimal? SumFees(XElement total)
        {
            var fees = Children(Child(total, "DSLPhi"), "LPhi").Select(f => Number(Text(f, "TPhi"))).Where(v => v.HasValue).ToList();

            // Cổng in "Tổng tiền phí" là 0 khi hóa đơn không có khoản phí nào (khác "Tổng tiền chiết khấu thương mại" để trống).
            return total == null ? (decimal?)null : fees.Sum(v => v.Value);
        }

        private static string SigningTimeText(XElement signer)
        {
            var property = signer == null ? null : signer.Descendants().FirstOrDefault(e => e.Name.LocalName == "SigningTime");
            var text = property == null ? null : property.Value.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>CN trong <c>X509SubjectName</c> của chữ ký (ví dụ <c>CN=CÔNG TY A, O=...</c>); null khi không có hoặc không có CN.</summary>
        private static string CommonName(XElement signer)
        {
            var subject = signer == null ? null : signer.Descendants().FirstOrDefault(e => e.Name.LocalName == "X509SubjectName");
            if (subject == null) return null;
            foreach (var part in subject.Value.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 3) return trimmed.Substring(3).Trim();
            }

            return null;
        }

        private static DateTime? SigningTime(XElement signer)
        {
            var property = signer == null ? null : signer.Descendants().FirstOrDefault(e => e.Name.LocalName == "SigningTime");
            if (property == null) return null;
            DateTime value;
            return DateTime.TryParse(property.Value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ? (DateTime?)value : null;
        }

        private static XElement Child(XElement parent, string localName)
        {
            if (parent == null) return null;
            return parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
        }

        private static IEnumerable<XElement> Children(XElement parent, string localName)
        {
            return parent == null ? Enumerable.Empty<XElement>() : parent.Elements().Where(e => e.Name.LocalName == localName);
        }

        private static string Text(XElement parent, string localName)
        {
            var element = Child(parent, localName);
            if (element == null) return null;
            var text = element.Value == null ? null : element.Value.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static decimal? Number(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            decimal value;
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? (decimal?)value : null;
        }

        private static DateTime? Date(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            DateTime value;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ? (DateTime?)value.Date : null;
        }
    }
}
