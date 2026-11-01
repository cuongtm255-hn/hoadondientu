using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Shell
{
    /// <summary>Màn hình tạm cho tới khi từng feature (DD-02…DD-11) được implement.</summary>
    public sealed class PlaceholderScreenControl : KryptonPanel
    {
        public PlaceholderScreenControl(string title, string subtitle)
        {
            var lblTitle = new KryptonLabel
            {
                Text = title,
                Location = new Point(24, 20),
                AutoSize = true
            };
            lblTitle.StateCommon.ShortText.Font = new Font("Segoe UI", 18F, FontStyle.Bold);

            var lblSubtitle = new KryptonLabel
            {
                Text = subtitle,
                Location = new Point(26, 62),
                AutoSize = true
            };

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
        }
    }
}
