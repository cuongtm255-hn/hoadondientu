using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Xuất kết quả tra cứu hồ sơ đã nộp ra Excel (FT-DVC-08, FR-DVC-EXP-002): các cột đúng thứ tự trên màn hình, kèm dòng ghi nguồn, khoảng ngày,
    /// bộ lọc, số bản ghi và thời điểm tra cứu. Dữ liệu hồ sơ là riêng tư của doanh nghiệp: tệp chỉ được ghi khi người dùng chủ động xuất (BR-DVC-07),
    /// và không thông tin nào của tệp đi vào nhật ký.
    /// </summary>
    public static class HoSoExcelExport
    {
        public const string Title = "HỒ SƠ ĐÃ NỘP — CỔNG DỊCH VỤ CÔNG THUẾ";

        /// <param name="columns">Tên cột theo thứ tự màn hình (không gồm STT, do bộ ghi tự thêm).</param>
        /// <param name="rows">Các dòng (mỗi ô là văn bản).</param>
        /// <param name="filters">Bộ lọc đã áp dụng lúc tìm (không có CAPTCHA); null nếu không rõ.</param>
        /// <param name="scopeText">Phạm vi xuất, ví dụ "Trang 2/3" hoặc "Tất cả các trang (3 trang)".</param>
        public static void Write(Stream output, IList<string> columns, IList<HoSoRow> rows, HoSoQuery filters, string scopeText, DateTime queriedAt, Action<int> onRow)
        {
            if (columns == null) throw new ArgumentNullException("columns");
            if (rows == null) throw new ArgumentNullException("rows");

            var info = new List<string>
            {
                "Nguồn: " + SourceLabel(filters == null ? HoSoSource.Dvc : filters.Source),
                "Phạm vi: " + (scopeText ?? string.Empty),
                "Khoảng ngày: " + (filters == null ? "không rõ" : filters.IgnoreDates ? "không giới hạn" : "từ " + Date(filters.FromDate) + " đến " + Date(filters.ToDate)),
                "Bộ lọc khác: " + DescribeFilters(filters),
                "Số bản ghi trong tệp: " + rows.Count,
                "Thời điểm tra cứu: " + queriedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            };

            var tableColumns = columns.Select(c => new TableExcelWriter.Column(c, WidthFor(c))).ToList();
            var data = rows.Select(r => r.Cells);
            TableExcelWriter.Write(output, Title, info, tableColumns, data, onRow);
        }

        public static string SourceLabel(HoSoSource source)
        {
            if (source == HoSoSource.LuuTam) return "Hồ sơ lưu tạm trên Dịch vụ công";
            return source == HoSoSource.ThueDienTu ? "Hồ sơ đã nộp trên thuế điện tử" : "Hồ sơ đã nộp trên Dịch vụ công";
        }

        /// <summary>Các điều kiện lọc ngoài khoảng ngày mà người dùng đã nhập (không bao giờ có CAPTCHA).</summary>
        public static string DescribeFilters(HoSoQuery q)
        {
            if (q == null) return "Không rõ";
            var parts = new List<string>();
            Add(parts, "Tên TTHC", q.ProcedureName);
            Add(parts, "Loại TTHC", q.ProcedureType);
            Add(parts, "Tờ khai", q.Declaration);
            Add(parts, "Mã hồ sơ", q.FileCode);
            Add(parts, "Mã giao dịch", q.TransactionCode);
            Add(parts, "MST ủy quyền", q.AuthorizedTaxCode);
            return parts.Count == 0 ? "Không" : string.Join("; ", parts);
        }

        public static string SuggestedFileName(DateTime now)
        {
            return "HoSoDaNop-" + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".xlsx";
        }

        private static void Add(List<string> parts, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) parts.Add(name + " = \"" + value.Trim() + "\"");
        }

        private static string Date(DateTime d)
        {
            return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>Độ rộng cột theo tên cột (cột nội dung dài rộng hơn); cột chưa biết dùng độ rộng mặc định.</summary>
        public static double WidthFor(string column)
        {
            string c = (column ?? string.Empty).ToLowerInvariant();
            if (c.Contains("mã hồ sơ") || c.Contains("mã giao dịch")) return 36;
            if (c.Contains("tên tthc") || c.Contains("tờ khai") && !c.Contains("loại") && !c.Contains("lần nộp")) return 44;
            if (c.Contains("cơ quan")) return 44;
            if (c.Contains("ngày")) return 18;
            if (c.Contains("trạng thái")) return 18;
            if (c.Contains("kỳ")) return 12;
            return 20;
        }
    }
}
