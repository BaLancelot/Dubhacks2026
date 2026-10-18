using System;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.Menu
{
    /// <summary>Temporary options UI, built at runtime so existing menu scenes need no wiring.</summary>
    public class MenuOptionsPanel : MonoBehaviour
    {
        Text sfxLabel, volumeLabel, musicLabel;

        public static MenuOptionsPanel Create(Transform parent, Action back)
        {
            var root = new GameObject("Options", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.15f, 0.1f);
            rect.anchorMax = new Vector2(0.85f, 0.9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.08f, 0.97f);
            var panel = root.AddComponent<MenuOptionsPanel>();
            panel.Label("OPTIONS", 0.9f, 32);
            panel.sfxLabel = panel.Button("", 0.74f, () =>
            {
                GameOptions.SetSfx(!GameOptions.SfxEnabled);
                panel.Refresh();
            });
            panel.volumeLabel = panel.Label("", 0.59f, 22);
            panel.Button("−", 0.47f, () => panel.ChangeVolume(-0.1f), 0.3f, 0.15f);
            panel.Button("+", 0.47f, () => panel.ChangeVolume(0.1f), 0.7f, 0.15f);
            panel.musicLabel = panel.Label("", 0.32f, 22);
            panel.Button("−", 0.20f, () => panel.ChangeMusicVolume(-0.1f), 0.3f, 0.15f);
            panel.Button("+", 0.20f, () => panel.ChangeMusicVolume(0.1f), 0.7f, 0.15f);
            panel.Button("BACK", 0.07f, back);
            panel.Refresh();
            root.SetActive(false);
            return panel;
        }

        void ChangeVolume(float delta)
        {
            GameOptions.SetSfxVolume(GameOptions.SfxVolume + delta);
            Refresh();
        }

        void ChangeMusicVolume(float delta)
        {
            GameOptions.SetMusicVolume(GameOptions.MusicVolume + delta);
            Refresh();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            if (sfxLabel == null || volumeLabel == null || musicLabel == null) return;
            sfxLabel.text = "SFX: " + (GameOptions.SfxEnabled ? "ON" : "OFF");
            musicLabel.text = $"Music volume: {Mathf.RoundToInt(GameOptions.MusicVolume * 100)}%";
            volumeLabel.text = $"SFX volume: {Mathf.RoundToInt(GameOptions.SfxVolume * 100)}%";
        }

        Text Label(string value, float y, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            Place((RectTransform)go.transform, y, 0.5f, 0.9f);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.95f, 0.83f);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            return text;
        }

        Text Button(string title, float y, Action clicked, float x = 0.5f, float width = 0.75f)
        {
            var go = new GameObject("OptionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            Place((RectTransform)go.transform, y, x, width);
            go.GetComponent<Image>().color = new Color(0.4f, 0.22f, 0.18f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => clicked());
            var label = Label(title, y, 24);
            label.transform.SetParent(go.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return label;
        }

        static void Place(RectTransform rect, float y, float x, float width)
        {
            rect.anchorMin = new Vector2(x - width / 2f, y - 0.055f);
            rect.anchorMax = new Vector2(x + width / 2f, y + 0.055f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
