using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LevelEditorAudio
{
    static readonly Type Util = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    static readonly MethodInfo PlayMethod = Method("PlayPreviewClip");
    static readonly MethodInfo StopMethod = Method("StopAllPreviewClips");
    static readonly MethodInfo PlayingMethod = Method("IsPreviewClipPlaying");
    static readonly MethodInfo SampleMethod = Method("GetPreviewClipSamplePosition");
    static readonly MethodInfo MinMaxMethod = Method("GetMinMaxData");

    static MethodInfo Method(string name) =>
        Util?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    public static bool Available => PlayMethod != null && StopMethod != null;

    public static void Play(AudioClip clip, double seconds)
    {
        Stop();
        if (clip == null || PlayMethod == null) return;
        int sample = Mathf.Clamp((int)(seconds * clip.frequency), 0, clip.samples - 1);
        PlayMethod.Invoke(null, new object[] { clip, sample, false });
    }

    public static void Stop() => StopMethod?.Invoke(null, null);

    public static bool IsPlaying => PlayingMethod != null && (bool)PlayingMethod.Invoke(null, null);

    public static double Position(AudioClip clip) =>
        SampleMethod == null || clip == null ? double.NaN : (int)SampleMethod.Invoke(null, null) / (double)clip.frequency;

    public static float[] Envelope(AudioClip clip)
    {
        if (clip == null || MinMaxMethod == null) return null;
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is not AudioImporter importer) return null;
        if (MinMaxMethod.Invoke(null, new object[] { importer }) is not float[] data || data.Length == 0) return null;
        int channels = Mathf.Max(1, clip.channels);
        int count = data.Length / (2 * channels);
        var envelope = new float[count];
        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
        {
            float amplitude = 0f;
            for (int c = 0; c < channels; c++)
            {
                int index = 2 * (i * channels + c);
                amplitude = Mathf.Max(amplitude, Mathf.Abs(data[index] - data[index + 1]) * 0.5f);
            }
            envelope[i] = amplitude;
            peak = Mathf.Max(peak, amplitude);
        }
        for (int i = 0; i < count; i++) envelope[i] /= peak;
        return envelope;
    }
}
