using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class MorphAppearanceTests
    {
        private static MorphPalette For(Genotype genotype, GrowthStage stage = GrowthStage.Adult) =>
            MorphAppearance.PaletteFor(genotype, stage);

        [Test]
        public void Normal_MatchesTheSpriteGenerator()
        {
            Assert.That(MorphAppearance.Normal[PaletteRole.Outline], Is.EqualTo(new Rgb(58, 36, 26)));
            Assert.That(MorphAppearance.Normal[PaletteRole.Base], Is.EqualTo(new Rgb(246, 178, 58)));
            Assert.That(MorphAppearance.Normal[PaletteRole.Spot], Is.EqualTo(new Rgb(70, 44, 30)));
            Assert.That(MorphAppearance.Normal[PaletteRole.Eye], Is.EqualTo(new Rgb(30, 22, 18)));
            Assert.That(MorphAppearance.Normal[PaletteRole.LegFar], Is.EqualTo(new Rgb(206, 132, 44)));
        }

        [Test]
        public void ANormalLookingGenotype_KeepsTheNormalColours()
        {
            var het = Genotype.Normal(hypo: 30d, tangerine: 30d).Set(GeneId.Blizzard, 1);

            Assert.That(For(het).Key, Is.EqualTo(MorphAppearance.Normal.Key));
        }

        [Test]
        public void Albino_HasRedEyesAndPaleSpots()
        {
            var palette = For(Genotype.Normal().Set(GeneId.BellAlbino, 2));

            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(190, 50, 60)));
            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(new Rgb(196, 140, 112)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(new Rgb(255, 255, 255)));
        }

        [Test]
        public void Eclipse_HasSolidBlackEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.Eclipse, 2));

            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(12, 12, 14)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(palette[PaletteRole.Eye]));
        }

        [Test]
        public void Raptor_HasSolidRedEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2));

            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(190, 50, 60)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(palette[PaletteRole.Eye]));
        }

        [Test]
        public void Blizzard_HasNoPattern()
        {
            var palette = For(Genotype.Normal().Set(GeneId.Blizzard, 2));

            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(palette[PaletteRole.Base]));
            Assert.That(palette[PaletteRole.Band], Is.EqualTo(palette[PaletteRole.Shade]));
        }

        [Test]
        public void BlazingBlizzard_StaysPatternlessWithRedEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2));

            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(palette[PaletteRole.Base]));
            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(190, 50, 60)));
        }

        [Test]
        public void SuperSnow_IsWhiteWithBlackSpotsAndEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.MackSnow, 2));

            Assert.That(palette[PaletteRole.Base], Is.EqualTo(new Rgb(242, 242, 238)));
            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(new Rgb(30, 30, 34)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(palette[PaletteRole.Eye]));
        }

        [Test]
        public void MurphyPatternless_LosesSpotsOnlyOnceGrown()
        {
            var murphy = Genotype.Normal().Set(GeneId.MurphyPatternless, 2);

            Assert.That(For(murphy, GrowthStage.Baby)[PaletteRole.Spot], Is.EqualTo(MorphAppearance.Normal[PaletteRole.Spot]));
            var grown = For(murphy, GrowthStage.Juvenile);
            Assert.That(grown[PaletteRole.Spot], Is.EqualTo(grown[PaletteRole.Base]));
        }

        [Test]
        public void Hypo_RemovesBodySpotsButKeepsTailBands()
        {
            var palette = For(Genotype.Normal(hypo: 70d));

            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(palette[PaletteRole.Base]));
            Assert.That(palette[PaletteRole.Band], Is.EqualTo(MorphAppearance.Normal[PaletteRole.Band]));
        }

        [Test]
        public void Tangerine_ShiftsTheBodyTowardsOrange()
        {
            var palette = For(Genotype.Normal(tangerine: 100d));

            Assert.That(palette[PaletteRole.Base], Is.EqualTo(new Rgb(250, 130, 40)));
        }

        [Test]
        public void MackSnowAndWhiteAndYellow_LightenTheBody()
        {
            var normalBase = MorphAppearance.Normal[PaletteRole.Base];

            Assert.That(For(Genotype.Normal().Set(GeneId.MackSnow, 1))[PaletteRole.Base].B, Is.GreaterThan(normalBase.B));
            Assert.That(For(Genotype.Normal().Set(GeneId.WhiteAndYellow, 1))[PaletteRole.Belly], Is.EqualTo(new Rgb(255, 255, 255)));
        }

        [Test]
        public void Key_IsEqualForEqualInputsAndDiffersBetweenMorphs()
        {
            var a = For(Genotype.Normal().Set(GeneId.Eclipse, 2));
            var b = For(Genotype.Normal().Set(GeneId.Eclipse, 2));

            Assert.That(a.Key, Is.EqualTo(b.Key));
            Assert.That(a.Key, Is.Not.EqualTo(MorphAppearance.Normal.Key));
        }

        [Test]
        public void PreShed_MatchesThePythonGenerator()
        {
            Assert.That(MorphAppearance.PreShed(new Rgb(58, 36, 26), PaletteRole.Outline), Is.EqualTo(new Rgb(111, 96, 91)));
            Assert.That(MorphAppearance.PreShed(new Rgb(246, 178, 58), PaletteRole.Base), Is.EqualTo(new Rgb(241, 207, 150)));
        }
    }
}
