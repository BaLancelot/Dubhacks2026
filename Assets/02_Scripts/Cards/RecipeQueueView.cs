using System;
using System.Collections;
using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Cards
{
    [RequireComponent(typeof(RectTransform))]
    public class RecipeQueueView : MonoBehaviour
    {
        [SerializeField] RecipeCardUI cardPrefab;
        [SerializeField] RecipeData[] recipeDeck = Array.Empty<RecipeData>();
        [SerializeField, Min(0f)] float cardSpacing = 160f;
        [SerializeField, Range(0.1f, 1f)] float cardScale = 0.65f;
        [SerializeField, Min(0.01f)] float animationDuration = 0.35f;
        [SerializeField, Min(0f)] float exitDistance = 190f;

        [Header("Completion")]
        [Tooltip("Cooking effect on the board. The card waits for it. Empty = finds one in the scene; none = old instant behavior.")]
        [SerializeField] MergePresentation mergePresentation;
        [Tooltip("Card scale peak during the 'pop' once the dish is done (multiplier).")]
        [SerializeField, Min(1f)] float cardPopScale = 1.15f;
        [SerializeField, Min(0.01f)] float cardPopDuration = 0.2f;
        [Tooltip("The completed card fades away over this long before the next card moves up.")]
        [SerializeField, Min(0.01f)] float cardFadeDuration = 0.45f;

        RecipeCardUI topCard;
        RecipeCardUI nextCard;
        [SerializeField] IngredientPlacementZone placementZone;
        readonly List<IngredientData> submittedIngredients = new();
        int currentIndex;
        bool isAnimating;

        public event Action<RecipeData> RecipeCompleted;

        public RecipeData CurrentRecipe =>
            currentIndex < recipeDeck.Length ? recipeDeck[currentIndex] : null;

        void Awake()
        {
            Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }

            if (recipeDeck == null)
            {
                Debug.LogError($"{nameof(RecipeQueueView)} requires a recipe deck.", this);
                enabled = false;
                return;
            }

            if (cardPrefab == null)
            {
                Debug.LogError($"{nameof(RecipeQueueView)} requires a recipe card prefab.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < recipeDeck.Length; i++)
            {
                if (recipeDeck[i] != null) continue;

                Debug.LogError($"{nameof(RecipeQueueView)} has an empty recipe deck entry at index {i}.", this);
                enabled = false;
                return;
            }

            recipeDeck = (RecipeData[])recipeDeck.Clone();
            ShuffleRecipeDeck();
            topCard = Instantiate(cardPrefab, transform, false);
            nextCard = Instantiate(cardPrefab, transform, false);
            PositionCard(topCard, TopSlotPosition);
            PositionCard(nextCard, NextSlotPosition);
            RefreshCards();
            Debug.Log($"[RecipeQueue] Ready in {gameObject.scene.name}; recipes={recipeDeck.Length}.", this);
        }

        void ShuffleRecipeDeck()
        {
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = recipeDeck.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (recipeDeck[i], recipeDeck[swapIndex]) =
                    (recipeDeck[swapIndex], recipeDeck[i]);
            }
        }

        void OnEnable()
        {
            if (mergePresentation == null)
                mergePresentation = FindFirstObjectByType<MergePresentation>();
            if (placementZone == null)
                placementZone = FindFirstObjectByType<IngredientPlacementZone>();
            if (placementZone == null)
            {
                Debug.LogWarning(
                    $"{nameof(RecipeQueueView)} could not find an ingredient placement zone; " +
                    "recipes can still be completed by calling CompleteTopRecipe().",
                    this);
                return;
            }

            placementZone.AcceptingIngredients = CurrentRecipe != null && !isAnimating;
            placementZone.IngredientPlaced += OnIngredientPlaced;
            placementZone.IngredientsDiscarded += OnIngredientsDiscarded;
        }

        void OnDisable()
        {
            if (placementZone != null)
            {
                placementZone.IngredientPlaced -= OnIngredientPlaced;
                placementZone.IngredientsDiscarded -= OnIngredientsDiscarded;
            }
            submittedIngredients.Clear();
        }

        public void CompleteTopRecipe()
        {
            if (!enabled || isAnimating || currentIndex >= recipeDeck.Length) return;
            // The board ingredients are handed to the merge effect (or cleared) inside the routine.
            submittedIngredients.Clear();
            StartCoroutine(CompleteTopRecipeRoutine());
        }

        void OnIngredientPlaced(IngredientData ingredient)
        {
            if (CurrentRecipe == null || placementZone == null) return;

            if (isAnimating) return;

            submittedIngredients.Add(ingredient);
            RecipeData secondRecipe = currentIndex + 1 < recipeDeck.Length
                ? recipeDeck[currentIndex + 1] : null;
            if (MatchesRecipe(CurrentRecipe))
            {
                CompleteTopRecipe();
                return;
            }
            if (secondRecipe != null && MatchesRecipe(secondRecipe))
            {
                submittedIngredients.Clear();
                StartCoroutine(CompleteSecondRecipeRoutine());
                return;
            }

        }

        void OnIngredientsDiscarded(int discardedIngredientCount)
        {
            submittedIngredients.Clear();
            NetworkScoreboard scoreboard = FindFirstObjectByType<NetworkScoreboard>();
            if (scoreboard != null)
                scoreboard.RecordDiscardedIngredients(discardedIngredientCount);
        }

        bool MatchesRecipe(RecipeData recipe) =>
            submittedIngredients.Count == CountRequiredIngredients(recipe) && CanCompleteRecipe(recipe);

        bool CanCompleteRecipe(RecipeData recipe)
        {
            bool[] matched = new bool[recipe.Ingredients.Count];
            foreach (IngredientData submitted in submittedIngredients)
            {
                int matchIndex = -1;
                for (int i = 0; i < recipe.Ingredients.Count; i++)
                {
                    if (matched[i] || recipe.Ingredients[i] == null || recipe.Ingredients[i] != submitted) continue;
                    matchIndex = i;
                    break;
                }
                if (matchIndex < 0) return false;
                matched[matchIndex] = true;
            }
            return true;
        }

        IEnumerator CompleteSecondRecipeRoutine()
        {
            isAnimating = true;
            if (placementZone != null) placementZone.AcceptingIngredients = false;
            RecipeData completedRecipe = recipeDeck[currentIndex + 1];
            yield return CookOnBoard(completedRecipe);
            yield return PopAndFade(nextCard);

            // Remove only the second recipe; the first remains available.
            for (int i = currentIndex + 1; i < recipeDeck.Length - 1; i++)
                recipeDeck[i] = recipeDeck[i + 1];
            Array.Resize(ref recipeDeck, recipeDeck.Length - 1);
            RefreshCards();
            if (currentIndex + 1 < recipeDeck.Length)
            {
                PositionCard(nextCard, NextSlotPosition + Vector2.right * exitDistance);
                yield return AnimateCards((RectTransform)nextCard.transform,
                    NextSlotPosition, Vector3.one * cardScale, null, Vector2.zero, Vector3.one);
            }
            isAnimating = false;
            if (placementZone != null) placementZone.AcceptingIngredients = CurrentRecipe != null;
            NotifyRecipeCompleted(completedRecipe);
        }

        static int CountRequiredIngredients(RecipeData recipe)
        {
            int count = 0;
            foreach (IngredientData ingredient in recipe.Ingredients)
            {
                if (ingredient != null) count++;
            }

            return count;
        }

        void NotifyRecipeCompleted(RecipeData recipe)
        {
            RecipeCompleted?.Invoke(recipe);
            NetworkScoreboard scoreboard = FindFirstObjectByType<NetworkScoreboard>();
            if (scoreboard != null)
                scoreboard.RecordRecipeCompleted(CountRequiredIngredients(recipe));
        }

        IEnumerator CompleteTopRecipeRoutine()
        {
            isAnimating = true;
            if (placementZone != null) placementZone.AcceptingIngredients = false;
            RecipeData completedRecipe = recipeDeck[currentIndex];
            RectTransform topRect = (RectTransform)topCard.transform;
            RectTransform nextRect = currentIndex + 1 < recipeDeck.Length
                ? (RectTransform)nextCard.transform
                : null;

            // Cook first, then the finished card pops and fades, then the next card moves up.
            yield return CookOnBoard(completedRecipe);
            yield return PopAndFade(topCard);
            if (nextRect != null)
                yield return AnimateCards(nextRect, TopSlotPosition, Vector3.one * cardScale, null, Vector2.zero, Vector3.one);
            topRect.anchoredPosition = TopSlotPosition + Vector2.left * exitDistance;

            currentIndex++;
            RecipeCardUI oldTopCard = topCard;
            topCard = nextCard;
            nextCard = oldTopCard;

            if (currentIndex < recipeDeck.Length)
            {
                PositionCard(topCard, TopSlotPosition);
                if (currentIndex + 1 < recipeDeck.Length)
                {
                    nextCard.SetData(recipeDeck[currentIndex + 1]);
                    nextCard.gameObject.SetActive(true);
                    PositionCard(nextCard, NextSlotPosition + Vector2.right * exitDistance);
                    yield return AnimateCards(
                        (RectTransform)nextCard.transform,
                        NextSlotPosition,
                        Vector3.one * cardScale,
                        null,
                        Vector2.zero,
                        Vector3.one);
                }
                else
                {
                    nextCard.SetData(null);
                    nextCard.gameObject.SetActive(false);
                }
            }
            else
            {
                topCard.gameObject.SetActive(false);
                nextCard.gameObject.SetActive(false);
            }

            isAnimating = false;
            if (placementZone != null) placementZone.AcceptingIngredients = CurrentRecipe != null;
            NotifyRecipeCompleted(completedRecipe);
        }

        /// <summary>Plays the board cooking effect, or just clears the board when there is none.</summary>
        IEnumerator CookOnBoard(RecipeData recipe)
        {
            if (mergePresentation != null)
                yield return mergePresentation.Play(recipe);
            else if (placementZone != null)
                placementZone.ConsumePlacedIngredients();
        }

        /// <summary>The completed card bounces once, then fades out where it is.</summary>
        IEnumerator PopAndFade(RecipeCardUI card)
        {
            RectTransform rect = (RectTransform)card.transform;
            CanvasGroup group = CardGroup(card);
            Vector3 baseScale = rect.localScale;

            for (float t = 0f; t < cardPopDuration; t += Time.unscaledDeltaTime)
            {
                float bump = Mathf.Sin(t / cardPopDuration * Mathf.PI);
                rect.localScale = baseScale * Mathf.Lerp(1f, cardPopScale, bump);
                yield return null;
            }
            rect.localScale = baseScale;

            for (float t = 0f; t < cardFadeDuration; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / cardFadeDuration);
                yield return null;
            }
            group.alpha = 0f;
        }

        static CanvasGroup CardGroup(RecipeCardUI card) =>
            card.TryGetComponent(out CanvasGroup group) ? group : card.gameObject.AddComponent<CanvasGroup>();

        IEnumerator AnimateCards(
            RectTransform first,
            Vector2 firstTarget,
            Vector3 firstScaleTarget,
            RectTransform second,
            Vector2 secondTarget,
            Vector3 secondScaleTarget)
        {
            Vector2 firstStart = first.anchoredPosition;
            Vector3 firstScaleStart = first.localScale;
            Vector2 secondStart = second != null ? second.anchoredPosition : Vector2.zero;
            Vector3 secondScaleStart = second != null ? second.localScale : Vector3.one;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, animationDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                first.anchoredPosition = Vector2.LerpUnclamped(firstStart, firstTarget, progress);
                first.localScale = Vector3.LerpUnclamped(firstScaleStart, firstScaleTarget, progress);

                if (second != null)
                {
                    second.anchoredPosition = Vector2.LerpUnclamped(secondStart, secondTarget, progress);
                    second.localScale = Vector3.LerpUnclamped(secondScaleStart, secondScaleTarget, progress);
                }

                yield return null;
            }

            first.anchoredPosition = firstTarget;
            first.localScale = firstScaleTarget;
            if (second != null)
            {
                second.anchoredPosition = secondTarget;
                second.localScale = secondScaleTarget;
            }
        }

        void RefreshCards()
        {
            topCard.SetData(CurrentRecipe);
            topCard.gameObject.SetActive(CurrentRecipe != null);

            RecipeData nextRecipe = currentIndex + 1 < recipeDeck.Length
                ? recipeDeck[currentIndex + 1]
                : null;
            nextCard.SetData(nextRecipe);
            nextCard.gameObject.SetActive(nextRecipe != null);
        }

        void PositionCard(RecipeCardUI card, Vector2 position)
        {
            RectTransform cardRect = (RectTransform)card.transform;
            cardRect.anchoredPosition = position;
            cardRect.localScale = Vector3.one * cardScale;
            // A card that was faded out after completion comes back visible when reused.
            CardGroup(card).alpha = 1f;
        }

        Vector2 TopSlotPosition => new(-cardSpacing * 0.5f, 0f);
        Vector2 NextSlotPosition => new(cardSpacing * 0.5f, 0f);
    }
}
