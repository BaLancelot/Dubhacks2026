using TMPro;
using UnityEngine;

namespace SushiDojo.UI
{
    /// <summary>
    /// Shows a player's score in a speech bubble's text. Counts up the same way the HUD did
    /// and pops the text each time the score changes. Shows Offline Text until the match scoreboard is live.
    /// On a world SpeechBubble with no text assigned, it makes its own TextMeshPro child at play time.
    /// </summary>
    public class BubbleScoreText : MonoBehaviour
    {
        enum ScoreOwner { Opponent, Local }

        const int NotShown = int.MinValue;
        const string CreatedTextName = "ScoreText";

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

        [Header("Auto Text (world bubble without Text)")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField, Min(0.1f)] float fontSize = 8f;
        [SerializeField] Color textColor = new(0.3f, 0.17f, 0.1f, 1f);
        [Tooltip("Pulls the text toward the camera so it draws over the bubble.")]
        [SerializeField] float towardCamera = -0.01f;

        NetworkScoreboard scoreboard;
        Vector3 baseScale = Vector3.one;
        int shownScore = NotShown;
        float popTimer;

        void Awake()
        {
            if (text == null) text = CreateWorldText();
            if (text != null) baseScale = text.transform.localScale;
        }

        void Update()
        {
            if (text == null) return;

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

            UpdatePop();
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
