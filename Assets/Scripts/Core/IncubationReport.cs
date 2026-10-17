using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>What happened in incubators and nest boxes during one advance (phase 5 §4.2 notices).</summary>
    public sealed class IncubationReport
    {
        /// <summary>Hatchlings, now in the hatchling tray (Task 6).</summary>
        public List<PetState> Hatched { get; } = new List<PetState>();

        /// <summary>Incubator eggs that reached 7 game days and turned out infertile.</summary>
        public List<Egg> InfertileRevealed { get; } = new List<Egg>();

        /// <summary>Incubator eggs that died (Failure says why; IncubatorId says where).</summary>
        public List<Egg> EggsDied { get; } = new List<Egg>();

        /// <summary>Incubators (with eggs) whose humidity fell below 60% during this advance.</summary>
        public List<int> HumidityLowIncubatorIds { get; } = new List<int>();

        /// <summary>Dead or infertile eggs cleared from nest boxes (CageId says where, ★8).</summary>
        public List<Egg> NestBoxEggsRemoved { get; } = new List<Egg>();

        public bool HasEvents =>
            Hatched.Count + InfertileRevealed.Count + EggsDied.Count + HumidityLowIncubatorIds.Count + NestBoxEggsRemoved.Count > 0;
    }
}
