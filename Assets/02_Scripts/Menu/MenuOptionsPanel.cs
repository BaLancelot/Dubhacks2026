using System;
using SushiDojo.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Menu
{
    /// <summary>
    /// Options screen. Build it into the scene with Tools > Menu > Create Options Panel to edit its look;
    /// if the scene has none, MainMenu builds a default one at runtime.
    /// </summary>
    public class MenuOptionsPanel : MonoBehaviour
    {
        static readonly Color TextColor = new(0.3f, 0.17f, 0.1f);
        static readonly Color PanelColor = new(0.95f, 0.91f, 0.84f);
        static readonly Color TrackColor = new(0.55f, 0.42f, 0.3f);
        static readonly Color FillColor = new(0.85f, 0.55f, 0.3f);
        static readonly Color HandleColor = new(0.85f, 0.82f, 0.76f);
        static readonly Color ButtonColor = new(0.96f, 0.9f, 0.78f);

        // Label tilt matching each tilted panel sprite (left scale, right scale, slope).
        static readonly Vector3 Panel1Perspective = new(1.24f, 0.76f, -2f);
        static readonly Vector3 Panel2Perspective = new(0.75f, 1.25f, 3f);
        static readonly Vector3 Panel4Perspective = new(0.73f, 1.27f, -1.5f);

        [SerializeField] Button sfxToggle;
        [SerializeField] TMP_Text sfxLabel;
        [SerializeField] TMP_Text volumeLabel;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text musicLabel;
        [SerializeField] Slider musicSlider;
        [SerializeField] Button backButton;

        public event Action BackPressed;

        /// <summary>Sprites and font for the built layout. Any can be null (falls back to flat colors).</summary>
        public struct Skin
        {
            public Sprite panel, title, toggle, back, knob;
            public TMP_FontAsset font;
        }

        void Awake()
        {
            if (!TryGetComponent(out ResultsPresentation _)) AddOpeningAnimation();
            if (sfxToggle != null) sfxToggle.onClick.AddListener(() =>
            {
                GameOptions.SetSfx(!GameOptions.SfxEnabled);
                Refresh();
            });
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(value =>
            {
                GameOptions.SetSfxVolume(value);
                Refresh();
            });
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(value =>
            {
                GameOptions.SetMusicVolume(value);
                Refresh();
            });
            if (backButton != null) backButton.onClick.AddListener(() => BackPressed?.Invoke());
        }

        void OnEnable() => Refresh();

        /// <summary>
        /// Gates-open + boing intro (gentle, no stars), wired to whatever this screen currently holds,
        /// so hand-edited layouts in the scene get it without rebuilding.
        /// </summary>
        void AddOpeningAnimation()
        {
            Transform anchor = sfxToggle != null ? sfxToggle.transform : backButton != null ? backButton.transform : null;
            var board = (anchor != null ? anchor.parent : transform.Find("Panel")) as RectTransform;
            if (board == null) return;

            var title = board.Find("Title") as RectTransform;
            var buttons = new System.Collections.Generic.List<RectTransform>();
            if (sfxToggle != null) buttons.Add((RectTransform)sfxToggle.transform);
            if (backButton != null) buttons.Add((RectTransform)backButton.transform);
            var contents = new System.Collections.Generic.List<RectTransform>();
            foreach (Component part in new Component[] { volumeLabel, sfxSlider, musicLabel, musicSlider })
                if (part != null) contents.Add((RectTransform)part.transform);

            gameObject.AddComponent<ResultsPresentation>().Setup(
                GetComponent<Image>(), board, title != null ? new[] { title } : new RectTransform[0],
                buttons.ToArray(), contents.ToArray(), subtle: true);
        }

        void Refresh()
        {
            if (sfxLabel != null) sfxLabel.text = "SFX: " + (GameOptions.SfxEnabled ? "ON" : "OFF");
            if (volumeLabel != null) volumeLabel.text = $"SFX volume: {Mathf.RoundToInt(GameOptions.SfxVolume * 100)}%";
            if (musicLabel != null) musicLabel.text = $"Music volume: {Mathf.RoundToInt(GameOptions.MusicVolume * 100)}%";
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameOptions.SfxVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameOptions.MusicVolume);
        }

        /// <summary>
        /// Builds the default options layout under parent (hidden), laid out like the match results screen:
        /// black backdrop, option panel, title banner on top, SFX toggle, two volume sliders, BACK at the bottom.
        /// Works in edit mode too, so the editor tool can save it into the scene. Listeners are hooked up in Awake.
        /// </summary>
        public static MenuOptionsPanel Create(Transform parent, Skin skin = default)
        {
            var root = new GameObject("Options", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.layer = parent.gameObject.layer;
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            Stretch(rect);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            if (skin.font == null)
            {
                // Match the menu's font when there is one.
                TMP_Text menuText = parent.GetComponentInChildren<TMP_Text>(true);
                skin.font = menuText != null ? menuText.font : TMP_Settings.defaultFontAsset;
            }

            var panel = root.AddComponent<MenuOptionsPanel>();
            RectTransform board = SpriteBox(rect, "Panel", skin.panel, PanelColor, new Vector2(0f, -10f), new Vector2(1000f, 703f));

            RectTransform title = SpriteBox(board, "Title", skin.title, ButtonColor, new Vector2(0f, 310f), new Vector2(480f, 185f));
            Stretch(Label(title, skin.font, "Label", "OPTIONS", 48, Panel1Perspective).rectTransform, 40f, 20f);

            panel.sfxToggle = SpriteButton(board, skin.toggle, skin.font, "SfxToggle", "SFX: ON",
                new Vector2(0f, 150f), new Vector2(360f, 148f), 36, Panel2Perspective, out panel.sfxLabel);

            panel.volumeLabel = Label(board, skin.font, "SfxVolumeLabel", "SFX volume", 30, null);
            Place(panel.volumeLabel.rectTransform, new Vector2(0f, 45f), new Vector2(700f, 50f));
            panel.sfxSlider = Slider(board, skin.knob, "SfxVolumeSlider", new Vector2(0f, -5f));

            panel.musicLabel = Label(board, skin.font, "MusicVolumeLabel", "Music volume", 30, null);
            Place(panel.musicLabel.rectTransform, new Vector2(0f, -85f), new Vector2(700f, 50f));
            panel.musicSlider = Slider(board, skin.knob, "MusicVolumeSlider", new Vector2(0f, -135f));

            panel.backButton = SpriteButton(board, skin.back, skin.font, "Back", "BACK",
                new Vector2(0f, -310f), new Vector2(340f, 133f), 40, Panel4Perspective, out _);

            return panel;
        }

        static RectTransform SpriteBox(RectTransform parent, string name, Sprite sprite, Color fallback, Vector2 pos, Vector2 size)
        {
            Image image = Child<Image>(parent, name);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallback;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            Place(image.rectTransform, pos, size);
            return image.rectTransform;
        }

        static Button SpriteButton(RectTransform parent, Sprite sprite, TMP_FontAsset font, string name, string title,
            Vector2 pos, Vector2 size, int fontSize, Vector3 perspective, out TMP_Text label)
        {
            RectTransform rect = SpriteBox(parent, name, sprite, ButtonColor, pos, size);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = Label(rect, font, "Label", title, fontSize, perspective);
            Stretch(label.rectTransform, 30f, 15f);
            return button;
        }

        static TMP_Text Label(RectTransform parent, TMP_FontAsset font, string name, string value, int size, Vector3? perspective)
        {
            var text = Child<TextMeshProUGUI>(parent, name);
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = TextColor;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (perspective.HasValue)
            {
                Vector3 p = perspective.Value;
                text.gameObject.AddComponent<PerspectiveText>().Configure(p.x, p.y, p.z);
            }
            return text;
        }

        /// <summary>Horizontal 0..1 slider: track, fill and a knob sprite.</summary>
        static Slider Slider(RectTransform parent, Sprite knob, string name, Vector2 pos)
        {
            RectTransform rect = Child<RectTransform>(parent, name);
            Place(rect, pos, new Vector2(600f, 20f));

            Image track = Child<Image>(rect, "Background");
            track.color = TrackColor;
            Stretch(track.rectTransform);

            RectTransform fillArea = Child<RectTransform>(rect, "Fill Area");
            Stretch(fillArea);
            Image fill = Child<Image>(fillArea, "Fill");
            fill.color = FillColor;
            Stretch(fill.rectTransform);

            RectTransform handleArea = Child<RectTransform>(rect, "Handle Slide Area");
            Stretch(handleArea, 18f, 0f);
            Image handle = Child<Image>(handleArea, "Handle");
            handle.sprite = knob;
            handle.color = knob != null ? Color.white : HandleColor;
            handle.preserveAspect = knob != null;
            // The slider only drives the horizontal anchors; keep the knob centered vertically at a fixed size.
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(34f, 72f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        static T Child<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return typeof(T) == typeof(RectTransform) ? go.GetComponent<T>() : go.AddComponent<T>();
        }

        static void Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        static void Place(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }
    }
}
