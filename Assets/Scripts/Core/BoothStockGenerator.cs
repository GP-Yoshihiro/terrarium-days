using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// What another booth shows when the player browses (phase 6 spec §2.5): 1–3 animals, at most
    /// one of them rare (shown first), priced at market × event demand × 1.0–1.15. The ordinary
    /// animals are made like the shop's, but with more juveniles and adults (★P). No eggs.
    /// </summary>
    public static class BoothStockGenerator
    {
        public const int MinOffers = 1;
        public const int MaxOffers = 3;
        public const double BabyShare = 0.20d;
        public const double JuvenileShare = 0.45d;
        public const double HighPolygenic = 80d;
        public const double MinMarkup = 1.0d;
        public const double MaxMarkup = 1.15d;

        private static readonly GeneId[] Recessives =
        {
            GeneId.TremperAlbino, GeneId.BellAlbino, GeneId.RainwaterAlbino, GeneId.Eclipse, GeneId.Blizzard, GeneId.MurphyPatternless
        };

        /// <summary>
        /// Looks the shop never has (weights sum to 100). Each passes <see cref="IsRare(Genotype, KnownGenetics)"/>.
        /// A null result is the "three proven hets" look, built in <see cref="RareAnimal"/>.
        /// </summary>
        private static readonly List<(int Weight, Func<Random, Genotype> Build)> RareLooks = new List<(int, Func<Random, Genotype>)>
        {
            (12, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2).Set(GeneId.MackSnow, 1)),
            (10, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2).Set(GeneId.MackSnow, 1)),
            (8, r => Plain(r).Set(GeneId.WhiteAndYellow, 1).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)),
            (12, r => Plain(r).Set(GeneId.BellAlbino, 2).Set(GeneId.Eclipse, 2)),
            (12, r => Plain(r).Set(GeneId.RainwaterAlbino, 2).Set(GeneId.Eclipse, 2)),
            (10, r => Plain(r).Set(GeneId.BellAlbino, 2).Set(GeneId.MackSnow, 1)),
            (8, r => Plain(r).Set(GeneId.RainwaterAlbino, 2).Set(GeneId.Blizzard, 2)),
            (8, r => Plain(r).Set(GeneId.BellAlbino, 2).Set(GeneId.MurphyPatternless, 2)),
            (10, r => HighColour(r)),
            (10, r => null),
        };

        public static bool IsRare(PetState pet) => IsRare(pet.Genotype, pet.Known);

        /// <summary>
        /// Rare (★6): three or more visible genes; Bell or Rainwater with another visible gene; three
        /// or more proven hidden hets; or a visible gene with hypo or tangerine at 80 or more.
        /// </summary>
        public static bool IsRare(Genotype genotype, KnownGenetics known)
        {
            var visible = 0;
            var bellOrRainwater = false;
            var provenHets = 0;
            foreach (var gene in Genes.All)
            {
                if (genotype.Shows(gene))
                {
                    visible++;
                    bellOrRainwater |= gene == GeneId.BellAlbino || gene == GeneId.RainwaterAlbino;
                }
                else if (known != null && Genes.IsRecessive(gene) && KnownGenetics.IsProvenHet(known.HetProbability(gene)))
                {
                    provenHets++;
                }
            }

            return visible >= 3
                || (bellOrRainwater && visible >= 2)
                || provenHets >= 3
                || (visible >= 1 && (genotype.Hypo >= HighPolygenic || genotype.Tangerine >= HighPolygenic));
        }

        public static List<EventBoothOffer> Generate(Random random, EventKind kind, int demandSeed, DateTimeOffset nowUtc,
            CareTuning care, int firstOfferId, int actionIndex)
        {
            var count = random.Next(MinOffers, MaxOffers + 1);
            var hasRare = random.NextDouble() < EventSchedule.RareChance(kind);
            var offers = new List<EventBoothOffer>();
            for (var i = 0; i < count; i++)
            {
                var pet = i == 0 && hasRare ? RareAnimal(random, nowUtc, care) : CommonAnimal(random, nowUtc, care);
                var market = MarketPrice.For(pet, EventDemand.For(demandSeed, pet.Genotype, kind == EventKind.Big));
                offers.Add(new EventBoothOffer
                {
                    OfferId = firstOfferId + i,
                    ActionIndex = actionIndex,
                    Animal = pet,
                    MorphLabel = EventRules.MorphLabel(pet),
                    Price = MarketPrice.RoundYen(market * BreedingRandom.Uniform(random, MinMarkup, MaxMarkup)),
                    Rare = IsRare(pet),
                });
            }

            return offers;
        }

        /// <summary>A shop-style animal with the booth's stage mix: baby 20%, juvenile 45%, adult 35% (★7).</summary>
        public static PetState CommonAnimal(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var pet = ShopStockGenerator.Animal(random, nowUtc, care);
            var roll = random.NextDouble();
            GrowthStage stage;
            double weight;
            double ageMonths;
            if (roll < BabyShare)
            {
                stage = GrowthStage.Baby;
                weight = 4d + random.NextDouble() * 8d;
                ageMonths = 1d + random.NextDouble() * 2d;
            }
            else if (roll < BabyShare + JuvenileShare)
            {
                stage = GrowthStage.Juvenile;
                weight = 16d + random.NextDouble() * 14d;
                ageMonths = 4d + random.NextDouble() * 3d;
            }
            else
            {
                stage = GrowthStage.Adult;
                weight = 45d + random.NextDouble() * 25d;
                ageMonths = 10d + random.NextDouble() * 4d;
            }

            pet.Stage = stage;
            pet.WeightGrams = weight;
            pet.SexRevealed = stage != GrowthStage.Baby;
            // One real day is one game month, so an age in months is that many real days back.
            pet.HatchedAtUtc = nowUtc.AddDays(-ageMonths);
            pet.NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(stage, care);
            return pet;
        }

        public static PetState RareAnimal(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var pet = CommonAnimal(random, nowUtc, care);
            var genotype = PickRareLook(random);
            var known = new KnownGenetics();
            if (genotype == null)
            {
                genotype = Plain(random);
                ProveThreeHets(genotype, known, random);
            }

            pet.Genotype = genotype;
            pet.Known = known;
            return pet;
        }

        private static Genotype PickRareLook(Random random)
        {
            var total = 0;
            foreach (var look in RareLooks)
            {
                total += look.Weight;
            }

            var roll = random.Next(total);
            foreach (var look in RareLooks)
            {
                if (roll < look.Weight)
                {
                    return look.Build(random);
                }

                roll -= look.Weight;
            }

            return RareLooks[0].Build(random);
        }

        /// <summary>Three different hidden recessive genes, each carried once and disclosed as proven.</summary>
        private static void ProveThreeHets(Genotype genotype, KnownGenetics known, Random random)
        {
            var genes = new List<GeneId>(Recessives);
            for (var i = 0; i < 3; i++)
            {
                var pick = random.Next(i, genes.Count);
                (genes[i], genes[pick]) = (genes[pick], genes[i]);
                genotype.Set(genes[i], 1);
                known.SetHet(genes[i], 1d);
            }
        }

        /// <summary>One visible gene with very high hypo and tangerine (both 80–100).</summary>
        private static Genotype HighColour(Random random)
        {
            var genotype = Genotype.Normal(HighPolygenic + random.NextDouble() * 20d, HighPolygenic + random.NextDouble() * 20d);
            switch (random.Next(3))
            {
                case 0:
                    return genotype.Set(GeneId.TremperAlbino, 2);
                case 1:
                    return genotype.Set(GeneId.MackSnow, 1);
                default:
                    return genotype.Set(GeneId.Eclipse, 2);
            }
        }

        private static Genotype Plain(Random random) => Genotype.Normal(Low(random), Low(random));

        private static double Low(Random random) =>
            StarterGenetics.MinPolygenic + random.NextDouble() * (StarterGenetics.MaxPolygenic - StarterGenetics.MinPolygenic);
    }
}
