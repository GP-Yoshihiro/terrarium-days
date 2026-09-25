using System;

namespace TerrariumDays.Core
{
    /// <summary>A date on the in-game calendar.</summary>
    public readonly struct GameDate
    {
        public readonly int Year;
        public readonly int Month;
        public readonly int Day;

        public GameDate(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public string ToDisplayText() => $"{Year}年{Month}月{Day}日";
    }

    /// <summary>
    /// Compressed game time: one real day is one game month (30 days), so one game day is
    /// 48 real minutes. Day 0 is 1 April 2026 at the colony's epoch.
    /// </summary>
    public sealed class GameCalendar
    {
        public const double RealMinutesPerGameDay = 48d;
        public const int DaysPerMonth = 30;
        public const int StartYear = 2026;
        public const int StartMonth = 4;

        public GameCalendar(DateTimeOffset epochUtc)
        {
            EpochUtc = epochUtc;
        }

        public DateTimeOffset EpochUtc { get; }

        public static TimeSpan RealTimeFor(double gameDays) => TimeSpan.FromMinutes(gameDays * RealMinutesPerGameDay);

        public static double GameDaysBetween(DateTimeOffset from, DateTimeOffset to) =>
            (to - from).TotalMinutes / RealMinutesPerGameDay;

        public double GameDaysAt(DateTimeOffset utc) => Math.Max(0d, GameDaysBetween(EpochUtc, utc));

        public int MonthIndexAt(DateTimeOffset utc) => (int)Math.Floor(GameDaysAt(utc) / DaysPerMonth);

        public GameDate DateAt(DateTimeOffset utc)
        {
            var days = GameDaysAt(utc);
            var monthIndex = (int)Math.Floor(days / DaysPerMonth);
            var monthsFromJanuary = StartMonth - 1 + monthIndex;
            var year = StartYear + monthsFromJanuary / 12;
            var month = monthsFromJanuary % 12 + 1;
            var day = (int)Math.Floor(days - monthIndex * DaysPerMonth) + 1;
            return new GameDate(year, month, day);
        }
    }
}
