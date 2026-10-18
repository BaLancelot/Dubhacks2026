using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiDojo.Board
{
    /// <summary>
    /// Click a ThrowableBody to pick it up, drag to carry it over the table, release to throw.
    /// Throw velocity comes from the recent cursor movement on the hold plane.
    /// </summary>
    public class MouseThrower : MonoBehaviour
    {
        [Tooltip("Falls back to Camera.main.")]
        [SerializeField] Camera targetCamera;
        [Tooltip("The tilted table frame. Held bodies move on a plane above it. Falls back to this transform.")]
        [SerializeField] Transform table;
        [Tooltip("Height of the carry plane above the table origin (table local Y).")]
        [SerializeField] float holdHeight = 1f;
        [SerializeField] LayerMask grabMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] float maxGrabDistance = 100f;
        [Tooltip("Cursor history used to measure throw speed, in seconds.")]
        [SerializeField, Min(0.01f)] float velocityWindow = 0.08f;

        readonly Queue<(float time, Vector3 point)> samples = new();
        ThrowableBody held;
        Vector3 grabOffset;
        Vector3 lastPlanePoint;

        Camera Cam => targetCamera != null ? targetCamera : Camera.main;
        Transform Table => table != null ? table : transform;

        void Update()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Cam;
            if (mouse == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (held == null)
            {
                if (mouse.leftButton.wasPressedThisFrame) TryGrab(ray);
                return;
            }

            Vector3 target = PointOnHoldPlane(ray) + grabOffset;
            held.MoveHeld(target);
            RecordSample(target);

            if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
            {
                held.Release(EstimateVelocity());
                held = null;
            }
        }

        void TryGrab(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, maxGrabDistance, grabMask)) return;
            if (hit.rigidbody == null || !hit.rigidbody.TryGetComponent(out ThrowableBody body)) return;
            if (!body.IsGrabbable) return;

            Vector3 planePoint = PointOnHoldPlane(ray);
            grabOffset = Vector3.ProjectOnPlane(body.transform.position - planePoint, Table.up);

            held = body;
            samples.Clear();
            held.Grab(planePoint + grabOffset);
            RecordSample(planePoint + grabOffset);
        }

        Vector3 PointOnHoldPlane(Ray ray)
        {
            var plane = new Plane(Table.up, Table.position + Table.up * holdHeight);
            if (plane.Raycast(ray, out float enter)) lastPlanePoint = ray.GetPoint(enter);
            return lastPlanePoint;
        }

        void RecordSample(Vector3 point)
        {
            float now = Time.time;
            samples.Enqueue((now, point));
            while (samples.Count > 2 && now - samples.Peek().time > velocityWindow)
                samples.Dequeue();
        }

        Vector3 EstimateVelocity()
        {
            if (samples.Count < 2) return Vector3.zero;

            (float time, Vector3 point) oldest = samples.Peek();
            (float time, Vector3 point) newest = default;
            foreach ((float time, Vector3 point) sample in samples) newest = sample;

            float elapsed = newest.time - oldest.time;
            return elapsed > 0f ? (newest.point - oldest.point) / elapsed : Vector3.zero;
        }
    }
}
