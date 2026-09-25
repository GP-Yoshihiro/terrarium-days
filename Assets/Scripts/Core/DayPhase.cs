using System;

namespace TerrariumDays.Core
{
    /// <summary>Time of day on the player's local clock.</summary>
    public enum DayPhase
    {
        Morning,
        Day,
        Evening,
        Night
    }

    /// <summary>
    /// Local-time phases. Leopard geckos are crepuscular/nocturnal: they sleep through the
    /// day, wake around dusk and are active at night, so the pet's rhythm follows this.
    /// </summary>
    public static class DayPhaseClock
    {
        public static DayPhase PhaseAt(DateTime localTime, PetBehaviourTuning tuning)
        {
            var hour = localTime.Hour + localTime.Minute / 60d;

            if (hour >= tuning.NightStartHour || hour < tuning.MorningStartHour)
            {
                return DayPhase.Night;
            }

            if (hour >= tuning.EveningStartHour)
            {
                return DayPhase.Evening;
            }

            return hour >= tuning.DayStartHour ? DayPhase.Day : DayPhase.Morning;
        }

        /// <summary>Colour washed over the terrarium for the time of day (RGBA 0–1).</summary>
        public static void Tint(DayPhase phase, PetBehaviourTuning tuning, out float r, out float g, out float b, out float a)
        {
            switch (phase)
            {
                case DayPhase.Night:
                    r = 0.07f; g = 0.09f; b = 0.27f; a = tuning.NightDarkness;
                    break;
                case DayPhase.Evening:
                    r = 1f; g = 0.47f; b = 0.16f; a = tuning.EveningGlow;
                    break;
                case DayPhase.Morning:
                    r = 1f; g = 0.82f; b = 0.63f; a = tuning.MorningGlow;
                    break;
                default:
                    r = 1f; g = 1f; b = 1f; a = 0f;
                    break;
            }
        }

        public static string Label(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning:
                    return "朝";
                case DayPhase.Day:
                    return "昼";
                case DayPhase.Evening:
                    return "夕方";
                default:
                    return "夜";
            }
        }
    }
}
