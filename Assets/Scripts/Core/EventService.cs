using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Why a registration or a listing change was refused. Any problem refuses the whole request (planner).</summary>
    public enum EventRegisterResult
    {
        Ok,
        NotOpen,
        Pending,
        AlreadyRegistered,
        TooMany,
        NotEligible,
        Duplicate,
        InvalidPrice,
        InvalidTolerance,
        NotEnoughMoney,
        Frozen
    }

    public sealed class ListingRequest
    {
        public int AnimalId { get; set; }
        public long Price { get; set; }
        public int TolerancePercent { get; set; }
    }

    /// <summary>What one <see cref="EventService.Advance"/> found for the notices (★9). -1 = nothing.</summary>
    public sealed class EventReport
    {
        /// <summary>Names of the animals whose listing was taken back (missing, or not eligible at 0:00).</summary>
        public List<string> Withdrawn { get; } = new List<string>();

        public int DeadlineNoticeNumber { get; set; } = -1;

        /// <summary>3 for the three-days notice, 1 for the day-before notice.</summary>
        public int DeadlineDaysLeft { get; set; }

        /// <summary>The event whose day just began while the app was open.</summary>
        public int DayStartedNumber { get; set; } = -1;

        /// <summary>Set by the session on load: an entered event whose day is over but not played to the end.</summary>
        public int PendingNumber { get; set; } = -1;

        public bool HasEvents => Withdrawn.Count > 0 || DeadlineNoticeNumber >= 0 || DayStartedNumber >= 0 || PendingNumber >= 0;
    }

    /// <summary>
    /// Entering events (phase 6 spec §2.1–2.2): register with listings and pay the fee, change the
    /// listings until the day before, freeze everything at 0:00 on the day, and the deadline notices.
    /// The day itself is in EventService.Day.cs. Pure: the caller passes the game time.
    /// </summary>
    public sealed partial class EventService
    {
        public const long MinPrice = 100L;

        private readonly CareTuning care;

        public EventService(CareTuning care)
        {
            this.care = care;
        }

        public CareTuning Care => care;

        /// <summary>The event that takes entries now, or -1 while an entered event is not finished (★M・★Q).</summary>
        public int OpenNumber(Colony colony, GameCalendar calendar, DateTimeOffset nowUtc) =>
            colony.Events.Entry != null ? -1 : EventSchedule.NextOpenNumber(calendar, nowUtc);

        public long BaseValueOf(Colony colony, PetState pet, int number) =>
            EventRules.BaseValue(pet, EventRandom.DemandSeed(colony.BreedingSeed, number), EventSchedule.KindOf(number));

        /// <summary>The whole-event sale chance P for this price and tolerance (the preparation screen recomputes it until 0:00, ★N).</summary>
        public double ChanceOf(Colony colony, PetState pet, int number, long price, int tolerancePercent) =>
            EventOdds.SaleChance(price, tolerancePercent, BaseValueOf(colony, pet, number), EventSchedule.KindOf(number));

        public EventRegisterResult Register(Colony colony, GameCalendar calendar, int number, IList<ListingRequest> requests, DateTimeOffset nowUtc)
        {
            requests = requests ?? new List<ListingRequest>();
            var entry = colony.Events.Entry;
            if (entry != null)
            {
                return entry.Number == number ? EventRegisterResult.AlreadyRegistered : EventRegisterResult.Pending;
            }

            if (number != EventSchedule.NextOpenNumber(calendar, nowUtc))
            {
                return EventRegisterResult.NotOpen;
            }

            var problem = Validate(colony, number, requests);
            if (problem != EventRegisterResult.Ok)
            {
                return problem;
            }

            var fee = EventSchedule.BoothFee(EventSchedule.KindOf(number));
            if (!colony.Wallet.TrySpend(fee, LedgerCategory.BoothFee, $"{EventSchedule.NameOf(number)}の出店料", nowUtc))
            {
                return EventRegisterResult.NotEnoughMoney;
            }

            entry = new EventEntry { Number = number, FeePaid = fee };
            SetListings(entry, requests);
            colony.Events.Entry = entry;
            return EventRegisterResult.Ok;
        }

        /// <summary>Replaces the listings until the day before (★B). The fee is never refunded; 0 listings is allowed (★2).</summary>
        public EventRegisterResult UpdateListings(Colony colony, GameCalendar calendar, IList<ListingRequest> requests, DateTimeOffset nowUtc)
        {
            requests = requests ?? new List<ListingRequest>();
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventRegisterResult.NotOpen;
            }

            if (entry.Frozen || EventSchedule.HasStarted(calendar, entry.Number, nowUtc))
            {
                return EventRegisterResult.Frozen;
            }

            var problem = Validate(colony, entry.Number, requests);
            if (problem != EventRegisterResult.Ok)
            {
                return problem;
            }

            SetListings(entry, requests);
            return EventRegisterResult.Ok;
        }

        /// <summary>The entered event if its day is over and it is still not played to the end, else -1.</summary>
        public int PendingNumber(Colony colony, GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var entry = colony.Events.Entry;
            return entry != null && nowUtc >= EventSchedule.EndUtc(calendar, entry.Number) ? entry.Number : -1;
        }

        /// <summary>Called by the session after every catch-up: drops missing animals, freezes at 0:00, counts the deadline notices.</summary>
        public EventReport Advance(Colony colony, GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var report = new EventReport();
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                UpdateDeadlineNotice(colony.Events, calendar, nowUtc, report);
                return report;
            }

            DropMissing(colony, entry, report);
            if (!entry.Frozen && EventSchedule.HasStarted(calendar, entry.Number, nowUtc))
            {
                Freeze(colony, entry, report);
                if (nowUtc < EventSchedule.EndUtc(calendar, entry.Number))
                {
                    report.DayStartedNumber = entry.Number;
                }
            }

            // A loaded entry can be exhausted with nothing left to buy; finish it so the event cannot stay open forever.
            if (entry.Frozen)
            {
                FinishIfDone(colony, entry);
            }

            return report;
        }

        private static EventRegisterResult Validate(Colony colony, int number, IList<ListingRequest> requests)
        {
            if (requests.Count > EventSchedule.MaxListings(EventSchedule.KindOf(number)))
            {
                return EventRegisterResult.TooMany;
            }

            var seen = new HashSet<int>();
            foreach (var request in requests)
            {
                if (!seen.Add(request.AnimalId))
                {
                    return EventRegisterResult.Duplicate;
                }

                if (EventRules.Check(colony, colony.AnimalById(request.AnimalId)) != ListingProblem.None)
                {
                    return EventRegisterResult.NotEligible;
                }

                if (request.Price < MinPrice)
                {
                    return EventRegisterResult.InvalidPrice;
                }

                if (!EventOdds.IsValidTolerance(request.TolerancePercent))
                {
                    return EventRegisterResult.InvalidTolerance;
                }
            }

            return EventRegisterResult.Ok;
        }

        private static void SetListings(EventEntry entry, IList<ListingRequest> requests)
        {
            entry.Listings = new List<EventListing>();
            foreach (var request in requests)
            {
                entry.Listings.Add(new EventListing { AnimalId = request.AnimalId, Price = request.Price, TolerancePercent = request.TolerancePercent });
            }

            entry.Listings.Sort((a, b) => a.AnimalId.CompareTo(b.AnimalId));
        }

        /// <summary>Unsold listings whose animal is no longer in the room are taken back (planner: checked on every Advance).</summary>
        private static void DropMissing(Colony colony, EventEntry entry, EventReport report)
        {
            for (var i = 0; i < entry.Listings.Count; i++)
            {
                var listing = entry.Listings[i];
                if (listing.Sold || colony.AnimalById(listing.AnimalId) != null)
                {
                    continue;
                }

                report.Withdrawn.Add(NameOf(colony, listing.AnimalId));
                entry.Withdrawn.Add(WithdrawnLine(colony, listing));
                entry.Listings.RemoveAt(i);
                i--;
            }
        }

        /// <summary>0:00 on the day (★N): ineligible animals are taken back, then the base values, chances and seed are fixed.</summary>
        private void Freeze(Colony colony, EventEntry entry, EventReport report)
        {
            var kind = EventSchedule.KindOf(entry.Number);
            var kept = new List<EventListing>();
            foreach (var listing in entry.Listings)
            {
                var pet = colony.AnimalById(listing.AnimalId);
                if (EventRules.Check(colony, pet) != ListingProblem.None)
                {
                    report.Withdrawn.Add(NameOf(colony, listing.AnimalId));
                    entry.Withdrawn.Add(WithdrawnLine(colony, listing));
                    continue;
                }

                listing.FrozenBase = BaseValueOf(colony, pet, entry.Number);
                listing.FrozenChance = EventOdds.SaleChance(listing.Price, listing.TolerancePercent, listing.FrozenBase, kind);
                kept.Add(listing);
            }

            kept.Sort((a, b) => a.AnimalId.CompareTo(b.AnimalId));
            entry.Listings = kept;
            entry.ListingSeed = EventRandom.ListingSeed(colony.BreedingSeed, entry.Number, kept);
            entry.Frozen = true;
        }

        /// <summary>Stage 1 at three days left, stage 2 on the day before; only a higher stage makes a notice (★L・★9).</summary>
        private static void UpdateDeadlineNotice(EventState state, GameCalendar calendar, DateTimeOffset nowUtc, EventReport report)
        {
            var number = EventSchedule.NextOpenNumber(calendar, nowUtc);
            if (state.NoticeNumber != number)
            {
                state.NoticeNumber = number;
                state.NoticeStage = 0;
            }

            var daysLeft = EventSchedule.GameDaysUntilStart(calendar, number, nowUtc);
            var stage = daysLeft <= 1d ? 2 : daysLeft <= 3d ? 1 : 0;
            if (stage <= state.NoticeStage)
            {
                return;
            }

            state.NoticeStage = stage;
            report.DeadlineNoticeNumber = number;
            report.DeadlineDaysLeft = stage == 2 ? 1 : 3;
        }

        private static EventAnimalLine WithdrawnLine(Colony colony, EventListing listing)
        {
            var pet = colony.AnimalById(listing.AnimalId);
            return new EventAnimalLine
            {
                AnimalId = listing.AnimalId,
                Name = NameOf(colony, listing.AnimalId),
                MorphLabel = pet != null ? EventRules.MorphLabel(pet) : string.Empty,
                Amount = listing.Price,
            };
        }

        private static string NameOf(Colony colony, int animalId)
        {
            var pet = colony.AnimalById(animalId);
            if (pet != null)
            {
                return pet.Name;
            }

            var record = colony.Lineage.Find(r => r.Id == animalId);
            return record != null ? record.Name : $"#{animalId}";
        }
    }
}
