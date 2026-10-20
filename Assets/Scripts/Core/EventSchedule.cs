using System;

namespace TerrariumDays.Core
{
    public enum EventKind
    {
        Small,
        Big
    }

    /// <summary>What one event action is used for (phase 6 spec §2.3). Appended values only.</summary>
    public enum EventActionKind
    {
        Serve,
        Browse
    }

    /// <summary>
    /// The monthly event calendar (§11, phase 6 spec §2.1): every month on the 15th, big in April,
    /// July, October and December. Event number n is the n-th game month (April 2026 = 0).
    /// </summary>
    public static class EventSchedule
    {
        public const int EventDayOfMonth = 15;
        public const int DeadlineDayOfMonth = 14;
        public const int OpenMinuteOfDay = 10 * 60;
        public const int CloseMinuteOfDay = 17 * 60;
        public const string SmallName = "レプタイル・マルシェ";
        public const string BigName = "ジャパン・レプタイルズ・フェスタ";

        public static int MonthOf(int number) => (GameCalendar.StartMonth - 1 + number) % 12 + 1;

        public static int YearOf(int number) => GameCalendar.StartYear + (GameCalendar.StartMonth - 1 + number) / 12;

        public static bool IsBig(int number)
        {
            var month = MonthOf(number);
            return month == 4 || month == 7 || month == 10 || month == 12;
        }

        public static EventKind KindOf(int number) => IsBig(number) ? EventKind.Big : EventKind.Small;

        public static string NameOf(int number)
        {
            switch (MonthOf(number))
            {
                case 4:
                    return BigName + "（春）";
                case 7:
                    return BigName + "（夏）";
                case 10:
                    return BigName + "（秋）";
                case 12:
                    return BigName + "（冬）";
                default:
                    return SmallName;
            }
        }

        public static GameDate DateOf(int number) => new GameDate(YearOf(number), MonthOf(number), EventDayOfMonth);

        public static GameDate DeadlineDateOf(int number) => new GameDate(YearOf(number), MonthOf(number), DeadlineDayOfMonth);

        /// <summary>Game days from the epoch to 0:00 on the event day (day 0 is the 1st).</summary>
        public static double StartGameDay(int number) => (double)number * GameCalendar.DaysPerMonth + (EventDayOfMonth - 1);

        public static DateTimeOffset StartUtc(GameCalendar calendar, int number) =>
            calendar.EpochUtc + GameCalendar.RealTimeFor(StartGameDay(number));

        /// <summary>0:00 on the day after the event (the "continue" notice is for events past this).</summary>
        public static DateTimeOffset EndUtc(GameCalendar calendar, int number) =>
            calendar.EpochUtc + GameCalendar.RealTimeFor(StartGameDay(number) + 1d);

        public static bool HasStarted(GameCalendar calendar, int number, DateTimeOffset nowUtc) => nowUtc >= StartUtc(calendar, number);

        public static double GameDaysUntilStart(GameCalendar calendar, int number, DateTimeOffset nowUtc) =>
            GameCalendar.GameDaysBetween(nowUtc, StartUtc(calendar, number));

        /// <summary>The first event whose day has not started yet: the only one that takes entries.</summary>
        public static int NextOpenNumber(GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var days = calendar.GameDaysAt(nowUtc);
            var first = EventDayOfMonth - 1;
            return days < first ? 0 : (int)Math.Floor((days - first) / GameCalendar.DaysPerMonth) + 1;
        }

        public static long BoothFee(EventKind kind) => kind == EventKind.Big ? 10000L : 3000L;

        public static int MaxListings(EventKind kind) => kind == EventKind.Big ? 10 : 5;

        public static int ActionCount(EventKind kind) => kind == EventKind.Big ? 8 : 5;

        /// <summary>The cap of the §10.2 sale chance.</summary>
        public static double SaleCap(EventKind kind) => kind == EventKind.Big ? 1.5d : 1.2d;

        /// <summary>The chance that one visited booth shows at least one rare animal (user, 2026-10-20).</summary>
        public static double RareChance(EventKind kind) => kind == EventKind.Big ? 0.25d : 0.15d;

        /// <summary>Action k covers an equal slice of 10:00–17:00 (only for the log's clock; it never changes results).</summary>
        public static int ActionWindowStart(int index, int actionCount) =>
            OpenMinuteOfDay + (CloseMinuteOfDay - OpenMinuteOfDay) * index / actionCount;

        public static int ActionWindowLength(int actionCount) => (CloseMinuteOfDay - OpenMinuteOfDay) / actionCount;
    }
}
