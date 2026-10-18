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

        RecipeCardUI topCard;
        RecipeCardUI nextCard;
        IngredientPlacementZone placementZone;
        readonly List<IngredientData> submittedIngredients = new();
        int currentIndex;
        bool isAnimating;

        public event Action<RecipeData> RecipeCompleted;

        public RecipeData CurrentRecipe =>
            currentIndex < recipeDeck.Length ? recipeDeck[currentIndex] : null;

        void Awake()
        {
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

            topCard = Instantiate(cardPrefab, transform, false);
            nextCard = Instantiate(cardPrefab, transform, false);
            PositionCard(topCard, TopSlotPosition);
            PositionCard(nextCard, NextSlotPosition);
            RefreshCards();
        }

        void OnEnable()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            placementZone = canvas != null
                ? canvas.GetComponentInChildren<IngredientPlacementZone>(true)
                : null;
            if (placementZone == null)
            {
                Debug.LogWarning(
                    $"{nameof(RecipeQueueView)} could not find an ingredient placement zone; " +
                    "recipes can still be completed by calling CompleteTopRecipe().",
                    this);
                return;
            }

            placementZone.IngredientPlaced += OnIngredientPlaced;
        }

        void OnDisable()
        {
            if (placementZone != null)
                placementZone.IngredientPlaced -= OnIngredientPlaced;
            submittedIngredients.Clear();
        }

        public void CompleteTopRecipe()
        {
            if (!enabled || isAnimating || currentIndex >= recipeDeck.Length) return;
            ClearSubmittedIngredients();
            StartCoroutine(CompleteTopRecipeRoutine());
        }

        void OnIngredientPlaced(IngredientData ingredient)
        {
            if (CurrentRecipe == null || placementZone == null) return;

            if (isAnimating)
            {
                Debug.LogWarning("Wait for the recipe queue animation to finish before placing ingredients.", this);
                ClearSubmittedIngredients();
                return;
            }

            submittedIngredients.Add(ingredient);
            int requiredIngredientCount = CountRequiredIngredients(CurrentRecipe);
            if (submittedIngredients.Count < requiredIngredientCount) return;

            if (MatchesRecipe(CurrentRecipe))
            {
                CompleteTopRecipe();
                return;
            }

            Debug.LogWarning(
                $"Ingredients do not match {CurrentRecipe.DisplayName}; cleared the placement zone.",
                this);
            ClearSubmittedIngredients();
        }

        bool MatchesRecipe(RecipeData recipe)
        {
            if (submittedIngredients.Count != CountRequiredIngredients(recipe)) return false;

            bool[] matched = new bool[submittedIngredients.Count];
            foreach (IngredientData requiredIngredient in recipe.Ingredients)
            {
                if (requiredIngredient == null) continue;

                int matchIndex = -1;
                for (int i = 0; i < submittedIngredients.Count; i++)
                {
                    if (matched[i] || submittedIngredients[i] != requiredIngredient) continue;
                    matchIndex = i;
                    break;
                }

                if (matchIndex < 0) return false;
                matched[matchIndex] = true;
            }

            return true;
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

        void ClearSubmittedIngredients()
        {
            submittedIngredients.Clear();
            if (placementZone != null)
                placementZone.ClearPlacedIngredients();
        }

        IEnumerator CompleteTopRecipeRoutine()
        {
            isAnimating = true;
            RecipeData completedRecipe = recipeDeck[currentIndex];
            RectTransform topRect = (RectTransform)topCard.transform;
            RectTransform nextRect = currentIndex + 1 < recipeDeck.Length
                ? (RectTransform)nextCard.transform
                : null;

            yield return AnimateCards(
                topRect,
                TopSlotPosition + Vector2.left * exitDistance,
                Vector3.one * (cardScale * 0.8f),
                nextRect,
                TopSlotPosition,
                Vector3.one * cardScale);

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
            RecipeCompleted?.Invoke(completedRecipe);
        }

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
        }

        Vector2 TopSlotPosition => new(-cardSpacing * 0.5f, 0f);
        Vector2 NextSlotPosition => new(cardSpacing * 0.5f, 0f);
    }
}
