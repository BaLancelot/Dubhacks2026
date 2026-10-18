using System.Collections.Generic;
using UnityEngine;

namespace SushiDojo.Sound
{
    /// <summary>
    /// Plays SoundCues. Keep one in the scene and call SoundManager.Play(cue) from anywhere.
    /// SFX use a pool of voices so overlapping sounds don't cut each other off.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] float bgmVolume = 0.7f;

        [Header("Voices")]
        [Tooltip("How many sound effects can play at once.")]
        [SerializeField, Min(1)] int sfxVoices = 16;
        [SerializeField, Min(0f)] float bgmFadeDuration = 0.5f;

        [Header("BGM")]
        [Tooltip("Starts looping when the scene starts. Empty = no music.")]
        [SerializeField] SoundCue startBgm;

        public static SoundManager Instance { get; private set; }

        readonly Dictionary<SoundCue, float> lastPlayTimes = new();
        AudioSource[] sfxSources;
        AudioSource bgmSource;
        float[] sfxBaseVolumes;
        SoundCue currentBgm;
        int nextVoice;
        float bgmFadeTimer;
        float bgmStartVolume;
        float bgmTargetVolume;
        float bgmPlaybackVolume;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{nameof(SoundManager)}] More than one in the scene. Disabling this one.", this);
                enabled = false;
                return;
            }
            Instance = this;

            sfxSources = new AudioSource[sfxVoices];
            sfxBaseVolumes = new float[sfxVoices];
            for (int i = 0; i < sfxVoices; i++) sfxSources[i] = CreateSource(false);
            bgmSource = CreateSource(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (startBgm != null) StartBgm(startBgm);
        }

        void Update()
        {
            for (int i = 0; i < sfxSources.Length; i++)
            {
                sfxSources[i].mute = !GameOptions.SfxEnabled;
                sfxSources[i].volume = sfxBaseVolumes[i] * GameOptions.SfxVolume;
            }
            LoopBgmPastTrim();

            // Apply the preference after the fade, so changing it also affects
            // music already playing without stopping its beat clock.
            bgmSource.volume = bgmPlaybackVolume * GameOptions.MusicVolume;
            if (bgmFadeTimer <= 0f) return;

            bgmFadeTimer = Mathf.Max(bgmFadeTimer - Time.unscaledDeltaTime, 0f);
            float progress = bgmFadeDuration > 0f ? 1f - bgmFadeTimer / bgmFadeDuration : 1f;
            bgmPlaybackVolume = Mathf.Lerp(bgmStartVolume, bgmTargetVolume, progress);
            bgmSource.volume = bgmPlaybackVolume * GameOptions.MusicVolume;
            if (bgmFadeTimer <= 0f && bgmTargetVolume <= 0f) bgmSource.Stop();
        }

        /// <summary>Skips the cue's trailing and leading silence so the loop has no gap.</summary>
        void LoopBgmPastTrim()
        {
            if (currentBgm == null || !bgmSource.isPlaying || bgmSource.clip == null) return;

            float loopEnd = bgmSource.clip.length - currentBgm.EndTrim;
            if (bgmSource.time >= loopEnd) bgmSource.time = currentBgm.StartTime;
        }

        /// <summary>Plays a sound effect. volumeScale multiplies the cue's volume (e.g. impact strength).</summary>
        public static void Play(SoundCue cue, float volumeScale = 1f)
        {
            if (cue == null) return;
            if (Instance == null)
            {
                Debug.LogWarning($"[{nameof(SoundManager)}] None in the scene, can't play {cue.name}.");
                return;
            }
            Instance.PlaySfx(cue, volumeScale);
        }

        public static void PlayBgm(SoundCue cue)
        {
            if (Instance != null) Instance.StartBgm(cue);
        }

        /// <summary>Playback position of the current BGM clip in seconds. Use it to sync things to the music.</summary>
        public static bool TryGetBgmTime(out float time)
        {
            time = 0f;
            if (Instance == null || Instance.bgmSource == null || !Instance.bgmSource.isPlaying) return false;
            time = Instance.bgmSource.time;
            return true;
        }

        public static void StopBgm()
        {
            if (Instance != null) Instance.FadeBgmTo(0f);
        }

        void PlaySfx(SoundCue cue, float volumeScale)
        {
            if (!GameOptions.SfxEnabled || GameOptions.SfxVolume <= 0f) return;
            float now = Time.unscaledTime;
            if (lastPlayTimes.TryGetValue(cue, out float last) && now - last < cue.Cooldown) return;

            AudioClip clip = cue.PickClip();
            if (clip == null) return;
            lastPlayTimes[cue] = now;

            AudioSource source = NextVoice();
            source.clip = clip;
            float baseVolume = cue.Volume * Mathf.Clamp01(volumeScale) * sfxVolume * masterVolume;
            sfxBaseVolumes[System.Array.IndexOf(sfxSources, source)] = baseVolume;
            source.mute = false;
            source.volume = baseVolume * GameOptions.SfxVolume;
            source.pitch = cue.PickPitch();
            source.time = Mathf.Clamp(cue.StartTime, 0f, Mathf.Max(clip.length - Time.fixedDeltaTime, 0f));
            source.Play();
        }

        void StartBgm(SoundCue cue)
        {
            if (cue == null || cue == currentBgm) return;

            AudioClip clip = cue.PickClip();
            if (clip == null) return;

            currentBgm = cue;
            bgmSource.clip = clip;
            bgmSource.pitch = 1f;
            bgmPlaybackVolume = 0f;
            bgmSource.volume = 0f;
            bgmSource.time = Mathf.Clamp(cue.StartTime, 0f, clip.length);
            bgmSource.Play();
            FadeBgmTo(cue.Volume * bgmVolume * masterVolume);
        }

        void FadeBgmTo(float volume)
        {
            if (volume <= 0f) currentBgm = null;
            bgmStartVolume = bgmPlaybackVolume;
            bgmTargetVolume = volume;
            bgmFadeTimer = Mathf.Max(bgmFadeDuration, Mathf.Epsilon);
        }

        AudioSource NextVoice()
        {
            // Prefer an idle voice; otherwise steal the next one in rotation.
            for (int i = 0; i < sfxSources.Length; i++)
            {
                int index = (nextVoice + i) % sfxSources.Length;
                if (!sfxSources[index].isPlaying)
                {
                    nextVoice = (index + 1) % sfxSources.Length;
                    return sfxSources[index];
                }
            }

            AudioSource stolen = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;
            return stolen;
        }

        AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
