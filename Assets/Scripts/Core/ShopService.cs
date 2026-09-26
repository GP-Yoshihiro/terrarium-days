using System;

namespace TerrariumDays.Core
{
    public enum ShopResult
    {
        Ok,
        NotFound,
        NotEnoughMoney,
        NoEmptyCage,
        NoRackSpace,
        RackLimit
    }

    /// <summary>
    /// Buying animals and supplies and wholesaling animals (§9). Every yen goes through the
    /// wallet so the ledger records it. Prices come from the market (§10.1) with no event demand.
    /// </summary>
    public sealed class ShopService
    {
        private readonly EconomyTuning economy;
        private readonly CareTuning care;

        public ShopService(EconomyTuning economy, CareTuning care)
        {
            this.economy = economy;
            this.care = care;
        }

        public long MarketOf(PetState pet) => MarketPrice.For(pet, EventDemand.None);

        public long PriceOf(ShopOffer offer) => MarketPrice.ShopPrice(MarketOf(offer.Animal), economy);

        public long WholesalePriceOf(PetState pet) => MarketPrice.WholesalePrice(MarketOf(pet), economy);

        public ShopResult BuyAnimal(Colony colony, int offerId, DateTimeOffset nowUtc)
        {
            var offer = colony.Shop.Offers.Find(o => o.OfferId == offerId);
            if (offer == null)
            {
                return ShopResult.NotFound;
            }

            var cage = colony.Cages.Find(c => c.IsEmpty);
            if (cage == null)
            {
                return ShopResult.NoEmptyCage;
            }

            var pet = offer.Animal;
            var name = $"レオパ{colony.NextAnimalId}";
            var note = $"{name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）を購入";
            if (!colony.Wallet.TrySpend(PriceOf(offer), LedgerCategory.AnimalPurchase, note, nowUtc))
            {
                return ShopResult.NotEnoughMoney;
            }

            colony.Shop.Offers.Remove(offer);
            pet.Name = name;
            pet.LastSavedAtUtc = nowUtc;
            pet.LastShedAtUtc = nowUtc;
            pet.NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(pet.Stage, care);
            pet.PersonalityKnown = false;
            pet.PersonalityRevealAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays);
            colony.AddAnimal(pet, cage);
            return ShopResult.Ok;
        }

        public ShopResult BuyItem(Colony colony, string itemId, DateTimeOffset nowUtc)
        {
            var item = ShopCatalog.Find(itemId, economy);
            if (item == null)
            {
                return ShopResult.NotFound;
            }

            if (item.Kind == ShopItemKind.Cage && !colony.CanAddCage)
            {
                return ShopResult.NoRackSpace;
            }

            if (item.Kind == ShopItemKind.Rack && colony.RackCount >= economy.MaxRacks)
            {
                return ShopResult.RackLimit;
            }

            if (!colony.Wallet.TrySpend(item.Price, LedgerCategory.Purchase, $"{item.Label}を購入", nowUtc))
            {
                return ShopResult.NotEnoughMoney;
            }

            switch (item.Kind)
            {
                case ShopItemKind.Cage:
                    colony.AddCage(item.CageSize);
                    break;
                case ShopItemKind.Rack:
                    colony.RackCount++;
                    break;
                case ShopItemKind.Incubator:
                    colony.Incubators.Add(item.Incubator);
                    break;
                default:
                    colony.Inventory.Add(item.Id, 1);
                    break;
            }

            return ShopResult.Ok;
        }

        /// <summary>Sells one of the player's animals to the shop at 40% of the market. Selling the last one is allowed (§ planner: 0 animals).</summary>
        public ShopResult Wholesale(Colony colony, int animalId, DateTimeOffset nowUtc)
        {
            var pet = colony.AnimalById(animalId);
            if (pet == null)
            {
                return ShopResult.NotFound;
            }

            var pay = WholesalePriceOf(pet);
            colony.RemoveAnimal(pet);
            colony.Wallet.Earn(pay, LedgerCategory.Wholesale, $"{pet.Name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）を卸売り", nowUtc);
            return ShopResult.Ok;
        }
    }
}
