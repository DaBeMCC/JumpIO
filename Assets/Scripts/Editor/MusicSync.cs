using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MusicSync
{
    const int Hop = 512;
    const float AnalysedSeconds = 90f;

    [MenuItem("JumpIO/Setup/Music Sync")]
    public static void Configure()
    {
        string report = "";
        foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (level.song == null) continue;
            var (bpm, phase, drop) = Analyse(level.song);
            level.bpm = bpm;
            level.firstBeat = phase;
            float climax = level.changeX.Length > 1
                ? level.TimeAtX(level.changeX[level.changeX.Length - 1])
                : level.TimeAtX(level.length) * 0.8f;
            level.songOffset = Mathf.Max(0f, drop - climax);
            EditorUtility.SetDirty(level);
            report += $"{level.levelName}: bpm={bpm:0.00} firstBeat={phase:0.000}s drop={drop:0.000}s " +
                      $"climax={climax:0.00}s songOffset={level.songOffset:0.000}s; ";
        }
        AddPulses();
        AssetDatabase.SaveAssets();
        Debug.Log("JUMPIO_MUSIC_SYNC_OK " + report);
    }

    static (float bpm, float phase, float drop) Analyse(AudioClip clip)
    {
        float[] onset = Onsets(clip, out float hopTime, out float[] loudness);

        var smooth = new float[onset.Length];
        for (int i = 1; i < onset.Length - 1; i++) smooth[i] = 0.25f * onset[i - 1] + 0.5f * onset[i] + 0.25f * onset[i + 1];

        float bestScore = float.MinValue, bestBpm = 120f, bestPhase = 0f;
        for (float bpm = 70f; bpm <= 190f; bpm += 0.05f)
        {
            float period = 60f / bpm / hopTime;
            float octave = Mathf.Log(bpm / 125f, 2f);
            float prior = Mathf.Exp(-0.5f * octave * octave / 0.36f);
            for (float phase = 0f; phase < period; phase += 1f)
            {
                float sum = 0f;
                int count = 0;
                for (float k = phase; k < smooth.Length; k += period)
                {
                    sum += smooth[(int)Math.Round(k) < smooth.Length ? (int)Math.Round(k) : smooth.Length - 1];
                    count++;
                }
                float score = sum / Mathf.Max(1, count) * prior;
                if (score <= bestScore) continue;
                bestScore = score;
                bestBpm = bpm;
                bestPhase = phase;
            }
        }

        float bpmRounded = Mathf.Round(bestBpm * 100f) / 100f;
        float beat = 60f / bpmRounded;
        float firstBeat = bestPhase * hopTime;
        float drop = Drop(loudness, hopTime);
        float bars = Mathf.Round((drop - firstBeat) / (4f * beat));
        return (bpmRounded, firstBeat, firstBeat + bars * 4f * beat);
    }

    static float Drop(float[] loudness, float hopTime)
    {
        int window = Mathf.RoundToInt(2f / hopTime);
        var prefix = new double[loudness.Length + 1];
        for (int i = 0; i < loudness.Length; i++) prefix[i + 1] = prefix[i] + loudness[i];
        double best = 0;
        int bestIndex = 0;
        for (int h = window; h + window < loudness.Length; h++)
        {
            double before = (prefix[h] - prefix[h - window]) / window;
            double after = (prefix[h + window] - prefix[h]) / window;
            double ratio = after / (before + 1e-6);
            if (ratio <= best) continue;
            best = ratio;
            bestIndex = h;
        }
        return bestIndex * hopTime;
    }

    static float[] Onsets(AudioClip clip, out float hopTime, out float[] loudness)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var original = importer.defaultSampleSettings;
        var decoded = original;
        decoded.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = decoded;
        importer.SaveAndReimport();
        try
        {
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            int channels = clip.channels;
            int frames = Mathf.Min(clip.samples, Mathf.RoundToInt(AnalysedSeconds * clip.frequency));
            var data = new float[frames * channels];
            if (!clip.GetData(data, 0)) throw new InvalidOperationException("Could not read samples of " + path);
            hopTime = Hop / (float)clip.frequency;

            int hops = frames / Hop;
            var energy = new float[hops];
            loudness = new float[hops];
            float previous = 0f;
            for (int h = 0; h < hops; h++)
            {
                float e = 0f, power = 0f;
                for (int n = h * Hop; n < (h + 1) * Hop; n++)
                {
                    float mono = 0f;
                    for (int c = 0; c < channels; c++) mono += data[n * channels + c];
                    mono /= channels;
                    float diff = mono - previous;
                    previous = mono;
                    e += diff * diff;
                    power += mono * mono;
                }
                loudness[h] = power / Hop;
                energy[h] = Mathf.Log(1f + 1000f * e);
            }
            var onset = new float[hops];
            for (int h = 1; h < hops; h++) onset[h] = Mathf.Max(0f, energy[h] - energy[h - 1]);
            return onset;
        }
        finally
        {
            importer.defaultSampleSettings = original;
            importer.SaveAndReimport();
        }
    }

    static void AddPulses()
    {
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Objects" })
            .Select(AssetDatabase.GUIDToAssetPath).Append("Assets/Prefabs/FinishLine.prefab");
        foreach (string path in prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var sprite = root.GetComponentInChildren<SpriteRenderer>(true);
            if (sprite != null && sprite.GetComponent<BeatPulse>() == null)
            {
                var pulse = sprite.gameObject.AddComponent<BeatPulse>();
                if (root.GetComponent<JumpOrb>() == null && root.GetComponent<JumpPad>() == null && root.GetComponent<Coin>() == null)
                {
                    var serialized = new SerializedObject(pulse);
                    serialized.FindProperty("strength").floatValue = 0.35f;
                    serialized.FindProperty("every").intValue = 2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
