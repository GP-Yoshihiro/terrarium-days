using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// An animal's true genes: 0–2 copies per single-locus gene, plus the polygenic hypo and
    /// tangerine values (0–100). Hidden from the player; see <see cref="KnownGenetics"/>.
    /// </summary>
    public sealed class Genotype
    {
        private readonly int[] copies = new int[Genes.All.Length];
        private double hypo;
        private double tangerine;

        public double Hypo
        {
            get => hypo;
            set => hypo = Math.Max(0d, Math.Min(100d, value));
        }

        public double Tangerine
        {
            get => tangerine;
            set => tangerine = Math.Max(0d, Math.Min(100d, value));
        }

        public int Copies(GeneId gene) => copies[(int)gene];

        public Genotype Set(GeneId gene, int count)
        {
            copies[(int)gene] = Math.Max(0, Math.Min(2, count));
            return this;
        }

        public bool Shows(GeneId gene) => Genes.IsVisible(gene, Copies(gene));

        public Genotype Clone()
        {
            var clone = new Genotype { Hypo = Hypo, Tangerine = Tangerine };
            Array.Copy(copies, clone.copies, copies.Length);
            return clone;
        }

        public static Genotype Normal(double hypo = 25d, double tangerine = 25d) =>
            new Genotype { Hypo = hypo, Tangerine = tangerine };
    }
}
