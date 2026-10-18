using UnityEngine;

[ExecuteAlways]
public class BeltSortingFix : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 5;

    private void OnEnable()
    {
        ApplySorting();
    }

    private void OnValidate()
    {
        ApplySorting();
    }

    private void ApplySorting()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
    }
}