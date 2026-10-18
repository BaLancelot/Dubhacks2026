using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SushiDojo.UI
{
    /// <summary>
    /// Live world-space speech bubble mesh, rebuilt every frame (also in the editor):
    /// a rounded body (optionally sized to its text), a tail that grows toward the speaker,
    /// a sprite-textured fill, and a hand-drawn outline whose width varies like pen pressure.
    /// The bubble lies on its local XY plane. Material 0 = fill, material 1 = outline.
    /// For a Canvas version see UISpeechBubble.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SpeechBubble : MonoBehaviour
    {
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [Header("Body")]
        [SerializeField] BubbleBodyShape shape = BubbleBodyShape.RoundedRect;
        [Tooltip("Body size when there is no Fit Text.")]
        [SerializeField] Vector2 size = new(3f, 1.6f);
        [SerializeField, Min(0f)] float cornerRadius = 0.5f;
        [Tooltip("Points around the outline. More = smoother.")]
        [SerializeField, Min(16)] int resolution = 120;

        [Header("Text (optional)")]
        [Tooltip("TextMeshPro inside the bubble. The body grows to fit it plus padding. Empty = the first TextMeshPro under the bubble.")]
        [SerializeField] TMP_Text fitText;
        [SerializeField] Vector2 textPadding = new(0.35f, 0.25f);
        [Tooltip("Text wraps beyond this width. 0 = never wraps; the bubble just keeps growing.")]
        [SerializeField, Min(0f)] float maxTextWidth = 6f;

        [Header("Tail")]
        [Tooltip("Who is talking. The tail grows toward it.")]
        [SerializeField] Transform speaker;
        [Tooltip("Used when Speaker is empty, in the bubble's local space.")]
        [SerializeField] Vector2 speakerOffset = new(-1.5f, -2.2f);
        [SerializeField, Min(0.01f)] float tailBaseWidth = 0.6f;
        [Tooltip("The tail stops this far short of the speaker.")]
        [SerializeField, Min(0f)] float tailGap = 0.3f;
        [SerializeField, Min(0f)] float tailMaxLength = 2.5f;
        [Tooltip("Sideways bend, as a fraction of the tail length. The sign picks the side (for a speaker on the left).")]
        [SerializeField, Range(-1f, 1f)] float tailBend = 0.25f;
        [Tooltip("Flip the bend when the speaker is on the right, so the tail looks the same mirrored.")]
        [SerializeField] bool mirrorBendOnRight = true;
        [SerializeField, Min(1)] int tailSegments = 10;

        [Header("Fill")]
        [Tooltip("Stretched over the whole bubble.")]
        [SerializeField] Sprite fillSprite;
        [SerializeField] Color fillColor = Color.white;

        [Header("Outline (hand drawn)")]
        [SerializeField] Color outlineColor = new(0.16f, 0.1f, 0.08f, 1f);
        [SerializeField, Min(0f)] float outlineWidth = 0.08f;
        [Tooltip("How much the stroke width varies, like pen pressure. 0 = even.")]
        [SerializeField, Range(0f, 1f)] float pressureVariation = 0.5f;
        [Tooltip("How quickly the pressure changes along the stroke (per world unit).")]
        [SerializeField, Min(0f)] float pressureFrequency = 1.6f;
        [Tooltip("Sideways wobble of the line, in world units.")]
        [SerializeField, Min(0f)] float wobble = 0.025f;
        [SerializeField, Min(0f)] float wobbleFrequency = 3f;
        [Tooltip("Redraws the wobble this many times per second for a boiling hand-drawn look. 0 = still.")]
        [SerializeField, Min(0f)] float boilFps;
        [SerializeField] int seed = 7;

        [Header("Sorting")]
        [SerializeField] string sortingLayerName = "Default";
        [SerializeField] int sortingOrder = 50;

        readonly List<Vector2> body = new();
        readonly List<Vector2> outline = new();
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<Vector2> uvs = new();
        readonly List<int> fillTriangles = new();
        readonly List<int> strokeTriangles = new();

        Mesh mesh;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;

        void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block ??= new MaterialPropertyBlock();
            if (mesh == null)
            {
                mesh = new Mesh { name = "SpeechBubble", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            GetComponent<MeshFilter>().sharedMesh = mesh;
            Rebuild();
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        void LateUpdate()
        {
            Rebuild();
        }

        void OnValidate()
        {
            if (isActiveAndEnabled && mesh != null) Rebuild();
        }

        public TMP_Text FitText => fitText;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;

        /// <summary>Text the body grows to fit.</summary>
        public void SetFitText(TMP_Text text)
        {
            fitText = text;
        }

        /// <summary>Sets who is talking at runtime.</summary>
        public void SetSpeaker(Transform target)
        {
            speaker = target;
        }

        void Rebuild()
        {
            BubbleGeometry.BuildBody(body, shape, FitBodyToText(), cornerRadius, resolution);

            Vector2 target = speaker != null ? (Vector2)transform.InverseTransformPoint(speaker.position) : speakerOffset;
            int tailStart = BubbleGeometry.BuildOutline(body, target, tailBaseWidth, tailGap, tailMaxLength,
                tailBend, mirrorBendOnRight, tailSegments, outline, out int tailEnd);

            float seedOffset = BubbleGeometry.SeedOffset(seed, boilFps);
            BubbleGeometry.ApplyWobble(outline, wobble, wobbleFrequency, seedOffset);

            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            fillTriangles.Clear();
            strokeTriangles.Clear();

            BubbleGeometry.BuildFill(outline, tailStart, tailEnd, BubbleGeometry.SpriteUvRect(fillSprite), fillColor,
                vertices, colors, uvs, fillTriangles);
            BubbleGeometry.BuildStroke(outline, outlineWidth, pressureVariation, pressureFrequency, seedOffset, seed,
                outlineColor, vertices, colors, uvs, strokeTriangles);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(fillTriangles, 0);
            mesh.SetTriangles(strokeTriangles, 1);
            mesh.RecalculateBounds();

            ApplyRendererSettings();
        }

        Vector2 FitBodyToText()
        {
            TMP_Text text = fitText != null ? fitText : GetComponentInChildren<TMP_Text>();
            if (text == null) return size;
            return BubbleTextFit.Fit(text, maxTextWidth) + textPadding * 2f;
        }

        void ApplyRendererSettings()
        {
            if (meshRenderer == null) return;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;

            if (fillSprite == null || meshRenderer.sharedMaterials.Length == 0) return;
            meshRenderer.GetPropertyBlock(block, 0);
            block.SetTexture(MainTexId, fillSprite.texture);
            meshRenderer.SetPropertyBlock(block, 0);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 target = speaker != null ? speaker.position : transform.TransformPoint(speakerOffset);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target, 0.1f);
        }
    }
}
