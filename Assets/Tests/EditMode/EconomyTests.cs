using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EconomyTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        private readonly EconomyTuning tuning = new EconomyTuning();

        [Test]
        public void TrySpend_RecordsAnExpenseWhenAffordable()
        {
            var wallet = new Wallet { Money = 100 };

            Assert.That(wallet.TrySpend(80, LedgerCategory.Food, "餌代", Now), Is.True);

            Assert.That(wallet.Money, Is.EqualTo(20));
            Assert.That(wallet.Ledger[0].Amount, Is.EqualTo(-80));
            Assert.That(wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Food));
        }

        [Test]
        public void TrySpend_RefusesWithoutEnoughMoneyAndRecordsNothing()
        {
            var wallet = new Wallet { Money = 10 };

            Assert.That(wallet.TrySpend(30, LedgerCategory.Food, "餌代", Now), Is.False);

            Assert.That(wallet.Money, Is.EqualTo(10));
            Assert.That(wallet.Ledger, Is.Empty);
        }

        [Test]
        public void Charge_MayGoNegative()
        {
            var wallet = new Wallet { Money = 100 };

            wallet.Charge(800, LedgerCategory.Electricity, "電気代", Now);

            Assert.That(wallet.Money, Is.EqualTo(-700));
        }

        [TestCase(GrowthStage.Baby, 30)]
        [TestCase(GrowthStage.Juvenile, 50)]
        [TestCase(GrowthStage.Adult, 80)]
        public void FeedCost_DependsOnTheStage(GrowthStage stage, long yen)
        {
            Assert.That(MaintenanceCosts.FeedCost(stage, tuning), Is.EqualTo(yen));
        }

        [Test]
        public void MonthlyElectricity_IsPerCageAndPerIncubator()
        {
            Assert.That(MaintenanceCosts.MonthlyElectricity(3, 1, tuning), Is.EqualTo(3 * 300 + 500));
        }

        [Test]
        public void TrySpend_CanSpendExactlyEverything()
        {
            var wallet = new Wallet { Money = 80 };

            Assert.That(wallet.TrySpend(80, LedgerCategory.Purchase, "岩を購入", Now), Is.True);

            Assert.That(wallet.Money, Is.EqualTo(0));
            Assert.That(wallet.Ledger, Has.Count.EqualTo(1));
        }

        [Test]
        public void Earn_RecordsIncome()
        {
            var wallet = new Wallet { Money = 100 };

            wallet.Earn(1600, LedgerCategory.Wholesale, "卸売り", Now);

            Assert.That(wallet.Money, Is.EqualTo(1700));
            Assert.That(wallet.Ledger[0].Amount, Is.EqualTo(1600));
            Assert.That(wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Wholesale));
        }

        [Test]
        public void NegativeAmounts_AreRejectedAndRecordNothing()
        {
            var wallet = new Wallet { Money = 100 };

            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1, LedgerCategory.Food, "x", Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Charge(-1, LedgerCategory.Electricity, "x", Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Earn(-1, LedgerCategory.Wholesale, "x", Now));

            Assert.That(wallet.Money, Is.EqualTo(100));
            Assert.That(wallet.Ledger, Is.Empty);
        }

        [Test]
        public void ShopPrices_FollowTheSpec()
        {
            Assert.That(tuning.CagePrice(CageSize.Small), Is.EqualTo(3000));
            Assert.That(tuning.CagePrice(CageSize.Standard), Is.EqualTo(6000));
            Assert.That(tuning.CagePrice(CageSize.Large), Is.EqualTo(10000));
            Assert.That(tuning.RackPrice, Is.EqualTo(8000));
            Assert.That(tuning.MaxRacks, Is.EqualTo(4));
            Assert.That(tuning.NestBoxPrice, Is.EqualTo(1500));
            Assert.That(tuning.StandardIncubatorPrice, Is.EqualTo(15000));
            Assert.That(tuning.LuxuryIncubatorPrice, Is.EqualTo(40000));
            Assert.That(tuning.ShopMarkup, Is.EqualTo(1.2d));
            Assert.That(tuning.WholesaleRate, Is.EqualTo(0.4d));
            Assert.That(tuning.DecorPrices.Count, Is.EqualTo(5));
            foreach (var price in tuning.DecorPrices.Values)
            {
                Assert.That(price, Is.InRange(1000L, 3000L));
            }
        }
    }
}
