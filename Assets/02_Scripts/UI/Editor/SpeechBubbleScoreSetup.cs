using TMPro;
using UnityEditor;
using UnityEngine;

namespace SushiDojo.UI.EditorTools
{
    /// <summary>
    /// Adds a TextMeshPro "ScoreText" inside the SpeechBubble prefab, makes the bubble fit around it,
    /// and adds BubbleScoreText so it shows the opponent's score. Safe to run again.
    /// </summary>
    public static class SpeechBubbleScoreSetup
    {
        const string PrefabPath = "Assets/03_Prefabs/SpeechBubble.prefab";
        const string FontPath = "Assets/01_Arts/Fonts/bordersprite SDF.asset";
        const string TextName = "ScoreText";
        const float FontSize = 8f;
        const float TowardCamera = -0.01f;
        const int SortingAboveBubble = 1;
        static readonly Color TextColor = new(0.3f, 0.17f, 0.1f, 1f);

        [MenuItem("Tools/UI/Speech Bubble: Add Opponent Score Text")]
        static void Apply()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                TextMeshPro text = FindOrCreateText(root.transform);

                if (root.TryGetComponent(out SpeechBubble bubble))
                {
                    var bubbleSo = new SerializedObject(bubble);
                    bubbleSo.FindProperty("fitText").objectReferenceValue = text;
                    text.sortingLayerID = SortingLayer.NameToID(bubbleSo.FindProperty("sortingLayerName").stringValue);
                    text.sortingOrder = bubbleSo.FindProperty("sortingOrder").intValue + SortingAboveBubble;
                    bubbleSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (!root.TryGetComponent(out BubbleScoreText score)) score = root.AddComponent<BubbleScoreText>();
                var scoreSo = new SerializedObject(score);
                scoreSo.FindProperty("text").objectReferenceValue = text;
                scoreSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[SpeechBubble] Opponent score text added. The bubble now fits around it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static TextMeshPro FindOrCreateText(Transform root)
        {
            Transform existing = root.Find(TextName);
            if (existing != null && existing.TryGetComponent(out TextMeshPro found)) return found;

            var go = new GameObject(TextName);
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 0f, TowardCamera);

            TextMeshPro text = go.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) text.font = font;
            text.text = "0";
            text.fontSize = FontSize;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}
