using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventScheduleTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        [Test]
        public void NumberZero_IsTheSpringFestivalInApril2026()
        {
            Assert.That(EventSchedule.MonthOf(0), Is.EqualTo(4));
            Assert.That(EventSchedule.YearOf(0), Is.EqualTo(2026));
            Assert.That(EventSchedule.KindOf(0), Is.EqualTo(EventKind.Big));
            Assert.That(EventSchedule.NameOf(0), Is.EqualTo("ジャパン・レプタイルズ・フェスタ（春）"));
        }

        [TestCase(1, 5, 2026, EventKind.Small, "レプタイル・マルシェ")]
        [TestCase(3, 7, 2026, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（夏）")]
        [TestCase(6, 10, 2026, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（秋）")]
        [TestCase(8, 12, 2026, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（冬）")]
        [TestCase(9, 1, 2027, EventKind.Small, "レプタイル・マルシェ")]
        [TestCase(12, 4, 2027, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（春）")]
        public void Months_KindsAndNames(int number, int month, int year, EventKind kind, string name)
        {
            Assert.That(EventSchedule.MonthOf(number), Is.EqualTo(month));
            Assert.That(EventSchedule.YearOf(number), Is.EqualTo(year));
            Assert.That(EventSchedule.KindOf(number), Is.EqualTo(kind));
            Assert.That(EventSchedule.NameOf(number), Is.EqualTo(name));
        }

        [Test]
        public void EachYear_HasFourBigEvents()
        {
            var big = 0;
            for (var n = 0; n < 12; n++)
            {
                if (EventSchedule.IsBig(n))
                {
                    big++;
                }
            }

            Assert.That(big, Is.EqualTo(4));
        }

        [Test]
        public void DateAndDeadline_AreThe15thAndThe14th()
        {
            Assert.That(EventSchedule.DateOf(1).ToDisplayText(), Is.EqualTo("2026年5月15日"));
            Assert.That(EventSchedule.DeadlineDateOf(1).ToDisplayText(), Is.EqualTo("2026年5月14日"));
            Assert.That(EventSchedule.DateOf(9).ToDisplayText(), Is.EqualTo("2027年1月15日"));
        }

        [Test]
        public void StartUtc_IsMidnightOfThe15th()
        {
            Assert.That(EventSchedule.StartUtc(calendar, 0), Is.EqualTo(Epoch + TimeSpan.FromMinutes(14 * 48)));
            Assert.That(calendar.DateAt(EventSchedule.StartUtc(calendar, 1)).ToDisplayText(), Is.EqualTo("2026年5月15日"));
            Assert.That(calendar.DateAt(EventSchedule.StartUtc(calendar, 1) - TimeSpan.FromSeconds(1)).ToDisplayText(), Is.EqualTo("2026年5月14日"));
            Assert.That(EventSchedule.EndUtc(calendar, 1) - EventSchedule.StartUtc(calendar, 1), Is.EqualTo(TimeSpan.FromMinutes(48)));
        }

        [Test]
        public void NextOpenNumber_IsTheNextEventThatHasNotStarted()
        {
            var second = TimeSpan.FromSeconds(1);
            Assert.That(EventSchedule.NextOpenNumber(calendar, Epoch), Is.EqualTo(0));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 0) - second), Is.EqualTo(0));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 0)), Is.EqualTo(1));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 1) - second), Is.EqualTo(1));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 1)), Is.EqualTo(2));
        }

        [Test]
        public void HasStartedAndDaysUntilStart()
        {
            var start = EventSchedule.StartUtc(calendar, 2);
            Assert.That(EventSchedule.HasStarted(calendar, 2, start - TimeSpan.FromSeconds(1)), Is.False);
            Assert.That(EventSchedule.HasStarted(calendar, 2, start), Is.True);
            Assert.That(EventSchedule.GameDaysUntilStart(calendar, 2, start - GameCalendar.RealTimeFor(3)), Is.EqualTo(3d).Within(1e-9));
        }

        [Test]
        public void NumbersPerKind()
        {
            Assert.That(EventSchedule.BoothFee(EventKind.Small), Is.EqualTo(3000));
            Assert.That(EventSchedule.BoothFee(EventKind.Big), Is.EqualTo(10000));
            Assert.That(EventSchedule.MaxListings(EventKind.Small), Is.EqualTo(5));
            Assert.That(EventSchedule.MaxListings(EventKind.Big), Is.EqualTo(10));
            Assert.That(EventSchedule.ActionCount(EventKind.Small), Is.EqualTo(5));
            Assert.That(EventSchedule.ActionCount(EventKind.Big), Is.EqualTo(8));
            Assert.That(EventSchedule.SaleCap(EventKind.Small), Is.EqualTo(1.2d));
            Assert.That(EventSchedule.SaleCap(EventKind.Big), Is.EqualTo(1.5d));
            Assert.That(EventSchedule.RareChance(EventKind.Small), Is.EqualTo(0.15d));
            Assert.That(EventSchedule.RareChance(EventKind.Big), Is.EqualTo(0.25d));
        }

        [Test]
        public void ActionWindows_SplitTenToFiveEvenly()
        {
            Assert.That(EventSchedule.ActionWindowStart(0, 5), Is.EqualTo(600));
            Assert.That(EventSchedule.ActionWindowStart(4, 5), Is.EqualTo(936));
            Assert.That(EventSchedule.ActionWindowLength(5), Is.EqualTo(84));
            Assert.That(EventSchedule.ActionWindowStart(7, 8) + EventSchedule.ActionWindowLength(8), Is.LessThanOrEqualTo(1020));
        }
    }
}
