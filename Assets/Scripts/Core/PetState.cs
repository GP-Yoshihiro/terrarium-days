using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Care state, growth, and decor selection for the single pet. Plain data, no MonoBehaviour.
    /// Field shape mirrors the save JSON in Terrarium_Days_仕様書.md section 8.1.
    /// </summary>
    public sealed class PetState
    {
        public const string DefaultDecorId = "rock_01";

        private double hunger = 80d;
        private double hydration = 80d;
        private double cleanliness = 80d;
        private double health = 100d;
        private double growth;

        public double Hunger
        {
            get => hunger;
            set => hunger = StatusValue.Clamp(value);
        }

        public double Hydration
        {
            get => hydration;
            set => hydration = StatusValue.Clamp(value);
        }

        public double Cleanliness
        {
            get => cleanliness;
            set => cleanliness = StatusValue.Clamp(value);
        }

        public double Health
        {
            get => health;
            set => health = StatusValue.Clamp(value);
        }

        public double Growth
        {
            get => growth;
            set => growth = StatusValue.Clamp(value);
        }

        public GrowthStage GrowthStage => GrowthStageFromGrowth(Growth);

        public string SelectedDecorId { get; set; } = DefaultDecorId;

        public List<string> UnlockedDecorIds { get; set; } = new List<string> { DefaultDecorId };

        public DateTimeOffset LastSavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>When the pet last shed (脱皮). Default: never, i.e. at creation.</summary>
        public DateTimeOffset LastShedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>When the next periodic shed is due; food is refused in the days before it.</summary>
        public DateTimeOffset NextShedAtUtc { get; set; } = DateTimeOffset.UtcNow.AddDays(new CareTuning().ShedIntervalDays);

        public static GrowthStage GrowthStageFromGrowth(double growth)
        {
            if (growth >= 100d)
            {
                return GrowthStage.Adult;
            }

            if (growth >= 50d)
            {
                return GrowthStage.Juvenile;
            }

            return GrowthStage.Baby;
        }
    }
}
