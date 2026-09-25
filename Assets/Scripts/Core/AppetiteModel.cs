using System;

namespace TerrariumDays.Core
{
    /// <summary>Whether the pet will eat, and if not, why (拒食の理由).</summary>
    public enum AppetiteState
    {
        Normal,
        /// <summary>The days before a shed: skin dulls and the pet refuses food.</summary>
        PreShed,
        /// <summary>Just before the growth gauge fills and the pet moves up a stage.</summary>
        PreGrowth
    }

    /// <summary>
    /// Periodic food refusal of a leopard gecko, derived only from the pet state and the
    /// time, so offline progress and the live screen always agree.
    /// </summary>
    public static class AppetiteModel
    {
        public static AppetiteState Evaluate(PetState state, DateTimeOffset nowUtc, CareTuning tuning)
        {
            if (nowUtc >= state.NextShedAtUtc - GameCalendar.RealTimeFor(tuning.PreShedGameDays))
            {
                return AppetiteState.PreShed;
            }

            if (state.StageUpDueAtUtc.HasValue && nowUtc < state.StageUpDueAtUtc.Value)
            {
                return AppetiteState.PreGrowth;
            }

            return AppetiteState.Normal;
        }

        public static bool IsRefusingFood(PetState state, DateTimeOffset nowUtc, CareTuning tuning)
        {
            return Evaluate(state, nowUtc, tuning) != AppetiteState.Normal;
        }
    }
}
