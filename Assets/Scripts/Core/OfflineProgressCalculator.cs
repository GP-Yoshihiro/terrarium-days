using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Deterministic elapsed-time state update, capped and stepped per
    /// Terrarium_Days_仕様書.md section 7. Weight, stage-up and sheds run on game time.
    /// No MonoBehaviour dependency.
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
            var sexRevealed = false;

            for (var i = 0; i < stepCount; i++)
            {
                var stepEndUtc = previousUtc + TimeSpan.FromTicks(step.Ticks * (i + 1));
                var refusing = AppetiteModel.IsRefusingFood(state, stepEndUtc, tuning);
                ApplyStep(state, refusing);

                if (refusing)
                {
                    state.WeightGrams -= tuning.FastingWeightLossPerGameDay * step.TotalMinutes / GameCalendar.RealMinutesPerGameDay;
                }

                var shedNow = stepEndUtc >= state.NextShedAtUtc;
                if (!state.StageUpDueAtUtc.HasValue && GrowthModel.MeetsNextStage(state, stepEndUtc, tuning))
                {
                    // Pre-growth fast first; the stage goes up (with a shed) when it ends.
                    state.StageUpDueAtUtc = stepEndUtc + GameCalendar.RealTimeFor(
                        tuning.PreGrowthFastGameDays * PersonalityTraits.FastMultiplier(state.Personality));
                }
                else if (state.StageUpDueAtUtc.HasValue && stepEndUtc >= state.StageUpDueAtUtc.Value)
                {
                    state.Stage = GrowthModel.NextStage(state.Stage) ?? state.Stage;
                    state.StageUpDueAtUtc = null;
                    shedNow = true;
                }

                if (shedNow)
                {
                    state.LastShedAtUtc = stepEndUtc;
                    state.NextShedAtUtc = stepEndUtc + SheddingModel.IntervalFor(state.Stage, tuning);
                    sheds++;

                    if (state.Stage != GrowthStage.Baby && !state.SexRevealed)
                    {
                        state.SexRevealed = true;
                        sexRevealed = true;
                    }
                }
            }

            var appliedElapsed = TimeSpan.FromMinutes(stepCount * tuning.OfflineProgressStepMinutes);
            var stageAfter = state.GrowthStage;

            return new OfflineProgressResult(appliedElapsed, stageAfter != stageBefore ? stageAfter : (GrowthStage?)null, sheds, sexRevealed);
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
            }
            else if (anyLow)
            {
                state.Health -= tuning.HealthDecayPerHour * stepHours;
            }
        }
    }
}
