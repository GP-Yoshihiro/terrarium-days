using System;
using System.Collections.Generic;
using System.Linq;
using TerrariumDays.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// The event-day and result surfaces (phase 6 spec §2.3–2.7). An action's result is decided
    /// and saved before its replay starts; the replay only reveals log lines, so skipping it or
    /// closing the app changes nothing (★12).
    /// </summary>
    public sealed partial class EventView
    {
        public const float FrameSeconds = 0.25f;

        private ColonySession sessionRef;

        private Label dayTitle;
        private Label dayLeft;
        private VisualElement stage;
        private VisualElement cases;
        private VisualElement visitor;
        private VisualElement bubble;
        private Label bubbleLabel;
        private VisualElement offersBox;
        private Button serveButton;
        private Button browseButton;
        private Button serveAllButton;
        private Button skipButton;
        private Button finishButton;
        private ScrollView log;

        private Label resultTitle;
        private VisualElement resultSold;
        private VisualElement resultUnsold;
        private VisualElement resultBought;
        private Label resultTotals;
        private Button resultClose;

        private VisualElement buyModal;
        private Label buyMessage;
        private Button buyYes;
        private Button buyNo;
        private int buyOfferId;

        private VisualElement blockedModal;
        private Label blockedMessage;
        private Button blockedShop;
        private Button blockedClose;
        private bool blockedOpen;

        private EventEntry replayEntry;
        private EventActionRecord replayRecord;
        private List<ReplayLine> replayLines;
        private double replayElapsed;
        private double replayDuration;
        private int replayRevealed;

        private string daySignature;
        private string logSignature;
        private string casesSignature;
        private string resultSignature;

        public bool Playing => replayRecord != null;

        public int RevealedLines => replayRevealed;

        public bool BuyModalOpen => buyOfferId > 0;

        /// <summary>The "can't buy" dialog (reason, plus the shop button when no cage is free) is showing.</summary>
        public bool BlockedModalOpen => blockedOpen;

        /// <summary>The player asked to go and buy a cage; the owner switches to the shop.</summary>
        public event Action ShopRequested;

        public event Action<EventActionKind> ActionRequested;

        public event Action ServeAllRequested;

        public event Action FinishRequested;

        public event Action CloseRequested;

        public event Action<int> BuyConfirmed;

        private void InitDay(VisualElement root, VisualElement modalRoot)
        {
            dayTitle = root.Q<Label>("event-day-title");
            dayLeft = root.Q<Label>("event-day-left");
            stage = root.Q<VisualElement>("event-day-stage");
            offersBox = root.Q<VisualElement>("event-day-offers");
            serveButton = root.Q<Button>("event-day-serve-button");
            browseButton = root.Q<Button>("event-day-browse-button");
            serveAllButton = root.Q<Button>("event-day-serve-all-button");
            skipButton = root.Q<Button>("event-day-skip-button");
            finishButton = root.Q<Button>("event-day-finish-button");
            log = root.Q<ScrollView>("event-day-log");

            resultTitle = root.Q<Label>("event-result-title");
            resultSold = root.Q<VisualElement>("event-result-sold");
            resultUnsold = root.Q<VisualElement>("event-result-unsold");
            resultBought = root.Q<VisualElement>("event-result-bought");
            resultTotals = root.Q<Label>("event-result-totals");
            resultClose = root.Q<Button>("event-result-close-button");

            buyModal = modalRoot.Q<VisualElement>("event-buy-modal");
            buyMessage = modalRoot.Q<Label>("event-buy-message");
            blockedModal = modalRoot.Q<VisualElement>("event-blocked-modal");
            blockedMessage = modalRoot.Q<Label>("event-blocked-message");
            blockedShop = modalRoot.Q<Button>("event-blocked-shop-button");
            blockedClose = modalRoot.Q<Button>("event-blocked-close-button");
            buyYes = modalRoot.Q<Button>("event-buy-yes-button");
            buyNo = modalRoot.Q<Button>("event-buy-no-button");

            if (stage != null)
            {
                cases = new VisualElement { name = "event-cases" };
                cases.AddToClassList("event-cases");
                cases.pickingMode = PickingMode.Ignore;
                stage.Add(cases);

                visitor = new VisualElement { name = "event-visitor" };
                visitor.AddToClassList("event-visitor");
                visitor.pickingMode = PickingMode.Ignore;
                stage.Add(visitor);

                bubble = new VisualElement { name = "event-bubble" };
                bubble.AddToClassList("event-bubble");
                bubble.pickingMode = PickingMode.Ignore;
                bubbleLabel = new Label { name = "event-bubble-label" };
                bubbleLabel.AddToClassList("event-bubble-label");
                bubble.Add(bubbleLabel);
                stage.Add(bubble);
                HideVisitor();
            }

            Wire(serveButton, () => ActionRequested?.Invoke(EventActionKind.Serve));
            Wire(browseButton, () => ActionRequested?.Invoke(EventActionKind.Browse));
            Wire(serveAllButton, () => ServeAllRequested?.Invoke());
            Wire(finishButton, () => FinishRequested?.Invoke());
            Wire(skipButton, Skip);
            Wire(resultClose, () => CloseRequested?.Invoke());
            Wire(buyNo, CloseBuy);
            Wire(blockedClose, CloseBlocked);
            Wire(blockedShop, () =>
            {
                CloseBlocked();
                ShopRequested?.Invoke();
            });
            Wire(buyYes, () =>
            {
                if (buyOfferId > 0 && events != null && colony != null && events.CheckBuy(colony, buyOfferId) == EventBuyResult.Ok)
                {
                    BuyConfirmed?.Invoke(buyOfferId);
                }
            });
            CloseBuy();
            CloseBlocked();
        }

        private static void Wire(Button button, Action action)
        {
            if (button != null)
            {
                button.clicked += action;
            }
        }

        /// <summary>
        /// Starts the replay of an action whose result is already decided and saved. <paramref name="entry"/>
        /// is the event as it was when the action was played (the colony may have closed it already).
        /// </summary>
        public void BeginReplay(EventActionRecord record, EventEntry entry)
        {
            if (record == null || entry == null)
            {
                return;
            }

            replayEntry = entry;
            replayRecord = record;
            var count = EventSchedule.ActionCount(EventSchedule.KindOf(entry.Number));
            replayLines = EventReplay.Script(record, entry.BoothOffers, count);
            replayDuration = EventReplay.DurationSeconds(EventReplay.EventCount(record, entry.BoothOffers));
            replayElapsed = 0d;
            replayRevealed = EventReplay.RevealedCount(replayLines.Count, 0d, replayDuration);
            if (sessionRef != null)
            {
                RefreshDay();
            }
        }

        private void Skip()
        {
            if (!Playing)
            {
                return;
            }

            EndReplay();
            RefreshDay();
        }

        private void EndReplay()
        {
            replayRecord = null;
            replayEntry = null;
            replayLines = null;
            replayRevealed = 0;
            replayElapsed = 0d;
            HideVisitor();
            daySignature = null;
            logSignature = null;
        }

        /// <summary>Per-frame: advances the replay and the open buy dialog.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Playing)
            {
                replayElapsed += deltaSeconds;
                if (replayElapsed >= replayDuration)
                {
                    EndReplay();
                    RefreshDay();
                }
                else
                {
                    replayRevealed = EventReplay.RevealedCount(replayLines.Count, replayElapsed, replayDuration);
                    RebuildCases(replayEntry);
                    UpdateLog();
                    UpdateVisitor();
                }
            }

            UpdateBuyModal();
        }

        private EventEntry DayEntry() => Playing ? replayEntry : colony?.Events.Entry;

        private void RefreshDay()
        {
            var entry = DayEntry();
            if (entry == null || sessionRef == null)
            {
                return;
            }

            colony = sessionRef.Colony;
            events = sessionRef.Events;
            var actionCount = EventSchedule.ActionCount(EventSchedule.KindOf(entry.Number));
            var left = Playing ? Math.Max(0, actionCount - entry.Actions.Count) : events.ActionsLeft(colony);
            dayTitle.text = EventSchedule.NameOf(entry.Number);
            dayLeft.text = EventText.ActionsLeft(left, actionCount);

            RebuildCases(entry);
            var visible = VisibleOffers(entry);
            var signature = entry.Number + "|" + Playing + "|" + string.Join(",", visible.Select(o => o.OfferId));
            if (signature != daySignature)
            {
                daySignature = signature;
                RebuildOffers(visible);
            }

            var canAct = !Playing && left > 0;
            serveButton.SetEnabled(canAct);
            browseButton.SetEnabled(canAct);
            serveAllButton.SetEnabled(canAct);
            skipButton.SetEnabled(Playing);
            finishButton.style.visibility = !Playing && left == 0 ? Visibility.Visible : Visibility.Hidden;
            finishButton.SetEnabled(!Playing && left == 0);
            UpdateLog();
            UpdateVisitor();
            UpdateBuyModal();
        }

        private List<EventBoothOffer> VisibleOffers(EventEntry entry)
        {
            return entry.BoothOffers.Where(o => !o.Bought && !(Playing && o.ActionIndex == replayRecord.Index)).ToList();
        }

        private void RebuildCases(EventEntry entry)
        {
            if (cases == null)
            {
                return;
            }

            var signature = entry.Number + "|" + string.Join(",", entry.Listings.Select(l => l.AnimalId + (ShownSold(l) ? "s" : "u") + l.Price));
            if (signature == casesSignature)
            {
                return;
            }

            casesSignature = signature;
            cases.Clear();
            foreach (var listing in entry.Listings.OrderBy(l => l.AnimalId))
            {
                var box = new VisualElement { name = $"event-case-{listing.AnimalId}" };
                box.AddToClassList("event-case");
                box.pickingMode = PickingMode.Ignore;
                var price = new Label(EventText.Yen(listing.Price));
                price.AddToClassList("event-case-price");
                price.pickingMode = PickingMode.Ignore;
                box.Add(price);
                if (ShownSold(listing))
                {
                    var tag = new VisualElement { name = $"event-sold-tag-{listing.AnimalId}" };
                    tag.AddToClassList("event-sold-tag");
                    tag.pickingMode = PickingMode.Ignore;
                    box.Add(tag);
                }

                cases.Add(box);
            }
        }

        /// <summary>A listing sold in the action being replayed looks unsold until its sale line is revealed.</summary>
        private bool ShownSold(EventListing listing)
        {
            if (!listing.Sold)
            {
                return false;
            }

            if (!Playing || listing.SoldInAction != replayRecord.Index)
            {
                return true;
            }

            var sale = replayRecord.Sales.Find(x => x.AnimalId == listing.AnimalId);
            if (sale == null)
            {
                return true;
            }

            var text = EventText.SaleLine(sale);
            return replayLines.Take(replayRevealed).Any(l => l.Text == text);
        }

        private void RebuildOffers(List<EventBoothOffer> visible)
        {
            offersBox.Clear();
            foreach (var offer in visible)
            {
                var row = new VisualElement { name = $"event-offer-{offer.OfferId}" };
                row.AddToClassList("event-offer-row");
                var label = new Label(EventText.OfferLine(offer));
                label.AddToClassList("event-offer-label");
                row.Add(label);
                var id = offer.OfferId;
                var buy = new Button(() => OpenBuy(id)) { name = $"event-offer-buy-{offer.OfferId}", text = "買う" };
                buy.AddToClassList("event-offer-buy");
                buy.SetEnabled(!Playing);
                row.Add(buy);
                offersBox.Add(row);
            }
        }

        /// <summary>Redraws the log from the finished actions plus the revealed part of the running replay.</summary>
        private void UpdateLog()
        {
            var entry = DayEntry();
            if (entry == null || log == null)
            {
                return;
            }

            var finished = entry.Actions.Where(a => a != replayRecord).ToList();
            var signature = entry.Number + "|" + finished.Count + "|" + replayRevealed + "|" + (Playing ? replayRecord.Index : -1);
            if (signature == logSignature)
            {
                return;
            }

            logSignature = signature;
            log.Clear();
            var count = EventSchedule.ActionCount(EventSchedule.KindOf(entry.Number));
            foreach (var action in finished)
            {
                foreach (var line in EventReplay.Script(action, entry.BoothOffers, count))
                {
                    AddLogLine(line);
                }
            }

            if (Playing)
            {
                foreach (var line in replayLines.Take(replayRevealed))
                {
                    AddLogLine(line);
                }
            }

            log.schedule.Execute(() => log.scrollOffset = new Vector2(0f, log.verticalScroller.highValue));
        }

        private void AddLogLine(ReplayLine line)
        {
            var label = new Label(line.Text);
            label.AddToClassList("event-log-line");
            if (line.Highlight)
            {
                label.AddToClassList("event-log-highlight");
            }

            log.Add(label);
        }

        private static Dictionary<string, Texture2D> visitorFrames;

        /// <summary>Loads every Event/visitor_* texture once, the same way geckos load (LoadAll on the folder, Default import); warns about any missing frame.</summary>
        public static Dictionary<string, Texture2D> LoadVisitorFrames()
        {
            if (visitorFrames != null)
            {
                return visitorFrames;
            }

            var loaded = new Dictionary<string, Texture2D>();
            foreach (var texture in Resources.LoadAll<Texture2D>("Event"))
            {
                loaded[texture.name] = texture;
            }

            var complete = true;
            foreach (var kind in new[] { 'a', 'b', 'c' })
            {
                for (var frame = 0; frame < 2; frame++)
                {
                    var name = $"visitor_{kind}_{frame}";
                    if (!loaded.ContainsKey(name))
                    {
                        Debug.LogWarning($"EventView: Resources asset Event/{name} failed to load");
                        complete = false;
                    }
                }
            }

            if (complete)
            {
                visitorFrames = loaded;
            }

            return loaded;
        }

        private static Texture2D VisitorFrame(char kind, int frame)
        {
            return LoadVisitorFrames().TryGetValue($"visitor_{kind}_{frame}", out var texture) ? texture : null;
        }

        private void UpdateVisitor()
        {
            if (visitor == null)
            {
                return;
            }

            if (!Playing)
            {
                HideVisitor();
                return;
            }

            var kind = (char)('a' + replayRecord.Index % 3);
            var frame = (int)(replayElapsed / FrameSeconds) % 2;
            var picture = VisitorFrame(kind, frame);
            visitor.style.backgroundImage = picture != null ? new StyleBackground(picture) : new StyleBackground(StyleKeyword.None);
            visitor.style.display = DisplayStyle.Flex;
            var left = Mathf.Lerp(-18f, 30f, Mathf.Clamp01((float)(replayElapsed / 2.5d)));
            visitor.style.left = Length.Percent(left);

            var shown = Math.Min(replayRevealed, replayLines.Count);
            var text = shown > 0 ? replayLines[shown - 1].Bubble : null;

            if (string.IsNullOrEmpty(text))
            {
                bubble.style.display = DisplayStyle.None;
            }
            else
            {
                bubbleLabel.text = text;
                bubble.style.left = Length.Percent(Mathf.Max(left, 1f));
                bubble.style.display = DisplayStyle.Flex;
            }
        }

        private void HideVisitor()
        {
            if (visitor != null)
            {
                visitor.style.display = DisplayStyle.None;
            }

            if (bubble != null)
            {
                bubble.style.display = DisplayStyle.None;
            }
        }

        private void OpenBuy(int offerId)
        {
            if (Playing || buyModal == null)
            {
                return;
            }

            var offer = colony?.Events.Entry?.BoothOffers.Find(o => o.OfferId == offerId);
            if (offer == null)
            {
                return;
            }

            var check = events != null && colony != null ? events.CheckBuy(colony, offerId) : EventBuyResult.NoEvent;
            if (check != EventBuyResult.Ok)
            {
                ShowBlocked(check);
                return;
            }

            buyOfferId = offerId;
            buyMessage.text = EventText.ConfirmBuy(offer);
            buyModal.style.display = DisplayStyle.Flex;
            UpdateBuyModal();
        }

        public void CloseBuy()
        {
            buyOfferId = 0;
            if (buyModal != null)
            {
                buyModal.style.display = DisplayStyle.None;
            }
        }

        /// <summary>Shows why a purchase cannot go ahead; a missing cage also offers the way to the shop.</summary>
        public void ShowBlocked(EventBuyResult reason)
        {
            CloseBuy();
            if (blockedModal == null)
            {
                return;
            }

            blockedOpen = true;
            blockedMessage.text = EventText.BuyFailure(reason);
            if (blockedShop != null)
            {
                blockedShop.style.display = reason == EventBuyResult.NoEmptyCage ? DisplayStyle.Flex : DisplayStyle.None;
            }

            blockedModal.style.display = DisplayStyle.Flex;
        }

        public void CloseBlocked()
        {
            blockedOpen = false;
            if (blockedModal != null)
            {
                blockedModal.style.display = DisplayStyle.None;
            }
        }

        /// <summary>Re-checks the open buy dialog (money and cages change as time passes).</summary>
        private void UpdateBuyModal()
        {
            if (buyOfferId <= 0 || colony == null || events == null)
            {
                return;
            }

            var check = events.CheckBuy(colony, buyOfferId);
            if (check == EventBuyResult.NoEvent || check == EventBuyResult.NotFound || check == EventBuyResult.AlreadyBought)
            {
                CloseBuy();
                return;
            }

            if (check != EventBuyResult.Ok)
            {
                ShowBlocked(check);
            }
        }

        private void RenderResult(EventSummary summary)
        {
            var signature = summary.Number + "|" + summary.Sold.Count + "|" + summary.Unsold.Count + "|" + summary.Bought.Count;
            if (signature == resultSignature)
            {
                return;
            }

            resultSignature = signature;
            resultTitle.text = EventSchedule.NameOf(summary.Number) + " 結果";
            FillBlock(resultSold, summary.Sold);
            FillBlock(resultUnsold, summary.Unsold);
            FillBlock(resultBought, summary.Bought);
            resultTotals.text = $"売上 {EventText.Yen(summary.TotalSales)}　仕入れ {EventText.Yen(summary.PurchaseTotal)}　出店料 {EventText.Yen(summary.BoothFee)}"
                + $"\n差し引き {EventText.SignedYen(summary.Net)}";
        }

        private static void FillBlock(VisualElement block, List<EventAnimalLine> lines)
        {
            block.Clear();
            if (lines.Count == 0)
            {
                block.Add(new Label("なし"));
                return;
            }

            foreach (var line in lines)
            {
                block.Add(new Label(EventText.AnimalLine(line)));
            }
        }
    }
}
