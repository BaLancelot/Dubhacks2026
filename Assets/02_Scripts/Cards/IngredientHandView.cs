using System;
using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiDojo.Cards
{
    public class IngredientHandView : MonoBehaviour
    {
        [SerializeField] IngredientCardUI cardPrefab;
        [SerializeField] IngredientData[] startingCards = Array.Empty<IngredientData>();
        [SerializeField, Min(0f)] float cardSpacing = 80f;
        [SerializeField, Min(0f)] float fanDrop = 42f;
        [SerializeField, Min(0f)] float fanAngle = 8f;
        [SerializeField, Min(0f)] float hoverLift = 32f;
        [SerializeField, Min(1f)] float hoverScale = 1.1f;
        [SerializeField, Min(0f)] float hoverSwitchPadding = 8f;
        [SerializeField, Min(0.01f)] float animationSmoothTime = 0.12f;

        readonly List<IngredientCardUI> cards = new();
        readonly Dictionary<IngredientCardUI, CardMotion> cardMotions = new();
        IngredientCardUI hoveredCard;
        IngredientCardUI draggedCard;
        IngredientPlacementZone placementZone;
        Vector2 dragOffset;

        sealed class CardMotion
        {
            public Vector2 basePosition;
            public Vector2 targetPosition;
            public Vector2 positionVelocity;
            public float baseAngle;
            public float targetAngle;
            public float angleVelocity;
            public Vector3 targetScale;
            public Vector3 scaleVelocity;
        }

        public IReadOnlyList<IngredientCardUI> Cards => cards;

        void Awake()
        {
            foreach (IngredientData data in startingCards)
                AddCard(data);
        }

        void Start()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                placementZone = canvas.GetComponentInChildren<IngredientPlacementZone>();
            LayoutCards();
        }

        void Update()
        {
            UpdateHoveredCardFromPointer();

            float smoothTime = Mathf.Max(0.01f, animationSmoothTime);
            float deltaTime = Time.unscaledDeltaTime;

            foreach (IngredientCardUI card in cards)
            {
                if (!cardMotions.TryGetValue(card, out CardMotion motion)) continue;

                RectTransform cardRect = (RectTransform)card.transform;
                if (card != draggedCard)
                {
                    cardRect.anchoredPosition = Vector2.SmoothDamp(
                        cardRect.anchoredPosition,
                        motion.targetPosition,
                        ref motion.positionVelocity,
                        smoothTime,
                        Mathf.Infinity,
                        deltaTime);
                }

                float angle = Mathf.SmoothDampAngle(
                    cardRect.localEulerAngles.z,
                    motion.targetAngle,
                    ref motion.angleVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
                cardRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                cardRect.localScale = Vector3.SmoothDamp(
                    cardRect.localScale,
                    motion.targetScale,
                    ref motion.scaleVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
        }

        void OnRectTransformDimensionsChange()
        {
            if (cards != null) LayoutCards();
        }

        public IngredientCardUI AddCard(IngredientData data)
        {
            return AddCard(data, Vector2.zero);
        }

        public IngredientCardUI AddCard(IngredientData data, Vector2 startPosition)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (cardPrefab == null)
                throw new InvalidOperationException($"{nameof(IngredientHandView)} requires a card prefab.");

            IngredientCardUI card = Instantiate(cardPrefab, transform, false);
            ((RectTransform)card.transform).anchoredPosition = startPosition;
            card.SetData(data);
            cards.Add(card);
            cardMotions.Add(card, new CardMotion());
            LayoutCards();
            return card;
        }

        public bool RemoveCard(IngredientData data)
        {
            int index = cards.FindIndex(card => card.Data == data);
            if (index < 0) return false;

            IngredientCardUI card = cards[index];
            RemoveCard(card);
            return true;
        }

        void RemoveCard(IngredientCardUI card)
        {
            if (!cards.Remove(card)) return;

            cardMotions.Remove(card);
            if (hoveredCard == card) hoveredCard = null;
            if (draggedCard == card) draggedCard = null;
            Destroy(card.gameObject);
            LayoutCards();
        }

        public void ClearHand()
        {
            foreach (IngredientCardUI card in cards)
                Destroy(card.gameObject);

            cards.Clear();
            cardMotions.Clear();
            hoveredCard = null;
            draggedCard = null;
        }

        void UpdateHoveredCardFromPointer()
        {
            if (Mouse.current == null || cards.Count == 0)
            {
                EndDrag();
                return;
            }

            RectTransform handRect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    handRect, pointerPosition, eventCamera, out Vector2 localPointerPosition))
            {
                EndDrag();
                SetHoveredCard(null);
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame && draggedCard == null)
            {
                IngredientCardUI pressedCard = GetCardAtPointer(localPointerPosition);
                if (pressedCard != null)
                {
                    draggedCard = pressedCard;
                    hoveredCard = pressedCard;
                    RectTransform cardRect = (RectTransform)pressedCard.transform;
                    dragOffset = localPointerPosition - cardRect.anchoredPosition;
                    LayoutCards();
                }
            }

            if (draggedCard != null && Mouse.current.leftButton.isPressed)
            {
                ReorderDraggedCard(localPointerPosition.x);
                RectTransform draggedRect = (RectTransform)draggedCard.transform;
                draggedRect.anchoredPosition = localPointerPosition - dragOffset;
                return;
            }

            if (draggedCard != null)
            {
                IngredientCardUI card = draggedCard;
                bool placed = placementZone != null &&
                    placementZone.TryPlaceIngredient(card.Data, pointerPosition, eventCamera);
                EndDrag();
                if (placed) RemoveCard(card);
            }

            SetHoveredCard(GetCardAtPointer(localPointerPosition));
        }

        IngredientCardUI GetCardAtPointer(Vector2 localPointerPosition)
        {
            IngredientCardUI candidate = null;
            int candidateSiblingIndex = -1;
            foreach (IngredientCardUI card in cards)
            {
                float padding = card == hoveredCard ? hoverSwitchPadding : 0f;
                if (!IsPointerOverCard(card, localPointerPosition, padding)) continue;

                int siblingIndex = card.transform.GetSiblingIndex();
                if (siblingIndex <= candidateSiblingIndex) continue;

                candidate = card;
                candidateSiblingIndex = siblingIndex;
            }

            return candidate;
        }

        void ReorderDraggedCard(float pointerX)
        {
            int currentIndex = cards.IndexOf(draggedCard);
            if (currentIndex < 0) return;

            cards.RemoveAt(currentIndex);
            int newIndex = 0;
            foreach (IngredientCardUI card in cards)
            {
                if (pointerX > cardMotions[card].basePosition.x)
                    newIndex++;
            }

            cards.Insert(newIndex, draggedCard);
            if (newIndex != currentIndex) LayoutCards();
        }

        void EndDrag()
        {
            if (draggedCard == null) return;

            draggedCard = null;
            LayoutCards();
        }

        bool IsPointerOverCard(IngredientCardUI card, Vector2 localPointerPosition, float padding)
        {
            CardMotion motion = cardMotions[card];
            RectTransform cardRect = (RectTransform)card.transform;
            float angle = motion.baseAngle * Mathf.Deg2Rad;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector2 pointerFromCard = localPointerPosition - motion.basePosition;
            Vector2 cardLocalPosition = new(
                cos * pointerFromCard.x + sin * pointerFromCard.y,
                -sin * pointerFromCard.x + cos * pointerFromCard.y);

            Rect cardBounds = new Rect(-cardRect.rect.size * 0.5f, cardRect.rect.size);
            cardBounds.xMin -= padding;
            cardBounds.xMax += padding;
            cardBounds.yMin -= padding;
            cardBounds.yMax += padding;
            return cardBounds.Contains(cardLocalPosition);
        }

        void SetHoveredCard(IngredientCardUI card)
        {
            if (hoveredCard == card) return;

            hoveredCard = card;
            LayoutCards();
        }

        void LayoutCards()
        {
            if (cards.Count == 0) return;

            RectTransform handRect = (RectTransform)transform;
            float cardWidth = ((RectTransform)cards[0].transform).rect.width;
            float availableSpan = Mathf.Max(0f, handRect.rect.width - cardWidth);
            float spacing = cards.Count > 1
                ? Mathf.Min(cardSpacing, availableSpan / (cards.Count - 1))
                : 0f;
            float centerHeight = Mathf.Min(150f, handRect.rect.height - ((RectTransform)cards[0].transform).rect.height * 0.5f);

            for (int i = 0; i < cards.Count; i++)
            {
                IngredientCardUI card = cards[i];
                RectTransform cardRect = (RectTransform)card.transform;
                float t = cards.Count > 1 ? (i / (float)(cards.Count - 1) * 2f) - 1f : 0f;
                CardMotion motion = cardMotions[card];

                cardRect.anchorMin = new Vector2(0.5f, 0f);
                cardRect.anchorMax = new Vector2(0.5f, 0f);
                motion.basePosition = new Vector2(
                    (i - (cards.Count - 1) * 0.5f) * spacing,
                    centerHeight - fanDrop * t * t);
                motion.targetPosition = motion.basePosition +
                    (card == hoveredCard ? Vector2.up * hoverLift : Vector2.zero);
                motion.baseAngle = -t * fanAngle;
                motion.targetAngle = card == draggedCard ? 0f : motion.baseAngle;
                motion.targetScale = Vector3.one * (card == hoveredCard ? hoverScale : 1f);
                cardRect.SetSiblingIndex(i);
            }

            if (hoveredCard != null) hoveredCard.transform.SetAsLastSibling();
        }
    }
}
