using System;
using System.Collections;
using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiDojo.Cards
{
    public class IngredientHandView : MonoBehaviour
    {
        public const int MaxCards = 8;
        [SerializeField] IngredientCardUI cardPrefab;
        [SerializeField] NetworkConveyor networkConveyor;
        [SerializeField] IngredientData[] startingCards = Array.Empty<IngredientData>();
        [SerializeField, Min(0f)] float cardSpacing = 80f;
        [SerializeField, Min(0f)] float fanDrop = 42f;
        [SerializeField, Min(0f)] float fanAngle = 8f;
        [SerializeField, Min(0f)] float hoverLift = 32f;
        [SerializeField, Min(1f)] float hoverScale = 1.1f;
        [SerializeField, Min(0f)] float hoverSwitchPadding = 8f;
        [SerializeField, Min(0.01f)] float animationSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] float rejectShakeDuration = 0.48f;
        [SerializeField, Min(0f)] float handShakeStrength = 10f;
        [SerializeField, Min(0f)] float cardShakeStrength = 14f;
        [SerializeField] Color rejectFlashColor = new(1f, 0.28f, 0.28f, 1f);
        [SerializeField, Range(0f, 1f)] float rejectFlashStrength = 0.7f;
        [SerializeField, Min(0f)] float wasteStartDelay = 2f;
        [SerializeField, Min(0.01f)] float wasteExitDuration = 0.45f;
        [SerializeField, Min(0f)] float wasteExitDistance = 90f;
        [SerializeField, Min(0f)] float wasteCardPause = 0.35f;
        [SerializeField, Min(0f)] float wasteThrowDelay = 1f;
        [SerializeField, Min(0f)] float wasteThrowHold = 4f;
        [SerializeField, Min(0f)] float wasteSkipHold = 2f;

        readonly List<IngredientCardUI> cards = new();
        readonly Dictionary<IngredientCardUI, CardMotion> cardMotions = new();
        readonly List<GraphicColorSample> handFlashGraphics = new();
        readonly List<GraphicColorSample> cardFlashGraphics = new();
        IngredientCardUI hoveredCard;
        IngredientCardUI draggedCard;
        [SerializeField] IngredientPlacementZone placementZone;
        Vector2 dragOffset;
        Vector2 handRestPosition;
        float handShakeTimer;
        float cardShakeTimer;
        IngredientCardUI shakingCard;
        NetworkMatchTimer matchTimer;
        bool settlingWaste;
        bool wasteThrowStarted;

        struct GraphicColorSample
        {
            public Graphic Graphic;
            public Color Color;
        }

        /// <summary>The pointer moved onto a card (not fired while dragging).</summary>
        public event Action<IngredientCardUI> CardHovered;
        /// <summary>A card was pressed and picked up.</summary>
        public event Action<IngredientCardUI> CardPicked;

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
        public bool IsFull => cards.Count >= MaxCards;

        void OnEnable()
        {
            if (networkConveyor == null)
                networkConveyor = FindFirstObjectByType<NetworkConveyor>();
            if (networkConveyor != null)
                networkConveyor.OnIngredientAwarded += HandleIngredientAwarded;
            if (matchTimer == null)
                matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
            if (matchTimer != null)
            {
                matchTimer.OnMatchSettling += BeginWasteSettle;
                matchTimer.OnWasteThrow += HandleWasteThrow;
            }
            if (matchTimer != null && matchTimer.WasteThrowStarted)
                wasteThrowStarted = true;
            if (matchTimer != null && matchTimer.IsMatchSettling)
                BeginWasteSettle();
        }

        void OnDisable()
        {
            if (networkConveyor != null)
                networkConveyor.OnIngredientAwarded -= HandleIngredientAwarded;
            if (matchTimer != null)
            {
                matchTimer.OnMatchSettling -= BeginWasteSettle;
                matchTimer.OnWasteThrow -= HandleWasteThrow;
            }
        }

        void HandleWasteThrow()
        {
            wasteThrowStarted = true;
        }

        void BeginWasteSettle()
        {
            if (!isActiveAndEnabled || settlingWaste) return;
            settlingWaste = true;
            draggedCard = null;
            hoveredCard = null;
            LayoutCards();
            StartCoroutine(DiscardTrayAsWaste());
        }

        IEnumerator DiscardTrayAsWaste()
        {
            if (placementZone == null)
                placementZone = FindFirstObjectByType<IngredientPlacementZone>();
            if (matchTimer == null)
                matchTimer = FindFirstObjectByType<NetworkMatchTimer>();

            int placed = placementZone != null ? placementZone.PlacedCount : 0;
            matchTimer?.NotifyWasteCount(cards.Count + placed);
            while (matchTimer != null && !matchTimer.WastePlanReady && matchTimer.IsMatchSettling)
                yield return null;

            if (matchTimer != null && matchTimer.WastePlanSkipped)
            {
                if (wasteSkipHold > 0f)
                    yield return new WaitForSecondsRealtime(wasteSkipHold);
                settlingWaste = false;
                matchTimer.NotifyLocalSettleComplete();
                yield break;
            }

            if (wasteStartDelay > 0f)
                yield return new WaitForSecondsRealtime(wasteStartDelay);

            while (cards.Count > 0)
            {
                IngredientCardUI card = cards[cards.Count - 1];
                cards.Remove(card);
                cardMotions.Remove(card);
                LayoutCards();
                yield return SlideOut(card);
                if (placementZone != null && card.Data != null)
                    placementZone.PlaceForWaste(card.Data);
                Destroy(card.gameObject);
                if (wasteCardPause > 0f && cards.Count > 0)
                    yield return new WaitForSecondsRealtime(wasteCardPause);
            }

            if (matchTimer == null)
                matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
            if (matchTimer != null)
            {
                matchTimer.NotifyReadyToThrow();
                while (!wasteThrowStarted && !matchTimer.WasteThrowStarted && matchTimer.IsMatchSettling)
                    yield return null;
            }

            if (wasteThrowDelay > 0f)
                yield return new WaitForSecondsRealtime(wasteThrowDelay);

            if (placementZone != null && placementZone.HasPlacedIngredients)
            {
                placementZone.ThrowForWaste();
                if (wasteThrowHold > 0f)
                    yield return new WaitForSecondsRealtime(wasteThrowHold);
            }

            settlingWaste = false;
            if (matchTimer == null)
                matchTimer = FindFirstObjectByType<NetworkMatchTimer>();
            matchTimer?.NotifyLocalSettleComplete();
        }

        IEnumerator SlideOut(IngredientCardUI card)
        {
            RectTransform rect = (RectTransform)card.transform;
            CanvasGroup group = card.TryGetComponent(out CanvasGroup existing)
                ? existing
                : card.gameObject.AddComponent<CanvasGroup>();
            Vector2 start = rect.anchoredPosition;
            Vector2 target = start + Vector2.up * wasteExitDistance;
            float duration = Mathf.Max(0.01f, wasteExitDuration);

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, t / duration);
                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, progress);
                group.alpha = 1f - progress;
                yield return null;
            }

            group.alpha = 0f;
        }

        void HandleIngredientAwarded(IngredientData ingredient)
        {
            if (settlingWaste) return;
            if (AddCard(ingredient) != null)
            {
                Debug.Log($"[IngredientHand] Added {ingredient.DisplayName}; cards={cards.Count}.", this);
                return;
            }

            if (IsFull) PlayFullHandShake();
        }

        void Awake()
        {
            // Apply at runtime too: editor/virtual-player scene snapshots can
            // retain Screen Space Camera despite the saved scene using Overlay.
            Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }

            handRestPosition = ((RectTransform)transform).anchoredPosition;

            foreach (IngredientData data in startingCards)
                AddCard(data);
        }

        /// <summary>Shake the whole hand when the player tries to take with a full deck.</summary>
        public void PlayFullHandShake()
        {
            RestoreFlash(handFlashGraphics);
            CaptureFlash(transform, handFlashGraphics);
            handShakeTimer = rejectShakeDuration;
        }

        void PlayCardRejectShake(IngredientCardUI card)
        {
            if (card == null) return;
            RestoreFlash(cardFlashGraphics);
            CaptureFlash(card.transform, cardFlashGraphics);
            shakingCard = card;
            cardShakeTimer = rejectShakeDuration;
        }

        static void CaptureFlash(Transform root, List<GraphicColorSample> samples)
        {
            samples.Clear();
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                samples.Add(new GraphicColorSample { Graphic = graphic, Color = graphic.color });
        }

        void ApplyFlash(List<GraphicColorSample> samples, float amount)
        {
            if (samples.Count == 0) return;
            float blend = Mathf.Clamp01(amount) * rejectFlashStrength;
            for (int i = 0; i < samples.Count; i++)
            {
                Graphic graphic = samples[i].Graphic;
                if (graphic == null) continue;
                Color baseColor = samples[i].Color;
                Color flash = rejectFlashColor;
                flash.a = baseColor.a;
                graphic.color = Color.Lerp(baseColor, flash, blend);
            }
        }

        static void RestoreFlash(List<GraphicColorSample> samples)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                Graphic graphic = samples[i].Graphic;
                if (graphic != null) graphic.color = samples[i].Color;
            }
            samples.Clear();
        }

        void Start()
        {
            if (placementZone == null)
                placementZone = FindFirstObjectByType<IngredientPlacementZone>();
            LayoutCards();
            Debug.Log($"[IngredientHand] Ready in {gameObject.scene.name}; conveyor bound={networkConveyor != null}.", this);
        }

        void Update()
        {
            if (!settlingWaste)
                UpdateHoveredCardFromPointer();

            float smoothTime = Mathf.Max(0.01f, animationSmoothTime);
            float deltaTime = Time.unscaledDeltaTime;
            RectTransform handRect = (RectTransform)transform;

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

            if (handShakeTimer > 0f)
            {
                handShakeTimer = Mathf.Max(0f, handShakeTimer - deltaTime);
                float falloff = handShakeTimer / rejectShakeDuration;
                falloff *= falloff;
                handRect.anchoredPosition = handRestPosition +
                    UnityEngine.Random.insideUnitCircle * (handShakeStrength * falloff);
                ApplyFlash(handFlashGraphics, falloff);
                if (handShakeTimer <= 0f) RestoreFlash(handFlashGraphics);
            }
            else
            {
                handRect.anchoredPosition = handRestPosition;
            }

            if (cardShakeTimer > 0f && shakingCard != null && cards.Contains(shakingCard))
            {
                cardShakeTimer = Mathf.Max(0f, cardShakeTimer - deltaTime);
                float falloff = cardShakeTimer / rejectShakeDuration;
                falloff *= falloff;
                RectTransform shakingRect = (RectTransform)shakingCard.transform;
                shakingRect.anchoredPosition +=
                    UnityEngine.Random.insideUnitCircle * (cardShakeStrength * falloff);
                ApplyFlash(cardFlashGraphics, falloff);
                if (cardShakeTimer <= 0f)
                {
                    RestoreFlash(cardFlashGraphics);
                    shakingCard = null;
                }
            }
            else if (cardShakeTimer <= 0f)
            {
                if (cardFlashGraphics.Count > 0) RestoreFlash(cardFlashGraphics);
                shakingCard = null;
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
            if (IsFull) return null;
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
            if (shakingCard == card)
            {
                RestoreFlash(cardFlashGraphics);
                shakingCard = null;
                cardShakeTimer = 0f;
            }
            Destroy(card.gameObject);
            LayoutCards();
        }

        public void ClearHand()
        {
            RestoreFlash(handFlashGraphics);
            RestoreFlash(cardFlashGraphics);

            foreach (IngredientCardUI card in cards)
                Destroy(card.gameObject);

            cards.Clear();
            cardMotions.Clear();
            hoveredCard = null;
            draggedCard = null;
            shakingCard = null;
            cardShakeTimer = 0f;
            handShakeTimer = 0f;
            ((RectTransform)transform).anchoredPosition = handRestPosition;
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
                    CardPicked?.Invoke(pressedCard);
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
                bool overZone = placementZone != null &&
                    placementZone.ContainsScreenPoint(pointerPosition, eventCamera);
                bool placed = overZone &&
                    placementZone.TryPlaceIngredient(card.Data, pointerPosition, eventCamera);
                EndDrag();
                if (placed)
                    RemoveCard(card);
                else if (overZone && placementZone.IsFull)
                    PlayCardRejectShake(card);
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
            if (card != null && draggedCard == null) CardHovered?.Invoke(card);
        }

        // Shared with the editor preview so its outlines match the playable hand.
        public void GetLayoutMetrics(Vector2 cardSize, int count, out float spacing, out float centerHeight)
        {
            RectTransform handRect = (RectTransform)transform;
            float angle = Mathf.Abs(fanAngle) * Mathf.Deg2Rad;
            float scale = Mathf.Max(1f, hoverScale);
            float halfWidth = (cardSize.x * Mathf.Abs(Mathf.Cos(angle)) +
                cardSize.y * Mathf.Abs(Mathf.Sin(angle))) * scale * 0.5f;
            float halfHeight = (cardSize.y * Mathf.Abs(Mathf.Cos(angle)) +
                cardSize.x * Mathf.Abs(Mathf.Sin(angle))) * scale * 0.5f;
            float availableSpan = Mathf.Max(0f, handRect.rect.width - halfWidth * 2f - 16f);
            spacing = count > 1 ? Mathf.Min(cardSpacing, availableSpan / (count - 1)) : 0f;
            centerHeight = Mathf.Min(halfHeight + fanDrop + 8f,
                handRect.rect.height - halfHeight - hoverLift - 8f);
        }

        void LayoutCards()
        {
            if (cards.Count == 0) return;

            GetLayoutMetrics(((RectTransform)cards[0].transform).rect.size, cards.Count,
                out float spacing, out float centerHeight);

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
