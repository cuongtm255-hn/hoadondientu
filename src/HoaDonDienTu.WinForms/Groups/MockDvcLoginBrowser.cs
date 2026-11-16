#if DEBUG
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Trình duyệt đăng nhập DVC GIẢ, chỉ có trong bản Debug (CLAUDE.md: mock không được với tới từ Release). Không gọi cổng thật.
    /// CAPTCHA là ảnh do chính lớp này vẽ; mật khẩu "sai" / "khoa" / "tab" mô phỏng các lỗi tương ứng, còn lại thành công.
    /// </summary>
    public sealed class MockDvcLoginBrowser : IDvcLoginBrowser
    {
        public const string Hint = "MÔ PHỎNG: nhập đúng mã trong ảnh; mật khẩu bất kỳ là thành công; \"sai\", \"khoa\", \"tab\" mô phỏng lỗi.";

        private static readonly Random Rng = new Random();
        private string _captcha = string.Empty;

        public Task<byte[]> GetCaptchaAsync(CancellationToken cancellationToken)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[5];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[Rng.Next(alphabet.Length)];
            _captcha = new string(chars);

            using (var bmp = new Bitmap(140, 44))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Consolas", 20f, FontStyle.Bold | FontStyle.Italic))
            using (var stream = new MemoryStream())
            {
                g.Clear(Color.Lavender);
                g.DrawString(_captcha, font, Brushes.DarkSlateBlue, 10, 6);
                g.DrawString("MẪU", new Font("Segoe UI", 7f), Brushes.Gray, 108, 28);
                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return Task.FromResult(stream.ToArray());
            }
        }

        public Task<DvcLoginResult> SubmitAsync(DvcCredentials credentials, string captcha, CancellationToken cancellationToken)
        {
            if (!string.Equals(captcha, _captcha, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(DvcLoginResult.Fail(DvcLoginFailure.WrongCaptcha));

            switch (credentials.Password)
            {
                case "sai": return Task.FromResult(DvcLoginResult.Fail(DvcLoginFailure.WrongPassword));
                case "khoa": return Task.FromResult(DvcLoginResult.Fail(DvcLoginFailure.Locked));
                case "tab": return Task.FromResult(DvcLoginResult.Fail(DvcLoginFailure.AnotherTab));
            }
            return Task.FromResult(DvcLoginResult.Ok("CÔNG TY MẪU (mô phỏng)"));
        }

        public Task LogoutAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
#endif
