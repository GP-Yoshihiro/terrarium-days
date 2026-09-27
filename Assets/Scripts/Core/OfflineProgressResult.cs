using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Outcome of one OfflineProgressCalculator.Apply call.
    /// </summary>
    public readonly struct OfflineProgressResult
    {
        public OfflineProgressResult(TimeSpan appliedElapsed, GrowthStage? newGrowthStage, int shedCount = 0, bool sexRevealed = false,
            bool becameWeak = false, bool recoveredFromWeak = false)
        {
            AppliedElapsed = appliedElapsed;
            NewGrowthStage = newGrowthStage;
            ShedCount = shedCount;
            SexRevealed = sexRevealed;
            BecameWeak = becameWeak;
            RecoveredFromWeak = recoveredFromWeak;
        }

        /// <summary>How many times the pet shed during the applied time (periodic or on growing).</summary>
        public int ShedCount { get; }

        public TimeSpan AppliedElapsed { get; }

        public GrowthStage? NewGrowthStage { get; }

        /// <summary>True when this call revealed the pet's sex (a juvenile-or-older shed).</summary>
        public bool SexRevealed { get; }

        /// <summary>The animal was not weak before this call and is weak after it (§5.5).</summary>
        public bool BecameWeak { get; }

        /// <summary>The animal was weak before this call and has recovered (§5.5).</summary>
        public bool RecoveredFromWeak { get; }
    }
}
