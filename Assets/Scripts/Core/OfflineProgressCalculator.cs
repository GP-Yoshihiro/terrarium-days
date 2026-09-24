using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Deterministic elapsed-time state update, capped and stepped per
    /// Terrarium_Days_仕様書.md section 7. No MonoBehaviour dependency.
    /// </summary>
    public sealed class OfflineProgressCalculator
    {
        private readonly CareTuning tuning;

        public OfflineProgressCalculator(CareTuning tuning)
        {
            this.tuning = tuning;
        }

        public OfflineProgressResult Apply(PetState state, DateTimeOffset previousUtc, DateTimeOffset currentUtc)
        {
            var rawElapsed = currentUtc - previousUtc;
            if (rawElapsed <= TimeSpan.Zero)
            {
                return new OfflineProgressResult(TimeSpan.Zero, null);
            }

            var maxElapsed = TimeSpan.FromHours(tuning.MaxOfflineProgressHours);
            var cappedElapsed = rawElapsed > maxElapsed ? maxElapsed : rawElapsed;

            var stepCount = (int)Math.Floor(cappedElapsed.TotalMinutes / tuning.OfflineProgressStepMinutes);
            var stageBefore = state.GrowthStage;

            for (var step = 0; step < stepCount; step++)
            {
                ApplyStep(state);
            }

            var appliedElapsed = TimeSpan.FromMinutes(stepCount * tuning.OfflineProgressStepMinutes);
            var stageAfter = state.GrowthStage;

            return new OfflineProgressResult(appliedElapsed, stageAfter != stageBefore ? stageAfter : (GrowthStage?)null);
        }

        private void ApplyStep(PetState state)
        {
            var stepHours = tuning.OfflineProgressStepMinutes / 60d;

            state.Hunger -= tuning.HungerDecayPerHour * stepHours;
            state.Hydration -= tuning.HydrationDecayPerHour * stepHours;
            state.Cleanliness -= tuning.CleanlinessDecayPerHour * stepHours;

            var allHealthy = state.Hunger >= tuning.HealthyCareThreshold
                && state.Hydration >= tuning.HealthyCareThreshold
                && state.Cleanliness >= tuning.HealthyCareThreshold;

            var anyLow = state.Hunger < tuning.LowCareThreshold
                || state.Hydration < tuning.LowCareThreshold
                || state.Cleanliness < tuning.LowCareThreshold;

            if (allHealthy)
            {
                state.Health += tuning.HealthRecoveryPerHour * stepHours;
                state.Growth += tuning.GrowthPerHour * stepHours;
            }
            else if (anyLow)
            {
                state.Health -= tuning.HealthDecayPerHour * stepHours;
            }
        }
    }
}
