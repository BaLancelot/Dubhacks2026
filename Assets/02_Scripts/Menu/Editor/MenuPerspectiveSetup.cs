using SushiDojo.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SushiDojo.Menu.EditorTools
{
    /// <summary>
    /// Adds PerspectiveText to each main menu button label, tuned to its tilted panel
    /// (heights measured from Panel1-4: which side is taller and by how much).
    /// </summary>
    public static class MenuPerspectiveSetup
    {
        // Button name, left scale, right scale, slope.
        static readonly (string name, float left, float right, float slope)[] Settings =
        {
            ("HOST", 1.24f, 0.76f, -2f),
            ("JOIN", 0.75f, 1.25f, 3f),
            ("OPTIONS", 1.25f, 0.75f, 8f),
            ("QUIT", 0.73f, 1.27f, -1.5f),
        };

        [MenuItem("Tools/Menu/Apply Perspective Text")]
        static void Apply()
        {
            int applied = 0;
            foreach (var (name, left, right, slope) in Settings)
            {
                GameObject button = GameObject.Find(name);
                TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
                if (label == null)
                {
                    Debug.LogWarning($"[MenuPerspective] No button named {name} with a TextMeshPro label in the open scene.");
                    continue;
                }

                if (!label.TryGetComponent(out PerspectiveText perspective))
                    perspective = Undo.AddComponent<PerspectiveText>(label.gameObject);

                var so = new SerializedObject(perspective);
                so.FindProperty("leftScale").floatValue = left;
                so.FindProperty("rightScale").floatValue = right;
                so.FindProperty("slope").floatValue = slope;
                so.ApplyModifiedProperties();
                applied++;
            }

            Debug.Log($"[MenuPerspective] Applied to {applied} button labels. Save the scene to keep it.");
        }
    }
}
