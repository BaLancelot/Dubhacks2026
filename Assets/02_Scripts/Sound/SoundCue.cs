using System.Collections.Generic;
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
        [Tooltip("Amplifies quiet clips past volume 1 by scaling their samples. 2 = twice as loud (+6 dB). Too high distorts.")]
        [SerializeField, Range(1f, 16f)] float gain = 1f;
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

        // Boosted copies of the clips, built on first play.
        readonly Dictionary<AudioClip, AudioClip> boosted = new();
        float boostedGain;

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            return gain > 1.001f && clip != null ? Boosted(clip) : clip;
        }

        void OnValidate()
        {
            boosted.Clear();
        }

        AudioClip Boosted(AudioClip clip)
        {
            if (!Mathf.Approximately(boostedGain, gain))
            {
                boosted.Clear();
                boostedGain = gain;
            }
            if (boosted.TryGetValue(clip, out AudioClip cached) && cached != null) return cached;

            // Needs the clip's samples in memory (Load Type: Decompress On Load, the default for short SFX).
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
            {
                Debug.LogWarning($"[{nameof(SoundCue)}] Can't boost {clip.name}: set its Load Type to Decompress On Load.", this);
                boosted[clip] = clip;
                return clip;
            }

            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            AudioClip louder = AudioClip.Create($"{clip.name} (x{gain:0.#})", clip.samples, clip.channels, clip.frequency, false);
            louder.SetData(samples, 0);
            boosted[clip] = louder;
            return louder;
        }

        public float PickPitch()
        {
            return Random.Range(pitchRange.x, pitchRange.y);
        }
    }
}
