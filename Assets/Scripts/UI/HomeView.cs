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
        private readonly PetSpriteLibrary sprites;
        private string lastSignature;

        public HomeView(VisualElement rackList, PetSpriteLibrary sprites)
        {
            this.rackList = rackList;
            this.sprites = sprites;
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
                signature.Append(';').Append(cage.Id).Append(':');
                if (pet != null)
                {
                    signature.Append(pet.Name).Append(':')
                        .Append(CageStatusText.TitleFor(cage, pet)).Append(':')
                        .Append(pet.WeightGrams.ToString("0.0")).Append(':')
                        .Append(CageStatusText.AlertsFor(pet, nowUtc, tuning));
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
            var slot = new Button(() => CageSelected?.Invoke(cage.Id));
            slot.AddToClassList("rack-slot");
            slot.SetEnabled(pet != null);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("rack-thumb");
            var frame = pet != null ? sprites?.Frame(PetClip.Idle, 0) : null;
            if (frame != null)
            {
                thumb.style.backgroundImage = new StyleBackground(frame);
            }

            slot.Add(thumb);
            slot.Add(Label(pet != null ? pet.Name : "空きケージ", "rack-name"));
            slot.Add(Label(pet != null ? CageStatusText.AlertsFor(pet, nowUtc, tuning) : string.Empty, "rack-alert"));
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
        private readonly PetSpriteLibrary sprites;
        private string lastSignature;

        public CageListView(VisualElement list, PetSpriteLibrary sprites)
        {
            this.list = list;
            this.sprites = sprites;
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
                var row = new Button(() => CageSelected?.Invoke(cage.Id));
                row.AddToClassList("cage-row");
                row.SetEnabled(pet != null);
                var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
                thumb.AddToClassList("cage-row-thumb");
                var frame = pet != null ? sprites?.Frame(PetClip.Idle, 0) : null;
                if (frame != null)
                {
                    thumb.style.backgroundImage = new StyleBackground(frame);
                }

                row.Add(thumb);
                var texts = new VisualElement { pickingMode = PickingMode.Ignore };
                texts.AddToClassList("cage-row-texts");
                texts.Add(new Label(CageStatusText.TitleFor(cage, pet)) { pickingMode = PickingMode.Ignore });
                var detail = pet != null
                    ? $"{pet.WeightGrams:0.0}g　{CageStatusText.AlertsFor(pet, nowUtc, tuning)}"
                    : string.Empty;
                var detailLabel = new Label(detail) { pickingMode = PickingMode.Ignore };
                detailLabel.AddToClassList("cage-row-detail");
                texts.Add(detailLabel);
                row.Add(texts);
                list.Add(row);
            }
        }
    }
}
