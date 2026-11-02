namespace HoaDonDienTu.WinForms.Detail
{
    partial class InvoiceViewerControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pnlTitle = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlTitleActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnClose = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnPrint = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnSaveCopy = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlMessage = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlTools = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblSha = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.cboVersion = new ComponentFactory.Krypton.Toolkit.KryptonComboBox();
            this.lblVersion = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.tabTree = new System.Windows.Forms.TabPage();
            this.treeXml = new System.Windows.Forms.TreeView();
            this.tabRaw = new System.Windows.Forms.TabPage();
            this.txtRaw = new System.Windows.Forms.TextBox();
            this.tabPdf = new System.Windows.Forms.TabPage();
            this.lblPdf = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.cboVersion)).BeginInit();
            this.tabs.SuspendLayout();
            this.tabTree.SuspendLayout();
            this.tabRaw.SuspendLayout();
            this.tabPdf.SuspendLayout();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(1000, 84);
            this.pnlTitle.TabIndex = 4;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Xem chứng từ";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Location = new System.Drawing.Point(16, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(520, 24);
            this.lblSubtitle.Values.Text = " ";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.btnSaveCopy);
            this.pnlTitleActions.Controls.Add(this.btnPrint);
            this.pnlTitleActions.Controls.Add(this.btnClose);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 22, 16, 22);
            this.pnlTitleActions.Size = new System.Drawing.Size(380, 84);
            //
            // btnClose / btnPrint / btnSaveCopy (thứ tự thêm quyết định thứ tự xếp từ phải sang trái)
            //
            this.btnClose.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 40);
            this.btnClose.Values.Text = "Đóng";
            this.btnClose.Click += new System.EventHandler(this.OnCloseClick);
            this.btnPrint.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnPrint.Enabled = false;
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Size = new System.Drawing.Size(80, 40);
            this.btnPrint.Values.Text = "In";
            this.btnSaveCopy.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnSaveCopy.Enabled = false;
            this.btnSaveCopy.Name = "btnSaveCopy";
            this.btnSaveCopy.Size = new System.Drawing.Size(130, 40);
            this.btnSaveCopy.Values.Text = "Lưu bản sao";
            this.btnSaveCopy.Click += new System.EventHandler(this.OnSaveCopyClick);
            //
            // pnlMessage
            //
            this.pnlMessage.Controls.Add(this.lblMessage);
            this.pnlMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMessage.Name = "pnlMessage";
            this.pnlMessage.Padding = new System.Windows.Forms.Padding(16, 4, 16, 4);
            this.pnlMessage.Size = new System.Drawing.Size(1000, 30);
            this.pnlMessage.TabIndex = 3;
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Text = "";
            //
            // pnlTools
            //
            this.pnlTools.Controls.Add(this.lblSha);
            this.pnlTools.Controls.Add(this.cboVersion);
            this.pnlTools.Controls.Add(this.lblVersion);
            this.pnlTools.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTools.Name = "pnlTools";
            this.pnlTools.Padding = new System.Windows.Forms.Padding(16, 6, 16, 6);
            this.pnlTools.Size = new System.Drawing.Size(1000, 40);
            this.pnlTools.TabIndex = 2;
            this.lblVersion.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Values.Text = "Phiên bản XML:";
            this.cboVersion.Dock = System.Windows.Forms.DockStyle.Left;
            this.cboVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboVersion.Name = "cboVersion";
            this.cboVersion.Size = new System.Drawing.Size(170, 25);
            this.cboVersion.SelectedIndexChanged += new System.EventHandler(this.OnVersionChanged);
            this.lblSha.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblSha.Name = "lblSha";
            this.lblSha.Values.Text = " ";
            //
            // tabs
            //
            this.tabs.Controls.Add(this.tabTree);
            this.tabs.Controls.Add(this.tabRaw);
            this.tabs.Controls.Add(this.tabPdf);
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Name = "tabs";
            this.tabs.Padding = new System.Drawing.Point(14, 5);
            this.tabs.TabIndex = 0;
            //
            // tabTree
            //
            this.tabTree.Controls.Add(this.treeXml);
            this.tabTree.Name = "tabTree";
            this.tabTree.Text = "XML dạng cây";
            this.treeXml.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeXml.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.treeXml.Name = "treeXml";
            this.treeXml.ShowLines = true;
            //
            // tabRaw
            //
            this.tabRaw.Controls.Add(this.txtRaw);
            this.tabRaw.Name = "tabRaw";
            this.tabRaw.Text = "XML thô";
            this.txtRaw.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRaw.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.txtRaw.Multiline = true;
            this.txtRaw.Name = "txtRaw";
            this.txtRaw.ReadOnly = true;
            this.txtRaw.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtRaw.WordWrap = false;
            //
            // tabPdf
            //
            this.tabPdf.Controls.Add(this.lblPdf);
            this.tabPdf.Name = "tabPdf";
            this.tabPdf.Text = "PDF (chưa hỗ trợ)";
            this.lblPdf.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPdf.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblPdf.Name = "lblPdf";
            this.lblPdf.Padding = new System.Windows.Forms.Padding(24);
            this.lblPdf.Text = "Chức năng xem và in PDF chưa khả dụng: đang chờ quyết định cách tạo PDF (G-03) và thư viện hỗ trợ (G-09).\r\nXML là chứng từ gốc; bạn vẫn xem được nội dung ở hai tab bên cạnh và lưu bản sao XML.";
            //
            // InvoiceViewerControl
            //
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.pnlTools);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.pnlTitle);
            this.Name = "InvoiceViewerControl";
            this.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.Size = new System.Drawing.Size(1000, 640);
            ((System.ComponentModel.ISupportInitialize)(this.cboVersion)).EndInit();
            this.tabs.ResumeLayout(false);
            this.tabTree.ResumeLayout(false);
            this.tabRaw.ResumeLayout(false);
            this.tabRaw.PerformLayout();
            this.tabPdf.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnClose;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnPrint;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSaveCopy;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTools;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSha;
        private ComponentFactory.Krypton.Toolkit.KryptonComboBox cboVersion;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblVersion;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.TabPage tabTree;
        private System.Windows.Forms.TreeView treeXml;
        private System.Windows.Forms.TabPage tabRaw;
        private System.Windows.Forms.TextBox txtRaw;
        private System.Windows.Forms.TabPage tabPdf;
        private System.Windows.Forms.Label lblPdf;
    }
}
