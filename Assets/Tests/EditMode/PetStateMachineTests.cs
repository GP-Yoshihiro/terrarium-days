using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class PetStateMachineTests
    {
        private readonly PetBehaviourTuning tuning = new PetBehaviourTuning();

        private PetBehaviour Create(int seed = 5) => new PetBehaviour(tuning, new Random(seed));

        private static void Run(PetBehaviour pet, float seconds, PetMood mood = PetMood.Lively, float step = 1f / 30f)
        {
            for (var t = 0f; t < seconds; t += step)
            {
                pet.Tick(step, mood);
            }
        }

        private PetBehaviour AsleepPet(float sleepSeconds = 10f)
        {
            // Daytime with a certain nap, so the first decision is to sleep where it stands.
            tuning.DaySleepChance = 1d;
            tuning.DaySleepMinSeconds = tuning.DaySleepMaxSeconds = sleepSeconds;
            var pet = Create();
            pet.Phase = DayPhase.Day;
            Run(pet, 1.1f);
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Sleep));
            return pet;
        }

        private void NoRandomActivities()
        {
            tuning.MorningSleepChance = tuning.DaySleepChance = tuning.EveningSleepChance = tuning.NightSleepChance = 0d;
            tuning.NormalMoodSleepBonus = tuning.SluggishMoodSleepBonus = 0d;
            tuning.YawnChance = 0d;
            tuning.TailWagChance = 0d;
        }

        [Test]
        public void StartsIdle()
        {
            Assert.That(Create().Clip, Is.EqualTo(PetClip.Idle));
        }

        [Test]
        public void Feed_TwitchesItsTailThenStrikesAndEats()
        {
            var pet = Create();

            pet.Feed();
            Assert.That(pet.Clip, Is.EqualTo(PetClip.TailWag));

            Run(pet, tuning.PreStrikeTailWagSeconds + 0.05f);
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Eat));

            Run(pet, tuning.EatSeconds + 0.1f);
            Assert.That(pet.Clip, Is.Not.EqualTo(PetClip.Eat));
        }

        [Test]
        public void AThreatDuringThePreStrikeTwitch_CancelsTheMeal()
        {
            var pet = Create();
            pet.Feed();

            for (var i = 0; i < tuning.ThreatTapCount; i++)
            {
                pet.Tap(PetMood.Lively);
            }

            Run(pet, tuning.ThreatSeconds + 0.1f);
            Assert.That(pet.Clip, Is.Not.EqualTo(PetClip.Eat));
        }

        [Test]
        public void RefuseFood_TurnsAwayWithoutEating()
        {
            var pet = Create();
            var facing = pet.Facing;

            pet.RefuseFood(PetMood.Lively);

            Assert.That(pet.Facing, Is.EqualTo(-facing));
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Idle));
        }

        [Test]
        public void Cheer_PlaysHappyAndCountsACheerForTheHearts()
        {
            var pet = Create();

            Assert.That(pet.Cheer(), Is.True);

            Assert.That(pet.Clip, Is.EqualTo(PetClip.Happy));
            Assert.That(pet.CheerCount, Is.EqualTo(1));
        }

        [Test]
        public void Tap_WhenWellCaredFor_MakesThePetHappy()
        {
            var pet = Create();

            Assert.That(pet.Tap(PetMood.Lively), Is.EqualTo(PetTapReaction.Happy));
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Happy));
        }

        [Test]
        public void Tap_WhenSluggish_OnlyNoticesWithoutCheering()
        {
            var pet = Create();

            Assert.That(pet.Tap(PetMood.Sluggish), Is.EqualTo(PetTapReaction.Noticed));
            Assert.That(pet.CheerCount, Is.EqualTo(0));
        }

        [Test]
        public void TappingTooOftenInAShortTime_MakesThePetThreaten()
        {
            var pet = Create();

            for (var i = 0; i < tuning.ThreatTapCount - 1; i++)
            {
                Assert.That(pet.Tap(PetMood.Lively), Is.Not.EqualTo(PetTapReaction.Threat));
                Run(pet, 0.3f);
            }

            Assert.That(pet.Tap(PetMood.Lively), Is.EqualTo(PetTapReaction.Threat));
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Threat));
        }

        [Test]
        public void RepeatedTapsWhileAlreadyHappy_DoNotPileUpMoreHearts()
        {
            var pet = Create();

            pet.Tap(PetMood.Lively);
            Run(pet, 0.2f);
            pet.Tap(PetMood.Lively);
            Run(pet, 0.2f);
            pet.Tap(PetMood.Lively);

            Assert.That(pet.CheerCount, Is.EqualTo(1));
        }

        [Test]
        public void TapsSpreadOutOverTime_NeverTriggerTheThreat()
        {
            var pet = Create();

            for (var i = 0; i < tuning.ThreatTapCount * 2; i++)
            {
                Assert.That(pet.Tap(PetMood.Lively), Is.Not.EqualTo(PetTapReaction.Threat));
                Run(pet, tuning.ThreatTapWindowSeconds);
            }
        }

        [Test]
        public void TappingASleepingPet_WakesItWithAYawn()
        {
            tuning.WakeStartleChance = 0d;
            var pet = AsleepPet();

            Assert.That(pet.Tap(PetMood.Lively), Is.EqualTo(PetTapReaction.Woke));
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Yawn));
        }

        [Test]
        public void TappingASleepingPet_CanStartleItIntoAThreat()
        {
            tuning.WakeStartleChance = 1d;
            var pet = AsleepPet();

            Assert.That(pet.Tap(PetMood.Lively), Is.EqualTo(PetTapReaction.Startled));
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Threat));
        }

        [Test]
        public void ASleepingPet_StaysAsleepForTheWholeSleep()
        {
            var pet = AsleepPet(sleepSeconds: 300f);

            for (var t = 0f; t < 299f; t += 1f)
            {
                pet.Tick(1f, PetMood.Lively);
                Assert.That(pet.Clip, Is.EqualTo(PetClip.Sleep), $"woke up early at {t}s");
            }
        }

        [Test]
        public void AfterBeingWoken_ItStaysUpForAWhileEvenByDay()
        {
            tuning.WakeStartleChance = 0d;
            var pet = AsleepPet();
            pet.Tap(PetMood.Lively);

            var slept = false;
            for (var t = 0f; t < tuning.AwakeAfterWakingSeconds - 1f; t += 1f / 30f)
            {
                pet.Tick(1f / 30f, PetMood.Lively);
                slept |= pet.IsAsleep;
            }

            Assert.That(slept, Is.False);
        }

        [Test]
        public void WithAShelter_ItWalksOverAndSleepsJustBehindIt()
        {
            tuning.DaySleepChance = 1d;
            tuning.DaySleepMinSeconds = tuning.DaySleepMaxSeconds = 60f;
            var pet = Create();
            pet.Phase = DayPhase.Day;
            pet.SetShelter(0.2f, 0.5f);

            Run(pet, 1.1f);
            Assert.That(pet.IsHeadingToBed, Is.True);

            Run(pet, 40f);
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Sleep));
            Assert.That(pet.Depth, Is.LessThan(0.5f), "tucked in just behind the shelter");
            Assert.That(Math.Abs(pet.X - 0.2f), Is.EqualTo(tuning.ShelterSideOffset).Within(1e-4f));
        }

        [Test]
        public void ASleepingPet_SleepsThroughWaterAndCleaning()
        {
            var pet = AsleepPet();

            Assert.That(pet.Cheer(), Is.False);
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Sleep));
        }

        [Test]
        public void Sleep_EndsWithAYawnThenIdle()
        {
            var pet = AsleepPet();
            // Now night-time, so it does not simply fall back asleep.
            pet.Phase = DayPhase.Night;
            tuning.NightSleepChance = 0d;

            Run(pet, tuning.DaySleepMaxSeconds);
            Assert.That(pet.Clip, Is.EqualTo(PetClip.Yawn));

            Run(pet, tuning.YawnSeconds + 0.1f);
            Assert.That(pet.Clip, Is.Not.EqualTo(PetClip.Yawn).And.Not.EqualTo(PetClip.Sleep));
        }

        [Test]
        public void Walking_FacesTheDirectionOfTravelAndStaysOnTheFloor()
        {
            NoRandomActivities();
            var pet = Create(9);

            for (var i = 0; i < 3000; i++)
            {
                var before = pet.X;
                pet.Tick(1f / 30f, PetMood.Lively);

                Assert.That(pet.X, Is.InRange(0f, 1f));
                Assert.That(pet.Depth, Is.InRange(0f, 1f));
                if (pet.Clip == PetClip.Walk && Math.Abs(pet.X - before) > 1e-5f)
                {
                    var expected = pet.X > before ? PetBehaviour.FacingRight : PetBehaviour.FacingLeft;
                    Assert.That(pet.Facing, Is.EqualTo(expected));
                }
            }
        }

        [TestCase(PetClip.Walk, 0.25f, 8, 2)]
        [TestCase(PetClip.Walk, 0.85f, 8, 0)]
        [TestCase(PetClip.Idle, 0.5f, 8, 3)]
        public void FrameIndex_LoopsAtTheClipsFrameRate(PetClip clip, float time, int frames, int expected)
        {
            Assert.That(PetAnimation.FrameIndex(clip, time, frames, tuning), Is.EqualTo(expected));
        }

        [TestCase(0f, 0)]
        [TestCase(1.99f, 0)]
        [TestCase(2f, 1)]
        [TestCase(23.9f, 11)]
        [TestCase(24f, 0)]
        [TestCase(50f, 1)]
        public void WalkFrameIndex_AdvancesOneFramePerStepOfDistance(float distance, int expected)
        {
            // Stride 24 sprite px over 12 frames: one frame per 2 px walked.
            Assert.That(PetAnimation.WalkFrameIndex(distance, 24f, 12), Is.EqualTo(expected));
        }

        [Test]
        public void FrameIndex_YawnHoldsItsLastFrame()
        {
            Assert.That(PetAnimation.FrameIndex(PetClip.Yawn, 60f, 8, tuning), Is.EqualTo(7));
        }
    }
}
