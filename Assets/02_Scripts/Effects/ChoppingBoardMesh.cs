using System.Collections.Generic;
using UnityEngine;

namespace SushiDojo.Effects
{
    /// <summary>
    /// Builds a flat-shaded chopping board: rounded slab with a stadium-shaped handle hole.
    /// Board lies on local XZ (top faces +Y). Updates a BoxCollider on the same object if present.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter))]
    public class ChoppingBoardMesh : MonoBehaviour
    {
        const float Epsilon = 1e-5f;
        const float HalfTurn = Mathf.PI;
        const float QuarterTurn = Mathf.PI * 0.5f;

        [Header("Board")]
        [SerializeField, Min(0.01f)] float width = 7.6f;
        [SerializeField, Min(0.01f)] float depth = 4.8f;
        [SerializeField, Min(0.001f)] float thickness = 0.24f;
        [SerializeField, Min(0f)] float cornerRadius = 0.3f;
        [SerializeField, Min(1)] int cornerSegments = 4;

        [Header("Handle Hole")]
        [Tooltip("Hole center on the board (X along width, Y along depth).")]
        [SerializeField] Vector2 holeCenter = new(2.75f, 0.2f);
        [Tooltip("Hole length along the board depth.")]
        [SerializeField, Min(0.01f)] float holeLength = 2.4f;
        [SerializeField, Min(0.01f)] float holeWidth = 0.55f;
        [SerializeField, Min(1)] int holeEndSegments = 6;

        [Header("Look")]
        [SerializeField] Color topColor = Color.white;
        [SerializeField] Color sideColor = new(0.83f, 0.77f, 0.7f, 1f);
        [Tooltip("Texture V range used on the side walls (samples a thin strip of the wood).")]
        [SerializeField, Range(0f, 1f)] float sideUvHeight = 0.1f;

        readonly List<Vector3> vertices = new();
        readonly List<Vector3> normals = new();
        readonly List<Vector2> uvs = new();
        readonly List<Color> colors = new();
        readonly List<int> triangles = new();

        Mesh mesh;
        bool isDirty = true;

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
                mesh = new Mesh { name = "ChoppingBoard", hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();

            List<Vector2> outer = BuildOuterOutline();
            List<Vector2> hole = BuildHoleOutline();
            float halfThickness = thickness * 0.5f;

            BuildCaps(outer, hole, halfThickness);
            BuildWalls(outer, halfThickness, pointIntoHole: false);
            BuildWalls(hole, halfThickness, pointIntoHole: true);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            if (TryGetComponent(out BoxCollider box))
            {
                box.center = Vector3.zero;
                box.size = new Vector3(width, thickness, depth);
            }
        }

        // ---------- Outlines (2D: x = width axis, y = depth axis), counter-clockwise ----------

        List<Vector2> BuildOuterOutline()
        {
            float radius = Mathf.Min(cornerRadius, width * 0.5f, depth * 0.5f);
            float innerX = width * 0.5f - radius;
            float innerY = depth * 0.5f - radius;
            var corners = new[]
            {
                new Vector2(innerX, innerY),
                new Vector2(-innerX, innerY),
                new Vector2(-innerX, -innerY),
                new Vector2(innerX, -innerY),
            };

            var points = new List<Vector2>();
            for (int c = 0; c < corners.Length; c++)
                AddArc(points, corners[c], radius, c * QuarterTurn, QuarterTurn, cornerSegments);
            return points;
        }

        List<Vector2> BuildHoleOutline()
        {
            float radius = holeWidth * 0.5f;
            float straight = Mathf.Max(holeLength * 0.5f - radius, 0f);
            var points = new List<Vector2>();
            AddArc(points, holeCenter + Vector2.up * straight, radius, 0f, HalfTurn, holeEndSegments);
            AddArc(points, holeCenter - Vector2.up * straight, radius, HalfTurn, HalfTurn, holeEndSegments);
            return points;
        }

        static void AddArc(List<Vector2> points, Vector2 center, float radius, float startAngle, float sweep, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + sweep * i / segments;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        // ---------- Top & bottom: ring between outer outline and hole, swept around the hole center ----------

        void BuildCaps(List<Vector2> outer, List<Vector2> hole, float halfThickness)
        {
            var angles = new List<float>();
            foreach (Vector2 p in outer) angles.Add(AngleFrom(holeCenter, p));
            foreach (Vector2 p in hole) angles.Add(AngleFrom(holeCenter, p));
            angles.Sort();

            var outerHits = new List<Vector2>();
            var innerHits = new List<Vector2>();
            float previous = float.NegativeInfinity;
            foreach (float angle in angles)
            {
                if (angle - previous < Epsilon) continue;
                previous = angle;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                outerHits.Add(RayHit(outer, holeCenter, direction));
                innerHits.Add(RayHit(hole, holeCenter, direction));
            }

            int count = outerHits.Count;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                foreach (float y in new[] { halfThickness, -halfThickness })
                {
                    Vector3 normal = y > 0f ? Vector3.up : Vector3.down;
                    AddQuad(
                        ToBoard(outerHits[i], y), ToBoard(outerHits[next], y),
                        ToBoard(innerHits[next], y), ToBoard(innerHits[i], y),
                        normal, topColor, CapUv);
                }
            }
        }

        Vector2 CapUv(Vector3 p)
        {
            return new Vector2(p.x / width + 0.5f, p.z / depth + 0.5f);
        }

        static float AngleFrom(Vector2 origin, Vector2 point)
        {
            Vector2 d = point - origin;
            return Mathf.Atan2(d.y, d.x);
        }

        /// <summary>Nearest hit of a ray from inside a convex polygon.</summary>
        static Vector2 RayHit(List<Vector2> polygon, Vector2 origin, Vector2 direction)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 edge = polygon[(i + 1) % polygon.Count] - a;
                float denominator = Cross(direction, edge);
                if (Mathf.Abs(denominator) < Epsilon) continue;

                Vector2 toA = a - origin;
                float t = Cross(toA, edge) / denominator;
                float s = Cross(toA, direction) / denominator;
                if (t > 0f && s >= -Epsilon && s <= 1f + Epsilon && t < best) best = t;
            }
            return origin + direction * (float.IsPositiveInfinity(best) ? 0f : best);
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        // ---------- Side walls ----------

        void BuildWalls(List<Vector2> outline, float halfThickness, bool pointIntoHole)
        {
            float travelled = 0f;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];
                float length = Vector2.Distance(a, b);
                if (length < Epsilon) continue;

                // Outline is counter-clockwise, so the right-hand perpendicular points outward.
                Vector2 outward = new Vector2(b.y - a.y, a.x - b.x) / length;
                if (pointIntoHole) outward = -outward;
                Vector3 normal = new(outward.x, 0f, outward.y);

                float uStart = travelled / width;
                float uEnd = (travelled + length) / width;
                travelled += length;

                AddQuad(
                    ToBoard(a, halfThickness), ToBoard(b, halfThickness),
                    ToBoard(b, -halfThickness), ToBoard(a, -halfThickness),
                    normal, sideColor,
                    new Vector2(uStart, sideUvHeight), new Vector2(uEnd, sideUvHeight),
                    new Vector2(uEnd, 0f), new Vector2(uStart, 0f));
            }
        }

        // ---------- Mesh helpers ----------

        static Vector3 ToBoard(Vector2 p, float y)
        {
            return new Vector3(p.x, y, p.y);
        }

        void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Color color,
            System.Func<Vector3, Vector2> uvOf)
        {
            AddQuad(p0, p1, p2, p3, normal, color, uvOf(p0), uvOf(p1), uvOf(p2), uvOf(p3));
        }

        void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Color color,
            Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int start = vertices.Count;
            vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
            uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
                colors.Add(color);
            }

            AddTriangle(start, start + 1, start + 2, normal);
            AddTriangle(start, start + 2, start + 3, normal);
        }

        /// <summary>Adds a triangle wound so its front face points along the given normal.</summary>
        void AddTriangle(int a, int b, int c, Vector3 normal)
        {
            Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(faceNormal, normal) < 0f) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
