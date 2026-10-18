using UnityEngine;

namespace SushiDojo
{
    /// <summary>Local presentation preferences, shared across scenes and saved between launches.</summary>
    public static class GameOptions
    {
        public static bool SfxEnabled { get; private set; }
        public static float SfxVolume { get; private set; }
        public static float MusicVolume { get; private set; }

        static GameOptions() => Load();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Load()
        {
            SfxEnabled = PlayerPrefs.GetInt("Options.SFX", 1) != 0;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Options.MusicVolume", 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Options.SfxVolume", 1f));
        }

        public static void SetSfx(bool value)
        {
            SfxEnabled = value;
            PlayerPrefs.SetInt("Options.SFX", value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Options.MusicVolume", MusicVolume);
            PlayerPrefs.Save();
        }

        public static void SetSfxVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Options.MusicVolume", 1f));
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Options.SfxVolume", SfxVolume);
            PlayerPrefs.Save();
        }
    }
}
