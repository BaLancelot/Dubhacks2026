using System;
using UnityEngine;

namespace SushiDojo.Board
{
    /// <summary>
    /// Rigidbody that can be held and thrown. Gravity pulls along the table frame's -Y,
    /// so a tilted table (for the camera view) still behaves like a flat table.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableBody : MonoBehaviour
    {
        const float HalfTurnDegrees = 180f;
        const float FullTurnDegrees = 360f;
        const float MinSqrSpeed = 1e-4f;

        [Tooltip("Gravity and 'upright' follow this frame. Falls back to the parent.")]
        [SerializeField] Transform tableFrame;
        [SerializeField, Min(0f)] float gravity = 25f;

        [Header("Holding")]
        [Tooltip("How quickly the body follows the cursor while held (1/s).")]
        [SerializeField, Min(0f)] float followSharpness = 20f;
        [Tooltip("How quickly it levels out while held (1/s).")]
        [SerializeField, Min(0f)] float uprightSharpness = 10f;

        [Header("Throw")]
        [SerializeField, Min(0f)] float throwVelocityScale = 1f;
        [SerializeField, Min(0f)] float maxThrowSpeed = 20f;
        [Tooltip("Extra speed along the table normal on release.")]
        [SerializeField, Min(0f)] float releaseLift = 4f;
        [Tooltip("Flip spin (rad/s) per unit of throw speed.")]
        [SerializeField, Min(0f)] float flipSpinPerSpeed = 1.2f;
        [SerializeField, Min(0f)] float maxFlipSpin = 25f;

        public event Action Grabbed;
        public event Action Released;

        public bool IsHeld { get; private set; }
        /// <summary>Set false to block picking up (e.g. while fading out).</summary>
        public bool IsGrabbable { get; set; } = true;

        Rigidbody body;
        Quaternion uprightLocalRotation;
        Vector3 holdTarget;

        Transform Frame => tableFrame != null ? tableFrame : transform.parent;
        Vector3 Up => Frame != null ? Frame.up : Vector3.up;
        Quaternion FrameRotation => Frame != null ? Frame.rotation : Quaternion.identity;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.maxAngularVelocity = maxFlipSpin;
            uprightLocalRotation = Quaternion.Inverse(FrameRotation) * transform.rotation;
        }

        void FixedUpdate()
        {
            if (IsHeld) FollowHoldTarget();
            else body.AddForce(-Up * gravity, ForceMode.Acceleration);
        }

        public void Grab(Vector3 target)
        {
            IsHeld = true;
            holdTarget = target;
            Grabbed?.Invoke();
        }

        public void MoveHeld(Vector3 target)
        {
            holdTarget = target;
        }

        public void Release(Vector3 throwVelocity)
        {
            IsHeld = false;

            Vector3 velocity = Vector3.ClampMagnitude(throwVelocity * throwVelocityScale, maxThrowSpeed);
            body.linearVelocity = velocity + Up * releaseLift;

            // Spin end over end in the throw direction.
            Vector3 planar = Vector3.ProjectOnPlane(velocity, Up);
            Vector3 fallbackAxis = Frame != null ? Frame.right : Vector3.right;
            Vector3 spinAxis = planar.sqrMagnitude > MinSqrSpeed ? Vector3.Cross(Up, planar).normalized : fallbackAxis;
            body.angularVelocity = spinAxis * Mathf.Min(planar.magnitude * flipSpinPerSpeed, maxFlipSpin);
            Released?.Invoke();
        }

        void FollowHoldTarget()
        {
            // Velocity-driven so it still collides with walls while held.
            body.linearVelocity = (holdTarget - body.position) * followSharpness;

            Quaternion upright = FrameRotation * uprightLocalRotation;
            (upright * Quaternion.Inverse(body.rotation)).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > HalfTurnDegrees) angle -= FullTurnDegrees;
            body.angularVelocity = float.IsFinite(axis.x)
                ? axis * (angle * Mathf.Deg2Rad * uprightSharpness)
                : Vector3.zero;
        }
    }
}
