using System.Collections.Generic;
using System.Linq;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Portal
{
    /// <summary>Một lựa chọn của dropdown trên form tra cứu: mã gửi lên cổng và nhãn hiển thị đúng như cổng Thuế.</summary>
    public sealed class PortalOption
    {
        public PortalOption(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; private set; }
        public string Label { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>
    /// Các giá trị dropdown của form "Tra cứu hóa đơn" lấy từ sample-api-web.md mục 2.4–2.6. Mã rỗng là "Tất cả" (không gửi tham số).
    /// Dùng chung cho giao diện lọc, cột "Trạng thái"/"Kết quả kiểm tra" của bảng kê Excel và bộ dữ liệu giả.
    /// </summary>
    public static class PortalOptions
    {
        public const string All = "Tất cả";

        /// <summary>Trạng thái hóa đơn, tham số <c>tthai</c>.</summary>
        public static readonly IList<PortalOption> InvoiceStatuses = new List<PortalOption>
        {
            new PortalOption(string.Empty, All),
            new PortalOption("1", "Hóa đơn mới"),
            new PortalOption("2", "Hóa đơn thay thế"),
            new PortalOption("3", "Hóa đơn điều chỉnh"),
            new PortalOption("4", "Hóa đơn đã bị thay thế"),
            new PortalOption("5", "Hóa đơn đã bị điều chỉnh"),
            new PortalOption("6", "Hóa đơn đã bị hủy")
        }.ToList().AsReadOnly();

        /// <summary>Kết quả kiểm tra hóa đơn, tham số <c>ttxly</c>.</summary>
        public static readonly IList<PortalOption> CheckResults = new List<PortalOption>
        {
            new PortalOption(string.Empty, All),
            new PortalOption("0", "Cục Thuế đã nhận"),
            new PortalOption("1", "Đang tiến hành kiểm tra điều kiện cấp mã"),
            new PortalOption("2", "CQT từ chối hóa đơn theo từng lần phát sinh"),
            new PortalOption("3", "Hóa đơn đủ điều kiện cấp mã"),
            new PortalOption("4", "Hóa đơn không đủ điều kiện cấp mã"),
            new PortalOption("5", "Đã cấp mã hóa đơn"),
            new PortalOption("6", "Cục Thuế đã nhận không mã"),
            new PortalOption("7", "Đã kiểm tra định kỳ HĐĐT không có mã"),
            new PortalOption("8", "Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền")
        }.ToList().AsReadOnly();

        /// <summary>
        /// Kết quả kiểm tra của form tra cứu <b>mua vào</b> (sample-api-web.md mục 4.5): chỉ 5, 6, 8 và bắt buộc chọn một, không có "Tất cả".
        /// Mục đầu (mã rỗng) là lựa chọn của ứng dụng: tải lần lượt cả ba mã rồi gộp, vì cổng không có "Tất cả" cho chiều này.
        /// </summary>
        public static readonly IList<PortalOption> PurchaseCheckResults = new List<PortalOption>
        {
            new PortalOption(string.Empty, "Cả 3 loại (tải lần lượt)"),
            new PortalOption("5", "Đã cấp mã hóa đơn"),
            new PortalOption("6", "Cục Thuế đã nhận không mã"),
            new PortalOption("8", "Cục Thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền")
        }.ToList().AsReadOnly();

        /// <summary>Danh sách Kết quả kiểm tra của dropdown tra cứu theo chiều.</summary>
        public static IList<PortalOption> CheckResultsFor(InvoiceDirection direction)
        {
            return direction == InvoiceDirection.Purchase ? PurchaseCheckResults : CheckResults;
        }

        /// <summary>
        /// Các mã <c>ttxly</c> phải gọi lần lượt khi người dùng không chọn kết quả kiểm tra. Rỗng nghĩa là cổng nhận "Tất cả"
        /// (bán ra) nên chỉ cần một lượt không có điều kiện này.
        /// </summary>
        public static IList<string> CheckResultPasses(InvoiceDirection direction)
        {
            return direction == InvoiceDirection.Purchase
                ? PurchaseCheckResults.Where(o => o.Code.Length > 0).Select(o => o.Code).ToList().AsReadOnly()
                : new List<string>().AsReadOnly();
        }

        /// <summary>Ký hiệu mẫu số hóa đơn, tham số <c>khmshdon</c> (giá trị 1 đến 9).</summary>
        public static readonly IList<PortalOption> FormSymbols = new[] { new PortalOption(string.Empty, All) }
            .Concat(Enumerable.Range(1, 9).Select(i => new PortalOption(i.ToString(), i.ToString())))
            .ToList().AsReadOnly();

        public static string StatusLabel(string code)
        {
            return LabelOf(InvoiceStatuses, code);
        }

        public static string CheckResultLabel(string code)
        {
            return LabelOf(CheckResults, code);
        }

        private static string LabelOf(IList<PortalOption> options, string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var found = options.FirstOrDefault(o => o.Code == code);
            return found == null ? code : found.Label;
        }
    }
}
