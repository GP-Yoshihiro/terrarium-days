using System;
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

        public HomeView(VisualElement rackList, PetSpriteLibrary sprites)
        {
            this.rackList = rackList;
            this.sprites = sprites;
        }

        public event Action<int> CageSelected;

        public void Render(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
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

        public CageListView(VisualElement list, PetSpriteLibrary sprites)
        {
            this.list = list;
            this.sprites = sprites;
        }

        public event Action<int> CageSelected;

        public void Render(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
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
