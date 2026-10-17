using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using TerrariumDays.UI;
using UnityEngine;

namespace TerrariumDays.Tests
{
    public sealed class MorphSpriteCacheTests
    {
        private static List<PetState> DistinctLooks(int count)
        {
            var pets = new List<PetState>();
            var keys = new HashSet<string>();
            for (var step = 0; step <= 10 && pets.Count < count; step++)
            {
                foreach (var mack in new[] { 0, 1 })
                {
                    foreach (var white in new[] { 0, 1 })
                    {
                        var genotype = Genotype.Normal(25d, 30d + step * 7d)
                            .Set(GeneId.MackSnow, mack)
                            .Set(GeneId.WhiteAndYellow, white);
                        var pet = new PetState { Genotype = genotype, Stage = GrowthStage.Adult };
                        if (pets.Count < count && keys.Add(MorphSprites.KeyFor(pet)))
                        {
                            pets.Add(pet);
                        }
                    }
                }
            }

            Assert.That(pets.Count, Is.EqualTo(count), "fixture must yield enough distinct looks");
            return pets;
        }

        [SetUp]
        public void SetUp() => MorphSprites.ClearCache();

        [TearDown]
        public void TearDown() => MorphSprites.ClearCache();

        [Test]
        public void ManyLooks_KeepCacheAtCapacity()
        {
            foreach (var pet in DistinctLooks(40))
            {
                Assert.That(MorphSprites.Thumbnail(pet), Is.Not.Null);
            }

            Assert.That(MorphSprites.CachedCount, Is.LessThanOrEqualTo(SpriteCachePolicy.DefaultCapacity));
        }

        [Test]
        public void KeysInUse_SurviveEvictionAndStayUsable()
        {
            var pets = DistinctLooks(40);
            var inUse = new[] { pets[0], pets[1] };
            MorphSprites.SetKeysInUse(new[] { MorphSprites.KeyFor(inUse[0]), MorphSprites.KeyFor(inUse[1]) });
            var kept = new[] { MorphSprites.Thumbnail(inUse[0]), MorphSprites.Thumbnail(inUse[1]) };

            foreach (var pet in pets)
            {
                MorphSprites.Thumbnail(pet);
            }

            Assert.That(MorphSprites.CachedCount, Is.LessThanOrEqualTo(SpriteCachePolicy.DefaultCapacity + inUse.Length));
            Assert.That(kept[0] != null && kept[1] != null, Is.True);
            Assert.That(MorphSprites.For(inUse[0]).Frame(PetClip.Idle, 0, true) != null, Is.True);
        }

        [Test]
        public void EvictedLook_IsRebuiltWhenRequestedAgain()
        {
            var pets = DistinctLooks(40);
            var first = MorphSprites.Thumbnail(pets[0]);
            foreach (var pet in pets)
            {
                MorphSprites.Thumbnail(pet);
            }

            Assert.That(first == null, Is.True, "oldest look was evicted and its sprite destroyed");
            var again = MorphSprites.Thumbnail(pets[0]);
            Assert.That(again != null, Is.True);
        }

        [Test]
        public void AllLooksInUse_KeepEveryThumbnailAlive()
        {
            var pets = DistinctLooks(40);
            var keys = new List<string>();
            foreach (var pet in pets)
            {
                keys.Add(MorphSprites.KeyFor(pet));
            }

            MorphSprites.SetKeysInUse(keys);
            var sprites = new List<Sprite>();
            foreach (var pet in pets)
            {
                sprites.Add(MorphSprites.Thumbnail(pet));
            }

            foreach (var sprite in sprites)
            {
                Assert.That(sprite != null, Is.True);
            }
        }

        [Test]
        public void EvictionViaFor_KeepsSourceFrame()
        {
            var pets = DistinctLooks(40);
            var source = MorphSprites.Base.Frame(PetClip.Idle, 0);
            Assert.That(source != null, Is.True);
            foreach (var pet in pets)
            {
                MorphSprites.For(pet).Frame(PetClip.Idle, 0, true);
            }

            Assert.That(MorphSprites.CachedCount, Is.LessThanOrEqualTo(SpriteCachePolicy.DefaultCapacity));
            Assert.That(source != null, Is.True);
            Assert.That(MorphSprites.Base.Frame(PetClip.Idle, 0) != null, Is.True);
        }
    }
}
