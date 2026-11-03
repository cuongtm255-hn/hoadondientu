using System;
using System.Collections.Generic;
using System.Linq;

namespace HoaDonDienTu.Domain.Invoices
{
    /// <summary>
    /// Bọc một <see cref="IPeriodSplitter"/> để không tải phần của kỳ nằm trong tương lai: đoạn bắt đầu sau hôm nay bị bỏ, đoạn đang chạy bị cắt tới hôm nay
    /// (ví dụ chọn cả năm 2026 vào tháng 11 thì chỉ còn tháng 1 đến 3/11). Hóa đơn lập sau đó được tải ở lần sau; khóa chống trùng giữ cho dữ liệu không nhân đôi.
    /// </summary>
    public sealed class TodayClampedSplitter : IPeriodSplitter
    {
        private readonly IPeriodSplitter _inner;
        private readonly Func<DateTime> _today;

        public TodayClampedSplitter(IPeriodSplitter inner, Func<DateTime> today)
        {
            if (inner == null) throw new ArgumentNullException("inner");
            if (today == null) throw new ArgumentNullException("today");
            _inner = inner;
            _today = today;
        }

        public PeriodValidationResult Validate(PeriodSpec spec)
        {
            var result = _inner.Validate(spec);
            if (!result.IsValid) return result;

            var all = _inner.Split(spec);
            var kept = Clamp(all);
            var errors = new List<string>(result.Errors);
            var warnings = new List<string>(result.Warnings);
            if (kept.Count == 0)
            {
                errors.Add("Kỳ đã chọn nằm hoàn toàn trong tương lai nên chưa có hóa đơn để tải.");
            }
            else if (kept.Count != all.Count || !kept[kept.Count - 1].End.Equals(all[all.Count - 1].End))
            {
                warnings.Add("Kỳ có phần chưa tới: chỉ tải đến hết ngày " + _today().Date.ToString("dd/MM/yyyy") + ".");
            }

            return new PeriodValidationResult(errors, warnings);
        }

        public IList<DateSegment> Split(PeriodSpec spec)
        {
            return Clamp(_inner.Split(spec));
        }

        private IList<DateSegment> Clamp(IList<DateSegment> segments)
        {
            var today = _today().Date;
            return segments
                .Where(s => s.Start <= today)
                .Select(s => s.End <= today ? s : new DateSegment(s.Start, today))
                .ToList();
        }
    }
}
