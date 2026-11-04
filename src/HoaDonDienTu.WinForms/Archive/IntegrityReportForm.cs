using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>
    /// Báo cáo kiểm tra toàn vẹn kho: từng file có vấn đề, hóa đơn và phiên bản tương ứng, file lạ và file tạm sót lại.
    /// Chỉ xem; việc duy nhất làm được là xóa file tạm sót lại (bấm nút, có hỏi lại).
    /// </summary>
    public partial class IntegrityReportForm : KryptonForm
    {
        private readonly IntegrityReport _report;
        private readonly Func<IList<string>, IList<string>> _tempCleaner;

        /// <param name="tempCleaner">Xóa các file tạm theo đường dẫn tương đối và trả về những file đã xóa được; null thì không có nút xóa.</param>
        public IntegrityReportForm(IntegrityReport report, Func<IList<string>, IList<string>> tempCleaner = null)
        {
            if (report == null) throw new ArgumentNullException("report");
            _report = report;
            _tempCleaner = tempCleaner;
            InitializeComponent();
            Fill();
        }

        private void Fill()
        {
            lblSummary.Text = IntegrityReportText.Summary(_report);

            int hidden;
            var rows = IntegrityReportText.Rows(_report, out hidden);
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var row in rows) list.Items.Add(new ListViewItem(row));
            list.EndUpdate();

            var canClean = _tempCleaner != null && _report.StrayTemps.Count > 0;
            btnCleanTemps.Visible = canClean;
            var tempHint = canClean
                ? "file tạm: bấm \"Xóa file tạm\" để dọn (chỉ là bản ghi dở, an toàn); file lạ: có thể xóa tay nếu chắc chắn không cần."
                : "file lạ và file tạm: có thể xóa tay nếu chắc chắn không cần.";
            lblNote.Text = (hidden > 0 ? "Chỉ hiển thị " + IntegrityReportText.MaxRows.ToString("N0") + " dòng đầu, còn " + hidden.ToString("N0") + " dòng nữa. " : string.Empty)
                + "Ứng dụng không tự sửa hay xóa file XML nào. File thiếu hoặc bị đổi: tải lại hóa đơn từ cổng để thay thế; " + tempHint;
        }

        private void OnCleanTempsClick(object sender, EventArgs e)
        {
            var paths = _report.StrayTemps.ToList();
            if (paths.Count == 0 || _tempCleaner == null) return;
            var confirm = KryptonMessageBox.Show(this,
                "Xóa " + paths.Count.ToString("N0") + " file tạm sót lại do ghi dở? File hóa đơn chính thức không bị đụng tới.",
                "Xóa file tạm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            IList<string> deleted;
            try
            {
                deleted = _tempCleaner(paths);
            }
            catch (Exception ex)
            {
                KryptonMessageBox.Show(this, "Không xóa được file tạm: " + ex.Message, "Xóa file tạm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Chỉ bỏ khỏi báo cáo những file đã thật sự xóa; file bị khóa vẫn còn để thử lại.
            foreach (var path in deleted) _report.StrayTemps.Remove(path);
            Fill();
            if (_report.StrayTemps.Count > 0)
            {
                KryptonMessageBox.Show(this, "Đã xóa " + deleted.Count.ToString("N0") + "/" + paths.Count.ToString("N0") + " file tạm; các file còn lại đang bị khóa hoặc không có quyền xóa.",
                    "Xóa file tạm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
