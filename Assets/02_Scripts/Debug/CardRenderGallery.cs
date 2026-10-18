using System.Collections.Generic;
using SushiDojo.Cards;
using SushiDojo.Conveyor;
using SushiDojo.Ingredients;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SushiDojo.DebugTools
{
    /// <summary>
    /// Lays out every recipe and ingredient card so you can visually verify rendering.
    /// Open <c>Assets/00_Scenes/CardPreview.unity</c> and press Play.
    /// </summary>
    public class CardRenderGallery : MonoBehaviour
    {
        [SerializeField] IngredientCardUI ingredientCardPrefab;
        [SerializeField] RecipeCardUI recipeCardPrefab;
        [SerializeField] IngredientData[] ingredients;
        [SerializeField] RecipeData[] recipes;
        [SerializeField] float ingredientScale = 0.85f;
        [SerializeField] float recipeScale = 0.5f;
        [SerializeField] float padding = 18f;
        [SerializeField] int ingredientColumns = 6;
        [SerializeField] int recipeColumns = 5;
        [SerializeField] bool hideOtherPreviewUi = true;
        [SerializeField] GameObject[] hideOnPlay;

        void Awake()
        {
#if UNITY_EDITOR
            if (ingredients == null || ingredients.Length == 0)
                ingredients = LoadAllAssets<IngredientData>("Assets/04_Data/Ingredients");
            if (recipes == null || recipes.Length == 0)
                recipes = LoadAllAssets<RecipeData>("Assets/04_Data/Recipes");
#endif

            if (ingredientCardPrefab == null || recipeCardPrefab == null)
            {
                Debug.LogError($"{nameof(CardRenderGallery)} needs ingredient and recipe card prefabs.", this);
                enabled = false;
                return;
            }

            if (hideOtherPreviewUi)
                HidePreviewUi();

            Build();
        }

        void HidePreviewUi()
        {
            if (hideOnPlay != null)
            {
                foreach (GameObject go in hideOnPlay)
                    if (go != null) go.SetActive(false);
            }

            foreach (var hand in FindObjectsByType<IngredientHandView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                hand.gameObject.SetActive(false);
            foreach (var queue in FindObjectsByType<RecipeQueueView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                queue.gameObject.SetActive(false);
            foreach (var zone in FindObjectsByType<IngredientPlacementZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                zone.gameObject.SetActive(false);
            foreach (var addCard in FindObjectsByType<AddCardTest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                addCard.gameObject.SetActive(false);
            foreach (var belt in FindObjectsByType<ConveyorBelt>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                belt.gameObject.SetActive(false);
        }

        void Build()
        {
            RectTransform root = GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();

            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(root, false);
            Stretch(scrollRect);
            var scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = new Color(0.07f, 0.09f, 0.11f, 1f);
            scrollImage.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            RectTransform viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(scrollRect, false);
            Stretch(viewport);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var vertical = contentGo.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(24, 24, 24, 24);
            vertical.spacing = 28f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            AddSectionLabel(content, $"Ingredients ({ingredients?.Length ?? 0})");
            BuildIngredientGrid(content);

            AddSectionLabel(content, $"Recipes ({recipes?.Length ?? 0})");
            BuildRecipeGrid(content);

            Debug.Log(
                $"[CardRenderGallery] Rendered {ingredients?.Length ?? 0} ingredients and {recipes?.Length ?? 0} recipes.",
                this);
        }

        void BuildIngredientGrid(Transform parent)
        {
            if (ingredients == null || ingredients.Length == 0) return;

            Vector2 prefabSize = ((RectTransform)ingredientCardPrefab.transform).rect.size;
            RectTransform grid = CreateGrid(
                parent, "Ingredient Grid", ingredientColumns, padding, prefabSize * ingredientScale);

            foreach (IngredientData data in ingredients)
            {
                if (data == null) continue;
                RectTransform cell = CreateCell(grid, prefabSize * ingredientScale);
                IngredientCardUI card = Instantiate(ingredientCardPrefab, cell, false);
                PrepareCardRect((RectTransform)card.transform, prefabSize, ingredientScale);
                DisableCardInteraction(card.gameObject);
                card.SetData(data);
            }
        }

        void BuildRecipeGrid(Transform parent)
        {
            if (recipes == null || recipes.Length == 0) return;

            Vector2 prefabSize = ((RectTransform)recipeCardPrefab.transform).rect.size;
            RectTransform grid = CreateGrid(
                parent, "Recipe Grid", recipeColumns, padding, prefabSize * recipeScale);

            foreach (RecipeData data in recipes)
            {
                if (data == null) continue;
                RectTransform cell = CreateCell(grid, prefabSize * recipeScale);
                RecipeCardUI card = Instantiate(recipeCardPrefab, cell, false);
                PrepareCardRect((RectTransform)card.transform, prefabSize, recipeScale);
                DisableCardInteraction(card.gameObject);
                card.SetData(data);
            }
        }

        static RectTransform CreateGrid(
            Transform parent, string name, int columns, float spacing, Vector2 cellSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.cellSize = cellSize;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        static RectTransform CreateCell(Transform parent, Vector2 size)
        {
            var go = new GameObject("Cell", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        static void PrepareCardRect(RectTransform rect, Vector2 prefabSize, float scale)
        {
            rect.localScale = Vector3.one * scale;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = prefabSize;
            rect.anchoredPosition = Vector2.zero;
        }

        static void DisableCardInteraction(GameObject card)
        {
            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            foreach (var behaviour in card.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IngredientCardUI or RecipeCardUI) continue;
                // Leave layout/visual components alone; only kill drag helpers if present.
                if (behaviour.GetType().Name.Contains("Drag") || behaviour.GetType().Name.Contains("Pointer"))
                    behaviour.enabled = false;
            }
        }

        static void AddSectionLabel(Transform parent, string text)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 28f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.92f, 0.9f, 0.84f, 1f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            var layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

#if UNITY_EDITOR
        static T[] LoadAllAssets<T>(string folder) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            var list = new List<T>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        [MenuItem("Tools/Cards/Open Card Render Gallery")]
        static void OpenGalleryScene()
        {
            const string scenePath = "Assets/00_Scenes/CardPreview.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogError($"Missing scene: {scenePath}");
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Debug.Log("[CardRenderGallery] Opened CardPreview. Press Play to render all cards.");
        }
#endif
    }
}
