using System;
using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Cards
{
    [RequireComponent(typeof(RectTransform), typeof(Canvas))]
    public class IngredientPlacementZone : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;

        public bool AcceptingIngredients { get; set; } = true;

        readonly List<string> placedIngredientNames = new();

        public event Action<IngredientData> IngredientPlaced;
        public IReadOnlyList<string> PlacedIngredientNames => placedIngredientNames;

        public bool TryPlaceIngredient(IngredientData data, Vector2 screenPosition, Camera eventCamera)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!AcceptingIngredients) return false;

            Camera camera = worldCamera != null ? worldCamera :
                (eventCamera != null ? eventCamera : Camera.main);
            if (camera == null || !isActiveAndEnabled) return false;

            RectTransform zoneRect = (RectTransform)transform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(zoneRect, screenPosition, camera))
                return false;

            if (placedIngredientNames.Count >= RecipeData.IngredientSlotCount)
            {
                Debug.LogWarning(
                    $"[{nameof(IngredientPlacementZone)}] Cannot place more than {RecipeData.IngredientSlotCount} ingredients.",
                    this);
                return false;
            }

            placedIngredientNames.Add(data.DisplayName);
            Debug.Log(
                $"[IngredientPlacementZone] Placed: {data.DisplayName} ({placedIngredientNames.Count}/{RecipeData.IngredientSlotCount})",
                this);
            IngredientPlaced?.Invoke(data);
            return true;
        }

        public void ClearPlacedIngredients()
        {
            placedIngredientNames.Clear();
        }
    }
}
