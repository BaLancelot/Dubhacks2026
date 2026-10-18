using UnityEngine;

namespace SushiDojo.Effects
{
    /// <summary>
    /// Sets sorting layer/order on non-sprite renderers (e.g. MeshRenderer) in this object and its children,
    /// so 3D props sort with 2D sprites from the inspector.
    /// </summary>
    [ExecuteAlways]
    public class RendererSorting : MonoBehaviour
    {
        [SerializeField] string sortingLayerName = "Default";
        [SerializeField] int sortingOrder;
        [SerializeField] bool includeChildren = true;

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        void Apply()
        {
            Renderer[] targets = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (Renderer target in targets)
            {
                target.sortingLayerName = sortingLayerName;
                target.sortingOrder = sortingOrder;
            }
        }
    }
}
