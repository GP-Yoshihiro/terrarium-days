using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// What the player knows about an animal's hidden (het) genes (§4.3): per recessive gene
    /// the chance it carries one copy (1 = proven het, between 0 and 1 = possible het).
    /// What shows is visible to the player and is read from the genotype itself.
    /// </summary>
    public sealed class KnownGenetics
    {
        private readonly double[] het = new double[Genes.All.Length];

        /// <summary>Hets were never tracked (e.g. the starting animal): shown as 「ヘテロ不明」.</summary>
        public bool HetsUnknown { get; set; }

        /// <summary>Probabilities this close to 0 or 1 are snapped, so float error never reads "99%" for a proven het.</summary>
        public const double SnapEpsilon = 1e-9;

        public double HetProbability(GeneId gene) => het[(int)gene];

        public KnownGenetics SetHet(GeneId gene, double probability)
        {
            var p = double.IsNaN(probability) ? 0d : Math.Max(0d, Math.Min(1d, probability));
            if (p >= 1d - SnapEpsilon)
            {
                p = 1d;
            }
            else if (p <= SnapEpsilon)
            {
                p = 0d;
            }

            het[(int)gene] = p;
            return this;
        }

        /// <summary>The one place that decides "proven het" (names, prices).</summary>
        public static bool IsProvenHet(double probability) => probability >= 1d - SnapEpsilon;

        public KnownGenetics Clone()
        {
            var clone = new KnownGenetics { HetsUnknown = HetsUnknown };
            Array.Copy(het, clone.het, het.Length);
            return clone;
        }

        public static KnownGenetics Unknown() => new KnownGenetics { HetsUnknown = true };

        /// <summary>The copy count (0/1/2) the player believes an animal has, as probabilities.</summary>
        public static double[] BelievedCopies(Genotype genotype, KnownGenetics known, GeneId gene)
        {
            if (!Genes.IsRecessive(gene))
            {
                // Visible: mack snow shows 1 vs 2 copies; white & yellow looks the same with
                // either, so a visible W&Y is assumed to carry one.
                var copies = !genotype.Shows(gene) ? 0 : gene == GeneId.MackSnow ? genotype.Copies(gene) : 1;
                return OneHot(copies);
            }

            if (genotype.Shows(gene))
            {
                return OneHot(2);
            }

            var p = known.HetProbability(gene);
            return new[] { 1d - p, p, 0d };
        }

        /// <summary>Child copy odds from two parents' copy-count distributions.</summary>
        public static double[] Blend(double[] mother, double[] father)
        {
            var result = new double[3];
            for (var m = 0; m < 3; m++)
            {
                for (var f = 0; f < 3; f++)
                {
                    var weight = mother[m] * father[f];
                    if (weight <= 0d)
                    {
                        continue;
                    }

                    var outcome = GeneticsCalculator.CopiesOutcome(m, f);
                    for (var k = 0; k < 3; k++)
                    {
                        result[k] += weight * outcome[k];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// What the player knows about a newly hatched child: for each recessive gene the child
        /// does not show, the chance it is het given what the player knew about the parents.
        /// </summary>
        public static KnownGenetics ForChild(Genotype mother, KnownGenetics motherKnown, Genotype father,
            KnownGenetics fatherKnown, Genotype child)
        {
            var known = new KnownGenetics();
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || child.Shows(gene))
                {
                    continue;
                }

                var odds = Blend(BelievedCopies(mother, motherKnown, gene), BelievedCopies(father, fatherKnown, gene));
                var notVisual = odds[0] + odds[1];
                if (notVisual > 0d)
                {
                    known.SetHet(gene, odds[1] / notVisual);
                }
            }

            return known;
        }

        private static double[] OneHot(int copies)
        {
            var result = new double[3];
            result[copies] = 1d;
            return result;
        }
    }
}
