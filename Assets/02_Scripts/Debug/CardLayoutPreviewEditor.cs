#if UNITY_EDITOR
using SushiDojo.Cards;
using SushiDojo.Ingredients;
using UnityEditor;
using UnityEngine;

namespace SushiDojo.DebugTools
{
    [CustomEditor(typeof(IngredientHandView))]
    sealed class IngredientHandViewEditor : Editor
    {
        static readonly Color CardOutline = new(0.35f, 0.9f, 1f, 0.9f);

        void OnSceneGUI()
        {
            if (Application.isPlaying) return;

            serializedObject.UpdateIfRequiredOrScript();
            RectTransform handRect = (RectTransform)((IngredientHandView)target).transform;
            CardLayoutPreviewDrawing.DrawBounds((Component)target, CardOutline);
            IngredientCardUI cardPrefab =
                serializedObject.FindProperty("cardPrefab").objectReferenceValue as IngredientCardUI;
            if (cardPrefab == null) return;

            RectTransform cardRect = cardPrefab.transform as RectTransform;
            if (cardRect == null) return;

            SerializedProperty startingCards = serializedObject.FindProperty("startingCards");
            int count = startingCards.arraySize;
            if (count == 0) return;

            float cardWidth = cardRect.rect.width;
            float cardHeight = cardRect.rect.height;
            ((IngredientHandView)target).GetLayoutMetrics(cardRect.rect.size, count,
                out float spacing, out float centerHeight);
            float fanDrop = serializedObject.FindProperty("fanDrop").floatValue;
            float fanAngle = serializedObject.FindProperty("fanAngle").floatValue;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (i / (float)(count - 1) * 2f) - 1f : 0f;
                Vector2 position = new(
                    (i - (count - 1) * 0.5f) * spacing,
                    centerHeight - fanDrop * t * t);
                IngredientData ingredient = startingCards.GetArrayElementAtIndex(i).objectReferenceValue
                    as IngredientData;
                string label = ingredient != null ? ingredient.DisplayName : $"Card {i + 1}";
                CardLayoutPreviewDrawing.DrawCard(
                    handRect,
                    position,
                    new Vector2(cardWidth, cardHeight),
                    -t * fanAngle,
                    label,
                    CardOutline);
            }
        }
    }

    [CustomEditor(typeof(RecipeQueueView))]
    sealed class RecipeQueueViewEditor : Editor
    {
        static readonly Color CurrentCardOutline = new(1f, 0.78f, 0.28f, 0.95f);
        static readonly Color NextCardOutline = new(0.65f, 0.85f, 1f, 0.8f);

        void OnSceneGUI()
        {
            if (Application.isPlaying) return;

            serializedObject.UpdateIfRequiredOrScript();
            CardLayoutPreviewDrawing.DrawBounds((Component)target, CurrentCardOutline);
            RecipeCardUI cardPrefab =
                serializedObject.FindProperty("cardPrefab").objectReferenceValue as RecipeCardUI;
            if (cardPrefab == null) return;

            RectTransform cardRect = cardPrefab.transform as RectTransform;
            if (cardRect == null) return;

            float spacing = serializedObject.FindProperty("cardSpacing").floatValue;
            float scale = serializedObject.FindProperty("cardScale").floatValue;
            SerializedProperty recipeDeck = serializedObject.FindProperty("recipeDeck");
            DrawRecipeCard(-spacing * 0.5f, 0f, scale, 0, CurrentCardOutline, recipeDeck);
            DrawRecipeCard(spacing * 0.5f, 0f, scale, 1, NextCardOutline, recipeDeck);

            void DrawRecipeCard(
                float x,
                float y,
                float cardScale,
                int index,
                Color outline,
                SerializedProperty deck)
            {
                if (index >= deck.arraySize) return;

                RecipeData recipe = deck.GetArrayElementAtIndex(index).objectReferenceValue as RecipeData;
                string label = recipe != null ? recipe.DisplayName : $"Recipe {index + 1}";
                CardLayoutPreviewDrawing.DrawCard(
                    ((RecipeQueueView)target).transform,
                    new Vector2(x, y),
                    cardRect.rect.size * cardScale,
                    0f,
                    label,
                    outline);
            }
        }
    }

    [CustomEditor(typeof(IngredientPlacementZone))]
    sealed class IngredientPlacementZoneEditor : Editor
    {
        static readonly Color ZoneOutline = new(0.45f, 1f, 0.55f, 0.95f);

        void OnSceneGUI()
        {
            CardLayoutPreviewDrawing.DrawBounds((Component)target, ZoneOutline);
        }
    }

    [CustomEditor(typeof(IngredientCardUI))]
    sealed class IngredientCardBoundsEditor : Editor
    {
        void OnSceneGUI() => CardLayoutPreviewDrawing.DrawBounds(
            (Component)target, new Color(0.35f, 0.9f, 1f, 0.9f));
    }

    [CustomEditor(typeof(RecipeCardUI))]
    sealed class RecipeCardBoundsEditor : Editor
    {
        void OnSceneGUI() => CardLayoutPreviewDrawing.DrawBounds(
            (Component)target, new Color(1f, 0.78f, 0.28f, 0.95f));
    }

    static class CardLayoutPreviewDrawing
    {
        public static void DrawBounds(Component component, Color outline)
        {
            if (Application.isPlaying) return;
            if (component.transform is not RectTransform rect) return;
            DrawCard(rect, rect.rect.center, rect.rect.size, 0f, component.name, outline);
        }

        public static void DrawCard(
            Transform parent,
            Vector2 center,
            Vector2 size,
            float angle,
            string label,
            Color outline)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector3[] corners =
            {
                ToWorld(-0.5f, -0.5f),
                ToWorld(-0.5f, 0.5f),
                ToWorld(0.5f, 0.5f),
                ToWorld(0.5f, -0.5f),
                ToWorld(-0.5f, -0.5f)
            };
            Color previousColor = Handles.color;
            Handles.color = outline;
            Color fill = new(outline.r, outline.g, outline.b, 0.22f);
            Handles.DrawSolidRectangleWithOutline(
                new[] { corners[0], corners[1], corners[2], corners[3] }, fill, outline);
            Handles.DrawAAPolyLine(2f, corners);
            Handles.Label(ToWorld(0f, 0.5f) + parent.up * 10f, label, EditorStyles.boldLabel);
            Handles.color = previousColor;

            Vector3 ToWorld(float x, float y)
            {
                Vector2 offset = rotation * new Vector2(size.x * x, size.y * y);
                return parent.TransformPoint(center + offset);
            }
        }
    }
}
#endif
