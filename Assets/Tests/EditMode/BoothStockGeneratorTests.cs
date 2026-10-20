using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class BoothStockGeneratorTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        [Test]
        public void ShopStock_IsNeverRare()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                foreach (var offer in ShopStockGenerator.Generate(new Random(seed), Now, care))
                {
                    Assert.That(BoothStockGenerator.IsRare(offer.Animal), Is.False, $"seed {seed}: {MorphNamer.FullName(offer.Animal.Genotype, offer.Animal.Known)}");
                }
            }
        }

        [Test]
        public void RareAnimals_AreAlwaysRare()
        {
            for (var seed = 0; seed < 300; seed++)
            {
                var pet = BoothStockGenerator.RareAnimal(new Random(seed), Now, care);
                Assert.That(BoothStockGenerator.IsRare(pet), Is.True, $"seed {seed}: {MorphNamer.FullName(pet.Genotype, pet.Known)}");
            }
        }

        [Test]
        public void IsRare_Conditions()
        {
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2).Set(GeneId.MackSnow, 1))), Is.True);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2))), Is.False);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.BellAlbino, 2).Set(GeneId.MackSnow, 1))), Is.True);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.BellAlbino, 2))), Is.False);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal(85d, 30d).Set(GeneId.MackSnow, 1))), Is.True);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal(85d, 95d))), Is.False);

            var hets = Pet(Genotype.Normal().Set(GeneId.Eclipse, 1).Set(GeneId.Blizzard, 1).Set(GeneId.BellAlbino, 1));
            hets.Known.SetHet(GeneId.Eclipse, 1d).SetHet(GeneId.Blizzard, 1d).SetHet(GeneId.BellAlbino, 1d);
            Assert.That(BoothStockGenerator.IsRare(hets), Is.True);
            hets.Known.SetHet(GeneId.BellAlbino, 0.66d);
            Assert.That(BoothStockGenerator.IsRare(hets), Is.False);
        }

        [Test]
        public void RareBoothRate_SmallIsAbout15Percent() => AssertRareBoothRate(EventKind.Small, 0.15d, 0.02d);

        [Test]
        public void RareBoothRate_BigIsAbout25Percent() => AssertRareBoothRate(EventKind.Big, 0.25d, 0.025d);

        private void AssertRareBoothRate(EventKind kind, double expected, double tolerance)
        {
            var rareBooths = 0;
            const int booths = 4000;
            for (var seed = 0; seed < booths; seed++)
            {
                var offers = BoothStockGenerator.Generate(new Random(seed), kind, 7, Now, care, 1, 0);
                var anyRare = false;
                for (var i = 0; i < offers.Count; i++)
                {
                    Assert.That(offers[i].Rare, Is.EqualTo(BoothStockGenerator.IsRare(offers[i].Animal)));
                    if (offers[i].Rare)
                    {
                        Assert.That(i, Is.EqualTo(0), "the rare animal comes first");
                        anyRare = true;
                    }
                }

                if (anyRare)
                {
                    rareBooths++;
                }
            }

            Assert.That(rareBooths / (double)booths, Is.EqualTo(expected).Within(tolerance));
        }

        [Test]
        public void Generate_ShowsOneToThreeAnimalsWithIdsAndPrices()
        {
            var counts = new int[4];
            for (var seed = 0; seed < 500; seed++)
            {
                var offers = BoothStockGenerator.Generate(new Random(seed), EventKind.Big, 7, Now, care, 10, 3);
                Assert.That(offers.Count, Is.InRange(1, 3));
                counts[offers.Count]++;
                for (var i = 0; i < offers.Count; i++)
                {
                    var offer = offers[i];
                    var pet = offer.Animal;
                    Assert.That(offer.OfferId, Is.EqualTo(10 + i));
                    Assert.That(offer.ActionIndex, Is.EqualTo(3));
                    Assert.That(offer.Bought, Is.False);
                    Assert.That(offer.MorphLabel, Is.EqualTo(EventRules.MorphLabel(pet)));
                    var market = MarketPrice.For(pet, EventDemand.For(7, pet.Genotype, true), false);
                    Assert.That(offer.Price, Is.InRange(market, MarketPrice.RoundYen(market * 1.15d)));
                    Assert.That(offer.Price % 100, Is.EqualTo(0));
                    Assert.That(pet.Weak, Is.False);
                    Assert.That(pet.PersonalityKnown, Is.False);
                    Assert.That(pet.Name, Is.Empty);
                    Assert.That(pet.SexRevealed, Is.EqualTo(pet.Stage != GrowthStage.Baby));
                }
            }

            Assert.That(counts[1], Is.GreaterThan(0));
            Assert.That(counts[2], Is.GreaterThan(0));
            Assert.That(counts[3], Is.GreaterThan(0));
        }

        [Test]
        public void Stages_AreMostlyJuvenileAndAdult()
        {
            var stages = new int[3];
            var total = 0;
            for (var seed = 0; seed < 3000; seed++)
            {
                foreach (var offer in BoothStockGenerator.Generate(new Random(seed), EventKind.Small, 7, Now, care, 1, 0))
                {
                    stages[(int)offer.Animal.Stage]++;
                    total++;
                    var weight = offer.Animal.WeightGrams;
                    switch (offer.Animal.Stage)
                    {
                        case GrowthStage.Baby:
                            Assert.That(weight, Is.InRange(4d, 12d));
                            break;
                        case GrowthStage.Juvenile:
                            Assert.That(weight, Is.InRange(16d, 30d));
                            break;
                        default:
                            Assert.That(weight, Is.InRange(45d, 70d));
                            break;
                    }
                }
            }

            Assert.That(stages[(int)GrowthStage.Baby] / (double)total, Is.EqualTo(0.20d).Within(0.03d));
            Assert.That(stages[(int)GrowthStage.Juvenile] / (double)total, Is.EqualTo(0.45d).Within(0.03d));
            Assert.That(stages[(int)GrowthStage.Adult] / (double)total, Is.EqualTo(0.35d).Within(0.03d));
        }

        [Test]
        public void Generate_IsFixedByTheRandom()
        {
            var a = BoothStockGenerator.Generate(new Random(9), EventKind.Small, 7, Now, care, 1, 0);
            var b = BoothStockGenerator.Generate(new Random(9), EventKind.Small, 7, Now, care, 1, 0);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (var i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].Price, Is.EqualTo(b[i].Price));
                Assert.That(MorphNamer.FullName(a[i].Animal.Genotype, a[i].Animal.Known), Is.EqualTo(MorphNamer.FullName(b[i].Animal.Genotype, b[i].Animal.Known)));
            }
        }

        private static PetState Pet(Genotype genotype) => new PetState { Genotype = genotype, Known = new KnownGenetics() };
    }
}
