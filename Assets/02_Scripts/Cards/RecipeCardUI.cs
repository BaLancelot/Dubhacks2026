using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Cards
{
    public class RecipeCardUI : MonoBehaviour
    {
        [SerializeField] Image foodImage;
        [SerializeField] Text recipeName;
        [SerializeField] Image[] ingredientImages = new Image[RecipeData.IngredientSlotCount];
        [SerializeField] Sprite emptySlotSprite;

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
            for (int i = 0; i < ingredientImages.Length; i++)
            {
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
        }
    }
}
