using System.Collections.Generic;
using SushiDojo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI.EditorTools
{
    /// <summary>
    /// Replaces every legacy UI Text in the open scenes with TextMeshProUGUI.
    /// Keeps text, size, color, alignment, style, wrapping and auto size,
    /// re-adds PerspectiveText with the same settings, and re-points scene references to the new component.
    /// </summary>
    public static class LegacyTextToTMPConverter
    {
        [MenuItem("Tools/UI/Convert Legacy Text To TextMeshPro")]
        static void ConvertOpenScenes()
        {
            Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (texts.Length == 0)
            {
                Debug.Log("[TMP Convert] No legacy Text in the open scenes.");
                return;
            }

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Undo.SetCurrentGroupName("Convert Legacy Text To TextMeshPro");
            int group = Undo.GetCurrentGroup();

            foreach (Text text in texts) Convert(text, behaviours);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[TMP Convert] Converted {texts.Length} Text component(s). Save the scene to keep it.");
        }

        static void Convert(Text text, MonoBehaviour[] behaviours)
        {
            GameObject target = text.gameObject;
            int oldId = text.GetInstanceID();

            string content = text.text;
            int fontSize = text.fontSize;
            Color color = text.color;
            TextAnchor alignment = text.alignment;
            FontStyle style = text.fontStyle;
            bool richText = text.supportRichText;
            bool raycastTarget = text.raycastTarget;
            bool wrap = text.horizontalOverflow == HorizontalWrapMode.Wrap;
            bool autoSize = text.resizeTextForBestFit;
            int minSize = text.resizeTextMinSize;
            int maxSize = text.resizeTextMaxSize;

            // Remember who points at this Text so they can point at the TMP version afterwards.
            var references = new List<(SerializedObject owner, string path)>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour.gameObject == target && behaviour is BaseMeshEffect) continue;
                var owner = new SerializedObject(behaviour);
                SerializedProperty property = owner.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceInstanceIDValue == oldId)
                        references.Add((owner, property.propertyPath));
            }

            string perspectiveJson = null;
            if (target.TryGetComponent(out PerspectiveText perspective))
            {
                perspectiveJson = EditorJsonUtility.ToJson(perspective);
                Undo.DestroyObjectImmediate(perspective);
            }

            foreach (BaseMeshEffect effect in target.GetComponents<BaseMeshEffect>())
            {
                Debug.LogWarning($"[TMP Convert] Removed {effect.GetType().Name} from {target.name} (doesn't work with TMP).", target);
                Undo.DestroyObjectImmediate(effect);
            }

            Undo.DestroyObjectImmediate(text);
            TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(target);

            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = ToTmpAlignment(alignment);
            tmp.fontStyle = ToTmpStyle(style);
            tmp.richText = richText;
            tmp.raycastTarget = raycastTarget;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = autoSize;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;

            if (perspectiveJson != null)
            {
                PerspectiveText restored = Undo.AddComponent<PerspectiveText>(target);
                EditorJsonUtility.FromJsonOverwrite(perspectiveJson, restored);
            }

            foreach ((SerializedObject owner, string path) in references)
            {
                owner.Update();
                SerializedProperty property = owner.FindProperty(path);
                if (property == null) continue;
                property.objectReferenceValue = tmp;
                owner.ApplyModifiedProperties();
                if (property.objectReferenceValue != tmp)
                    Debug.LogWarning($"[TMP Convert] {owner.targetObject.name}.{path} can't hold a TMP text. Change its type to TMP_Text.", owner.targetObject);
            }
        }

        static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };

        static FontStyles ToTmpStyle(FontStyle style) => style switch
        {
            FontStyle.Bold => FontStyles.Bold,
            FontStyle.Italic => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _ => FontStyles.Normal,
        };
    }
}
