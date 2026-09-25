using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// Small sprites (hearts, "Zzz") that rise from a point, sway, and fade out. They are
    /// added to the terrarium view above everything else and never take pointer input.
    /// </summary>
    public sealed class FloatingEffects
    {
        private sealed class Particle
        {
            public VisualElement Element;
            public Vector2 Origin;
            public float Delay;
            public float Age;
            public float Life;
            public float Rise;
            public float Sway;
            public float Size;
        }

        private readonly VisualElement container;
        private readonly List<Particle> particles = new List<Particle>();

        public FloatingEffects(VisualElement container)
        {
            this.container = container;
        }

        public int ActiveCount => particles.Count;

        public void Spawn(Texture2D texture, Vector2 origin, float size, float life, float rise, float sway, float delay = 0f)
        {
            if (container == null || texture == null)
            {
                return;
            }

            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.width = size;
            element.style.height = size;
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.opacity = 0f;
            container.Add(element);

            particles.Add(new Particle
            {
                Element = element,
                Origin = origin,
                Delay = delay,
                Life = life,
                Rise = rise,
                Sway = sway,
                Size = size,
            });
        }

        public void Tick(float deltaSeconds)
        {
            for (var i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += deltaSeconds;
                var t = (p.Age - p.Delay) / p.Life;

                if (t >= 1f)
                {
                    p.Element.RemoveFromHierarchy();
                    particles.RemoveAt(i);
                    continue;
                }

                if (t < 0f)
                {
                    continue;
                }

                // Pop in quickly, drift up with a gentle sway, fade out over the last half.
                var scale = Mathf.Min(1f, t * 6f);
                p.Element.style.left = p.Origin.x - p.Size / 2f + p.Sway * Mathf.Sin(t * Mathf.PI * 2f);
                p.Element.style.top = p.Origin.y - p.Size / 2f - p.Rise * t;
                p.Element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                p.Element.style.opacity = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            }
        }

        public void Clear()
        {
            foreach (var p in particles)
            {
                p.Element.RemoveFromHierarchy();
            }

            particles.Clear();
        }
    }
}
