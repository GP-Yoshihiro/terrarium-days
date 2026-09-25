using System;

namespace TerrariumDays.Core
{
    /// <summary>Real-time length of the shed cycle for a growth stage (game days → real time).</summary>
    public static class SheddingModel
    {
        public static TimeSpan IntervalFor(GrowthStage stage, CareTuning tuning) =>
            GameCalendar.RealTimeFor(stage == GrowthStage.Adult ? tuning.AdultShedIntervalGameDays : tuning.YoungShedIntervalGameDays);
    }
}
