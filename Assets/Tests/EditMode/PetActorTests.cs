using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using TerrariumDays.UI;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    public sealed class PetActorTests
    {
        private const float ViewWidth = 393f;
        private const float ViewHeight = 360f;

        private readonly PetBehaviourTuning tuning = new PetBehaviourTuning();
        private readonly TerrariumArtLayout art = new TerrariumArtLayout();

        private PetActor CreateActor(VisualElement element, VisualElement effectsLayer = null, int seed = 3)
        {
            var actor = new PetActor(element, tuning, art, new System.Random(seed), PetSpriteLibrary.LoadFromResources(), effectsLayer);
            actor.SetViewSize(ViewWidth, ViewHeight);
            return actor;
        }

        private float FeetY(VisualElement element)
        {
            return element.style.top.value.value + art.Pet.BodyBottom / art.Pet.ImageHeight * element.style.height.value.value;
        }

        [Test]
        public void GeneratedSprites_AreAllPresentInResources()
        {
            var library = PetSpriteLibrary.LoadFromResources();

            foreach (PetClip clip in Enum.GetValues(typeof(PetClip)))
            {
                Assert.That(library.FrameCount(clip), Is.GreaterThanOrEqualTo(4), clip.ToString());
                Assert.That(library.Frame(clip, 0).width, Is.EqualTo((int)art.Pet.ImageWidth), clip.ToString());
            }

            Assert.That(library.Heart, Is.Not.Null);
            Assert.That(library.Zzz, Is.Not.Null);
        }

        [Test]
        public void BeforeAShed_ThePaleFrameSetIsShown()
        {
            var element = new VisualElement();
            var actor = CreateActor(element);
            var library = PetSpriteLibrary.LoadFromResources();

            actor.SetAppetite(AppetiteState.PreShed);
            var pale = element.style.backgroundImage.value.texture;
            actor.SetAppetite(AppetiteState.Normal);
            var normal = element.style.backgroundImage.value.texture;

            Assert.That(pale, Is.SameAs(library.Frame(PetClip.Idle, 0, preShed: true)));
            Assert.That(normal, Is.SameAs(library.Frame(PetClip.Idle, 0)));
            Assert.That(pale, Is.Not.SameAs(normal));
        }

        [Test]
        public void BeforeTheViewSizeIsKnown_ThePetIsHidden()
        {
            var element = new VisualElement();

            new PetActor(element, tuning, art, new System.Random(3));

            Assert.That(element.style.visibility.value, Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void WhateverItIsDoing_TheFeetStayOnTheFloorLine()
        {
            var element = new VisualElement();
            var actor = CreateActor(element);
            actor.SetCondition(PetMood.Lively, GrowthStage.Baby);
            var projection = new TerrariumProjection(ViewWidth, ViewHeight, art);
            var startX = actor.X;

            for (var i = 0; i < 1800; i++)
            {
                actor.Tick(1f / 30f);

                projection.FloorLine(actor.Depth, out var floorY, out _, out _);
                Assert.That(FeetY(element), Is.EqualTo(floorY).Within(0.01f), $"frame {i} ({actor.Behaviour.Clip})");
            }

            Assert.That(actor.X, Is.Not.EqualTo(startX), "the pet should have walked somewhere");
        }

        [Test]
        public void WalkFrames_AdvanceWithDistanceWalkedNotWithTime()
        {
            tuning.EveningSleepChance = 0d;
            tuning.YawnChance = 0d;
            tuning.TailWagChance = 0d;
            var element = new VisualElement();
            var actor = CreateActor(element);
            actor.SetCondition(PetMood.Lively, GrowthStage.Baby);
            var library = PetSpriteLibrary.LoadFromResources();
            var frames = library.FrameCount(PetClip.Walk);
            var checkedFrames = 0;

            for (var i = 0; i < 1200; i++)
            {
                actor.Tick(1f / 30f);
                if (actor.Behaviour.Clip != PetClip.Walk)
                {
                    continue;
                }

                var expected = library.Frame(PetClip.Walk, PetAnimation.WalkFrameIndex(actor.WalkDistance, art.PetWalkStride, frames));
                Assert.That(element.style.backgroundImage.value.texture, Is.SameAs(expected));
                checkedFrames++;
            }

            Assert.That(checkedFrames, Is.GreaterThan(30), "the pet should have walked during the test");
        }

        [Test]
        public void Tick_ShowsTheCurrentClipsFrame()
        {
            var element = new VisualElement();
            var actor = CreateActor(element);
            var library = PetSpriteLibrary.LoadFromResources();

            actor.Feed();
            actor.Tick(0.3f);

            // Feeding opens with the pre-strike tail twitch.
            var expected = library.Frame(PetClip.TailWag, PetAnimation.FrameIndex(PetClip.TailWag, 0.3f, library.FrameCount(PetClip.TailWag), tuning));
            Assert.That(element.style.backgroundImage.value.texture, Is.SameAs(expected));
        }

        [Test]
        public void SetCondition_GrowsTheSpriteWithTheGrowthStage()
        {
            var element = new VisualElement();
            var actor = CreateActor(element);

            actor.SetCondition(PetMood.Normal, GrowthStage.Baby);
            var babyWidth = element.style.width.value.value;
            actor.SetCondition(PetMood.Normal, GrowthStage.Adult);

            Assert.That(element.style.width.value.value, Is.EqualTo(babyWidth * tuning.AdultBodyWidth / tuning.BabyBodyWidth).Within(0.01f));
        }

        [Test]
        public void SetCondition_Sluggish_DimsTheSprite()
        {
            var element = new VisualElement();
            var actor = CreateActor(element);

            actor.SetCondition(PetMood.Sluggish, GrowthStage.Baby);

            Assert.That(element.style.unityBackgroundImageTintColor.value.r, Is.EqualTo(tuning.SluggishTint).Within(1e-4f));
        }

        [Test]
        public void Threat_ClearsHeartsLeftFromEarlierTaps()
        {
            var layer = new VisualElement();
            var element = new VisualElement();
            layer.Add(element);
            var actor = CreateActor(element, layer);

            PetTapReaction reaction = PetTapReaction.Noticed;
            for (var i = 0; i < tuning.ThreatTapCount; i++)
            {
                reaction = actor.Tap();
                actor.Tick(0.1f);
            }

            Assert.That(reaction, Is.EqualTo(PetTapReaction.Threat));
            Assert.That(actor.Effects.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void WhileAsleep_ZzzFloatUpFromTheHead()
        {
            tuning.EveningSleepChance = 1d;
            var layer = new VisualElement();
            var element = new VisualElement();
            layer.Add(element);
            var actor = CreateActor(element, layer);

            for (var t = 0f; t < 1.5f; t += 1f / 30f)
            {
                actor.Tick(1f / 30f);
            }

            Assert.That(actor.Behaviour.Clip, Is.EqualTo(PetClip.Sleep));
            Assert.That(actor.Effects.ActiveCount, Is.GreaterThan(0));
        }

        [Test]
        public void Cheer_FloatsHeartsThatFadeAway()
        {
            var layer = new VisualElement();
            var element = new VisualElement();
            layer.Add(element);
            var actor = CreateActor(element, layer);

            actor.Cheer();
            Assert.That(actor.Effects.ActiveCount, Is.EqualTo(tuning.HeartsPerCheer));

            for (var t = 0f; t < tuning.HeartLifeSeconds + tuning.HeartStaggerSeconds * tuning.HeartsPerCheer + 0.2f; t += 1f / 30f)
            {
                actor.Tick(1f / 30f);
            }

            Assert.That(actor.Effects.ActiveCount, Is.EqualTo(0));
            Assert.That(layer.childCount, Is.EqualTo(1), "only the pet is left in the layer");
        }
    }
}
