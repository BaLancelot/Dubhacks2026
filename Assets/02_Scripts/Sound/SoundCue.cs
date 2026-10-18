using UnityEngine;

namespace SushiDojo.Sound
{
    /// <summary>One sound: a set of clips (random pick), its volume and pitch variation.</summary>
    [CreateAssetMenu(fileName = "Cue", menuName = "Sushi Dojo/Sound Cue")]
    public class SoundCue : ScriptableObject
    {
        [Tooltip("One is picked at random each play.")]
        [SerializeField] AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [Tooltip("Random pitch between X and Y. (1, 1) = no variation.")]
        [SerializeField] Vector2 pitchRange = Vector2.one;
        [Tooltip("Plays within this many seconds of the last one are skipped (avoids stacking).")]
        [SerializeField, Min(0f)] float cooldown = 0.05f;
        [Tooltip("Skip this many seconds at the start of the clip (e.g. leading silence).")]
        [SerializeField, Min(0f)] float startTime;
        [Tooltip("BGM only: loop back this many seconds before the clip ends (e.g. trailing silence).")]
        [SerializeField, Min(0f)] float endTrim;

        public float Volume => volume;
        public float Cooldown => cooldown;
        public float StartTime => startTime;
        public float EndTrim => endTrim;

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        public float PickPitch()
        {
            return Random.Range(pitchRange.x, pitchRange.y);
        }
    }
}
