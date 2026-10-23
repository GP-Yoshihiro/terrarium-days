using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    public sealed partial class TerrariumView
    {
        /// <summary>Subscribes the events tab's actions; Awake and the PlayMode harness (which skips Awake) both call it.</summary>
        public void WireEventEvents()
        {
            if (eventView == null)
            {
                return;
            }

            eventView.RegisterRequested += OnEventRegisterRequested;
            eventView.SaveRequested += OnEventSaveRequested;
            eventView.ActionRequested += OnEventActionRequested;
            eventView.ServeAllRequested += OnEventServeAllRequested;
            eventView.FinishRequested += OnEventFinishRequested;
            eventView.CloseRequested += OnEventCloseRequested;
            eventView.BuyConfirmed += OnEventBuyConfirmed;
            eventView.ShopRequested += OnEventShopRequested;
        }

        private void OnEventActionRequested(EventActionKind kind)
        {
            if (session == null || eventView == null || eventView.Playing)
            {
                return;
            }

            var entry = session.Colony.Events.Entry;
            if (session.Events.PlayAction(session.Colony, session.Calendar, kind, GameNowUtc(), out var record) != EventActionResult.Ok)
            {
                return;
            }

            // The result is final and saved before the replay starts (★12).
            SaveCurrentState();
            eventView.BeginReplay(record, entry);
            ColonyChanged?.Invoke();
            RefreshShell();
        }

        private void OnEventServeAllRequested()
        {
            if (session == null || eventView == null || eventView.Playing)
            {
                return;
            }

            confirmDialog?.Show(EventText.ConfirmServeAll, ConfirmEventServeAll);
        }

        private void ConfirmEventServeAll()
        {
            if (session.Events.ServeAllRemaining(session.Colony, session.Calendar, GameNowUtc(), out _) != EventActionResult.Ok)
            {
                return;
            }

            SaveCurrentState();
            eventView?.CloseBuy();
            eventView?.Invalidate();
            ColonyChanged?.Invoke();
            RefreshShell();
        }

        private void OnEventFinishRequested()
        {
            if (session == null || !session.Events.Finish(session.Colony))
            {
                return;
            }

            SaveCurrentState();
            eventView?.CloseBuy();
            eventView?.Invalidate();
            ColonyChanged?.Invoke();
            RefreshShell();
        }

        private void OnEventCloseRequested()
        {
            if (session == null)
            {
                return;
            }

            session.Events.DismissSummary(session.Colony);
            SaveCurrentState();
            eventView?.Invalidate();
            ColonyChanged?.Invoke();
            RefreshShell();
        }

        private void OnEventShopRequested()
        {
            shopView?.ShowCages();
            navigator?.ShowTab(ShellTab.Shop);
        }

        private void OnEventBuyConfirmed(int offerId)
        {
            if (session == null)
            {
                return;
            }

            var result = session.Events.Buy(session.Colony, offerId, GameNowUtc());
            if (result != EventBuyResult.Ok)
            {
                eventView?.ShowBlocked(result);
            }
            else
            {
                eventView?.CloseBuy();
                SaveCurrentState();
                eventView?.Invalidate();
                ColonyChanged?.Invoke();
            }

            RefreshShell();
        }

        private void OnEventRegisterRequested(int number, List<ListingRequest> requests)
        {
            confirmDialog?.Show(EventText.ConfirmRegister(number, requests.Count), () => ConfirmEventRegister(number, requests));
        }

        private void ConfirmEventRegister(int number, List<ListingRequest> requests)
        {
            var result = session.Events.Register(session.Colony, session.Calendar, number, requests, GameNowUtc());
            if (result != EventRegisterResult.Ok)
            {
                eventView?.ShowMessage(EventText.RegisterFailure(result));
                return;
            }

            eventView?.ShowMessage(string.Empty);
            SaveCurrentState();
            eventView?.Invalidate();
            ColonyChanged?.Invoke();
        }

        private void OnEventSaveRequested(List<ListingRequest> requests)
        {
            var result = session.Events.UpdateListings(session.Colony, session.Calendar, requests, GameNowUtc());
            if (result != EventRegisterResult.Ok)
            {
                eventView?.ShowMessage(EventText.RegisterFailure(result));
                return;
            }

            eventView?.ShowMessage("出品を直しました");
            SaveCurrentState();
            eventView?.Invalidate();
            RefreshShell();
        }
    }
}
