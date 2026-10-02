using UnityEngine;

public enum AudioChannel { Master, Music, Sfx }

public static class AudioPreferences
{
    const string Prefix = "JumpIO.Audio.";
    const string JumpSoundKey = Prefix + "JumpSound";
    public const float MinDecibels = -80f;

    public static float GetVolume(AudioChannel channel) => PlayerPrefs.GetFloat(Prefix + channel, Default(channel));

    public static void SetVolume(AudioChannel channel, float linear)
    {
        PlayerPrefs.SetFloat(Prefix + channel, Mathf.Clamp01(linear));
        PlayerPrefs.Save();
    }

    public static bool JumpSound
    {
        get => PlayerPrefs.GetInt(JumpSoundKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(JumpSoundKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static float ToDecibels(float linear) => linear <= 0.0001f ? MinDecibels : 20f * Mathf.Log10(linear);

    static float Default(AudioChannel channel) => channel switch
    {
        AudioChannel.Music => 0.8f,
        AudioChannel.Sfx => 0.9f,
        _ => 1f,
    };
}
