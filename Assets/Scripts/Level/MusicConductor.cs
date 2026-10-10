using System;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(AudioSource))]
public class MusicConductor : MonoBehaviour
{
    [FormerlySerializedAs("nivel")]
    [SerializeField] LevelData level;
    [FormerlySerializedAs("anticipacion")]
    [SerializeField] double scheduleLead = 0.1;
    [SerializeField] float snapThreshold = 0.03f;
    [FormerlySerializedAs("correccion")]
    [SerializeField, Range(0f, 1f)] float correctionRate = 0.005f;

    AudioSource source;
    double clock;
    double dspStart;
    bool running;
    double audioStep;
    double lastReported, lastReportedAt;
    double audioTime = double.NaN;
    double rate = 1;
    double rateAnchorAudio = double.NaN, rateAnchorTime;
    int progressPercent = -1;
    int beat = int.MinValue;

    public LevelData Level => level;
    public float LevelTime => (float)clock;
    public float PositionX => level.XAtTime(Mathf.Max(0f, (float)clock));
    public float CurrentSpeed => level.SpeedAtTime(Mathf.Max(0f, (float)clock));

    public double AudioTime => audioTime;

    bool IsMusicPlaying => source.clip != null && source.isPlaying && AudioSettings.dspTime >= dspStart;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        var config = AudioSettings.GetConfiguration();
        audioStep = config.dspBufferSize / (double)config.sampleRate;
    }

    void OnEnable()
    {
        GameEvents.AttemptStarted += Begin;
        GameEvents.PlayerDied += Stop;
        GameEvents.PauseChanged += SetPaused;
    }

    void OnDisable()
    {
        GameEvents.AttemptStarted -= Begin;
        GameEvents.PlayerDied -= Stop;
        GameEvents.PauseChanged -= SetPaused;
    }

    void Begin(int attempt)
    {
        double start = PlaytestStart.Active ? PlaytestStart.Time : 0.0;
        clock = start - scheduleLead;
        running = true;
        lastReported = -1;
        audioTime = double.NaN;
        rateAnchorAudio = double.NaN;
        progressPercent = -1;
        beat = int.MinValue;
        PublishProgress();
        if (source.clip == null) return;

        source.Stop();
        source.time = level.songOffset + (float)start;
        dspStart = AudioSettings.dspTime + scheduleLead;
        source.PlayScheduled(dspStart);
    }

    void Stop()
    {
        running = false;
        audioTime = double.NaN;
        source.Stop();
    }

    public void SetPaused(bool paused)
    {
        if (paused) source.Pause();
        else source.UnPause();
    }

    void FixedUpdate()
    {
        if (!running) return;
        clock += Time.fixedDeltaTime * rate;
        PublishProgress();

        audioTime = IsMusicPlaying ? EstimateSongTime() - level.songOffset - level.latencyMs / 1000.0 : double.NaN;
        if (!double.IsNaN(audioTime))
        {
            MeasureRate();
            double drift = audioTime - clock;
            if (Math.Abs(drift) > snapThreshold)
            {
                clock = audioTime;
                rateAnchorAudio = double.NaN;
            }
            else clock += drift * correctionRate;
        }
        PublishBeat();
    }

    void PublishBeat()
    {
        if (clock < 0.0) return;
        int current = level.BeatAt(clock);
        if (current == beat || current < 0) return;
        beat = current;
        GameEvents.RaiseBeat(beat);
    }

    void PublishProgress()
    {
        if (level == null || level.length <= 0f) return;
        float progress = Mathf.Clamp01(PositionX / level.length);
        int percent = Mathf.FloorToInt(progress * 100f);
        if (percent == progressPercent) return;
        progressPercent = percent;
        GameEvents.RaiseProgressChanged(progress);
    }

    void MeasureRate()
    {
        double now = Time.fixedTimeAsDouble;
        if (double.IsNaN(rateAnchorAudio)) { rateAnchorAudio = audioTime; rateAnchorTime = now; }
        else if (now - rateAnchorTime >= 1.0) rate = Math.Clamp((audioTime - rateAnchorAudio) / (now - rateAnchorTime), 0.9, 1.1);
    }

    double EstimateSongTime()
    {
        double reported = (double)source.timeSamples / source.clip.frequency;
        double now = Time.fixedTimeAsDouble;
        if (reported != lastReported) { lastReported = reported; lastReportedAt = now; }
        return reported + Math.Min(now - lastReportedAt, audioStep);
    }
}
