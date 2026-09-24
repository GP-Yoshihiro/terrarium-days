using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// UTC clock access and the debug time multiplier. The multiplier only ever scales
    /// debug-driven real-time progression / simulated elapsed time (仕様書 section 7.4);
    /// it does not apply to normal play or offline-resume progress.
    /// </summary>
    public sealed class TimeService
    {
        private readonly Func<DateTimeOffset> nowProvider;

        public TimeService() : this(() => DateTimeOffset.UtcNow)
        {
        }

        public TimeService(Func<DateTimeOffset> nowProvider)
        {
            this.nowProvider = nowProvider;
        }

        public double TimeMultiplier { get; set; } = 1d;

        public DateTimeOffset UtcNow()
        {
            return nowProvider();
        }

        public TimeSpan ScaleElapsed(TimeSpan realElapsed)
        {
            return TimeSpan.FromTicks((long)(realElapsed.Ticks * TimeMultiplier));
        }
    }
}
