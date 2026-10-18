using System.Collections.Generic;
using SushiDojo.Ingredients;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Cards
{
    public class RecipeCardUI : MonoBehaviour
    {
        [SerializeField] Image foodImage;
        [SerializeField] TMP_Text recipeName;
        [SerializeField] Image[] ingredientImages = new Image[RecipeData.IngredientSlotCount];
        [SerializeField] Sprite emptySlotSprite;
        [Tooltip("Slots shown even when the recipe has fewer ingredients. Extra slots are hidden and the layout re-centers.")]
        [SerializeField, Range(0, RecipeData.IngredientSlotCount)] int minVisibleSlots = 3;

        public void SetData(RecipeData data)
        {
            if (data == null)
            {
                foodImage.sprite = null;
                recipeName.text = string.Empty;
                SetIngredients(null);
                return;
            }

            foodImage.sprite = data.FoodSprite;
            recipeName.text = data.DisplayName;
            SetIngredients(data.Ingredients);
        }

        void SetIngredients(IReadOnlyList<IngredientData> ingredients)
        {
            int visibleCount = Mathf.Clamp(UsedSlotCount(ingredients), minVisibleSlots, ingredientImages.Length);

            for (int i = 0; i < ingredientImages.Length; i++)
            {
                bool isVisible = i < visibleCount;
                ingredientImages[i].gameObject.SetActive(isVisible);
                if (!isVisible) continue;

                IngredientData ingredient = ingredients != null && i < ingredients.Count
                    ? ingredients[i]
                    : null;
                Sprite ingredientSprite = ingredient != null ? ingredient.Sprite : null;
                ingredientImages[i].sprite = ingredientSprite != null
                    ? ingredientSprite
                    : emptySlotSprite;
                ingredientImages[i].color = ingredient != null
                    ? Color.white
                    : new Color(0.87f, 0.82f, 0.72f);
                ingredientImages[i].type = Image.Type.Simple;
                ingredientImages[i].preserveAspect = true;
            }

            HideEmptyRows();
        }

        /// <summary>A layout row with nothing shown is turned off so the remaining rows re-center.</summary>
        void HideEmptyRows()
        {
            foreach (Image slot in ingredientImages)
            {
                Transform row = slot.transform.parent;
                if (row == null || row == transform || !row.TryGetComponent(out HorizontalLayoutGroup _)) continue;

                bool hasVisibleSlot = false;
                foreach (Transform child in row)
                    if (child.gameObject.activeSelf) hasVisibleSlot = true;
                row.gameObject.SetActive(hasVisibleSlot);
            }
        }

        /// <summary>Up to and including the last filled ingredient, so gaps in the recipe still show.</summary>
        static int UsedSlotCount(IReadOnlyList<IngredientData> ingredients)
        {
            if (ingredients == null) return 0;
            for (int i = ingredients.Count - 1; i >= 0; i--)
                if (ingredients[i] != null) return i + 1;
            return 0;
        }
    }
}
