using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class MarketPriceTests
    {
        private readonly EconomyTuning economy = new EconomyTuning();

        /// <summary>Sex and personality unknown, so neither multiplier applies unless a test sets them.</summary>
        private static PetState Pet(Genotype genotype, GrowthStage stage, KnownGenetics known = null) => new PetState
        {
            Genotype = genotype,
            Known = known ?? new KnownGenetics(),
            Stage = stage,
            SexRevealed = false,
            PersonalityKnown = false,
        };

        [Test]
        public void ANormalBaby_IsFiveThousandTimesPointEight()
        {
            Assert.That(MarketPrice.For(Pet(Genotype.Normal(), GrowthStage.Baby)), Is.EqualTo(4000));
        }

        [Test]
        public void BasePrice_UsesTheTableAndCombinations()
        {
            Assert.That(MarketPrice.BasePrice(Genotype.Normal()), Is.EqualTo(5000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.WhiteAndYellow, 1)), Is.EqualTo(20000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 2)), Is.EqualTo(25000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2)), Is.EqualTo(25000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)), Is.EqualTo(30000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.TremperAlbino, 2)), Is.EqualTo(13000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.Eclipse, 2).Set(GeneId.Blizzard, 2)), Is.EqualTo(19500));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 2).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)), Is.EqualTo(39000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.Eclipse, 2).Set(GeneId.MurphyPatternless, 2)), Is.EqualTo(25350));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal(tangerine: 90d)), Is.EqualTo(12000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal(hypo: 90d)), Is.EqualTo(5000), "hypo alone is priced by the polygenic multiplier");
        }

        [Test]
        public void AKnownFemaleWithAKnownCalmPersonality_GetsBothMultipliers()
        {
            var pet = Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2), GrowthStage.Juvenile);
            pet.Sex = Sex.Female;
            pet.SexRevealed = true;
            pet.Personality = Personality.Calm;
            pet.PersonalityKnown = true;

            Assert.That(MarketPrice.For(pet), Is.EqualTo(13200));
        }

        [Test]
        public void AnUnknownSexOrPersonality_AddsNothing()
        {
            var pet = Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2), GrowthStage.Juvenile);
            pet.Sex = Sex.Female;
            pet.Personality = Personality.Calm;

            Assert.That(MarketPrice.For(pet), Is.EqualTo(10000));
        }

        [Test]
        public void ProvenAndPossibleHets_AddTwentyPercentAndProbabilityTimesTwelvePercent()
        {
            var known = new KnownGenetics().SetHet(GeneId.Blizzard, 1d).SetHet(GeneId.MurphyPatternless, 0.66d);
            var raptor = Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2).Set(GeneId.Blizzard, 1);
            var pet = Pet(raptor, GrowthStage.Adult, known);
            pet.Sex = Sex.Male;
            pet.SexRevealed = true;

            Assert.That(MarketPrice.HetMultiplier(raptor, known), Is.EqualTo(1.2792d).Within(1e-9));
            Assert.That(MarketPrice.For(pet), Is.EqualTo(49900));
        }

        [Test]
        public void UnknownHets_AddNothing()
        {
            var carrier = Genotype.Normal().Set(GeneId.Eclipse, 1);

            Assert.That(MarketPrice.HetMultiplier(carrier, KnownGenetics.Unknown()), Is.EqualTo(1d));
        }

        [TestCase(GrowthStage.Baby, 0.8d)]
        [TestCase(GrowthStage.Juvenile, 1d)]
        [TestCase(GrowthStage.Adult, 1.3d)]
        public void StageMultiplier(GrowthStage stage, double multiplier)
        {
            Assert.That(MarketPrice.StageMultiplier(stage), Is.EqualTo(multiplier));
        }

        [Test]
        public void Polygenic_AddsUpToFiftyPercent()
        {
            Assert.That(MarketPrice.PolygenicMultiplier(Genotype.Normal(25d, 25d)), Is.EqualTo(1d));
            Assert.That(MarketPrice.PolygenicMultiplier(Genotype.Normal(100d, 100d)), Is.EqualTo(1.5d).Within(1e-9));
            Assert.That(MarketPrice.PolygenicMultiplier(Genotype.Normal(25d, 90d)), Is.EqualTo(1.2d).Within(1e-9));
            Assert.That(MarketPrice.For(Pet(Genotype.Normal(25d, 90d), GrowthStage.Juvenile)), Is.EqualTo(14400));
        }

        [Test]
        public void WeaknessAndDemand_Multiply()
        {
            var pet = Pet(Genotype.Normal(), GrowthStage.Juvenile);

            Assert.That(MarketPrice.For(pet, weak: true), Is.EqualTo(1500));
            Assert.That(MarketPrice.For(pet, demand: 1.3d), Is.EqualTo(6500));
            Assert.That(MarketPrice.For(pet, demand: EventDemand.None), Is.EqualTo(5000));
        }

        [Test]
        public void ShopAndWholesalePrices_AreMarketTimesTheirRates()
        {
            Assert.That(MarketPrice.ShopPrice(4000, economy), Is.EqualTo(4800));
            Assert.That(MarketPrice.WholesalePrice(4000, economy), Is.EqualTo(1600));
        }

        [Test]
        public void RoundYen_RoundsToHundredsWithAFloor()
        {
            Assert.That(MarketPrice.RoundYen(12349d), Is.EqualTo(12300));
            Assert.That(MarketPrice.RoundYen(12351d), Is.EqualTo(12400));
            Assert.That(MarketPrice.RoundYen(10d), Is.EqualTo(100));
        }

        [Test]
        public void Demand_IsTheSameForTheSameEventAndMorph()
        {
            var eclipse = Genotype.Normal().Set(GeneId.Eclipse, 2);

            Assert.That(EventDemand.For(12, eclipse, false), Is.EqualTo(EventDemand.For(12, eclipse, false)));
        }

        [Test]
        public void Demand_SpreadsBetweenPointEightAndOnePointThree()
        {
            var min = double.MaxValue;
            var max = double.MinValue;
            for (var seed = 0; seed < 200; seed++)
            {
                var demand = EventDemand.For(seed, Genotype.Normal(), false);
                Assert.That(demand, Is.GreaterThanOrEqualTo(0.8d).And.LessThan(1.3d));
                min = Math.Min(min, demand);
                max = Math.Max(max, demand);
            }

            Assert.That(min, Is.LessThan(0.85d));
            Assert.That(max, Is.GreaterThan(1.25d));
        }

        [Test]
        public void Demand_DiffersBetweenMorphs()
        {
            var tremper = Genotype.Normal().Set(GeneId.TremperAlbino, 2);
            var differ = 0;
            for (var seed = 0; seed < 20; seed++)
            {
                if (Math.Abs(EventDemand.For(seed, Genotype.Normal(), false) - EventDemand.For(seed, tremper, false)) > 1e-9)
                {
                    differ++;
                }
            }

            Assert.That(differ, Is.GreaterThanOrEqualTo(15));
        }

        [Test]
        public void ABigEvent_BoostsOnlyMorphsFromFifteenThousandYen()
        {
            var eclipse = Genotype.Normal().Set(GeneId.Eclipse, 2);
            var tremper = Genotype.Normal().Set(GeneId.TremperAlbino, 2);

            Assert.That(EventDemand.For(5, eclipse, true), Is.EqualTo(EventDemand.For(5, eclipse, false) * 1.15d).Within(1e-12));
            Assert.That(EventDemand.For(5, tremper, true), Is.EqualTo(EventDemand.For(5, tremper, false)));
        }
    }
}
