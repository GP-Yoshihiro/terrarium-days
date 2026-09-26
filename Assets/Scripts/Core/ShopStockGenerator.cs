using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Makes a month's shop animals (§9): 4–6, mostly babies, from normal to popular morphs,
    /// with proven and possible hets disclosed truthfully. Sex is unknown for babies and the
    /// personality is unknown until kept a while after purchase.
    /// </summary>
    public static class ShopStockGenerator
    {
        public const int MinOffers = 4;
        public const int MaxOffers = 6;
        public const double JuvenileChance = 0.25d;
        public const double ProvenHetChance = 0.15d;
        public const double PossibleHetChance = 0.1d;
        public const int MaxDisclosedHets = 2;

        /// <summary>Weighted looks (weights sum to 100).</summary>
        private static readonly List<(int Weight, Func<Random, Genotype> Build)> Templates = new List<(int, Func<Random, Genotype>)>
        {
            (20, r => Plain(r)),
            (8, r => Plain(r).Set(GeneId.MackSnow, 1)),
            (8, r => Plain(r).Set(GeneId.TremperAlbino, 2)),
            (6, r => Genotype.Normal(Low(r), High(r))),
            (5, r => Plain(r).Set(GeneId.BellAlbino, 2)),
            (5, r => Plain(r).Set(GeneId.RainwaterAlbino, 2)),
            (6, r => Plain(r).Set(GeneId.Eclipse, 2)),
            (6, r => Plain(r).Set(GeneId.Blizzard, 2)),
            (5, r => Plain(r).Set(GeneId.MurphyPatternless, 2)),
            (6, r => Plain(r).Set(GeneId.WhiteAndYellow, 1)),
            (5, r => Plain(r).Set(GeneId.MackSnow, 2)),
            (5, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2)),
            (5, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)),
            (5, r => Plain(r).Set(GeneId.MackSnow, 1).Set(GeneId.TremperAlbino, 2)),
            (5, r => Genotype.Normal(75d + r.NextDouble() * 20d, High(r))),
        };

        public static int SeedFor(int shopSeed, int monthIndex) => unchecked(shopSeed * 31 + monthIndex * 7919 + 17);

        public static List<ShopOffer> Generate(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var count = random.Next(MinOffers, MaxOffers + 1);
            var offers = new List<ShopOffer>();
            for (var i = 0; i < count; i++)
            {
                offers.Add(new ShopOffer { OfferId = i + 1, Animal = Animal(random, nowUtc, care) });
            }

            return offers;
        }

        public static PetState Animal(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var genotype = PickGenotype(random);
            var known = DiscloseHets(genotype, random);
            var juvenile = random.NextDouble() < JuvenileChance;
            var stage = juvenile ? GrowthStage.Juvenile : GrowthStage.Baby;
            var weight = juvenile ? 16d + random.NextDouble() * 14d : 4d + random.NextDouble() * 8d;
            // One real day is one game month, so an age in months is that many real days back.
            var ageMonths = juvenile ? 4d + random.NextDouble() * 3d : 1d + random.NextDouble() * 2d;
            var sex = random.NextDouble() < 0.5d ? Sex.Female : Sex.Male;
            var personality = PersonalityTraits.Roll(random);
            return new PetState
            {
                Name = string.Empty,
                Sex = sex,
                SexRevealed = juvenile,
                Personality = personality,
                PersonalityKnown = false,
                Genotype = genotype,
                Known = known,
                WeightGrams = weight,
                Stage = stage,
                HatchedAtUtc = nowUtc.AddDays(-ageMonths),
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(stage, care),
            };
        }

        private static Genotype PickGenotype(Random random)
        {
            var total = 0;
            foreach (var template in Templates)
            {
                total += template.Weight;
            }

            var roll = random.Next(total);
            foreach (var template in Templates)
            {
                if (roll < template.Weight)
                {
                    return template.Build(random);
                }

                roll -= template.Weight;
            }

            return Templates[0].Build(random);
        }

        /// <summary>Per hidden recessive gene: 15% proven het, 10% possible het (50% or 66%, carried with that chance). At most two shown.</summary>
        private static KnownGenetics DiscloseHets(Genotype genotype, Random random)
        {
            var known = new KnownGenetics();
            var disclosed = 0;
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || genotype.Shows(gene))
                {
                    continue;
                }

                var roll = random.NextDouble();
                if (disclosed >= MaxDisclosedHets)
                {
                    continue;
                }

                if (roll < ProvenHetChance)
                {
                    genotype.Set(gene, 1);
                    known.SetHet(gene, 1d);
                    disclosed++;
                }
                else if (roll < ProvenHetChance + PossibleHetChance)
                {
                    var p = random.NextDouble() < 0.5d ? 0.5d : 0.66d;
                    if (random.NextDouble() < p)
                    {
                        genotype.Set(gene, 1);
                    }

                    known.SetHet(gene, p);
                    disclosed++;
                }
            }

            return known;
        }

        private static Genotype Plain(Random r) => Genotype.Normal(Low(r), Low(r));

        private static double Low(Random r) =>
            StarterGenetics.MinPolygenic + r.NextDouble() * (StarterGenetics.MaxPolygenic - StarterGenetics.MinPolygenic);

        private static double High(Random r) => 65d + r.NextDouble() * 30d;
    }
}
