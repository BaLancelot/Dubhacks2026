using SushiDojo.Effects;
using SushiDojo.Ingredients;
using UnityEngine;
using System;

namespace SushiDojo.Conveyor
{
    /// <summary>
    /// Covered: closed dome only.
    /// Revealed (at center): plate + ingredient + open lid floating above.
    /// Passed (left of center): plate + ingredient.
    /// </summary>
    public class BeltItem : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] SpriteRenderer ingredientRenderer;
        [SerializeField] SpriteRenderer coverRenderer;
        [SerializeField] SpriteRenderer plateRenderer;
        [Tooltip("Its prefab pose is the final open pose.")]
        [SerializeField] SpriteRenderer openLidRenderer;
        [SerializeField] Sprite fallbackIngredientSprite;

        [Tooltip("Longest side of the ingredient sprite is scaled to this size (world units).")]
        [SerializeField, Min(0.01f)] float ingredientFitSize = 1f;

        [Header("Reveal")]
        [SerializeField] RevealSparkle revealSparkle;
        [SerializeField, Min(0f)] float revealPopDuration = 0.25f;
        [Tooltip("Ingredient scale multiplier during the pop. Should end at 1.")]
        [SerializeField] AnimationCurve revealPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Lid Open")]
        [SerializeField, Min(0f)] float lidOpenDuration = 0.3f;
        [Tooltip("Lid starts at its prefab pose plus this offset, then moves to the prefab pose.")]
        [SerializeField] Vector2 lidStartOffset = new(-0.15f, -0.55f);
        [Tooltip("Extra Z rotation at the start of the open, in degrees.")]
        [SerializeField] float lidStartAngle = -15f;
        [Tooltip("0 = start pose, 1 = prefab pose. Values above 1 overshoot.")]
        [SerializeField] AnimationCurve lidOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public IngredientData Data { get; private set; }
        public bool IsRevealed { get; private set; }
        public bool IsTaken { get; private set; }

        private Action<BeltItem> onClicked;
        Vector3 ingredientBaseScale = Vector3.one;
        Vector3 lidRestPosition;
        Quaternion lidRestRotation;
        float revealTimer;
        bool isAnimatingReveal;

        void Awake()
        {
            // Resolve the layer by name in every player and keep the complete reveal
            // above BeltBody (Back / 2), including the plate and sparkle children.
            ApplySorting(plateRenderer, 3);
            ApplySorting(ingredientRenderer, 4);
            ApplySorting(coverRenderer, 5);
            ApplySorting(openLidRenderer, 6);
            if (revealSparkle != null)
            {
                foreach (SpriteRenderer sparkle in revealSparkle.GetComponentsInChildren<SpriteRenderer>(true))
                    ApplySorting(sparkle, 7);
            }

            Transform lid = openLidRenderer.transform;
            lidRestPosition = lid.localPosition;
            lidRestRotation = lid.localRotation;
        }

        static void ApplySorting(SpriteRenderer target, int order)
        {
            if (target == null) return;
            target.sortingLayerName = "Back";
            target.sortingOrder = order;
        }

        void Update()
        {
            if (!isAnimatingReveal) return;

            revealTimer += Time.deltaTime;
            float popProgress = Progress(revealTimer, revealPopDuration);
            float lidProgress = Progress(revealTimer, lidOpenDuration);

            if (!IsTaken)
                ingredientRenderer.transform.localScale = ingredientBaseScale * revealPopCurve.Evaluate(popProgress);
            ApplyLidPose(lidOpenCurve.Evaluate(lidProgress));

            if (popProgress >= 1f && lidProgress >= 1f) FinishRevealAnimation();
        }

        public void Init(IngredientData data, Action<BeltItem> clickHandler = null)
        {
            onClicked = clickHandler;
            Data = data;
            IsTaken = false;
            ingredientRenderer.sprite = data.Sprite != null ? data.Sprite : fallbackIngredientSprite;
            if (ingredientRenderer.sprite == null)
            {
                Debug.LogError(
                    $"{nameof(BeltItem)} needs a fallback sprite when '{data.DisplayName}' has no sprite.",
                    this);
                return;
            }
            FitIngredientToSize();
            Cover();
        }

        void OnMouseDown()
{
    if (IsRevealed && !IsTaken)
    {
        Debug.Log("Requesting ingredient claim!");
        onClicked?.Invoke(this);
    }
}
    
        public void Cover()
        {
            IsRevealed = false;
            SetVisible(cover: true, plate: false, ingredient: false, openLid: false);
            FinishRevealAnimation();
        }

        public void Reveal()
        {
            if (IsRevealed) return;

            IsRevealed = true;
            SetVisible(cover: false, plate: true, ingredient: true, openLid: true);

            revealTimer = 0f;
            isAnimatingReveal = true;
            ApplyLidPose(0f);
            if (revealSparkle != null) revealSparkle.Play();
        }

        /// <summary>Removes only the ingredient. Plate and lid stay on the belt.</summary>
        public void TakeIngredient()
        {
            IsTaken = true;
            ingredientRenderer.enabled = false;
            ingredientRenderer.transform.localScale = ingredientBaseScale;
        }

        /// <summary>Keeps the plate and ingredient visible but removes the open lid.</summary>
        public void LeaveCenter()
        {
            FinishRevealAnimation();
            openLidRenderer.enabled = false;
        }

        void SetVisible(bool cover, bool plate, bool ingredient, bool openLid)
{
    coverRenderer.enabled = cover;
    plateRenderer.enabled = plate;

    // Once taken, the ingredient must stay hidden.
    ingredientRenderer.enabled = ingredient && !IsTaken;

    openLidRenderer.enabled = openLid;
}

        void FinishRevealAnimation()
        {
            isAnimatingReveal = false;
            ingredientRenderer.transform.localScale = ingredientBaseScale;
            ApplyLidPose(1f);
        }

        void ApplyLidPose(float t)
        {
            Transform lid = openLidRenderer.transform;
            Vector3 startPosition = lidRestPosition + (Vector3)lidStartOffset;
            lid.localPosition = Vector3.LerpUnclamped(startPosition, lidRestPosition, t);
            lid.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(lidStartAngle, 0f, t)) * lidRestRotation;
        }

        static float Progress(float elapsed, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        }

        void FitIngredientToSize()
        {
            Sprite sprite = ingredientRenderer.sprite;
            if (sprite == null) return;

            Vector2 size = sprite.bounds.size;
            float longestSide = Mathf.Max(size.x, size.y);
            if (longestSide <= 0f) return;

            ingredientBaseScale = Vector3.one * (ingredientFitSize / longestSide);
            ingredientRenderer.transform.localScale = ingredientBaseScale;
        }
    }
}
