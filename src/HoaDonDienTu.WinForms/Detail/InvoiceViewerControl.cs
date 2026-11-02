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

        private bool _suppress;

        public InvoiceViewerControl()
        {
            InitializeComponent();
        }

        public event EventHandler CloseRequested;
        public event EventHandler SaveCopyRequested;
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
                        treeXml.Nodes.Add(ToTreeNode(m.Tree));
                        treeXml.ExpandAll();
                        if (treeXml.Nodes.Count > 0) treeXml.Nodes[0].EnsureVisible();
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

        public void ShowMessage(string text, bool isError)
        {
            OnUi(delegate
            {
                lblMessage.Text = text ?? string.Empty;
                lblMessage.ForeColor = isError ? Bad : Good;
            });
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
