#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Nguồn danh mục TTHC GIẢ, chỉ có trong bản Debug (CLAUDE.md). Mọi bản ghi ghi rõ "(mẫu)"; không gọi cổng thật.
    /// </summary>
    public sealed class MockTthcSource : ITthcSource
    {
        public const string Hint = "MÔ PHỎNG: dữ liệu mẫu, không phải danh mục thật của cổng.";

        private static readonly string[] Subjects =
        {
            "Đăng ký thuế", "Khai thuế giá trị gia tăng", "Hoàn thuế", "Miễn giảm thuế", "Đăng ký sử dụng hóa đơn điện tử",
            "Xác nhận nghĩa vụ thuế", "Gia hạn nộp thuế", "Cấp mã số thuế cá nhân", "Thay đổi thông tin đăng ký thuế", "Tra soát nộp thuế"
        };

        private static readonly string[] Levels = { "Toàn trình", "Một phần" };

        private readonly List<TthcSummary> _all;

        public MockTthcSource()
        {
            _all = new List<TthcSummary>();
            for (int i = 0; i < 35; i++)
            {
                _all.Add(new TthcSummary(
                    "9." + (100000 + i),
                    Subjects[i % Subjects.Length] + " (mẫu " + (i + 1) + ")",
                    Levels[i % Levels.Length],
                    "Cơ quan thuế (mẫu)"));
            }
        }

        public Task<TthcFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
        {
            var levels = new List<TthcOption> { new TthcOption("TT", "Toàn trình"), new TthcOption("MP", "Một phần") };
            var types = new List<TthcOption> { new TthcOption("1", "Loại A (mẫu)"), new TthcOption("2", "Loại B (mẫu)") };
            var decls = new List<TthcOption> { new TthcOption("01", "Tờ khai mẫu 01") };
            var taxpayers = new List<TthcOption> { new TthcOption("DN", "Doanh nghiệp (mẫu)"), new TthcOption("CN", "Cá nhân (mẫu)") };
            return Task.FromResult(new TthcFilterOptions(types, decls, levels, taxpayers));
        }

        public Task<TthcPage> SearchAsync(TthcQuery query, CancellationToken cancellationToken)
        {
            IEnumerable<TthcSummary> rows = _all;
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                string k = query.Keyword.Trim();
                rows = rows.Where(r => r.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 || r.Code.Contains(k));
            }
            if (query.Level == "TT") rows = rows.Where(r => r.Level == "Toàn trình");
            if (query.Level == "MP") rows = rows.Where(r => r.Level == "Một phần");

            var list = rows.ToList();
            int pageCount = list.Count == 0 ? 0 : (list.Count + TthcPage.DefaultPageSize - 1) / TthcPage.DefaultPageSize;
            int page = Math.Min(Math.Max(1, query.PageNumber), Math.Max(1, pageCount));
            var items = list.Skip((page - 1) * TthcPage.DefaultPageSize).Take(TthcPage.DefaultPageSize).ToList();
            return Task.FromResult(new TthcPage(items, list.Count, list.Count, page, pageCount));
        }

        public Task<TthcDetail> GetDetailAsync(string code, CancellationToken cancellationToken)
        {
            TthcSummary row = _all.FirstOrDefault(r => r.Code == code);
            if (row == null) throw new TthcServiceException(TthcFailure.NotFound);

            var sections = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Mã thủ tục", row.Code),
                new KeyValuePair<string, string>("Cấp thực hiện", "Cấp mẫu"),
                new KeyValuePair<string, string>("Trình tự thực hiện", "Bước 1: (mẫu) chuẩn bị hồ sơ." + Environment.NewLine + "Bước 2: (mẫu) nộp hồ sơ."),
                new KeyValuePair<string, string>("Cách thức thực hiện", "Trực tuyến (mẫu)"),
                new KeyValuePair<string, string>("Thành phần hồ sơ", "- Giấy tờ A (mẫu)" + Environment.NewLine + "- Giấy tờ B (mẫu)"),
                new KeyValuePair<string, string>("Đối tượng thực hiện", "Người nộp thuế (mẫu)"),
                new KeyValuePair<string, string>("Cơ quan thực hiện", row.Agency),
                new KeyValuePair<string, string>("Địa chỉ tiếp nhận hồ sơ", "Địa chỉ mẫu"),
                new KeyValuePair<string, string>("Kết quả thực hiện", "Thông báo (mẫu)"),
                new KeyValuePair<string, string>("Điều kiện thực hiện", "Không có (mẫu)"),
                new KeyValuePair<string, string>("Căn cứ pháp lý", "Văn bản mẫu số 00/0000")
            };
            return Task.FromResult(new TthcDetail(row.Code, row.Name, sections));
        }
    }
}
#endif
