using TMPro;
using UnityEngine;

namespace SushiDojo.UI
{
    /// <summary>Sizes a bubble's text to its content so the bubble grows around it (not the other way round).</summary>
    public static class BubbleTextFit
    {
        /// <summary>
        /// Turns off auto-size (which would shrink the text into its box), measures the text at its own font size,
        /// sets the text box to that size and returns it. maxWidth 0 = no wrapping.
        /// </summary>
        public static Vector2 Fit(TMP_Text text, float maxWidth)
        {
            if (text.enableAutoSizing) text.enableAutoSizing = false;

            bool wraps = maxWidth > 0f;
            TextWrappingModes wrapping = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            if (text.textWrappingMode != wrapping) text.textWrappingMode = wrapping;

            Vector2 preferred = text.GetPreferredValues(text.text, wraps ? maxWidth : float.PositiveInfinity, float.PositiveInfinity);
            if (wraps) preferred.x = Mathf.Min(preferred.x, maxWidth);

            if (text.rectTransform.sizeDelta != preferred) text.rectTransform.sizeDelta = preferred;
            return preferred;
        }
    }
}
