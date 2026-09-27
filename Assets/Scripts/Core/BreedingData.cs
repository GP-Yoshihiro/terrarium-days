using System;

namespace TerrariumDays.Core
{
    /// <summary>A female visiting a male's cage for pairing (§7.3); she lives there until it ends (§6.1).</summary>
    public sealed class Pairing
    {
        public int Id { get; set; }
        public int MaleId { get; set; }
        public int FemaleId { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset EndsAtUtc { get; set; }

        /// <summary>Fixed at the start from the true personalities, health and weight (§7.3).</summary>
        public double SuccessChance { get; set; }
    }

    /// <summary>
    /// A female carrying eggs after a successful pairing (§7.4–7.6). The father's genes are
    /// copied in, so the clutch does not depend on him staying in the room.
    /// </summary>
    public sealed class GravidState
    {
        public int PairingId { get; set; }
        public int FatherId { get; set; }
        public string FatherName { get; set; } = string.Empty;
        public Genotype FatherGenotype { get; set; } = Genotype.Normal();
        public KnownGenetics FatherKnown { get; set; } = new KnownGenetics();
        public Compatibility Compatibility { get; set; } = Compatibility.Normal;

        /// <summary>The game year of the breeding season; the state ends with that season (§7.5).</summary>
        public int SeasonYear { get; set; }

        public int ClutchesPlanned { get; set; }
        public int ClutchesLaid { get; set; }
        public DateTimeOffset NextClutchAtUtc { get; set; }
    }

    public enum EggPlace
    {
        /// <summary>In the cage's nest box: develops at room temperature (§7.7).</summary>
        NestBox,

        /// <summary>On the cage floor (no nest box): dries out after two game days (§7.7).</summary>
        Loose
    }

    public enum EggFailure
    {
        None,
        Dried,
        Cold
    }

    /// <summary>
    /// One egg (§7, §8). The child's genes are fixed when it is laid; sex (incubation
    /// temperature), candling and hatching come in phase 5.
    /// </summary>
    public sealed class Egg
    {
        public int Id { get; set; }
        public int MotherId { get; set; }
        public int FatherId { get; set; }
        public int CageId { get; set; } = -1;
        public DateTimeOffset LaidAtUtc { get; set; }
        public EggPlace Place { get; set; } = EggPlace.NestBox;

        /// <summary>Hidden from the player until candling (phase 5).</summary>
        public bool Fertile { get; set; }

        public Genotype ChildGenotype { get; set; } = Genotype.Normal();
        public KnownGenetics ChildKnown { get; set; } = new KnownGenetics();

        /// <summary>0–100. Only a fertile egg at 24 ℃ or warmer develops.</summary>
        public double DevelopmentPercent { get; set; }

        /// <summary>Time-weighted temperature over the middle third of development (§5.1, used in phase 5).</summary>
        public double MiddleThirdTemperatureSum { get; set; }

        public double MiddleThirdGameDays { get; set; }

        /// <summary>Game days spent below 24 ℃ in total; enough of them kill the egg.</summary>
        public double ColdGameDays { get; set; }

        public EggFailure Failure { get; set; } = EggFailure.None;

        public bool Failed => Failure != EggFailure.None;

        /// <summary>How far this egg's development has been applied (game clock).</summary>
        public DateTimeOffset AppliedUntilUtc { get; set; }
    }
}
