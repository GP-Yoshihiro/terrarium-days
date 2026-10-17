using System;
using System.Collections.Generic;
using System.Text;
using TerrariumDays.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// The incubator tab's cards (phase 5 §2.2, §3): one per incubator with the set and real
    /// temperature, humidity and the candled eggs. Actions leave as events; TerrariumView
    /// applies them, saves and re-renders. Like PairingView it rebuilds only when the
    /// signature of what is shown changes, so a rebuild mid-tap never swallows the tap.
    /// </summary>
    public sealed class IncubatorView
    {
        private readonly VisualElement cardList;
        private readonly Label eggDetail;
        private readonly Dictionary<CandlingStage, Texture2D> pictures = new Dictionary<CandlingStage, Texture2D>();

        private string lastSignature;
        private int? selectedEggId;

        public IncubatorView(VisualElement root)
        {
            cardList = root.Q<VisualElement>("incubator-card-list");
            eggDetail = root.Q<Label>("incubator-egg-detail");
        }

        public event Action<Incubator, int> TemperatureChangeRequested;

        public event Action<Incubator> WaterRequested;

        public event Action<Egg> EggTapped;

        public event Action<Egg> RemoveRequested;

        public int? SelectedEggId => selectedEggId;

        public void Invalidate()
        {
            lastSignature = null;
        }

        /// <summary>Selects the egg (tapping the selected egg again deselects it) and forces a redraw.</summary>
        public void ToggleSelection(Egg egg)
        {
            selectedEggId = egg != null && selectedEggId != egg.Id ? egg.Id : (int?)null;
            Invalidate();
        }

        public void ClearSelection()
        {
            selectedEggId = null;
            Invalidate();
        }

        public void Render(Colony colony, IncubatorService incubation, DateTimeOffset gameNowUtc, double roomC, CareTuning care)
        {
            if (cardList == null || colony == null)
            {
                return;
            }

            if (selectedEggId.HasValue && !colony.Eggs.Exists(e => e.Id == selectedEggId.Value && e.Place == EggPlace.Incubator))
            {
                selectedEggId = null;
            }

            var signature = Signature(colony, incubation, gameNowUtc, roomC, care);
            if (signature == lastSignature)
            {
                return;
            }

            lastSignature = signature;
            cardList.Clear();
            foreach (var incubator in colony.Incubators)
            {
                cardList.Add(BuildCard(colony, incubation, incubator, gameNowUtc, roomC, care));
            }

            UpdateDetail(colony, gameNowUtc, care);
        }

        private string Signature(Colony colony, IncubatorService incubation, DateTimeOffset gameNowUtc, double roomC, CareTuning care)
        {
            var text = new StringBuilder();
            foreach (var incubator in colony.Incubators)
            {
                text.Append(incubator.Id).Append(':').Append(incubator.Model).Append(':').Append(incubator.SetTemperatureC)
                    .Append(':').Append(IncubatorText.RealTemperature(incubation.RealTemperatureAt(colony, incubator, gameNowUtc, roomC)))
                    .Append(':').Append(IncubatorText.Humidity(incubator.HumidityPercent)).Append('[');
                foreach (var egg in IncubatorService.EggsInIncubator(colony, incubator))
                {
                    var stage = Candling.StageOf(egg, gameNowUtc, care);
                    text.Append(egg.Id).Append('=').Append((int)stage).Append(incubation.CanRemove(egg, gameNowUtc) ? 'x' : '-').Append(',');
                }

                text.Append(']');
            }

            text.Append('|').Append(selectedEggId);
            var selected = selectedEggId.HasValue ? colony.Eggs.Find(e => e.Id == selectedEggId.Value) : null;
            if (selected != null)
            {
                text.Append(IncubatorText.EggDetail(selected, Candling.StageOf(selected, gameNowUtc, care), gameNowUtc));
            }

            return text.ToString();
        }

        private VisualElement BuildCard(Colony colony, IncubatorService incubation, Incubator incubator, DateTimeOffset gameNowUtc, double roomC, CareTuning care)
        {
            var eggs = IncubatorService.EggsInIncubator(colony, incubator);
            var capacity = IncubatorRules.Capacity(incubator.Model);

            var card = new VisualElement { name = $"incubator-card-{incubator.Id}" };
            card.AddToClassList("incubator-card");

            var header = new VisualElement();
            header.AddToClassList("incubator-card__header");
            header.Add(new Label(IncubatorText.IncubatorName(incubator)));
            header.Add(new Label(IncubatorText.Occupancy(eggs.Count, capacity)));
            card.Add(header);

            var tempRow = new VisualElement();
            tempRow.AddToClassList("incubator-temp-row");
            var down = new Button { name = $"incubator-{incubator.Id}-temp-down", text = "−" };
            down.AddToClassList("incubator-temp-button");
            down.SetEnabled(incubator.SetTemperatureC > IncubatorRules.MinSetC);
            down.clicked += () => TemperatureChangeRequested?.Invoke(incubator, -1);
            var up = new Button { name = $"incubator-{incubator.Id}-temp-up", text = "＋" };
            up.AddToClassList("incubator-temp-button");
            up.SetEnabled(incubator.SetTemperatureC < IncubatorRules.MaxSetC);
            up.clicked += () => TemperatureChangeRequested?.Invoke(incubator, 1);
            var tempLabel = new Label(IncubatorText.SetTemperature(incubator.SetTemperatureC));
            tempLabel.AddToClassList("incubator-temp-label");
            var realLabel = new Label(IncubatorText.RealTemperature(incubation.RealTemperatureAt(colony, incubator, gameNowUtc, roomC)));
            realLabel.AddToClassList("incubator-temp-label");
            tempRow.Add(down);
            tempRow.Add(tempLabel);
            tempRow.Add(realLabel);
            tempRow.Add(up);
            card.Add(tempRow);

            var humidityRow = new VisualElement();
            humidityRow.AddToClassList("incubator-humidity-row");
            humidityRow.Add(new Label(IncubatorText.Humidity(incubator.HumidityPercent)));
            var water = new Button { name = $"incubator-{incubator.Id}-water", text = "水を足す" };
            water.AddToClassList("incubator-water-button");
            water.clicked += () => WaterRequested?.Invoke(incubator);
            humidityRow.Add(water);
            card.Add(humidityRow);

            var bar = new VisualElement();
            bar.AddToClassList("humidity-bar");
            if (IncubatorText.IsHumidityWarning(incubator.HumidityPercent, care))
            {
                bar.AddToClassList("humidity-bar--warning");
            }

            var fill = new VisualElement();
            fill.AddToClassList("humidity-bar__fill");
            fill.style.width = Length.Percent((float)Math.Max(0d, Math.Min(100d, incubator.HumidityPercent)));
            bar.Add(fill);
            card.Add(bar);

            var grid = new VisualElement();
            grid.AddToClassList("egg-grid");
            for (var i = 0; i < Math.Max(capacity, eggs.Count); i++)
            {
                grid.Add(i < eggs.Count ? BuildEggSlot(eggs[i], incubation, gameNowUtc, care) : BuildEmptySlot());
            }

            card.Add(grid);
            return card;
        }

        private VisualElement BuildEmptySlot()
        {
            var slot = new VisualElement();
            slot.AddToClassList("egg-slot");
            slot.AddToClassList("egg-slot--empty");
            var image = new VisualElement();
            image.AddToClassList("egg-slot__image");
            slot.Add(image);
            return slot;
        }

        private VisualElement BuildEggSlot(Egg egg, IncubatorService incubation, DateTimeOffset gameNowUtc, CareTuning care)
        {
            var slot = new VisualElement { name = $"egg-slot-{egg.Id}" };
            slot.AddToClassList("egg-slot");
            if (selectedEggId == egg.Id)
            {
                slot.AddToClassList("egg-slot--selected");
            }

            var image = new VisualElement();
            image.AddToClassList("egg-slot__image");
            var picture = PictureFor(Candling.StageOf(egg, gameNowUtc, care));
            if (picture != null)
            {
                image.style.backgroundImage = new StyleBackground(picture);
            }

            slot.Add(image);
            slot.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button)
                {
                    return;
                }

                EggTapped?.Invoke(egg);
            });

            if (incubation.CanRemove(egg, gameNowUtc))
            {
                var remove = new Button { name = $"egg-remove-{egg.Id}", text = "取り除く" };
                remove.AddToClassList("egg-slot__remove");
                remove.clicked += () => RemoveRequested?.Invoke(egg);
                slot.Add(remove);
            }

            return slot;
        }

        private void UpdateDetail(Colony colony, DateTimeOffset gameNowUtc, CareTuning care)
        {
            if (eggDetail == null)
            {
                return;
            }

            var egg = selectedEggId.HasValue ? colony.Eggs.Find(e => e.Id == selectedEggId.Value) : null;
            eggDetail.text = egg != null ? IncubatorText.EggDetail(egg, Candling.StageOf(egg, gameNowUtc, care), gameNowUtc) : string.Empty;
            eggDetail.style.visibility = egg != null ? Visibility.Visible : Visibility.Hidden;
        }

        // Loaded as Texture2D: the candling PNGs are imported with sprite mode Multiple and no
        // slices, so Resources.Load<Sprite> finds nothing (Task 13 import setting).
        private Texture2D PictureFor(CandlingStage stage)
        {
            if (!pictures.TryGetValue(stage, out var picture))
            {
                picture = Resources.Load<Texture2D>("Candling/candling_" + FileStage(stage));
                pictures[stage] = picture;
            }

            return picture;
        }

        private static string FileStage(CandlingStage stage) =>
            stage == CandlingStage.AboutToHatch ? "hatching" : stage.ToString().ToLowerInvariant();
    }
}
