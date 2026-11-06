using System;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Startup;

namespace HoaDonDienTu.WinForms.Shell
{
    /// <summary>Hộp thoại bốn lựa chọn khi đóng ứng dụng lúc đang có tác vụ tải (DD-01 mục 17.2).</summary>
    public partial class ShutdownPromptForm : KryptonForm
    {
        public ShutdownPromptForm(string companyName)
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
            lblMessage.Text = "Đang có tác vụ tải hóa đơn của " + companyName + ".\r\n"
                + "Chọn cách xử lý trước khi thoát. Tạm dừng sẽ ghi lại tiến độ để tiếp tục lần sau.";
            Choice = ShutdownChoice.StayOpen;
        }

        public ShutdownChoice Choice { get; private set; }

        private void OnWaitClick(object sender, EventArgs e)
        {
            Complete(ShutdownChoice.WaitForCompletion);
        }

        private void OnPauseClick(object sender, EventArgs e)
        {
            Complete(ShutdownChoice.PauseAndExit);
        }

        private void OnCancelJobClick(object sender, EventArgs e)
        {
            Complete(ShutdownChoice.CancelJobAndExit);
        }

        private void OnStayClick(object sender, EventArgs e)
        {
            Complete(ShutdownChoice.StayOpen);
        }

        private void Complete(ShutdownChoice choice)
        {
            Choice = choice;
            DialogResult = DialogResult.OK;
        }
    }

    public sealed class ShutdownPrompt : IShutdownPrompt
    {
        public ShutdownChoice Ask(string companyName)
        {
            using (var form = new ShutdownPromptForm(companyName))
            {
                form.ShowDialog(Form.ActiveForm);
                // Đóng hộp thoại bằng nút X được hiểu là ở lại.
                return form.Choice;
            }
        }
    }
}
