using SushiDojo.Board;
using UnityEngine;

namespace SushiDojo.Sound
{
    /// <summary>
    /// Plays a cue each time a corner of this box touches down on the floor.
    /// Volume follows how high that corner fell (from its downward speed at contact).
    /// Corners hitting at the same moment merge into one sound through the cue's cooldown.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class CornerLandingSound : MonoBehaviour
    {
        const int CornerCount = 8;

        [SerializeField] SoundCue cue;
        [Tooltip("The floor collider. Its top face is the landing surface.")]
        [SerializeField] BoxCollider floor;

        [Header("Contact")]
        [Tooltip("A corner closer than this to the floor counts as touching.")]
        [SerializeField, Min(0f)] float contactDistance = 0.03f;
        [Tooltip("A corner must rise above this before it can land again (stops jitter).")]
        [SerializeField, Min(0f)] float liftDistance = 0.1f;

        [Header("Volume by Fall Height")]
        [Tooltip("Gravity used to turn landing speed into fall height. Match the board's gravity.")]
        [SerializeField, Min(0.01f)] float gravity = 25f;
        [Tooltip("Falls from this height or more play at full volume (world units).")]
        [SerializeField, Min(0.01f)] float fullVolumeHeight = 1.5f;
        [Tooltip("Volume for the tiniest drop (fraction of the cue's volume).")]
        [SerializeField, Range(0f, 1f)] float minVolume = 0.15f;

        Rigidbody body;
        BoxCollider box;
        readonly Vector3[] localCorners = new Vector3[CornerCount];
        readonly bool[] isAirborne = new bool[CornerCount];

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
        }

        void FixedUpdate()
        {
            if (floor == null) return;

            CacheCorners();
            Vector3 up = floor.transform.up;
            Vector3 surface = floor.transform.TransformPoint(floor.center + Vector3.up * floor.size.y * 0.5f);

            for (int i = 0; i < CornerCount; i++)
            {
                Vector3 corner = transform.TransformPoint(localCorners[i]);
                float distance = Vector3.Dot(corner - surface, up);

                // Teleports (respawn) and holding don't count as landings.
                if (body.isKinematic)
                {
                    isAirborne[i] = distance > liftDistance;
                    continue;
                }

                if (distance > liftDistance)
                {
                    isAirborne[i] = true;
                    continue;
                }

                // Look one physics step ahead so the sound starts on the frame of impact, not after it.
                float downSpeed = Mathf.Max(-Vector3.Dot(body.GetPointVelocity(corner), up), 0f);
                if (isAirborne[i] && distance - downSpeed * Time.fixedDeltaTime <= contactDistance)
                {
                    isAirborne[i] = false;
                    PlayLanding(downSpeed);
                }
            }
        }

        void PlayLanding(float downSpeed)
        {
            float fallHeight = downSpeed * downSpeed / (2f * gravity);
            float strength = Mathf.Clamp01(fallHeight / fullVolumeHeight);
            SoundManager.Play(cue, Mathf.Lerp(minVolume, 1f, strength));
        }

        void CacheCorners()
        {
            Vector3 half = box.size * 0.5f;
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        localCorners[index++] = box.center + Vector3.Scale(half, new Vector3(x, y, z));
        }
    }
}
