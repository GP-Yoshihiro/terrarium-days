using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class PetBehaviourTests
    {
        private readonly CareTuning careTuning = new CareTuning();
        private readonly PetBehaviourTuning behaviourTuning = new PetBehaviourTuning();

        [Test]
        public void Evaluate_WellCaredForPet_IsLively()
        {
            var state = new PetState { Hunger = 80d, Hydration = 80d, Cleanliness = 80d, Health = 100d };

            Assert.That(PetMoodEvaluator.Evaluate(state, careTuning, behaviourTuning), Is.EqualTo(PetMood.Lively));
        }

        [Test]
        public void Evaluate_OneCareStatInTheWarningBand_IsNormal()
        {
            var state = new PetState { Hunger = 30d, Hydration = 80d, Cleanliness = 80d, Health = 100d };

            Assert.That(PetMoodEvaluator.Evaluate(state, careTuning, behaviourTuning), Is.EqualTo(PetMood.Normal));
        }

        [Test]
        public void Evaluate_AnyCareStatBelowTheLowThreshold_IsSluggish()
        {
            var state = new PetState { Hunger = 80d, Hydration = 80d, Cleanliness = 10d, Health = 100d };

            Assert.That(PetMoodEvaluator.Evaluate(state, careTuning, behaviourTuning), Is.EqualTo(PetMood.Sluggish));
        }

        [Test]
        public void Evaluate_PoorHealth_IsSluggishEvenWhenCareIsFine()
        {
            var state = new PetState { Hunger = 90d, Hydration = 90d, Cleanliness = 90d, Health = 30d };

            Assert.That(PetMoodEvaluator.Evaluate(state, careTuning, behaviourTuning), Is.EqualTo(PetMood.Sluggish));
        }

        [TestCase(10d, 80d, 80d, PetMoodMessage.Hungry)]
        [TestCase(80d, 10d, 80d, PetMoodMessage.Thirsty)]
        [TestCase(80d, 80d, 10d, PetMoodMessage.Dirty)]
        public void MoodMessage_PointsAtTheMostPressingNeed(double hunger, double hydration, double cleanliness, string expected)
        {
            var state = new PetState { Hunger = hunger, Hydration = hydration, Cleanliness = cleanliness };

            Assert.That(PetMoodMessage.For(state, careTuning), Is.EqualTo(expected));
        }

        [Test]
        public void MoodMessage_CareIsFineButHealthIsLow_SaysUnwell()
        {
            var state = new PetState { Hunger = 90d, Hydration = 90d, Cleanliness = 90d, Health = 20d };

            Assert.That(PetMoodMessage.For(state, careTuning), Is.EqualTo(PetMoodMessage.Unwell));
        }

        [Test]
        public void MoodMessage_EverythingIsFine_SaysHappy()
        {
            Assert.That(PetMoodMessage.For(new PetState(), careTuning), Is.EqualTo(PetMoodMessage.Happy));
        }

        [Test]
        public void NextAfterRest_WalkTargetsStayInsideTheFloorAndMoveFarEnough()
        {
            var planner = new PetBehaviourPlanner(behaviourTuning, new Random(1234));
            var walks = 0;

            for (var i = 0; i < 300; i++)
            {
                var intent = planner.NextAfterRest(PetMood.Lively, DayPhase.Night, 0.5f, 0.1f);
                if (!intent.Walk)
                {
                    continue;
                }

                walks++;
                Assert.That(intent.TargetX, Is.InRange(behaviourTuning.MinX, behaviourTuning.MaxX));
                Assert.That(intent.TargetDepth, Is.InRange(behaviourTuning.MinDepth, behaviourTuning.MaxDepth));
                Assert.That(Math.Abs(intent.TargetX - 0.5f), Is.GreaterThanOrEqualTo(behaviourTuning.MinWalkDistance));
            }

            Assert.That(walks, Is.GreaterThan(180), "a lively gecko at night mostly wanders");
        }

        [Test]
        public void SluggishPet_WalksSlowerAndRestsLongerThanALivelyOne()
        {
            var planner = new PetBehaviourPlanner(behaviourTuning, new Random(42));

            Assert.That(planner.SpeedFor(PetMood.Sluggish), Is.LessThan(planner.SpeedFor(PetMood.Lively)));

            for (var i = 0; i < 50; i++)
            {
                Assert.That(planner.Rest(PetMood.Sluggish).Seconds, Is.GreaterThanOrEqualTo(behaviourTuning.SluggishIdleMinSeconds));
                Assert.That(planner.Rest(PetMood.Lively).Seconds, Is.LessThanOrEqualTo(behaviourTuning.LivelyIdleMaxSeconds));
            }
        }

        private int CountSleeps(PetMood mood, DayPhase phase)
        {
            var planner = new PetBehaviourPlanner(behaviourTuning, new Random(7));
            var sleeps = 0;
            for (var i = 0; i < 1000; i++)
            {
                if (planner.NextAfterRest(mood, phase, 0.5f, 0.5f).Activity == PetActivity.Sleep)
                {
                    sleeps++;
                }
            }

            return sleeps;
        }

        [Test]
        public void LikeARealLeopardGecko_ItSleepsByDayAndIsActiveAtNight()
        {
            Assert.That(CountSleeps(PetMood.Lively, DayPhase.Day), Is.GreaterThan(CountSleeps(PetMood.Lively, DayPhase.Night) * 5));
            Assert.That(CountSleeps(PetMood.Lively, DayPhase.Evening), Is.LessThan(CountSleeps(PetMood.Lively, DayPhase.Morning)));
        }

        [Test]
        public void SluggishPet_SleepsFarMoreOftenThanALivelyOne()
        {
            Assert.That(CountSleeps(PetMood.Sluggish, DayPhase.Night), Is.GreaterThan(CountSleeps(PetMood.Lively, DayPhase.Night) * 3));
        }

        [Test]
        public void DaytimeSleep_LastsMinutesWhileNightNapsAreShort()
        {
            var planner = new PetBehaviourPlanner(behaviourTuning, new Random(11));

            for (var i = 0; i < 500; i++)
            {
                var day = planner.NextAfterRest(PetMood.Lively, DayPhase.Day, 0.5f, 0.5f);
                if (day.Activity == PetActivity.Sleep)
                {
                    Assert.That(day.Seconds, Is.InRange(behaviourTuning.DaySleepMinSeconds, behaviourTuning.DaySleepMaxSeconds));
                }

                var night = planner.NextAfterRest(PetMood.Sluggish, DayPhase.Night, 0.5f, 0.5f);
                if (night.Activity == PetActivity.Sleep)
                {
                    Assert.That(night.Seconds, Is.InRange(behaviourTuning.NightSleepMinSeconds, behaviourTuning.NightSleepMaxSeconds));
                }
            }

            Assert.That(behaviourTuning.DaySleepMinSeconds, Is.GreaterThanOrEqualTo(180f), "a daytime sleep lasts minutes");
        }

        [Test]
        public void SometimesItJustWagsItsTail()
        {
            var planner = new PetBehaviourPlanner(behaviourTuning, new Random(3));
            var wags = 0;
            for (var i = 0; i < 1000; i++)
            {
                if (planner.NextAfterRest(PetMood.Lively, DayPhase.Night, 0.5f, 0.5f).Activity == PetActivity.TailWag)
                {
                    wags++;
                }
            }

            Assert.That(wags, Is.InRange(60, 180));
        }
    }
}
