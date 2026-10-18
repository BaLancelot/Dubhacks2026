using System.Collections.Generic;
using SushiDojo.Ingredients;
using UnityEngine;

namespace SushiDojo.Cards
{
    /// <summary>
    /// Belt spawn weighting: base × global frequency × active demand (scaled by recipe hardness),
    /// blended toward equal odds by diversity mix.
    /// </summary>
    public static class RecipeIngredientWeights
    {
        public static int CountIngredients(RecipeData recipe)
        {
            if (recipe?.Ingredients == null) return 0;
            int count = 0;
            foreach (IngredientData ingredient in recipe.Ingredients)
                if (ingredient != null) count++;
            return count;
        }

        public static void AddRecipeFrequencies(
            RecipeData recipe, Dictionary<IngredientData, int> frequencies)
        {
            if (recipe?.Ingredients == null || frequencies == null) return;
            foreach (IngredientData ingredient in recipe.Ingredients)
            {
                if (ingredient == null) continue;
                frequencies.TryGetValue(ingredient, out int count);
                frequencies[ingredient] = count + 1;
            }
        }

        public static int MaxFrequency(Dictionary<IngredientData, int> frequencies)
        {
            int max = 0;
            if (frequencies == null) return max;
            foreach (int count in frequencies.Values)
                if (count > max) max = count;
            return max;
        }

        /// <summary>
        /// focused = base * (1 + globalBoost * globalNorm) * (1 + activeBoost * hardnessScale),
        /// then lerped toward equal odds by <paramref name="diversityMix"/>.
        /// <paramref name="activeHardness"/> is the hardest active recipe size that needs this ingredient
        /// (shared needs use max, not sum — so two players needing Rice for 3-ings don't stack).
        /// Base 0 stays 0.
        /// </summary>
        public static float EffectiveWeight(
            float baseWeight,
            int globalCount,
            int maxGlobalCount,
            int activeHardness,
            float globalFrequencyBoost,
            float activeRecipeBoost,
            float diversityMix)
        {
            if (baseWeight <= 0f) return 0f;

            float globalNorm = maxGlobalCount > 0 ? globalCount / (float)maxGlobalCount : 0f;
            float globalFactor = 1f + Mathf.Max(0f, globalFrequencyBoost) * globalNorm;
            float activeFactor = activeHardness > 0
                ? 1f + Mathf.Max(0f, activeRecipeBoost) * HardnessScale(activeHardness)
                : 1f;
            float focused = baseWeight * globalFactor * activeFactor;
            const float equalShare = 1f;
            return Mathf.Lerp(focused, equalShare, Mathf.Clamp01(diversityMix));
        }

        /// <summary>Harder recipes get more belt help for their ingredients.</summary>
        public static float HardnessScale(int ingredientCount)
        {
            if (ingredientCount <= 3) return 0.7f;
            if (ingredientCount == 4) return 1.0f;
            if (ingredientCount == 5) return 1.25f;
            return 1.55f; // 6+
        }

        public static void CollectQueueRecipes(
            RecipeQueueView queue,
            Dictionary<IngredientData, int> globalFrequencies,
            Dictionary<IngredientData, int> activeHardness)
        {
            if (queue == null) return;

            foreach (RecipeData recipe in queue.DeckRecipes)
                AddRecipeFrequencies(recipe, globalFrequencies);

            MarkActiveIngredients(queue.CurrentRecipe, activeHardness);
            MarkActiveIngredients(queue.NextRecipe, activeHardness);
        }

        static void MarkActiveIngredients(
            RecipeData recipe, Dictionary<IngredientData, int> activeHardness)
        {
            if (recipe?.Ingredients == null || activeHardness == null) return;
            int hardness = CountIngredients(recipe);
            if (hardness <= 0) return;

            foreach (IngredientData ingredient in recipe.Ingredients)
            {
                if (ingredient == null) continue;
                activeHardness.TryGetValue(ingredient, out int existing);
                if (hardness > existing)
                    activeHardness[ingredient] = hardness;
            }
        }

        public static void CollectAllQueues(
            Dictionary<IngredientData, int> globalFrequencies,
            Dictionary<IngredientData, int> activeHardness)
        {
            globalFrequencies.Clear();
            activeHardness.Clear();

            RecipeQueueView[] queues = Object.FindObjectsByType<RecipeQueueView>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (RecipeQueueView queue in queues)
                CollectQueueRecipes(queue, globalFrequencies, activeHardness);
        }
    }
}
