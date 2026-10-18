using SushiDojo.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Menu.EditorTools
{
    /// <summary>
    /// Builds the JOIN (room code) and HOST (lobby) panels next to the main menu buttons and wires them
    /// into MainMenu. Styling is copied from the existing HOST button. Running it again rebuilds both panels.
    /// </summary>
    public static class MenuLobbySetup
    {
        const string SpriteFolder = "Assets/01_Arts/Sprites/UI/";
        const string PlayerPrefabPath = "Assets/01_Arts/Model/Chef_Unity/ChefImpact.prefab";
        static readonly Color LightText = new(0.98f, 0.93f, 0.82f, 1f);
        static readonly Color DarkText = new(0.3f, 0.17f, 0.1f, 1f);
        static readonly Color BackdropColor = new(0f, 0f, 0f, 0.6f);

        // Panel sprite heights at width 400, matching each PNG's aspect ratio.
        static readonly float[] PanelHeights = { 154f, 164f, 170f, 157f };
        // Per-panel perspective for labels (left scale, right scale, slope), as on the main buttons.
        static readonly Vector3[] PanelPerspective =
        {
            new(1.24f, 0.76f, -2f), new(0.75f, 1.25f, 3f), new(1.25f, 0.75f, 8f), new(0.73f, 1.27f, -1.5f)
        };

        static Button templateButton;
        static TMP_Text templateLabel;

        [MenuItem("Tools/Menu/Create Host and Join Panels")]
        static void Create()
        {
            var mainMenu = Object.FindAnyObjectByType<MainMenu>();
            if (mainMenu == null)
            {
                EditorUtility.DisplayDialog("Host / Join Panels", "Open the MainMenu scene first.", "OK");
                return;
            }

            var menuSo = new SerializedObject(mainMenu);
            var buttonPanel = (RectTransform)menuSo.FindProperty("buttonPanel").objectReferenceValue;
            templateButton = buttonPanel != null ? buttonPanel.GetComponentInChildren<Button>(true) : null;
            templateLabel = templateButton != null ? templateButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (templateButton == null || templateLabel == null)
            {
                EditorUtility.DisplayDialog("Host / Join Panels", "MainMenu's Button Panel needs at least one button with a TextMeshPro label. Run Tools > UI > Convert Legacy Text To TextMeshPro first.", "OK");
                return;
            }

            RemoveExisting(buttonPanel.parent, "JoinPanel");
            RemoveExisting(buttonPanel.parent, "LobbyPanel");

            MenuJoinPanel join = BuildJoinPanel(buttonPanel);
            MenuLobbyPanel lobby = BuildLobbyPanel(buttonPanel);

            menuSo.FindProperty("joinPanel").objectReferenceValue = join;
            menuSo.FindProperty("lobbyPanel").objectReferenceValue = lobby;
            menuSo.ApplyModifiedProperties();

            Selection.activeGameObject = lobby.gameObject;
            Debug.Log("[MenuLobby] Join and Lobby panels created (hidden until HOST/JOIN). Save the scene to keep them.", lobby);
        }

        static MenuJoinPanel BuildJoinPanel(RectTransform buttonPanel)
        {
            RectTransform root = CreateScreen("JoinPanel", buttonPanel);

            CreateHeader(root, "Header", "JOIN", 1, 240f);
            TMP_InputField input = CreateInputField(root, "CodeInput", 2, 45f);
            Button connect = CreatePanelButton(root, "Connect", "CONNECT", 3, -140f);
            TMP_Text status = CreateText(root, "Status", "", 30, LightText, new Vector2(460f, 50f), -250f);
            Button back = CreateTextButton(root, "Back", "BACK", -320f);

            var panel = root.gameObject.AddComponent<MenuJoinPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("codeInput").objectReferenceValue = input;
            so.FindProperty("connectButton").objectReferenceValue = connect;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return panel;
        }

        static MenuLobbyPanel BuildLobbyPanel(RectTransform buttonPanel)
        {
            RectTransform root = CreateScreen("LobbyPanel", buttonPanel);
            CreateBackdrop(root);

            CreateHeader(root, "Header", "LOBBY", 0, 270f);
            TMP_Text roomCode = CreateText(root, "RoomCode", "", 36, LightText, new Vector2(460f, 50f), 165f);

            GameObject previewObject = CreateUIObject("OpponentPreview", root, new Vector2(340f, 340f), -5f);
            var raw = previewObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var preview = previewObject.AddComponent<LobbyPlayerPreview>();
            var previewSo = new SerializedObject(preview);
            previewSo.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            previewSo.ApplyModifiedPropertiesWithoutUndo();

            TMP_Text waiting = CreateText(root, "Waiting", "WAITING FOR PLAYER", 32, LightText, new Vector2(460f, 60f), -5f);
            Button start = CreatePanelButton(root, "GameStart", "START", 3, -215f);
            Button back = CreateTextButton(root, "Back", "BACK", -320f);

            var panel = root.gameObject.AddComponent<MenuLobbyPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("roomCodeText").objectReferenceValue = roomCode;
            so.FindProperty("waitingText").objectReferenceValue = waiting;
            so.FindProperty("opponentPreview").objectReferenceValue = preview;
            so.FindProperty("startButton").objectReferenceValue = start;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Semi-transparent black card behind the lobby contents, a bit larger than the panel.</summary>
        static void CreateBackdrop(RectTransform parent)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-40f, -30f);
            rect.offsetMax = new Vector2(40f, 30f);
            var image = go.AddComponent<Image>();
            image.color = BackdropColor;
            image.raycastTarget = false;
        }

        /// <summary>A screen root that sits exactly where the main buttons do.</summary>
        static RectTransform CreateScreen(string name, RectTransform buttonPanel)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(go, "Create Host and Join Panels");
            go.layer = buttonPanel.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(buttonPanel.parent, false);
            rect.SetSiblingIndex(buttonPanel.GetSiblingIndex() + 1);
            rect.anchorMin = buttonPanel.anchorMin;
            rect.anchorMax = buttonPanel.anchorMax;
            rect.pivot = buttonPanel.pivot;
            rect.anchoredPosition = buttonPanel.anchoredPosition;
            rect.sizeDelta = new Vector2(buttonPanel.sizeDelta.x, 700f);
            return rect;
        }

        static void CreateHeader(RectTransform parent, string name, string text, int panelIndex, float y)
        {
            GameObject go = CreateUIObject(name, parent, new Vector2(400f, PanelHeights[panelIndex]), y);
            Image image = AddPanelImage(go, panelIndex);
            image.raycastTarget = false;
            CopyShadow(go);
            CreateLabel(go.transform, text, panelIndex);
        }

        static Button CreatePanelButton(RectTransform parent, string name, string text, int panelIndex, float y)
        {
            GameObject go = CreateUIObject(name, parent, new Vector2(400f, PanelHeights[panelIndex]), y);
            Image image = AddPanelImage(go, panelIndex);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = templateButton.colors;
            CopyShadow(go);
            CopyMenuButton(go);
            CreateLabel(go.transform, text, panelIndex);
            return button;
        }

        static Button CreateTextButton(RectTransform parent, string name, string text, float y)
        {
            TMP_Text label = CreateText(parent, name, text, 34, LightText, new Vector2(220f, 60f), y);
            label.raycastTarget = true;
            var button = label.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            button.colors = templateButton.colors;
            CopyMenuButton(label.gameObject);
            return button;
        }

        static TMP_InputField CreateInputField(RectTransform parent, string name, int panelIndex, float y)
        {
            GameObject go = CreateUIObject(name, parent, new Vector2(400f, PanelHeights[panelIndex]), y);
            Image image = AddPanelImage(go, panelIndex);
            CopyShadow(go);

            // TMP_InputField needs a masked text area holding the placeholder and the text.
            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.layer = go.layer;
            var areaRect = (RectTransform)textArea.transform;
            areaRect.SetParent(go.transform, false);
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(30f, 10f);
            areaRect.offsetMax = new Vector2(-30f, -10f);

            TMP_Text placeholder = CreateStretchedText(areaRect, "Placeholder", "CODE", 44,
                new Color(DarkText.r, DarkText.g, DarkText.b, 0.45f), Vector2.zero);
            TMP_Text text = CreateStretchedText(areaRect, "Text", "", 52, DarkText, Vector2.zero);
            text.richText = false;

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = templateLabel.font;
            input.pointSize = text.fontSize;
            input.characterLimit = 12;
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = DarkText;
            input.customCaretColor = true;
            return input;
        }

        static void CreateLabel(Transform parent, string text, int panelIndex)
        {
            TMP_Text label = CreateStretchedText(parent, "Label", text, templateLabel.fontSize, templateLabel.color);
            var perspective = label.gameObject.AddComponent<PerspectiveText>();
            var so = new SerializedObject(perspective);
            Vector3 p = PanelPerspective[panelIndex];
            so.FindProperty("leftScale").floatValue = p.x;
            so.FindProperty("rightScale").floatValue = p.y;
            so.FindProperty("slope").floatValue = p.z;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static readonly Vector2 LabelPadding = new(30f, 10f);

        static TMP_Text CreateStretchedText(Transform parent, string name, string text, float size, Color color)
        {
            return CreateStretchedText(parent, name, text, size, color, LabelPadding);
        }

        static TMP_Text CreateStretchedText(Transform parent, string name, string text, float size, Color color, Vector2 padding)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
            return SetupText(go.AddComponent<TextMeshProUGUI>(), text, size, color);
        }

        static TMP_Text CreateText(RectTransform parent, string name, string text, float size, Color color, Vector2 rectSize, float y)
        {
            GameObject go = CreateUIObject(name, parent, rectSize, y);
            return SetupText(go.AddComponent<TextMeshProUGUI>(), text, size, color);
        }

        static TMP_Text SetupText(TMP_Text t, string text, float size, Color color)
        {
            t.font = templateLabel.font;
            t.fontSharedMaterial = templateLabel.fontSharedMaterial;
            t.fontStyle = templateLabel.fontStyle;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static GameObject CreateUIObject(string name, RectTransform parent, Vector2 size, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, y);
            return go;
        }

        static Image AddPanelImage(GameObject go, int panelIndex)
        {
            var image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}Panel{panelIndex + 1}.png");
            image.preserveAspect = true;
            return image;
        }

        static void CopyShadow(GameObject go)
        {
            if (!templateButton.TryGetComponent(out Shadow shadow)) return;
            EditorUtility.CopySerialized(shadow, go.AddComponent<Shadow>());
        }

        static void CopyMenuButton(GameObject go)
        {
            if (!templateButton.TryGetComponent(out MenuButton menuButton)) return;
            EditorUtility.CopySerialized(menuButton, go.AddComponent<MenuButton>());
        }

        static void RemoveExisting(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        }
    }
}
