using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CandlingTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly CareTuning care = new CareTuning();

        private static DateTimeOffset AtDay(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        private static Egg NewEgg(bool fertile, double development) =>
            new Egg { LaidAtUtc = Epoch, Fertile = fertile, DevelopmentPercent = development };

        [Test]
        public void BeforeSevenDays_NothingIsKnown()
        {
            Assert.AreEqual(CandlingStage.Unknown, Candling.StageOf(NewEgg(false, 0d), AtDay(6.9d), care));
            Assert.AreEqual(CandlingStage.Unknown, Candling.StageOf(NewEgg(true, 20d), AtDay(6.9d), care));
        }

        [Test]
        public void FromSevenDays_InfertileEggsShow()
        {
            Assert.AreEqual(CandlingStage.Infertile, Candling.StageOf(NewEgg(false, 0d), AtDay(7d), care));
        }

        [TestCase(0d, CandlingStage.Early)]
        [TestCase(33d, CandlingStage.Early)]
        [TestCase(33.1d, CandlingStage.Middle)]
        [TestCase(66d, CandlingStage.Middle)]
        [TestCase(66.1d, CandlingStage.Late)]
        [TestCase(95d, CandlingStage.Late)]
        [TestCase(95.1d, CandlingStage.AboutToHatch)]
        [TestCase(100d, CandlingStage.AboutToHatch)]
        public void FertileEggs_ShowTheirDevelopment(double development, CandlingStage expected)
        {
            Assert.AreEqual(expected, Candling.StageOf(NewEgg(true, development), AtDay(10d), care));
        }

        [Test]
        public void DeadEggs_ShowAsDead_EvenEarly()
        {
            var egg = NewEgg(true, 5d);
            egg.Failure = EggFailure.Mold;

            Assert.AreEqual(CandlingStage.Dead, Candling.StageOf(egg, AtDay(2d), care));
        }

        [TestCase(26d, 0.95d)]
        [TestCase(28d, 0.95d)]
        [TestCase(28.5d, 0.825d)]
        [TestCase(29d, 0.70d)]
        [TestCase(30d, 0.60d)]
        [TestCase(30.5d, 0.425d)]
        [TestCase(31d, 0.25d)]
        [TestCase(33d, 0.25d)]
        public void FemaleChance_FollowsTheTable(double celsius, double expected)
        {
            Assert.AreEqual(expected, HatchSex.FemaleChance(celsius), 1e-9);
        }

        [Test]
        public void MiddleThirdAverage_UsesTheRecord_OrTheFallback()
        {
            var recorded = new Egg { MiddleThirdGameDays = 10d, MiddleThirdTemperatureSum = 305d };
            var unrecorded = new Egg();

            Assert.AreEqual(30.5d, HatchSex.MiddleThirdAverage(recorded, 26d), 1e-9);
            Assert.AreEqual(26d, HatchSex.MiddleThirdAverage(unrecorded, 26d), 1e-9);
        }

        [Test]
        public void Decide_ComparesOneRollWithTheChance()
        {
            var egg = new Egg();

            Assert.AreEqual(Sex.Female, HatchSex.Decide(egg, 28d, new QueuedRandom(0.5d)));
            Assert.AreEqual(Sex.Male, HatchSex.Decide(egg, 31d, new QueuedRandom(0.5d)));
        }
    }
}
