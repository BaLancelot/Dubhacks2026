using TMPro;
using UnityEngine;

namespace SushiDojo.UI
{
    /// <summary>
    /// Fakes perspective on TextMeshPro text: letters get taller toward one side, matching a tilted panel.
    /// Height is scaled around the rect's vertical center, blending from Left Scale to Right Scale across the text.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(TMP_Text))]
    public class PerspectiveText : MonoBehaviour
    {
        [Tooltip("Height multiplier at the text's left edge.")]
        [SerializeField, Min(0f)] float leftScale = 1.2f;
        [Tooltip("Height multiplier at the text's right edge.")]
        [SerializeField, Min(0f)] float rightScale = 0.75f;
        [Tooltip("How much higher the right end sits than the left (UI units). Follows a sloped panel.")]
        [SerializeField] float slope;

        TMP_Text text;

        void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            text.OnPreRenderText += ApplyPerspective;
            text.SetVerticesDirty();
        }

        void OnDisable()
        {
            if (text == null) return;
            text.OnPreRenderText -= ApplyPerspective;
            text.SetVerticesDirty();
        }

        void OnValidate()
        {
            if (text != null) text.SetVerticesDirty();
        }

        /// <summary>Runs on the generated mesh right before TMP uploads it.</summary>
        void ApplyPerspective(TMP_TextInfo info)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            for (int m = 0; m < info.materialCount; m++)
            {
                TMP_MeshInfo mesh = info.meshInfo[m];
                for (int v = 0; v < mesh.vertexCount; v++)
                {
                    minX = Mathf.Min(minX, mesh.vertices[v].x);
                    maxX = Mathf.Max(maxX, mesh.vertices[v].x);
                }
            }
            if (minX > maxX) return;

            float centerY = text.rectTransform.rect.center.y;
            for (int m = 0; m < info.materialCount; m++)
            {
                Vector3[] vertices = info.meshInfo[m].vertices;
                for (int v = 0; v < info.meshInfo[m].vertexCount; v++)
                {
                    Vector3 position = vertices[v];
                    float t = Mathf.InverseLerp(minX, maxX, position.x);
                    float scale = Mathf.Lerp(leftScale, rightScale, t);
                    position.y = centerY + (position.y - centerY) * scale + (t - 0.5f) * slope;
                    vertices[v] = position;
                }
            }
        }
    }
}
