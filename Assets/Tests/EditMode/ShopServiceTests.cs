using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ShopServiceTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        private readonly EconomyTuning economy = new EconomyTuning();
        private readonly CareTuning care = new CareTuning();
        private ShopService shop;

        [SetUp]
        public void SetUp() => shop = new ShopService(economy, care);

        private Colony ColonyWithOffer(out ShopOffer offer)
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            offer = new ShopOffer { OfferId = 7, Animal = ShopStockGenerator.Animal(new Random(5), Now, care) };
            colony.Shop.Offers.Add(offer);
            return colony;
        }

        [Test]
        public void PriceOf_IsTheMarketTimesOnePointTwo()
        {
            ColonyWithOffer(out var offer);

            Assert.That(shop.PriceOf(offer), Is.EqualTo(MarketPrice.ShopPrice(MarketPrice.For(offer.Animal), economy)));
        }

        [Test]
        public void BuyAnimal_PaysPutsItInTheFirstEmptyCageAndHidesItsPersonality()
        {
            var colony = ColonyWithOffer(out var offer);
            var empty = colony.AddCage(CageSize.Small);
            colony.AddCage(CageSize.Large);
            colony.Wallet.Money = 1_000_000;
            var price = shop.PriceOf(offer);

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.Ok));

            var pet = colony.AnimalIn(empty);
            Assert.That(pet, Is.SameAs(offer.Animal));
            Assert.That(pet.Name, Is.EqualTo("レオパ2"));
            Assert.That(pet.PersonalityKnown, Is.False);
            Assert.That(pet.PersonalityRevealAtUtc, Is.EqualTo(Now + GameCalendar.RealTimeFor(7d)));
            Assert.That(pet.LastSavedAtUtc, Is.EqualTo(Now));
            Assert.That(colony.Wallet.Money, Is.EqualTo(1_000_000 - price));
            var entry = colony.Wallet.Ledger.Last();
            Assert.That((entry.Category, entry.Amount), Is.EqualTo((LedgerCategory.AnimalPurchase, -price)));
            StringAssert.StartsWith("レオパ2（", entry.Note);
            Assert.That(colony.Shop.Offers, Is.Empty);
        }

        [Test]
        public void BuyAnimal_WithoutAnEmptyCage_ChangesNothing()
        {
            var colony = ColonyWithOffer(out _);
            colony.Wallet.Money = 1_000_000;

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.NoEmptyCage));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(colony.Shop.Offers, Has.Count.EqualTo(1));
            Assert.That(colony.Wallet.Money, Is.EqualTo(1_000_000));
        }

        [Test]
        public void BuyAnimal_WithoutEnoughMoney_ChangesNothing()
        {
            var colony = ColonyWithOffer(out var offer);
            colony.AddCage(CageSize.Standard);
            colony.Wallet.Money = shop.PriceOf(offer) - 1;

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.NotEnoughMoney));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(colony.Shop.Offers, Has.Count.EqualTo(1));
            Assert.That(colony.Wallet.Ledger, Is.Empty);
        }

        [Test]
        public void BuyAnimal_WithExactlyEnoughMoney_LeavesZero()
        {
            var colony = ColonyWithOffer(out var offer);
            colony.AddCage(CageSize.Standard);
            colony.Wallet.Money = shop.PriceOf(offer);

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.Ok));
            Assert.That(colony.Wallet.Money, Is.EqualTo(0));
        }

        [Test]
        public void BuyAnimal_AnOfferThatIsGone_IsNotFound()
        {
            var colony = ColonyWithOffer(out _);

            Assert.That(shop.BuyAnimal(colony, 99, Now), Is.EqualTo(ShopResult.NotFound));
        }

        [Test]
        public void BuyItem_ACageNeedsRoomOnARack()
        {
            var colony = ColonyWithOffer(out _);

            for (var i = 0; i < 3; i++)
            {
                Assert.That(shop.BuyItem(colony, "cage_large", Now), Is.EqualTo(ShopResult.Ok));
            }

            Assert.That(shop.BuyItem(colony, "cage_small", Now), Is.EqualTo(ShopResult.NoRackSpace));
            Assert.That(colony.Cages.Count, Is.EqualTo(4));
            Assert.That(colony.Cages[3].Size, Is.EqualTo(CageSize.Large));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 - 3 * 10000));
            Assert.That(colony.Wallet.Ledger.Last().Category, Is.EqualTo(LedgerCategory.Purchase));
            Assert.That(colony.Wallet.Ledger.Last().Note, Is.EqualTo("大型ケージを購入"));
        }

        [Test]
        public void BuyItem_RacksStopAtTheLimit()
        {
            var colony = ColonyWithOffer(out _);
            colony.Wallet.Money = 1_000_000;

            for (var i = 1; i < economy.MaxRacks; i++)
            {
                Assert.That(shop.BuyItem(colony, "rack", Now), Is.EqualTo(ShopResult.Ok));
            }

            Assert.That(colony.RackCount, Is.EqualTo(economy.MaxRacks));
            Assert.That(shop.BuyItem(colony, "rack", Now), Is.EqualTo(ShopResult.RackLimit));
        }

        [Test]
        public void BuyItem_DecorAndNestBoxesGoToTheInventory()
        {
            var colony = ColonyWithOffer(out _);

            Assert.That(shop.BuyItem(colony, "driftwood_01", Now), Is.EqualTo(ShopResult.Ok));
            Assert.That(shop.BuyItem(colony, ShopCatalog.NestBoxId, Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Inventory.Count("driftwood_01"), Is.EqualTo(1));
            Assert.That(colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(1));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 - 2000 - 1500));
        }

        [Test]
        public void BuyItem_AnIncubatorAddsItsModel()
        {
            var colony = ColonyWithOffer(out _);

            Assert.That(shop.BuyItem(colony, "incubator_standard", Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Standard }));
            Assert.That(shop.BuyItem(colony, "incubator_luxury", Now), Is.EqualTo(ShopResult.NotEnoughMoney));
        }

        [Test]
        public void BuyItem_AnUnknownId_IsNotFound()
        {
            Assert.That(shop.BuyItem(ColonyWithOffer(out _), "golden_cage", Now), Is.EqualTo(ShopResult.NotFound));
        }

        [Test]
        public void Wholesale_PaysFortyPercentOfTheMarketAndFreesTheCage()
        {
            var colony = ColonyWithOffer(out _);
            var second = colony.AddAnimal(new PetState { Name = "レオパ2", Stage = GrowthStage.Juvenile, PersonalityKnown = false }, colony.AddCage(CageSize.Standard));
            var cage = colony.CageOf(second);
            var pay = MarketPrice.WholesalePrice(MarketPrice.For(second), economy);

            Assert.That(shop.WholesalePriceOf(second), Is.EqualTo(pay));
            Assert.That(shop.Wholesale(colony, second.Id, Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(cage.IsEmpty, Is.True);
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 + pay));
            var entry = colony.Wallet.Ledger.Last();
            Assert.That((entry.Category, entry.Amount), Is.EqualTo((LedgerCategory.Wholesale, pay)));
            StringAssert.StartsWith("レオパ2（", entry.Note);
        }

        [Test]
        public void Wholesale_TheLastAnimalCanBeSoldLeavingAnEmptyRoom()
        {
            var colony = ColonyWithOffer(out _);
            var cage = colony.Cages[0];

            Assert.That(shop.Wholesale(colony, colony.Animals[0].Id, Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Animals, Is.Empty);
            Assert.That(cage.IsEmpty, Is.True);
            Assert.That(colony.OccupiedCages(), Is.Empty);
            Assert.That(shop.Wholesale(colony, 99, Now), Is.EqualTo(ShopResult.NotFound));
        }

        [Test]
        public void AfterSellingEverything_AnAnimalCanBeBoughtBack()
        {
            var colony = ColonyWithOffer(out var offer);
            shop.Wholesale(colony, colony.Animals[0].Id, Now);
            colony.Wallet.Money = 1_000_000;

            Assert.That(shop.BuyAnimal(colony, offer.OfferId, Now), Is.EqualTo(ShopResult.Ok));
            Assert.That(colony.AnimalIn(colony.Cages[0]), Is.SameAs(offer.Animal));
        }

        [Test]
        public void BuyingThenSelling_LeavesBothInTheLedger()
        {
            var colony = ColonyWithOffer(out var offer);
            colony.AddCage(CageSize.Standard);
            colony.Wallet.Money = 1_000_000;
            var price = shop.PriceOf(offer);
            shop.BuyAnimal(colony, 7, Now);
            var pay = shop.WholesalePriceOf(offer.Animal);

            shop.Wholesale(colony, offer.Animal.Id, Now.AddMinutes(5));

            Assert.That(colony.Wallet.Ledger.Select(e => (e.Category, e.Amount)), Is.EqualTo(new[]
            {
                (LedgerCategory.AnimalPurchase, -price),
                (LedgerCategory.Wholesale, pay),
            }));
            Assert.That(colony.Wallet.Money, Is.EqualTo(1_000_000 - price + pay));
        }

        [Test]
        public void Catalog_ListsEverySpecItem()
        {
            var items = ShopCatalog.Items(economy);

            Assert.That(items.Select(i => i.Id), Is.EqualTo(new[]
            {
                "cage_small", "cage_standard", "cage_large", "rack",
                "rock_01", "plant_01", "water_dish_01", "heat_lamp_01", "driftwood_01",
                ShopCatalog.NestBoxId, "incubator_standard", "incubator_luxury",
            }));
            Assert.That(ShopCatalog.Find("cage_standard", economy).Price, Is.EqualTo(6000));
            Assert.That(ShopCatalog.Find("heat_lamp_01", economy).Price, Is.EqualTo(3000));
            Assert.That(ShopCatalog.Find("incubator_luxury", economy).Price, Is.EqualTo(40000));
            Assert.That(ShopCatalog.Find("incubator_luxury", economy).UsableFromPhase, Is.EqualTo(5));
            Assert.That(ShopCatalog.Find(ShopCatalog.NestBoxId, economy).UsableFromPhase, Is.EqualTo(4));
            Assert.That(ShopCatalog.Find("rack", economy).UsableFromPhase, Is.EqualTo(0));
        }

        [Test]
        public void PersonalityReveal_HappensOnceWhenDue()
        {
            var pet = new PetState { Name = "レオパ3", Personality = Personality.Curious, PersonalityKnown = false, PersonalityRevealAtUtc = Now };

            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now.AddMinutes(-1)), Is.False);
            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now), Is.True);
            Assert.That(pet.PersonalityKnown, Is.True);
            Assert.That(pet.PersonalityRevealAtUtc, Is.Null);
            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now.AddDays(1)), Is.False);
            Assert.That(PersonalityReveal.Message(pet), Is.EqualTo("レオパ3の性格は「好奇心旺盛」のようです"));
        }

        [Test]
        public void PersonalityReveal_WithoutADate_DoesNothing()
        {
            var pet = new PetState { PersonalityKnown = false };

            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now), Is.False);
            Assert.That(pet.PersonalityKnown, Is.False);
        }

        [Test]
        public void AWeakAnimal_IsPricedAtThirtyPercent()
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            var pet = colony.Animals[0];
            var healthy = shop.MarketOf(pet);

            pet.Weak = true;

            Assert.That(shop.MarketOf(pet), Is.EqualTo(MarketPrice.For(pet, weak: true)));
            Assert.That(shop.MarketOf(pet), Is.LessThan(healthy));
        }
    }
}
