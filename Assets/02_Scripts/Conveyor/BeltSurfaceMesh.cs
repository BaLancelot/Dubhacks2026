using UnityEngine;

namespace SushiDojo.Conveyor
{
    /// <summary>
    /// Builds a trapezoid surface so texture lines fan out toward the viewer (fake perspective).
    /// Mesh spans 1 unit on X at its middle row, -0.5..0.5 on Y (y = -0.5 is the near edge).
    /// Texture U runs along the belt, so scrolling keeps the perspective correct.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter))]
    public class BeltSurfaceMesh : MonoBehaviour
    {
        [Tooltip("Width change from the middle. Near edge = 1 + s, far edge = 1 - s.")]
        [SerializeField, Range(0f, 0.9f)] float perspectiveSpread = 0.15f;
        [Tooltip("Columns along the belt. More columns = less texture warping.")]
        [SerializeField, Min(1)] int columns = 32;

        [Header("Layout")]
        [Tooltip("How tall the top surface looks on screen.")]
        [SerializeField, Min(0.01f)] float topDepth = 0.9f;
        [Tooltip("Thickness of the front face.")]
        [SerializeField, Min(0f)] float frontHeight = 0.4f;
        [Tooltip("Sized to the near edge and snapped right below the top surface.")]
        [SerializeField] Transform frontFace;

        Mesh mesh;
        bool isDirty = true;

        public float NearEdgeScale => 1f + perspectiveSpread;

        void OnEnable()
        {
            isDirty = true;
        }

        void OnValidate()
        {
            isDirty = true;
        }

        void Update()
        {
            if (isDirty) Rebuild();
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        void Rebuild()
        {
            isDirty = false;

            if (mesh == null)
            {
                mesh = new Mesh { name = "BeltSurface", hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            int rowLength = columns + 1;
            var vertices = new Vector3[rowLength * 2];
            var uvs = new Vector2[rowLength * 2];
            var triangles = new int[columns * 6];

            float nearHalf = 0.5f * (1f + perspectiveSpread);
            float farHalf = 0.5f * (1f - perspectiveSpread);

            for (int i = 0; i < rowLength; i++)
            {
                float t = (float)i / columns;
                vertices[i] = new Vector3(Mathf.Lerp(-nearHalf, nearHalf, t), -0.5f, 0f);
                vertices[i + rowLength] = new Vector3(Mathf.Lerp(-farHalf, farHalf, t), 0.5f, 0f);
                uvs[i] = new Vector2(t, 0f);
                uvs[i + rowLength] = new Vector2(t, 1f);
            }

            for (int i = 0; i < columns; i++)
            {
                int nearLeft = i;
                int nearRight = i + 1;
                int farLeft = i + rowLength;
                int farRight = i + 1 + rowLength;
                int tri = i * 6;

                triangles[tri] = nearLeft;
                triangles[tri + 1] = farLeft;
                triangles[tri + 2] = nearRight;
                triangles[tri + 3] = nearRight;
                triangles[tri + 4] = farLeft;
                triangles[tri + 5] = farRight;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            ApplyLayout();
        }

        void ApplyLayout()
        {
            Vector3 topScale = transform.localScale;
            topScale.y = topDepth;
            transform.localScale = topScale;

            if (frontFace == null) return;

            Vector3 frontScale = frontFace.localScale;
            frontScale.x = NearEdgeScale;
            frontScale.y = frontHeight;
            frontFace.localScale = frontScale;

            Vector3 frontPosition = frontFace.localPosition;
            frontPosition.x = transform.localPosition.x;
            frontPosition.y = transform.localPosition.y - (topDepth + frontHeight) * 0.5f;
            frontFace.localPosition = frontPosition;
        }
    }
}
