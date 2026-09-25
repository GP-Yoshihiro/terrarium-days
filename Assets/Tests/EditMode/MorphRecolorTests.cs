using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;
using UnityEngine;

namespace TerrariumDays.Tests
{
    public sealed class MorphRecolorTests
    {
        private static Texture2D Pixels(params Color32[] pixels)
        {
            var texture = new Texture2D(pixels.Length, 1, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Color32 C(Rgb c, byte a = 255) => new Color32(c.R, c.G, c.B, a);

        [Test]
        public void Apply_ReplacesPaletteColoursByRole()
        {
            var normal = MorphAppearance.Normal;
            var albino = MorphAppearance.PaletteFor(Genotype.Normal().Set(GeneId.TremperAlbino, 2), GrowthStage.Adult);
            var source = Pixels(C(normal[PaletteRole.Eye]), C(normal[PaletteRole.Spot]), C(normal[PaletteRole.Base]));

            var result = MorphRecolor.Apply(source, albino, preShed: false).GetPixels32();

            Assert.That(result[0], Is.EqualTo(C(albino[PaletteRole.Eye])));
            Assert.That(result[1], Is.EqualTo(C(albino[PaletteRole.Spot])));
            Assert.That(result[2], Is.EqualTo(C(albino[PaletteRole.Base])));
        }

        [Test]
        public void Apply_KeepsTransparentAndUnknownPixels()
        {
            var unknown = new Color32(1, 2, 3, 255);
            var source = Pixels(new Color32(0, 0, 0, 0), unknown);

            var result = MorphRecolor.Apply(source, MorphAppearance.Normal, preShed: false).GetPixels32();

            Assert.That(result[0].a, Is.EqualTo(0));
            Assert.That(result[1], Is.EqualTo(unknown));
        }

        [Test]
        public void Apply_PreShed_WhitensTheRecolouredPixel()
        {
            var normal = MorphAppearance.Normal;
            var source = Pixels(C(normal[PaletteRole.Outline]));

            var result = MorphRecolor.Apply(source, normal, preShed: true).GetPixels32();

            Assert.That(result[0], Is.EqualTo(C(new Rgb(111, 96, 91))));
        }

        [Test]
        public void GeckoSprites_UseOnlyPaletteColours()
        {
            // Guards the generator and the palette against drifting apart.
            var known = new System.Collections.Generic.HashSet<Rgb>();
            foreach (PaletteRole role in System.Enum.GetValues(typeof(PaletteRole)))
            {
                known.Add(MorphAppearance.Normal[role]);
            }

            foreach (var texture in Resources.LoadAll<Texture2D>("Gecko"))
            {
                Assert.That(texture.isReadable, Is.True, texture.name + " must be imported with Read/Write enabled");
                foreach (var pixel in texture.GetPixels32())
                {
                    if (pixel.a > 0)
                    {
                        Assert.That(known.Contains(new Rgb(pixel.r, pixel.g, pixel.b)), Is.True,
                            $"{texture.name} has a colour outside the palette: {pixel}");
                    }
                }
            }
        }

        [Test]
        public void Library_WithPalette_RecoloursFramesLazilyAndCaches()
        {
            var normal = MorphAppearance.Normal;
            var frame = Pixels(C(normal[PaletteRole.Base]));
            frame.name = "idle_00";
            var library = new PetSpriteLibrary(new[] { frame }, null, null);
            var blizzard = MorphAppearance.PaletteFor(Genotype.Normal().Set(GeneId.Blizzard, 2), GrowthStage.Adult);

            var recoloured = library.WithPalette(blizzard);

            Assert.That(library.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0), Is.SameAs(frame));
            var first = recoloured.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0);
            Assert.That(first.GetPixel(0, 0), Is.EqualTo((Color)C(blizzard[PaletteRole.Base])));
            Assert.That(recoloured.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0), Is.SameAs(first));
            Assert.That(recoloured.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0, preShed: true), Is.Not.SameAs(first));
        }
    }
}
