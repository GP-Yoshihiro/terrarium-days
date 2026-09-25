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

    /// <summary>One cage on a rack; holds at most one animal (pairing comes later).</summary>
    public sealed class Cage
    {
        public int Id { get; set; }
        public CageSize Size { get; set; } = CageSize.Standard;
        public int AnimalId { get; set; } = -1;
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
        public int IncubatorCount { get; set; } = 1;
        public int NextAnimalId { get; set; } = 1;
        public int NextCageId { get; set; } = 1;
        public int LastBilledMonthIndex { get; set; }

        public int CageCapacity => RackCount * CagesPerRack;

        public bool CanAddCage => Cages.Count < CageCapacity;

        public PetState AnimalById(int id) => Animals.Find(a => a.Id == id);

        public PetState AnimalIn(Cage cage) => cage == null || cage.IsEmpty ? null : AnimalById(cage.AnimalId);

        public Cage CageOf(PetState pet) => pet == null ? null : Cages.Find(c => c.AnimalId == pet.Id);

        public List<Cage> OccupiedCages() => Cages.FindAll(c => !c.IsEmpty);

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
            pet.Id = NextAnimalId++;
            Animals.Add(pet);
            if (cage != null)
            {
                cage.AnimalId = pet.Id;
            }

            return pet;
        }

        /// <summary>A fresh room: one baby of random sex in one standard cage, starting money, one simple incubator.</summary>
        public static Colony CreateNew(DateTimeOffset nowUtc, EconomyTuning economy, CareTuning care, Random random)
        {
            var colony = new Colony { CalendarEpochUtc = nowUtc };
            colony.Wallet.Money = economy.StartingMoney;
            var cage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState
            {
                Name = "レオパ1",
                Sex = random.NextDouble() < 0.5 ? Sex.Female : Sex.Male,
                WeightGrams = care.HatchlingWeightGrams,
                HatchedAtUtc = nowUtc,
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(GrowthStage.Baby, care),
            }, cage);
            return colony;
        }
    }
}
