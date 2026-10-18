using SushiDojo.Ingredients;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Cards
{
    public class IngredientCardUI : MonoBehaviour
    {
        [SerializeField] Image ingredientImage;
        [SerializeField] TMP_Text ingredientName;

        public IngredientData Data { get; private set; }

        public void SetData(IngredientData data)
        {
            Data = data;

            if (data == null)
            {
                ingredientImage.sprite = null;
                ingredientName.text = string.Empty;
                return;
            }

            ingredientImage.sprite = data.Sprite;
            ingredientName.text = data.DisplayName;
        }
    }
}
