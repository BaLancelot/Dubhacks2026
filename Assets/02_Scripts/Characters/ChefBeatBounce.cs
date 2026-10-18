using SushiDojo.Sound;
using UnityEngine;

namespace SushiDojo.Characters
{
    /// <summary>
    /// Puts the chef at the player's seat and jiggles it like jelly on every BGM beat.
    /// The chef stays in place; ChefImpactJelly does the squash/stretch and hat wobble.
    /// </summary>
    [RequireComponent(typeof(ChefImpactJelly))]
    public class ChefBeatBounce : MonoBehaviour
    {
        [Header("Seat")]
        [Tooltip("Where the chef stands. Empty = find an object named Seat Name.")]
        [SerializeField] Transform seat;
        [SerializeField] string seatName = "tempChar";
        [SerializeField] Vector3 seatOffset;
        [Tooltip("Hide the seat's placeholder sprite once the chef takes its place.")]
        [SerializeField] bool hideSeatSprite = true;

        [Header("Beat")]
        [SerializeField, Min(1f)] float bpm = 136.4f;
        [Tooltip("BGM clip time (seconds) of the first beat. Nudge this if the jiggle feels early/late.")]
        [SerializeField] float firstBeatTime = 0.21f;
        [Tooltip("1 = jiggle every beat, 2 = every other beat.")]
        [SerializeField, Min(1)] int beatsPerJiggle = 1;

        [Header("Jiggle")]
        [Tooltip("How hard each beat squashes the chef.")]
        [SerializeField, Min(0f)] float strength = 4f;
        [Tooltip("Sideways tilt per beat, alternating left/right. 0 = straight up and down.")]
        [SerializeField, Range(0f, 1f)] float sway = 0.35f;

        ChefImpactJelly jelly;
        int lastBeat = int.MinValue;

        void Awake()
        {
            jelly = GetComponent<ChefImpactJelly>();
            // Stay put on the seat; physics would otherwise drop the chef.
            if (TryGetComponent(out Rigidbody body)) body.isKinematic = true;
        }

        void Start()
        {
            if (seat == null && !string.IsNullOrEmpty(seatName))
            {
                GameObject found = GameObject.Find(seatName);
                if (found != null) seat = found.transform;
            }

            if (seat != null)
            {
                transform.position = seat.position + seatOffset;
                if (hideSeatSprite && seat.TryGetComponent(out SpriteRenderer sprite)) sprite.enabled = false;
            }
        }

        void Update()
        {
            float time = SoundManager.TryGetBgmTime(out float bgmTime) ? bgmTime : Time.time;
            int beat = Mathf.FloorToInt((time - firstBeatTime) * bpm / 60f / beatsPerJiggle);
            if (beat == lastBeat) return;

            bool first = lastBeat == int.MinValue;
            lastBeat = beat;
            if (first) return;

            float side = (beat & 1) == 0 ? sway : -sway;
            Vector3 normal = transform.up + transform.right * side;
            jelly.Impact(normal, strength);
        }
    }
}
