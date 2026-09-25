using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>One animal: identity, body, care state and schedules. Plain data.</summary>
    public sealed class PetState
    {
        public const string DefaultDecorId = "rock_01";

        private double hunger = 80d;
        private double hydration = 80d;
        private double cleanliness = 80d;
        private double health = 100d;
        private double weightGrams = 3d;

        public int Id { get; set; }

        public string Name { get; set; } = "レオパ";

        /// <summary>True sex. Shown to the player only once <see cref="SexKnown"/>.</summary>
        public Sex Sex { get; set; } = Sex.Female;

        /// <summary>Stored: set when a juvenile-or-older animal sheds (§5.1 "known at juvenile").</summary>
        public bool SexRevealed { get; set; }

        public bool SexKnown => SexRevealed;

        public Personality Personality { get; set; } = Personality.Calm;

        /// <summary>False for a bought animal until it has been kept a while (§5.2); own hatchlings know it.</summary>
        public bool PersonalityKnown { get; set; } = true;

        /// <summary>True genes; hidden from the player except what shows.</summary>
        public Genotype Genotype { get; set; } = Genotype.Normal();

        /// <summary>What the player knows about this animal's hets.</summary>
        public KnownGenetics Known { get; set; } = new KnownGenetics();

        public double WeightGrams
        {
            get => weightGrams;
            set => weightGrams = Math.Max(0d, value);
        }

        public DateTimeOffset HatchedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Stored growth stage; it only advances after the pre-growth fast.</summary>
        public GrowthStage Stage { get; set; } = GrowthStage.Baby;

        public GrowthStage GrowthStage => Stage;

        /// <summary>When the pre-growth fast ends and the stage goes up; null when not fasting for growth.</summary>
        public DateTimeOffset? StageUpDueAtUtc { get; set; }

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

        public string SelectedDecorId { get; set; } = DefaultDecorId;

        public List<string> UnlockedDecorIds { get; set; } = new List<string> { DefaultDecorId };

        public DateTimeOffset LastSavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>When the pet last shed (脱皮). Default: never, i.e. at creation.</summary>
        public DateTimeOffset LastShedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>When the next periodic shed is due; food is refused in the days before it.</summary>
        public DateTimeOffset NextShedAtUtc { get; set; } = DateTimeOffset.UtcNow + GameCalendar.RealTimeFor(new CareTuning().YoungShedIntervalGameDays);
    }
}
