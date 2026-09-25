using System.Collections.Generic;
using TerrariumDays.Core;
using UnityEngine;

namespace TerrariumDays.UI
{
    /// <summary>Recolours a gecko frame from the base palette to a morph palette (§4.5).</summary>
    public static class MorphRecolor
    {
        private static Dictionary<int, PaletteRole> roleByColor;

        public static Texture2D Apply(Texture2D source, MorphPalette palette, bool preShed)
        {
            var roles = RoleByColor();
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 0)
                {
                    continue;
                }

                Rgb color;
                if (roles.TryGetValue((p.r << 16) | (p.g << 8) | p.b, out var role))
                {
                    color = palette[role];
                    if (preShed)
                    {
                        color = MorphAppearance.PreShed(color, role);
                    }
                }
                else if (preShed)
                {
                    color = MorphAppearance.PreShed(new Rgb(p.r, p.g, p.b), PaletteRole.Base);
                }
                else
                {
                    continue;
                }

                pixels[i] = new Color32(color.R, color.G, color.B, p.a);
            }

            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = source.name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            result.SetPixels32(pixels);
            result.Apply(false, false);
            return result;
        }

        private static Dictionary<int, PaletteRole> RoleByColor()
        {
            if (roleByColor != null)
            {
                return roleByColor;
            }

            roleByColor = new Dictionary<int, PaletteRole>();
            foreach (PaletteRole role in System.Enum.GetValues(typeof(PaletteRole)))
            {
                var c = MorphAppearance.Normal[role];
                roleByColor[(c.R << 16) | (c.G << 8) | c.B] = role;
            }

            return roleByColor;
        }
    }
}
