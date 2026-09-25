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
    }
}
