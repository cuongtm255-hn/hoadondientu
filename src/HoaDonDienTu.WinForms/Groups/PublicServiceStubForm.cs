using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Chỗ giữ chỗ cho shell nhóm Dịch vụ công cho tới khi có DD-DVC-01 trở đi. Không gọi cổng, không có dữ liệu.
    /// </summary>
    public sealed class PublicServiceStubForm : KryptonForm
    {
        public PublicServiceStubForm()
        {
            Text = "Hóa đơn điện tử — Tra cứu Dịch vụ công thuế";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 200);
            Controls.Add(new Label
            {
                Text = "Nhóm Tra cứu Dịch vụ công thuế đang được phát triển." + System.Environment.NewLine +
                       "Chưa có chức năng nào ở bản này.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
    }
}
