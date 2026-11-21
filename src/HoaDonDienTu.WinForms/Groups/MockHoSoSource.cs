#if DEBUG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Nguồn hồ sơ đã nộp GIẢ, chỉ có trong bản Debug (CLAUDE.md). Dữ liệu ghi rõ "(mẫu)"; CAPTCHA do chính lớp này vẽ và chỉ dùng được MỘT lần
    /// (như cổng thật theo BR-DVC-03); không gọi cổng thật.
    /// </summary>
    public sealed class MockHoSoSource : IHoSoSource
    {
        public const string Hint = "MÔ PHỎNG: dữ liệu mẫu; nhập đúng mã trong ảnh (mã chỉ dùng một lần, tìm xong phải nhập mã mới).";

        private static readonly Random Rng = new Random();
        private string _captcha;
        private string _lastGood; // mã của lần tìm thành công gần nhất: đổi trang dùng lại được (giả định đã chốt, giống trang cổng)

        public Task<byte[]> GetCaptchaAsync(HoSoSource source, CancellationToken cancellationToken)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[5];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[Rng.Next(alphabet.Length)];
            _captcha = new string(chars);

            using (var bmp = new Bitmap(140, 44))
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Consolas", 20f, FontStyle.Bold | FontStyle.Italic))
            using (var small = new Font("Segoe UI", 7f))
            using (var stream = new MemoryStream())
            {
                g.Clear(Color.Honeydew);
                g.DrawString(_captcha, font, Brushes.DarkGreen, 10, 6);
                g.DrawString("MẪU", small, Brushes.Gray, 108, 28);
                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return Task.FromResult(stream.ToArray());
            }
        }

        public Task<HoSoPage> SearchAsync(HoSoQuery query, CancellationToken cancellationToken)
        {
            // Tìm mới: mã chỉ dùng một lần, đúng hay sai đều hết hiệu lực. Đổi trang (trang > 1): chấp nhận lại mã của lần tìm thành công trước.
            string expected = _captcha;
            _captcha = null;
            bool paging = query.PageNumber > 1 && _lastGood != null && string.Equals(query.CaptchaText, _lastGood, StringComparison.OrdinalIgnoreCase);
            bool fresh = expected != null && string.Equals(query.CaptchaText, expected, StringComparison.OrdinalIgnoreCase);
            if (!paging && !fresh)
            {
                _lastGood = null;
                throw new HoSoServiceException(HoSoFailure.WrongCaptcha);
            }
            _lastGood = query.CaptchaText;

            bool dvc = query.Source == HoSoSource.Dvc;
            IList<string> columns = dvc
                ? new[] { "Mã hồ sơ", "Tên TTHC", "Tờ khai", "Ngày nộp", "Trạng thái" }
                : new[] { "Mã giao dịch", "Tờ khai", "Ngày nộp", "Trạng thái" };

            DateTime start = dvc ? new DateTime(2026, 11, 1) : new DateTime(2025, 6, 1);
            var all = new List<KeyValuePair<DateTime, string[]>>();
            for (int i = 0; i < 23; i++)
            {
                DateTime day = start.AddDays(i);
                string code = (dvc ? "HS-MAU-" : "GD-MAU-") + (1000 + i);
                string status = i % 3 == 0 ? "Đã tiếp nhận (mẫu)" : (i % 3 == 1 ? "Đang xử lý (mẫu)" : "Hoàn thành (mẫu)");
                all.Add(new KeyValuePair<DateTime, string[]>(day, dvc
                    ? new[] { code, "Thủ tục mẫu " + (i % 5 + 1), "Tờ khai mẫu 01", day.ToString("dd/MM/yyyy"), status }
                    : new[] { code, "Tờ khai mẫu 01", day.ToString("dd/MM/yyyy"), status }));
            }

            IEnumerable<KeyValuePair<DateTime, string[]>> rows = all.Where(r => r.Key.Date >= query.FromDate.Date && r.Key.Date <= query.ToDate.Date);
            string codeFilter = dvc ? query.FileCode : query.TransactionCode;
            if (!string.IsNullOrWhiteSpace(codeFilter)) rows = rows.Where(r => r.Value[0].IndexOf(codeFilter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            if (dvc && !string.IsNullOrWhiteSpace(query.ProcedureName)) rows = rows.Where(r => r.Value[1].IndexOf(query.ProcedureName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            var list = rows.Select(r => r.Value).ToList();
            if (list.Count == 0)
            {
                string message = dvc ? "Không có dữ liệu" : "Không có dữ liệu thỏa mãn điều kiện tìm kiếm!";
                return Task.FromResult(new HoSoPage(columns, null, 0, 1, 0, message));
            }

            int pageCount = (list.Count + HoSoPage.DefaultPageSize - 1) / HoSoPage.DefaultPageSize;
            int page = Math.Min(Math.Max(1, query.PageNumber), pageCount);
            var items = list.Skip((page - 1) * HoSoPage.DefaultPageSize).Take(HoSoPage.DefaultPageSize).Select(c => new HoSoRow(c)).ToList();
            return Task.FromResult(new HoSoPage(columns, items, list.Count, page, pageCount, null));
        }
    }
}
#endif
