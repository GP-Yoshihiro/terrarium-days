using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum NestBoxResult
    {
        Ok,
        NoCage,
        AlreadyPlaced,
        NoneInInventory,
        NotPlaced,
        EggsInside
    }

    /// <summary>
    /// Pairing, laying and eggs (§7). Pure: time comes in as arguments and randomness from
    /// BreedingRandom. The session calls Advance on every time step.
    /// </summary>
    public sealed class BreedingService
    {
        private readonly CareTuning care;

        public BreedingService(CareTuning care)
        {
            this.care = care;
        }

        public CareTuning Care => care;

        /// <summary>§7.1 for a concrete pair in this room: both in cages, in season, one male and one female, neither busy.</summary>
        public PairingProblem CheckPair(Colony colony, PetState male, PetState female, DateTimeOffset nowUtc, GameCalendar calendar)
        {
            if (male == null || female == null || colony.CageOf(male) == null || colony.CageOf(female) == null)
            {
                return PairingProblem.NotFound;
            }

            if (!BreedingRules.IsBreedingSeason(calendar.DateAt(nowUtc), care))
            {
                return PairingProblem.OutOfSeason;
            }

            var problem = BreedingRules.CheckAnimal(male, Sex.Male, nowUtc, care);
            if (problem == PairingProblem.None)
            {
                problem = BreedingRules.CheckAnimal(female, Sex.Female, nowUtc, care);
            }

            if (problem != PairingProblem.None)
            {
                return problem;
            }

            if (female.Gravid != null)
            {
                return PairingProblem.Gravid;
            }

            return colony.PairingOf(male) != null || colony.PairingOf(female) != null
                ? PairingProblem.AlreadyPairing
                : PairingProblem.None;
        }

        /// <summary>One animal on its own, for the pairing screen's lists (its own reasons first, the season last).</summary>
        public PairingProblem CheckCandidate(Colony colony, PetState pet, DateTimeOffset nowUtc, GameCalendar calendar)
        {
            if (pet == null || colony.CageOf(pet) == null)
            {
                return PairingProblem.NotFound;
            }

            if (!pet.SexKnown)
            {
                return PairingProblem.SexUnknown;
            }

            var problem = BreedingRules.CheckAnimal(pet, pet.Sex, nowUtc, care);
            if (problem != PairingProblem.None)
            {
                return problem;
            }

            if (pet.Gravid != null)
            {
                return PairingProblem.Gravid;
            }

            if (colony.PairingOf(pet) != null)
            {
                return PairingProblem.AlreadyPairing;
            }

            return BreedingRules.IsBreedingSeason(calendar.DateAt(nowUtc), care) ? PairingProblem.None : PairingProblem.OutOfSeason;
        }

        /// <summary>Puts the female in the male's cage for PairingGameDays (§7.3). The outcome is rolled when it ends.</summary>
        public PairingProblem StartPairing(Colony colony, int maleId, int femaleId, DateTimeOffset nowUtc, GameCalendar calendar)
        {
            var male = colony.AnimalById(maleId);
            var female = colony.AnimalById(femaleId);
            var problem = CheckPair(colony, male, female, nowUtc, calendar);
            if (problem != PairingProblem.None)
            {
                return problem;
            }

            var compatibility = PersonalityTraits.CompatibilityOf(male.Personality, female.Personality);
            colony.Pairings.Add(new Pairing
            {
                Id = colony.NextPairingId++,
                MaleId = male.Id,
                FemaleId = female.Id,
                StartedAtUtc = nowUtc,
                EndsAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PairingGameDays),
                SuccessChance = BreedingRules.MatingSuccess(compatibility, male, female, care),
            });
            colony.CageOf(male).VisitorAnimalId = female.Id;
            return PairingProblem.None;
        }

        /// <summary>Ends a pairing early with no outcome and sends the female home. False when the animal is not pairing.</summary>
        public bool CancelPairing(Colony colony, PetState pet)
        {
            var pairing = colony.PairingOf(pet);
            if (pairing == null)
            {
                return false;
            }

            EndPairing(colony, pairing);
            return true;
        }

        /// <summary>
        /// Brings breeding up to targetUtc (game clock): pairings that ended, clutches that were
        /// due, the season's end, and the eggs. Scheduled events fire at their own times however
        /// long the gap; egg development applies at most MaxOfflineProgressHours at once, like
        /// the animals' care values.
        /// </summary>
        public BreedingReport Advance(Colony colony, DateTimeOffset targetUtc, GameCalendar calendar, double roomTemperatureC)
        {
            var report = new BreedingReport();
            ResolvePairings(colony, targetUtc, calendar, report);
            foreach (var female in colony.Animals)
            {
                LayClutches(colony, female, targetUtc, calendar, report);
            }

            AdvanceEggs(colony, targetUtc, roomTemperatureC, report);
            return report;
        }

        public NestBoxResult PlaceNestBox(Colony colony, Cage cage)
        {
            if (cage == null)
            {
                return NestBoxResult.NoCage;
            }

            if (cage.HasNestBox)
            {
                return NestBoxResult.AlreadyPlaced;
            }

            if (!colony.Inventory.TryTake(ShopCatalog.NestBoxId))
            {
                return NestBoxResult.NoneInInventory;
            }

            cage.HasNestBox = true;
            return NestBoxResult.Ok;
        }

        public NestBoxResult RemoveNestBox(Colony colony, Cage cage)
        {
            if (cage == null)
            {
                return NestBoxResult.NoCage;
            }

            if (!cage.HasNestBox)
            {
                return NestBoxResult.NotPlaced;
            }

            if (colony.Eggs.Exists(e => e.CageId == cage.Id && e.Place == EggPlace.NestBox))
            {
                return NestBoxResult.EggsInside;
            }

            cage.HasNestBox = false;
            colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            return NestBoxResult.Ok;
        }

        private void ResolvePairings(Colony colony, DateTimeOffset targetUtc, GameCalendar calendar, BreedingReport report)
        {
            foreach (var pairing in colony.Pairings.ToArray())
            {
                var male = colony.AnimalById(pairing.MaleId);
                var female = colony.AnimalById(pairing.FemaleId);
                if (male == null || female == null)
                {
                    EndPairing(colony, pairing);
                    continue;
                }

                if (male.Weak || female.Weak)
                {
                    EndPairing(colony, pairing);
                    report.PairingsCancelled.Add((female, male, BreedingEnd.Weak));
                    continue;
                }

                if (pairing.EndsAtUtc > targetUtc)
                {
                    continue;
                }

                EndPairing(colony, pairing);
                var season = BreedingRules.SeasonOf(calendar.DateAt(pairing.EndsAtUtc), care);
                if (season < 0)
                {
                    report.PairingsCancelled.Add((female, male, BreedingEnd.SeasonOver));
                    continue;
                }

                var random = BreedingRandom.For(colony.BreedingSeed, BreedingRandom.PairingStream, pairing.Id, 0);
                if (random.NextDouble() >= pairing.SuccessChance)
                {
                    report.PairingsFailed.Add((female, male));
                    continue;
                }

                var compatibility = PersonalityTraits.CompatibilityOf(male.Personality, female.Personality);
                var clutches = BreedingRules.ClutchRange(compatibility, care);
                female.Gravid = new GravidState
                {
                    PairingId = pairing.Id,
                    FatherId = male.Id,
                    FatherName = male.Name,
                    FatherGenotype = male.Genotype.Clone(),
                    FatherKnown = male.Known.Clone(),
                    Compatibility = compatibility,
                    SeasonYear = season,
                    ClutchesPlanned = random.Next(clutches.Min, clutches.Max + 1),
                    NextClutchAtUtc = pairing.EndsAtUtc + GameCalendar.RealTimeFor(
                        BreedingRandom.Uniform(random, care.FirstClutchMinGameDays, care.FirstClutchMaxGameDays)),
                };
                report.PairingsSucceeded.Add((female, male));
            }
        }

        private void LayClutches(Colony colony, PetState female, DateTimeOffset targetUtc, GameCalendar calendar, BreedingReport report)
        {
            while (female.Gravid != null && female.Gravid.NextClutchAtUtc <= targetUtc)
            {
                var gravid = female.Gravid;
                var at = gravid.NextClutchAtUtc;
                if (BreedingRules.SeasonOf(calendar.DateAt(at), care) != gravid.SeasonYear)
                {
                    EndGravid(female, BreedingEnd.SeasonOver, report);
                    return;
                }

                if (female.Weak)
                {
                    EndGravid(female, BreedingEnd.Weak, report);
                    return;
                }

                if (female.WeightGrams < care.LayingStopWeightGrams)
                {
                    EndGravid(female, BreedingEnd.TooThin, report);
                    return;
                }

                Lay(colony, female, gravid, at, report);

                if (gravid.ClutchesLaid >= gravid.ClutchesPlanned)
                {
                    EndGravid(female, BreedingEnd.AllClutchesLaid, report);
                    return;
                }

                if (female.WeightGrams < care.LayingStopWeightGrams)
                {
                    EndGravid(female, BreedingEnd.TooThin, report);
                    return;
                }
            }

            if (female.Gravid == null)
            {
                return;
            }

            if (BreedingRules.SeasonOf(calendar.DateAt(targetUtc), care) != female.Gravid.SeasonYear)
            {
                EndGravid(female, BreedingEnd.SeasonOver, report);
            }
            else if (female.Weak)
            {
                EndGravid(female, BreedingEnd.Weak, report);
            }
        }

        private void Lay(Colony colony, PetState female, GravidState gravid, DateTimeOffset at, BreedingReport report)
        {
            var random = BreedingRandom.For(colony.BreedingSeed, BreedingRandom.ClutchStream, gravid.PairingId, gravid.ClutchesLaid);
            var cage = colony.CageOf(female);
            var inNestBox = cage != null && cage.HasNestBox;
            var count = random.NextDouble() < care.SingleEggChance ? 1 : 2;
            var fertility = BreedingRules.FertilityFor(gravid.Compatibility, care);
            for (var i = 0; i < count; i++)
            {
                var child = GeneticsCalculator.Breed(female.Genotype, gravid.FatherGenotype, random);
                colony.Eggs.Add(new Egg
                {
                    Id = colony.NextEggId++,
                    MotherId = female.Id,
                    FatherId = gravid.FatherId,
                    CageId = cage != null ? cage.Id : -1,
                    LaidAtUtc = at,
                    AppliedUntilUtc = at,
                    Place = inNestBox ? EggPlace.NestBox : EggPlace.Loose,
                    Fertile = random.NextDouble() < fertility,
                    ChildGenotype = child,
                    ChildKnown = KnownGenetics.ForChild(female.Genotype, female.Known, gravid.FatherGenotype, gravid.FatherKnown, child),
                });
            }

            female.WeightGrams -= BreedingRandom.Uniform(random, care.ClutchWeightLossMinGrams, care.ClutchWeightLossMaxGrams);
            gravid.ClutchesLaid++;
            gravid.NextClutchAtUtc = at + GameCalendar.RealTimeFor(
                BreedingRandom.Uniform(random, care.ClutchIntervalMinGameDays, care.ClutchIntervalMaxGameDays));
            report.Clutches.Add(new ClutchReport(female, count, inNestBox));
        }

        private void AdvanceEggs(Colony colony, DateTimeOffset targetUtc, double roomTemperatureC, BreedingReport report)
        {
            var dryAfter = GameCalendar.RealTimeFor(care.LooseEggDryGameDays);
            var cap = TimeSpan.FromHours(care.MaxOfflineProgressHours);
            foreach (var egg in colony.Eggs.ToArray())
            {
                if (egg.Place == EggPlace.Loose)
                {
                    if (targetUtc >= egg.LaidAtUtc + dryAfter)
                    {
                        egg.Failure = EggFailure.Dried;
                        colony.Eggs.Remove(egg);
                        report.EggsDried.Add(egg);
                    }

                    continue;
                }

                var elapsed = targetUtc - egg.AppliedUntilUtc;
                if (elapsed <= TimeSpan.Zero)
                {
                    continue;
                }

                egg.AppliedUntilUtc = targetUtc;
                var applied = elapsed > cap ? cap : elapsed;
                EggDevelopment.Apply(egg, applied.TotalMinutes / GameCalendar.RealMinutesPerGameDay, roomTemperatureC, care);
            }
        }

        private static void EndGravid(PetState female, BreedingEnd reason, BreedingReport report)
        {
            female.Gravid = null;
            report.GravidEnded.Add((female, reason));
        }

        private static void EndPairing(Colony colony, Pairing pairing)
        {
            colony.Pairings.Remove(pairing);
            foreach (var cage in colony.Cages)
            {
                if (cage.VisitorAnimalId == pairing.FemaleId)
                {
                    cage.VisitorAnimalId = -1;
                }
            }
        }
    }
}
