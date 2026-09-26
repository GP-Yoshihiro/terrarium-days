using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Saves and loads the colony (schema 3). Schema 1/2 single-pet saves are migrated into
    /// cage 1 and the original file is kept as a backup.
    /// </summary>
    public sealed class ColonySaveService
    {
        public const int CurrentSchemaVersion = 3;
        public const int CurrentGenomeVersion = 1;
        public const int CurrentInventoryVersion = 1;
        private const string TimestampFormat = "o";

        private readonly CareTuning care;
        private readonly EconomyTuning economy;

        public ColonySaveService(CareTuning care, EconomyTuning economy)
        {
            this.care = care;
            this.economy = economy;
        }

        public bool LastLoadMigrated { get; private set; }

        /// <summary>True when the previous LoadOrCreate found an unreadable save and backed it up instead of starting fresh over it.</summary>
        public bool LastLoadFailed { get; private set; }

        /// <summary>True when the previous LoadOrCreate moved decor from animals'/legacy unlockedDecorIds into the shared inventory.</summary>
        public bool LastLoadMovedDecor { get; private set; }

        /// <summary>True when the previous LoadOrCreate found an unreadable save AND could not even make a backup copy of it.</summary>
        public bool LastLoadBackupFailed { get; private set; }

        public static string BackupPathFor(string path) => path + ".v2.bak";

        public static string CorruptBackupPathFor(string path, DateTimeOffset nowUtc) => path + ".corrupt-" + nowUtc.UtcTicks + ".bak";

        public Colony LoadOrCreate(string path, DateTimeOffset nowUtc, System.Random random)
        {
            LastLoadMigrated = false;
            LastLoadFailed = false;
            LastLoadMovedDecor = false;
            LastLoadBackupFailed = false;
            if (!File.Exists(path))
            {
                var created = Colony.CreateNew(nowUtc, economy, care, random);
                Save(path, created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(path);

                // Validate JSON format before parsing to avoid JsonUtility error logs
                var trimmed = json.TrimStart();
                if (string.IsNullOrEmpty(json) || (!trimmed.StartsWith("{") && !trimmed.StartsWith("[")))
                {
                    throw new InvalidDataException("Not valid JSON.");
                }

                var probe = JsonUtility.FromJson<SchemaProbe>(json);
                if (probe == null)
                {
                    throw new InvalidDataException("Empty save.");
                }

                if (probe.schemaVersion <= 0 && !(json.Contains("\"lastSavedAtUtc\"") && json.Contains("\"growthStage\"")))
                {
                    throw new InvalidDataException("No schema version and not a schema-1 pet save.");
                }

                if (probe.schemaVersion >= CurrentSchemaVersion)
                {
                    return FromSaveData(JsonUtility.FromJson<ColonySaveData>(json), nowUtc, random);
                }

                File.Copy(path, BackupPathFor(path), true);
                var migrated = MigrateLegacy(JsonUtility.FromJson<PetSaveData>(json), nowUtc, random);
                LastLoadMigrated = true;
                Save(path, migrated);
                return migrated;
            }
            catch (Exception ex)
            {
                LastLoadFailed = true;
                try
                {
                    File.Copy(path, CorruptBackupPathFor(path, nowUtc), true);
                }
                catch (Exception backupEx)
                {
                    LastLoadBackupFailed = true;
                    Debug.LogError($"Could not back up unreadable save at '{path}' ({backupEx.Message}).");
                }

                Debug.LogError($"Save data at '{path}' could not be loaded ({ex.Message}); starting fresh without overwriting the file.");
                return Colony.CreateNew(nowUtc, economy, care, random);
            }
        }

        /// <summary>
        /// Atomic write: builds the new content next to the target as a ".tmp" file, then
        /// replaces (or moves, first time) the target in one filesystem operation. Avoids a
        /// truncated/corrupt save if the process is killed mid-write (e.g. iOS suspending the
        /// app during OnApplicationPause).
        /// </summary>
        public void Save(string path, Colony colony)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, JsonUtility.ToJson(ToSaveData(colony)));
            if (File.Exists(path))
            {
                File.Replace(tmpPath, path, null);
            }
            else
            {
                File.Move(tmpPath, path);
            }
        }

        /// <summary>Old 0–100 growth gauge → grams: 0→3, 50→15, 100→45.</summary>
        public static double WeightFromLegacyGrowth(double growth)
        {
            growth = Math.Max(0d, Math.Min(100d, growth));
            return growth <= 50d ? 3d + growth / 50d * 12d : 15d + (growth - 50d) / 50d * 30d;
        }

        private Colony MigrateLegacy(PetSaveData data, DateTimeOffset nowUtc, System.Random random)
        {
            var colony = new Colony { CalendarEpochUtc = nowUtc };
            colony.Wallet.Money = economy.StartingMoney;
            var cage = colony.AddCage(CageSize.Standard);
            var stage = Enum.TryParse(data.growthStage, out GrowthStage parsed) ? parsed : GrowthStage.Baby;
            // Game time runs 1 real day = 1 game month, so an age in game months is applied
            // here as that many real days back from now.
            var ageRealDays = stage == GrowthStage.Adult ? 12d : stage == GrowthStage.Juvenile ? 5d : 1d;
            var lastSaved = Parse(data.lastSavedAtUtc, nowUtc);
            var nextShed = Parse(data.nextShedAtUtc, nowUtc);
            var interval = SheddingModel.IntervalFor(stage, care);
            if (nextShed - nowUtc > interval)
            {
                nextShed = nowUtc + interval;
            }

            var pet = new PetState
            {
                Name = "レオパ1",
                Sex = random.NextDouble() < 0.5 ? Sex.Female : Sex.Male,
                WeightGrams = WeightFromLegacyGrowth(data.growth),
                Stage = stage,
                HatchedAtUtc = nowUtc.AddDays(-ageRealDays),
                Hunger = data.hunger,
                Hydration = data.hydration,
                Cleanliness = data.cleanliness,
                Health = data.health,
                LastSavedAtUtc = lastSaved,
                LastShedAtUtc = Parse(data.lastShedAtUtc, lastSaved),
                NextShedAtUtc = nextShed,
                SexRevealed = false,
            };
            StarterGenetics.Apply(pet, random);
            colony.AddAnimal(pet, cage);

            MoveUnlockedDecorToInventory(colony, data.unlockedDecorIds);
            LastLoadMovedDecor = true;
            colony.Shop.Seed = random.Next();

            return colony;
        }

        private Colony FromSaveData(ColonySaveData data, DateTimeOffset nowUtc, System.Random random)
        {
            var colony = new Colony
            {
                CalendarEpochUtc = Parse(data.calendarEpochUtc, nowUtc),
                RackCount = data.rackCount,
                NextAnimalId = data.nextAnimalId,
                NextCageId = data.nextCageId,
                LastBilledMonthIndex = data.lastBilledMonthIndex,
            };
            colony.Incubators.Clear();
            if (data.incubators != null && data.incubators.Count > 0)
            {
                foreach (var incubator in data.incubators)
                {
                    colony.Incubators.Add(Enum.TryParse(incubator, out IncubatorModel model) ? model : IncubatorModel.Simple);
                }
            }
            else
            {
                for (var i = 0; i < data.incubatorCount; i++)
                {
                    colony.Incubators.Add(IncubatorModel.Simple);
                }
            }

            colony.Wallet.Money = data.money;
            foreach (var entry in data.ledger)
            {
                colony.Wallet.Ledger.Add(new LedgerEntry
                {
                    AtUtc = Parse(entry.atUtc, nowUtc),
                    Category = Enum.TryParse(entry.category, out LedgerCategory category) ? category : LedgerCategory.Other,
                    Amount = entry.amount,
                    Note = entry.note,
                });
            }

            foreach (var a in data.animals)
            {
                colony.Animals.Add(FromAnimalSaveData(a, nowUtc, random, owned: true));
            }

            foreach (var c in data.cages)
            {
                colony.Cages.Add(new Cage
                {
                    Id = c.id,
                    Size = Enum.TryParse(c.size, out CageSize size) ? size : CageSize.Standard,
                    AnimalId = c.animalId,
                    DecorIds = data.inventoryVersion >= CurrentInventoryVersion && c.decorIds != null
                        ? new List<string>(c.decorIds)
                        : new List<string>(),
                });
            }

            if (data.inventoryVersion >= CurrentInventoryVersion)
            {
                if (data.inventory != null)
                {
                    foreach (var item in data.inventory)
                    {
                        if (!string.IsNullOrEmpty(item.id) && item.count > 0)
                        {
                            colony.Inventory.Add(item.id, item.count);
                        }
                    }
                }
            }
            else
            {
                foreach (var a in data.animals)
                {
                    MoveUnlockedDecorToInventory(colony, a.unlockedDecorIds);
                }

                LastLoadMovedDecor = true;
            }

            colony.Shop.Seed = data.shopSeed;
            colony.Shop.StockMonthIndex = data.shopStocked ? data.shopMonthIndex : ShopStock.NeverStocked;
            if (data.shopOffers != null)
            {
                foreach (var offer in data.shopOffers)
                {
                    if (offer.animal == null)
                    {
                        continue;
                    }

                    colony.Shop.Offers.Add(new ShopOffer
                    {
                        OfferId = offer.offerId,
                        Animal = FromAnimalSaveData(offer.animal, nowUtc, random, owned: false),
                    });
                }
            }

            return colony;
        }

        private static void MoveUnlockedDecorToInventory(Colony colony, List<string> unlockedDecorIds)
        {
            foreach (var id in unlockedDecorIds ?? new List<string> { DecorItems.StarterDecorId })
            {
                if (DecorItems.IsDecor(id))
                {
                    colony.Inventory.Add(id, 1);
                }
            }
        }

        private PetState FromAnimalSaveData(AnimalSaveData a, DateTimeOffset nowUtc, System.Random random, bool owned)
        {
            var pet = new PetState
            {
                Id = a.id,
                Name = a.name,
                Sex = Enum.TryParse(a.sex, out Sex sex) ? sex : Sex.Female,
                WeightGrams = a.weightGrams,
                HatchedAtUtc = Parse(a.hatchedAtUtc, nowUtc),
                Stage = Enum.TryParse(a.stage, out GrowthStage stage) ? stage : GrowthStage.Baby,
                StageUpDueAtUtc = string.IsNullOrEmpty(a.stageUpDueAtUtc) ? (DateTimeOffset?)null : Parse(a.stageUpDueAtUtc, nowUtc),
                Hunger = a.hunger,
                Hydration = a.hydration,
                Cleanliness = a.cleanliness,
                Health = a.health,
                LastSavedAtUtc = Parse(a.lastSavedAtUtc, nowUtc),
                LastShedAtUtc = Parse(a.lastShedAtUtc, nowUtc),
                NextShedAtUtc = Parse(a.nextShedAtUtc, nowUtc),
                SexRevealed = a.sexRevealed,
            };

            if (a.genomeVersion >= 1)
            {
                var genotype = Genotype.Normal(a.hypo, a.tangerine);
                if (a.genes != null)
                {
                    foreach (var g in a.genes)
                    {
                        if (Enum.TryParse(g.gene, out GeneId geneId))
                        {
                            genotype.Set(geneId, g.copies);
                        }
                    }
                }

                pet.Genotype = genotype;

                var known = new KnownGenetics { HetsUnknown = a.hetsUnknown };
                if (a.hets != null)
                {
                    foreach (var h in a.hets)
                    {
                        if (Enum.TryParse(h.gene, out GeneId geneId))
                        {
                            known.SetHet(geneId, h.probability);
                        }
                    }
                }

                pet.Known = known;

                pet.Personality = Enum.TryParse(a.personality, out Personality personality) ? personality : PersonalityTraits.Roll(random);
                pet.PersonalityKnown = a.personalityKnown;
            }
            else
            {
                StarterGenetics.Apply(pet, random);

                // Phase-1 schema-3 saves predate genome tracking but not sex reveal:
                // players already saw ♂/♀ for anything past Baby, so keep it revealed
                // on migration. Schema-1/2 saves go through MigrateLegacy instead and
                // stay SexRevealed = false there (spec).
                if (pet.Stage != GrowthStage.Baby)
                {
                    pet.SexRevealed = true;
                }
            }

            pet.PersonalityRevealAtUtc = string.IsNullOrEmpty(a.personalityRevealAtUtc) ? (DateTimeOffset?)null : Parse(a.personalityRevealAtUtc, nowUtc);
            if (owned && !pet.PersonalityKnown && !pet.PersonalityRevealAtUtc.HasValue)
            {
                pet.PersonalityRevealAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays);
            }

            return pet;
        }

        private static ColonySaveData ToSaveData(Colony colony)
        {
            var data = new ColonySaveData
            {
                schemaVersion = CurrentSchemaVersion,
                calendarEpochUtc = Format(colony.CalendarEpochUtc),
                money = colony.Wallet.Money,
                rackCount = colony.RackCount,
                incubatorCount = colony.IncubatorCount,
                nextAnimalId = colony.NextAnimalId,
                nextCageId = colony.NextCageId,
                lastBilledMonthIndex = colony.LastBilledMonthIndex,
                inventoryVersion = CurrentInventoryVersion,
                shopSeed = colony.Shop.Seed,
                shopStocked = colony.Shop.StockMonthIndex != ShopStock.NeverStocked,
                shopMonthIndex = colony.Shop.StockMonthIndex,
            };
            foreach (var incubator in colony.Incubators)
            {
                data.incubators.Add(incubator.ToString());
            }

            foreach (var entry in colony.Wallet.Ledger)
            {
                data.ledger.Add(new LedgerSaveData
                {
                    atUtc = Format(entry.AtUtc),
                    category = entry.Category.ToString(),
                    amount = entry.Amount,
                    note = entry.Note,
                });
            }

            foreach (var a in colony.Animals)
            {
                data.animals.Add(ToAnimalSaveData(a));
            }

            foreach (var c in colony.Cages)
            {
                data.cages.Add(new CageSaveData
                {
                    id = c.Id,
                    size = c.Size.ToString(),
                    animalId = c.AnimalId,
                    decorIds = new List<string>(c.DecorIds),
                });
            }

            foreach (var item in colony.Inventory.Items)
            {
                data.inventory.Add(new InventorySaveData { id = item.Key, count = item.Value });
            }

            foreach (var offer in colony.Shop.Offers)
            {
                data.shopOffers.Add(new ShopOfferSaveData { offerId = offer.OfferId, animal = ToAnimalSaveData(offer.Animal) });
            }

            return data;
        }

        private static AnimalSaveData ToAnimalSaveData(PetState a)
        {
            var genes = new List<GeneSaveData>();
            var hets = new List<HetSaveData>();
            foreach (var gene in Genes.All)
            {
                var copies = a.Genotype.Copies(gene);
                if (copies >= 1)
                {
                    genes.Add(new GeneSaveData { gene = gene.ToString(), copies = copies });
                }

                var probability = a.Known.HetProbability(gene);
                if (probability > 0d)
                {
                    hets.Add(new HetSaveData { gene = gene.ToString(), probability = probability });
                }
            }

            return new AnimalSaveData
            {
                id = a.Id,
                name = a.Name,
                sex = a.Sex.ToString(),
                weightGrams = a.WeightGrams,
                hatchedAtUtc = Format(a.HatchedAtUtc),
                stage = a.Stage.ToString(),
                stageUpDueAtUtc = a.StageUpDueAtUtc.HasValue ? Format(a.StageUpDueAtUtc.Value) : string.Empty,
                hunger = a.Hunger,
                hydration = a.Hydration,
                cleanliness = a.Cleanliness,
                health = a.Health,
                selectedDecorId = null,
                unlockedDecorIds = null,
                lastSavedAtUtc = Format(a.LastSavedAtUtc),
                lastShedAtUtc = Format(a.LastShedAtUtc),
                nextShedAtUtc = Format(a.NextShedAtUtc),
                genomeVersion = CurrentGenomeVersion,
                genes = genes,
                hypo = a.Genotype.Hypo,
                tangerine = a.Genotype.Tangerine,
                hets = hets,
                hetsUnknown = a.Known.HetsUnknown,
                personality = a.Personality.ToString(),
                personalityKnown = a.PersonalityKnown,
                sexRevealed = a.SexRevealed,
                personalityRevealAtUtc = a.PersonalityRevealAtUtc.HasValue ? Format(a.PersonalityRevealAtUtc.Value) : string.Empty,
            };
        }

        private static string Format(DateTimeOffset value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        private static DateTimeOffset Parse(string value, DateTimeOffset fallback) =>
            string.IsNullOrEmpty(value) ? fallback : DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture);
    }
}
