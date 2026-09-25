using System;
using System.Text;

namespace TerrariumDays.Core
{
    /// <summary>The colour roles of the gecko sprites (tools/sprites/gecko_sprites.py).</summary>
    public enum PaletteRole
    {
        Outline,
        Base,
        Light,
        Shade,
        Belly,
        BellyShade,
        Spot,
        Band,
        Eye,
        EyeHighlight,
        Mouth,
        Tongue,
        LegFar
    }

    public sealed class MorphPalette
    {
        private readonly Rgb[] colors;

        public MorphPalette(Rgb[] colors)
        {
            this.colors = colors;
            var key = new StringBuilder();
            foreach (var color in colors)
            {
                key.Append(color.ToString());
            }

            Key = key.ToString();
        }

        public Rgb this[PaletteRole role] => colors[(int)role];

        /// <summary>Equal for equal colours: the sprite cache key.</summary>
        public string Key { get; }
    }

    /// <summary>How each morph recolours the base sprites (§4.5).</summary>
    public static class MorphAppearance
    {
        private static readonly Rgb PreShedWhite = new Rgb(236, 236, 242);
        private static readonly PaletteRole[] Body = { PaletteRole.Base, PaletteRole.Light, PaletteRole.Shade, PaletteRole.LegFar };

        public static readonly MorphPalette Normal = new MorphPalette(NormalColors());

        private static Rgb[] NormalColors() => new[]
        {
            new Rgb(58, 36, 26),    // Outline
            new Rgb(246, 178, 58),  // Base
            new Rgb(253, 206, 104), // Light
            new Rgb(222, 142, 46),  // Shade
            new Rgb(251, 238, 214), // Belly
            new Rgb(232, 212, 180), // BellyShade
            new Rgb(70, 44, 30),    // Spot
            new Rgb(84, 52, 34),    // Band
            new Rgb(30, 22, 18),    // Eye
            new Rgb(255, 255, 255), // EyeHighlight
            new Rgb(200, 70, 80),   // Mouth
            new Rgb(240, 110, 120), // Tongue
            new Rgb(206, 132, 44),  // LegFar
        };

        public static MorphPalette PaletteFor(Genotype g, GrowthStage stage)
        {
            var c = NormalColors();

            var tangerine = Math.Max(0d, Math.Min(1d, (g.Tangerine - 30d) / 70d));
            if (tangerine > 0d)
            {
                Toward(c, PaletteRole.Base, new Rgb(250, 130, 40), tangerine);
                Toward(c, PaletteRole.Light, new Rgb(252, 165, 80), tangerine);
                Toward(c, PaletteRole.Shade, new Rgb(225, 100, 30), tangerine);
                Toward(c, PaletteRole.LegFar, new Rgb(205, 95, 30), tangerine);
            }

            if (g.Shows(GeneId.WhiteAndYellow))
            {
                BodyToward(c, new Rgb(250, 246, 230), 0.35d);
                Set(c, PaletteRole.Belly, new Rgb(255, 255, 255));
            }

            if (g.Copies(GeneId.MackSnow) == 1)
            {
                BodyToward(c, new Rgb(238, 236, 220), 0.7d);
            }
            else if (g.Copies(GeneId.MackSnow) == 2)
            {
                Set(c, PaletteRole.Base, new Rgb(242, 242, 238));
                Set(c, PaletteRole.Light, new Rgb(252, 252, 250));
                Set(c, PaletteRole.Shade, new Rgb(214, 214, 212));
                Set(c, PaletteRole.LegFar, new Rgb(200, 200, 198));
                Set(c, PaletteRole.Spot, new Rgb(30, 30, 34));
                Set(c, PaletteRole.Band, new Rgb(30, 30, 34));
                Set(c, PaletteRole.Eye, new Rgb(20, 20, 22));
                Set(c, PaletteRole.EyeHighlight, c[(int)PaletteRole.Eye]);
            }

            if (g.Hypo >= MorphNamer.HypoNameThreshold)
            {
                Set(c, PaletteRole.Spot, c[(int)PaletteRole.Base]);
            }

            if (g.Shows(GeneId.MurphyPatternless) && stage != GrowthStage.Baby)
            {
                BodyToward(c, new Rgb(205, 170, 120), 0.5d);
                Set(c, PaletteRole.Spot, c[(int)PaletteRole.Base]);
                Set(c, PaletteRole.Band, c[(int)PaletteRole.Shade]);
            }

            if (g.Shows(GeneId.Blizzard))
            {
                Set(c, PaletteRole.Base, new Rgb(205, 205, 210));
                Set(c, PaletteRole.Light, new Rgb(228, 228, 232));
                Set(c, PaletteRole.Shade, new Rgb(180, 180, 188));
                Set(c, PaletteRole.LegFar, new Rgb(170, 170, 178));
                Set(c, PaletteRole.Spot, c[(int)PaletteRole.Base]);
                Set(c, PaletteRole.Band, c[(int)PaletteRole.Shade]);
            }

            var albino = g.Shows(GeneId.TremperAlbino) || g.Shows(GeneId.BellAlbino) || g.Shows(GeneId.RainwaterAlbino);
            if (albino)
            {
                if (!c[(int)PaletteRole.Spot].Equals(c[(int)PaletteRole.Base]))
                {
                    Set(c, PaletteRole.Spot, new Rgb(196, 140, 112));
                }

                if (!c[(int)PaletteRole.Band].Equals(c[(int)PaletteRole.Shade]))
                {
                    Set(c, PaletteRole.Band, new Rgb(206, 158, 128));
                }

                Set(c, PaletteRole.Eye, new Rgb(190, 50, 60));
            }

            if (g.Shows(GeneId.Eclipse))
            {
                if (!albino)
                {
                    Set(c, PaletteRole.Eye, new Rgb(12, 12, 14));
                }

                Set(c, PaletteRole.EyeHighlight, c[(int)PaletteRole.Eye]);
            }

            return new MorphPalette(c);
        }

        /// <summary>The milky pre-shed skin, as tools/sprites/gecko_sprites.py pre_shed() did.</summary>
        public static Rgb PreShed(Rgb color, PaletteRole role)
        {
            var k = role == PaletteRole.Outline || role == PaletteRole.Eye ? 0.3d : 0.5d;
            return Rgb.Lerp(color, PreShedWhite, k);
        }

        private static void Set(Rgb[] c, PaletteRole role, Rgb color) => c[(int)role] = color;

        private static void Toward(Rgb[] c, PaletteRole role, Rgb target, double t) =>
            c[(int)role] = Rgb.Lerp(c[(int)role], target, t);

        private static void BodyToward(Rgb[] c, Rgb target, double t)
        {
            foreach (var role in Body)
            {
                Toward(c, role, target, t);
            }
        }
    }
}
