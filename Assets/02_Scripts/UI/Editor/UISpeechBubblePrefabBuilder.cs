using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SushiDojo.UI.EditorTools
{
    /// <summary>
    /// Builds the UI speech bubble prefab for the local player's score:
    /// UISpeechBubble (fill) + Outline child + ScoreText (TextMeshProUGUI) + a Speaker target the tail points at,
    /// and BubbleScoreText set to the local score. A second menu drops it onto a Canvas in the open scene.
    /// </summary>
    public static class UISpeechBubblePrefabBuilder
    {
        const string PrefabPath = "Assets/03_Prefabs/SpeechBubbleUI.prefab";
        const string FontPath = "Assets/01_Arts/Fonts/bordersprite SDF.asset";
        const string FillSpritePath = "Assets/01_Arts/Sprites/Texture/Floor.png";
        const float FontSize = 64f;
        const int LocalScoreOwner = 1;
        static readonly Vector2 StartSize = new(260f, 160f);
        static readonly Vector2 SpeakerOffset = new(-150f, -200f);
        static readonly Vector2 ScenePosition = new(-600f, -150f);
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
                bubble.raycastTarget = false;

                var outlineObject = new GameObject("Outline", typeof(RectTransform));
                outlineObject.transform.SetParent(root.transform, false);
                outlineObject.AddComponent<UISpeechBubbleOutline>().raycastTarget = false;

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

                // Move this to the player (or set Speaker UI / Speaker World on the bubble).
                var speakerObject = new GameObject("Speaker (move me)", typeof(RectTransform));
                speakerObject.transform.SetParent(root.transform, false);
                ((RectTransform)speakerObject.transform).anchoredPosition = SpeakerOffset;

                var bubbleSo = new SerializedObject(bubble);
                bubbleSo.FindProperty("fitText").objectReferenceValue = text;
                bubbleSo.FindProperty("fillSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(FillSpritePath);
                bubbleSo.FindProperty("speakerUI").objectReferenceValue = speakerObject.transform;
                bubbleSo.ApplyModifiedPropertiesWithoutUndo();

                BubbleScoreText score = root.AddComponent<BubbleScoreText>();
                var scoreSo = new SerializedObject(score);
                scoreSo.FindProperty("text").objectReferenceValue = text;
                scoreSo.FindProperty("owner").enumValueIndex = LocalScoreOwner;
                scoreSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SpeechBubbleUI] Saved {PrefabPath}. Use Tools > UI > Add Player Score Speech Bubble To Scene.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/UI/Add Player Score Speech Bubble To Scene")]
        static void AddToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Build();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            Canvas canvas = FindTargetCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Speech Bubble", "No active Canvas in the open scene.", "OK");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Add Player Score Speech Bubble");
            ((RectTransform)instance.transform).anchoredPosition = ScenePosition;
            instance.transform.SetAsLastSibling();
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log($"[SpeechBubbleUI] Added under '{canvas.name}'. Drag 'Speaker (move me)' to the player, then save the scene.", instance);
        }

        /// <summary>The active root canvas holding the HUD if there is one, else the first active root canvas.</summary>
        static Canvas FindTargetCanvas()
        {
            Canvas fallback = null;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.gameObject.activeInHierarchy) continue;
                if (canvas.GetComponentInChildren<MultiplayerHUD>(true) != null) return canvas;
                fallback ??= canvas;
            }
            return fallback;
        }
    }
}
