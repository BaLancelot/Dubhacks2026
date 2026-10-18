using System.Collections.Generic;
using SushiDojo.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI
{
    /// <summary>Screen-space ingredient hits shown on the player who did not throw.</summary>
    public class BoardSplatOverlay : MonoBehaviour
    {
        const float FlightDuration = 0.42f;
        const float HitDuration = 0.14f;
        const float Linger = 0.45f;
        const float FadeDuration = 0.55f;
        const float Stagger = 0.07f;

        readonly List<Splat> splats = new();

        sealed class Splat
        {
            public RectTransform Rect;
            public Image Image;
            public float Age;
            public float Delay;
            public Vector2 Start;
            public Vector2 Impact;
            public float StartAngle;
            public float ImpactAngle;
        }

        public static void Play(IReadOnlyList<Sprite> sprites)
        {
            if (sprites == null || sprites.Count == 0)
                return;

            BoardSplatOverlay overlay = FindFirstObjectByType<BoardSplatOverlay>();
            if (overlay == null)
                overlay = Create();
            overlay.Add(sprites);
        }

        static BoardSplatOverlay Create()
        {
            IngredientHandView hand = FindFirstObjectByType<IngredientHandView>();
            Canvas host = hand != null ? hand.GetComponentInParent<Canvas>()?.rootCanvas : null;

            GameObject overlayObject = new GameObject("Opponent Splatters", typeof(RectTransform));
            if (host != null)
                overlayObject.transform.SetParent(host.transform, false);
            else
            {
                Canvas canvas = overlayObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90;
            }

            RectTransform rect = (RectTransform)overlayObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            return overlayObject.AddComponent<BoardSplatOverlay>();
        }

        void Add(IReadOnlyList<Sprite> sprites)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null)
                    continue;

                GameObject splatObject = new GameObject("Splat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                splatObject.transform.SetParent(transform, false);

                Image image = splatObject.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;

                RectTransform rect = (RectTransform)splatObject.transform;
                float size = Random.Range(110f, 170f);
                Vector2 impact = new Vector2(Random.Range(-340f, 340f), Random.Range(-120f, 160f));
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * size;
                rect.anchoredPosition = impact * 0.12f;
                rect.localScale = Vector3.one * 0.12f;
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-80f, 80f));

                Color color = image.color;
                color.a = 0.35f;
                image.color = color;

                splats.Add(new Splat
                {
                    Rect = rect,
                    Image = image,
                    Delay = i * Stagger,
                    Start = impact * 0.12f,
                    Impact = impact,
                    StartAngle = rect.localEulerAngles.z,
                    ImpactAngle = Random.Range(-28f, 28f),
                });
            }
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            for (int i = splats.Count - 1; i >= 0; i--)
            {
                Splat splat = splats[i];
                if (splat.Rect == null)
                {
                    splats.RemoveAt(i);
                    continue;
                }

                splat.Age += delta;
                float flightTime = splat.Age - splat.Delay;
                if (flightTime < 0f)
                {
                    splat.Image.color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }

                float flight = Mathf.Clamp01(flightTime / FlightDuration);
                // Speeds up as it reaches the screen, like it's coming at the camera.
                float approach = flight * flight;
                splat.Rect.anchoredPosition = Vector2.LerpUnclamped(splat.Start, splat.Impact, approach);
                float spin = Mathf.LerpAngle(splat.StartAngle, splat.ImpactAngle, approach);
                splat.Rect.localRotation = Quaternion.Euler(0f, 0f, spin);

                float scale = Mathf.Lerp(0.12f, 1f, approach);
                float alpha = Mathf.Lerp(0.35f, 1f, approach);
                if (flight >= 1f)
                {
                    float hit = Mathf.Clamp01((flightTime - FlightDuration) / HitDuration);
                    float smack = Mathf.Sin(hit * Mathf.PI);
                    scale = 1f + smack * 0.22f;
                    alpha = 1f;
                }

                float sinceHit = flightTime - FlightDuration;
                if (sinceHit > Linger)
                    alpha *= 1f - Mathf.Clamp01((sinceHit - Linger) / FadeDuration);
                splat.Rect.localScale = Vector3.one * scale;
                Color color = splat.Image.color;
                color.a = alpha;
                splat.Image.color = color;

                if (sinceHit < Linger + FadeDuration)
                    continue;

                Destroy(splat.Rect.gameObject);
                splats.RemoveAt(i);
            }
        }
    }
}
