using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SushiDojo.UI
{
    /// <summary>
    /// Draws the hand-drawn outline of the parent UISpeechBubble. Lives on a child that covers the parent
    /// exactly (kept stretched automatically) and draws on top of the fill.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public class UISpeechBubbleOutline : MaskableGraphic
    {
        readonly List<Vector2> points = new();
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<Vector2> uvs = new();
        readonly List<int> triangles = new();

        UISpeechBubble bubble;

#if UNITY_EDITOR
        // The outline shouldn't block clicks; off by default when the component is added.
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif

        protected override void OnEnable()
        {
            base.OnEnable();
            bubble = GetComponentInParent<UISpeechBubble>();
        }

        void Update()
        {
            if (bubble == null) bubble = GetComponentInParent<UISpeechBubble>();
            if (bubble == null) return;

            MatchParent();
            // Children rebuild after their parent, so this picks up the bubble's fresh outline.
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (bubble == null) bubble = GetComponentInParent<UISpeechBubble>();
            if (bubble == null || bubble.Outline.Count == 0) return;

            points.Clear();
            points.AddRange(bubble.Outline);
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();
            BubbleGeometry.BuildStroke(points, bubble.OutlineWidth, bubble.PressureVariation, bubble.PressureFrequency,
                bubble.CurrentSeedOffset, bubble.Seed, bubble.OutlineColor * color, vertices, colors, uvs, triangles);

            for (int i = 0; i < vertices.Count; i++) vh.AddVert(vertices[i], colors[i], uvs[i]);
            for (int i = 0; i < triangles.Count; i += 3) vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }

        /// <summary>Same local space as the bubble: stretched over it with the same pivot.</summary>
        void MatchParent()
        {
            RectTransform self = rectTransform;
            RectTransform parent = bubble.rectTransform;
            if (self.parent != parent) return;
            if (self.anchorMin == Vector2.zero && self.anchorMax == Vector2.one && self.pivot == parent.pivot
                && self.offsetMin == Vector2.zero && self.offsetMax == Vector2.zero) return;

            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.pivot = parent.pivot;
            self.offsetMin = Vector2.zero;
            self.offsetMax = Vector2.zero;
        }
    }
}
