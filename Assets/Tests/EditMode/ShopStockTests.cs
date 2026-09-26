using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ShopStockTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        private IEnumerable<PetState> ManyAnimals(int months)
        {
            for (var month = 0; month < months; month++)
            {
                foreach (var offer in ShopStockGenerator.Generate(new Random(ShopStockGenerator.SeedFor(7, month)), Now, care))
                {
                    yield return offer.Animal;
                }
            }
        }

        [Test]
        public void Generate_OffersFourToSixNumberedAnimals()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var offers = ShopStockGenerator.Generate(new Random(seed), Now, care);

                Assert.That(offers.Count, Is.InRange(ShopStockGenerator.MinOffers, ShopStockGenerator.MaxOffers));
                Assert.That(offers.Select(o => o.OfferId), Is.EqualTo(Enumerable.Range(1, offers.Count)));
            }
        }

        [Test]
        public void Generate_IsTheSameForTheSameSeed()
        {
            string Describe(List<ShopOffer> offers) => string.Join("|", offers.Select(o =>
                $"{MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known)}:{o.Animal.Stage}:{o.Animal.WeightGrams}:{o.Animal.Sex}:{o.Animal.Personality}"));

            Assert.That(Describe(ShopStockGenerator.Generate(new Random(3), Now, care)),
                Is.EqualTo(Describe(ShopStockGenerator.Generate(new Random(3), Now, care))));
        }

        [Test]
        public void Animals_AreMostlyBabiesWithUnknownSexAndAllHaveAnUnknownPersonality()
        {
            var animals = ManyAnimals(100).ToList();
            var babies = animals.Count(a => a.Stage == GrowthStage.Baby);

            Assert.That(babies / (double)animals.Count, Is.InRange(0.6d, 0.9d));
            foreach (var a in animals)
            {
                Assert.That(a.Stage, Is.EqualTo(GrowthStage.Baby).Or.EqualTo(GrowthStage.Juvenile));
                Assert.That(a.PersonalityKnown, Is.False);
                Assert.That(a.SexRevealed, Is.EqualTo(a.Stage == GrowthStage.Juvenile));
                Assert.That(a.Known.HetsUnknown, Is.False);
                if (a.Stage == GrowthStage.Baby)
                {
                    Assert.That(a.WeightGrams, Is.GreaterThanOrEqualTo(4d).And.LessThan(15d));
                }
                else
                {
                    Assert.That(a.WeightGrams, Is.GreaterThanOrEqualTo(16d).And.LessThan(40d));
                }
            }
        }

        [Test]
        public void Animals_RangeFromNormalToPopularMorphs()
        {
            var names = ManyAnimals(100).Select(a => MorphNamer.VisualName(a.Genotype)).Distinct().ToList();

            Assert.That(names, Has.Member("ノーマル"));
            Assert.That(names, Has.Member("レイプター"));
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void DisclosedHets_AreTruthfulAndAtMostTwo()
        {
            var sawProven = false;
            var sawPossible = false;
            foreach (var a in ManyAnimals(100))
            {
                var disclosed = 0;
                foreach (var gene in Genes.All)
                {
                    if (!Genes.IsRecessive(gene) || a.Genotype.Shows(gene))
                    {
                        continue;
                    }

                    var p = a.Known.HetProbability(gene);
                    if (p >= 1d)
                    {
                        Assert.That(a.Genotype.Copies(gene), Is.EqualTo(1));
                        sawProven = true;
                    }
                    else if (p > 0d)
                    {
                        Assert.That(p, Is.EqualTo(0.5d).Or.EqualTo(0.66d));
                        sawPossible = true;
                    }
                    else
                    {
                        Assert.That(a.Genotype.Copies(gene), Is.EqualTo(0));
                    }

                    if (p > 0d)
                    {
                        disclosed++;
                    }
                }

                Assert.That(disclosed, Is.LessThanOrEqualTo(ShopStockGenerator.MaxDisclosedHets));
            }

            Assert.That(sawProven && sawPossible, Is.True);
        }

        [Test]
        public void EnsureStocked_RestocksOnlyWhenTheMonthChanges()
        {
            var stock = new ShopStock { Seed = 11 };
            Assert.That(stock.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));

            Assert.That(stock.EnsureStocked(0, Now, care), Is.True);
            var first = stock.Offers;
            Assert.That(stock.StockMonthIndex, Is.EqualTo(0));

            Assert.That(stock.EnsureStocked(0, Now, care), Is.False);
            Assert.That(stock.Offers, Is.SameAs(first));

            Assert.That(stock.EnsureStocked(1, Now, care), Is.True);
            Assert.That(stock.Offers, Is.Not.SameAs(first));
        }

        [Test]
        public void EnsureStocked_IsTheSameForTheSameSeedAndMonth()
        {
            var a = new ShopStock { Seed = 11 };
            var b = new ShopStock { Seed = 11 };
            a.EnsureStocked(4, Now, care);
            b.EnsureStocked(4, Now, care);

            Assert.That(a.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known)),
                Is.EqualTo(b.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known))));
            Assert.That(ShopStockGenerator.SeedFor(11, 4), Is.Not.EqualTo(ShopStockGenerator.SeedFor(11, 5)));
        }

        [Test]
        public void ANewColony_HasAShopThatHasNotBeenStocked()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), care, new Random(1));

            Assert.That(colony.Shop.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));
            Assert.That(colony.Shop.Offers, Is.Empty);
        }
    }
}
