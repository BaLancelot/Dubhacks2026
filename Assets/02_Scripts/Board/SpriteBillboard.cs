using UnityEngine;

namespace SushiDojo.Board
{
    /// <summary>
    /// Keeps a sprite facing the camera while following a physics body.
    /// The body's spin shows up as an on-screen roll so tumbling still reads.
    /// </summary>
    public class SpriteBillboard : MonoBehaviour
    {
        [Tooltip("Falls back to Camera.main.")]
        [SerializeField] Camera targetCamera;
        [Tooltip("Body to follow. Falls back to the parent.")]
        [SerializeField] Rigidbody body;
        [Tooltip("Lift along the camera's up so the sprite sits on the surface instead of sinking in.")]
        [SerializeField] float screenUpOffset = 0.25f;
        [Tooltip("Pull toward the camera so 3D surfaces don't cut into the sprite.")]
        [SerializeField] float towardCamera = 0.3f;
        [Tooltip("How much the body's spin rolls the sprite. 0 = never rolls.")]
        [SerializeField, Min(0f)] float rollFromSpin = 1f;

        float roll;

        public void ResetRoll()
        {
            roll = 0f;
        }

        void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Transform view = cam.transform;
            Transform anchor = body != null ? body.transform : transform.parent;
            if (anchor == null) return;

            if (body != null)
                roll += Vector3.Dot(body.angularVelocity, -view.forward) * Mathf.Rad2Deg * rollFromSpin * Time.deltaTime;

            Vector3 position = anchor.position + view.up * screenUpOffset - view.forward * towardCamera;
            transform.SetPositionAndRotation(position, view.rotation * Quaternion.Euler(0f, 0f, roll));
        }
    }
}
