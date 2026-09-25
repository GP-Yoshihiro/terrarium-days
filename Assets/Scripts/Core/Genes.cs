using System;

namespace TerrariumDays.Core
{
    /// <summary>The single-locus genes (§4.1). Each albino strain is its own locus.</summary>
    public enum GeneId
    {
        TremperAlbino,
        BellAlbino,
        RainwaterAlbino,
        Eclipse,
        Blizzard,
        MurphyPatternless,
        MackSnow,
        WhiteAndYellow
    }

    public enum Inheritance
    {
        Recessive,
        Codominant,
        Dominant
    }

    public static class Genes
    {
        public static readonly GeneId[] All = (GeneId[])Enum.GetValues(typeof(GeneId));

        public static Inheritance InheritanceOf(GeneId gene)
        {
            switch (gene)
            {
                case GeneId.MackSnow:
                    return Inheritance.Codominant;
                case GeneId.WhiteAndYellow:
                    return Inheritance.Dominant;
                default:
                    return Inheritance.Recessive;
            }
        }

        public static bool IsRecessive(GeneId gene) => InheritanceOf(gene) == Inheritance.Recessive;

        /// <summary>Whether an animal carrying this many copies shows the gene.</summary>
        public static bool IsVisible(GeneId gene, int copies) => IsRecessive(gene) ? copies >= 2 : copies >= 1;

        public static string Label(GeneId gene)
        {
            switch (gene)
            {
                case GeneId.TremperAlbino:
                    return "トレンパーアルビノ";
                case GeneId.BellAlbino:
                    return "ベルアルビノ";
                case GeneId.RainwaterAlbino:
                    return "レインウォーターアルビノ";
                case GeneId.Eclipse:
                    return "エクリプス";
                case GeneId.Blizzard:
                    return "ブリザード";
                case GeneId.MurphyPatternless:
                    return "マーフィーパターンレス";
                case GeneId.MackSnow:
                    return "マックスノー";
                default:
                    return "ホワイト&イエロー";
            }
        }
    }
}
