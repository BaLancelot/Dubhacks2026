using TMPro;
using UnityEngine;

namespace SushiDojo.UI
{
    /// <summary>
    /// Shows a player's score in a speech bubble's text. Counts up the same way the HUD did
    /// and pops the text each time the score changes. Shows Offline Text until the match scoreboard is live.
    /// With no Text assigned it uses the first TextMeshPro under this object (the one you placed in the bubble),
    /// and only makes its own at play time if there is none.
    /// Can also show a message for a while (ShowMessage) and play a penalty shake + red flash (PlayPenalty).
    /// </summary>
    public class BubbleScoreText : MonoBehaviour
    {
        enum ScoreOwner { Opponent, Local }

        const int NotShown = int.MinValue;
        const string CreatedTextName = "ScoreText";
        // Vertical shake runs at a different rate and half the size, so it reads as a rattle rather than a slide.
        const float VerticalShakeRate = 1.3f;
        const float VerticalShakePhase = 1f;
        const float VerticalShakeSize = 0.5f;

        [Tooltip("Empty = the first TextMeshPro under this object.")]
        [SerializeField] TMP_Text text;
        [Tooltip("Whose score to show.")]
        [SerializeField] ScoreOwner owner = ScoreOwner.Opponent;
        [Tooltip("{0} = the score.")]
        [SerializeField] string format = "{0}";
        [Tooltip("Shown before the networked scoreboard exists (e.g. offline testing).")]
        [SerializeField] string offlineText = "0";

        [Header("Pop")]
        [SerializeField, Min(1f)] float popScale = 1.3f;
        [SerializeField, Min(0.01f)] float popDuration = 0.25f;

        [Header("Penalty (shake + red flash)")]
        [SerializeField] Color penaltyColor = new(0.85f, 0.12f, 0.1f, 1f);
        [SerializeField, Min(0.01f)] float penaltyDuration = 0.8f;
        [Tooltip("Shake distance in the bubble's parent space (canvas units for UI, world units for world bubbles).")]
        [SerializeField, Min(0f)] float shakeStrength = 12f;
        [Tooltip("Shake speed, radians per second.")]
        [SerializeField, Min(0f)] float shakeSpeed = 55f;

        [Header("Auto Text (only when the bubble has no TextMeshPro under it)")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField, Min(0.1f)] float fontSize = 8f;
        [SerializeField] Color textColor = new(0.3f, 0.17f, 0.1f, 1f);
        [Tooltip("Pulls the text toward the camera so it draws over the bubble.")]
        [SerializeField] float towardCamera = -0.01f;

        NetworkScoreboard scoreboard;
        Vector3 baseScale = Vector3.one;
        Color baseColor;
        int shownScore = NotShown;
        float popTimer;
        float messageTimer;
        float penaltyTimer;
        Vector3 shakeOrigin;

        public bool ShowsLocalScore => owner == ScoreOwner.Local;

        void Awake()
        {
            if (text == null) text = GetComponentInChildren<TMP_Text>(true);
            if (text == null) text = CreateWorldText();
            if (text == null) return;

            baseScale = text.transform.localScale;
            baseColor = text.color;

            if (TryGetComponent(out UISpeechBubble uiBubble) && uiBubble.FitText == null) uiBubble.SetFitText(text);
            if (!TryGetComponent(out SpeechBubble bubble)) return;

            // The bubble grows around this text unless it was told to fit something else.
            if (bubble.FitText == null) bubble.SetFitText(text);

            // Keep the text drawn over the bubble.
            if (text is TextMeshPro worldText && SortsBelow(worldText, bubble))
            {
                worldText.sortingLayerID = SortingLayer.NameToID(bubble.SortingLayerName);
                worldText.sortingOrder = bubble.SortingOrder + 1;
            }
        }

        static bool SortsBelow(TextMeshPro worldText, SpeechBubble bubble)
        {
            int textLayer = SortingLayer.GetLayerValueFromID(worldText.sortingLayerID);
            int bubbleLayer = SortingLayer.GetLayerValueFromName(bubble.SortingLayerName);
            return textLayer < bubbleLayer || (textLayer == bubbleLayer && worldText.sortingOrder <= bubble.SortingOrder);
        }

        /// <summary>Shows a message instead of the score for a while, then goes back to the score.</summary>
        public void ShowMessage(string message, float duration)
        {
            if (text == null) return;
            text.text = message;
            messageTimer = duration;
            popTimer = popDuration;
        }

        /// <summary>Shakes the bubble and flashes the text red; the score itself drops through the scoreboard.</summary>
        public void PlayPenalty()
        {
            if (penaltyTimer <= 0f) shakeOrigin = transform.localPosition;
            penaltyTimer = penaltyDuration;
        }

        void Update()
        {
            if (text == null) return;

            UpdatePenalty();
            UpdatePop();

            if (messageTimer > 0f)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer > 0f) return;
                shownScore = NotShown; // redraw the score without a pop
                text.text = offlineText;
            }

            if (scoreboard == null) scoreboard = FindFirstObjectByType<NetworkScoreboard>();
            bool isLive = scoreboard != null && scoreboard.IsSpawned;

            if (!isLive)
            {
                if (shownScore != NotShown || text.text != offlineText) text.text = offlineText;
                shownScore = NotShown;
            }
            else
            {
                int score = owner == ScoreOwner.Local ? scoreboard.DisplayedLocalScore : scoreboard.DisplayedOpponentScore;
                if (score != shownScore)
                {
                    if (shownScore != NotShown) popTimer = popDuration;
                    shownScore = score;
                    text.text = string.Format(format, score);
                }
            }
        }

        void UpdatePenalty()
        {
            if (penaltyTimer <= 0f) return;

            penaltyTimer = Mathf.Max(penaltyTimer - Time.unscaledDeltaTime, 0f);
            float strength = penaltyTimer / penaltyDuration; // 1 -> 0: strongest at the hit, then settles
            float elapsed = penaltyDuration - penaltyTimer;

            Vector3 shake = new(
                Mathf.Sin(elapsed * shakeSpeed),
                Mathf.Sin(elapsed * shakeSpeed * VerticalShakeRate + VerticalShakePhase) * VerticalShakeSize,
                0f);
            transform.localPosition = shakeOrigin + shake * (shakeStrength * strength);
            text.color = Color.Lerp(baseColor, penaltyColor, strength);

            if (penaltyTimer > 0f) return;
            transform.localPosition = shakeOrigin;
            text.color = baseColor;
        }

        void UpdatePop()
        {
            if (popTimer <= 0f)
            {
                text.transform.localScale = baseScale;
                return;
            }

            popTimer = Mathf.Max(popTimer - Time.unscaledDeltaTime, 0f);
            float bump = Mathf.Sin((1f - popTimer / popDuration) * Mathf.PI);
            text.transform.localScale = baseScale * Mathf.Lerp(1f, popScale, bump);
        }

        /// <summary>A TextMeshPro child sized and sorted for the world SpeechBubble on this object.</summary>
        TMP_Text CreateWorldText()
        {
            if (!TryGetComponent(out SpeechBubble bubble)) return null;

            var go = new GameObject(CreatedTextName);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, towardCamera);

            TextMeshPro created = go.AddComponent<TextMeshPro>();
            if (font != null) created.font = font;
            created.text = offlineText;
            created.fontSize = fontSize;
            created.color = textColor;
            created.alignment = TextAlignmentOptions.Center;
            created.sortingLayerID = SortingLayer.NameToID(bubble.SortingLayerName);
            created.sortingOrder = bubble.SortingOrder + 1;

            bubble.SetFitText(created);
            return created;
        }
    }
}
