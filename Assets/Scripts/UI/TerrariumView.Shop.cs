using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    public sealed partial class TerrariumView
    {
        /// <summary>Sets the shop tab's fixed-height feedback slot (§9); hidden while empty, like feedback-label.</summary>
        private void ShowShopMessage(string message)
        {
            if (shopMessageLabel == null)
            {
                return;
            }

            shopMessageLabel.text = message;
            shopMessageLabel.style.visibility = string.IsNullOrEmpty(message) ? Visibility.Hidden : Visibility.Visible;
        }

        /// <summary>Asks to buy an offered animal; the actual purchase happens on the confirmation's "はい" (see ConfirmBuyAnimal).</summary>
        public void OnShopOfferBuyRequested(int offerId)
        {
            if (session == null || shopService == null)
            {
                return;
            }

            var offer = session.Colony.Shop.Offers.Find(o => o.OfferId == offerId);
            if (offer == null)
            {
                return;
            }

            var price = shopService.PriceOf(offer);
            var what = MorphNamer.VisualName(offer.Animal.Genotype);
            confirmDialog?.Show(ShopText.ConfirmBuy(what, price), () => ConfirmBuyAnimal(offer, price));
        }

        /// <summary>Confirms against the exact offer shown (not just its id) and its price: time keeps passing while the dialog is open (§7), and next month's stock reuses the same offer ids for different animals (see ShopStockGenerator).</summary>
        private void ConfirmBuyAnimal(ShopOffer offer, long expectedPrice)
        {
            if (!session.Colony.Shop.Offers.Contains(offer) || shopService.PriceOf(offer) != expectedPrice)
            {
                ShowShopMessage(ShopText.OfferChangedMessage);
                shopView?.Invalidate();
                RefreshShell();
                return;
            }

            var result = shopService.BuyAnimal(session.Colony, offer.OfferId, GameNowUtc());
            if (result == ShopResult.Ok)
            {
                var pet = session.Colony.Animals[session.Colony.Animals.Count - 1];
                var cage = session.Colony.CageOf(pet);
                ShowShopMessage(ShopText.BoughtAnimalMessage(pet, cage));
                if (currentCage == null)
                {
                    SelectCage(cage.Id);
                }

                SaveCurrentState();
                ColonyChanged?.Invoke();
                shopView?.Invalidate();
            }
            else if (result == ShopResult.NotEnoughMoney || result == ShopResult.NoEmptyCage)
            {
                ShowShopFailureDialog(result);
            }
            else
            {
                ShowShopMessage(ShopText.FailureMessage(result));
            }

            RefreshShell();
        }

        /// <summary>Asks to buy a supply item; the actual purchase happens on the confirmation's "はい" (see ConfirmBuyItem).</summary>
        public void OnShopItemBuyRequested(string itemId)
        {
            if (session == null || shopService == null)
            {
                return;
            }

            var item = ShopCatalog.Find(itemId, shopService.Economy);
            if (item == null)
            {
                return;
            }

            confirmDialog?.Show(ShopText.ConfirmBuy(item.Label, item.Price), () => ConfirmBuyItem(itemId));
        }

        private void ConfirmBuyItem(string itemId)
        {
            var item = ShopCatalog.Find(itemId, shopService.Economy);
            var result = shopService.BuyItem(session.Colony, itemId, GameNowUtc());
            if (result == ShopResult.Ok)
            {
                ShowShopMessage(item != null ? ShopText.BoughtItemMessage(item) : string.Empty);
                SaveCurrentState();
                ColonyChanged?.Invoke();
                shopView?.Invalidate();
            }
            else if (result == ShopResult.NotEnoughMoney)
            {
                ShowShopFailureDialog(result);
            }
            else
            {
                ShowShopMessage(ShopText.FailureMessage(result));
            }

            RefreshShell();
        }

        /// <summary>Asks to wholesale one of the player's animals; the sale happens on the confirmation's "はい" (see ConfirmWholesale).</summary>
        public void OnShopWholesaleRequested(int animalId)
        {
            if (session == null || shopService == null)
            {
                return;
            }

            var pet = session.Colony.AnimalById(animalId);
            if (pet == null)
            {
                return;
            }

            if (session.Colony.Events.IsListed(pet.Id))
            {
                ShowShopMessage(ShopText.FailureMessage(ShopResult.Listed));
                return;
            }

            var pay = shopService.WholesalePriceOf(pet);
            var message = ShopText.ConfirmWholesale(pet, pay, shopService.Economy);
            if (session.Colony.Animals.Count == 1)
            {
                message += ShopText.LastAnimalWarning;
            }

            confirmDialog?.Show(message, () => ConfirmWholesale(pet, pay));
        }

        /// <summary>Confirms against the exact animal shown and its payout: time keeps passing while the dialog is open (§7), so the animal could be sold or its market price could shift before "はい".</summary>
        private void ConfirmWholesale(PetState pet, long expectedPay)
        {
            if (!session.Colony.Animals.Contains(pet) || shopService.WholesalePriceOf(pet) != expectedPay)
            {
                ShowShopMessage(ShopText.OfferChangedMessage);
                shopView?.Invalidate();
                RefreshShell();
                return;
            }

            var name = pet.Name;
            var pay = expectedPay;
            var wasSelected = state == pet;
            var result = shopService.Wholesale(session.Colony, pet.Id, GameNowUtc());
            if (result == ShopResult.Ok)
            {
                ShowShopMessage(ShopText.WholesaleMessage(name, pay));
                if (navigator != null && navigator.Tab == ShellTab.Incubator)
                {
                    ShowFeedback(ShopText.WholesaleMessage(name, pay));
                }

                if (wasSelected)
                {
                    var occupied = session.Colony.ShownCages();
                    if (occupied.Count > 0)
                    {
                        SelectCage(occupied[0].Id);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }

                SaveCurrentState();
                ColonyChanged?.Invoke();
                shopView?.Invalidate();
            }
            else
            {
                ShowShopMessage(ShopText.FailureMessage(result));
            }

            RefreshShell();
        }
    }
}
