using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI
{
    /// <summary>
    /// Canvas version of the speech bubble: draws the sprite-textured fill and keeps the outline
    /// (UISpeechBubbleOutline on a child) in sync. Size follows this RectTransform, or Fit Text plus padding.
    /// The tail points at a UI element, a world object, or a fixed local offset (in that order).
    /// Rebuilds every frame, also in the editor.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public class UISpeechBubble : MaskableGraphic
    {
        [Header("Body")]
        [SerializeField] BubbleBodyShape shape = BubbleBodyShape.RoundedRect;
        [SerializeField, Min(0f)] float cornerRadius = 40f;
        [Tooltip("Points around the outline. More = smoother.")]
        [SerializeField, Min(16)] int resolution = 120;

        [Header("Text (optional)")]
        [Tooltip("Text inside the bubble. This RectTransform resizes to fit it plus padding. Empty = the first TextMeshPro under the bubble.")]
        [SerializeField] TMP_Text fitText;
        [SerializeField] Vector2 textPadding = new(36f, 24f);
        [Tooltip("Text wraps beyond this width. 0 = never wraps; the bubble just keeps growing.")]
        [SerializeField, Min(0f)] float maxTextWidth = 500f;

        [Header("Tail")]
        [Tooltip("UI element that is talking. Takes priority.")]
        [SerializeField] RectTransform speakerUI;
        [Tooltip("World object that is talking (seen through Speaker Camera).")]
        [SerializeField] Transform speakerWorld;
        [Tooltip("Falls back to Camera.main.")]
        [SerializeField] Camera speakerCamera;
        [Tooltip("Used when no speaker is set, in this RectTransform's local space.")]
        [SerializeField] Vector2 speakerOffset = new(-150f, -200f);
        [SerializeField, Min(0.01f)] float tailBaseWidth = 60f;
        [Tooltip("The tail stops this far short of the speaker.")]
        [SerializeField, Min(0f)] float tailGap = 25f;
        [SerializeField, Min(0f)] float tailMaxLength = 220f;
        [Tooltip("Sideways bend, as a fraction of the tail length. The sign picks the side (for a speaker on the left).")]
        [SerializeField, Range(-1f, 1f)] float tailBend = 0.25f;
        [Tooltip("Flip the bend when the speaker is on the right, so the tail looks the same mirrored.")]
        [SerializeField] bool mirrorBendOnRight = true;
        [SerializeField, Min(1)] int tailSegments = 10;

        [Header("Fill")]
        [Tooltip("Stretched over the whole bubble.")]
        [SerializeField] Sprite fillSprite;

        [Header("Outline (hand drawn)")]
        [SerializeField] Color outlineColor = new(0.16f, 0.1f, 0.08f, 1f);
        [SerializeField, Min(0f)] float outlineWidth = 7f;
        [Tooltip("How much the stroke width varies, like pen pressure. 0 = even.")]
        [SerializeField, Range(0f, 1f)] float pressureVariation = 0.5f;
        [Tooltip("How quickly the pressure changes along the stroke (per canvas unit).")]
        [SerializeField, Min(0f)] float pressureFrequency = 0.016f;
        [Tooltip("Sideways wobble of the line, in canvas units.")]
        [SerializeField, Min(0f)] float wobble = 2.5f;
        [SerializeField, Min(0f)] float wobbleFrequency = 0.03f;
        [Tooltip("Redraws the wobble this many times per second for a boiling hand-drawn look. 0 = still.")]
        [SerializeField, Min(0f)] float boilFps;
        [SerializeField] int seed = 7;

        readonly List<Vector2> body = new();
        readonly List<Vector2> outline = new();
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<Vector2> uvs = new();
        readonly List<int> triangles = new();

        /// <summary>Last built outline in this RectTransform's local space (pivot origin).</summary>
        public IReadOnlyList<Vector2> Outline => outline;
        public Color OutlineColor => outlineColor;
        public float OutlineWidth => outlineWidth;
        public float PressureVariation => pressureVariation;
        public float PressureFrequency => pressureFrequency;
        public int Seed => seed;
        public float CurrentSeedOffset { get; private set; }
        /// <summary>Vertices produced by the last mesh build (for diagnosing an invisible bubble).</summary>
        public int LastVertexCount { get; private set; }

        public override Texture mainTexture => fillSprite != null ? fillSprite.texture : s_WhiteTexture;

#if UNITY_EDITOR
        // A bubble shouldn't block clicks; off by default when the component is added.
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif

        bool warnedNoCanvas;

        void Update()
        {
            // UI only renders under a Canvas; say so instead of silently drawing nothing.
            if (canvas == null)
            {
                if (!warnedNoCanvas)
                    Debug.LogWarning($"[{nameof(UISpeechBubble)}] '{name}' is not under a Canvas, so it can't render. Move it under a Canvas.", this);
                warnedNoCanvas = true;
                return;
            }
            warnedNoCanvas = false;

            FitToText();
            SetVerticesDirty();
        }

        public TMP_Text FitText => fitText;

        /// <summary>Text the bubble grows to fit.</summary>
        public void SetFitText(TMP_Text text)
        {
            fitText = text;
        }

        /// <summary>Points the tail at a UI element at runtime.</summary>
        public void SetSpeaker(RectTransform target)
        {
            speakerUI = target;
            speakerWorld = null;
        }

        /// <summary>Points the tail at a world object at runtime.</summary>
        public void SetSpeaker(Transform target)
        {
            speakerWorld = target;
            speakerUI = null;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVertexCount = 0;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            // Geometry is built around the rect center, then shifted to pivot-local coordinates.
            Vector2 center = rect.center;
            BubbleGeometry.BuildBody(body, shape, rect.size, cornerRadius, resolution);
            int tailStart = BubbleGeometry.BuildOutline(body, SpeakerLocal() - center, tailBaseWidth, tailGap,
                tailMaxLength, tailBend, mirrorBendOnRight, tailSegments, outline, out int tailEnd);

            CurrentSeedOffset = BubbleGeometry.SeedOffset(seed, boilFps);
            BubbleGeometry.ApplyWobble(outline, wobble, wobbleFrequency, CurrentSeedOffset);

            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();
            BubbleGeometry.BuildFill(outline, tailStart, tailEnd, BubbleGeometry.SpriteUvRect(fillSprite), color,
                vertices, colors, uvs, triangles);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 uv = uvs[i];
                vh.AddVert(new Vector3(vertices[i].x + center.x, vertices[i].y + center.y, 0f), (Color32)colors[i], new Vector4(uv.x, uv.y, 0f, 0f));
            }
            for (int i = 0; i < triangles.Count; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
            LastVertexCount = vh.currentVertCount;

            // The outline graphic (a child, rebuilt right after this) reads this, in pivot-local coordinates like the fill.
            for (int i = 0; i < outline.Count; i++) outline[i] += center;
        }

        /// <summary>Speaker position in this RectTransform's local space.</summary>
        Vector2 SpeakerLocal()
        {
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

            if (speakerUI != null)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, speakerUI.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out Vector2 local))
                    return local;
            }

            if (speakerWorld != null)
            {
                Camera worldCamera = speakerCamera != null ? speakerCamera : Camera.main;
                if (worldCamera != null)
                {
                    Vector2 screen = worldCamera.WorldToScreenPoint(speakerWorld.position);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out Vector2 local))
                        return local;
                }
            }

            return rectTransform.rect.center + speakerOffset;
        }

        void FitToText()
        {
            TMP_Text text = fitText != null ? fitText : GetComponentInChildren<TMP_Text>();
            if (text == null) return;

            Vector2 wanted = BubbleTextFit.Fit(text, maxTextWidth) + textPadding * 2f;
            if (rectTransform.rect.size != wanted)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, wanted.x);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, wanted.y);
            }
        }
    }
}
