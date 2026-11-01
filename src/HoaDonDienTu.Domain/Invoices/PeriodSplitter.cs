using System;
using System.Collections.Generic;

namespace HoaDonDienTu.Domain.Invoices
{
    public sealed class PeriodValidationResult
    {
        public PeriodValidationResult(IList<string> errors, IList<string> warnings)
        {
            Errors = new List<string>(errors ?? new List<string>()).AsReadOnly();
            Warnings = new List<string>(warnings ?? new List<string>()).AsReadOnly();
        }

        public IList<string> Errors { get; private set; }

        /// <summary>Kỳ vẫn dùng được nhưng nên báo cho người dùng (ví dụ khoảng quá dài).</summary>
        public IList<string> Warnings { get; private set; }

        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }

    public interface IPeriodSplitter
    {
        PeriodValidationResult Validate(PeriodSpec spec);

        /// <summary>Các đoạn tăng dần theo thời gian, mỗi đoạn nằm trong một tháng.</summary>
        /// <exception cref="ArgumentException">Kỳ không hợp lệ.</exception>
        IList<DateSegment> Split(PeriodSpec spec);
    }

    /// <summary>
    /// Chia kỳ thành các đoạn không vượt ranh giới tháng (cổng Thuế giới hạn một tháng mỗi lần tra cứu).
    /// Chỉ dùng phần ngày (<see cref="DateTime.Date"/>), không phụ thuộc múi giờ hay UI (NFR-MNT-001).
    /// </summary>
    public sealed class PeriodSplitter : IPeriodSplitter
    {
        public const int MinYear = 2000;
        public const int MaxYear = 2100;

        public PeriodSplitter()
        {
            MaxCustomYears = 10;
        }

        /// <summary>Khoảng tùy chọn dài hơn số năm này chỉ bị cảnh báo, không bị chặn.</summary>
        public int MaxCustomYears { get; set; }

        public PeriodValidationResult Validate(PeriodSpec spec)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (spec == null)
            {
                errors.Add("Chưa chọn kỳ.");
                return new PeriodValidationResult(errors, warnings);
            }

            switch (spec.Kind)
            {
                case PeriodKind.Month:
                    CheckYear(spec.Year, errors);
                    if (!spec.Month.HasValue || spec.Month < 1 || spec.Month > 12) errors.Add("Tháng phải từ 1 đến 12.");
                    break;

                case PeriodKind.Quarter:
                    CheckYear(spec.Year, errors);
                    if (!spec.Quarter.HasValue || spec.Quarter < 1 || spec.Quarter > 4) errors.Add("Quý phải từ 1 đến 4.");
                    break;

                case PeriodKind.Year:
                    CheckYear(spec.Year, errors);
                    break;

                case PeriodKind.Custom:
                    ValidateCustom(spec, errors, warnings);
                    break;

                default:
                    errors.Add("Kiểu kỳ không hợp lệ.");
                    break;
            }

            return new PeriodValidationResult(errors, warnings);
        }

        public IList<DateSegment> Split(PeriodSpec spec)
        {
            var validation = Validate(spec);
            if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));

            DateTime from, to;
            Normalize(spec, out from, out to);

            var segments = new List<DateSegment>();
            var cursor = from;
            while (cursor <= to)
            {
                var endOfMonth = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
                var end = endOfMonth < to ? endOfMonth : to;
                segments.Add(new DateSegment(cursor, end));
                cursor = end.AddDays(1);
            }

            return segments;
        }

        private static void Normalize(PeriodSpec spec, out DateTime from, out DateTime to)
        {
            switch (spec.Kind)
            {
                case PeriodKind.Month:
                    from = new DateTime(spec.Year, spec.Month.Value, 1);
                    to = new DateTime(spec.Year, spec.Month.Value, DateTime.DaysInMonth(spec.Year, spec.Month.Value));
                    break;

                case PeriodKind.Quarter:
                    var firstMonth = (spec.Quarter.Value - 1) * 3 + 1;
                    from = new DateTime(spec.Year, firstMonth, 1);
                    to = new DateTime(spec.Year, firstMonth + 2, DateTime.DaysInMonth(spec.Year, firstMonth + 2));
                    break;

                case PeriodKind.Year:
                    from = new DateTime(spec.Year, 1, 1);
                    to = new DateTime(spec.Year, 12, 31);
                    break;

                default:
                    from = spec.From.Value;
                    to = spec.To.Value;
                    break;
            }
        }

        private void ValidateCustom(PeriodSpec spec, List<string> errors, List<string> warnings)
        {
            if (!spec.From.HasValue) errors.Add("Chưa nhập ngày bắt đầu.");
            if (!spec.To.HasValue) errors.Add("Chưa nhập ngày kết thúc.");
            if (!spec.From.HasValue || !spec.To.HasValue) return;

            var from = spec.From.Value;
            var to = spec.To.Value;
            if (to < from)
            {
                errors.Add("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
                return;
            }

            if (from.Year < MinYear || to.Year > MaxYear)
            {
                errors.Add("Ngày phải nằm trong khoảng năm " + MinYear + "–" + MaxYear + ".");
                return;
            }

            if (MaxCustomYears > 0 && to > from.AddYears(MaxCustomYears))
            {
                warnings.Add("Khoảng thời gian dài hơn " + MaxCustomYears + " năm sẽ tạo rất nhiều đoạn tải.");
            }
        }

        private static void CheckYear(int year, List<string> errors)
        {
            if (year < MinYear || year > MaxYear) errors.Add("Năm phải từ " + MinYear + " đến " + MaxYear + ".");
        }
    }
}
