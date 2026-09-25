using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Outcome of one OfflineProgressCalculator.Apply call.
    /// </summary>
    public readonly struct OfflineProgressResult
    {
        public OfflineProgressResult(TimeSpan appliedElapsed, GrowthStage? newGrowthStage, int shedCount = 0)
        {
            AppliedElapsed = appliedElapsed;
            NewGrowthStage = newGrowthStage;
            ShedCount = shedCount;
        }

        /// <summary>How many times the pet shed during the applied time (periodic or on growing).</summary>
        public int ShedCount { get; }

        public TimeSpan AppliedElapsed { get; }

        public GrowthStage? NewGrowthStage { get; }
    }
}
