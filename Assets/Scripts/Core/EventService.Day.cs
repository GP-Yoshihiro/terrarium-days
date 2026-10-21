using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum EventActionResult
    {
        Ok,
        NoEvent,
        NotYet,
        NoActionsLeft
    }

    public enum EventBuyResult
    {
        Ok,
        NoEvent,
        NotFound,
        AlreadyBought,
        NotEnoughMoney,
        NoEmptyCage
    }

    /// <summary>
    /// The event day (phase 6 spec §2.3–2.7): each action is decided by its own seed, so the same
    /// choices give the same results whenever they are played; the ledger uses the play time (★R).
    /// </summary>
    public sealed partial class EventService
    {
        public int ActionsLeft(Colony colony)
        {
            var entry = colony.Events.Entry;
            return entry == null ? 0 : Math.Max(0, ActionCountOf(entry) - entry.Actions.Count);
        }

        public EventActionResult PlayAction(Colony colony, GameCalendar calendar, EventActionKind kind, DateTimeOffset playedAtUtc, out EventActionRecord record)
        {
            record = null;
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventActionResult.NoEvent;
            }

            if (!EventSchedule.HasStarted(calendar, entry.Number, playedAtUtc))
            {
                return EventActionResult.NotYet;
            }

            var count = ActionCountOf(entry);
            if (entry.Actions.Count >= count)
            {
                return EventActionResult.NoActionsLeft;
            }

            if (!entry.Frozen)
            {
                // Advance normally froze it at 0:00; this covers a caller that skipped it.
                Freeze(colony, entry, new EventReport());
            }

            var index = entry.Actions.Count;
            record = new EventActionRecord { Index = index, Kind = kind, PlayedAtUtc = playedAtUtc };
            var windowStart = EventSchedule.ActionWindowStart(index, count);
            var windowLength = EventSchedule.ActionWindowLength(count);
            var eventName = EventSchedule.NameOf(entry.Number);
            var random = EventRandom.ForAction(entry.ListingSeed, index, kind);
            var listings = new List<EventListing>(entry.Listings);
            listings.Sort((a, b) => a.AnimalId.CompareTo(b.AnimalId));
            foreach (var listing in listings)
            {
                // Always three draws per listing, so one listing's result never shifts another's.
                var saleRoll = random.NextDouble();
                var discountRoll = random.NextDouble();
                var minuteRoll = random.NextDouble();
                if (listing.Sold || saleRoll >= EventOdds.ActionChance(listing.FrozenChance, count, kind))
                {
                    continue;
                }

                var pet = colony.AnimalById(listing.AnimalId);
                if (pet == null)
                {
                    continue;
                }

                var amount = EventOdds.SaleAmount(listing.Price, listing.TolerancePercent, discountRoll);
                var note = $"{eventName}：{pet.Name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）が売れました";
                listing.SoldInAction = index;
                record.Sales.Add(new EventAnimalLine
                {
                    AnimalId = pet.Id,
                    Name = pet.Name,
                    MorphLabel = EventRules.MorphLabel(pet),
                    Amount = amount,
                    MinuteOfDay = windowStart + (int)(minuteRoll * windowLength),
                });
                colony.RemoveAnimal(pet);
                colony.Wallet.Earn(amount, LedgerCategory.EventSale, note, playedAtUtc);
            }

            record.Sales.Sort((a, b) => a.MinuteOfDay != b.MinuteOfDay ? a.MinuteOfDay.CompareTo(b.MinuteOfDay) : a.AnimalId.CompareTo(b.AnimalId));

            if (kind == EventActionKind.Browse)
            {
                var offers = BoothStockGenerator.Generate(EventRandom.ForBooth(entry.ListingSeed, index), EventSchedule.KindOf(entry.Number),
                    EventRandom.DemandSeed(colony.BreedingSeed, entry.Number), playedAtUtc, care, entry.NextOfferId, index);
                entry.NextOfferId += offers.Count;
                entry.BoothOffers.AddRange(offers);
                record.BoothMinuteOfDay = windowStart + windowLength / 2;
            }

            entry.Actions.Add(record);
            FinishIfDone(colony, entry);
            return EventActionResult.Ok;
        }

        /// <summary>「残りを全部接客にする」: the remaining actions as serves, then the event is finished even with unbought offers (spec §2.3・§2.7).</summary>
        public EventActionResult ServeAllRemaining(Colony colony, GameCalendar calendar, DateTimeOffset playedAtUtc, out List<EventActionRecord> records)
        {
            records = new List<EventActionRecord>();
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventActionResult.NoEvent;
            }

            if (!EventSchedule.HasStarted(calendar, entry.Number, playedAtUtc))
            {
                return EventActionResult.NotYet;
            }

            while (colony.Events.Entry == entry && ActionsLeft(colony) > 0)
            {
                PlayAction(colony, calendar, EventActionKind.Serve, playedAtUtc, out var record);
                records.Add(record);
            }

            if (colony.Events.Entry == entry)
            {
                Finish(colony);
            }

            return EventActionResult.Ok;
        }

        /// <summary>The buy dialog's check: the cage before the money, so a full room says 「ケージが足りません」 (spec §2.5).</summary>
        public EventBuyResult CheckBuy(Colony colony, int offerId)
        {
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventBuyResult.NoEvent;
            }

            var offer = entry.BoothOffers.Find(o => o.OfferId == offerId);
            if (offer == null)
            {
                return EventBuyResult.NotFound;
            }

            if (offer.Bought || offer.Animal == null)
            {
                return EventBuyResult.AlreadyBought;
            }

            if (colony.FirstEmptyCage() == null)
            {
                return EventBuyResult.NoEmptyCage;
            }

            return colony.Wallet.Money < offer.Price ? EventBuyResult.NotEnoughMoney : EventBuyResult.Ok;
        }

        /// <summary>Like the shop (★8): named レオパN, personality shown after 7 game days, into the lowest-numbered empty cage.</summary>
        public EventBuyResult Buy(Colony colony, int offerId, DateTimeOffset nowUtc)
        {
            var problem = CheckBuy(colony, offerId);
            if (problem != EventBuyResult.Ok)
            {
                return problem;
            }

            var entry = colony.Events.Entry;
            var offer = entry.BoothOffers.Find(o => o.OfferId == offerId);
            var pet = offer.Animal;
            var name = $"レオパ{colony.NextAnimalId}";
            var note = $"{EventSchedule.NameOf(entry.Number)}：{name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）を購入";
            if (!colony.Wallet.TrySpend(offer.Price, LedgerCategory.AnimalPurchase, note, nowUtc))
            {
                return EventBuyResult.NotEnoughMoney;
            }

            pet.Name = name;
            pet.LastSavedAtUtc = nowUtc;
            pet.LastShedAtUtc = nowUtc;
            pet.NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(pet.Stage, care);
            pet.PersonalityKnown = false;
            pet.PersonalityRevealAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays);
            colony.AddAnimal(pet, colony.FirstEmptyCage());
            offer.Animal = null;
            offer.Bought = true;
            entry.Purchases.Add(new EventAnimalLine { AnimalId = pet.Id, Name = name, MorphLabel = offer.MorphLabel, Amount = offer.Price });
            FinishIfDone(colony, entry);
            return EventBuyResult.Ok;
        }

        /// <summary>「集計へ」: allowed once every action is used. Makes the summary and opens the next event.</summary>
        public bool Finish(Colony colony)
        {
            var entry = colony.Events.Entry;
            if (entry == null || entry.Actions.Count < ActionCountOf(entry))
            {
                return false;
            }

            var summary = new EventSummary { Number = entry.Number, BoothFee = entry.FeePaid };
            foreach (var action in entry.Actions)
            {
                summary.Sold.AddRange(action.Sales);
            }

            foreach (var listing in entry.Listings)
            {
                if (listing.Sold)
                {
                    continue;
                }

                var pet = colony.AnimalById(listing.AnimalId);
                summary.Unsold.Add(new EventAnimalLine
                {
                    AnimalId = listing.AnimalId,
                    Name = pet != null ? pet.Name : string.Empty,
                    MorphLabel = pet != null ? EventRules.MorphLabel(pet) : string.Empty,
                    Amount = listing.Price,
                });
            }

            summary.Unsold.AddRange(entry.Withdrawn);
            summary.Bought.AddRange(entry.Purchases);
            colony.Events.LastSummary = summary;
            colony.Events.LastSummarySeen = false;
            colony.Events.Entry = null;
            return true;
        }

        /// <summary>「閉じる」 on the result screen (★11).</summary>
        public void DismissSummary(Colony colony) => colony.Events.LastSummarySeen = true;

        private static int ActionCountOf(EventEntry entry) => EventSchedule.ActionCount(EventSchedule.KindOf(entry.Number));

        /// <summary>★5: after the last action the event waits while anything is left to buy.</summary>
        private void FinishIfDone(Colony colony, EventEntry entry)
        {
            if (entry.Actions.Count >= ActionCountOf(entry) && !entry.BoothOffers.Exists(o => !o.Bought))
            {
                Finish(colony);
            }
        }
    }
}
