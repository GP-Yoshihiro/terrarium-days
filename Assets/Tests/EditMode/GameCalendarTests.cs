using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class GameCalendarTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        [Test]
        public void TheGameStartsOnTheFirstOfAprilTwentyTwentySix()
        {
            var date = calendar.DateAt(Epoch);

            Assert.That((date.Year, date.Month, date.Day), Is.EqualTo((2026, 4, 1)));
            Assert.That(date.ToDisplayText(), Is.EqualTo("2026年4月1日"));
        }

        [Test]
        public void FortyEightRealMinutesAreOneGameDay()
        {
            Assert.That(calendar.GameDaysAt(Epoch.AddMinutes(48)), Is.EqualTo(1d).Within(1e-9));
            Assert.That(GameCalendar.RealTimeFor(3d), Is.EqualTo(TimeSpan.FromMinutes(144)));
        }

        [Test]
        public void OneRealDayIsOneGameMonth()
        {
            Assert.That(calendar.MonthIndexAt(Epoch.AddDays(1)), Is.EqualTo(1));
            var date = calendar.DateAt(Epoch.AddDays(1));
            Assert.That((date.Year, date.Month, date.Day), Is.EqualTo((2026, 5, 1)));
        }

        [Test]
        public void TheYearRollsOverAfterMarch()
        {
            var date = calendar.DateAt(Epoch.AddDays(9));

            Assert.That((date.Year, date.Month), Is.EqualTo((2027, 1)));
        }

        [Test]
        public void TimesBeforeTheEpochClampToTheStartDate()
        {
            Assert.That(calendar.GameDaysAt(Epoch.AddHours(-5)), Is.EqualTo(0d));
            Assert.That(calendar.MonthIndexAt(Epoch.AddHours(-5)), Is.EqualTo(0));
        }
    }
}
