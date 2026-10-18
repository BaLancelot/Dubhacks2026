using System.Collections;
using SushiDojo.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI
{
    /// <summary>
    /// Opening animation for the match results screen, played every time it is shown:
    /// black backdrop fades in -> the panel swings open from the middle like gates ->
    /// the title boings in with a star burst -> the buttons boing in one after another.
    /// Only scale and alpha are animated, so positions set in the scene are kept.
    /// </summary>
    public class ResultsPresentation : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("Full-screen black backdrop. Its alpha fades from 0 to its scene value.")]
        [SerializeField] Image backdrop;
        [SerializeField] RectTransform panel;
        [Tooltip("Title text and its banner; they pop together.")]
        [SerializeField] RectTransform[] title;
        [Tooltip("Pop in order, top to bottom / left to right.")]
        [SerializeField] RectTransform[] buttons;
        [Tooltip("Text that appears with the panel (scores, rematch status).")]
        [SerializeField] RectTransform[] panelContents;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] float backdropFade = 0.3f;
        [SerializeField, Min(0.01f)] float panelOpen = 0.4f;
        [Tooltip("0 = closed, 1 = open. Overshoot past 1 for a gate swing.")]
        [SerializeField] AnimationCurve panelOpenCurve = new(
            new Keyframe(0f, 0f), new Keyframe(0.65f, 1.08f), new Keyframe(0.85f, 0.97f), new Keyframe(1f, 1f));
        [SerializeField, Min(0.01f)] float popDuration = 0.4f;
        [SerializeField] AnimationCurve popCurve = new(
            new Keyframe(0f, 0f), new Keyframe(0.45f, 1.25f), new Keyframe(0.7f, 0.9f), new Keyframe(0.87f, 1.04f), new Keyframe(1f, 1f));
        [Tooltip("Gap between the title and each button popping in.")]
        [SerializeField, Min(0f)] float popStagger = 0.15f;

        [Header("Star Burst")]
        [SerializeField] Sprite starSprite;
        [SerializeField] Vector2Int starCount = new(6, 9);
        [SerializeField] Vector2 starDistance = new(170f, 300f);
        [SerializeField] Vector2 starSize = new(30f, 60f);
        [SerializeField, Min(0.01f)] float starDuration = 0.6f;
        [SerializeField] float starSpin = 240f;
        [SerializeField] Color starColor = new(1f, 0.92f, 0.45f, 1f);

        float backdropAlpha = -1f;
        Image[] stars;

        /// <summary>
        /// Points the animation at a screen built in code (e.g. the options panel). No star burst.
        /// Subtle = smaller overshoot and quicker timing.
        /// </summary>
        public void Setup(Image backdropImage, RectTransform panelRect, RectTransform[] titleParts,
            RectTransform[] buttonParts, RectTransform[] contents, bool subtle)
        {
            backdrop = backdropImage;
            panel = panelRect;
            title = titleParts;
            buttons = buttonParts;
            panelContents = contents;
            starSprite = null;
            if (subtle)
            {
                backdropFade = 0.2f;
                panelOpen = 0.3f;
                popDuration = 0.28f;
                popStagger = 0.08f;
                panelOpenCurve = SwingCurve(1.02f);
                popCurve = PopCurve(1.06f);
            }

            // Added with AddComponent, OnEnable already ran before these parts were known: start over now.
            if (isActiveAndEnabled)
            {
                if (backdropAlpha < 0f && backdrop != null) backdropAlpha = backdrop.color.a;
                StopAllCoroutines();
                StartCoroutine(Play());
            }
        }

        static AnimationCurve SwingCurve(float overshoot) => new(
            new Keyframe(0f, 0f), new Keyframe(0.65f, overshoot),
            new Keyframe(0.85f, 1f - (overshoot - 1f) * 0.4f), new Keyframe(1f, 1f));

        static AnimationCurve PopCurve(float overshoot) => new(
            new Keyframe(0f, 0f), new Keyframe(0.45f, overshoot), new Keyframe(0.7f, 1f - (overshoot - 1f) * 0.4f),
            new Keyframe(0.87f, 1f + (overshoot - 1f) * 0.16f), new Keyframe(1f, 1f));

        void OnEnable()
        {
            if (backdropAlpha < 0f && backdrop != null) backdropAlpha = backdrop.color.a;
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        void OnDisable()
        {
            // Leave everything in its finished state if hidden mid-animation.
            SetBackdrop(1f);
            SetScale(panel, Vector3.one);
            SetScale(title, 1f);
            SetScale(buttons, 1f);
            SetScale(panelContents, 1f);
            SetHoverScaling(title, true);
            SetHoverScaling(buttons, true);
            if (stars != null) foreach (Image star in stars) if (star != null) star.enabled = false;
        }

        IEnumerator Play()
        {
            SetBackdrop(0f);
            SetScale(panel, new Vector3(0f, 1f, 1f));
            SetScale(panelContents, 0f);
            SetScale(title, 0f);
            SetScale(buttons, 0f);
            // MenuButton's hover grow sets the scale every frame; pause it until each part has popped in.
            SetHoverScaling(title, false);
            SetHoverScaling(buttons, false);

            yield return Animate(backdropFade, t => SetBackdrop(t));

            yield return Animate(panelOpen, t =>
            {
                float open = panelOpenCurve.Evaluate(t);
                // Gates swing out from the center: width opens, with a small vertical give.
                SetScale(panel, new Vector3(open, Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(open)), 1f));
                SetScale(panelContents, Mathf.Clamp01((t - 0.6f) / 0.4f));
            });

            if (title.Length > 0) StartCoroutine(StarBurst(title[0]));
            StartCoroutine(Pop(title));
            foreach (RectTransform button in buttons)
            {
                yield return new WaitForSeconds(popStagger);
                StartCoroutine(Pop(new[] { button }));
            }
        }

        IEnumerator Pop(RectTransform[] targets)
        {
            yield return Animate(popDuration, t => SetScale(targets, popCurve.Evaluate(t)));
            SetHoverScaling(targets, true);
        }

        static void SetHoverScaling(RectTransform[] targets, bool on)
        {
            if (targets == null) return;
            foreach (RectTransform target in targets)
                if (target != null && target.TryGetComponent(out MenuButton hover)) hover.enabled = on;
        }

        IEnumerator StarBurst(RectTransform origin)
        {
            if (starSprite == null || origin == null) yield break;
            EnsureStars();

            int count = Mathf.Min(stars.Length, Random.Range(starCount.x, starCount.y + 1));
            var directions = new Vector2[count];
            var distances = new float[count];
            var spins = new float[count];
            Vector2 center = origin.anchoredPosition;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                distances[i] = Random.Range(starDistance.x, starDistance.y);
                spins[i] = (Random.value < 0.5f ? -1f : 1f) * starSpin;
                float size = Random.Range(starSize.x, starSize.y);
                stars[i].rectTransform.sizeDelta = new Vector2(size, size);
                stars[i].enabled = true;
            }

            yield return Animate(starDuration, t =>
            {
                float travel = 1f - (1f - t) * (1f - t);
                float scale = Mathf.Sin(t * Mathf.PI);
                for (int i = 0; i < count; i++)
                {
                    RectTransform star = stars[i].rectTransform;
                    star.anchoredPosition = center + directions[i] * (distances[i] * travel);
                    star.localScale = Vector3.one * scale;
                    star.localRotation = Quaternion.Euler(0f, 0f, spins[i] * t * starDuration);
                    stars[i].color = new Color(starColor.r, starColor.g, starColor.b, starColor.a * (1f - t * t));
                }
            });

            for (int i = 0; i < count; i++) stars[i].enabled = false;
        }

        void EnsureStars()
        {
            if (stars != null) return;
            stars = new Image[starCount.y];
            Transform parent = title.Length > 0 && title[0] != null ? title[0].parent : transform;
            for (int i = 0; i < stars.Length; i++)
            {
                var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                var image = go.GetComponent<Image>();
                image.sprite = starSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.enabled = false;
                stars[i] = image;
            }
        }

        static IEnumerator Animate(float duration, System.Action<float> step)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                step(time / duration);
                yield return null;
            }
            step(1f);
        }

        void SetBackdrop(float amount)
        {
            if (backdrop == null || backdropAlpha < 0f) return;
            Color c = backdrop.color;
            c.a = backdropAlpha * amount;
            backdrop.color = c;
        }

        static void SetScale(RectTransform target, Vector3 scale)
        {
            if (target != null) target.localScale = scale;
        }

        static void SetScale(RectTransform[] targets, float scale)
        {
            if (targets == null) return;
            foreach (RectTransform target in targets) SetScale(target, Vector3.one * scale);
        }
    }
}
