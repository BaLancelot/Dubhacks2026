using UnityEngine;

namespace SushiDojo.Sound
{
    /// <summary>Beat count from the playing BGM (falls back to game time when no BGM is playing).</summary>
    public static class BeatClock
    {
        public const float DefaultBpm = 136.4f;
        public const float DefaultFirstBeatTime = 0.21f;

        /// <summary>Fractional beats since the first beat. Floor it for the beat index.</summary>
        public static float Beats(float bpm = DefaultBpm, float firstBeatTime = DefaultFirstBeatTime)
        {
            float time = SoundManager.TryGetBgmTime(out float bgmTime) ? bgmTime : Time.time;
            return (time - firstBeatTime) * bpm / 60f;
        }
    }
}
