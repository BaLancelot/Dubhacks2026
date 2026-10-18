using TMPro;
using UnityEditor;
using UnityEngine;

namespace SushiDojo.UI.EditorTools
{
    /// <summary>
    /// Builds the UI speech bubble prefab for the local player's score:
    /// UISpeechBubble (fill) + Outline child + ScoreText (TextMeshProUGUI) + BubbleScoreText set to Local.
    /// Running it again rebuilds the prefab (instances keep their overrides).
    /// </summary>
    public static class UISpeechBubblePrefabBuilder
    {
        const string PrefabPath = "Assets/03_Prefabs/SpeechBubbleUI.prefab";
        const string FontPath = "Assets/01_Arts/Fonts/bordersprite SDF.asset";
        const string FillSpritePath = "Assets/01_Arts/Sprites/Texture/Floor.png";
        const float FontSize = 64f;
        const int LocalScoreOwner = 1;
        static readonly Vector2 StartSize = new(260f, 160f);
        static readonly Color TextColor = new(0.3f, 0.17f, 0.1f, 1f);

        [MenuItem("Tools/UI/Create Player Score Speech Bubble (UI)")]
        static void Build()
        {
            var root = new GameObject("SpeechBubbleUI", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = StartSize;

                UISpeechBubble bubble = root.AddComponent<UISpeechBubble>();
                var outlineObject = new GameObject("Outline", typeof(RectTransform));
                outlineObject.transform.SetParent(root.transform, false);
                outlineObject.AddComponent<UISpeechBubbleOutline>();

                var textObject = new GameObject("ScoreText", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font != null) text.font = font;
                text.text = "0";
                text.fontSize = FontSize;
                text.color = TextColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;

                var bubbleSo = new SerializedObject(bubble);
                bubbleSo.FindProperty("fitText").objectReferenceValue = text;
                bubbleSo.FindProperty("fillSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(FillSpritePath);
                bubbleSo.ApplyModifiedPropertiesWithoutUndo();

                BubbleScoreText score = root.AddComponent<BubbleScoreText>();
                var scoreSo = new SerializedObject(score);
                scoreSo.FindProperty("text").objectReferenceValue = text;
                scoreSo.FindProperty("owner").enumValueIndex = LocalScoreOwner;
                scoreSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SpeechBubbleUI] Saved {PrefabPath}. Drop it under a Canvas and set its speaker.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
