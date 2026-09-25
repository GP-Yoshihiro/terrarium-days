using System;

namespace TerrariumDays.Core
{
    /// <summary>Mendelian odds and the actual offspring genotype (§4.1, §4.4).</summary>
    public static class GeneticsCalculator
    {
        /// <summary>Standard deviation of a polygenic value around the parents' mean.</summary>
        public const double PolygenicSpread = 10d;

        /// <summary>Probabilities that a child carries 0, 1 or 2 copies.</summary>
        public static double[] CopiesOutcome(int motherCopies, int fatherCopies)
        {
            var m = Math.Max(0, Math.Min(2, motherCopies)) / 2d;
            var f = Math.Max(0, Math.Min(2, fatherCopies)) / 2d;
            return new[] { (1d - m) * (1d - f), m * (1d - f) + (1d - m) * f, m * f };
        }

        /// <summary>The child's true genotype. Deterministic for a given Random seed.</summary>
        public static Genotype Breed(Genotype mother, Genotype father, Random random)
        {
            var child = new Genotype();
            foreach (var gene in Genes.All)
            {
                child.Set(gene, Allele(mother.Copies(gene), random) + Allele(father.Copies(gene), random));
            }

            child.Hypo = (mother.Hypo + father.Hypo) / 2d + Gaussian(random) * PolygenicSpread;
            child.Tangerine = (mother.Tangerine + father.Tangerine) / 2d + Gaussian(random) * PolygenicSpread;
            return child;
        }

        /// <summary>A standard normal sample (Box–Muller).</summary>
        public static double Gaussian(Random random)
        {
            var u1 = 1d - random.NextDouble();
            var u2 = random.NextDouble();
            return Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
        }

        private static int Allele(int copies, Random random)
        {
            // Always draw, so one parent's copy count never shifts the other's random sequence.
            var roll = random.NextDouble();
            return copies >= 2 ? 1 : copies <= 0 ? 0 : roll < 0.5d ? 1 : 0;
        }
    }
}
