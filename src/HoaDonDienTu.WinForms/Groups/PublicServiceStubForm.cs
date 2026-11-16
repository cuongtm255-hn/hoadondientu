using System;
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

            var switchButton = new KryptonButton
            {
                Text = "Đổi nhóm chức năng",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            switchButton.Click += (s, e) =>
            {
                var handler = SwitchRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };

            Controls.Add(new Label
            {
                Text = "Nhóm Tra cứu Dịch vụ công thuế đang được phát triển." + Environment.NewLine +
                       "Chưa có chức năng nào ở bản này.",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            Controls.Add(switchButton);
        }

        /// <summary>Người dùng bấm "Đổi nhóm chức năng".</summary>
        public event EventHandler SwitchRequested;
    }
}
