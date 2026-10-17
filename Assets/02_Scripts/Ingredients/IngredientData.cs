using UnityEngine;

namespace SushiDojo.Ingredients
{
    [CreateAssetMenu(fileName = "Ingredient", menuName = "Sushi Dojo/Ingredient")]
    public class IngredientData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Sprite sprite;

        [Tooltip("Relative spawn chance on the belt. 0 = never spawns.")]
        [SerializeField, Min(0f)] float spawnWeight = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public float SpawnWeight => spawnWeight;
    }
}
