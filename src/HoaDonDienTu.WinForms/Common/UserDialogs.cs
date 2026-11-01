using System.Text;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Common;

namespace HoaDonDienTu.WinForms.Common
{
    /// <summary>Hiển thị lỗi theo cấu trúc: việc gì, hướng xử lý, mã tham chiếu để tra log.</summary>
    public sealed class UserErrorPresenter : IUserErrorPresenter
    {
        public void Show(UserFacingError error)
        {
            if (error == null) return;

            var text = new StringBuilder(error.Message);
            if (!string.IsNullOrEmpty(error.SuggestedAction))
            {
                text.AppendLine().AppendLine().Append(error.SuggestedAction);
            }

            if (!string.IsNullOrEmpty(error.CorrelationId))
            {
                text.AppendLine().AppendLine().Append("Mã tham chiếu: ").Append(error.CorrelationId);
            }

            var icon = error.Category == ErrorCategory.Cancelled ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
            KryptonMessageBox.Show(Form.ActiveForm, text.ToString(), error.Title, MessageBoxButtons.OK, icon);
        }
    }

    public sealed class ConfirmationService : IConfirmationService
    {
        public bool Confirm(string title, string message)
        {
            return KryptonMessageBox.Show(Form.ActiveForm, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                == DialogResult.Yes;
        }
    }
}
