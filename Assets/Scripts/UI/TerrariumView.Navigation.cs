using System;
using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    public sealed partial class TerrariumView
    {
        /// <summary>
        /// Selects the given cage's animal. Returns false and leaves the current selection
        /// untouched when the cage is empty (e.g. a bought-but-unfilled cage), so callers
        /// never navigate to a detail screen that would show/act on the previous animal.
        /// </summary>
        public bool SelectCage(int cageId)
        {
            var cage = session.Colony.Cages.Find(c => c.Id == cageId);
            var pet = session.Colony.ResidentShown(cage);
            if (pet == null)
            {
                return false;
            }

            session.Colony.NewRackIndices.Remove(session.Colony.Cages.IndexOf(cage) / Colony.CagesPerRack);

            currentCage = cage;
            if (petElement != null)
            {
                petElement.style.display = DisplayStyle.Flex;
            }

            Initialize(pet, tuning);
            RebuildPetActor();
            ColonyChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Clears the current selection when there is no animal to show it for (the colony
        /// has zero animals — e.g. the last one was just wholesaled). Hides the pet and decor
        /// so a stale look is never left on screen, and backs out of the cage detail screen
        /// if it was open (it would otherwise show a detail view for nothing).
        /// </summary>
        public void ClearSelection()
        {
            state = null;
            currentCage = null;
            petActor?.Effects.Clear();
            petActor = null;
            if (petElement != null)
            {
                petElement.style.display = DisplayStyle.None;
            }

            foreach (var element in decorImageElements)
            {
                if (element != null)
                {
                    element.style.display = DisplayStyle.None;
                }
            }

            if (navigator != null && navigator.ShowingCageDetail)
            {
                navigator.ShowCageList();
            }
        }

        public void ShowCageStep(int delta)
        {
            var occupied = session.Colony.ShownCages();
            if (occupied.Count == 0)
            {
                return;
            }

            var index = Math.Max(0, occupied.IndexOf(currentCage));
            var next = ((index + delta) % occupied.Count + occupied.Count) % occupied.Count;
            SelectCage(occupied[next].Id);
        }

        private void OnHomeButtonClicked()
        {
            homeView.Invalidate();
            navigator.ShowHome();
            RefreshShell();
        }

        private void OnPrevCageButtonClicked() => ShowCageStep(-1);

        private void OnNextCageButtonClicked() => ShowCageStep(1);

        private void OnHomeIncubatorButtonClicked() => navigator.ShowTab(ShellTab.Incubator);

        private void OnCageTapped(int cageId)
        {
            if (SelectCage(cageId))
            {
                navigator.ShowCageDetail();
            }
        }

        private void OnTabChanged(ShellTab tab)
        {
            OnFamilyTreeCloseClicked();
            if (tab == ShellTab.Cages && !navigator.ShowingCageDetail)
            {
                cageListView.Invalidate();
            }

            if (tab == ShellTab.Shop)
            {
                shopView?.Invalidate();
                ShowShopMessage(string.Empty);
            }

            if (tab == ShellTab.Ledger)
            {
                ledgerView?.Invalidate();
            }

            if (tab == ShellTab.Breeding)
            {
                pairingView?.Invalidate();
                pairingView?.ShowMessage(string.Empty);
            }

            if (tab == ShellTab.Incubator)
            {
                incubatorView?.Invalidate();
            }

            if (tab == ShellTab.Events)
            {
                eventView?.Invalidate();
                eventView?.ShowMessage(string.Empty);
            }

            RefreshShell();
        }
    }
}
