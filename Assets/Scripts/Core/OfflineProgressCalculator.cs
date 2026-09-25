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
            var step = TimeSpan.FromMinutes(tuning.OfflineProgressStepMinutes);
            var stageBefore = state.GrowthStage;
            var sheds = 0;

            for (var i = 0; i < stepCount; i++)
            {
                var stepEndUtc = previousUtc + TimeSpan.FromTicks(step.Ticks * (i + 1));
                var stageAtStepStart = state.GrowthStage;
                ApplyStep(state, AppetiteModel.IsRefusingFood(state, stepEndUtc, tuning));

                // Growing into a new stage sheds the old skin; otherwise shed on schedule.
                if (state.GrowthStage != stageAtStepStart || stepEndUtc >= state.NextShedAtUtc)
                {
                    state.LastShedAtUtc = stepEndUtc;
                    state.NextShedAtUtc = stepEndUtc.AddDays(tuning.ShedIntervalDays);
                    sheds++;
                }
            }

            var appliedElapsed = TimeSpan.FromMinutes(stepCount * tuning.OfflineProgressStepMinutes);
            var stageAfter = state.GrowthStage;

            return new OfflineProgressResult(appliedElapsed, stageAfter != stageBefore ? stageAfter : (GrowthStage?)null, sheds);
        }

        private void ApplyStep(PetState state, bool refusingFood)
        {
            var stepHours = tuning.OfflineProgressStepMinutes / 60d;

            // A fasting gecko slows down: hunger falls slower and does not count against it.
            var hungerDecay = tuning.HungerDecayPerHour * (refusingFood ? tuning.AnorexiaHungerDecayMultiplier : 1d);
            state.Hunger -= hungerDecay * stepHours;
            state.Hydration -= tuning.HydrationDecayPerHour * stepHours;
            state.Cleanliness -= tuning.CleanlinessDecayPerHour * stepHours;

            var hungerOk = refusingFood || state.Hunger >= tuning.HealthyCareThreshold;
            var hungerLow = !refusingFood && state.Hunger < tuning.LowCareThreshold;

            var allHealthy = hungerOk
                && state.Hydration >= tuning.HealthyCareThreshold
                && state.Cleanliness >= tuning.HealthyCareThreshold;

            var anyLow = hungerLow
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
