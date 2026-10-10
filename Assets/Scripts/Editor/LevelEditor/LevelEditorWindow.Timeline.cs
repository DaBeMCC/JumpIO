using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class LevelEditorWindow
{
    [SerializeField] double playhead;
    [SerializeField] float viewStart = -2f;
    [SerializeField] float viewLength = 20f;
    [SerializeField] bool followCamera = true;
    [SerializeField] bool showBeats = true;

    bool playing;
    double playStartReal, playStartTime;
    float[] envelope;
    bool timelineDirty = true;
    double nextSync;
    bool draggingPlayhead;
    float? spawnY;
    readonly List<(float time, Color color)> objectTimes = new();

    AudioClip Song => level != null ? level.song : null;
    double MinTime => level != null ? -level.songOffset : 0.0;
    double MaxTime => Song != null ? Song.length - level.songOffset : level != null ? level.TimeAtX(level.length) + 5f : 60.0;

    void OnUpdate()
    {
        if (!Ready)
        {
            if (playing) StopPreview();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        if (now >= nextSync)
        {
            nextSync = now + 0.5;
            SyncLevelData();
        }
        if (!playing) return;

        playhead = playStartTime + (now - playStartReal);
        double audio = LevelEditorAudio.Position(Song);
        if (!double.IsNaN(audio) && audio > 0.0)
        {
            double drift = audio - level.songOffset - playhead;
            if (System.Math.Abs(drift) > 0.06) { playStartReal -= drift; playhead += drift; }
        }
        if (playhead >= MaxTime) { StopPreview(); playhead = MaxTime; }
        if (playhead > viewStart + viewLength * 0.85f || playhead < viewStart) viewStart = (float)playhead - viewLength * 0.15f;
        if (followCamera) FollowPlayhead();
        Repaint();
        SceneView.RepaintAll();
    }

    void TogglePlay()
    {
        if (playing) StopPreview();
        else StartPreview();
    }

    void StartPreview()
    {
        if (Song == null) return;
        playing = true;
        playStartReal = EditorApplication.timeSinceStartup;
        playStartTime = playhead;
        LevelEditorAudio.Play(Song, level.songOffset + playhead);
    }

    void StopPreview()
    {
        if (!playing) return;
        playing = false;
        LevelEditorAudio.Stop();
        Repaint();
    }

    void Seek(double time)
    {
        playhead = System.Math.Clamp(time, MinTime, MaxTime);
        spawnY = null;
        if (playing) StartPreview();
        if (followCamera) FollowPlayhead();
        SceneView.RepaintAll();
        Repaint();
    }

    void FollowPlayhead()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        var pivot = view.pivot;
        view.pivot = new Vector3(PlayheadX, pivot.y, pivot.z);
    }

    float PlayheadX => level.XAtTime((float)playhead);

    double NearestBeatTime(double time)
    {
        if (level.bpm <= 0f) return time;
        double step = 60.0 / level.bpm / Division;
        double song = level.songOffset + time;
        double k = System.Math.Round((song - level.firstBeat) / step);
        return level.firstBeat + k * step - level.songOffset;
    }

    float NearestBeatX(float x) => level.XAtTime((float)NearestBeatTime(level.TimeAtX(x)));

    void RebuildObjectTimes()
    {
        timelineDirty = false;
        objectTimes.Clear();
        if (objects == null || level == null) return;
        foreach (Transform child in objects)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            var item = palette.Find(p => p.prefab != null && p.prefab == source);
            objectTimes.Add((level.TimeAtX(child.position.x), item?.timelineColor ?? Color.gray));
        }
    }

    void DrawTimeline(Rect area)
    {
        if (timelineDirty) RebuildObjectTimes();
        if (envelope == null && Song != null) envelope = LevelEditorAudio.Envelope(Song) ?? new float[0];

        EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.15f));
        var header = new Rect(area.x + 4f, area.y + 4f, area.width - 8f, 20f);
        GUILayout.BeginArea(header);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "❚❚" : "▶", EditorStyles.miniButton, GUILayout.Width(32f))) TogglePlay();
            if (GUILayout.Button("|◀", EditorStyles.miniButton, GUILayout.Width(28f))) Seek(0.0);
            if (GUILayout.Button("Marker", EditorStyles.miniButton, GUILayout.Width(50f))) AddMarker();
            float x = PlayheadX;
            int beat = level.BeatAt(playhead);
            GUILayout.Label($"{Clock(playhead)}  x {x:0.0}  beat {(beat == int.MinValue ? "-" : (beat / 4 + 1) + "." + (Mod(beat, 4) + 1))}",
                EditorStyles.whiteMiniLabel, GUILayout.Width(190f));
            GUILayout.FlexibleSpace();
            followCamera = GUILayout.Toggle(followCamera, "Follow", EditorStyles.miniButton, GUILayout.Width(48f));
            showBeats = GUILayout.Toggle(showBeats, "Beats", EditorStyles.miniButton, GUILayout.Width(44f));
            viewLength = GUILayout.HorizontalSlider(viewLength, 4f, 120f, GUILayout.Width(80f));
        }
        GUILayout.EndArea();
        if (Song == null)
            GUI.Label(new Rect(area.x + 8f, area.y + 26f, 300f, 16f), "Level Data has no song.", EditorStyles.whiteMiniLabel);

        var strip = new Rect(area.x + 4f, area.y + 28f, area.width - 8f, area.height - 34f);
        HandleTimelineInput(strip);
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(strip, new Color(0.08f, 0.08f, 0.1f));
        var wave = new Rect(strip.x, strip.y + 14f, strip.width, strip.height - 30f);
        DrawWaveform(wave);
        if (showBeats) DrawBeatLines(strip);

        DrawTimeLine(strip, 0f, new Color(0.3f, 1f, 0.4f), "Start");
        DrawTimeLine(strip, level.TimeAtX(level.length), Color.white, "Finish");
        for (int i = 1; i < level.changeTimes.Length && i < level.speeds.Length; i++)
            DrawTimeLine(strip, level.changeTimes[i], new Color(1f, 0.55f, 0.15f), $"{level.speeds[i]:0.#}");
        if (level.markers != null)
            foreach (float marker in level.markers) DrawTimeLine(strip, marker, new Color(1f, 0.9f, 0.2f, 0.9f), null);

        foreach (var (time, color) in objectTimes)
        {
            float px = PixelAt(strip, time);
            if (px < strip.x || px > strip.xMax) continue;
            EditorGUI.DrawRect(new Rect(px - 1f, strip.yMax - 12f, 3f, 10f), color);
        }

        float head = PixelAt(strip, (float)playhead);
        if (head >= strip.x && head <= strip.xMax)
        {
            EditorGUI.DrawRect(new Rect(head - 1f, strip.y, 2f, strip.height), new Color(1f, 0.25f, 0.25f));
            EditorGUI.DrawRect(new Rect(head - 5f, strip.y, 10f, 6f), new Color(1f, 0.25f, 0.25f));
        }
    }

    void DrawWaveform(Rect wave)
    {
        if (envelope == null || envelope.Length == 0 || Song == null) return;
        var color = new Color(0.35f, 0.62f, 0.95f, 0.85f);
        float mid = wave.center.y;
        for (int px = 0; px < (int)wave.width; px++)
        {
            double t0 = TimeAtPixel(wave, wave.x + px), t1 = TimeAtPixel(wave, wave.x + px + 1);
            int a = Index(t0), b = Mathf.Max(a + 1, Index(t1));
            if (a < 0 || a >= envelope.Length) continue;
            float amplitude = 0f;
            for (int i = a; i < b && i < envelope.Length; i++) amplitude = Mathf.Max(amplitude, envelope[i]);
            float h = Mathf.Max(1f, amplitude * wave.height * 0.5f);
            EditorGUI.DrawRect(new Rect(wave.x + px, mid - h, 1f, h * 2f), color);
        }
    }

    int Index(double levelTime) =>
        (int)((level.songOffset + levelTime) / Song.length * envelope.Length);

    void DrawBeatLines(Rect strip)
    {
        if (level.bpm <= 0f) return;
        double beat = 60.0 / level.bpm;
        double songStart = level.songOffset + viewStart;
        long first = (long)System.Math.Ceiling((songStart - level.firstBeat) / beat);
        float pixelsPerBeat = (float)(strip.width / viewLength * beat);
        if (pixelsPerBeat < 3f) return;
        for (long k = first; ; k++)
        {
            float t = (float)(level.firstBeat + k * beat - level.songOffset);
            if (t > viewStart + viewLength) break;
            bool bar = k % 4 == 0;
            float px = PixelAt(strip, t);
            EditorGUI.DrawRect(new Rect(px, strip.y, 1f, strip.height), new Color(1f, 1f, 1f, bar ? 0.28f : 0.09f));
            if (bar && pixelsPerBeat > 10f)
                GUI.Label(new Rect(px + 2f, strip.y, 40f, 14f), (k / 4 + 1).ToString(), EditorStyles.whiteMiniLabel);
        }
    }

    void DrawTimeLine(Rect strip, float time, Color color, string label)
    {
        float px = PixelAt(strip, time);
        if (px < strip.x || px > strip.xMax) return;
        EditorGUI.DrawRect(new Rect(px - 1f, strip.y, 2f, strip.height), color);
        if (label != null) GUI.Label(new Rect(px + 3f, strip.yMax - 28f, 60f, 14f), label, EditorStyles.whiteMiniLabel);
    }

    void HandleTimelineInput(Rect strip)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && strip.Contains(e.mousePosition))
        {
            draggingPlayhead = true;
            Seek(TimeAtPixel(strip, e.mousePosition.x));
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && draggingPlayhead)
        {
            playhead = System.Math.Clamp(TimeAtPixel(strip, e.mousePosition.x), MinTime, MaxTime);
            spawnY = null;
            if (followCamera) FollowPlayhead();
            SceneView.RepaintAll();
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && draggingPlayhead)
        {
            draggingPlayhead = false;
            if (playing) StartPreview();
            e.Use();
        }
        else if (e.type == EventType.ScrollWheel && strip.Contains(e.mousePosition))
        {
            if (e.control || e.command)
            {
                float anchor = (float)TimeAtPixel(strip, e.mousePosition.x);
                float factor = e.delta.y > 0f ? 1.15f : 1f / 1.15f;
                viewLength = Mathf.Clamp(viewLength * factor, 4f, 120f);
                viewStart = anchor - (e.mousePosition.x - strip.x) / strip.width * viewLength;
            }
            else viewStart += e.delta.y * viewLength * 0.03f;
            e.Use();
            Repaint();
        }
    }

    float PixelAt(Rect strip, float time) => strip.x + (time - viewStart) / viewLength * strip.width;

    double TimeAtPixel(Rect strip, float px) => viewStart + (px - strip.x) / strip.width * viewLength;

    static int Mod(int a, int b) => (a % b + b) % b;

    static string Clock(double t)
    {
        string sign = t < 0 ? "-" : "";
        t = System.Math.Abs(t);
        return $"{sign}{(int)(t / 60)}:{t % 60:00.00}";
    }
}
