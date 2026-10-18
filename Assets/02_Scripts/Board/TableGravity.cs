using UnityEngine;

namespace SushiDojo.Board
{
    /// <summary>Gravity along a (tilted) table frame's -Y instead of world down.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TableGravity : MonoBehaviour
    {
        [Tooltip("Gravity pulls along this frame's -Y. Falls back to the parent.")]
        [SerializeField] Transform tableFrame;
        [SerializeField, Min(0f)] float gravity = 25f;

        Rigidbody body;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
        }

        void FixedUpdate()
        {
            Transform frame = tableFrame != null ? tableFrame : transform.parent;
            Vector3 up = frame != null ? frame.up : Vector3.up;
            body.AddForce(-up * gravity, ForceMode.Acceleration);
        }
    }
}
