using UnityEngine;

namespace SushiDojo.Conveyor
{
    /// <summary>
    /// Repeats a belt face texture along its length and scrolls it in sync with the conveyor.
    /// Repeat length comes from <see cref="ConveyorBelt.TextureTileLength"/>.
    /// Expects a mesh whose U axis (0..1) spans the belt's length.
    /// The texture's Wrap Mode must be Repeat.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class BeltTexture : MonoBehaviour
    {
        [SerializeField] ConveyorBelt belt;
        [SerializeField] bool scrollWithBelt = true;
        [SerializeField] bool reverseScroll;
        [Tooltip("Repeats across the face's height (V).")]
        [SerializeField, Min(0.01f)] float tilingV = 1f;

        Material material;

        void Awake()
        {
            material = GetComponent<Renderer>().material;
        }

        void LateUpdate()
        {
            if (belt == null) return;

            float tileLength = belt.TextureTileLength;
            // Same repeat count on every face so lines line up across faces.
            material.mainTextureScale = new Vector2(belt.BeltLength / tileLength, tilingV);

            if (!scrollWithBelt) return;

            float offset = Mathf.Repeat(belt.TravelDistance / tileLength, 1f);
            material.mainTextureOffset = new Vector2(reverseScroll ? -offset : offset, 0f);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
