using UnityEngine;

namespace SushiDojo.Sound
{
    /// <summary>Plays a cue when this rigidbody hits something hard enough. Louder for harder hits.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ImpactSound : MonoBehaviour
    {
        [SerializeField] SoundCue cue;
        [Tooltip("Hits slower than this (units/s) are silent.")]
        [SerializeField, Min(0f)] float minImpactSpeed = 2f;
        [Tooltip("Hits at or above this speed play at full volume.")]
        [SerializeField, Min(0.01f)] float fullVolumeSpeed = 12f;
        [Tooltip("Volume for the softest audible hit (fraction of the cue's volume).")]
        [SerializeField, Range(0f, 1f)] float minVolume = 0.3f;
        [Tooltip("Only hits against these colliders make sound. Empty = anything.")]
        [SerializeField] Collider[] onlyAgainst;

        void OnCollisionEnter(Collision collision)
        {
            if (!IsTarget(collision.collider)) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            float strength = Mathf.InverseLerp(minImpactSpeed, Mathf.Max(fullVolumeSpeed, minImpactSpeed), speed);
            SoundManager.Play(cue, Mathf.Lerp(minVolume, 1f, strength));
        }

        bool IsTarget(Collider other)
        {
            if (onlyAgainst == null || onlyAgainst.Length == 0) return true;
            foreach (Collider target in onlyAgainst)
                if (target == other) return true;
            return false;
        }
    }
}
