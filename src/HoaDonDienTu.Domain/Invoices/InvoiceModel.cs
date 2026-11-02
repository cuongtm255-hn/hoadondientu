using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HoaDonDienTu.Domain.Invoices
{
    /// <summary>Trạng thái hóa đơn chuẩn hóa từ <c>tthai</c> của cổng Thuế (1..6).</summary>
    public enum InvoiceStatus
    {
        Unknown = 0,
        New = 1,
        Replacement = 2,
        Adjustment = 3,
        Replaced = 4,
        Adjusted = 5,
        Cancelled = 6
    }

    /// <summary>Kết quả kiểm tra hóa đơn chuẩn hóa từ <c>ttxly</c> của cổng Thuế (0..8).</summary>
    public enum InvoiceCheckResult
    {
        Unknown = -1,
        TaxAuthorityReceived = 0,
        CheckingForCode = 1,
        RejectedPerOccurrence = 2,
        EligibleForCode = 3,
        NotEligibleForCode = 4,
        CodeIssued = 5,
        ReceivedWithoutCode = 6,
        PeriodicallyChecked = 7,
        ReceivedFromCashRegister = 8
    }

    public static class InvoiceCodes
    {
        public static InvoiceStatus StatusFrom(string raw)
        {
            int value;
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 1 || value > 6) return InvoiceStatus.Unknown;
            return (InvoiceStatus)value;
        }

        public static InvoiceCheckResult CheckResultFrom(string raw)
        {
            int value;
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value > 8) return InvoiceCheckResult.Unknown;
            return (InvoiceCheckResult)value;
        }
    }

    /// <summary>
    /// Hóa đơn đã chuẩn hóa: dùng chung cho mua vào và bán ra (FR-STORE-001). Số hóa đơn và ký hiệu giữ dạng text.
    /// Giá trị nguồn của trạng thái/kết quả kiểm tra được giữ kèm giá trị chuẩn hóa.
    /// </summary>
    public sealed class NormalizedInvoice
    {
        public InvoiceDirection Direction { get; set; }
        public string PortalInvoiceId { get; set; }
        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public string SellerTaxCode { get; set; }
        public string SellerName { get; set; }

        /// <summary>MST người mua; null khi người mua là cá nhân chỉ có CCCD.</summary>
        public string BuyerTaxCode { get; set; }

        public string BuyerIdNumber { get; set; }
        public string BuyerName { get; set; }
        public string BuyerAddress { get; set; }
        public decimal? TotalBeforeTax { get; set; }
        public decimal? TotalTax { get; set; }
        public decimal? TotalDiscount { get; set; }
        public decimal? TotalFee { get; set; }
        public decimal? TotalPayment { get; set; }
        public string Currency { get; set; }
        public decimal? ExchangeRate { get; set; }
        public InvoiceStatus Status { get; set; }
        public string StatusRaw { get; set; }
        public InvoiceCheckResult CheckResult { get; set; }
        public string CheckResultRaw { get; set; }

        /// <summary>Giá trị cột "MST người mua" của bảng kê: MST, nếu là cá nhân thì CCCD.</summary>
        public string BuyerIdentifier
        {
            get { return string.IsNullOrEmpty(BuyerTaxCode) ? BuyerIdNumber : BuyerTaxCode; }
        }

        /// <summary>Đối tác từ góc nhìn của doanh nghiệp: mua vào là người bán, bán ra là người mua.</summary>
        public string CounterpartyIdentifier
        {
            get { return Direction == InvoiceDirection.Purchase ? SellerTaxCode : BuyerIdentifier; }
        }
    }

    /// <summary>
    /// Khóa chống trùng. Cổng định danh hóa đơn bằng (MST người bán, ký hiệu mẫu số, ký hiệu, số), còn <c>id</c> chỉ là mã nội bộ
    /// (docs/api-samples/README.md mục 3), nên khóa dựa vào bộ bốn trường đó cộng chiều. Số hóa đơn thuần chữ số được bỏ số 0 đầu để
    /// "0000040" và "40" là cùng một hóa đơn; ký hiệu so sánh không phân biệt hoa thường.
    /// </summary>
    public static class BusinessKey
    {
        public static string Build(NormalizedInvoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException("invoice");
            return Build(invoice.Direction, invoice.SellerTaxCode, invoice.FormSymbol, invoice.InvoiceSymbol, invoice.InvoiceNumber);
        }

        public static string Build(InvoiceDirection direction, string sellerTaxCode, string formSymbol, string invoiceSymbol, string invoiceNumber)
        {
            return string.Join(
                "|",
                direction == InvoiceDirection.Purchase ? "P" : "S",
                (sellerTaxCode ?? string.Empty).Trim(),
                (formSymbol ?? string.Empty).Trim(),
                (invoiceSymbol ?? string.Empty).Trim().ToUpperInvariant(),
                NormalizeNumber(invoiceNumber));
        }

        /// <summary>Số hóa đơn thuần chữ số bỏ số 0 đầu; dùng chung cho khóa chống trùng và tra cứu kho.</summary>
        public static string NormalizeNumber(string number)
        {
            var text = (number ?? string.Empty).Trim();
            if (text.Length > 0 && text.All(char.IsDigit))
            {
                var trimmed = text.TrimStart('0');
                return trimmed.Length == 0 ? "0" : trimmed;
            }

            return text;
        }
    }

    /// <summary>
    /// Đặt tên và đường dẫn file XML trong kho (DD-06 mục 3): <c>yyyy\MM\{MuaVao|BanRa}\XML\yyyyMMdd_đối-tác_mẫu-số_ký-hiệu_số_vN.xml</c>.
    /// Lớp thuần, không đụng tới đĩa. Ký tự không hợp lệ với Windows được thay bằng <c>_</c>.
    /// </summary>
    public static class InvoiceFileNamer
    {
        /// <summary>Giới hạn đường dẫn đầy đủ của Windows (MAX_PATH trừ ký tự kết thúc).</summary>
        public const int MaxPathLength = 259;

        private static readonly Regex Invalid = new Regex("[" + Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()) + "/\\:") + "\\s]+", RegexOptions.Compiled);

        public static string RelativeFolder(NormalizedInvoice invoice)
        {
            return System.IO.Path.Combine(
                invoice.IssueDate.Year.ToString("0000", CultureInfo.InvariantCulture),
                invoice.IssueDate.Month.ToString("00", CultureInfo.InvariantCulture),
                invoice.Direction == InvoiceDirection.Purchase ? "MuaVao" : "BanRa",
                "XML");
        }

        public static string FileName(NormalizedInvoice invoice, int versionNo)
        {
            var parts = new[]
            {
                invoice.IssueDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                Clean(invoice.CounterpartyIdentifier, "NA"),
                Clean(invoice.FormSymbol, "0"),
                Clean(invoice.InvoiceSymbol, "NA"),
                Clean(invoice.InvoiceNumber, "0"),
                "v" + versionNo.ToString(CultureInfo.InvariantCulture)
            };
            return string.Join("_", parts) + ".xml";
        }

        public static string RelativePath(NormalizedInvoice invoice, int versionNo)
        {
            return System.IO.Path.Combine(RelativeFolder(invoice), FileName(invoice, versionNo));
        }

        /// <summary>Thêm hậu tố chống va chạm trước phần mở rộng: <c>a.xml</c> → <c>a_2.xml</c>.</summary>
        public static string WithCollisionSuffix(string fileName, int attempt)
        {
            var ext = System.IO.Path.GetExtension(fileName);
            return System.IO.Path.GetFileNameWithoutExtension(fileName) + "_" + attempt.ToString(CultureInfo.InvariantCulture) + ext;
        }

        public static bool FitsPathLimit(string root, string relativePath)
        {
            return (root ?? string.Empty).TrimEnd('\\').Length + 1 + relativePath.Length <= MaxPathLength;
        }

        private static string Clean(string value, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            text = Invalid.Replace(text, "_");
            return text.Length > 40 ? text.Substring(0, 40) : text;
        }
    }
}
