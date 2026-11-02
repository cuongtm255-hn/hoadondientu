namespace HoaDonDienTu.WinForms.Download
{
    partial class DownloadProgressControl
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
            this.tlpActions = new System.Windows.Forms.TableLayoutPanel();
            this.btnPause = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnResume = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnCancel = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSubtitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblBreadcrumb = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
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
            this.pnlOverall = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.progressTotal = new System.Windows.Forms.ProgressBar();
            this.lblCheckpoint = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.lblOverallTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            this.pnlMessage = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.pnlSegments = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.gridSegments = new ComponentFactory.Krypton.Toolkit.KryptonDataGridView();
            this.colMonth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRange = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colState = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPages = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colReceived = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSaved = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRetry = new System.Windows.Forms.DataGridViewButtonColumn();
            this.pnlSegmentsHeader = new ComponentFactory.Krypton.Toolkit.KryptonPanel();
            this.btnRetryFailed = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.lblSegmentsTitle = new ComponentFactory.Krypton.Toolkit.KryptonLabel();
            ((System.ComponentModel.ISupportInitialize)(this.gridSegments)).BeginInit();
            this.SuspendLayout();
            //
            // pnlTitle
            //
            this.pnlTitle.Controls.Add(this.pnlTitleActions);
            this.pnlTitle.Controls.Add(this.lblSubtitle);
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Controls.Add(this.lblBreadcrumb);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(960, 100);
            this.pnlTitle.TabIndex = 4;
            //
            // lblBreadcrumb
            //
            this.lblBreadcrumb.Location = new System.Drawing.Point(14, 6);
            this.lblBreadcrumb.Name = "lblBreadcrumb";
            this.lblBreadcrumb.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblBreadcrumb.Values.Text = "Tải hóa đơn / Tiến độ";
            //
            // lblTitle
            //
            this.lblTitle.Location = new System.Drawing.Point(14, 26);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Values.Text = "Tiến độ tải hóa đơn";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = false;
            this.lblSubtitle.Size = new System.Drawing.Size(520, 24);
            this.lblSubtitle.Location = new System.Drawing.Point(16, 66);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Values.Text = "Đang tải thông tin phiên…";
            //
            // pnlTitleActions
            //
            this.pnlTitleActions.Controls.Add(this.tlpActions);
            this.pnlTitleActions.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTitleActions.Name = "pnlTitleActions";
            this.pnlTitleActions.Padding = new System.Windows.Forms.Padding(0, 30, 16, 30);
            this.pnlTitleActions.Size = new System.Drawing.Size(420, 100);
            //
            // tlpActions
            //
            this.tlpActions.BackColor = System.Drawing.Color.Transparent;
            this.tlpActions.ColumnCount = 3;
            this.tlpActions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tlpActions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
            this.tlpActions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.34F));
            this.tlpActions.Controls.Add(this.btnPause, 0, 0);
            this.tlpActions.Controls.Add(this.btnResume, 1, 0);
            this.tlpActions.Controls.Add(this.btnCancel, 2, 0);
            this.tlpActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpActions.Name = "tlpActions";
            this.tlpActions.RowCount = 1;
            this.tlpActions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpActions.Size = new System.Drawing.Size(404, 40);
            //
            // btnPause
            //
            this.btnPause.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnPause.Enabled = false;
            this.btnPause.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.btnPause.Name = "btnPause";
            this.btnPause.Values.Text = "Tạm dừng";
            this.btnPause.Click += new System.EventHandler(this.OnPauseClick);
            //
            // btnResume
            //
            this.btnResume.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnResume.Enabled = false;
            this.btnResume.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.btnResume.Name = "btnResume";
            this.btnResume.StateCommon.Content.ShortText.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnResume.Values.Text = "Tiếp tục";
            this.btnResume.Click += new System.EventHandler(this.OnResumeClick);
            //
            // btnCancel
            //
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCancel.Enabled = false;
            this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Values.Text = "Hủy phiên";
            this.btnCancel.Click += new System.EventHandler(this.OnCancelClick);
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
            this.tlpStats.Padding = new System.Windows.Forms.Padding(16, 0, 16, 0);
            this.tlpStats.RowCount = 1;
            this.tlpStats.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpStats.Size = new System.Drawing.Size(960, 96);
            this.tlpStats.TabIndex = 3;
            //
            // stat cards
            //
            this.pnlStat1.Controls.Add(this.lblStat1Sub);
            this.pnlStat1.Controls.Add(this.lblStat1Value);
            this.pnlStat1.Controls.Add(this.lblStat1Caption);
            this.pnlStat1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat1.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            this.pnlStat1.Name = "pnlStat1";
            this.pnlStat1.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat1Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Caption.Name = "lblStat1Caption";
            this.lblStat1Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Caption.Values.Text = "TIẾN ĐỘ";
            this.lblStat1Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Value.Name = "lblStat1Value";
            this.lblStat1Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat1Value.Values.Text = "0%";
            this.lblStat1Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat1Sub.Name = "lblStat1Sub";
            this.lblStat1Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat1Sub.Values.Text = "—";
            this.pnlStat2.Controls.Add(this.lblStat2Sub);
            this.pnlStat2.Controls.Add(this.lblStat2Value);
            this.pnlStat2.Controls.Add(this.lblStat2Caption);
            this.pnlStat2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat2.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            this.pnlStat2.Name = "pnlStat2";
            this.pnlStat2.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat2Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat2Caption.Name = "lblStat2Caption";
            this.lblStat2Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat2Caption.Values.Text = "HÓA ĐƠN NHẬN ĐƯỢC";
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
            this.pnlStat3.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            this.pnlStat3.Name = "pnlStat3";
            this.pnlStat3.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat3Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Caption.Name = "lblStat3Caption";
            this.lblStat3Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat3Caption.Values.Text = "XML ĐÃ LƯU";
            this.lblStat3Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Value.Name = "lblStat3Value";
            this.lblStat3Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat3Value.Values.Text = "0";
            this.lblStat3Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat3Sub.Name = "lblStat3Sub";
            this.lblStat3Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat3Sub.Values.Text = "—";
            this.pnlStat4.Controls.Add(this.lblStat4Sub);
            this.pnlStat4.Controls.Add(this.lblStat4Value);
            this.pnlStat4.Controls.Add(this.lblStat4Caption);
            this.pnlStat4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStat4.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            this.pnlStat4.Name = "pnlStat4";
            this.pnlStat4.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.lblStat4Caption.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Caption.Name = "lblStat4Caption";
            this.lblStat4Caption.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat4Caption.Values.Text = "THỜI GIAN";
            this.lblStat4Value.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Value.Name = "lblStat4Value";
            this.lblStat4Value.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblStat4Value.Values.Text = "00:00:00";
            this.lblStat4Sub.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblStat4Sub.Name = "lblStat4Sub";
            this.lblStat4Sub.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblStat4Sub.Values.Text = "—";
            //
            // pnlOverall
            //
            this.pnlOverall.Controls.Add(this.progressTotal);
            this.pnlOverall.Controls.Add(this.lblCheckpoint);
            this.pnlOverall.Controls.Add(this.lblOverallTitle);
            this.pnlOverall.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlOverall.Name = "pnlOverall";
            this.pnlOverall.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlOverall.Padding = new System.Windows.Forms.Padding(12, 6, 12, 8);
            this.pnlOverall.Size = new System.Drawing.Size(960, 74);
            this.pnlOverall.TabIndex = 2;
            //
            // lblOverallTitle
            //
            this.lblOverallTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblOverallTitle.Name = "lblOverallTitle";
            this.lblOverallTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblOverallTitle.Values.Text = "Tiến độ tổng";
            //
            // lblCheckpoint
            //
            this.lblCheckpoint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCheckpoint.Name = "lblCheckpoint";
            this.lblCheckpoint.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblCheckpoint.Values.Text = "—";
            //
            // progressTotal
            //
            this.progressTotal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressTotal.Name = "progressTotal";
            this.progressTotal.TabIndex = 0;
            //
            // pnlMessage
            //
            this.pnlMessage.Controls.Add(this.lblMessage);
            this.pnlMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMessage.Name = "pnlMessage";
            this.pnlMessage.Padding = new System.Windows.Forms.Padding(16, 6, 16, 6);
            this.pnlMessage.Size = new System.Drawing.Size(960, 36);
            this.pnlMessage.TabIndex = 1;
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Text = "";
            //
            // pnlSegments
            //
            this.pnlSegments.Controls.Add(this.gridSegments);
            this.pnlSegments.Controls.Add(this.pnlSegmentsHeader);
            this.pnlSegments.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSegments.Name = "pnlSegments";
            this.pnlSegments.Padding = new System.Windows.Forms.Padding(16, 4, 16, 8);
            this.pnlSegments.TabIndex = 0;
            //
            // pnlSegmentsHeader
            //
            this.pnlSegmentsHeader.Controls.Add(this.btnRetryFailed);
            this.pnlSegmentsHeader.Controls.Add(this.lblSegmentsTitle);
            this.pnlSegmentsHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlSegmentsHeader.Name = "pnlSegmentsHeader";
            this.pnlSegmentsHeader.PanelBackStyle = ComponentFactory.Krypton.Toolkit.PaletteBackStyle.PanelAlternate;
            this.pnlSegmentsHeader.Padding = new System.Windows.Forms.Padding(12, 4, 8, 4);
            this.pnlSegmentsHeader.Size = new System.Drawing.Size(928, 40);
            //
            // lblSegmentsTitle
            //
            this.lblSegmentsTitle.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblSegmentsTitle.Name = "lblSegmentsTitle";
            this.lblSegmentsTitle.StateCommon.ShortText.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblSegmentsTitle.Values.Text = "Chi tiết theo tháng";
            //
            // btnRetryFailed
            //
            this.btnRetryFailed.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnRetryFailed.Enabled = false;
            this.btnRetryFailed.Name = "btnRetryFailed";
            this.btnRetryFailed.Size = new System.Drawing.Size(140, 32);
            this.btnRetryFailed.Values.Text = "Thử lại các lỗi";
            this.btnRetryFailed.Click += new System.EventHandler(this.OnRetryFailedClick);
            //
            // gridSegments
            //
            this.gridSegments.AllowUserToAddRows = false;
            this.gridSegments.AllowUserToDeleteRows = false;
            this.gridSegments.AllowUserToResizeRows = false;
            this.gridSegments.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridSegments.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colMonth, this.colRange, this.colState, this.colPages, this.colReceived, this.colSaved, this.colNote, this.colRetry});
            this.gridSegments.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridSegments.Name = "gridSegments";
            this.gridSegments.ReadOnly = true;
            this.gridSegments.RowHeadersVisible = false;
            this.gridSegments.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridSegments.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.OnCellPainting);
            this.gridSegments.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.OnGridCellContentClick);
            //
            // columns
            //
            rightStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.colMonth.FillWeight = 70F;
            this.colMonth.HeaderText = "Tháng";
            this.colMonth.Name = "colMonth";
            this.colRange.FillWeight = 80F;
            this.colRange.HeaderText = "Khoảng ngày";
            this.colRange.Name = "colRange";
            this.colState.FillWeight = 100F;
            this.colState.HeaderText = "Trạng thái";
            this.colState.Name = "colState";
            this.colPages.DefaultCellStyle = rightStyle;
            this.colPages.FillWeight = 50F;
            this.colPages.HeaderText = "Trang";
            this.colPages.Name = "colPages";
            this.colReceived.DefaultCellStyle = rightStyle;
            this.colReceived.FillWeight = 65F;
            this.colReceived.HeaderText = "Hóa đơn";
            this.colReceived.Name = "colReceived";
            this.colSaved.DefaultCellStyle = rightStyle;
            this.colSaved.FillWeight = 55F;
            this.colSaved.HeaderText = "XML";
            this.colSaved.Name = "colSaved";
            this.colNote.FillWeight = 170F;
            this.colNote.HeaderText = "Lỗi/Ghi chú";
            this.colNote.Name = "colNote";
            this.colRetry.FillWeight = 60F;
            this.colRetry.HeaderText = "";
            this.colRetry.Name = "colRetry";
            //
            // DownloadProgressControl
            //
            this.Controls.Add(this.pnlSegments);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.pnlOverall);
            this.Controls.Add(this.tlpStats);
            this.Controls.Add(this.pnlTitle);
            this.Name = "DownloadProgressControl";
            this.Size = new System.Drawing.Size(960, 640);
            ((System.ComponentModel.ISupportInitialize)(this.gridSegments)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlTitleActions;
        private System.Windows.Forms.TableLayoutPanel tlpActions;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnPause;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnResume;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCancel;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSubtitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblBreadcrumb;
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
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlOverall;
        private System.Windows.Forms.ProgressBar progressTotal;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblCheckpoint;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblOverallTitle;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlSegments;
        private ComponentFactory.Krypton.Toolkit.KryptonDataGridView gridSegments;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonth;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRange;
        private System.Windows.Forms.DataGridViewTextBoxColumn colState;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPages;
        private System.Windows.Forms.DataGridViewTextBoxColumn colReceived;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSaved;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNote;
        private System.Windows.Forms.DataGridViewButtonColumn colRetry;
        private ComponentFactory.Krypton.Toolkit.KryptonPanel pnlSegmentsHeader;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnRetryFailed;
        private ComponentFactory.Krypton.Toolkit.KryptonLabel lblSegmentsTitle;
    }
}
