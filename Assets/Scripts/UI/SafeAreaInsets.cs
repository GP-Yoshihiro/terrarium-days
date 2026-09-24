using UnityEngine;

namespace TerrariumDays.UI
{
    /// <summary>
    /// Converts <see cref="Screen.safeArea"/> (screen pixels, origin bottom-left) into
    /// padding for a UI Toolkit panel (panel units, origin top-left), so content stays clear
    /// of the notch/Dynamic Island and the home indicator. Pure so it can be EditMode tested.
    /// </summary>
    public readonly struct SafeAreaInsets
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public SafeAreaInsets(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public static SafeAreaInsets Compute(Rect safeArea, Vector2 screenSize, Vector2 panelSize)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                return default;
            }

            float scaleX = panelSize.x / screenSize.x;
            float scaleY = panelSize.y / screenSize.y;

            return new SafeAreaInsets(
                Mathf.Max(0f, safeArea.xMin) * scaleX,
                Mathf.Max(0f, screenSize.y - safeArea.yMax) * scaleY,
                Mathf.Max(0f, screenSize.x - safeArea.xMax) * scaleX,
                Mathf.Max(0f, safeArea.yMin) * scaleY);
        }
    }
}
