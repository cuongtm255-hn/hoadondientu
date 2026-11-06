using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>SCR-07 Xem chứng từ. Chỉ đọc: XML hiển thị bằng TreeView và TextBox, không dùng control web, không thực thi gì từ nội dung (NFR-SEC-003).</summary>
    public partial class InvoiceViewerControl : KryptonPanel, IInvoiceViewerView
    {
        private static readonly Color Good = Color.FromArgb(30, 110, 60);
        private static readonly Color Bad = Color.FromArgb(170, 40, 30);

        /// <summary>Số nút tối đa để mở hết cây ngay khi hiển thị.</summary>
        public const int FullExpandLimit = 2000;

        private bool _suppress;

        public InvoiceViewerControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
        }

        public event EventHandler CloseRequested;
        public event EventHandler SaveCopyRequested;
        public event EventHandler SavePdfRequested;
        public event EventHandler PrintRequested;
        public event EventHandler<VersionRequestedEventArgs> VersionSelected;

        public void ShowResult(InvoiceViewerViewModel m)
        {
            OnUi(delegate
            {
                lblTitle.Values.Text = m.Title;
                lblSubtitle.Values.Text = m.Subtitle;
                lblSha.Values.Text = m.ShaText;
                btnSaveCopy.Enabled = m.CanSaveCopy;

                _suppress = true;
                try
                {
                    cboVersion.Items.Clear();
                    foreach (var v in m.Versions)
                    {
                        cboVersion.Items.Add(v);
                        if (v.VersionNo == m.SelectedVersion) cboVersion.SelectedItem = v;
                    }
                }
                finally
                {
                    _suppress = false;
                }

                treeXml.BeginUpdate();
                try
                {
                    treeXml.Nodes.Clear();
                    if (m.Tree != null)
                    {
                        var root = ToTreeNode(m.Tree);
                        treeXml.Nodes.Add(root);
                        ExpandForDisplay(root, m.Tree);
                        root.EnsureVisible();
                    }
                    else
                    {
                        treeXml.Nodes.Add(new TreeNode("(XML không đọc được ở dạng cây)"));
                    }
                }
                finally
                {
                    treeXml.EndUpdate();
                }

                txtRaw.Text = m.RawText ?? "(Không đọc được nội dung file)";
                txtRaw.Select(0, 0);
            });
        }

        public void ShowPdf(InvoicePdfViewModel m)
        {
            OnUi(delegate
            {
                btnPrint.Enabled = m.CanPrintOrSave;
                btnSavePdf.Enabled = m.CanPrintOrSave;

                // Giải phóng ảnh cũ trước khi nạp trang mới (mỗi trang là một bitmap).
                foreach (Control old in flowPdfPages.Controls)
                {
                    var box = old as PictureBox;
                    if (box != null && box.Image != null) box.Image.Dispose();
                }

                flowPdfPages.Controls.Clear();
                var pages = m.Pages ?? new System.Collections.Generic.List<byte[]>();
                foreach (var png in pages)
                {
                    using (var stream = new System.IO.MemoryStream(png))
                    {
                        flowPdfPages.Controls.Add(new PictureBox { Image = new Bitmap(stream), SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(0, 0, 0, 12), BackColor = Color.White });
                    }
                }

                var hasPages = pages.Count > 0;
                flowPdfPages.Visible = hasPages;
                lblPdf.Visible = !hasPages;
                lblPdf.Text = m.Notice ?? string.Empty;
                lblPdf.ForeColor = m.IsError ? Bad : Color.FromArgb(60, 60, 60);
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

        /// <summary>
        /// Mở hết cây khi nhỏ; cây lớn (tới 50.000 nút) chỉ mở gốc và các nút con trực tiếp vì ExpandAll trên TreeView treo luồng giao diện nhiều giây.
        /// Người dùng mở thêm từng nhánh khi cần, nội dung thô vẫn có ở tab bên cạnh.
        /// </summary>
        private static void ExpandForDisplay(TreeNode root, ViewerNode model)
        {
            if (CountNodes(model) <= FullExpandLimit)
            {
                root.ExpandAll();
                return;
            }

            root.Expand();
            if (root.Nodes.Count <= 50) foreach (TreeNode child in root.Nodes) child.Expand();
        }

        private static int CountNodes(ViewerNode node)
        {
            var count = 1;
            foreach (var child in node.Children) count += CountNodes(child);
            return count;
        }

        private static TreeNode ToTreeNode(ViewerNode node)
        {
            var result = new TreeNode(node.Text);
            foreach (var child in node.Children) result.Nodes.Add(ToTreeNode(child));
            return result;
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

        private void OnVersionChanged(object sender, EventArgs e)
        {
            if (_suppress) return;
            var option = cboVersion.SelectedItem as ViewerVersionOption;
            var handler = VersionSelected;
            if (option != null && handler != null) handler(this, new VersionRequestedEventArgs(option.VersionNo));
        }

        private void OnPrintClick(object sender, EventArgs e)
        {
            var handler = PrintRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnSavePdfClick(object sender, EventArgs e)
        {
            var handler = SavePdfRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            var handler = CloseRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnSaveCopyClick(object sender, EventArgs e)
        {
            var handler = SaveCopyRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
