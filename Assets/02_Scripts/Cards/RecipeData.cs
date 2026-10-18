using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Cards
{
    [CreateAssetMenu(fileName = "Recipe", menuName = "Sushi Dojo/Recipe")]
    public class RecipeData : ScriptableObject
    {
        public const int IngredientSlotCount = 6;

        [SerializeField] string displayName;
        [SerializeField] Sprite foodSprite;
        [SerializeField] IngredientData[] ingredients = new IngredientData[IngredientSlotCount];

        public string DisplayName => displayName;
        public Sprite FoodSprite => foodSprite;
        public IReadOnlyList<IngredientData> Ingredients => ingredients;
    }
}
