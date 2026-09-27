using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>A trade name that replaces a set of visible genes (§4.2).</summary>
    public sealed class CommonName
    {
        public CommonName(string name, params GeneId[] genes)
        {
            Name = name;
            Genes = genes;
        }

        public string Name { get; }

        public GeneId[] Genes { get; }
    }

    /// <summary>Morph names as breeders write them (§4.2).</summary>
    public static class MorphNamer
    {
        public const double HypoNameThreshold = 70d;
        public const double TangerineNameThreshold = 60d;

        /// <summary>Order visible genes are written in.</summary>
        private static readonly GeneId[] NameOrder =
        {
            GeneId.WhiteAndYellow, GeneId.MackSnow, GeneId.TremperAlbino, GeneId.BellAlbino,
            GeneId.RainwaterAlbino, GeneId.Eclipse, GeneId.Blizzard, GeneId.MurphyPatternless
        };

        /// <summary>Checked in order; extend by adding entries.</summary>
        public static readonly List<CommonName> CommonNames = new List<CommonName>
        {
            new CommonName("ブレイジングブリザード", GeneId.TremperAlbino, GeneId.Blizzard),
            new CommonName("レイプター", GeneId.TremperAlbino, GeneId.Eclipse),
        };

        public static string VisualName(Genotype genotype)
        {
            var words = new List<string>();
            if (genotype.Hypo >= HypoNameThreshold)
            {
                words.Add("ハイポ");
            }

            if (genotype.Tangerine >= TangerineNameThreshold)
            {
                words.Add("タンジェリン");
            }

            // A gene belongs to at most one matched trade name; the name is written where
            // its first gene would have been.
            var usedBy = new Dictionary<GeneId, CommonName>();
            foreach (var common in CommonNames)
            {
                if (Array.TrueForAll(common.Genes, g => genotype.Shows(g) && !usedBy.ContainsKey(g)))
                {
                    foreach (var gene in common.Genes)
                    {
                        usedBy[gene] = common;
                    }
                }
            }

            var written = new HashSet<CommonName>();
            foreach (var gene in NameOrder)
            {
                if (!genotype.Shows(gene))
                {
                    continue;
                }

                if (usedBy.TryGetValue(gene, out var common))
                {
                    if (written.Add(common))
                    {
                        words.Add(common.Name);
                    }

                    continue;
                }

                words.Add(gene == GeneId.MackSnow && genotype.Copies(gene) == 2 ? "スーパースノー" : Genes.Label(gene));
            }

            return words.Count == 0 ? "ノーマル" : string.Join(" ", words);
        }

        /// <summary>The visual name followed by proven and possible hets as the player knows them.</summary>
        public static string FullName(Genotype genotype, KnownGenetics known)
        {
            var name = VisualName(genotype);
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || genotype.Shows(gene))
                {
                    continue;
                }

                var p = known.HetProbability(gene);
                if (KnownGenetics.IsProvenHet(p))
                {
                    name += " ヘテロ" + Genes.Label(gene);
                }
                else if (p > 0d)
                {
                    name += $" {(int)Math.Floor(p * 100d + 1e-9)}%ポッシブルヘテロ{Genes.Label(gene)}";
                }
            }

            return known.HetsUnknown ? name + "（ヘテロ不明）" : name;
        }
    }
}
