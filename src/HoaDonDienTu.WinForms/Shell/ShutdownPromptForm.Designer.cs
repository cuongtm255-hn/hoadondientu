namespace HoaDonDienTu.WinForms.Shell
{
    partial class ShutdownPromptForm
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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblMessage = new ComponentFactory.Krypton.Toolkit.KryptonWrapLabel();
            this.btnWait = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnPause = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnCancelJob = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.btnStay = new ComponentFactory.Krypton.Toolkit.KryptonButton();
            this.SuspendLayout();
            //
            // lblMessage
            //
            this.lblMessage.AutoSize = false;
            this.lblMessage.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblMessage.Location = new System.Drawing.Point(20, 18);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(440, 90);
            this.lblMessage.Text = "";
            //
            // btnWait
            //
            this.btnWait.Location = new System.Drawing.Point(20, 120);
            this.btnWait.Name = "btnWait";
            this.btnWait.Size = new System.Drawing.Size(210, 34);
            this.btnWait.TabIndex = 0;
            this.btnWait.Values.Text = "Chờ hoàn tất rồi thoát";
            this.btnWait.Click += new System.EventHandler(this.OnWaitClick);
            //
            // btnPause
            //
            this.btnPause.Location = new System.Drawing.Point(250, 120);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(210, 34);
            this.btnPause.TabIndex = 1;
            this.btnPause.Values.Text = "Tạm dừng và thoát";
            this.btnPause.Click += new System.EventHandler(this.OnPauseClick);
            //
            // btnCancelJob
            //
            this.btnCancelJob.Location = new System.Drawing.Point(20, 164);
            this.btnCancelJob.Name = "btnCancelJob";
            this.btnCancelJob.Size = new System.Drawing.Size(210, 34);
            this.btnCancelJob.TabIndex = 2;
            this.btnCancelJob.Values.Text = "Hủy tác vụ và thoát";
            this.btnCancelJob.Click += new System.EventHandler(this.OnCancelJobClick);
            //
            // btnStay
            //
            this.btnStay.Location = new System.Drawing.Point(250, 164);
            this.btnStay.Name = "btnStay";
            this.btnStay.Size = new System.Drawing.Size(210, 34);
            this.btnStay.TabIndex = 3;
            this.btnStay.Values.Text = "Ở lại ứng dụng";
            this.btnStay.Click += new System.EventHandler(this.OnStayClick);
            //
            // ShutdownPromptForm
            //
            this.ClientSize = new System.Drawing.Size(480, 218);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.btnWait);
            this.Controls.Add(this.btnPause);
            this.Controls.Add(this.btnCancelJob);
            this.Controls.Add(this.btnStay);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ShutdownPromptForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Đang có tác vụ tải";
            this.ResumeLayout(false);
        }

        #endregion

        private ComponentFactory.Krypton.Toolkit.KryptonWrapLabel lblMessage;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnWait;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnPause;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnCancelJob;
        private ComponentFactory.Krypton.Toolkit.KryptonButton btnStay;
    }
}
