using System;
using System.Collections.Generic;
using System.Linq;
using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// The events tab (phase 6 spec §2.1–2.2): shows exactly one of the preparation, event-day and
    /// result surfaces. This class draws the preparation surface; the other two are filled by
    /// later tasks. The values being picked (check, price, tolerance) live here and are only
    /// reset by <see cref="Invalidate"/> or when the event changes, never by a per-frame Render.
    /// Actions leave as events; TerrariumView applies them, saves and re-renders.
    /// </summary>
    public sealed partial class EventView
    {
        public const long PriceStep = 1000L;
        public const long MinListingPrice = 1000L;

        private sealed class Pick
        {
            public bool On;
            public long Price;
            public int ToleranceIndex;
        }

        private sealed class Row
        {
            public PetState Pet;
            public ListingProblem Problem;
            public Button Check;
            public Label Price;
            public Button Tolerance;
            public Label Chance;
        }

        private readonly VisualElement prep;
        private readonly VisualElement day;
        private readonly VisualElement result;
        private readonly Label title;
        private readonly Label info;
        private readonly ScrollView list;
        private readonly Label total;
        private readonly Label count;
        private readonly Label message;
        private readonly Button saveButton;
        private readonly Button registerButton;

        private readonly Dictionary<int, Pick> picks = new Dictionary<int, Pick>();
        private readonly List<Row> rows = new List<Row>();

        private string lastSignature;
        private int shownNumber = -1;
        private bool shownEntered;

        private Colony colony;
        private EventService events;
        private int number = -1;
        private int max;

        public EventView(VisualElement root, VisualElement modalRoot = null)
        {
            prep = root.Q<VisualElement>("event-prep");
            day = root.Q<VisualElement>("event-day");
            result = root.Q<VisualElement>("event-result");
            title = root.Q<Label>("event-prep-title");
            info = root.Q<Label>("event-prep-info");
            list = root.Q<ScrollView>("event-prep-list");
            total = root.Q<Label>("event-prep-total");
            count = root.Q<Label>("event-prep-count");
            message = root.Q<Label>("event-prep-message");
            saveButton = root.Q<Button>("event-prep-save-button");
            registerButton = root.Q<Button>("event-prep-register-button");

            if (registerButton != null)
            {
                registerButton.clicked += () => RegisterRequested?.Invoke(number, BuildRequests());
            }

            if (saveButton != null)
            {
                saveButton.clicked += () => SaveRequested?.Invoke(BuildRequests());
            }

            InitDay(root, modalRoot ?? root);
        }

        public event Action<int, List<ListingRequest>> RegisterRequested;

        public event Action<List<ListingRequest>> SaveRequested;

        /// <summary>Forgets the picks (back to the defaults) and forces a redraw.</summary>
        public void Invalidate()
        {
            picks.Clear();
            lastSignature = null;
            daySignature = null;
            logSignature = null;
            casesSignature = null;
            resultSignature = null;
        }

        public void ShowMessage(string text)
        {
            if (message != null)
            {
                message.text = text ?? string.Empty;
            }
        }

        public void Render(ColonySession session, DateTimeOffset nowUtc)
        {
            if (prep == null || session == null)
            {
                return;
            }

            colony = session.Colony;
            events = session.Events;
            var calendar = session.Calendar;
            var entry = colony.Events.Entry;

            sessionRef = session;
            if (Playing && (replayEntry == null || !replayEntry.Actions.Contains(replayRecord)))
            {
                EndReplay();
            }

            var showResult = !Playing && colony.Events.LastSummary != null && !colony.Events.LastSummarySeen;
            var showDay = !showResult && (Playing || (entry != null && EventSchedule.HasStarted(calendar, entry.Number, nowUtc)));
            SetDisplay(result, showResult);
            SetDisplay(day, showDay);
            SetDisplay(prep, !showResult && !showDay);
            if (showResult)
            {
                RenderResult(colony.Events.LastSummary);
                UpdateBuyModal();
                return;
            }

            if (showDay)
            {
                RefreshDay();
                return;
            }

            number = entry != null ? entry.Number : events.OpenNumber(colony, calendar, nowUtc);
            var entered = entry != null;
            if (number != shownNumber || entered != shownEntered)
            {
                picks.Clear();
                lastSignature = null;
                shownNumber = number;
                shownEntered = entered;
            }

            if (number < 0)
            {
                title.text = string.Empty;
                info.text = string.Empty;
                total.text = string.Empty;
                count.text = string.Empty;
                list.Clear();
                rows.Clear();
                SetDisplay(saveButton, false);
                SetDisplay(registerButton, false);
                return;
            }

            var kind = EventSchedule.KindOf(number);
            max = EventSchedule.MaxListings(kind);
            title.text = $"{EventSchedule.NameOf(number)} {EventText.DateLabel(number)}" + (entered ? "（申し込み済み）" : string.Empty);
            info.text = $"出店料 {EventText.Yen(EventSchedule.BoothFee(kind))}　出品は最大{max}匹　締め切り {EventSchedule.MonthOf(number)}月{EventSchedule.DeadlineDayOfMonth}日"
                + $"\n所持金 {EventText.Yen(colony.Wallet.Money)}"
                + (entered ? "\n申し込み済み（出店料は返りません）" : string.Empty);
            SetDisplay(saveButton, entered);
            SetDisplay(registerButton, !entered);

            var pets = colony.Animals.OrderBy(p => p.Id).ToList();
            var signature = number + "|" + (entered ? 1 : 0) + "|" + string.Join(",", pets.Select(p => p.Id + ":" + (int)EventRules.Check(colony, p)));
            if (signature != lastSignature)
            {
                Rebuild(pets, entry);
                lastSignature = signature;
            }

            Refresh();
        }

        private static void SetDisplay(VisualElement element, bool show)
        {
            if (element != null)
            {
                element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private Pick PickOf(PetState pet, EventEntry entry)
        {
            if (picks.TryGetValue(pet.Id, out var pick))
            {
                return pick;
            }

            var listing = entry?.ListingOf(pet.Id);
            if (listing != null)
            {
                pick = new Pick
                {
                    On = true,
                    Price = listing.Price,
                    ToleranceIndex = Math.Max(0, Array.IndexOf(EventOdds.TolerancePercents, listing.TolerancePercent))
                };
            }
            else
            {
                var rounded = (long)Math.Round(events.BaseValueOf(colony, pet, number) / (double)PriceStep, MidpointRounding.AwayFromZero) * PriceStep;
                pick = new Pick { Price = Math.Max(MinListingPrice, rounded) };
            }

            picks[pet.Id] = pick;
            return pick;
        }

        private void Rebuild(List<PetState> pets, EventEntry entry)
        {
            list.Clear();
            rows.Clear();
            foreach (var pet in pets)
            {
                var pick = PickOf(pet, entry);
                var problem = EventRules.Check(colony, pet);
                var row = new Row { Pet = pet, Problem = problem };
                var container = new VisualElement { name = $"event-row-{pet.Id}" };
                container.AddToClassList("event-listing-row");

                var captured = pet;
                row.Check = new Button(() =>
                {
                    var current = PickOf(captured, null);
                    current.On = !current.On;
                    Refresh();
                })
                { name = $"event-check-{pet.Id}" };
                row.Check.AddToClassList("event-listing-check");
                container.Add(row.Check);

                var name = new Label($"{pet.Name} {EventRules.MorphLabel(pet)}") { name = $"event-name-{pet.Id}" };
                name.AddToClassList("event-listing-name");
                container.Add(name);

                var minus = new Button(() => ChangePrice(captured, -PriceStep)) { name = $"event-minus-{pet.Id}", text = "-" };
                minus.AddToClassList("event-listing-minus");
                container.Add(minus);
                row.Price = new Label { name = $"event-price-{pet.Id}" };
                row.Price.AddToClassList("event-listing-price");
                container.Add(row.Price);
                var plus = new Button(() => ChangePrice(captured, PriceStep)) { name = $"event-plus-{pet.Id}", text = "+" };
                plus.AddToClassList("event-listing-plus");
                container.Add(plus);

                row.Tolerance = new Button(() => CycleTolerance(captured)) { name = $"event-tol-{pet.Id}" };
                row.Tolerance.AddToClassList("event-listing-tolerance");
                container.Add(row.Tolerance);
                row.Chance = new Label { name = $"event-chance-{pet.Id}" };
                row.Chance.AddToClassList("event-listing-chance");
                container.Add(row.Chance);

                if (problem != ListingProblem.None)
                {
                    var reason = new Label(EventText.ListingReason(problem)) { name = $"event-reason-{pet.Id}" };
                    reason.AddToClassList("event-listing-reason");
                    container.Add(reason);
                }

                rows.Add(row);
                list.Add(container);
            }
        }

        private void ChangePrice(PetState pet, long delta)
        {
            var pick = PickOf(pet, null);
            pick.Price = Math.Max(MinListingPrice, pick.Price + delta);
            Refresh();
        }

        private void CycleTolerance(PetState pet)
        {
            var pick = PickOf(pet, null);
            pick.ToleranceIndex = (pick.ToleranceIndex + 1) % EventOdds.TolerancePercents.Length;
            Refresh();
        }

        /// <summary>Redraws texts, enabled states, the chances (recomputed every time, ★N) and the totals from the picks.</summary>
        private void Refresh()
        {
            var checkedCount = rows.Count(r => r.Problem == ListingProblem.None && PickOf(r.Pet, null).On);
            double sum = 0d;
            foreach (var row in rows)
            {
                var pick = PickOf(row.Pet, null);
                var tolerance = EventOdds.TolerancePercents[pick.ToleranceIndex];
                var chance = events.ChanceOf(colony, row.Pet, number, pick.Price, tolerance);
                row.Price.text = EventText.Yen(pick.Price);
                row.Tolerance.text = EventText.Tolerance(tolerance);
                row.Chance.text = EventText.Chance(chance);
                var eligible = row.Problem == ListingProblem.None;
                var on = eligible && pick.On;
                row.Check.text = on ? "■" : "□";
                row.Check.SetEnabled(eligible && (on || checkedCount < max));
                if (on)
                {
                    sum += EventOdds.ExpectedValue(pick.Price, tolerance, chance);
                }
            }

            total.text = EventText.ExpectedTotal(sum);
            count.text = EventText.Listings(checkedCount, max);
        }

        private List<ListingRequest> BuildRequests()
        {
            return rows
                .Where(r => r.Problem == ListingProblem.None && PickOf(r.Pet, null).On)
                .Select(r =>
                {
                    var pick = PickOf(r.Pet, null);
                    return new ListingRequest
                    {
                        AnimalId = r.Pet.Id,
                        Price = pick.Price,
                        TolerancePercent = EventOdds.TolerancePercents[pick.ToleranceIndex]
                    };
                })
                .ToList();
        }
    }
}
