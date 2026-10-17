using System;

namespace TerrariumDays.Core
{
    public enum MoveToCageResult
    {
        Ok,
        NoEmptyCage,
        NotInTray
    }

    /// <summary>
    /// Hatching (phase 5 spec §2.4) and nest-box clean-up (★8). Fertile live eggs at 100%
    /// hatch wherever they are into the hatchling tray; sex comes from the middle third.
    /// </summary>
    public sealed class Hatchery
    {
        private readonly CareTuning care;

        public Hatchery(CareTuning care)
        {
            this.care = care ?? new CareTuning();
        }

        public void Advance(Colony colony, DateTimeOffset gameNowUtc, double roomC, IncubationReport report)
        {
            foreach (var egg in colony.Eggs.ToArray())
            {
                if (egg.Fertile && !egg.Failed && egg.DevelopmentPercent >= 100d)
                {
                    report.Hatched.Add(Hatch(colony, egg, gameNowUtc, FallbackTemperature(colony, egg, roomC)));
                    continue;
                }

                if (egg.Place != EggPlace.NestBox)
                {
                    continue;
                }

                var knownInfertile = !egg.Fertile
                    && GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc) >= care.CandlingRevealGameDays;
                if (egg.Failed || knownInfertile)
                {
                    colony.Eggs.Remove(egg);
                    report.NestBoxEggsRemoved.Add(egg);
                }
            }
        }

        /// <summary>Turns the egg into a baby in the tray (no cage). The egg is removed.</summary>
        public PetState Hatch(Colony colony, Egg egg, DateTimeOffset gameNowUtc, double fallbackC)
        {
            var random = BreedingRandom.For(colony.BreedingSeed, BreedingRandom.HatchStream, egg.Id, 0);
            var sex = HatchSex.Decide(egg, fallbackC, random);
            var mother = colony.AnimalById(egg.MotherId);
            var father = colony.AnimalById(egg.FatherId);
            var personality = mother != null && father != null
                ? PersonalityTraits.Inherit(mother.Personality, father.Personality, random)
                : PersonalityTraits.Roll(random);

            var pet = new PetState
            {
                Sex = sex,
                SexRevealed = false,
                Personality = personality,
                PersonalityKnown = false,
                PersonalityRevealAtUtc = gameNowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays),
                Genotype = egg.ChildGenotype,
                Known = egg.ChildKnown,
                HatchedAtUtc = gameNowUtc,
                Stage = GrowthStage.Baby,
                LastSavedAtUtc = gameNowUtc,
                LastShedAtUtc = gameNowUtc,
                NextShedAtUtc = gameNowUtc + SheddingModel.IntervalFor(GrowthStage.Baby, care),
                MotherId = egg.MotherId,
                FatherId = egg.FatherId,
                CareGraceUntilUtc = gameNowUtc + GameCalendar.RealTimeFor(care.HatchlingCareGraceGameDays)
            };
            pet.WeightGrams = care.HatchlingWeightGrams;

            colony.AddAnimal(pet, null);
            pet.Name = $"レオパ{pet.Id}";
            colony.Eggs.Remove(egg);
            return pet;
        }

        /// <summary>Tray → the lowest-id empty cage (★12).</summary>
        public static MoveToCageResult MoveToCage(Colony colony, PetState pet)
        {
            if (pet == null || !colony.Animals.Contains(pet) || colony.CageOf(pet) != null)
            {
                return MoveToCageResult.NotInTray;
            }

            var cage = colony.FirstEmptyCage();
            if (cage == null)
            {
                return MoveToCageResult.NoEmptyCage;
            }

            cage.AnimalId = pet.Id;
            return MoveToCageResult.Ok;
        }

        /// <summary>Sex fallback when no middle third was recorded (★9): the incubator's setting, else the room.</summary>
        private static double FallbackTemperature(Colony colony, Egg egg, double roomC)
        {
            if (egg.Place != EggPlace.Incubator)
            {
                return roomC;
            }

            var incubator = colony.IncubatorById(egg.IncubatorId);
            return incubator != null ? incubator.SetTemperatureC : roomC;
        }
    }
}
