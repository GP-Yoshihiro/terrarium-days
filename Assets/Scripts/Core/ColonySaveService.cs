using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Saves and loads the colony (schema 4). Schema 1/2 single-pet saves are migrated into
    /// cage 1 and the original file is kept as a backup.
    /// </summary>
    public sealed class ColonySaveService
    {
        public const int CurrentSchemaVersion = 4;
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

                // Schema 3 loads here too: it only lacks the events field (read as no entry).
                if (probe.schemaVersion >= 3)
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
            var stage = TryParseDefined(data.growthStage, out GrowthStage parsed) ? parsed : GrowthStage.Baby;
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
                NewRackIndices = data.newRackIndices != null ? new List<int>(data.newRackIndices) : new List<int>(),
                NextCageId = data.nextCageId,
                LastBilledMonthIndex = data.lastBilledMonthIndex,
                GameClockOffset = TimeSpan.FromTicks(Math.Max(0L, data.gameClockOffsetTicks)),
            };
            colony.Incubators.Clear();
            var incubatorIdMap = new Dictionary<int, int>();
            if (data.incubatorStates != null && data.incubatorStates.Count > 0)
            {
                var seenIds = new HashSet<int>();
                var nextRenumberedId = 1;
                var idsAreSound = true;
                foreach (var state in data.incubatorStates)
                {
                    if (state == null || state.id <= 0 || !seenIds.Add(state.id))
                    {
                        idsAreSound = false;
                    }
                }

                // Unsound ids (0, negative, duplicated) renumber every incubator 1,2,3... in load
                // order; the first incubator that held a usable old id keeps its eggs.
                foreach (var rawState in data.incubatorStates)
                {
                    var state = rawState ?? new IncubatorSaveData();
                    var newId = idsAreSound ? state.id : nextRenumberedId++;
                    if (!idsAreSound && state.id > 0 && !incubatorIdMap.ContainsKey(state.id))
                    {
                        incubatorIdMap[state.id] = newId;
                    }

                    colony.Incubators.Add(new Incubator
                    {
                        Id = newId,
                        Model = TryParseDefined(state.model, out IncubatorModel model) ? model : IncubatorModel.Simple,
                        SetTemperatureC = IncubatorRules.ClampSetting(state.setTemperatureC),
                        HumidityPercent = IncubatorRules.ClampHumidity(state.humidityPercent),
                        AppliedUntilUtc = TryParseTime(state.appliedUntilUtc) ?? default,
                    });
                }
            }
            else if (data.incubators != null && data.incubators.Count > 0)
            {
                foreach (var name in data.incubators)
                {
                    colony.AddIncubator(TryParseDefined(name, out IncubatorModel model) ? model : IncubatorModel.Simple);
                }
            }
            else
            {
                for (var i = 0; i < data.incubatorCount; i++)
                {
                    colony.AddIncubator(IncubatorModel.Simple);
                }
            }

            if (colony.Incubators.Count == 0)
            {
                colony.AddIncubator(IncubatorModel.Simple);
            }

            colony.Wallet.Money = data.money;
            foreach (var entry in data.ledger)
            {
                colony.Wallet.Ledger.Add(new LedgerEntry
                {
                    AtUtc = Parse(entry.atUtc, nowUtc),
                    Category = TryParseDefined(entry.category, out LedgerCategory category) ? category : LedgerCategory.Other,
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
                    Size = TryParseDefined(c.size, out CageSize size) ? size : CageSize.Standard,
                    AnimalId = c.animalId,
                    DecorIds = data.inventoryVersion >= CurrentInventoryVersion && c.decorIds != null
                        ? new List<string>(c.decorIds)
                        : new List<string>(),
                    HasNestBox = c.hasNestBox,
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

            if (data.pairings != null)
            {
                foreach (var p in data.pairings)
                {
                    var male = colony.AnimalById(p.maleId);
                    var female = colony.AnimalById(p.femaleId);
                    if (male == null || female == null || male.Id == female.Id)
                    {
                        continue;
                    }

                    var maleCage = colony.CageOf(male);
                    var femaleCage = colony.CageOf(female);
                    if (maleCage == null || femaleCage == null)
                    {
                        continue;
                    }

                    if (colony.PairingOf(male) != null || colony.PairingOf(female) != null)
                    {
                        continue;
                    }

                    colony.Pairings.Add(new Pairing
                    {
                        Id = p.id,
                        MaleId = p.maleId,
                        FemaleId = p.femaleId,
                        StartedAtUtc = Parse(p.startedAtUtc, nowUtc),
                        EndsAtUtc = Parse(p.endsAtUtc, nowUtc),
                        SuccessChance = p.successChance,
                    });
                    maleCage.VisitorAnimalId = female.Id;
                }
            }

            if (data.gravidStates != null)
            {
                foreach (var g in data.gravidStates)
                {
                    var animal = colony.AnimalById(g.animalId);
                    if (animal == null || animal.Sex != Sex.Female)
                    {
                        continue;
                    }

                    animal.Gravid = new GravidState
                    {
                        PairingId = g.pairingId,
                        FatherId = g.fatherId,
                        FatherName = g.fatherName,
                        FatherGenotype = GenotypeFrom(g.fatherGenes, g.fatherHypo, g.fatherTangerine),
                        FatherKnown = KnownFrom(g.fatherHets, g.fatherHetsUnknown),
                        Compatibility = TryParseDefined(g.compatibility, out Compatibility compatibility) ? compatibility : Compatibility.Normal,
                        SeasonYear = g.seasonYear,
                        ClutchesPlanned = g.clutchesPlanned,
                        ClutchesLaid = g.clutchesLaid,
                        NextClutchAtUtc = Parse(g.nextClutchAtUtc, nowUtc),
                    };
                }
            }

            if (data.eggs != null)
            {
                foreach (var e in data.eggs)
                {
                    colony.Eggs.Add(new Egg
                    {
                        Id = e.id,
                        MotherId = e.motherId,
                        FatherId = e.fatherId,
                        CageId = e.cageId,
                        LaidAtUtc = Parse(e.laidAtUtc, nowUtc),
                        Place = TryParseDefined(e.place, out EggPlace place) ? place : EggPlace.NestBox,
                        IncubatorId = place == EggPlace.Incubator && incubatorIdMap.TryGetValue(e.incubatorId, out var mappedId) ? mappedId : (place == EggPlace.Incubator ? e.incubatorId : 0),
                        Fertile = e.fertile,
                        ChildGenotype = GenotypeFrom(e.genes, e.hypo, e.tangerine),
                        ChildKnown = KnownFrom(e.hets, e.hetsUnknown),
                        DevelopmentPercent = e.developmentPercent,
                        MiddleThirdTemperatureSum = e.middleThirdTemperatureSum,
                        MiddleThirdGameDays = e.middleThirdGameDays,
                        ColdGameDays = e.coldGameDays,
                        Failure = TryParseDefined(e.failure, out EggFailure failure) ? failure : EggFailure.None,
                        AppliedUntilUtc = Parse(e.appliedUntilUtc, nowUtc),
                    });
                }
            }

            // An egg that sits in an incubator that no longer exists goes to the first one with room
            // (the first incubator when all are full: over capacity beats losing the egg).
            foreach (var egg in colony.Eggs)
            {
                if (egg.Place == EggPlace.Incubator && colony.IncubatorById(egg.IncubatorId) == null)
                {
                    egg.IncubatorId = (IncubatorService.FirstWithFreeSlot(colony) ?? colony.Incubators[0]).Id;
                }
            }

            if (data.lineage != null)
            {
                foreach (var r in data.lineage)
                {
                    // JsonUtility turns a null entry into an all-default record (id 0): not a real animal.
                    if (r == null || r.id <= 0)
                    {
                        continue;
                    }

                    colony.Lineage.Add(new LineageRecord
                    {
                        Id = r.id,
                        Name = r.name ?? string.Empty,
                        MorphName = r.morphName ?? string.Empty,
                        Sex = TryParseDefined(r.sex, out Sex lineageSex) ? lineageSex : Sex.Female,
                        SexKnown = r.sexKnown,
                        MotherId = r.motherId,
                        FatherId = r.fatherId,
                    });
                }
            }

            colony.Events = EventsFromSaveData(data.events, colony, nowUtc, random);

            var maxAnimalId = Math.Max(
                colony.Animals.Count > 0 ? colony.Animals.Max(a => a.Id) : 0,
                colony.Lineage.Count > 0 ? colony.Lineage.Max(r => r.Id) : 0);
            colony.NextAnimalId = Math.Max(1, Math.Max(data.nextAnimalId, maxAnimalId + 1));
            var maxPairingId = colony.Pairings.Count > 0 ? colony.Pairings.Max(p => p.Id) : 0;
            colony.NextPairingId = Math.Max(1, Math.Max(data.nextPairingId, maxPairingId + 1));
            var maxEggId = colony.Eggs.Count > 0 ? colony.Eggs.Max(e => e.Id) : 0;
            colony.NextEggId = Math.Max(1, Math.Max(data.nextEggId, maxEggId + 1));

            return colony;
        }

        private static EventStateSaveData EventsToSaveData(EventState events)
        {
            var data = new EventStateSaveData
            {
                summarySeen = events.LastSummarySeen,
                noticeNumberPlusOne = events.NoticeNumber + 1,
                noticeStage = events.NoticeStage,
            };
            var entry = events.Entry;
            if (entry != null)
            {
                data.hasEntry = true;
                data.entry = new EventEntrySaveData
                {
                    number = entry.Number,
                    feePaid = entry.FeePaid,
                    frozen = entry.Frozen,
                    listingSeed = entry.ListingSeed,
                    nextOfferId = entry.NextOfferId,
                };
                foreach (var l in entry.Listings)
                {
                    data.entry.listings.Add(new EventListingSaveData
                    {
                        animalId = l.AnimalId,
                        price = l.Price,
                        tolerancePercent = l.TolerancePercent,
                        frozenBase = l.FrozenBase,
                        frozenChance = l.FrozenChance,
                        soldInActionPlusOne = l.SoldInAction + 1,
                    });
                }

                foreach (var a in entry.Actions)
                {
                    var action = new EventActionSaveData
                    {
                        index = a.Index,
                        kind = a.Kind.ToString(),
                        playedAtUtc = Format(a.PlayedAtUtc),
                        boothMinuteOfDay = a.BoothMinuteOfDay,
                    };
                    foreach (var line in a.Sales)
                    {
                        action.sales.Add(LineToSaveData(line));
                    }

                    data.entry.actions.Add(action);
                }

                foreach (var o in entry.BoothOffers)
                {
                    data.entry.boothOffers.Add(new EventBoothOfferSaveData
                    {
                        offerId = o.OfferId,
                        actionIndex = o.ActionIndex,
                        hasAnimal = o.Animal != null,
                        animal = o.Animal != null ? ToAnimalSaveData(o.Animal) : null,
                        morphLabel = o.MorphLabel,
                        price = o.Price,
                        rare = o.Rare,
                        bought = o.Bought,
                    });
                }

                foreach (var line in entry.Purchases)
                {
                    data.entry.purchases.Add(LineToSaveData(line));
                }

                foreach (var line in entry.Withdrawn)
                {
                    data.entry.withdrawn.Add(LineToSaveData(line));
                }
            }

            var summary = events.LastSummary;
            if (summary != null)
            {
                data.hasSummary = true;
                data.summary = new EventSummarySaveData { number = summary.Number, boothFee = summary.BoothFee };
                foreach (var line in summary.Sold)
                {
                    data.summary.sold.Add(LineToSaveData(line));
                }

                foreach (var line in summary.Unsold)
                {
                    data.summary.unsold.Add(LineToSaveData(line));
                }

                foreach (var line in summary.Bought)
                {
                    data.summary.bought.Add(LineToSaveData(line));
                }
            }

            return data;
        }

        private static EventAnimalLineSaveData LineToSaveData(EventAnimalLine line) => new EventAnimalLineSaveData
        {
            animalId = line.AnimalId,
            name = line.Name,
            morphLabel = line.MorphLabel,
            amount = line.Amount,
            minuteOfDay = line.MinuteOfDay,
        };

        private static EventAnimalLine LineFromSaveData(EventAnimalLineSaveData d) => new EventAnimalLine
        {
            AnimalId = d.animalId,
            Name = d.name ?? string.Empty,
            MorphLabel = d.morphLabel ?? string.Empty,
            Amount = d.amount,
            MinuteOfDay = d.minuteOfDay,
        };

        private static List<EventAnimalLine> LinesFromSaveData(List<EventAnimalLineSaveData> list)
        {
            var lines = new List<EventAnimalLine>();
            if (list != null)
            {
                foreach (var d in list)
                {
                    if (d != null)
                    {
                        lines.Add(LineFromSaveData(d));
                    }
                }
            }

            return lines;
        }

        /// <summary>Bad parts are dropped, never thrown (phase 6 decision 23); an old save has the empty default and reads as no entry.</summary>
        private EventState EventsFromSaveData(EventStateSaveData data, Colony colony, DateTimeOffset nowUtc, System.Random random)
        {
            var events = new EventState();
            if (data == null)
            {
                return events;
            }

            events.NoticeNumber = data.noticeNumberPlusOne - 1;
            if (events.NoticeNumber < -1)
            {
                events.NoticeNumber = -1;
            }

            events.NoticeStage = Math.Max(0, Math.Min(2, data.noticeStage));
            if (data.hasSummary && data.summary != null)
            {
                events.LastSummary = new EventSummary
                {
                    Number = data.summary.number,
                    BoothFee = data.summary.boothFee,
                    Sold = LinesFromSaveData(data.summary.sold),
                    Unsold = LinesFromSaveData(data.summary.unsold),
                    Bought = LinesFromSaveData(data.summary.bought),
                };
                events.LastSummarySeen = data.summarySeen;
            }
            else
            {
                events.LastSummarySeen = true;
            }

            if (data.hasEntry && data.entry != null && data.entry.number >= 0)
            {
                events.Entry = EntryFromSaveData(data.entry, colony, nowUtc, random);
            }

            return events;
        }

        private EventEntry EntryFromSaveData(EventEntrySaveData d, Colony colony, DateTimeOffset nowUtc, System.Random random)
        {
            var kind = EventSchedule.KindOf(d.number);
            var entry = new EventEntry
            {
                Number = d.number,
                FeePaid = d.feePaid,
                Frozen = d.frozen,
                ListingSeed = d.listingSeed,
            };

            var seen = new HashSet<int>();
            var maxListings = EventSchedule.MaxListings(kind);
            if (d.listings != null)
            {
                foreach (var l in d.listings)
                {
                    if (l == null)
                    {
                        continue;
                    }

                    if (seen.Contains(l.animalId) || entry.Listings.Count >= maxListings)
                    {
                        continue;
                    }

                    var chance = l.frozenChance;
                    if (!d.frozen)
                    {
                        if (colony.AnimalById(l.animalId) == null
                            || l.price < EventService.MinPrice
                            || !EventOdds.IsValidTolerance(l.tolerancePercent))
                        {
                            continue;
                        }
                    }
                    else if (double.IsNaN(chance) || chance < 0d || chance > 1d)
                    {
                        // Bad chance: recompute it from the frozen base value, or drop the listing when that is unusable.
                        if (l.frozenBase <= 0 || !EventOdds.IsValidTolerance(l.tolerancePercent))
                        {
                            continue;
                        }

                        chance = EventOdds.SaleChance(l.price, l.tolerancePercent, l.frozenBase, kind);
                    }

                    seen.Add(l.animalId);
                    entry.Listings.Add(new EventListing
                    {
                        AnimalId = l.animalId,
                        Price = l.price,
                        TolerancePercent = l.tolerancePercent,
                        FrozenBase = l.frozenBase,
                        FrozenChance = chance,
                        SoldInAction = System.Math.Max(-1, System.Math.Min(l.soldInActionPlusOne - 1, EventSchedule.ActionCount(kind) - 1)),
                    });
                }
            }

            if (d.actions != null)
            {
                var actionCount = EventSchedule.ActionCount(kind);
                foreach (var a in d.actions)
                {
                    if (a == null || entry.Actions.Count >= actionCount)
                    {
                        continue;
                    }

                    entry.Actions.Add(new EventActionRecord
                    {
                        Index = entry.Actions.Count,
                        Kind = TryParseDefined(a.kind, out EventActionKind actionKind) ? actionKind : EventActionKind.Serve,
                        PlayedAtUtc = TryParseTime(a.playedAtUtc) ?? nowUtc,
                        BoothMinuteOfDay = a.boothMinuteOfDay,
                        Sales = LinesFromSaveData(a.sales),
                    });
                }
            }

            if (d.boothOffers != null)
            {
                foreach (var o in d.boothOffers)
                {
                    if (o == null)
                    {
                        continue;
                    }

                    PetState animal = null;
                    if (o.hasAnimal && o.animal != null)
                    {
                        try
                        {
                            animal = FromAnimalSaveData(o.animal, nowUtc, random, owned: false);
                        }
                        catch (Exception)
                        {
                            animal = null;
                        }
                    }

                    if (animal == null && !o.bought)
                    {
                        continue;
                    }

                    entry.BoothOffers.Add(new EventBoothOffer
                    {
                        OfferId = o.offerId,
                        ActionIndex = o.actionIndex,
                        Animal = o.bought ? null : animal,
                        MorphLabel = o.morphLabel ?? string.Empty,
                        Price = o.price,
                        Rare = o.rare,
                        Bought = o.bought,
                    });
                }
            }

            var maxOfferId = 0;
            foreach (var o in entry.BoothOffers)
            {
                maxOfferId = Math.Max(maxOfferId, o.OfferId);
            }

            entry.NextOfferId = Math.Max(1, Math.Max(d.nextOfferId, maxOfferId + 1));
            entry.Purchases = LinesFromSaveData(d.purchases);
            entry.Withdrawn = LinesFromSaveData(d.withdrawn);
            return entry;
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
                Sex = TryParseDefined(a.sex, out Sex sex) ? sex : Sex.Female,
                WeightGrams = a.weightGrams,
                HatchedAtUtc = Parse(a.hatchedAtUtc, nowUtc),
                Stage = TryParseDefined(a.stage, out GrowthStage stage) ? stage : GrowthStage.Baby,
                StageUpDueAtUtc = string.IsNullOrEmpty(a.stageUpDueAtUtc) ? (DateTimeOffset?)null : Parse(a.stageUpDueAtUtc, nowUtc),
                Hunger = a.hunger,
                Hydration = a.hydration,
                Cleanliness = a.cleanliness,
                Health = a.health,
                LastSavedAtUtc = Parse(a.lastSavedAtUtc, nowUtc),
                LastShedAtUtc = Parse(a.lastShedAtUtc, nowUtc),
                NextShedAtUtc = Parse(a.nextShedAtUtc, nowUtc),
                SexRevealed = a.sexRevealed,
                Weak = a.weak,
            };

            if (a.genomeVersion >= 1)
            {
                pet.Genotype = GenotypeFrom(a.genes, a.hypo, a.tangerine);
                pet.Known = KnownFrom(a.hets, a.hetsUnknown);
                pet.Personality = TryParseDefined(a.personality, out Personality personality) ? personality : PersonalityTraits.Roll(random);
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

            pet.MotherId = a.motherId;
            pet.FatherId = a.fatherId;
            pet.CareGraceUntilUtc = TryParseTime(a.careGraceUntilUtc);

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
                newRackIndices = new List<int>(colony.NewRackIndices),
                incubatorCount = colony.IncubatorCount,
                nextAnimalId = colony.NextAnimalId,
                nextCageId = colony.NextCageId,
                lastBilledMonthIndex = colony.LastBilledMonthIndex,
                inventoryVersion = CurrentInventoryVersion,
                shopSeed = colony.Shop.Seed,
                shopStocked = colony.Shop.StockMonthIndex != ShopStock.NeverStocked,
                shopMonthIndex = colony.Shop.StockMonthIndex,
                gameClockOffsetTicks = colony.GameClockOffset.Ticks,
                nextPairingId = colony.NextPairingId,
                nextEggId = colony.NextEggId,
            };
            foreach (var incubator in colony.Incubators)
            {
                data.incubators.Add(incubator.Model.ToString());
                data.incubatorStates.Add(new IncubatorSaveData
                {
                    id = incubator.Id,
                    model = incubator.Model.ToString(),
                    setTemperatureC = incubator.SetTemperatureC,
                    humidityPercent = incubator.HumidityPercent,
                    appliedUntilUtc = Format(incubator.AppliedUntilUtc),
                });
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
                    hasNestBox = c.HasNestBox,
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

            data.events = EventsToSaveData(colony.Events);

            foreach (var p in colony.Pairings)
            {
                data.pairings.Add(new PairingSaveData
                {
                    id = p.Id,
                    maleId = p.MaleId,
                    femaleId = p.FemaleId,
                    startedAtUtc = Format(p.StartedAtUtc),
                    endsAtUtc = Format(p.EndsAtUtc),
                    successChance = p.SuccessChance,
                });
            }

            foreach (var a in colony.Animals)
            {
                if (a.Gravid == null)
                {
                    continue;
                }

                var g = a.Gravid;
                data.gravidStates.Add(new GravidSaveData
                {
                    animalId = a.Id,
                    pairingId = g.PairingId,
                    fatherId = g.FatherId,
                    fatherName = g.FatherName,
                    fatherGenes = GenesToSave(g.FatherGenotype),
                    fatherHypo = g.FatherGenotype.Hypo,
                    fatherTangerine = g.FatherGenotype.Tangerine,
                    fatherHets = HetsToSave(g.FatherKnown),
                    fatherHetsUnknown = g.FatherKnown.HetsUnknown,
                    compatibility = g.Compatibility.ToString(),
                    seasonYear = g.SeasonYear,
                    clutchesPlanned = g.ClutchesPlanned,
                    clutchesLaid = g.ClutchesLaid,
                    nextClutchAtUtc = Format(g.NextClutchAtUtc),
                });
            }

            foreach (var e in colony.Eggs)
            {
                data.eggs.Add(new EggSaveData
                {
                    id = e.Id,
                    motherId = e.MotherId,
                    fatherId = e.FatherId,
                    cageId = e.CageId,
                    laidAtUtc = Format(e.LaidAtUtc),
                    place = e.Place.ToString(),
                    fertile = e.Fertile,
                    genes = GenesToSave(e.ChildGenotype),
                    hypo = e.ChildGenotype.Hypo,
                    tangerine = e.ChildGenotype.Tangerine,
                    hets = HetsToSave(e.ChildKnown),
                    hetsUnknown = e.ChildKnown.HetsUnknown,
                    developmentPercent = e.DevelopmentPercent,
                    middleThirdTemperatureSum = e.MiddleThirdTemperatureSum,
                    middleThirdGameDays = e.MiddleThirdGameDays,
                    coldGameDays = e.ColdGameDays,
                    failure = e.Failure.ToString(),
                    appliedUntilUtc = Format(e.AppliedUntilUtc),
                    incubatorId = e.IncubatorId,
                });
            }

            foreach (var r in colony.Lineage)
            {
                data.lineage.Add(new LineageSaveData
                {
                    id = r.Id,
                    name = r.Name,
                    morphName = r.MorphName,
                    sex = r.Sex.ToString(),
                    sexKnown = r.SexKnown,
                    motherId = r.MotherId,
                    fatherId = r.FatherId,
                });
            }

            return data;
        }

        private static AnimalSaveData ToAnimalSaveData(PetState a)
        {
            var genes = GenesToSave(a.Genotype);
            var hets = HetsToSave(a.Known);

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
                weak = a.Weak,
                motherId = a.MotherId,
                fatherId = a.FatherId,
                careGraceUntilUtc = a.CareGraceUntilUtc.HasValue ? Format(a.CareGraceUntilUtc.Value) : string.Empty,
            };
        }

        private static List<GeneSaveData> GenesToSave(Genotype genotype)
        {
            var genes = new List<GeneSaveData>();
            foreach (var gene in Genes.All)
            {
                var copies = genotype.Copies(gene);
                if (copies >= 1)
                {
                    genes.Add(new GeneSaveData { gene = gene.ToString(), copies = copies });
                }
            }

            return genes;
        }

        private static List<HetSaveData> HetsToSave(KnownGenetics known)
        {
            var hets = new List<HetSaveData>();
            foreach (var gene in Genes.All)
            {
                var probability = known.HetProbability(gene);
                if (probability > 0d)
                {
                    hets.Add(new HetSaveData { gene = gene.ToString(), probability = probability });
                }
            }

            return hets;
        }

        private static Genotype GenotypeFrom(List<GeneSaveData> genes, double hypo, double tangerine)
        {
            var genotype = Genotype.Normal(hypo, tangerine);
            if (genes != null)
            {
                foreach (var g in genes)
                {
                    if (TryParseDefined(g.gene, out GeneId geneId))
                    {
                        genotype.Set(geneId, g.copies);
                    }
                }
            }

            return genotype;
        }

        private static KnownGenetics KnownFrom(List<HetSaveData> hets, bool hetsUnknown)
        {
            var known = new KnownGenetics { HetsUnknown = hetsUnknown };
            if (hets != null)
            {
                foreach (var h in hets)
                {
                    if (TryParseDefined(h.gene, out GeneId geneId))
                    {
                        known.SetHet(geneId, h.probability);
                    }
                }
            }

            return known;
        }

        private static string Format(DateTimeOffset value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        /// <summary>A timestamp that may be empty or damaged: null instead of failing the whole save.</summary>
        private static DateTimeOffset? TryParseTime(string value) =>
            !string.IsNullOrEmpty(value) && DateTimeOffset.TryParseExact(value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;

        private static DateTimeOffset Parse(string value, DateTimeOffset fallback) =>
            string.IsNullOrEmpty(value) ? fallback : DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture);

        /// <summary>
        /// Enum.TryParse also accepts any number ("99"), which would index past the gene and
        /// personality tables; only defined names/values count.
        /// </summary>
        private static bool TryParseDefined<T>(string value, out T result) where T : struct
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, out result) && Enum.IsDefined(typeof(T), result))
            {
                return true;
            }

            result = default;
            return false;
        }
    }
}
