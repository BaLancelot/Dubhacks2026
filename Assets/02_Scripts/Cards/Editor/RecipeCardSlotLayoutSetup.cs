using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Cards.EditorTools
{
    /// <summary>
    /// Lays the RecipeCard ingredient slots out as up to 3 per row, 2 rows:
    /// a Vertical Layout Group holding two Horizontal Layout Group rows. Each row centers what's shown,
    /// so RecipeCardUI can show 3 to 6 slots (e.g. 4 = three on top, one centered below).
    /// Safe to run again: it reuses the container and rows if they exist.
    /// </summary>
    public static class RecipeCardSlotLayoutSetup
    {
        const string PrefabPath = "Assets/03_Prefabs/RecipeCard.prefab";
        const string ContainerName = "Ingredient Slots";
        const string RowName = "Row";
        const string SlotPrefix = "Ingredient Slot ";
        const int SlotsPerRow = 3;
        const int RowCount = 2;

        static readonly Vector2 ContainerAnchorMin = new(0.15f, 0.05f);
        static readonly Vector2 ContainerAnchorMax = new(0.85f, 0.335f);
        const float Spacing = 4f;
        const float SlotSize = 40f;
        const float SlotMinSize = 20f;

        [MenuItem("Tools/Cards/Recipe Card: 3x2 Ingredient Slots")]
        static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RectTransform container = FindOrCreateContainer(root.transform);
                var rows = new RectTransform[RowCount];
                for (int r = 0; r < RowCount; r++) rows[r] = FindOrCreateRow(container, r);

                RectTransform[] slots = FindSlots(root.transform);
                for (int i = 0; i < slots.Length; i++)
                {
                    RectTransform slot = slots[i];
                    slot.SetParent(rows[Mathf.Min(i / SlotsPerRow, RowCount - 1)], false);
                    slot.anchorMin = slot.anchorMax = Vector2.zero;
                    slot.anchoredPosition = Vector2.zero;

                    if (!slot.TryGetComponent(out LayoutElement element)) element = slot.gameObject.AddComponent<LayoutElement>();
                    element.minWidth = element.minHeight = SlotMinSize;
                    element.preferredWidth = element.preferredHeight = SlotSize;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[RecipeCard] {slots.Length} ingredient slots laid out {SlotsPerRow} per row in '{ContainerName}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static RectTransform FindOrCreateContainer(Transform root)
        {
            Transform existing = root.Find(ContainerName);
            GameObject go = existing != null ? existing.gameObject : CreateChild(ContainerName, root);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = ContainerAnchorMin;
            rect.anchorMax = ContainerAnchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // An earlier single-row version used a horizontal layout here.
            if (go.TryGetComponent(out HorizontalLayoutGroup oldRow)) Object.DestroyImmediate(oldRow);
            if (!go.TryGetComponent(out VerticalLayoutGroup layout)) layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        static RectTransform FindOrCreateRow(RectTransform container, int index)
        {
            string name = $"{RowName} {index + 1}";
            Transform existing = container.Find(name);
            GameObject go = existing != null ? existing.gameObject : CreateChild(name, container);
            go.transform.SetSiblingIndex(index);

            if (!go.TryGetComponent(out HorizontalLayoutGroup layout)) layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Slots in name order (Ingredient Slot 1..6), wherever they currently are.</summary>
        static RectTransform[] FindSlots(Transform root)
        {
            var slots = new List<RectTransform>();
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name.StartsWith(SlotPrefix)) slots.Add(rect);
            slots.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
            return slots.ToArray();
        }
    }
}
