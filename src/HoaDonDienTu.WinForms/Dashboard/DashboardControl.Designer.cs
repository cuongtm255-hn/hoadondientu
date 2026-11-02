namespace HoaDonDienTu.WinForms.Dashboard
{
    partial class DashboardControl
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
            System.Windows.Forms.DataGridViewCellStyle rightStyle = new System.Windows.Forms.DataGridViewCellStyle();
            this.pnlTitle = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.pnlTitleActions = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnNewDownload = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblModeNote = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlMessage = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.tlpStats = new System.Windows.Forms.TableLayoutPanel();
            this.pnlStat1 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat1Sub = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat1Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat1Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat2 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat2Sub = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat2Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat2Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat3 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat3Sub = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat3Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat3Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlStat4 = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblStat4Sub = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat4Value = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblStat4Caption = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.tlpMiddle = new System.Windows.Forms.TableLayoutPanel();
            this.pnlSession = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblNoSession = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.btnSession = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSessionStats = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblProgressText = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.prgSession = new System.Windows.Forms.ProgressBar();
            this.lblSessionDetail = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblSessionTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblSessionBadge = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblSessionHeader = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlAttention = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.flowAttention = new System.Windows.Forms.FlowLayoutPanel();
            this.lblAttentionHeader = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlRecent = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.gridRecent = new ComponentFactory.Krypton.Toolkit.KryptonDataGridView();
            this.colDate = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDirection = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPartner = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAmount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pnlRecentHeader = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnOpenArchive = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblRecentHeader = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            ((System.ComponentModel.ISupportInitialize)(this.gridRecent)).BeginInit();
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
            this.pnlTitle.TabIndex = 5;
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Tổng quan";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Location = new System.Drawing.Point(16, 46);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(520, 24);
            this.lblSubtitle.Values.Text = "Tình trạng kho hóa đơn và công việc gần đây";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.lblModeNote);
            this.pnlTitleActions.Controls.Add(this.btnNewDownload);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 22, 16, 22);
            this.pnlTitleActions.Size = new System.Drawing.Size(430, 84);
            //
            // btnNewDownload
            //
            this.btnNewDownload.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnNewDownload.Name = "btnNewDownload";
            this.btnNewDownload.Size = new System.Drawing.Size(150, 40);
            this.btnNewDownload.Values.Text = "Tải hóa đơn";
            this.btnNewDownload.Click += new System.EventHandler(this.OnNewDownloadClick);
            //
            // lblModeNote
            //
            this.lblModeNote.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblModeNote.Name = "lblModeNote";
            this.lblModeNote.StateCommon.ShortText.TextH = ComponentFactory.Krypton.Toolkit.PaletteRelativeAlign.Far;
            this.lblModeNote.Values.Text = " ";
            //
            // pnlMessage
            //
            this.pnlMessage.Controls.Add(this.lblMessage);
            this.pnlMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMessage.Name = "pnlMessage";
            this.pnlMessage.Padding = new System.Windows.Forms.Padding(16, 4, 16, 4);
            this.pnlMessage.Size = new System.Drawing.Size(1000, 28);
            this.pnlMessage.TabIndex = 4;
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Text = "";
            //
            // tlpStats
            //
            this.tlpStats.BackColor = System.Drawing.Color.Transparent;
            this.tlpStats.ColumnCount = 4;
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tlpStats.Controls.Add(this.pnlStat1, 0, 0);
            this.tlpStats.Controls.Add(this.pnlStat2, 1, 0);
            this.tlpStats.Controls.Add(this.pnlStat3, 2, 0);
            this.tlpStats.Controls.Add(this.pnlStat4, 3, 0);
            this.tlpStats.Dock = System.Windows.Forms.DockStyle.Top;
            this.tlpStats.Name = "tlpStats";
            this.tlpStats.Padding = new System.Windows.Forms.Padding(16, 6, 16, 0);
            this.tlpStats.RowCount = 1;
            this.tlpStats.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpStats.Size = new System.Drawing.Size(1000, 98);
            this.tlpStats.TabIndex = 3;
            //
            // stat cards
            //
            this.pnlStat1.Controls.Add(this.lblStat1Sub);
            this.pnlStat1.Controls.Add(this.lblStat1Value);
            this.pnlStat1.Controls.Add(this.lblStat1Caption);
            this.pnlStat1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat1.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat1.Name = "pnlStat1";
            this.pnlStat1.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat1Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Caption.Name = "lblStat1Caption";
            this.lblStat1Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Caption.Values.Text = "HÓA ĐƠN ĐÃ LƯU";
            this.lblStat1Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Value.Name = "lblStat1Value";
            this.lblStat1Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat1Value.Values.Text = "0";
            this.lblStat1Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Sub.Name = "lblStat1Sub";
            this.lblStat1Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Sub.Values.Text = "—";
            this.pnlStat2.Controls.Add(this.lblStat2Sub);
            this.pnlStat2.Controls.Add(this.lblStat2Value);
            this.pnlStat2.Controls.Add(this.lblStat2Caption);
            this.pnlStat2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat2.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat2.Name = "pnlStat2";
            this.pnlStat2.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat2Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Caption.Name = "lblStat2Caption";
            this.lblStat2Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat2Caption.Values.Text = "TỔNG THANH TOÁN NĂM";
            this.lblStat2Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Value.Name = "lblStat2Value";
            this.lblStat2Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat2Value.Values.Text = "0";
            this.lblStat2Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Sub.Name = "lblStat2Sub";
            this.lblStat2Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat2Sub.Values.Text = "—";
            this.pnlStat3.Controls.Add(this.lblStat3Sub);
            this.pnlStat3.Controls.Add(this.lblStat3Value);
            this.pnlStat3.Controls.Add(this.lblStat3Caption);
            this.pnlStat3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat3.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlStat3.Name = "pnlStat3";
            this.pnlStat3.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat3Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Caption.Name = "lblStat3Caption";
            this.lblStat3Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat3Caption.Values.Text = "LẦN ĐỒNG BỘ GẦN NHẤT";
            this.lblStat3Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Value.Name = "lblStat3Value";
            this.lblStat3Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat3Value.Values.Text = "—";
            this.lblStat3Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Sub.Name = "lblStat3Sub";
            this.lblStat3Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat3Sub.Values.Text = "—";
            this.pnlStat4.Controls.Add(this.lblStat4Sub);
            this.pnlStat4.Controls.Add(this.lblStat4Value);
            this.pnlStat4.Controls.Add(this.lblStat4Caption);
            this.pnlStat4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat4.Margin = new System.Windows.Forms.Padding(0);
            this.pnlStat4.Name = "pnlStat4";
            this.pnlStat4.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat4Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Caption.Name = "lblStat4Caption";
            this.lblStat4Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat4Caption.Values.Text = "CẦN XỬ LÝ";
            this.lblStat4Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Value.Name = "lblStat4Value";
            this.lblStat4Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat4Value.Values.Text = "0";
            this.lblStat4Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Sub.Name = "lblStat4Sub";
            this.lblStat4Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat4Sub.Values.Text = "—";
            //
            // tlpMiddle
            //
            this.tlpMiddle.BackColor = System.Drawing.Color.Transparent;
            this.tlpMiddle.ColumnCount = 2;
            this.tlpMiddle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpMiddle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpMiddle.Controls.Add(this.pnlSession, 0, 0);
            this.tlpMiddle.Controls.Add(this.pnlAttention, 1, 0);
            this.tlpMiddle.Dock = System.Windows.Forms.DockStyle.Top;
            this.tlpMiddle.Name = "tlpMiddle";
            this.tlpMiddle.Padding = new System.Windows.Forms.Padding(16, 10, 16, 0);
            this.tlpMiddle.RowCount = 1;
            this.tlpMiddle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpMiddle.Size = new System.Drawing.Size(1000, 190);
            this.tlpMiddle.TabIndex = 2;
            //
            // pnlSession
            //
            this.pnlSession.Controls.Add(this.lblNoSession);
            this.pnlSession.Controls.Add(this.btnSession);
            this.pnlSession.Controls.Add(this.lblSessionStats);
            this.pnlSession.Controls.Add(this.lblProgressText);
            this.pnlSession.Controls.Add(this.prgSession);
            this.pnlSession.Controls.Add(this.lblSessionDetail);
            this.pnlSession.Controls.Add(this.lblSessionTitle);
            this.pnlSession.Controls.Add(this.lblSessionBadge);
            this.pnlSession.Controls.Add(this.lblSessionHeader);
            this.pnlSession.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSession.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.pnlSession.Name = "pnlSession";
            this.pnlSession.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblSessionHeader.Location = new System.Drawing.Point(8, 6);
            this.lblSessionHeader.Name = "lblSessionHeader";
            this.lblSessionHeader.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblSessionHeader.Values.Text = "Phiên tải đang chạy";
            this.lblSessionBadge.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSessionBadge.Location = new System.Drawing.Point(330, 6);
            this.lblSessionBadge.Name = "lblSessionBadge";
            this.lblSessionBadge.Values.Text = " ";
            this.lblSessionTitle.Location = new System.Drawing.Point(8, 44);
            this.lblSessionTitle.Name = "lblSessionTitle";
            this.lblSessionTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblSessionTitle.Values.Text = " ";
            this.lblSessionDetail.Location = new System.Drawing.Point(8, 72);
            this.lblSessionDetail.Name = "lblSessionDetail";
            this.lblSessionDetail.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblSessionDetail.Values.Text = " ";
            this.prgSession.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.prgSession.Location = new System.Drawing.Point(12, 104);
            this.prgSession.Name = "prgSession";
            this.prgSession.Size = new System.Drawing.Size(440, 10);
            this.lblProgressText.Location = new System.Drawing.Point(8, 120);
            this.lblProgressText.Name = "lblProgressText";
            this.lblProgressText.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblProgressText.Values.Text = " ";
            this.lblSessionStats.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSessionStats.Location = new System.Drawing.Point(300, 120);
            this.lblSessionStats.Name = "lblSessionStats";
            this.lblSessionStats.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblSessionStats.Values.Text = " ";
            this.btnSession.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSession.Location = new System.Drawing.Point(350, 44);
            this.btnSession.Name = "btnSession";
            this.btnSession.Size = new System.Drawing.Size(104, 34);
            this.btnSession.Values.Text = "Xem tiến độ";
            this.btnSession.Click += new System.EventHandler(this.OnSessionClick);
            this.lblNoSession.Location = new System.Drawing.Point(8, 50);
            this.lblNoSession.Name = "lblNoSession";
            this.lblNoSession.Values.Text = "Không có phiên nào đang chạy.";
            //
            // pnlAttention
            //
            this.pnlAttention.Controls.Add(this.flowAttention);
            this.pnlAttention.Controls.Add(this.lblAttentionHeader);
            this.pnlAttention.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlAttention.Margin = new System.Windows.Forms.Padding(0);
            this.pnlAttention.Name = "pnlAttention";
            this.pnlAttention.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblAttentionHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblAttentionHeader.Name = "lblAttentionHeader";
            this.lblAttentionHeader.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblAttentionHeader.Values.Text = "Công việc cần xử lý";
            this.flowAttention.AutoScroll = true;
            this.flowAttention.BackColor = System.Drawing.Color.Transparent;
            this.flowAttention.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowAttention.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowAttention.Name = "flowAttention";
            this.flowAttention.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.flowAttention.WrapContents = false;
            this.flowAttention.Resize += new System.EventHandler(this.OnAttentionResize);
            //
            // pnlRecent
            //
            this.pnlRecent.Controls.Add(this.gridRecent);
            this.pnlRecent.Controls.Add(this.pnlRecentHeader);
            this.pnlRecent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRecent.Name = "pnlRecent";
            this.pnlRecent.Padding = new System.Windows.Forms.Padding(16, 10, 16, 12);
            this.pnlRecent.TabIndex = 0;
            //
            // pnlRecentHeader
            //
            this.pnlRecentHeader.Controls.Add(this.lblRecentHeader);
            this.pnlRecentHeader.Controls.Add(this.btnOpenArchive);
            this.pnlRecentHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlRecentHeader.Name = "pnlRecentHeader";
            this.pnlRecentHeader.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlRecentHeader.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
            this.pnlRecentHeader.Size = new System.Drawing.Size(968, 44);
            this.btnOpenArchive.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnOpenArchive.Name = "btnOpenArchive";
            this.btnOpenArchive.Size = new System.Drawing.Size(140, 32);
            this.btnOpenArchive.Values.Text = "Mở Kho hóa đơn";
            this.btnOpenArchive.Click += new System.EventHandler(this.OnOpenArchiveClick);
            this.lblRecentHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRecentHeader.Name = "lblRecentHeader";
            this.lblRecentHeader.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblRecentHeader.Values.Text = "Hóa đơn mới tải";
            //
            // gridRecent
            //
            this.gridRecent.AllowUserToAddRows = false;
            this.gridRecent.AllowUserToDeleteRows = false;
            this.gridRecent.AllowUserToResizeRows = false;
            this.gridRecent.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridRecent.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colDate, this.colDirection, this.colPartner, this.colNumber, this.colStatus, this.colAmount});
            this.gridRecent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridRecent.Name = "gridRecent";
            this.gridRecent.ReadOnly = true;
            this.gridRecent.RowHeadersVisible = false;
            this.gridRecent.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRecent.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.OnCellFormatting);
            rightStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.colDate.FillWeight = 70F;
            this.colDate.HeaderText = "Ngày lập";
            this.colDate.Name = "colDate";
            this.colDirection.FillWeight = 60F;
            this.colDirection.HeaderText = "Loại";
            this.colDirection.Name = "colDirection";
            this.colPartner.FillWeight = 190F;
            this.colPartner.HeaderText = "Đối tác";
            this.colPartner.Name = "colPartner";
            this.colNumber.FillWeight = 70F;
            this.colNumber.HeaderText = "Số hóa đơn";
            this.colNumber.Name = "colNumber";
            this.colStatus.FillWeight = 100F;
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colAmount.DefaultCellStyle = rightStyle;
            this.colAmount.FillWeight = 90F;
            this.colAmount.HeaderText = "Tổng thanh toán";
            this.colAmount.Name = "colAmount";
            //
            // DashboardControl
            //
            this.Controls.Add(this.pnlRecent);
            this.Controls.Add(this.tlpMiddle);
            this.Controls.Add(this.tlpStats);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.pnlTitle);
            this.Name = "DashboardControl";
            this.Size = new System.Drawing.Size(1000, 640);
            ((System.ComponentModel.ISupportInitialize)(this.gridRecent)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnNewDownload;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblModeNote;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private System.Windows.Forms.TableLayoutPanel tlpStats;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat1;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Sub;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat1Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat2;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Sub;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat2Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat3;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Sub;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat3Caption;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlStat4;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Sub;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Value;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblStat4Caption;
        private System.Windows.Forms.TableLayoutPanel tlpMiddle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlSession;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblNoSession;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnSession;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSessionStats;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblProgressText;
        private System.Windows.Forms.ProgressBar prgSession;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSessionDetail;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSessionTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSessionBadge;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSessionHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlAttention;
        private System.Windows.Forms.FlowLayoutPanel flowAttention;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblAttentionHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlRecent;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlRecentHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnOpenArchive;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblRecentHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonDataGridView gridRecent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDate;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDirection;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPartner;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAmount;
    }
}
