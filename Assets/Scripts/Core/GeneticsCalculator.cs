using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Chance of each visual morph from what the player knows (§4.4). Single genes only;
        /// hypo/tangerine are judged on the parents' mean (see the plan's rulings).
        /// </summary>
        public static List<MorphOdds> PredictVisualOdds(Genotype mother, KnownGenetics motherKnown,
            Genotype father, KnownGenetics fatherKnown)
        {
            var perGene = new double[Genes.All.Length][];
            foreach (var gene in Genes.All)
            {
                perGene[(int)gene] = KnownGenetics.Blend(
                    KnownGenetics.BelievedCopies(mother, motherKnown, gene),
                    KnownGenetics.BelievedCopies(father, fatherKnown, gene));
            }

            var totals = new Dictionary<string, double>();
            var child = Genotype.Normal((mother.Hypo + father.Hypo) / 2d, (mother.Tangerine + father.Tangerine) / 2d);
            Enumerate(perGene, 0, 1d, child, totals);

            var result = new List<MorphOdds>();
            foreach (var pair in totals)
            {
                result.Add(new MorphOdds(pair.Key, pair.Value));
            }

            result.Sort((a, b) => b.Probability != a.Probability
                ? b.Probability.CompareTo(a.Probability)
                : string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        private static void Enumerate(double[][] perGene, int index, double probability, Genotype child,
            Dictionary<string, double> totals)
        {
            if (index == perGene.Length)
            {
                var name = MorphNamer.VisualName(child);
                totals.TryGetValue(name, out var sum);
                totals[name] = sum + probability;
                return;
            }

            for (var copies = 0; copies < 3; copies++)
            {
                var p = perGene[index][copies];
                if (p <= 0d)
                {
                    continue;
                }

                child.Set(Genes.All[index], copies);
                Enumerate(perGene, index + 1, probability * p, child, totals);
            }

            child.Set(Genes.All[index], 0);
        }
    }

    public readonly struct MorphOdds
    {
        public MorphOdds(string name, double probability)
        {
            Name = name;
            Probability = probability;
        }

        public string Name { get; }

        public double Probability { get; }
    }
}
