namespace TerrariumDays.Core
{
    /// <summary>
    /// Single source of truth for how the pet moves and looks on screen. Positions are
    /// floor coordinates (X 0 = left edge, 1 = right edge; depth 0 = back, 1 = front);
    /// sizes are the visible body width as a fraction of the terrarium view width.
    /// </summary>
    public sealed class PetBehaviourTuning
    {
        public double LivelyHealthThreshold { get; set; } = 80d;

        public float MinX { get; set; } = 0f;
        public float MaxX { get; set; } = 1f;
        public float MinDepth { get; set; } = 0.1f;
        public float MaxDepth { get; set; } = 1f;
        public float MinWalkDistance { get; set; } = 0.15f;

        /// <summary>How long one unit of depth is compared to one unit of X when walking.</summary>
        public float DepthWalkWeight { get; set; } = 0.3f;

        // Leopard geckos walk slowly and deliberately: these give roughly 1–1.5 strides/s.
        public float LivelyWalkSpeed { get; set; } = 0.06f;
        public float NormalWalkSpeed { get; set; } = 0.045f;
        public float SluggishWalkSpeed { get; set; } = 0.02f;

        public float LivelyIdleMinSeconds { get; set; } = 1.5f;
        public float LivelyIdleMaxSeconds { get; set; } = 4f;
        public float NormalIdleMinSeconds { get; set; } = 2.5f;
        public float NormalIdleMaxSeconds { get; set; } = 6f;
        public float SluggishIdleMinSeconds { get; set; } = 6f;
        public float SluggishIdleMaxSeconds { get; set; } = 12f;

        /// <summary>Chance a sluggish pet stays put instead of walking after resting.</summary>
        public double SluggishStayChance { get; set; } = 0.5d;

        public float BabyBodyWidth { get; set; } = 0.24f;
        public float JuvenileBodyWidth { get; set; } = 0.31f;
        public float AdultBodyWidth { get; set; } = 0.38f;


        /// <summary>Brightness multiplier applied to the sprite while sluggish.</summary>
        public float SluggishTint { get; set; } = 0.7f;

        // Daily rhythm (local time). Leopard geckos are crepuscular/nocturnal: long sleeps by
        // day, most active at dusk and night, only short naps then.
        public double MorningStartHour { get; set; } = 5d;
        public double DayStartHour { get; set; } = 10d;
        public double EveningStartHour { get; set; } = 16d;
        public double NightStartHour { get; set; } = 19d;

        // Chance to fall asleep at each decision after a rest, and how long a sleep lasts.
        public double MorningSleepChance { get; set; } = 0.45d;
        public double DaySleepChance { get; set; } = 0.75d;
        public double EveningSleepChance { get; set; } = 0.04d;
        public double NightSleepChance { get; set; } = 0.08d;
        public float MorningSleepMinSeconds { get; set; } = 90f;
        public float MorningSleepMaxSeconds { get; set; } = 240f;
        public float DaySleepMinSeconds { get; set; } = 240f;
        public float DaySleepMaxSeconds { get; set; } = 600f;
        public float NightSleepMinSeconds { get; set; } = 20f;
        public float NightSleepMaxSeconds { get; set; } = 60f;

        /// <summary>Extra sleep chance when out of sorts (a poorly kept gecko rests more).</summary>
        public double NormalMoodSleepBonus { get; set; } = 0.1d;
        public double SluggishMoodSleepBonus { get; set; } = 0.3d;

        public double YawnChance { get; set; } = 0.08d;
        public double TailWagChance { get; set; } = 0.12d;
        public float TailWagSeconds { get; set; } = 1.4f;
        /// <summary>The short tail twitch right before striking at food.</summary>
        public float PreStrikeTailWagSeconds { get; set; } = 0.6f;

        /// <summary>Woken by a tap: chance to be startled into a threat instead of yawning.</summary>
        public double WakeStartleChance { get; set; } = 0.35d;
        /// <summary>After being woken, stays up at least this long before it may sleep again.</summary>
        public float AwakeAfterWakingSeconds { get; set; } = 45f;

        /// <summary>It goes to sleep next to (just behind) its shelter decor when there is one.</summary>
        public float ShelterSideOffset { get; set; } = 0.16f;
        public float ShelterDepthOffset { get; set; } = 0.08f;
        public float ShelterReachedDistance { get; set; } = 0.05f;

        /// <summary>Night tint over the terrarium (0 = none).</summary>
        public float NightDarkness { get; set; } = 0.38f;
        public float EveningGlow { get; set; } = 0.14f;
        public float MorningGlow { get; set; } = 0.06f;

        // One-shot reactions: how long each plays before the pet goes back to resting.
        public float EatSeconds { get; set; } = 2f;
        public float HappySeconds { get; set; } = 1.6f;
        public float ThreatSeconds { get; set; } = 1.5f;
        public float YawnSeconds { get; set; } = 1.4f;

        /// <summary>Taps within the window that make the pet feel harassed and threaten.</summary>
        public int ThreatTapCount { get; set; } = 4;
        public float ThreatTapWindowSeconds { get; set; } = 2.5f;

        // Sprite-sheet playback speed per clip (frames per second).
        public float IdleFps { get; set; } = 6f;
        public float WalkFps { get; set; } = 10f;
        public float EatFps { get; set; } = 8f;
        public float SleepFps { get; set; } = 2f;
        public float YawnFps { get; set; } = 6f;
        public float ThreatFps { get; set; } = 8f;
        public float HappyFps { get; set; } = 10f;
        public float TailWagFps { get; set; } = 12f;

        // Floating effects.
        public int HeartsPerCheer { get; set; } = 3;
        public float HeartStaggerSeconds { get; set; } = 0.25f;
        public float HeartLifeSeconds { get; set; } = 1.3f;
        public float HeartRisePixels { get; set; } = 40f;
        public float HeartSizePixels { get; set; } = 20f;
        public float ZzzIntervalSeconds { get; set; } = 1.6f;
        public float ZzzLifeSeconds { get; set; } = 2f;
        public float ZzzRisePixels { get; set; } = 26f;
        public float ZzzSizePixels { get; set; } = 13f;
    }
}
