using System;
using System.Text;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>The breeding room: racks of four slots, each a bought cage or an empty shelf.</summary>
    public sealed class HomeView
    {
        private readonly VisualElement rackList;
        private string lastSignature;

        public HomeView(VisualElement rackList)
        {
            this.rackList = rackList;
        }

        public event Action<int> CageSelected;

        /// <summary>
        /// A pure summary of everything this view (and <see cref="CageListView"/>) draws:
        /// rack/cage counts, then per cage its id, name, title text, weight and alerts.
        /// Rendering is skipped when this has not changed since the last render, so a
        /// once-a-second refresh does not tear down and rebuild every row's Button (and
        /// swallow a tap in progress) when nothing the player can see has actually changed.
        /// </summary>
        public static string CageListSignature(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var signature = new StringBuilder();
            signature.Append(colony.RackCount).Append('|').Append(colony.Cages.Count);
            foreach (var cage in colony.Cages)
            {
                var pet = colony.AnimalIn(cage);
                var shown = colony.ResidentShown(cage);
                signature.Append(';').Append(cage.Id).Append(':')
                    .Append(shown != null ? shown.Id : -1).Append(':')
                    .Append(cage.VisitorAnimalId).Append(':')
                    .Append(pet != null && pet.Weak).Append(':')
                    .Append(pet != null && pet.Gravid != null).Append(':')
                    .Append(cage.HasNestBox).Append(':')
                    .Append(colony.EggsIn(cage).Count).Append(':');
                if (pet != null)
                {
                    signature.Append(pet.Name).Append(':')
                        .Append(shown != null ? CageStatusText.TitleFor(cage, shown) : CageStatusText.AwayTitle(cage)).Append(':')
                        .Append(pet.WeightGrams.ToString("0.0")).Append(':')
                        .Append(CageStatusText.AlertsFor(colony, cage, shown, nowUtc, tuning)).Append(':')
                        .Append(MorphAppearance.PaletteFor(pet.Genotype, pet.Stage).Key).Append(':')
                        .Append(pet.SexKnown);
                }
            }

            return signature.ToString();
        }

        /// <summary>Forces the next Render to rebuild even if the signature has not changed; call when the view becomes visible again.</summary>
        public void Invalidate()
        {
            lastSignature = null;
        }

        public void Render(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var signature = CageListSignature(colony, nowUtc, tuning);
            if (signature == lastSignature)
            {
                return;
            }

            lastSignature = signature;

            rackList.Clear();
            for (var rack = 0; rack < colony.RackCount; rack++)
            {
                var row = new VisualElement();
                row.AddToClassList("rack-row");
                for (var slot = 0; slot < Colony.CagesPerRack; slot++)
                {
                    var index = rack * Colony.CagesPerRack + slot;
                    row.Add(index < colony.Cages.Count
                        ? CageSlot(colony, colony.Cages[index], nowUtc, tuning)
                        : EmptyShelf());
                }

                rackList.Add(row);
            }
        }

        private Button CageSlot(Colony colony, Cage cage, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var pet = colony.AnimalIn(cage);
            var shown = colony.ResidentShown(cage);
            var visitor = colony.VisitorIn(cage);
            var slot = new Button(() => CageSelected?.Invoke(cage.Id));
            slot.AddToClassList("rack-slot");
            slot.SetEnabled(shown != null);
            if (visitor != null)
            {
                slot.AddToClassList("rack-slot-visiting");
            }

            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("rack-thumb");
            var thumbnail = shown != null ? MorphSprites.Thumbnail(shown) : null;
            if (thumbnail != null)
            {
                thumb.style.backgroundImage = new StyleBackground(thumbnail);
            }

            if (shown != null && shown.Weak)
            {
                thumb.Add(Label("！", "rack-badge-weak"));
            }

            slot.Add(thumb);

            if (visitor != null)
            {
                var visitorBox = new VisualElement { pickingMode = PickingMode.Ignore };
                visitorBox.AddToClassList("rack-visitor");
                var visitorThumb = new VisualElement { pickingMode = PickingMode.Ignore };
                visitorThumb.AddToClassList("rack-visitor-thumb");
                var visitorSprite = MorphSprites.Thumbnail(visitor);
                if (visitorSprite != null)
                {
                    visitorThumb.style.backgroundImage = new StyleBackground(visitorSprite);
                }

                visitorBox.Add(visitorThumb);
                visitorBox.Add(Label("訪問中", "rack-visitor-label"));
                slot.Add(visitorBox);
            }

            slot.Add(Label(pet == null ? "空きケージ" : shown != null ? shown.Name : string.Empty, "rack-name"));
            slot.Add(Label(CageStatusText.AlertsFor(colony, cage, shown, nowUtc, tuning), "rack-alert"));
            return slot;
        }

        private static Button EmptyShelf()
        {
            var slot = new Button();
            slot.AddToClassList("rack-slot");
            slot.AddToClassList("rack-slot-empty");
            slot.Add(Label("空き棚", "rack-name"));
            slot.SetEnabled(false);
            return slot;
        }

        private static Label Label(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }

    /// <summary>The Cages tab list: one row per cage, tap to open it.</summary>
    public sealed class CageListView
    {
        private readonly VisualElement list;
        private string lastSignature;

        public CageListView(VisualElement list)
        {
            this.list = list;
        }

        public event Action<int> CageSelected;

        /// <summary>Forces the next Render to rebuild even if the signature has not changed; call when the view becomes visible again.</summary>
        public void Invalidate()
        {
            lastSignature = null;
        }

        public void Render(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var signature = HomeView.CageListSignature(colony, nowUtc, tuning);
            if (signature == lastSignature)
            {
                return;
            }

            lastSignature = signature;

            list.Clear();
            foreach (var cage in colony.Cages)
            {
                var pet = colony.AnimalIn(cage);
                var shown = colony.ResidentShown(cage);
                var row = new Button(() => CageSelected?.Invoke(cage.Id));
                row.AddToClassList("cage-row");
                row.SetEnabled(shown != null);
                if (colony.VisitorIn(cage) != null)
                {
                    row.AddToClassList("cage-row-visiting");
                }

                var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
                thumb.AddToClassList("cage-row-thumb");
                var thumbnail = shown != null ? MorphSprites.Thumbnail(shown) : null;
                if (thumbnail != null)
                {
                    thumb.style.backgroundImage = new StyleBackground(thumbnail);
                }

                if (shown != null && shown.Weak)
                {
                    var badge = new Label("！") { pickingMode = PickingMode.Ignore };
                    badge.AddToClassList("cage-row-badge-weak");
                    thumb.Add(badge);
                }

                row.Add(thumb);
                var texts = new VisualElement { pickingMode = PickingMode.Ignore };
                texts.AddToClassList("cage-row-texts");
                texts.Add(new Label(TitleForRow(cage, pet, shown)) { pickingMode = PickingMode.Ignore });
                var detail = shown != null
                    ? $"{shown.WeightGrams:0.0}g　{CageStatusText.AlertsFor(colony, cage, shown, nowUtc, tuning)}"
                    : string.Empty;
                var detailLabel = new Label(detail) { pickingMode = PickingMode.Ignore };
                detailLabel.AddToClassList("cage-row-detail");
                texts.Add(detailLabel);
                row.Add(texts);
                list.Add(row);
            }
        }

        /// <summary>The cage-list title: full detail for a resident shown at home, id-only while it is away visiting (§7.2), or the empty-cage title.</summary>
        private static string TitleForRow(Cage cage, PetState pet, PetState shown)
        {
            if (pet == null)
            {
                return CageStatusText.TitleFor(cage, null);
            }

            return shown != null ? CageStatusText.TitleFor(cage, shown) : CageStatusText.AwayTitle(cage);
        }
    }
}
