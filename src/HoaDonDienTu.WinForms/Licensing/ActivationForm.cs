using System;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Licensing;

namespace HoaDonDienTu.WinForms.Licensing
{
    /// <summary>Hỏi người dùng có cho vào ứng dụng không khi hết thời gian dùng thử mà chưa kích hoạt.</summary>
    public interface IActivationPrompt
    {
        /// <summary>true khi người dùng đã kích hoạt thành công; false nếu đóng hộp thoại hoặc thoát.</summary>
        bool Prompt(MachineLicenseService license, LicenseSnapshot snapshot);
    }

    /// <summary>
    /// Cổng vào ứng dụng: còn dùng thử hoặc đã kích hoạt thì vào thẳng; hết thời gian dùng thử mà chưa kích hoạt thì hiện hộp thoại kích hoạt,
    /// không kích hoạt thành công thì không vào được ứng dụng.
    /// </summary>
    public static class ActivationGate
    {
        public static bool Allow(MachineLicenseService license, IActivationPrompt prompt)
        {
            if (license == null) throw new ArgumentNullException("license");
            if (prompt == null) throw new ArgumentNullException("prompt");

            var snapshot = license.Evaluate();
            if (snapshot.CanUse) return true;

            if (!prompt.Prompt(license, snapshot)) return false;

            // Quyết định theo trạng thái thật sau hộp thoại, không tin giá trị trả về của hộp thoại.
            return license.Evaluate().CanUse;
        }
    }

    public sealed class ActivationDialogPrompt : IActivationPrompt
    {
        public bool Prompt(MachineLicenseService license, LicenseSnapshot snapshot)
        {
            using (var form = new ActivationForm(license, snapshot))
            {
                return form.ShowDialog() == DialogResult.OK;
            }
        }
    }

    /// <summary>
    /// Hộp thoại kích hoạt bản quyền: mã máy (sao chép để gửi cho đơn vị phát hành), ô nhập mã kích hoạt, nút Kích hoạt và Thoát.
    /// Dựng bằng mã, không có Designer. Lỗi mã được báo ngay trong hộp thoại và hộp thoại giữ nguyên để nhập lại.
    /// </summary>
    public sealed class ActivationForm : KryptonForm
    {
        private readonly MachineLicenseService _license;
        private readonly LicenseSnapshot _snapshot;
        private KryptonTextBox _machineCode;
        private KryptonTextBox _key;
        private Label _status;
        private KryptonButton _activate;
        private KryptonButton _copy;
        private KryptonButton _exit;

        public ActivationForm(MachineLicenseService license, LicenseSnapshot snapshot)
        {
            if (license == null) throw new ArgumentNullException("license");
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            _license = license;
            _snapshot = snapshot;
            InitializeUi();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
        }

        /// <summary>Thử kích hoạt bằng mã đang nhập; trả về true và đóng hộp thoại (OK) nếu thành công.</summary>
        public bool TryActivate()
        {
            var key = (_key.Text ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                ShowStatus("Vui lòng nhập mã kích hoạt.", true);
                return false;
            }

            try
            {
                _license.ActivateAsync(key, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (LicenseException ex)
            {
                ShowStatus(ex.Message, true);
                return false;
            }

            ShowStatus("Kích hoạt bản quyền thành công.", false);
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }

        private void ShowStatus(string text, bool isError)
        {
            _status.Text = text ?? string.Empty;
            _status.ForeColor = isError ? Color.FromArgb(170, 40, 30) : Color.FromArgb(30, 110, 60);
        }

        private void OnActivateClick(object sender, EventArgs e)
        {
            TryActivate();
        }

        private void OnCopyClick(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(_machineCode.Text);
                ShowStatus("Đã sao chép mã máy. Gửi mã này cho đơn vị phát hành để nhận mã kích hoạt.", false);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                ShowStatus("Không sao chép được (bảng nhớ tạm đang bị chiếm). Hãy chọn mã máy và nhấn Ctrl+C.", true);
            }
        }

        private void OnKeyTextChanged(object sender, EventArgs e)
        {
            _activate.Enabled = _key.Text.Trim().Length > 0;
        }

        private void InitializeUi()
        {
            Text = "Kích hoạt bản quyền";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(700, 420);

            var title = new KryptonLabel { Location = new Point(20, 14), AutoSize = true };
            title.Values.Text = _snapshot.StoredKeyRejected ? "Mã kích hoạt không còn hợp lệ" : "Đã hết 30 ngày dùng thử";
            title.StateCommon.ShortText.Font = new Font("Segoe UI", 14F, FontStyle.Bold);

            var intro = new Label
            {
                Location = new Point(22, 54),
                Size = new Size(656, 62),
                Font = new Font("Segoe UI", 9.5F),
                BackColor = Color.Transparent,
                Text = _snapshot.StoredKeyRejected
                    ? "Mã kích hoạt đã lưu không đúng với máy này (" + _snapshot.StoredKeyProblem + "). Gửi mã máy bên dưới cho đơn vị phát hành để nhận mã mới. Dữ liệu của bạn vẫn được giữ nguyên."
                    : "Để tiếp tục sử dụng, hãy gửi mã máy bên dưới cho đơn vị phát hành để nhận mã kích hoạt riêng cho máy này, rồi dán vào ô Mã kích hoạt. Dữ liệu của bạn vẫn được giữ nguyên và sẽ dùng lại được ngay sau khi kích hoạt."
            };

            var machineLabel = new KryptonLabel { Location = new Point(20, 128), AutoSize = true };
            machineLabel.Values.Text = "Mã máy";
            _machineCode = new KryptonTextBox { Location = new Point(22, 154), Size = new Size(520, 27), ReadOnly = true, Name = "txtMachineCode", Text = _snapshot.MachineCode };
            _copy = new KryptonButton { Location = new Point(552, 152), Size = new Size(126, 30), Name = "btnCopyMachine" };
            _copy.Values.Text = "Sao chép";
            _copy.Click += OnCopyClick;

            var keyLabel = new KryptonLabel { Location = new Point(20, 196), AutoSize = true };
            keyLabel.Values.Text = "Mã kích hoạt";
            _key = new KryptonTextBox { Location = new Point(22, 222), Size = new Size(656, 84), Multiline = true, Name = "txtActivationKey", ScrollBars = ScrollBars.Vertical };
            _key.TextChanged += OnKeyTextChanged;

            _status = new Label { Location = new Point(22, 316), Size = new Size(656, 40), Font = new Font("Segoe UI", 9.5F), BackColor = Color.Transparent, Name = "lblStatus" };

            _exit = new KryptonButton { Location = new Point(552, 366), Size = new Size(126, 36), Name = "btnExit", DialogResult = DialogResult.Cancel };
            _exit.Values.Text = "Thoát";
            _activate = new KryptonButton { Location = new Point(414, 366), Size = new Size(130, 36), Name = "btnActivate", Enabled = false };
            _activate.Values.Text = "Kích hoạt";
            _activate.Click += OnActivateClick;

            Controls.AddRange(new Control[] { title, intro, machineLabel, _machineCode, _copy, keyLabel, _key, _status, _activate, _exit });
            AcceptButton = _activate;
            CancelButton = _exit;
        }
    }
}
