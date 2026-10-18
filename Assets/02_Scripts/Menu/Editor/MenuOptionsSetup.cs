using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Menu.EditorTools
{
    /// <summary>
    /// Saves the options screen into the open MainMenu scene so it can be edited by hand, styled like the
    /// match results screen (OptionPanel board, Panel1 title, Panel2 toggle, Panel4 BACK, Nobe slider knobs),
    /// and wires it into MainMenu. Running it again replaces the existing one.
    /// </summary>
    public static class MenuOptionsSetup
    {
        const string UiFolder = "Assets/01_Arts/Sprites/UI/";

        [MenuItem("Tools/Menu/Create Options Panel")]
        static void Create()
        {
            var mainMenu = Object.FindAnyObjectByType<MainMenu>();
            if (mainMenu == null)
            {
                EditorUtility.DisplayDialog("Options Panel", "Open the MainMenu scene first.", "OK");
                return;
            }

            var so = new SerializedObject(mainMenu);
            var buttonPanel = (RectTransform)so.FindProperty("buttonPanel").objectReferenceValue;
            if (buttonPanel == null)
            {
                EditorUtility.DisplayDialog("Options Panel", "MainMenu has no Button Panel assigned.", "OK");
                return;
            }

            var existing = (MenuOptionsPanel)so.FindProperty("optionsPanel").objectReferenceValue;
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var skin = new MenuOptionsPanel.Skin
            {
                panel = LoadSprite("OptionPanel"),
                title = LoadSprite("Panel1"),
                toggle = LoadSprite("Panel2"),
                back = LoadSprite("Panel4"),
                knob = LoadSprite("Nobe"),
            };
            MenuOptionsPanel panel = MenuOptionsPanel.Create(buttonPanel.parent, skin);
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Options Panel");

            // Same hover grow + hover/click sounds as the main menu buttons.
            MenuButton template = buttonPanel.GetComponentInChildren<MenuButton>(true);
            if (template != null)
                foreach (Button button in panel.GetComponentsInChildren<Button>(true))
                    EditorUtility.CopySerialized(template, button.gameObject.AddComponent<MenuButton>());

            so.FindProperty("optionsPanel").objectReferenceValue = panel;
            so.ApplyModifiedProperties();

            Selection.activeGameObject = panel.gameObject;
            Debug.Log("[MenuOptions] Options panel added (inactive; enable it to edit). Save the scene to keep it.", panel);
        }

        /// <summary>First sprite in a UI texture (works for Single and Multiple sprite modes).</summary>
        static Sprite LoadSprite(string name)
        {
            string path = $"{UiFolder}{name}.png";
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) Debug.LogWarning($"[MenuOptions] No sprite at {path}; using a flat color instead.");
            return sprite;
        }
    }
}
