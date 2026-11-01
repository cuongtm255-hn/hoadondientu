using System;

namespace HoaDonDienTu.Domain.Invoices
{
    public enum PeriodKind
    {
        Month,
        Quarter,
        Year,
        Custom
    }

    /// <summary>
    /// Kỳ người dùng chọn (tháng, quý, năm hoặc khoảng tùy chọn). Giữ nguyên giá trị nhập, kể cả không hợp lệ;
    /// <see cref="IPeriodSplitter.Validate"/> quyết định kỳ có dùng được hay không.
    /// </summary>
    public sealed class PeriodSpec
    {
        private PeriodSpec(PeriodKind kind, int year, int? month, int? quarter, DateTime? from, DateTime? to)
        {
            Kind = kind;
            Year = year;
            Month = month;
            Quarter = quarter;
            From = from.HasValue ? from.Value.Date : (DateTime?)null;
            To = to.HasValue ? to.Value.Date : (DateTime?)null;
        }

        public PeriodKind Kind { get; private set; }
        public int Year { get; private set; }

        /// <summary>Có giá trị khi <see cref="Kind"/> = Month.</summary>
        public int? Month { get; private set; }

        /// <summary>Có giá trị khi <see cref="Kind"/> = Quarter.</summary>
        public int? Quarter { get; private set; }

        /// <summary>Ngày bắt đầu (gồm) khi <see cref="Kind"/> = Custom.</summary>
        public DateTime? From { get; private set; }

        /// <summary>Ngày kết thúc (gồm) khi <see cref="Kind"/> = Custom.</summary>
        public DateTime? To { get; private set; }

        public static PeriodSpec ForMonth(int year, int month)
        {
            return new PeriodSpec(PeriodKind.Month, year, month, null, null, null);
        }

        public static PeriodSpec ForQuarter(int year, int quarter)
        {
            return new PeriodSpec(PeriodKind.Quarter, year, null, quarter, null, null);
        }

        public static PeriodSpec ForYear(int year)
        {
            return new PeriodSpec(PeriodKind.Year, year, null, null, null, null);
        }

        public static PeriodSpec Custom(DateTime? from, DateTime? to)
        {
            return new PeriodSpec(PeriodKind.Custom, from.HasValue ? from.Value.Year : 0, null, null, from, to);
        }
    }

    /// <summary>Một đoạn tra cứu nằm trọn trong một tháng; cả hai đầu đều được tính (BR-002, BR-003).</summary>
    public sealed class DateSegment : IEquatable<DateSegment>
    {
        public DateSegment(DateTime start, DateTime end)
        {
            start = start.Date;
            end = end.Date;
            if (end < start) throw new ArgumentException("Ngày kết thúc nhỏ hơn ngày bắt đầu.");
            if (start.Year != end.Year || start.Month != end.Month) throw new ArgumentException("Đoạn không được vượt ranh giới tháng.");
            Start = start;
            End = end;
        }

        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }

        public int Year
        {
            get { return Start.Year; }
        }

        public int Month
        {
            get { return Start.Month; }
        }

        public bool Equals(DateSegment other)
        {
            return other != null && Start == other.Start && End == other.End;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DateSegment);
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() * 31 + End.GetHashCode();
        }

        public override string ToString()
        {
            return Start.ToString("dd/MM/yyyy") + "–" + End.ToString("dd/MM/yyyy");
        }
    }
}
