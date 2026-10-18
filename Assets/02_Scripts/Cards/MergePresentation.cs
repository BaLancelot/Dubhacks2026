using System.Collections;
using System.Collections.Generic;
using SushiDojo.Board;
using SushiDojo.Effects;
using UnityEngine;

namespace SushiDojo.Cards
{
    /// <summary>
    /// Plays the "cooking" moment when a recipe is matched: a dust cloud covers the board,
    /// the ingredients vanish under it, and the finished dish pops out ("ta-da").
    /// RecipeQueueView waits on Play() before moving the recipe card on.
    /// </summary>
    public class MergePresentation : MonoBehaviour
    {
        [Tooltip("Falls back to a DustCloud on this object.")]
        [SerializeField] DustCloud dustCloud;
        [Tooltip("Falls back to the one in the scene.")]
        [SerializeField] IngredientPlacementZone placementZone;
        [Tooltip("Shows the finished dish lying on the board. Its sprite is set per recipe.")]
        [SerializeField] SpriteRenderer dishPrefab;

        [Header("Dish")]
        [Tooltip("Height above the board's center, along the board's up (board local units).")]
        [SerializeField] float dishHeight = 0.2f;
        [Tooltip("Longest side of the dish sprite, measured along the board's width (board local units). The sprite keeps its own aspect even on a stretched board.")]
        [SerializeField, Min(0.01f)] float dishSize = 3.2f;
        [Tooltip("Scale multiplier over the pop. Should end at 1.")]
        [SerializeField] AnimationCurve dishPopCurve = new(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.6f, 1.2f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField, Min(0.01f)] float dishPopDuration = 0.35f;
        [Tooltip("How long the dish stays on the board once revealed.")]
        [SerializeField, Min(0f)] float dishShowDuration = 1.6f;
        [SerializeField, Min(0.01f)] float dishHideDuration = 0.25f;

        [Header("Timing")]
        [Tooltip("Extra wait after the ta-da before the recipe card reacts.")]
        [SerializeField, Min(0f)] float cardReactDelay = 0.15f;

        void Awake()
        {
            if (dustCloud == null) dustCloud = GetComponent<DustCloud>();
            if (placementZone == null) placementZone = FindFirstObjectByType<IngredientPlacementZone>();
        }

        /// <summary>Runs until the dish has been revealed. The dish then lingers and leaves on its own.</summary>
        public IEnumerator Play(RecipeData recipe)
        {
            List<BoardIngredient> ingredients = placementZone != null
                ? placementZone.TakePlacedIngredients()
                : new List<BoardIngredient>();
            Transform board = placementZone != null ? placementZone.Board : null;

            if (dustCloud == null)
            {
                RemoveIngredients(ingredients);
                StartCoroutine(ShowDish(recipe, board));
                yield break;
            }

            bool covered = false;
            bool revealed = false;
            void HandleCovered() => covered = true;
            void HandleRevealed() => revealed = true;

            dustCloud.OnCovered += HandleCovered;
            dustCloud.OnSparkle += HandleRevealed;
            dustCloud.OnFinished += HandleRevealed;
            dustCloud.PlayOver(board != null ? board.position : transform.position);

            while (!covered) yield return null;
            // Swap under the cloud: ingredients out, dish in.
            RemoveIngredients(ingredients);
            StartCoroutine(ShowDish(recipe, board));

            while (!revealed) yield return null;
            dustCloud.OnCovered -= HandleCovered;
            dustCloud.OnSparkle -= HandleRevealed;
            dustCloud.OnFinished -= HandleRevealed;

            if (cardReactDelay > 0f) yield return new WaitForSeconds(cardReactDelay);
        }

        static void RemoveIngredients(List<BoardIngredient> ingredients)
        {
            foreach (BoardIngredient ingredient in ingredients)
                if (ingredient != null) Destroy(ingredient.gameObject);
        }

        IEnumerator ShowDish(RecipeData recipe, Transform board)
        {
            if (dishPrefab == null || recipe == null || recipe.FoodSprite == null) yield break;

            SpriteRenderer dish = Instantiate(dishPrefab, board);
            dish.sprite = recipe.FoodSprite;
            Transform dishTransform = dish.transform;
            if (board != null)
            {
                // Lies flat on the board, the same way placed ingredients do.
                dishTransform.localPosition = Vector3.up * dishHeight;
                dishTransform.localRotation = Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            }
            else
            {
                dishTransform.position = transform.position;
            }

            Vector3 baseScale = UniformDishScale(recipe.FoodSprite, board);

            for (float t = 0f; t < dishPopDuration; t += Time.deltaTime)
            {
                if (dish == null) yield break;
                dishTransform.localScale = baseScale * dishPopCurve.Evaluate(t / dishPopDuration);
                yield return null;
            }
            dishTransform.localScale = baseScale;

            if (dishShowDuration > 0f) yield return new WaitForSeconds(dishShowDuration);

            for (float t = 0f; t < dishHideDuration; t += Time.deltaTime)
            {
                if (dish == null) yield break;
                dishTransform.localScale = baseScale * (1f - t / dishHideDuration);
                yield return null;
            }
            if (dish != null) Destroy(dish.gameObject);
        }

        /// <summary>
        /// Local scale that gives the sprite the same world scale on both axes, undoing the board's own
        /// (possibly non-uniform) scale. Lying flat, the dish's X/Y/Z map to the board's X/Z/Y.
        /// </summary>
        Vector3 UniformDishScale(Sprite sprite, Transform board)
        {
            Vector2 spriteSize = sprite.bounds.size;
            float longestSide = Mathf.Max(spriteSize.x, spriteSize.y, Mathf.Epsilon);
            if (board == null) return Vector3.one * (dishSize / longestSide);

            Vector3 boardScale = board.lossyScale;
            float worldScale = dishSize * Mathf.Abs(boardScale.x) / longestSide;
            return new Vector3(
                worldScale / Mathf.Max(Mathf.Abs(boardScale.x), Mathf.Epsilon),
                worldScale / Mathf.Max(Mathf.Abs(boardScale.z), Mathf.Epsilon),
                worldScale / Mathf.Max(Mathf.Abs(boardScale.y), Mathf.Epsilon));
        }
    }
}
