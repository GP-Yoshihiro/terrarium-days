using UnityEngine;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// The aisle seen from inside our booth: a small pool of passersby walks left to right and right to left in
    /// two depth rows whenever the day screen is visible, and the customer of a replay walks in with them,
    /// stops at our counter and walks off again. All positions are percentages of the stage; nothing here
    /// allocates per frame (textures come from the cached frame table, elements are reused).
    /// </summary>
    public sealed partial class EventView
    {
        private const int FarCount = 4;
        private const int NearCount = 4;
        private const float CrowdMinLeft = -16f;
        private const float CrowdMaxLeft = 116f;
        private const float CustomerStopLeft = 43.25f;
        private const float CustomerArriveSeconds = 2.8f;
        private const float CustomerWalkSpeed = 22f;
        private const float MaxCrowdStep = 0.25f;

        private sealed class Passerby
        {
            public VisualElement Element;
            public bool Near;
            public int Kind;
            public int Dir;
            public float Left;
            public float Speed;
            public float Gait;
            public Texture2D Shown;
            public int ShownDir;
        }

        private readonly Passerby[] crowd = new Passerby[FarCount + NearCount];
        private readonly System.Random crowdRandom = new System.Random(20261021);
        private bool dayShown;

        private int customerKind;
        private int customerDir = 1;
        private bool customerLeaving;
        private float customerLeaveLeft;
        private float customerLeaveGait;

        /// <summary>Left edge (percent of the stage) of the customer; it stops at <see cref="CustomerStopLeft"/>.</summary>
        public float CustomerLeft { get; private set; }

        public static float CustomerStopPercent => CustomerStopLeft;

        /// <summary>How many passersby of the crowd are on the stage right now.</summary>
        public int VisibleCrowdCount
        {
            get
            {
                var count = 0;
                foreach (var walker in crowd)
                {
                    if (walker != null && walker.Element.style.display == DisplayStyle.Flex)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int VisibleCrowdRows(bool near)
        {
            var count = 0;
            foreach (var walker in crowd)
            {
                if (walker != null && walker.Near == near && walker.Element.style.display == DisplayStyle.Flex)
                {
                    count++;
                }
            }

            return count;
        }

        private void BuildCrowd()
        {
            for (var i = 0; i < crowd.Length; i++)
            {
                var near = i >= FarCount;
                var element = new VisualElement { name = near ? $"event-passerby-near-{i - FarCount}" : $"event-passerby-far-{i}" };
                element.AddToClassList(near ? "event-passerby-near" : "event-passerby-far");
                element.pickingMode = PickingMode.Ignore;
                stage.Add(element);
                var walker = new Passerby { Element = element, Near = near };
                crowd[i] = walker;
                Respawn(walker, true);
            }
        }

        /// <summary>Gives a walker a new look, direction and speed; at the start the walkers are spread over the stage.</summary>
        private void Respawn(Passerby walker, bool spread)
        {
            walker.Kind = crowdRandom.Next(3);
            walker.Dir = crowdRandom.Next(2) == 0 ? 1 : -1;
            walker.Speed = walker.Near ? 9f + (float)crowdRandom.NextDouble() * 7f : 5f + (float)crowdRandom.NextDouble() * 5f;
            walker.Gait = (float)crowdRandom.NextDouble() * 2f;
            var gap = (float)crowdRandom.NextDouble() * 30f;
            walker.Left = spread
                ? CrowdMinLeft + (float)crowdRandom.NextDouble() * (CrowdMaxLeft - CrowdMinLeft)
                : (walker.Dir > 0 ? CrowdMinLeft - gap : CrowdMaxLeft + gap);
            var baseTop = walker.Near ? 50f : 38f;
            walker.Element.style.top = Length.Percent(baseTop + (float)crowdRandom.NextDouble() * (walker.Near ? 3f : 4f) - 1f);
            walker.Shown = null;
            ApplyWalker(walker);
        }

        private void AdvanceCrowd(float deltaSeconds)
        {
            if (!dayShown || stage == null)
            {
                return;
            }

            var step = Mathf.Clamp(deltaSeconds, 0f, MaxCrowdStep);
            foreach (var walker in crowd)
            {
                walker.Left += walker.Dir * walker.Speed * step;
                walker.Gait += step * walker.Speed / 3.6f;
                if ((walker.Dir > 0 && walker.Left > CrowdMaxLeft) || (walker.Dir < 0 && walker.Left < CrowdMinLeft))
                {
                    Respawn(walker, false);
                }
                else
                {
                    ApplyWalker(walker);
                }
            }

            if (customerLeaving)
            {
                AdvanceCustomerLeaving(step);
            }
        }

        private void ApplyWalker(Passerby walker)
        {
            var picture = VisitorFrame(walker.Kind, (int)walker.Gait & 1);
            if (picture != walker.Shown)
            {
                walker.Shown = picture;
                walker.Element.style.backgroundImage = picture != null ? new StyleBackground(picture) : new StyleBackground(StyleKeyword.None);
            }

            if (walker.ShownDir != walker.Dir)
            {
                walker.ShownDir = walker.Dir;
                walker.Element.style.scale = new StyleScale(new Scale(new Vector3(walker.Dir < 0 ? -1f : 1f, 1f, 1f)));
            }

            walker.Element.style.left = Length.Percent(walker.Left);
            var onStage = walker.Left > CrowdMinLeft + 2f && walker.Left < CrowdMaxLeft - 2f;
            walker.Element.style.display = onStage ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void StartCustomerLeaving()
        {
            customerLeaving = true;
            customerLeaveLeft = CustomerStopLeft;
            customerLeaveGait = 0f;
            ShowCustomer(CustomerStopLeft, 0, true);
        }

        private void AdvanceCustomerLeaving(float step)
        {
            customerLeaveLeft += customerDir * CustomerWalkSpeed * step;
            customerLeaveGait += step / FrameSeconds;
            if ((customerDir > 0 && customerLeaveLeft > CrowdMaxLeft) || (customerDir < 0 && customerLeaveLeft < CrowdMinLeft))
            {
                customerLeaving = false;
                HideVisitor();
                return;
            }

            ShowCustomer(customerLeaveLeft, (int)customerLeaveGait & 1, true);
        }
    }
}
