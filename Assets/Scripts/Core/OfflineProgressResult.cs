using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Outcome of one OfflineProgressCalculator.Apply call.
    /// </summary>
    public readonly struct OfflineProgressResult
    {
        public OfflineProgressResult(TimeSpan appliedElapsed, GrowthStage? newGrowthStage, int shedCount = 0, bool sexRevealed = false)
        {
            AppliedElapsed = appliedElapsed;
            NewGrowthStage = newGrowthStage;
            ShedCount = shedCount;
            SexRevealed = sexRevealed;
        }

        /// <summary>How many times the pet shed during the applied time (periodic or on growing).</summary>
        public int ShedCount { get; }

        public TimeSpan AppliedElapsed { get; }

        public GrowthStage? NewGrowthStage { get; }

        /// <summary>True when this call revealed the pet's sex (a juvenile-or-older shed).</summary>
        public bool SexRevealed { get; }
    }
}
