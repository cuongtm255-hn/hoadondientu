using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using HoaDonDienTu.Application.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>Hiển thị báo cáo kiểm tra toàn vẹn kho cho người dùng (SCR-05).</summary>
    public interface IIntegrityReportDialog
    {
        void Show(IntegrityReport report);
    }

    /// <summary>Phần phụ thuộc cho kiểm tra nội dung file ở SCR-05; không truyền thì nút Kiểm tra file chỉ xem file còn trên đĩa hay không.</summary>
    public sealed class ArchiveIntegrityContext
    {
        public ArchiveIntegrityContext(IInvoiceIntegrityChecker checker, IIntegrityReportDialog dialog)
        {
            if (checker == null) throw new ArgumentNullException("checker");
            if (dialog == null) throw new ArgumentNullException("dialog");
            Checker = checker;
            Dialog = dialog;
        }

        public IInvoiceIntegrityChecker Checker { get; private set; }
        public IIntegrityReportDialog Dialog { get; private set; }
    }

    /// <summary>Chữ tiếng Việt của báo cáo toàn vẹn; tách khỏi giao diện để kiểm thử.</summary>
    public static class IntegrityReportText
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>Số dòng tối đa hiển thị trong hộp thoại; phần còn lại chỉ được đếm.</summary>
        public const int MaxRows = 5000;

        public static string Describe(IntegrityProblemKind kind)
        {
            switch (kind)
            {
                case IntegrityProblemKind.Missing: return "Thiếu file";
                case IntegrityProblemKind.Unreadable: return "Không đọc được file";
                case IntegrityProblemKind.HashMismatch: return "Nội dung đã bị đổi (sai SHA-256)";
                case IntegrityProblemKind.NotXml: return "Không phải XML đọc được";
                default: return kind.ToString();
            }
        }

        /// <summary>Một dòng kết luận cho thanh thông báo.</summary>
        public static string Summary(IntegrityReport report)
        {
            var checkedText = report.VersionsChecked.ToString("N0", Vi);
            if (report.IsClean) return "Đã kiểm tra " + checkedText + " file XML: toàn vẹn, không có file lạ hay file tạm sót lại.";

            var parts = new List<string>();
            Add(parts, report.Problems.Count(p => p.Kind == IntegrityProblemKind.Missing), "thiếu file");
            Add(parts, report.Problems.Count(p => p.Kind == IntegrityProblemKind.Unreadable), "không đọc được");
            Add(parts, report.Problems.Count(p => p.Kind == IntegrityProblemKind.HashMismatch), "bị đổi nội dung");
            Add(parts, report.Problems.Count(p => p.Kind == IntegrityProblemKind.NotXml), "không phải XML");
            Add(parts, report.Orphans.Count, "file lạ trong thư mục kho");
            Add(parts, report.StrayTemps.Count, "file tạm sót lại");
            return "Đã kiểm tra " + checkedText + " file XML: " + string.Join(", ", parts) + ".";
        }

        private static void Add(List<string> parts, int count, string what)
        {
            if (count > 0) parts.Add(count.ToString("N0", Vi) + " " + what);
        }

        /// <summary>Các dòng của hộp thoại: (loại, hóa đơn, phiên bản, file), vấn đề của hóa đơn trước rồi file lạ, file tạm; cắt ở <see cref="MaxRows"/>.</summary>
        public static IList<string[]> Rows(IntegrityReport report, out int hidden)
        {
            var rows = new List<string[]>();
            foreach (var p in report.Problems)
            {
                rows.Add(new[] { Describe(p.Kind), "Số " + p.InvoiceNumber + " · " + p.InvoiceSymbol, "v" + p.VersionNo.ToString(CultureInfo.InvariantCulture), p.RelativePath });
            }

            foreach (var o in report.Orphans) rows.Add(new[] { "File lạ (không hóa đơn nào dùng)", string.Empty, string.Empty, o });
            foreach (var t in report.StrayTemps) rows.Add(new[] { "File tạm sót lại", string.Empty, string.Empty, t });

            hidden = Math.Max(0, rows.Count - MaxRows);
            return hidden == 0 ? rows : rows.Take(MaxRows).ToList();
        }
    }

    public sealed class IntegrityReportDialog : IIntegrityReportDialog
    {
        public void Show(IntegrityReport report)
        {
            using (var form = new IntegrityReportForm(report)) form.ShowDialog(Form.ActiveForm);
        }
    }
}
