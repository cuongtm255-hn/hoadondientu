using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Xuất danh mục TTHC ra Excel (FT-DVC-08, FR-DVC-EXP-002): các cột đúng thứ tự trên màn hình, kèm dòng ghi bộ lọc, số bản ghi và thời điểm tra cứu.
    /// Danh mục TTHC là thông tin công khai; tệp chỉ được ghi khi người dùng chủ động xuất (BR-DVC-07).
    /// </summary>
    public static class TthcExcelExport
    {
        public const string Title = "DANH MỤC THỦ TỤC HÀNH CHÍNH — CỔNG DỊCH VỤ CÔNG THUẾ";

        public static readonly IList<TableExcelWriter.Column> Columns = new List<TableExcelWriter.Column>
        {
            new TableExcelWriter.Column("Mã", 14),
            new TableExcelWriter.Column("Tên thủ tục", 70),
            new TableExcelWriter.Column("Mức độ", 18),
            new TableExcelWriter.Column("Cơ quan thực hiện", 48)
        }.AsReadOnly();

        /// <param name="scopeText">Phạm vi xuất, ví dụ "Trang 2/15" hoặc "Tất cả các trang (147 bản ghi)".</param>
        /// <param name="filters">Bộ lọc đã áp dụng lúc tìm (không có số trang/CAPTCHA); null nếu chưa lọc.</param>
        public static void Write(Stream output, IList<TthcSummary> items, TthcQuery filters, TthcFilterOptions options, string scopeText, DateTime queriedAt, Action<int> onRow)
        {
            if (items == null) throw new ArgumentNullException("items");
            var info = new List<string>
            {
                "Phạm vi: " + (scopeText ?? string.Empty),
                "Bộ lọc: " + DescribeFilters(filters, options),
                "Số bản ghi trong tệp: " + items.Count,
                "Thời điểm tra cứu: " + queriedAt.ToString("dd/MM/yyyy HH:mm")
            };
            var rows = items.Select(i => (IList<string>)new[] { i.Code, i.Name, i.Level, i.Agency });
            TableExcelWriter.Write(output, Title, info, Columns, rows, onRow);
        }

        /// <summary>Mô tả bộ lọc bằng nhãn người đọc được (không phải mã nội bộ của cổng).</summary>
        public static string DescribeFilters(TthcQuery filters, TthcFilterOptions options)
        {
            if (filters == null) return "Không lọc";
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(filters.Keyword)) parts.Add("Tên/Mã TTHC = \"" + filters.Keyword.Trim() + "\"");
            AddOption(parts, "Loại TTHC", filters.ProcedureType, options == null ? null : options.ProcedureTypes);
            AddOption(parts, "Tờ khai", filters.Declaration, options == null ? null : options.Declarations);
            AddOption(parts, "Mức độ", filters.Level, options == null ? null : options.Levels);
            AddOption(parts, "Loại người nộp thuế", filters.TaxpayerType, options == null ? null : options.TaxpayerTypes);
            return parts.Count == 0 ? "Không lọc" : string.Join("; ", parts);
        }

        private static void AddOption(List<string> parts, string name, string code, IList<TthcOption> options)
        {
            if (string.IsNullOrEmpty(code)) return;
            TthcOption match = options == null ? null : options.FirstOrDefault(o => o.Code == code);
            parts.Add(name + " = " + (match != null ? match.Label : code));
        }

        /// <summary>Tên tệp gợi ý, ví dụ <c>DanhMucTTHC-20261121-1530.xlsx</c>.</summary>
        public static string SuggestedFileName(DateTime now)
        {
            return "DanhMucTTHC-" + now.ToString("yyyyMMdd-HHmm") + ".xlsx";
        }
    }
}
