using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum CageSize
    {
        Small,
        Standard,
        Large
    }

    public enum IncubatorModel
    {
        Simple,
        Standard,
        Luxury
    }

    /// <summary>One cage on a rack; holds at most one animal (pairing comes later).</summary>
    /// <summary>One cage on a rack; holds one animal, plus a visiting female while pairing (§6.1).</summary>
    public sealed class Cage
    {
        public int Id { get; set; }
        public CageSize Size { get; set; } = CageSize.Standard;
        public int AnimalId { get; set; } = -1;

        /// <summary>A female visiting for pairing; -1 when none. Only ever set on the male's cage.</summary>
        public int VisitorAnimalId { get; set; } = -1;

        /// <summary>A nest box is placed here (§7.7): eggs laid in this cage stay in it.</summary>
        public bool HasNestBox { get; set; }

        public List<string> DecorIds { get; set; } = new List<string>();
        public bool IsEmpty => AnimalId < 0;
    }

    /// <summary>
    /// The whole breeding room: animals, cages on racks, money and the calendar anchor.
    /// Plain data plus lookups; rules live in the services that use it.
    /// </summary>
    public sealed class Colony
    {
        public const int CagesPerRack = 4;

        public DateTimeOffset CalendarEpochUtc { get; set; }
        public Wallet Wallet { get; set; } = new Wallet();
        public List<PetState> Animals { get; set; } = new List<PetState>();
        public List<Cage> Cages { get; set; } = new List<Cage>();
        public int RackCount { get; set; } = 1;
        public Inventory Inventory { get; set; } = new Inventory();
        public List<IncubatorModel> Incubators { get; set; } = new List<IncubatorModel> { IncubatorModel.Simple };
        public int IncubatorCount => Incubators.Count;
        public int NextAnimalId { get; set; } = 1;
        public int NextCageId { get; set; } = 1;
        public int LastBilledMonthIndex { get; set; }
        /// <summary>
        /// How far the game clock runs ahead of real time. Debug fast-forward (the multiplier or
        /// "+12時間") adds to it and it is saved, so saving or reloading never rewinds the game
        /// date or replays scheduled events. Never negative; only a fresh save resets it.
        /// </summary>
        public TimeSpan GameClockOffset { get; set; } = TimeSpan.Zero;
        public List<Pairing> Pairings { get; set; } = new List<Pairing>();
        public List<Egg> Eggs { get; set; } = new List<Egg>();
        public int NextPairingId { get; set; } = 1;
        public int NextEggId { get; set; } = 1;

        /// <summary>Seed for BreedingRandom, from the calendar epoch (so it needs no save field).</summary>
        public int BreedingSeed => BreedingRandom.SeedOf(CalendarEpochUtc);
        public ShopStock Shop { get; set; } = new ShopStock();

        public int CageCapacity => RackCount * CagesPerRack;

        public bool CanAddCage => Cages.Count < CageCapacity;

        public PetState AnimalById(int id) => Animals.Find(a => a.Id == id);

        public PetState AnimalIn(Cage cage) => cage == null || cage.IsEmpty ? null : AnimalById(cage.AnimalId);

        public Cage CageOf(PetState pet) => pet == null ? null : Cages.Find(c => c.AnimalId == pet.Id);

        public List<Cage> OccupiedCages() => Cages.FindAll(c => !c.IsEmpty);

        public Pairing PairingOf(PetState pet) =>
            pet == null ? null : Pairings.Find(p => p.MaleId == pet.Id || p.FemaleId == pet.Id);

        public bool IsVisiting(PetState pet) => pet != null && Pairings.Exists(p => p.FemaleId == pet.Id);

        public PetState VisitorIn(Cage cage) => cage == null || cage.VisitorAnimalId < 0 ? null : AnimalById(cage.VisitorAnimalId);

        /// <summary>The cage an animal is in right now: the host male's while visiting, otherwise its own.</summary>
        public Cage CageShowing(PetState pet)
        {
            if (pet == null)
            {
                return null;
            }

            return Cages.Find(c => c.VisitorAnimalId == pet.Id) ?? CageOf(pet);
        }

        /// <summary>The resident a cage shows: none while that animal is away visiting (the cage looks empty but stays hers).</summary>
        public PetState ResidentShown(Cage cage)
        {
            var pet = AnimalIn(cage);
            return pet != null && IsVisiting(pet) ? null : pet;
        }

        /// <summary>Cages whose resident is at home: the ones the cage detail opens and swipes through.</summary>
        public List<Cage> ShownCages() => Cages.FindAll(c => ResidentShown(c) != null);

        public List<Egg> EggsIn(Cage cage) => cage == null ? new List<Egg>() : Eggs.FindAll(e => e.CageId == cage.Id);

        public Cage AddCage(CageSize size)
        {
            if (!CanAddCage)
            {
                return null;
            }

            var cage = new Cage { Id = NextCageId++, Size = size };
            Cages.Add(cage);
            return cage;
        }

        public PetState AddAnimal(PetState pet, Cage cage)
        {
            if (cage != null && !cage.IsEmpty)
            {
                throw new InvalidOperationException($"Cage {cage.Id} is already occupied.");
            }

            pet.Id = NextAnimalId++;
            Animals.Add(pet);
            if (cage != null)
            {
                cage.AnimalId = pet.Id;
            }

            return pet;
        }

        /// <summary>
        /// Takes the animal out of the room (sold): its cage becomes empty and keeps its decor,
        /// and any pairing it was in ends (a visiting female goes home).
        /// </summary>
        public bool RemoveAnimal(PetState pet)
        {
            if (pet == null || !Animals.Remove(pet))
            {
                return false;
            }

            Pairings.RemoveAll(p => p.MaleId == pet.Id || p.FemaleId == pet.Id);
            foreach (var cage in Cages)
            {
                if (cage.AnimalId == pet.Id)
                {
                    cage.AnimalId = -1;
                    cage.VisitorAnimalId = -1;
                }

                if (cage.VisitorAnimalId == pet.Id)
                {
                    cage.VisitorAnimalId = -1;
                }
            }

            return true;
        }

        /// <summary>A fresh room: one baby of random sex in one standard cage with a rock, starting money, one simple incubator.</summary>
        public static Colony CreateNew(DateTimeOffset nowUtc, EconomyTuning economy, CareTuning care, Random random)
        {
            var colony = new Colony { CalendarEpochUtc = nowUtc };
            colony.Wallet.Money = economy.StartingMoney;
            var cage = colony.AddCage(CageSize.Standard);
            cage.DecorIds.Add(DecorItems.StarterDecorId);
            var pet = new PetState
            {
                Name = "レオパ1",
                Sex = random.NextDouble() < 0.5 ? Sex.Female : Sex.Male,
                WeightGrams = care.HatchlingWeightGrams,
                HatchedAtUtc = nowUtc,
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(GrowthStage.Baby, care),
            };
            StarterGenetics.Apply(pet, random);
            colony.AddAnimal(pet, cage);
            colony.Shop.Seed = random.Next();
            return colony;
        }
    }
}
