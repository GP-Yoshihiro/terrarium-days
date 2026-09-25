using System;
using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>What the pet decided to do after a rest.</summary>
    public enum PetActivity
    {
        Rest,
        Walk,
        Sleep,
        Yawn,
        TailWag
    }

    /// <summary>
    /// One decision of the pet's idle/wander loop.
    /// </summary>
    public readonly struct PetIntent
    {
        public readonly PetActivity Activity;
        public readonly float TargetX;
        public readonly float TargetDepth;
        public readonly float Seconds;

        public PetIntent(PetActivity activity, float targetX, float targetDepth, float seconds)
        {
            Activity = activity;
            TargetX = targetX;
            TargetDepth = targetDepth;
            Seconds = seconds;
        }

        public bool Walk => Activity == PetActivity.Walk;
    }

    /// <summary>
    /// Decides where the pet wanders next, how long it rests, and when it sleeps, yawns or
    /// wags its tail — paced by the time of day (leopard geckos sleep by day and are active
    /// at dusk and night) and by its mood. Deterministic for a given Random seed, and free of
    /// Unity types so it is EditMode tested.
    /// </summary>
    public sealed class PetBehaviourPlanner
    {
        private readonly PetBehaviourTuning tuning;
        private readonly Random random;

        public PetBehaviourPlanner(PetBehaviourTuning tuning, Random random)
        {
            this.tuning = tuning;
            this.random = random;
        }

        public PetIntent Rest(PetMood mood)
        {
            GetIdleRange(mood, out var min, out var max);
            return new PetIntent(PetActivity.Rest, 0f, 0f, Range(min, max));
        }

        /// <summary>A uniform 0–1 roll from the planner's seeded random source.</summary>
        public double Roll()
        {
            return random.NextDouble();
        }

        public PetIntent NextAfterRest(PetMood mood, DayPhase phase, float currentX, float currentDepth)
        {
            var roll = random.NextDouble();
            var sleepChance = SleepChanceFor(phase, mood);
            if (roll < sleepChance)
            {
                return new PetIntent(PetActivity.Sleep, currentX, currentDepth, SleepSecondsFor(phase));
            }

            roll -= sleepChance;
            if (roll < tuning.YawnChance)
            {
                return new PetIntent(PetActivity.Yawn, currentX, currentDepth, tuning.YawnSeconds);
            }

            roll -= tuning.YawnChance;
            if (roll < tuning.TailWagChance)
            {
                return new PetIntent(PetActivity.TailWag, currentX, currentDepth, tuning.TailWagSeconds);
            }

            if (mood == PetMood.Sluggish && random.NextDouble() < tuning.SluggishStayChance)
            {
                return Rest(mood);
            }

            var targetX = currentX;
            for (var attempt = 0; attempt < 8 && Math.Abs(targetX - currentX) < tuning.MinWalkDistance; attempt++)
            {
                targetX = Range(tuning.MinX, tuning.MaxX);
            }

            return new PetIntent(PetActivity.Walk, targetX, Range(tuning.MinDepth, tuning.MaxDepth), 0f);
        }

        public float SpeedFor(PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Lively:
                    return tuning.LivelyWalkSpeed;
                case PetMood.Sluggish:
                    return tuning.SluggishWalkSpeed;
                default:
                    return tuning.NormalWalkSpeed;
            }
        }

        public double SleepChanceFor(DayPhase phase, PetMood mood)
        {
            double chance;
            switch (phase)
            {
                case DayPhase.Morning:
                    chance = tuning.MorningSleepChance;
                    break;
                case DayPhase.Day:
                    chance = tuning.DaySleepChance;
                    break;
                case DayPhase.Evening:
                    chance = tuning.EveningSleepChance;
                    break;
                default:
                    chance = tuning.NightSleepChance;
                    break;
            }

            if (mood == PetMood.Sluggish)
            {
                chance += tuning.SluggishMoodSleepBonus;
            }
            else if (mood == PetMood.Normal)
            {
                chance += tuning.NormalMoodSleepBonus;
            }

            return Math.Min(chance, 1d);
        }

        private float SleepSecondsFor(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning:
                    return Range(tuning.MorningSleepMinSeconds, tuning.MorningSleepMaxSeconds);
                case DayPhase.Day:
                    return Range(tuning.DaySleepMinSeconds, tuning.DaySleepMaxSeconds);
                default:
                    return Range(tuning.NightSleepMinSeconds, tuning.NightSleepMaxSeconds);
            }
        }

        private void GetIdleRange(PetMood mood, out float min, out float max)
        {
            switch (mood)
            {
                case PetMood.Lively:
                    min = tuning.LivelyIdleMinSeconds;
                    max = tuning.LivelyIdleMaxSeconds;
                    break;
                case PetMood.Sluggish:
                    min = tuning.SluggishIdleMinSeconds;
                    max = tuning.SluggishIdleMaxSeconds;
                    break;
                default:
                    min = tuning.NormalIdleMinSeconds;
                    max = tuning.NormalIdleMaxSeconds;
                    break;
            }
        }

        private float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
