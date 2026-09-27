using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Why a pairing or a gravid state ended.</summary>
    public enum BreedingEnd
    {
        Weak,
        SeasonOver,
        TooThin,
        AllClutchesLaid
    }

    public sealed class ClutchReport
    {
        public ClutchReport(PetState female, int eggCount, bool inNestBox)
        {
            Female = female;
            EggCount = eggCount;
            InNestBox = inNestBox;
        }

        public PetState Female { get; }

        public int EggCount { get; }

        public bool InNestBox { get; }
    }

    /// <summary>What happened in breeding during one time step, for notifications.</summary>
    public sealed class BreedingReport
    {
        public List<(PetState Female, PetState Male)> PairingsSucceeded { get; } = new List<(PetState, PetState)>();

        public List<(PetState Female, PetState Male)> PairingsFailed { get; } = new List<(PetState, PetState)>();

        public List<(PetState Female, PetState Male, BreedingEnd Reason)> PairingsCancelled { get; } =
            new List<(PetState, PetState, BreedingEnd)>();

        public List<ClutchReport> Clutches { get; } = new List<ClutchReport>();

        public List<(PetState Female, BreedingEnd Reason)> GravidEnded { get; } = new List<(PetState, BreedingEnd)>();

        public List<Egg> EggsDried { get; } = new List<Egg>();

        public bool HasEvents => PairingsSucceeded.Count > 0 || PairingsFailed.Count > 0 || PairingsCancelled.Count > 0
            || Clutches.Count > 0 || GravidEnded.Count > 0 || EggsDried.Count > 0;
    }
}
