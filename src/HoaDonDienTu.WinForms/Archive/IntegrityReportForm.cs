using System;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>Báo cáo kiểm tra toàn vẹn kho: từng file có vấn đề, hóa đơn và phiên bản tương ứng, file lạ và file tạm sót lại. Chỉ xem, không sửa hay xóa gì.</summary>
    public partial class IntegrityReportForm : KryptonForm
    {
        public IntegrityReportForm(IntegrityReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            InitializeComponent();
            lblSummary.Text = IntegrityReportText.Summary(report);

            int hidden;
            var rows = IntegrityReportText.Rows(report, out hidden);
            list.BeginUpdate();
            foreach (var row in rows) list.Items.Add(new ListViewItem(row));
            list.EndUpdate();

            lblNote.Text = (hidden > 0 ? "Chỉ hiển thị " + IntegrityReportText.MaxRows.ToString("N0") + " dòng đầu, còn " + hidden.ToString("N0") + " dòng nữa. " : string.Empty)
                + "Ứng dụng không tự sửa hay xóa file nào. File thiếu hoặc bị đổi: tải lại hóa đơn từ cổng để thay thế; file lạ và file tạm: có thể xóa tay nếu chắc chắn không cần.";
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
