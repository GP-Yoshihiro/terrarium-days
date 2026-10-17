using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Result of "move all eggs to the incubator" (phase 5 §4.2).</summary>
    public sealed class EggMoveResult
    {
        public EggMoveResult(int moved, int left)
        {
            Moved = moved;
            Left = left;
        }

        public int Moved { get; }

        /// <summary>Live eggs that stayed in the cage because no incubator had room.</summary>
        public int Left { get; }
    }

    /// <summary>
    /// Incubator actions (phase 5 spec §2.2, §3): moving eggs in, water, temperature,
    /// removing known-bad eggs; and advancing incubators over time (<see cref="Advance"/>).
    /// No Unity dependency.
    /// </summary>
    public sealed partial class IncubatorService
    {
        private readonly CareTuning care;

        public IncubatorService(CareTuning care)
        {
            this.care = care ?? new CareTuning();
        }

        /// <summary>Eggs in this incubator, lowest id first.</summary>
        public static List<Egg> EggsInIncubator(Colony colony, Incubator incubator)
        {
            var eggs = colony.Eggs.FindAll(e => e.Place == EggPlace.Incubator && e.IncubatorId == incubator.Id);
            eggs.Sort((a, b) => a.Id.CompareTo(b.Id));
            return eggs;
        }

        public static int FreeSlots(Colony colony, Incubator incubator) =>
            Math.Max(0, IncubatorRules.Capacity(incubator.Model) - EggsInIncubator(colony, incubator).Count);

        /// <summary>Eggs inside any incubator (the home button count, ★14).</summary>
        public static int EggsInIncubators(Colony colony) => colony.Eggs.FindAll(e => e.Place == EggPlace.Incubator).Count;

        /// <summary>The lowest-id incubator with a free slot, or null.</summary>
        public static Incubator FirstWithFreeSlot(Colony colony)
        {
            Incubator best = null;
            foreach (var incubator in colony.Incubators)
            {
                if (FreeSlots(colony, incubator) > 0 && (best == null || incubator.Id < best.Id))
                {
                    best = incubator;
                }
            }

            return best;
        }

        /// <summary>
        /// Moves the cage's live eggs (nest box or floor) into incubators: lowest egg id first,
        /// into the lowest-id incubator with room (★7). Development and the middle-third
        /// record carry over. Dead eggs stay where they are.
        /// </summary>
        public EggMoveResult MoveAllFromCage(Colony colony, Cage cage)
        {
            var eggs = colony.EggsIn(cage).FindAll(e => !e.Failed);
            eggs.Sort((a, b) => a.Id.CompareTo(b.Id));
            var moved = 0;
            var left = 0;
            foreach (var egg in eggs)
            {
                var target = FirstWithFreeSlot(colony);
                if (target == null)
                {
                    left++;
                    continue;
                }

                egg.Place = EggPlace.Incubator;
                egg.IncubatorId = target.Id;
                egg.CageId = -1;
                moved++;
            }

            return new EggMoveResult(moved, left);
        }

        /// <summary>Adds water (+15%, capped at 100%). Returns true when humidity is now above the mould line (★6).</summary>
        public bool AddWater(Incubator incubator)
        {
            incubator.HumidityPercent = IncubatorRules.ClampHumidity(incubator.HumidityPercent + care.AddWaterPercent);
            return incubator.HumidityPercent > care.HumidityMoldAbovePercent;
        }

        /// <summary>Sets the temperature (clamped to 24〜34℃). Returns the value set.</summary>
        public int SetTemperature(Incubator incubator, int celsius)
        {
            incubator.SetTemperatureC = IncubatorRules.ClampSetting(celsius);
            return incubator.SetTemperatureC;
        }

        /// <summary>An egg can be thrown away once it is dead, or known infertile (7 game days, §3).</summary>
        public bool CanRemove(Egg egg, DateTimeOffset gameNowUtc) =>
            egg != null
            && egg.Place == EggPlace.Incubator
            && (egg.Failed || (!egg.Fertile && GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc) >= care.CandlingRevealGameDays));

        public bool RemoveEgg(Colony colony, Egg egg, DateTimeOffset gameNowUtc) =>
            CanRemove(egg, gameNowUtc) && colony.Eggs.Remove(egg);

        /// <summary>
        /// Debug (★20): fertile eggs at 90% development, parents unknown, into the first incubator
        /// with room (only as many as fit). Returns how many were added.
        /// </summary>
        public int AddDebugEggs(Colony colony, DateTimeOffset gameNowUtc, int count = 3)
        {
            var incubator = FirstWithFreeSlot(colony);
            if (incubator == null)
            {
                return 0;
            }

            var added = Math.Min(count, FreeSlots(colony, incubator));
            var daysToHatch = EggDevelopment.DaysToHatch(incubator.SetTemperatureC);
            for (var i = 0; i < added; i++)
            {
                colony.Eggs.Add(new Egg
                {
                    Id = colony.NextEggId++,
                    MotherId = 0,
                    FatherId = 0,
                    CageId = -1,
                    LaidAtUtc = gameNowUtc - GameCalendar.RealTimeFor(daysToHatch * 0.9d),
                    Place = EggPlace.Incubator,
                    IncubatorId = incubator.Id,
                    Fertile = true,
                    ChildGenotype = Genotype.Normal(),
                    ChildKnown = new KnownGenetics(),
                    DevelopmentPercent = 90d,
                    MiddleThirdGameDays = daysToHatch / 3d,
                    MiddleThirdTemperatureSum = incubator.SetTemperatureC * daysToHatch / 3d,
                    AppliedUntilUtc = gameNowUtc
                });
            }

            return added;
        }
    }
}
