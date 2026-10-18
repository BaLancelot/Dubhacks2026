using SushiDojo.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Board
{
    /// <summary>
    /// Tutorial nudge: when the ingredients on the board can't make any visible recipe, a see-through hand
    /// flips between open and grabbing over the board to hint "grab it and throw".
    /// Once the player throws the board one time, it never shows again (for this run of the game).
    /// </summary>
    [RequireComponent(typeof(ThrowableBody))]
    public class ThrowHint : MonoBehaviour
    {
        [SerializeField] Sprite openHand;
        [SerializeField] Sprite grabHand;
        [Tooltip("Seconds each hand frame stays up.")]
        [SerializeField, Min(0.05f)] float frameDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] float opacity = 0.55f;
        [Tooltip("Hand height on screen (reference pixels at 1920x1080).")]
        [SerializeField, Min(1f)] float handSize = 150f;
        [Tooltip("Offset from the board's top-right corner on screen (reference pixels). The hand is centered there.")]
        [SerializeField] Vector2 screenOffset = new(-60f, -50f);
        [SerializeField, Min(0.01f)] float fadeDuration = 0.25f;

        static bool learned;

        ThrowableBody board;
        Renderer boardRenderer;
        RecipeQueueView recipes;
        Image hand;
        RectTransform handRect;
        RectTransform canvasRect;
        float alpha;
        float nextFindTime;

        void Awake()
        {
            board = GetComponent<ThrowableBody>();
            boardRenderer = GetComponent<Renderer>();
            CreateHand();
        }

        void OnEnable() => board.Released += HandleReleased;
        void OnDisable() => board.Released -= HandleReleased;

        void OnDestroy()
        {
            if (canvasRect != null) Destroy(canvasRect.gameObject);
        }

        void HandleReleased() => learned = true;

        void LateUpdate()
        {
            // The recipe queue lives in the gameplay UI, which can spawn after the board.
            if (recipes == null && Time.unscaledTime >= nextFindTime)
            {
                recipes = FindFirstObjectByType<RecipeQueueView>();
                nextFindTime = Time.unscaledTime + 1f;
            }

            bool show = !learned && !board.IsHeld && recipes != null && recipes.IsBoardHopeless;
            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, Time.deltaTime / fadeDuration);
            hand.enabled = alpha > 0f;
            if (!hand.enabled) return;

            bool grabbing = Mathf.FloorToInt(Time.time / frameDuration) % 2 == 1;
            Sprite sprite = grabbing ? grabHand : openHand;
            if (sprite != null && hand.sprite != sprite)
            {
                hand.sprite = sprite;
                Vector2 size = sprite.rect.size;
                handRect.sizeDelta = new Vector2(handSize * size.x / size.y, handSize);
            }
            hand.color = new Color(1f, 1f, 1f, opacity * alpha);

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 screen = TopRightOnScreen(cam);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                handRect.anchoredPosition = local + screenOffset;
        }

        /// <summary>Top-right corner of the board as it appears on screen (max x and y of its projected bounds).</summary>
        Vector2 TopRightOnScreen(Camera cam)
        {
            if (boardRenderer == null) return cam.WorldToScreenPoint(transform.position);

            Bounds b = boardRenderer.bounds;
            Vector2 max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 p = cam.WorldToScreenPoint(corner);
                max = Vector2.Max(max, p);
            }
            return max;
        }

        void CreateHand()
        {
            var canvasObject = new GameObject("ThrowHintCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the gameplay UI, below the hand cursor (999).
            canvas.sortingOrder = 500;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)canvasObject.transform;

            var handObject = new GameObject("ThrowHintHand", typeof(RectTransform), typeof(Image));
            handRect = (RectTransform)handObject.transform;
            handRect.SetParent(canvasRect, false);
            handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0.5f);
            hand = handObject.GetComponent<Image>();
            hand.raycastTarget = false;
            hand.preserveAspect = true;
            hand.enabled = false;
        }
    }
}
