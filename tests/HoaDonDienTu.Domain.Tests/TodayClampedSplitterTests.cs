using System;
using System.Linq;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Domain.Tests
{
    [TestFixture]
    public class TodayClampedSplitterTests
    {
        private static TodayClampedSplitter At(DateTime today)
        {
            return new TodayClampedSplitter(new PeriodSplitter(), () => today);
        }

        [Test]
        public void WholeYear_InTheMiddleOfNovember_StopsAtToday()
        {
            var segments = At(new DateTime(2026, 11, 3, 15, 0, 0)).Split(PeriodSpec.ForYear(2026));

            Assert.That(segments, Has.Count.EqualTo(11));
            Assert.That(segments.First().Start, Is.EqualTo(new DateTime(2026, 1, 1)));
            Assert.That(segments.Last().Start, Is.EqualTo(new DateTime(2026, 11, 1)));
            Assert.That(segments.Last().End, Is.EqualTo(new DateTime(2026, 11, 3)), "tháng đang chạy bị cắt tới hôm nay");
            Assert.That(segments.Take(10).All(s => s.End.Day == DateTime.DaysInMonth(s.End.Year, s.End.Month)), Is.True);
        }

        [Test]
        public void PastPeriod_IsUntouched()
        {
            var inner = new PeriodSplitter().Split(PeriodSpec.ForYear(2025));

            var clamped = At(new DateTime(2026, 11, 3)).Split(PeriodSpec.ForYear(2025));

            Assert.That(clamped.Select(s => s.Start), Is.EqualTo(inner.Select(s => s.Start)));
            Assert.That(clamped.Select(s => s.End), Is.EqualTo(inner.Select(s => s.End)));
        }

        [Test]
        public void LastDayOfTheMonth_KeepsTheWholeMonth()
        {
            var segments = At(new DateTime(2026, 11, 30)).Split(PeriodSpec.ForMonth(2026, 11));

            Assert.That(segments.Single().End, Is.EqualTo(new DateTime(2026, 11, 30)));
        }

        [Test]
        public void FutureQuarter_KeepsOnlyTheMonthsThatStarted()
        {
            var segments = At(new DateTime(2026, 11, 3)).Split(PeriodSpec.ForQuarter(2026, 4));

            Assert.That(segments.Select(s => s.Start.Month), Is.EqualTo(new[] { 10, 11 }));
            Assert.That(segments.Last().End, Is.EqualTo(new DateTime(2026, 11, 3)));
        }

        [Test]
        public void CustomRangeEndingInTheFuture_IsClamped()
        {
            var spec = PeriodSpec.Custom(new DateTime(2026, 10, 20), new DateTime(2027, 1, 31));

            var segments = At(new DateTime(2026, 11, 3)).Split(spec);

            Assert.That(segments.First().Start, Is.EqualTo(new DateTime(2026, 10, 20)));
            Assert.That(segments.Last().End, Is.EqualTo(new DateTime(2026, 11, 3)));
        }

        [Test]
        public void PeriodEntirelyInTheFuture_IsInvalid_WithAVietnameseReason()
        {
            var splitter = At(new DateTime(2026, 11, 3));

            var month = splitter.Validate(PeriodSpec.ForMonth(2026, 12));
            var year = splitter.Validate(PeriodSpec.ForYear(2027));

            Assert.That(month.IsValid, Is.False);
            Assert.That(month.Errors.Single(), Does.Contain("tương lai"));
            Assert.That(year.IsValid, Is.False);
            Assert.That(splitter.Split(PeriodSpec.ForMonth(2026, 12)), Is.Empty);
        }

        [Test]
        public void PeriodPartlyInTheFuture_IsValid_WithAWarningNamingTheCutOffDate()
        {
            var result = At(new DateTime(2026, 11, 3)).Validate(PeriodSpec.ForYear(2026));

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings.Single(), Does.Contain("03/11/2026"));
        }

        [Test]
        public void PastPeriod_HasNoWarning()
        {
            Assert.That(At(new DateTime(2026, 11, 3)).Validate(PeriodSpec.ForYear(2025)).Warnings, Is.Empty);
        }

        [Test]
        public void InnerErrors_AreKept()
        {
            var result = At(new DateTime(2026, 11, 3)).Validate(PeriodSpec.ForMonth(2026, 13));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("Tháng phải từ 1 đến 12"));
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new TodayClampedSplitter(null, () => DateTime.Today));
            Assert.Throws<ArgumentNullException>(() => new TodayClampedSplitter(new PeriodSplitter(), null));
        }
    }
}
