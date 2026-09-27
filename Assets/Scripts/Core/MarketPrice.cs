using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// The market reference price (相場, §10.1): morph base price, het bonus, individual
    /// multipliers and the event demand factor, rounded to 100 yen. Uses only what the
    /// player can know (visible genes, known hets, revealed sex and personality).
    /// </summary>
    public static class MarketPrice
    {
        public const long NormalPrice = 5000;
        public const double CombinationStep = 1.3d;
        public const double ProvenHetBonus = 0.2d;
        public const double PossibleHetBonusPerProbability = 0.12d;
        public const double FemaleMultiplier = 1.2d;
        public const double WeakMultiplier = 0.3d;
        public const double PolygenicBonusPerTrait = 0.25d;
        public const double PolygenicBonusFrom = 50d;

        /// <summary>Base price per word of <see cref="MorphNamer.VisualName"/>. ハイポ is priced by <see cref="PolygenicMultiplier"/>.</summary>
        public static readonly IReadOnlyDictionary<string, long> MorphPrices = new Dictionary<string, long>
        {
            ["ノーマル"] = 5000,
            ["マックスノー"] = 10000,
            ["トレンパーアルビノ"] = 10000,
            ["タンジェリン"] = 12000,
            ["ベルアルビノ"] = 15000,
            ["レインウォーターアルビノ"] = 15000,
            ["エクリプス"] = 15000,
            ["ブリザード"] = 15000,
            ["マーフィーパターンレス"] = 15000,
            ["ホワイト&イエロー"] = 20000,
            ["スーパースノー"] = 25000,
            ["ブレイジングブリザード"] = 25000,
            ["レイプター"] = 30000,
        };

        /// <summary>Most expensive element × 1.3^(number of other elements); a trade name is one element.</summary>
        public static long BasePrice(Genotype genotype)
        {
            long highest = 0;
            var elements = 0;
            foreach (var word in MorphNamer.VisualName(genotype).Split(' '))
            {
                if (!MorphPrices.TryGetValue(word, out var price))
                {
                    continue;
                }

                elements++;
                highest = Math.Max(highest, price);
            }

            if (elements == 0)
            {
                return NormalPrice;
            }

            return (long)Math.Round(highest * Math.Pow(CombinationStep, elements - 1), MidpointRounding.AwayFromZero);
        }

        /// <summary>1 + 0.2 per proven het + 0.12 × probability per possible het (recessive genes that do not show).</summary>
        public static double HetMultiplier(Genotype genotype, KnownGenetics known)
        {
            var bonus = 0d;
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || genotype.Shows(gene))
                {
                    continue;
                }

                var p = known.HetProbability(gene);
                bonus += KnownGenetics.IsProvenHet(p) ? ProvenHetBonus : p * PossibleHetBonusPerProbability;
            }

            return 1d + bonus;
        }

        /// <summary>Up to ×1.5: each of hypo and tangerine adds up to 0.25 above 50.</summary>
        public static double PolygenicMultiplier(Genotype genotype) =>
            1d + PolygenicBonusPerTrait * (Above(genotype.Hypo) + Above(genotype.Tangerine));

        public static double StageMultiplier(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return 1d;
                case GrowthStage.Adult:
                    return 1.3d;
                default:
                    return 0.8d;
            }
        }

        /// <summary>
        /// The market price of this animal. <paramref name="demand"/> is the event demand
        /// (<see cref="EventDemand.None"/> outside events); <paramref name="weak"/> is §5.5 (phase 4).
        /// </summary>
        public static long For(PetState pet, double demand = 1d, bool weak = false)
        {
            var yen = BasePrice(pet.Genotype) * HetMultiplier(pet.Genotype, pet.Known) * PolygenicMultiplier(pet.Genotype)
                * StageMultiplier(pet.Stage) * demand;
            if (pet.SexKnown && pet.Sex == Sex.Female)
            {
                yen *= FemaleMultiplier;
            }

            if (pet.PersonalityKnown)
            {
                yen *= PersonalityTraits.PriceMultiplier(pet.Personality);
            }

            if (weak)
            {
                yen *= WeakMultiplier;
            }

            return RoundYen(yen);
        }

        /// <summary>Nearest 100 yen, at least 100.</summary>
        public static long RoundYen(double yen) =>
            Math.Max(100L, (long)Math.Round(yen / 100d, MidpointRounding.AwayFromZero) * 100L);

        public static long ShopPrice(long market, EconomyTuning economy) => RoundYen(market * economy.ShopMarkup);

        public static long WholesalePrice(long market, EconomyTuning economy) => RoundYen(market * economy.WholesaleRate);

        private static double Above(double value) =>
            Math.Max(0d, Math.Min(1d, (value - PolygenicBonusFrom) / (100d - PolygenicBonusFrom)));
    }
}
