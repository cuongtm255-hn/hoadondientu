using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>
    /// Điều kiện lọc/sắp xếp đã chuẩn hóa để repository dịch sang SQL (hoặc lọc trong bộ nhớ). Giá trị trống là null; số hóa đơn và
    /// từ khóa tìm nhanh đã ở dạng khóa so sánh (<see cref="ArchiveKeys"/>).
    /// </summary>
    public sealed class ArchiveCriteria
    {
        public InvoiceDirection Direction { get; set; }
        public DateTime? IssueFrom { get; set; }
        public DateTime? IssueTo { get; set; }
        public string StatusCode { get; set; }
        public string CheckResultCode { get; set; }
        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }

        /// <summary>Khóa so sánh của số hóa đơn (<see cref="ArchiveKeys.NumberKey"/>).</summary>
        public string NumberKey { get; set; }

        public string CounterpartyId { get; set; }

        /// <summary>Từ khóa tìm nhanh đã hạ chữ thường và bỏ dấu.</summary>
        public string QuickSearchFolded { get; set; }

        public ArchiveSortKey SortKey { get; set; }
        public bool SortDescending { get; set; }

        public static ArchiveCriteria From(ArchiveQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            return new ArchiveCriteria
            {
                Direction = query.Direction,
                IssueFrom = query.IssueFrom.HasValue ? query.IssueFrom.Value.Date : (DateTime?)null,
                IssueTo = query.IssueTo.HasValue ? query.IssueTo.Value.Date : (DateTime?)null,
                StatusCode = Clean(query.StatusCode),
                CheckResultCode = Clean(query.CheckResultCode),
                FormSymbol = Clean(query.FormSymbol),
                InvoiceSymbol = Clean(query.InvoiceSymbol),
                NumberKey = Clean(query.InvoiceNumber) == null ? null : ArchiveKeys.NumberKey(query.InvoiceNumber),
                CounterpartyId = Clean(query.CounterpartyTaxCode),
                QuickSearchFolded = Clean(query.QuickSearch) == null ? null : ArchiveKeys.Fold(query.QuickSearch),
                SortKey = query.SortKey,
                SortDescending = query.SortDescending
            };
        }

        /// <summary>Lọc trong bộ nhớ, cùng ngữ nghĩa với câu SQL của <c>SqliteInvoiceRepository</c>.</summary>
        public bool Matches(StoredInvoice stored)
        {
            var i = stored.Invoice;
            if (i.Direction != Direction) return false;
            if (IssueFrom.HasValue && i.IssueDate.Date < IssueFrom.Value) return false;
            if (IssueTo.HasValue && i.IssueDate.Date > IssueTo.Value) return false;
            if (StatusCode != null && i.StatusRaw != StatusCode) return false;
            if (CheckResultCode != null && i.CheckResultRaw != CheckResultCode) return false;
            if (FormSymbol != null && !string.Equals(i.FormSymbol, FormSymbol, StringComparison.OrdinalIgnoreCase)) return false;
            if (InvoiceSymbol != null && !string.Equals(i.InvoiceSymbol, InvoiceSymbol, StringComparison.OrdinalIgnoreCase)) return false;
            if (NumberKey != null && ArchiveKeys.NumberKey(i.InvoiceNumber) != NumberKey) return false;
            if (CounterpartyId != null && !string.Equals(i.CounterpartyIdentifier, CounterpartyId, StringComparison.OrdinalIgnoreCase)) return false;
            if (QuickSearchFolded != null && ArchiveKeys.SearchText(i).IndexOf(QuickSearchFolded, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        /// <summary>Thứ tự hiển thị: khóa chính theo <see cref="SortKey"/>, rồi số hóa đơn, rồi mã (ổn định giữa các trang).</summary>
        public int Compare(StoredInvoice a, StoredInvoice b)
        {
            var result = ComparePrimary(a.Invoice, b.Invoice);
            var number = string.CompareOrdinal(ArchiveKeys.NumberKey(a.Invoice.InvoiceNumber), ArchiveKeys.NumberKey(b.Invoice.InvoiceNumber));
            if (SortDescending)
            {
                result = -result;
                number = -number;
            }

            if (result != 0) return result;
            if (number != 0) return number;
            return string.CompareOrdinal(a.InvoiceId.ToString("D"), b.InvoiceId.ToString("D"));
        }

        private int ComparePrimary(NormalizedInvoice a, NormalizedInvoice b)
        {
            switch (SortKey)
            {
                case ArchiveSortKey.InvoiceNumber:
                    return string.CompareOrdinal(ArchiveKeys.NumberKey(a.InvoiceNumber), ArchiveKeys.NumberKey(b.InvoiceNumber));
                case ArchiveSortKey.Counterparty:
                    return string.CompareOrdinal(ArchiveKeys.CounterpartySort(a), ArchiveKeys.CounterpartySort(b));
                case ArchiveSortKey.TotalPayment:
                    return (a.TotalPayment ?? 0m).CompareTo(b.TotalPayment ?? 0m);
                default:
                    return a.IssueDate.CompareTo(b.IssueDate);
            }
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>Khóa dẫn xuất dùng chung cho cột lưu trong <c>company.db</c> và lọc trong bộ nhớ, để hai nơi không lệch nhau.</summary>
    public static class ArchiveKeys
    {
        private const int NumberKeyWidth = 20;

        /// <summary>Số hóa đơn chuẩn hóa (bỏ số 0 đầu của số thuần chữ số) đệm số 0 bên trái: so sánh chữ cũng đúng thứ tự số (9 trước 10).</summary>
        public static string NumberKey(string number)
        {
            var n = BusinessKey.NormalizeNumber(number);
            return n.Length < NumberKeyWidth ? n.PadLeft(NumberKeyWidth, '0') : n;
        }

        /// <summary>Tên đối tác đã bỏ dấu, dùng để sắp xếp theo đối tác.</summary>
        public static string CounterpartySort(NormalizedInvoice invoice)
        {
            return Fold(invoice.Direction == InvoiceDirection.Purchase ? invoice.SellerName : invoice.BuyerName);
        }

        /// <summary>Chuỗi tìm nhanh: số hóa đơn, ký hiệu, MST/CCCD và tên đối tác, đã bỏ dấu.</summary>
        public static string SearchText(NormalizedInvoice i)
        {
            return Fold(string.Join(" ", i.InvoiceNumber, i.InvoiceSymbol, i.CounterpartyIdentifier, i.Direction == InvoiceDirection.Purchase ? i.SellerName : i.BuyerName));
        }

        /// <summary>Chữ thường, bỏ dấu tiếng Việt (kể cả đ) để "cong ty" tìm được "CÔNG TY".</summary>
        public static string Fold(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    /// <summary>Một hóa đơn rút gọn để kiểm tra file và cộng tổng mà không nạp cả bản ghi.</summary>
    public sealed class InvoiceFileRef
    {
        public Guid InvoiceId { get; set; }

        /// <summary>Đường dẫn tương đối của XML hiện hành; null khi hóa đơn chưa có phiên bản hiện hành.</summary>
        public string RelativePath { get; set; }

        public int VersionCount { get; set; }

        /// <summary>Vấn đề do lần kiểm tra file gần nhất ghi lại; null khi chưa kiểm hoặc file nguyên vẹn.</summary>
        public IntegrityProblemKind? StoredProblem { get; set; }

        public decimal TotalBeforeTax { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalPayment { get; set; }
    }
}
