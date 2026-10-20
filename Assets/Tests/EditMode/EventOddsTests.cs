using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventOddsTests
    {
        [Test]
        public void SaleChance_AtTheBaseValue_IsHalfTheCap()
        {
            Assert.That(EventOdds.SaleChance(10000, 0, 10000, EventKind.Small), Is.EqualTo(0.6d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(10000, 0, 10000, EventKind.Big), Is.EqualTo(0.75d).Within(1e-9));
        }

        [Test]
        public void SaleChance_At130Percent_IsAQuarterOfTheCap()
        {
            Assert.That(EventOdds.SaleChance(13000, 0, 10000, EventKind.Small), Is.EqualTo(0.3d).Within(1e-9));
        }

        [Test]
        public void SaleChance_At70Percent_IsThreeQuartersOfTheCapUpTo98Percent()
        {
            Assert.That(EventOdds.SaleChance(7000, 0, 10000, EventKind.Small), Is.EqualTo(0.9d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(7000, 0, 10000, EventKind.Big), Is.EqualTo(EventOdds.MaxChance));
            Assert.That(EventOdds.SaleChance(1000, 0, 10000, EventKind.Big), Is.EqualTo(0.98d));
        }

        [Test]
        public void Tolerance_UsesHalfOfItForTheEffectivePrice()
        {
            Assert.That(EventOdds.EffectivePrice(10000, 20), Is.EqualTo(9000d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(10000, 20, 9000, EventKind.Small), Is.EqualTo(0.6d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(10000, 20, 10000, EventKind.Small), Is.GreaterThan(EventOdds.SaleChance(10000, 0, 10000, EventKind.Small)));
        }

        [Test]
        public void SaleChance_WithoutABaseValueOrPrice_IsZero()
        {
            Assert.That(EventOdds.SaleChance(10000, 0, 0, EventKind.Small), Is.EqualTo(0d));
            Assert.That(EventOdds.SaleChance(0, 0, 10000, EventKind.Small), Is.EqualTo(0d));
        }

        [TestCase(0.1d, 5)]
        [TestCase(0.6d, 5)]
        [TestCase(0.98d, 5)]
        [TestCase(0.3d, 8)]
        [TestCase(0.75d, 8)]
        public void PerActionChance_AllServeMatchesEventChance(double eventChance, int actions)
        {
            var a = EventOdds.PerActionChance(eventChance, actions);

            Assert.That(1d - Math.Pow(1d - a, actions), Is.EqualTo(eventChance).Within(1e-12));
            Assert.That(a, Is.LessThan(eventChance));
        }

        [Test]
        public void ActionChance_BrowseIsSixtyPercentOfServe()
        {
            var serve = EventOdds.ActionChance(0.6d, 5, EventActionKind.Serve);

            Assert.That(serve, Is.EqualTo(EventOdds.PerActionChance(0.6d, 5)));
            Assert.That(EventOdds.ActionChance(0.6d, 5, EventActionKind.Browse), Is.EqualTo(serve * 0.6d).Within(1e-12));
            Assert.That(EventOdds.PerActionChance(0.6d, 0), Is.EqualTo(0d));
        }

        [Test]
        public void ExpectedValue_IsTheEffectivePriceTimesTheChanceToTheNearest100()
        {
            Assert.That(EventOdds.ExpectedValue(10000, 10, 0.6d), Is.EqualTo(5700));
            Assert.That(EventOdds.ExpectedValue(12345, 0, 0.5d), Is.EqualTo(6200));
            Assert.That(EventOdds.ExpectedValue(10000, 0, 0d), Is.EqualTo(0));
        }

        [Test]
        public void SaleAmount_TakesUpToTheToleranceOff()
        {
            Assert.That(EventOdds.SaleAmount(10000, 20, 0d), Is.EqualTo(10000));
            Assert.That(EventOdds.SaleAmount(10000, 20, 0.5d), Is.EqualTo(9000));
            Assert.That(EventOdds.SaleAmount(10000, 20, 1d), Is.EqualTo(8000));
            Assert.That(EventOdds.SaleAmount(10000, 0, 0.9d), Is.EqualTo(10000));
            Assert.That(EventOdds.SaleAmount(12340, 5, 0.37d), Is.EqualTo(12100));
        }

        [Test]
        public void Tolerances_AreTheFiveSteps()
        {
            Assert.That(EventOdds.TolerancePercents, Is.EqualTo(new[] { 0, 5, 10, 15, 20 }));
            foreach (var valid in new[] { 0, 5, 10, 15, 20 })
            {
                Assert.That(EventOdds.IsValidTolerance(valid), Is.True);
            }

            foreach (var invalid in new[] { -5, 3, 25 })
            {
                Assert.That(EventOdds.IsValidTolerance(invalid), Is.False);
            }
        }
    }
}
