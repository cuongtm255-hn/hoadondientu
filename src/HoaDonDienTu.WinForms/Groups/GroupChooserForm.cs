using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Groups;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Màn chọn nhóm chức năng khi mở ứng dụng (DD-DVC-00): hai thẻ, thẻ nhóm gần nhất được chọn sẵn, Enter để vào, Esc để thoát.
    /// Dựng bằng mã, không có Designer.
    /// </summary>
    public sealed class GroupChooserForm : KryptonForm
    {
        private readonly KryptonButton _invoice;
        private readonly KryptonButton _publicService;

        public GroupChooserForm(AppGroup suggested, bool mockMode)
        {
            Text = mockMode ? "Hóa đơn điện tử — Chọn nhóm chức năng [MÔ PHỎNG]" : "Hóa đơn điện tử — Chọn nhóm chức năng";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 250);
            KeyPreview = true;

            var heading = new Label
            {
                Text = "Chọn nhóm chức năng",
                Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 16)
            };
            _invoice = CreateCard("Tra cứu hóa đơn", "Hóa đơn mua vào, bán ra từ cổng hoadondientu.gdt.gov.vn", new Point(20, 56), AppGroup.Invoice);
            _publicService = CreateCard("Tra cứu Dịch vụ công thuế", "Thủ tục hành chính, hồ sơ, thông báo từ cổng dichvucong.gdt.gov.vn", new Point(290, 56), AppGroup.PublicService);

            Controls.Add(heading);
            Controls.Add(_invoice);
            Controls.Add(_publicService);

            Selected = suggested;
            Shown += (s, e) => (suggested == AppGroup.PublicService ? _publicService : _invoice).Focus();
        }

        /// <summary>Nhóm người dùng đã chọn (hợp lệ khi DialogResult là OK).</summary>
        public AppGroup Selected { get; private set; }

        private KryptonButton CreateCard(string title, string description, Point location, AppGroup group)
        {
            var button = new KryptonButton
            {
                Text = title + Environment.NewLine + Environment.NewLine + description,
                Location = location,
                Size = new Size(250, 150),
                Tag = group
            };
            button.Click += (s, e) => Choose(group);
            return button;
        }

        private void Choose(AppGroup group)
        {
            Selected = group;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }
}
