using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>SCR-06 Chi tiết hóa đơn. Chỉ hiển thị và phát sự kiện; các nhóm thông tin được dựng từ view model.</summary>
    public partial class InvoiceDetailControl : KryptonPanel, IInvoiceDetailView
    {
        private static readonly Color Good = Color.FromArgb(30, 110, 60);
        private static readonly Color Warning = Color.FromArgb(150, 100, 10);
        private static readonly Color Bad = Color.FromArgb(170, 40, 30);
        private static readonly Color GroupBack = Color.FromArgb(247, 249, 252);

        private const int LabelWidth = 170;
        private const int PartyLabelWidth = 112;
        private const int RowHeight = 28;
        private const int HeaderHeight = 36;

        private readonly List<Control> _groups = new List<Control>();

        public InvoiceDetailControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
        }

        public event EventHandler BackRequested;
        public event EventHandler ViewCurrentRequested;
        public event EventHandler ExportZipRequested;

        public void ShowExportAllowed(bool allowed)
        {
            OnUi(delegate { btnExportZip.Enabled = allowed; });
        }

        public void ShowResult(InvoiceDetailViewModel m)
        {
            OnUi(delegate
            {
                lblTitle.Values.Text = m.Title;
                lblBadge.Values.Text = string.IsNullOrEmpty(m.Badge) ? " " : m.Badge;
                // Nhãn tiêu đề chưa đo lại kích thước lúc này, nên tính vị trí từ độ rộng chữ thực.
                var titleWidth = TextRenderer.MeasureText(m.Title ?? string.Empty, lblTitle.StateCommon.ShortText.Font).Width;
                lblBadge.Location = new Point(lblTitle.Left + titleWidth + 32, lblBadge.Location.Y);
                lblBadge.Visible = !string.IsNullOrEmpty(m.Badge);
                lblSubtitle.Values.Text = m.Subtitle;
                btnView.Enabled = m.CanViewXml;

                ClearGroups();
                flowLeft.SuspendLayout();
                flowRight.SuspendLayout();
                try
                {
                    AddGroup(flowLeft, "Thông tin hóa đơn", KeyValues(m.Info, false));
                    AddGroup(flowLeft, "Bên bán và bên mua", Parties(m));
                    AddGroup(flowLeft, "Giá trị hóa đơn", KeyValues(m.Values, true));
                    AddGroup(flowRight, "File chứng từ", Files(m));
                    AddGroup(flowRight, "Lịch sử phiên bản", Versions(m.Versions));
                    AddGroup(flowRight, "Nguồn tải", KeyValues(m.Source, false));
                }
                finally
                {
                    flowLeft.ResumeLayout(true);
                    flowRight.ResumeLayout(true);
                }

                ResizeGroups();
            });
        }

        public void ShowMessage(string text, bool isError)
        {
            OnUi(delegate
            {
                lblMessage.Text = text ?? string.Empty;
                lblMessage.ForeColor = isError ? Bad : Good;
            });
        }

        // ---- Dựng các nhóm ----

        private void ClearGroups()
        {
            flowLeft.Controls.Clear();
            flowRight.Controls.Clear();
            foreach (var g in _groups) g.Dispose();
            _groups.Clear();
        }

        private void AddGroup(FlowLayoutPanel host, string title, Control content)
        {
            // Không dùng AutoSize: nó bỏ qua chiều rộng đặt theo khung chứa. Chiều cao tính từ nội dung đã biết trước.
            var table = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = GroupBack,
                Width = Math.Max(240, host.ClientSize.Width - 20),
                Height = HeaderHeight + content.Height + 12
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var header = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                Dock = DockStyle.Fill
            };
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(8, 0, 8, 8);
            table.Controls.Add(header, 0, 0);
            table.Controls.Add(content, 0, 1);
            host.Controls.Add(table);
            _groups.Add(table);
        }

        private static TableLayoutPanel KeyValues(IList<DetailField> fields, bool rightAlign, int labelWidth = LabelWidth)
        {
            var table = NewTable(2);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            foreach (var f in fields)
            {
                var row = table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
                var label = Cell(f.Label, f.Emphasized, false);
                label.ForeColor = SystemColors.GrayText;
                table.Controls.Add(label, 0, row);
                table.Controls.Add(Cell(f.Value, f.Emphasized, rightAlign), 1, row);
            }

            table.Height = table.RowCount * RowHeight;
            return table;
        }

        private static Control Parties(InvoiceDetailViewModel m)
        {
            var table = NewTable(2);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.RowCount = 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var seller = KeyValues(m.Seller, false, PartyLabelWidth);
            var buyer = KeyValues(m.Buyer, false, PartyLabelWidth);
            seller.Margin = new Padding(0, 0, 8, 0);
            buyer.Margin = new Padding(8, 0, 0, 0);
            table.Controls.Add(seller, 0, 0);
            table.Controls.Add(buyer, 1, 0);
            table.Height = Math.Max(seller.Height, buyer.Height);
            return table;
        }

        private Control Files(InvoiceDetailViewModel m)
        {
            // Mở XML/PDF đã có nút "Xem chứng từ" ở đầu màn hình; PDF dựng khi xem nên không có nút "Tạo".
            var table = NewTable(2);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            AddFileRow(table, "XML", m.XmlText, m.XmlSub, m.XmlProblem);
            AddFileRow(table, "PDF", m.PdfText, m.PdfSub, false);
            table.Height = table.RowCount * 56;
            return table;
        }

        private static void AddFileRow(TableLayoutPanel table, string kind, string text, string sub, bool problem)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            var badge = Cell(kind, true, false);
            badge.ForeColor = problem ? Bad : Good;
            var info = new Label { Text = text + "\r\n" + sub, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            if (problem) info.ForeColor = Bad;
            table.Controls.Add(badge, 0, row);
            table.Controls.Add(info, 1, row);
        }

        private Control Versions(IList<DetailVersionRow> versions)
        {
            // Đổi phiên bản xem ở màn Xem chứng từ (ô chọn phiên bản), nên mỗi dòng chỉ liệt kê.
            var table = NewTable(3);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            foreach (var v in versions)
            {
                var row = table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                var no = Cell(v.Name, true, false);
                var info = new Label { Text = v.Date + "\r\n" + v.Session, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                var status = Cell(v.Status, true, false);
                status.ForeColor = v.Problem ? Bad : v.Status == "Hiện hành" ? Good : Warning;
                table.Controls.Add(no, 0, row);
                table.Controls.Add(info, 1, row);
                table.Controls.Add(status, 2, row);
            }

            table.Height = Math.Max(1, table.RowCount) * 48;
            return table;
        }

        private static TableLayoutPanel NewTable(int columns)
        {
            return new TableLayoutPanel { ColumnCount = columns, RowCount = 0, BackColor = Color.Transparent, Height = 10 };
        }

        private static Label Cell(string text, bool bold, bool right)
        {
            return new Label
            {
                Text = text ?? string.Empty,
                Dock = DockStyle.Fill,
                TextAlign = right ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = bold ? new Font("Segoe UI", 9F, FontStyle.Bold) : new Font("Segoe UI", 9F)
            };
        }

        // ---- Nội bộ ----

        private void OnFlowResize(object sender, EventArgs e)
        {
            ResizeGroups();
        }

        private void ResizeGroups()
        {
            foreach (var g in _groups)
            {
                var host = g.Parent as FlowLayoutPanel;
                if (host != null) g.Width = Math.Max(240, host.ClientSize.Width - 20);
            }
        }

        private void OnUi(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnBackClick(object sender, EventArgs e)
        {
            Raise(BackRequested);
        }

        private void OnViewClick(object sender, EventArgs e)
        {
            Raise(ViewCurrentRequested);
        }

        private void OnExportZipClick(object sender, EventArgs e)
        {
            Raise(ExportZipRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
