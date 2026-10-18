using UnityEngine;

namespace SushiDojo.Board
{
    /// <summary>
    /// Sizes an invisible floor and four walls (child BoxColliders) around a play area
    /// in this transform's local space. Put the colliders on the Ignore Raycast layer.
    /// </summary>
    [ExecuteAlways]
    public class TableBounds : MonoBehaviour
    {
        [Tooltip("Play area on the table: X = width, Y = depth.")]
        [SerializeField] Vector2 areaSize = new(16f, 8f);
        [Tooltip("Play area center on the table: X = width, Y = depth. Lower Y pulls the back wall toward the viewer.")]
        [SerializeField] Vector2 areaCenter;
        [Tooltip("Table surface height in local Y.")]
        [SerializeField] float surfaceHeight = -0.12f;
        [SerializeField, Min(0.01f)] float wallHeight = 6f;
        [SerializeField, Min(0.01f)] float colliderThickness = 1f;

        [SerializeField] BoxCollider floor;
        [SerializeField] BoxCollider wallLeft;
        [SerializeField] BoxCollider wallRight;
        [SerializeField] BoxCollider wallBack;
        [SerializeField] BoxCollider wallFront;

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
            if (!isDirty) return;
            isDirty = false;
            Apply();
        }

        void Apply()
        {
            float t = colliderThickness;
            float halfWidth = areaSize.x * 0.5f;
            float halfDepth = areaSize.y * 0.5f;
            float wallCenterY = surfaceHeight + wallHeight * 0.5f;
            float spanX = areaSize.x + t * 2f;
            float spanZ = areaSize.y + t * 2f;

            float cx = areaCenter.x;
            float cz = areaCenter.y;

            Set(floor, new Vector3(cx, surfaceHeight - t * 0.5f, cz), new Vector3(spanX, t, spanZ));
            Set(wallLeft, new Vector3(cx - (halfWidth + t * 0.5f), wallCenterY, cz), new Vector3(t, wallHeight, spanZ));
            Set(wallRight, new Vector3(cx + halfWidth + t * 0.5f, wallCenterY, cz), new Vector3(t, wallHeight, spanZ));
            Set(wallBack, new Vector3(cx, wallCenterY, cz + halfDepth + t * 0.5f), new Vector3(spanX, wallHeight, t));
            Set(wallFront, new Vector3(cx, wallCenterY, cz - (halfDepth + t * 0.5f)), new Vector3(spanX, wallHeight, t));
        }

        static void Set(BoxCollider box, Vector3 center, Vector3 size)
        {
            if (box == null) return;
            box.center = center;
            box.size = size;
        }
    }
}
