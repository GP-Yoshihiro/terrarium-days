using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Genes for animals whose history is unknown (the starting animal, migrated saves):
    /// normal-looking, random hypo/tangerine, and a small chance of a hidden het per
    /// recessive gene. The player sees 「ノーマル（ヘテロ不明）」.
    /// </summary>
    public static class StarterGenetics
    {
        public const double HiddenHetChance = 0.1d;
        public const double MinPolygenic = 10d;
        public const double MaxPolygenic = 40d;

        public static void Apply(PetState pet, Random random)
        {
            var genotype = Genotype.Normal(
                MinPolygenic + random.NextDouble() * (MaxPolygenic - MinPolygenic),
                MinPolygenic + random.NextDouble() * (MaxPolygenic - MinPolygenic));
            foreach (var gene in Genes.All)
            {
                var roll = random.NextDouble();
                if (Genes.IsRecessive(gene) && roll < HiddenHetChance)
                {
                    genotype.Set(gene, 1);
                }
            }

            pet.Genotype = genotype;
            pet.Known = KnownGenetics.Unknown();
            pet.Personality = PersonalityTraits.Roll(random);
            pet.PersonalityKnown = true;
        }

        /// <summary>One of each look, for the debug menu and screen captures.</summary>
        public static readonly List<(string Label, Genotype Genotype)> Showcase = new List<(string, Genotype)>
        {
            ("ノーマル", Genotype.Normal()),
            ("トレンパーアルビノ", Genotype.Normal().Set(GeneId.TremperAlbino, 2)),
            ("ベルアルビノ", Genotype.Normal().Set(GeneId.BellAlbino, 2)),
            ("レインウォーターアルビノ", Genotype.Normal().Set(GeneId.RainwaterAlbino, 2)),
            ("エクリプス", Genotype.Normal().Set(GeneId.Eclipse, 2)),
            ("ブリザード", Genotype.Normal().Set(GeneId.Blizzard, 2)),
            ("マーフィーパターンレス", Genotype.Normal().Set(GeneId.MurphyPatternless, 2)),
            ("マックスノー", Genotype.Normal().Set(GeneId.MackSnow, 1)),
            ("スーパースノー", Genotype.Normal().Set(GeneId.MackSnow, 2)),
            ("ホワイト&イエロー", Genotype.Normal().Set(GeneId.WhiteAndYellow, 1)),
            ("ブレイジングブリザード", Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2)),
            ("レイプター", Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)),
            ("ハイポ タンジェリン", Genotype.Normal(hypo: 85d, tangerine: 90d)),
        };
    }
}
