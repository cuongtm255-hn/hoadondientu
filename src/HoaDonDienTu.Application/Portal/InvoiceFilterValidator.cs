using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HoaDonDienTu.Application.Portal
{
    /// <summary>
    /// Kiểm tra giá trị bộ lọc trước khi gửi lên cổng (FR-SEARCH-007): sai định dạng thì cổng trả lỗi 400 hoặc cho kết quả rỗng khó hiểu,
    /// nên báo ngay trên form. Giá trị rỗng nghĩa là không lọc và luôn hợp lệ.
    /// </summary>
    public static class InvoiceFilterValidator
    {
        private static readonly Regex TaxCode = new Regex(@"^(\d{10}|\d{13}|\d{10}-\d{3})$", RegexOptions.Compiled);
        private static readonly Regex IdNumber = new Regex(@"^(\d{9}|\d{12})$", RegexOptions.Compiled);
        private static readonly Regex Number = new Regex(@"^\d{1,8}$", RegexOptions.Compiled);
        private static readonly Regex Symbol = new Regex(@"^[A-Za-z0-9]{1,8}$", RegexOptions.Compiled);

        public static IList<string> Validate(InvoiceSearchFilter filter)
        {
            var errors = new List<string>();
            if (filter == null) return errors;

            if (Has(filter.CounterpartyTaxCode) && !TaxCode.IsMatch(filter.CounterpartyTaxCode.Trim()))
            {
                errors.Add("MST gồm 10 số, 13 số hoặc dạng 10 số-3 số (ví dụ 0100100006-001).");
            }

            if (Has(filter.BuyerIdNumber) && !IdNumber.IsMatch(filter.BuyerIdNumber.Trim()))
            {
                errors.Add("CCCD gồm 12 số (hoặc 9 số với CMND cũ).");
            }

            if (Has(filter.InvoiceNumber) && !Number.IsMatch(filter.InvoiceNumber.Trim()))
            {
                errors.Add("Số hóa đơn chỉ gồm chữ số, tối đa 8 chữ số.");
            }

            if (Has(filter.InvoiceSymbol) && !Symbol.IsMatch(filter.InvoiceSymbol.Trim()))
            {
                errors.Add("Ký hiệu hóa đơn chỉ gồm chữ và số, ví dụ C26TBN.");
            }

            if (Has(filter.StatusCode) && !PortalOptions.InvoiceStatuses.Any(o => o.Code == filter.StatusCode.Trim()))
            {
                errors.Add("Trạng thái hóa đơn không hợp lệ.");
            }

            if (Has(filter.CheckResultCode) && !PortalOptions.CheckResults.Any(o => o.Code == filter.CheckResultCode.Trim()))
            {
                errors.Add("Kết quả kiểm tra không hợp lệ.");
            }

            if (Has(filter.FormSymbol) && !PortalOptions.FormSymbols.Any(o => o.Code == filter.FormSymbol.Trim()))
            {
                errors.Add("Ký hiệu mẫu số hóa đơn là số từ 1 đến 9.");
            }

            return errors;
        }

        private static bool Has(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
