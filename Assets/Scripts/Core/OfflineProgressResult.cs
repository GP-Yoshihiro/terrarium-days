using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Outcome of one OfflineProgressCalculator.Apply call.
    /// </summary>
    public readonly struct OfflineProgressResult
    {
        public OfflineProgressResult(TimeSpan appliedElapsed, GrowthStage? newGrowthStage)
        {
            AppliedElapsed = appliedElapsed;
            NewGrowthStage = newGrowthStage;
        }

        public TimeSpan AppliedElapsed { get; }

        public GrowthStage? NewGrowthStage { get; }
    }
}
