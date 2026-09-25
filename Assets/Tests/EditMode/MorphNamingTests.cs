using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class MorphNamingTests
    {
        [Test]
        public void VisualName_NormalIsNormal()
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal()), Is.EqualTo("ノーマル"));
        }

        [Test]
        public void VisualName_AHetDoesNotShow()
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal().Set(GeneId.TremperAlbino, 1)), Is.EqualTo("ノーマル"));
        }

        [TestCase(GeneId.TremperAlbino, 2, "トレンパーアルビノ")]
        [TestCase(GeneId.Eclipse, 2, "エクリプス")]
        [TestCase(GeneId.MackSnow, 1, "マックスノー")]
        [TestCase(GeneId.MackSnow, 2, "スーパースノー")]
        [TestCase(GeneId.WhiteAndYellow, 1, "ホワイト&イエロー")]
        [TestCase(GeneId.WhiteAndYellow, 2, "ホワイト&イエロー")]
        public void VisualName_SingleGenes(GeneId gene, int copies, string expected)
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal().Set(gene, copies)), Is.EqualTo(expected));
        }

        [Test]
        public void VisualName_UsesCommonNames()
        {
            var blazing = Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2);
            var raptorSnow = Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2);

            Assert.That(MorphNamer.VisualName(blazing), Is.EqualTo("ブレイジングブリザード"));
            Assert.That(MorphNamer.VisualName(raptorSnow), Is.EqualTo("マックスノー レイプター"));
        }

        [Test]
        public void VisualName_BellAlbinoWithBlizzardHasNoCommonName()
        {
            var genotype = Genotype.Normal().Set(GeneId.BellAlbino, 2).Set(GeneId.Blizzard, 2);

            Assert.That(MorphNamer.VisualName(genotype), Is.EqualTo("ベルアルビノ ブリザード"));
        }

        [Test]
        public void VisualName_PolygenicNamesNeedTheThresholds()
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal(hypo: 69.9d)), Is.EqualTo("ノーマル"));
            Assert.That(MorphNamer.VisualName(Genotype.Normal(hypo: 70d)), Is.EqualTo("ハイポ"));
            Assert.That(MorphNamer.VisualName(Genotype.Normal(tangerine: 60d)), Is.EqualTo("タンジェリン"));
            Assert.That(MorphNamer.VisualName(Genotype.Normal(hypo: 80d, tangerine: 80d).Set(GeneId.MackSnow, 1)),
                Is.EqualTo("ハイポ タンジェリン マックスノー"));
        }

        [Test]
        public void FullName_ListsHetsAndPossibleHets()
        {
            var genotype = Genotype.Normal().Set(GeneId.TremperAlbino, 1).Set(GeneId.Eclipse, 1);
            var known = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d).SetHet(GeneId.Eclipse, 2d / 3d);

            Assert.That(MorphNamer.FullName(genotype, known), Is.EqualTo("ノーマル ヘテロトレンパーアルビノ 66%ポッシブルヘテロエクリプス"));
        }

        [Test]
        public void FullName_UnknownHets()
        {
            Assert.That(MorphNamer.FullName(Genotype.Normal(), KnownGenetics.Unknown()), Is.EqualTo("ノーマル（ヘテロ不明）"));
        }

        [Test]
        public void FullName_IgnoresHetInfoForAVisibleGene()
        {
            var genotype = Genotype.Normal().Set(GeneId.Eclipse, 2);
            var known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);

            Assert.That(MorphNamer.FullName(genotype, known), Is.EqualTo("エクリプス"));
        }

        [Test]
        public void ForChild_HetTimesHet_NormalLookingChildIs66Percent()
        {
            var het = Genotype.Normal().Set(GeneId.Blizzard, 1);
            var hetKnown = new KnownGenetics().SetHet(GeneId.Blizzard, 1d);

            var known = KnownGenetics.ForChild(het, hetKnown, het, hetKnown, Genotype.Normal());

            Assert.That(known.HetProbability(GeneId.Blizzard), Is.EqualTo(2d / 3d).Within(1e-9));
            Assert.That(known.HetsUnknown, Is.False);
        }

        [Test]
        public void ForChild_HetTimesNormal_Is50Percent()
        {
            var het = Genotype.Normal().Set(GeneId.Blizzard, 1);
            var hetKnown = new KnownGenetics().SetHet(GeneId.Blizzard, 1d);

            var known = KnownGenetics.ForChild(het, hetKnown, Genotype.Normal(), new KnownGenetics(), Genotype.Normal());

            Assert.That(known.HetProbability(GeneId.Blizzard), Is.EqualTo(0.5d).Within(1e-9));
        }

        [Test]
        public void ForChild_VisualTimesNormal_IsAProvenHet()
        {
            var visual = Genotype.Normal().Set(GeneId.Eclipse, 2);

            var known = KnownGenetics.ForChild(visual, new KnownGenetics(), Genotype.Normal(), new KnownGenetics(),
                Genotype.Normal().Set(GeneId.Eclipse, 1));

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(1d));
        }

        [Test]
        public void ForChild_UsesWhatThePlayerKnowsNotTheTruth()
        {
            // The father secretly carries tremper, but the player does not know it.
            var secretHet = Genotype.Normal().Set(GeneId.TremperAlbino, 1);

            var known = KnownGenetics.ForChild(Genotype.Normal(), new KnownGenetics(), secretHet, KnownGenetics.Unknown(),
                Genotype.Normal().Set(GeneId.TremperAlbino, 1));

            Assert.That(known.HetProbability(GeneId.TremperAlbino), Is.EqualTo(0d));
        }

        [Test]
        public void ForChild_AVisualChildHasNoHetEntry()
        {
            var visual = Genotype.Normal().Set(GeneId.Eclipse, 2);

            var known = KnownGenetics.ForChild(visual, new KnownGenetics(), visual, new KnownGenetics(), visual.Clone());

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(0d));
        }

        [Test]
        public void Predict_VisualTimesProvenHet_IsHalfAndHalf()
        {
            var visual = Genotype.Normal().Set(GeneId.TremperAlbino, 2);
            var het = Genotype.Normal().Set(GeneId.TremperAlbino, 1);
            var hetKnown = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d);

            var odds = GeneticsCalculator.PredictVisualOdds(visual, new KnownGenetics(), het, hetKnown);

            Assert.That(odds.Count, Is.EqualTo(2));
            Assert.That(odds.Single(o => o.Name == "トレンパーアルビノ").Probability, Is.EqualTo(0.5d).Within(1e-9));
            Assert.That(odds.Single(o => o.Name == "ノーマル").Probability, Is.EqualTo(0.5d).Within(1e-9));
        }

        [Test]
        public void Predict_MackSnowPair_IsQuarterHalfQuarter_SortedByProbability()
        {
            var snow = Genotype.Normal().Set(GeneId.MackSnow, 1);

            var odds = GeneticsCalculator.PredictVisualOdds(snow, new KnownGenetics(), snow, new KnownGenetics());

            Assert.That(odds[0].Name, Is.EqualTo("マックスノー"));
            Assert.That(odds[0].Probability, Is.EqualTo(0.5d).Within(1e-9));
            Assert.That(odds.Single(o => o.Name == "スーパースノー").Probability, Is.EqualTo(0.25d).Within(1e-9));
            Assert.That(odds.Single(o => o.Name == "ノーマル").Probability, Is.EqualTo(0.25d).Within(1e-9));
        }

        [Test]
        public void Predict_ProbabilitiesSumToOne()
        {
            var mother = Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.Eclipse, 1).Set(GeneId.WhiteAndYellow, 1);
            var motherKnown = new KnownGenetics().SetHet(GeneId.Eclipse, 0.66d).SetHet(GeneId.Blizzard, 0.5d);
            var father = Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 1);
            var fatherKnown = new KnownGenetics().SetHet(GeneId.Blizzard, 1d).SetHet(GeneId.Eclipse, 1d);

            var odds = GeneticsCalculator.PredictVisualOdds(mother, motherKnown, father, fatherKnown);

            Assert.That(odds.Sum(o => o.Probability), Is.EqualTo(1d).Within(1e-9));
            Assert.That(odds.Select(o => o.Name).Distinct().Count(), Is.EqualTo(odds.Count));
        }
    }
}
