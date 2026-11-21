using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Form đăng nhập cổng Dịch vụ công (FR-DVC-AUTH-001): tên đăng nhập (điền sẵn MST, cho sửa), mật khẩu, ảnh CAPTCHA.
    /// Người dùng tự gõ mật khẩu và CAPTCHA; form không lưu chúng. Dựng bằng mã, không có Designer.
    /// </summary>
    public sealed class DvcLoginForm : KryptonForm
    {
        private readonly DvcLoginService _service;
        private readonly string _taxCode;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly KryptonTextBox _userName = new KryptonTextBox();
        private readonly KryptonTextBox _password = new KryptonTextBox();
        private readonly KryptonTextBox _captcha = new KryptonTextBox();
        private readonly PictureBox _captchaImage = new PictureBox();
        private readonly Label _status = new Label();
        private readonly KryptonButton _login = new KryptonButton();
        private readonly KryptonButton _refresh = new KryptonButton();
        private bool _busy;

        public DvcLoginForm(DvcLoginService service, string taxCode, string notice)
        {
            _service = service;
            _taxCode = taxCode ?? string.Empty;

            Text = "Đăng nhập cổng Dịch vụ công — Thuế điện tử (Doanh nghiệp)";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 330);
            AcceptButton = _login;

            AddLabel("Tên đăng nhập", 20, 18);
            _userName.SetBounds(20, 38, 420, 27);
            _userName.Text = _service.SuggestUserName(_taxCode);

            AddLabel("Mật khẩu", 20, 74);
            _password.SetBounds(20, 94, 420, 27);
            _password.UseSystemPasswordChar = true;

            AddLabel("Mã xác nhận", 20, 130);
            _captchaImage.SetBounds(20, 150, 140, 44);
            _captchaImage.SizeMode = PictureBoxSizeMode.Zoom;
            _captchaImage.BorderStyle = BorderStyle.FixedSingle;
            _refresh.SetBounds(166, 150, 100, 44);
            _refresh.Values.Text = "Đổi mã";
            UiIcons.Apply(_refresh, UiIcon.Refresh);
            _refresh.Click += async (s, e) => await LoadCaptchaAsync();
            _captcha.SetBounds(276, 158, 164, 27);

            _status.SetBounds(20, 204, 420, 56);
            _status.Text = notice ?? string.Empty;
            _status.ForeColor = Color.DimGray;

            _login.SetBounds(222, 276, 130, 36);
            _login.Values.Text = "Đăng nhập";
            UiIcons.Apply(_login, UiIcon.Login);
            _login.Click += async (s, e) => await LoginAsync();
            var cancel = new KryptonButton();
            cancel.SetBounds(360, 276, 80, 36);
            cancel.Values.Text = "Hủy";
            UiIcons.Apply(cancel, UiIcon.Close);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(_userName);
            Controls.Add(_password);
            Controls.Add(_captchaImage);
            Controls.Add(_refresh);
            Controls.Add(_captcha);
            Controls.Add(_status);
            Controls.Add(_login);
            Controls.Add(cancel);

            // Thứ tự Tab theo thứ tự nhập: tên đăng nhập, mật khẩu, mã xác nhận, Đổi mã, Đăng nhập, Hủy.
            _userName.TabIndex = 0;
            _password.TabIndex = 1;
            _captcha.TabIndex = 2;
            _refresh.TabIndex = 3;
            _login.TabIndex = 4;
            cancel.TabIndex = 5;

            Shown += async (s, e) =>
            {
                // Con trỏ vào ô đầu tiên còn trống theo thứ tự nhập: tên đăng nhập, rồi mật khẩu (mật khẩu không bao giờ được điền sẵn).
                (_userName.Text.Trim().Length == 0 ? (Control)_userName : _password).Focus();
                await LoadCaptchaAsync();
            };
            FormClosed += (s, e) => _cancellation.Cancel();
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y) });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _login.Enabled = !busy;
            _refresh.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private async System.Threading.Tasks.Task LoadCaptchaAsync()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                byte[] bytes = await _service.RefreshCaptchaAsync(_cancellation.Token);
                var old = _captchaImage.Image;
                using (var stream = new MemoryStream(bytes))
                {
                    _captchaImage.Image = new Bitmap(stream);
                }
                if (old != null) old.Dispose();
                _captcha.Clear();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Không lấy được mã xác nhận. Kiểm tra mạng rồi bấm Đổi mã, hoặc đăng nhập trên trang cổng.";
            }
            finally
            {
                if (!IsDisposed) SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task LoginAsync()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var result = await _service.LoginAsync(_taxCode, _userName.Text, _password.Text, _captcha.Text, _cancellation.Token);
                if (result.Success)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                _status.ForeColor = Color.Firebrick;
                _status.Text = DvcLoginMessages.For(result.Failure);
                _password.Clear();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                if (!IsDisposed) SetBusy(false);
            }

            if (!IsDisposed) await LoadCaptchaAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellation.Dispose();
                if (_captchaImage.Image != null) _captchaImage.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
