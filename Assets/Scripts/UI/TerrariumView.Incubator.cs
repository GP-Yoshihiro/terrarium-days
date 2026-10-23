using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    public sealed partial class TerrariumView
    {
        /// <summary>Moves every live egg of the current cage into the incubators (★7).</summary>
        public void OnMoveEggsButtonClicked()
        {
            if (session == null || currentCage == null)
            {
                return;
            }

            var result = session.Incubation.MoveAllFromCage(session.Colony, currentCage);
            SaveCurrentState();
            ColonyChanged?.Invoke();
            incubatorView?.Invalidate();
            if (state != null)
            {
                Render(state, tuning);
            }

            RefreshShell();
            ShowFeedback(IncubatorText.MoveResult(result.Moved, result.Left));
        }

        public void OnIncubatorTemperatureChangeRequested(Incubator incubator, int delta)
        {
            if (session == null || incubator == null)
            {
                return;
            }

            session.Incubation.SetTemperature(incubator, incubator.SetTemperatureC + delta);
            AfterIncubatorAction();
        }

        public void OnIncubatorWaterRequested(Incubator incubator)
        {
            if (session == null || incubator == null)
            {
                return;
            }

            if (session.Incubation.AddWater(incubator))
            {
                ShowFeedback(IncubatorText.MoldWarning(tuning));
            }

            AfterIncubatorAction();
        }

        public void OnIncubatorEggTapped(Egg egg)
        {
            incubatorView?.ToggleSelection(egg);
            RefreshShell();
        }

        public void OnIncubatorRemoveRequested(Egg egg)
        {
            if (session == null || egg == null || !session.Incubation.RemoveEgg(session.Colony, egg, GameNowUtc()))
            {
                return;
            }

            incubatorView?.ClearSelection();
            AfterIncubatorAction();
        }

        /// <summary>Subscribes the incubator tab's events; Awake and the PlayMode harness (which skips Awake) both call it.</summary>
        public void WireIncubatorEvents()
        {
            if (incubatorView == null)
            {
                return;
            }

            incubatorView.TemperatureChangeRequested += OnIncubatorTemperatureChangeRequested;
            incubatorView.WaterRequested += OnIncubatorWaterRequested;
            incubatorView.EggTapped += OnIncubatorEggTapped;
            incubatorView.RemoveRequested += OnIncubatorRemoveRequested;
            incubatorView.MoveToCageRequested += OnTrayMoveToCageRequested;
            incubatorView.SellRequested += OnTraySellRequested;
        }

        public void OnTrayMoveToCageRequested(PetState pet)
        {
            if (session == null || pet == null)
            {
                return;
            }

            if (Hatchery.MoveToCage(session.Colony, pet) != MoveToCageResult.Ok)
            {
                AfterIncubatorAction();
                return;
            }

            var cage = session.Colony.CageOf(pet);
            AfterIncubatorAction();
            ShowFeedback($"{pet.Name}をケージ{cage.Id}へ移しました");
        }

        /// <summary>Sells a tray hatchling through the ordinary wholesale confirmation (★13); it has no cage to clear.</summary>
        public void OnTraySellRequested(PetState pet)
        {
            if (pet != null)
            {
                OnShopWholesaleRequested(pet.Id);
            }
        }

        private void AfterIncubatorAction()
        {
            SaveCurrentState();
            ColonyChanged?.Invoke();
            incubatorView?.Invalidate();
            RefreshShell();
        }
    }
}
