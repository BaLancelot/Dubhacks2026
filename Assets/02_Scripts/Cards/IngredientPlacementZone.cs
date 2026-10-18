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
        public IReadOnlyList<string> PlacedIngredientNames => placedIngredientNames;
        /// <summary>The board placed ingredients sit on.</summary>
        public Transform Board => discardTrigger != null ? discardTrigger.transform : transform;

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
            Vector3 boardUp = discardTrigger != null ? discardTrigger.transform.up : transform.up;
            foreach (BoardIngredient ingredient in placedIngredients)
            {
                if (ingredient != null) ingredient.LaunchForDiscard(boardUp);
            }

            placedIngredients.Clear();
            placedIngredientNames.Clear();
            IngredientsDiscarded?.Invoke(discardedIngredientCount);
        }
    }
}
