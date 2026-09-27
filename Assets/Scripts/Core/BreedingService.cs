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
