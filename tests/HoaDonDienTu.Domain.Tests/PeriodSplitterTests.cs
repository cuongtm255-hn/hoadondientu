using System;
using System.Linq;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Domain.Tests
{
    [TestFixture]
    public class PeriodSplitterTests
    {
        private readonly PeriodSplitter _splitter = new PeriodSplitter();

        private static DateTime D(int y, int m, int d)
        {
            return new DateTime(y, m, d);
        }

        private void AssertInvariants(PeriodSpec spec, DateTime from, DateTime to)
        {
            var segments = _splitter.Split(spec);

            Assert.That(segments.First().Start, Is.EqualTo(from), "Hợp các đoạn bắt đầu đúng ngày đầu");
            Assert.That(segments.Last().End, Is.EqualTo(to), "Hợp các đoạn kết thúc đúng ngày cuối");
            for (var i = 0; i < segments.Count; i++)
            {
                Assert.That(segments[i].Start.Month, Is.EqualTo(segments[i].End.Month), "Không đoạn nào vượt ranh giới tháng");
                if (i > 0) Assert.That(segments[i].Start, Is.EqualTo(segments[i - 1].End.AddDays(1)), "Liền kề, không hở, không chồng");
            }
        }

        // ---- Tháng ----

        [Test]
        public void Month_IsOneSegment_FirstToLastDay()
        {
            var segments = _splitter.Split(PeriodSpec.ForMonth(2026, 4));

            Assert.That(segments, Is.EqualTo(new[] { new DateSegment(D(2026, 4, 1), D(2026, 4, 30)) }));
        }

        [TestCase(2024, 2, 29)]
        [TestCase(2028, 2, 29)]
        [TestCase(2026, 2, 28)]
        [TestCase(2100, 2, 28)]
        [TestCase(2026, 1, 31)]
        [TestCase(2026, 12, 31)]
        public void Month_LastDay_FollowsRealCalendar_IncludingLeapYears(int year, int month, int lastDay)
        {
            var segments = _splitter.Split(PeriodSpec.ForMonth(year, month));

            Assert.That(segments.Single().End.Day, Is.EqualTo(lastDay));
        }

        // ---- Quý ----

        [TestCase(1, 1, 1, 3, 31)]
        [TestCase(2, 4, 1, 6, 30)]
        [TestCase(3, 7, 1, 9, 30)]
        [TestCase(4, 10, 1, 12, 31)]
        public void Quarter_IsThreeMonthSegments_OnCalendarQuarters(int quarter, int firstMonth, int fromDay, int lastMonth, int toDay)
        {
            var segments = _splitter.Split(PeriodSpec.ForQuarter(2026, quarter));

            Assert.That(segments.Count, Is.EqualTo(3));
            Assert.That(segments.First().Start, Is.EqualTo(D(2026, firstMonth, fromDay)));
            Assert.That(segments.Last().End, Is.EqualTo(D(2026, lastMonth, toDay)));
            Assert.That(segments.Select(s => s.Month), Is.EqualTo(new[] { firstMonth, firstMonth + 1, firstMonth + 2 }));
        }

        [Test]
        public void Quarter1_LeapYear_FebruaryHas29Days()
        {
            var segments = _splitter.Split(PeriodSpec.ForQuarter(2024, 1));

            Assert.That(segments[1], Is.EqualTo(new DateSegment(D(2024, 2, 1), D(2024, 2, 29))));
        }

        // ---- Năm ----

        [Test]
        public void Year_Is12Segments_ForCommonYear()
        {
            var segments = _splitter.Split(PeriodSpec.ForYear(2026));

            Assert.That(segments.Count, Is.EqualTo(12));
            Assert.That(segments.Select(s => s.Month), Is.EqualTo(Enumerable.Range(1, 12)));
            Assert.That(segments[1].End, Is.EqualTo(D(2026, 2, 28)));
            AssertInvariants(PeriodSpec.ForYear(2026), D(2026, 1, 1), D(2026, 12, 31));
        }

        [TestCase(2024)]
        [TestCase(2028)]
        public void Year_LeapYear_FebruaryHas29Days(int year)
        {
            var segments = _splitter.Split(PeriodSpec.ForYear(year));

            Assert.That(segments.Count, Is.EqualTo(12));
            Assert.That(segments[1].End.Day, Is.EqualTo(29));
        }

        // ---- Tùy chọn ----

        [Test]
        public void Custom_AcrossMonths_AC03()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2026, 1, 15), D(2026, 3, 10)));

            Assert.That(segments, Is.EqualTo(new[]
            {
                new DateSegment(D(2026, 1, 15), D(2026, 1, 31)),
                new DateSegment(D(2026, 2, 1), D(2026, 2, 28)),
                new DateSegment(D(2026, 3, 1), D(2026, 3, 10))
            }));
        }

        [Test]
        public void Custom_SingleDay_IsOneOneDaySegment_BothEndsInclusive()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2026, 5, 9), D(2026, 5, 9)));

            Assert.That(segments.Single(), Is.EqualTo(new DateSegment(D(2026, 5, 9), D(2026, 5, 9))));
        }

        [Test]
        public void Custom_FirstAndLastDayOfMonth_IsExactlyOneFullMonth()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2026, 6, 1), D(2026, 6, 30)));

            Assert.That(segments.Count, Is.EqualTo(1));
        }

        [Test]
        public void Custom_LastDayToFirstDayOfNextMonth_IsTwoSegments()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2026, 1, 31), D(2026, 2, 1)));

            Assert.That(segments, Is.EqualTo(new[]
            {
                new DateSegment(D(2026, 1, 31), D(2026, 1, 31)),
                new DateSegment(D(2026, 2, 1), D(2026, 2, 1))
            }));
        }

        [Test]
        public void Custom_AcrossYearBoundary()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2025, 12, 15), D(2026, 2, 10)));

            Assert.That(segments.Select(s => s.ToString()), Is.EqualTo(new[]
            {
                "15/12/2025–31/12/2025", "01/01/2026–31/01/2026", "01/02/2026–10/02/2026"
            }));
        }

        [Test]
        public void Custom_IgnoresTimeOfDay()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(new DateTime(2026, 1, 15, 23, 59, 59), new DateTime(2026, 1, 20, 0, 0, 1)));

            Assert.That(segments.Single(), Is.EqualTo(new DateSegment(D(2026, 1, 15), D(2026, 1, 20))));
        }

        [Test]
        public void Custom_LeapDay_IsIncluded()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2024, 2, 28), D(2024, 3, 1)));

            Assert.That(segments[0].End, Is.EqualTo(D(2024, 2, 29)));
            Assert.That(segments[1].Start, Is.EqualTo(D(2024, 3, 1)));
        }

        [TestCase(2025, 1, 17, 2026, 3, 9)]
        [TestCase(2026, 3, 31, 2026, 4, 1)]
        [TestCase(2024, 2, 1, 2024, 12, 31)]
        [TestCase(2020, 1, 1, 2026, 12, 31)]
        public void Custom_Invariants_HoldForArbitraryRanges(int y1, int m1, int d1, int y2, int m2, int d2)
        {
            var from = D(y1, m1, d1);
            var to = D(y2, m2, d2);

            AssertInvariants(PeriodSpec.Custom(from, to), from, to);
        }

        [Test]
        public void Segments_AreStrictlyIncreasing()
        {
            var segments = _splitter.Split(PeriodSpec.Custom(D(2024, 11, 20), D(2026, 4, 3)));

            for (var i = 1; i < segments.Count; i++) Assert.That(segments[i].Start, Is.GreaterThan(segments[i - 1].End));
        }

        // ---- Không hợp lệ ----

        [Test]
        public void Custom_EndBeforeStart_IsRejected()
        {
            var result = _splitter.Validate(PeriodSpec.Custom(D(2026, 3, 10), D(2026, 3, 9)));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single(), Does.Contain("lớn hơn hoặc bằng"));
            Assert.Throws<ArgumentException>(() => _splitter.Split(PeriodSpec.Custom(D(2026, 3, 10), D(2026, 3, 9))));
        }

        [Test]
        public void Custom_MissingEnds_AreRejectedIndividually()
        {
            Assert.That(_splitter.Validate(PeriodSpec.Custom(null, D(2026, 1, 1))).Errors.Single(), Does.Contain("bắt đầu"));
            Assert.That(_splitter.Validate(PeriodSpec.Custom(D(2026, 1, 1), null)).Errors.Single(), Does.Contain("kết thúc"));
            Assert.That(_splitter.Validate(PeriodSpec.Custom(null, null)).Errors.Count, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(13)]
        [TestCase(-1)]
        public void Month_OutOfRange_IsRejected(int month)
        {
            Assert.That(_splitter.Validate(PeriodSpec.ForMonth(2026, month)).IsValid, Is.False);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Quarter_OutOfRange_IsRejected(int quarter)
        {
            Assert.That(_splitter.Validate(PeriodSpec.ForQuarter(2026, quarter)).IsValid, Is.False);
        }

        [TestCase(1999)]
        [TestCase(2101)]
        [TestCase(0)]
        public void Year_OutOfRange_IsRejected_ForEveryKind(int year)
        {
            Assert.That(_splitter.Validate(PeriodSpec.ForYear(year)).IsValid, Is.False);
            Assert.That(_splitter.Validate(PeriodSpec.ForMonth(year, 1)).IsValid, Is.False);
            Assert.That(_splitter.Validate(PeriodSpec.ForQuarter(year, 1)).IsValid, Is.False);
        }

        [Test]
        public void NullSpec_IsRejected()
        {
            Assert.That(_splitter.Validate(null).IsValid, Is.False);
        }

        [Test]
        public void VeryLongCustomRange_WarnsButIsStillValid()
        {
            var spec = PeriodSpec.Custom(D(2010, 1, 1), D(2026, 12, 31));

            var result = _splitter.Validate(spec);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings.Count, Is.EqualTo(1));
            Assert.That(_splitter.Split(spec).Count, Is.EqualTo(17 * 12));
        }

        [Test]
        public void TenYearRange_DoesNotWarn_ButCanBeTightened()
        {
            Assert.That(_splitter.Validate(PeriodSpec.Custom(D(2016, 1, 1), D(2026, 1, 1))).Warnings, Is.Empty);

            var strict = new PeriodSplitter { MaxCustomYears = 1 };
            Assert.That(strict.Validate(PeriodSpec.Custom(D(2024, 1, 1), D(2026, 1, 1))).Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void DateSegment_RejectsCrossMonthOrReversed()
        {
            Assert.Throws<ArgumentException>(() => new DateSegment(D(2026, 1, 31), D(2026, 2, 1)));
            Assert.Throws<ArgumentException>(() => new DateSegment(D(2026, 1, 10), D(2026, 1, 9)));
        }

        [Test]
        public void Split_DoesNotDependOnCurrentCulture()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");

                var segments = _splitter.Split(PeriodSpec.ForYear(2026));

                Assert.That(segments.Count, Is.EqualTo(12));
                Assert.That(segments[0].Start, Is.EqualTo(D(2026, 1, 1)));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = old;
            }
        }
    }
}
