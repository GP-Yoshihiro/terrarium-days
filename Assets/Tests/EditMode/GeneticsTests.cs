using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class GeneticsTests
    {
        [TestCase(2, 2, 0d, 0d, 1d)]
        [TestCase(1, 1, 0.25d, 0.5d, 0.25d)]
        [TestCase(1, 0, 0.5d, 0.5d, 0d)]
        [TestCase(2, 0, 0d, 1d, 0d)]
        [TestCase(0, 0, 1d, 0d, 0d)]
        public void CopiesOutcome_FollowsMendel(int mother, int father, double none, double one, double two)
        {
            var odds = GeneticsCalculator.CopiesOutcome(mother, father);

            Assert.That(odds[0], Is.EqualTo(none).Within(1e-9));
            Assert.That(odds[1], Is.EqualTo(one).Within(1e-9));
            Assert.That(odds[2], Is.EqualTo(two).Within(1e-9));
        }

        [Test]
        public void Visibility_DependsOnInheritance()
        {
            Assert.That(Genes.IsVisible(GeneId.TremperAlbino, 1), Is.False);
            Assert.That(Genes.IsVisible(GeneId.TremperAlbino, 2), Is.True);
            Assert.That(Genes.IsVisible(GeneId.MackSnow, 1), Is.True);
            Assert.That(Genes.IsVisible(GeneId.WhiteAndYellow, 1), Is.True);
            Assert.That(Genes.IsVisible(GeneId.WhiteAndYellow, 0), Is.False);
        }

        [Test]
        public void Genotype_ClampsCopiesAndPolygenicValues()
        {
            var genotype = Genotype.Normal().Set(GeneId.Eclipse, 5);
            genotype.Hypo = 140d;
            genotype.Tangerine = -3d;

            Assert.That(genotype.Copies(GeneId.Eclipse), Is.EqualTo(2));
            Assert.That(genotype.Hypo, Is.EqualTo(100d));
            Assert.That(genotype.Tangerine, Is.EqualTo(0d));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var original = Genotype.Normal().Set(GeneId.Blizzard, 1);
            var copy = original.Clone().Set(GeneId.Blizzard, 2);

            Assert.That(original.Copies(GeneId.Blizzard), Is.EqualTo(1));
            Assert.That(copy.Copies(GeneId.Blizzard), Is.EqualTo(2));
        }

        [Test]
        public void Breed_HetTimesHet_GivesAboutAQuarterVisual()
        {
            var parent = Genotype.Normal().Set(GeneId.TremperAlbino, 1);
            var random = new Random(1234);
            var visual = 0;
            const int count = 4000;
            for (var i = 0; i < count; i++)
            {
                if (GeneticsCalculator.Breed(parent, parent, random).Shows(GeneId.TremperAlbino))
                {
                    visual++;
                }
            }

            Assert.That(visual / (double)count, Is.EqualTo(0.25d).Within(0.03d));
        }

        [Test]
        public void Breed_DifferentAlbinoStrains_NeverGivesAnAlbino()
        {
            var tremper = Genotype.Normal().Set(GeneId.TremperAlbino, 2);
            var bell = Genotype.Normal().Set(GeneId.BellAlbino, 2);
            var random = new Random(7);
            for (var i = 0; i < 200; i++)
            {
                var child = GeneticsCalculator.Breed(tremper, bell, random);
                Assert.That(child.Shows(GeneId.TremperAlbino) || child.Shows(GeneId.BellAlbino), Is.False);
                Assert.That(child.Copies(GeneId.TremperAlbino), Is.EqualTo(1));
                Assert.That(child.Copies(GeneId.BellAlbino), Is.EqualTo(1));
            }
        }

        [Test]
        public void Breed_MackSnowTimesMackSnow_GivesAboutAQuarterSuperSnow()
        {
            var parent = Genotype.Normal().Set(GeneId.MackSnow, 1);
            var random = new Random(99);
            var super = 0;
            const int count = 4000;
            for (var i = 0; i < count; i++)
            {
                if (GeneticsCalculator.Breed(parent, parent, random).Copies(GeneId.MackSnow) == 2)
                {
                    super++;
                }
            }

            Assert.That(super / (double)count, Is.EqualTo(0.25d).Within(0.03d));
        }

        [Test]
        public void Breed_Polygenic_CentresOnTheParentMeanWithSpreadTen()
        {
            var mother = Genotype.Normal(hypo: 40d, tangerine: 50d);
            var father = Genotype.Normal(hypo: 60d, tangerine: 50d);
            var random = new Random(5);
            const int count = 3000;
            double sum = 0d, sumSquares = 0d;
            for (var i = 0; i < count; i++)
            {
                var hypo = GeneticsCalculator.Breed(mother, father, random).Hypo;
                Assert.That(hypo, Is.InRange(0d, 100d));
                sum += hypo;
                sumSquares += hypo * hypo;
            }

            var mean = sum / count;
            var sd = Math.Sqrt(sumSquares / count - mean * mean);
            Assert.That(mean, Is.EqualTo(50d).Within(1d));
            Assert.That(sd, Is.EqualTo(GeneticsCalculator.PolygenicSpread).Within(1.5d));
        }

        [Test]
        public void Breed_IsReproducibleForTheSameSeed()
        {
            var mother = Genotype.Normal().Set(GeneId.Eclipse, 1).Set(GeneId.MackSnow, 1);
            var father = Genotype.Normal().Set(GeneId.Eclipse, 2);

            var a = GeneticsCalculator.Breed(mother, father, new Random(42));
            var b = GeneticsCalculator.Breed(mother, father, new Random(42));

            foreach (var gene in Genes.All)
            {
                Assert.That(a.Copies(gene), Is.EqualTo(b.Copies(gene)));
            }

            Assert.That(a.Hypo, Is.EqualTo(b.Hypo));
        }

        [Test]
        public void AProbabilityThatIsOneUpToRoundingError_CountsAsAProvenHet()
        {
            var nearlyOne = 0.7d + 0.2d + 0.1d; // 0.9999999999999999 in doubles
            Assert.That(nearlyOne, Is.LessThan(1d), "the test needs a real rounding error");

            var known = new KnownGenetics().SetHet(GeneId.Eclipse, nearlyOne).SetHet(GeneId.Blizzard, 1e-17);

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(1d));
            Assert.That(known.HetProbability(GeneId.Blizzard), Is.EqualTo(0d));
            Assert.That(KnownGenetics.IsProvenHet(nearlyOne), Is.True);
            Assert.That(KnownGenetics.IsProvenHet(0.99d), Is.False);
            Assert.That(MorphNamer.FullName(Genotype.Normal(), known), Is.EqualTo("ノーマル ヘテロエクリプス"));
            Assert.That(MarketPrice.HetMultiplier(Genotype.Normal(), known), Is.EqualTo(1.2d).Within(1e-9));
        }

        [Test]
        public void ANaNProbability_IsTreatedAsNoHet()
        {
            var known = new KnownGenetics().SetHet(GeneId.Eclipse, double.NaN);

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(0d));
        }
    }
}
