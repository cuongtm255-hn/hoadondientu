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

        /// <summary>Hồ sơ lưu tạm GIẢ: không CAPTCHA, ba dòng mẫu, cột do mô phỏng đặt (cột thật chưa kiểm chứng).</summary>
        private static HoSoPage SearchDraft(HoSoQuery query)
        {
            IList<string> columns = new[] { "Tên TTHC", "Tờ khai", "Ngày lưu", "Trạng thái" };
            var rows = new List<string[]>
            {
                new[] { "Thủ tục mẫu 1", "Tờ khai mẫu 01 (mô phỏng)", "05/11/2026", "Đã kiểm tra thông tin kê khai (mẫu)" },
                new[] { "Thủ tục mẫu 2", "Tờ khai mẫu 02 (mô phỏng)", "12/11/2026", "Chưa kiểm tra (mẫu)" },
                new[] { "Thủ tục mẫu 3", "Tờ khai mẫu 01 (mô phỏng)", "20/11/2026", "Chưa kiểm tra (mẫu)" }
            };
            if (!string.IsNullOrWhiteSpace(query.Declaration))
            {
                rows = rows.Where(r => r[1].IndexOf(query.Declaration.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            if (rows.Count == 0) return new HoSoPage(columns, null, 0, 1, 0, "Không có dữ liệu");
            return new HoSoPage(columns, rows.Select(r => new HoSoRow(r)).ToList(), rows.Count, 1, 1, null);
        }

        /// <summary>Phản ánh quyết toán GIẢ: không CAPTCHA, cột do mô phỏng đặt (cột thật chưa kiểm chứng); lọc theo năm.</summary>
        private static HoSoPage SearchFeedback(HoSoQuery query)
        {
            IList<string> columns = new[] { "MST tổ chức", "Năm quyết toán", "Nội dung phản ánh", "Ngày gửi", "Kênh gửi" };
            string channel = query.FeedbackOnETax ? "Thuế điện tử (mô phỏng)" : "Dịch vụ công (mô phỏng)";
            var rows = new List<string[]>
            {
                new[] { "0100000001 (mẫu)", "2025", "Phản ánh mẫu về số thuế (mô phỏng)", "10/11/2026", channel },
                new[] { "0100000001 (mẫu)", "2024", "Phản ánh mẫu về giảm trừ (mô phỏng)", "02/11/2026", channel }
            };
            if (!string.IsNullOrWhiteSpace(query.SettlementYear)) rows = rows.Where(r => r[1] == query.SettlementYear.Trim()).ToList();
            if (rows.Count == 0) return new HoSoPage(columns, null, 0, 1, 0, "Không có dữ liệu");
            return new HoSoPage(columns, rows.Select(r => new HoSoRow(r)).ToList(), rows.Count, 1, 1, null);
        }

        /// <summary>Thông báo LPTB GIẢ: không CAPTCHA, cột theo tiêu đề thật của cổng, dòng "(mẫu)"; lọc theo mã trạng thái.</summary>
        private static HoSoPage SearchLptb(HoSoQuery query)
        {
            IList<string> columns = new[] { "Mã giao dịch điện tử", "Mã số thuế", "Tên người nộp thuế", "Loại tài sản", "Số khung", "Số máy", "Số tiền nộp LPTB", "Trạng thái thông báo", "Trạng thái nộp tiền", "Thông báo LPTB", "Tình trạng giá", "Hướng dẫn" };
            var rows = new List<string[]>
            {
                new[] { "GD-LPTB-MAU-001", "0100000001", "Người nộp thuế mẫu", "Ô tô con (mẫu)", "KHUNG-MAU-001", "MAY-MAU-001", "12.000.000", "Chờ xác nhận", "Chưa nộp", "(mẫu)", "Có giá", "" },
                new[] { "GD-LPTB-MAU-002", "0100000001", "Người nộp thuế mẫu", "Xe máy (mẫu)", "KHUNG-MAU-002", "MAY-MAU-002", "1.500.000", "Đã xác nhận", "Đã nộp", "(mẫu)", "Có giá", "" }
            };
            if (!string.IsNullOrWhiteSpace(query.StatusCode))
            {
                string wanted = query.StatusCode.Trim() == "01" ? "Chờ xác nhận" : (query.StatusCode.Trim() == "02" ? "Đã xác nhận" : "?");
                rows = rows.Where(r => r[7] == wanted).ToList();
            }
            if (rows.Count == 0) return new HoSoPage(columns, null, 0, 1, 0, "Không có dữ liệu");
            return new HoSoPage(columns, rows.Select(r => new HoSoRow(r)).ToList(), rows.Count, 1, 1, null);
        }

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
            if (query.Source == HoSoSource.LuuTam) return Task.FromResult(SearchDraft(query));
            if (query.Source == HoSoSource.PhanAnh) return Task.FromResult(SearchFeedback(query));
            if (query.Source == HoSoSource.Lptb) return Task.FromResult(SearchLptb(query));

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
