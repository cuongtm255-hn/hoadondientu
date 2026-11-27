using System;
using System.Collections.Generic;
using System.Linq;
using HoaDonDienTu.Domain.PublicService;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.PublicService
{
    /// <summary>
    /// Dựng <see cref="HoSoPage"/> từ bảng do CHÍNH TRÌNH DUYỆT của WebView đọc (DOMParser trong trang), thay vì tự phân tích HTML: trình duyệt
    /// xử lý đúng các quirk của HTML cổng (form trong ô, thẻ không đóng...). JSON gồm <c>heads</c>, <c>rows</c> (mỗi ô là văn bản), <c>sum</c>
    /// (trang, số trang, tổng bản ghi đọc ở dòng tổng) và <c>active</c> (số trang hiện tại).
    /// </summary>
    public static class HoSoDomTable
    {
        private static readonly string[] SkippedColumns = { "STT", "Thao tác" };

        public static HoSoPage ToPage(JObject table, JArray summary, string pageInput)
        {
            return ToPage(table, summary, pageInput, false);
        }

        /// <param name="lenient">
        /// Cho nguồn CHƯA kiểm chứng (hồ sơ lưu tạm): thiếu dòng tổng thì coi là một trang với số dòng đang có, thay vì báo cổng đổi giao diện.
        /// </param>
        public static HoSoPage ToPage(JObject table, JArray summary, string pageInput, bool lenient)
        {
            if (table == null || table.Type != JTokenType.Object) throw new HoSoParseException("Không có bảng.");

            List<string> heads = Strings(table["heads"]);
            if (heads.Count == 0) throw new HoSoParseException("Bảng hồ sơ không có tiêu đề cột.");

            var keep = new List<int>();
            for (int i = 0; i < heads.Count; i++)
            {
                if (!SkippedColumns.Contains(heads[i], StringComparer.OrdinalIgnoreCase)) keep.Add(i);
            }

            var rows = new List<HoSoRow>();
            JArray rawRows = table["rows"] as JArray ?? new JArray();
            int number = 0;
            foreach (JToken rowToken in rawRows)
            {
                number++;
                List<string> cells = Strings(rowToken);
                string diag = "Dòng hồ sơ " + number + " có " + cells.Count + " ô, tiêu đề có " + heads.Count + " cột.";
                // Ô cuối rỗng có thể bị cổng bỏ khỏi HTML: bù ô rỗng ở cuối. Thừa ô hoặc thiếu nhiều hơn một thì coi là cấu trúc lạ.
                if (cells.Count > heads.Count || heads.Count - cells.Count > 1) throw new HoSoParseException(diag, diag);
                while (cells.Count < heads.Count) cells.Add(string.Empty);
                rows.Add(new HoSoRow(keep.Select(i => cells[i]).ToList()));
            }

            int pages = 0, total = 0, page = 1;
            if (lenient && (summary == null || summary.Count < 3))
            {
                return new HoSoPage(keep.Select(i => heads[i]).ToList(), rows, rows.Count, 1, 1, null);
            }
            if (summary == null || summary.Count < 3) throw new HoSoParseException("Không thấy dòng tổng số bản ghi.", "Không thấy dòng tổng số bản ghi (" + rows.Count + " dòng, " + heads.Count + " cột).");
            pages = Int(summary[1]);
            total = Int(summary[2]);
            int parsed;
            if (int.TryParse(pageInput, out parsed) && parsed > 0) page = parsed;
            else if (summary[0] != null && Int(summary[0]) > 0) page = Int(summary[0]);

            IList<string> columns = keep.Select(i => heads[i]).ToList();
            return new HoSoPage(columns, rows, total, page, pages, null);
        }

        private static List<string> Strings(JToken token)
        {
            var list = new List<string>();
            JArray array = token as JArray;
            if (array == null) return list;
            foreach (JToken t in array) list.Add(t == null || t.Type == JTokenType.Null ? string.Empty : t.ToString().Trim());
            return list;
        }

        private static int Int(JToken token)
        {
            int value;
            return token != null && int.TryParse(token.ToString(), out value) ? value : 0;
        }
    }
}
