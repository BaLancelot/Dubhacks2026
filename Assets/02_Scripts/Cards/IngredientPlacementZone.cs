using System;
using System.Collections.Generic;
using SushiDojo.Board;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Cards
{
    [RequireComponent(typeof(RectTransform), typeof(Canvas))]
    public class IngredientPlacementZone : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] BoardIngredient ingredientPrefab;
        [SerializeField] ThrowableBody discardTrigger;

        public bool AcceptingIngredients { get; set; } = true;

        readonly List<string> placedIngredientNames = new();
        readonly List<BoardIngredient> placedIngredients = new();
        int placementSortingOrderOffset;

        public event Action<IngredientData> IngredientPlaced;
        public event Action<int> IngredientsDiscarded;
        public event Action<IReadOnlyList<IngredientData>> IngredientsThrown;
        public IReadOnlyList<string> PlacedIngredientNames => placedIngredientNames;
        public bool IsFull => placedIngredientNames.Count >= RecipeData.IngredientSlotCount;
        public bool HasPlacedIngredients => placedIngredients.Count > 0;
        public int PlacedCount => placedIngredients.Count;
        /// <summary>The board placed ingredients sit on.</summary>
        public Transform Board => discardTrigger != null ? discardTrigger.transform : transform;

        public bool ContainsScreenPoint(Vector2 screenPosition, Camera eventCamera)
        {
            Camera camera = worldCamera != null ? worldCamera :
                (eventCamera != null ? eventCamera : Camera.main);
            if (camera == null || !isActiveAndEnabled) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)transform, screenPosition, camera);
        }

        void OnEnable()
        {
            if (discardTrigger == null)
                discardTrigger = GetComponentInParent<ThrowableBody>();

            if (discardTrigger == null)
            {
                Debug.LogError(
                    $"{nameof(IngredientPlacementZone)} requires a parent {nameof(ThrowableBody)} to discard placed ingredients.",
                    this);
                return;
            }

            discardTrigger.Released += DiscardPlacedIngredients;
        }

        void OnDisable()
        {
            if (discardTrigger != null)
                discardTrigger.Released -= DiscardPlacedIngredients;
        }

        public bool TryPlaceIngredient(IngredientData data, Vector2 screenPosition, Camera eventCamera)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!AcceptingIngredients) return false;
            if (SushiMultiplayer.Instance != null)
            {
                NetworkMatchTimer timer = FindFirstObjectByType<NetworkMatchTimer>();
                if (timer == null || !timer.IsMatchRunning || SushiMultiplayer.Instance.ConnectionLost)
                    return false;
            }

            if (ingredientPrefab == null)
            {
                Debug.LogError($"{nameof(IngredientPlacementZone)} requires an ingredient prefab.", this);
                return false;
            }

            if (discardTrigger == null)
            {
                Debug.LogError($"{nameof(IngredientPlacementZone)} has no board discard trigger.", this);
                return false;
            }

            Camera camera = worldCamera != null ? worldCamera :
                (eventCamera != null ? eventCamera : Camera.main);
            if (camera == null || !isActiveAndEnabled) return false;

            RectTransform zoneRect = (RectTransform)transform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(zoneRect, screenPosition, camera))
                return false;

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    zoneRect, screenPosition, camera, out Vector3 worldPosition))
                return false;

            if (placedIngredientNames.Count >= RecipeData.IngredientSlotCount)
            {
                Debug.LogWarning(
                    $"[{nameof(IngredientPlacementZone)}] Cannot place more than {RecipeData.IngredientSlotCount} ingredients.",
                    this);
                return false;
            }

            Transform board = discardTrigger.transform;
            BoardIngredient placedIngredient = Instantiate(
                ingredientPrefab,
                worldPosition + board.up * 0.08f,
                board.rotation,
                board);
            if (!placedIngredient.Initialize(data, placementSortingOrderOffset))
            {
                Destroy(placedIngredient.gameObject);
                return false;
            }

            placementSortingOrderOffset++;
            placedIngredients.Add(placedIngredient);
            placedIngredientNames.Add(data.DisplayName);
            Debug.Log(
                $"[IngredientPlacementZone] Placed: {data.DisplayName} ({placedIngredientNames.Count}/{RecipeData.IngredientSlotCount})",
                this);
            IngredientPlaced?.Invoke(data);
            return true;
        }

        /// <summary>Drops a leftover tray card onto the board without counting as a player placement.</summary>
        public bool PlaceForWaste(IngredientData data)
        {
            if (data == null || ingredientPrefab == null || discardTrigger == null || !isActiveAndEnabled)
                return false;

            RectTransform zoneRect = (RectTransform)transform;
            Rect rect = zoneRect.rect;
            int index = placedIngredients.Count;
            const int columns = 4;
            float x = Mathf.Lerp(rect.xMin, rect.xMax, ((index % columns) + 0.5f) / columns);
            float row = index / columns;
            float y = Mathf.Lerp(rect.yMin, rect.yMax, (row + 0.5f) / 3f);
            Vector3 worldPosition = zoneRect.TransformPoint(new Vector3(x, y, 0f));

            Transform board = discardTrigger.transform;
            BoardIngredient placedIngredient = Instantiate(
                ingredientPrefab,
                worldPosition + board.up * 0.08f,
                board.rotation,
                board);
            if (!placedIngredient.Initialize(data, placementSortingOrderOffset))
            {
                Destroy(placedIngredient.gameObject);
                return false;
            }

            placementSortingOrderOffset++;
            placedIngredients.Add(placedIngredient);
            placedIngredientNames.Add(data.DisplayName);
            return true;
        }

        /// <summary>Throws the board so every ingredient on it is discarded.</summary>
        public void ThrowForWaste()
        {
            if (discardTrigger == null || placedIngredients.Count == 0) return;
            Transform board = discardTrigger.transform;
            discardTrigger.Release(board.forward * 10f + board.up * 4f);
        }

        public void ConsumePlacedIngredients()
        {
            foreach (BoardIngredient ingredient in placedIngredients)
            {
                if (ingredient != null) Destroy(ingredient.gameObject);
            }

            placedIngredients.Clear();
            placedIngredientNames.Clear();
        }

        /// <summary>Hands the placed ingredients over (e.g. to the merge effect) without destroying them.</summary>
        public List<BoardIngredient> TakePlacedIngredients()
        {
            var taken = new List<BoardIngredient>(placedIngredients);
            placedIngredients.Clear();
            placedIngredientNames.Clear();
            return taken;
        }

        public void ClearPlacedIngredients()
        {
            DiscardPlacedIngredients();
        }

        void DiscardPlacedIngredients()
        {
            if (placedIngredients.Count == 0) return;

            int discardedIngredientCount = placedIngredients.Count;
            var thrown = new List<IngredientData>(discardedIngredientCount);
            Vector3 boardUp = discardTrigger != null ? discardTrigger.transform.up : transform.up;
            foreach (BoardIngredient ingredient in placedIngredients)
            {
                if (ingredient == null) continue;
                if (ingredient.Data != null) thrown.Add(ingredient.Data);
                ingredient.LaunchForDiscard(boardUp);
            }

            placedIngredients.Clear();
            placedIngredientNames.Clear();
            IngredientsDiscarded?.Invoke(discardedIngredientCount);
            if (thrown.Count > 0)
                IngredientsThrown?.Invoke(thrown);
        }
    }
}
